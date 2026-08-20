using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BrainCloud;
using BrainCloud.JsonFx.Json;

namespace RelayTestApp
{
    class GameApp
    {
        // Public: mirrored for display purposes by MainWindow (game timer / rematch
        // countdown text) rather than duplicating these as separate magic numbers there.
        public const int MATCH_DURATION_SEC = 90;
        public const int RESULT_GRACE_SEC = 3; // gap between the host's match_result broadcast and the actual EndMatch() call
        public const long MatchSummaryRematchMs = 45000; // matches the cpp/Java reference constant (a nearby cpp comment says 15s — the real constant is 45s)
        public const long LeaderboardTimeoutMs = 8000;

        const long CoverageRecomputeMs = 250;
        const long ChatChannelRetryMs = 5000;
        const long ResultsPollIntervalMs = 1000;
        const int MaxRelayBytes = 900;

        BrainCloudWrapper m_bcWrapper;
        string m_appVersion = "";
        static readonly Random m_random = new Random();

        // Move throttle — flush at most once per ~16 ms (~60 fps)
        long _lastMoveSendTime = 0;
        bool _pendingMoveSend = false;
        Point _pendingMovePos;

        // Relay ping broadcast — fires every 2 seconds while in game
        long _lastPingBroadcastTime = 0;

        // Deferred END_MATCH disconnect — cannot safely call Disconnect() from inside a relay callback
        bool _pendingEndMatch = false;

        // Guards DieWithMessage during voluntary relay disconnect (END_MATCH / CloseGame).
        // Stays true from disconnect until next successful relay Connect — mirrors Java's _disconnecting
        // and C++'s isDisconnecting, but kept across the async disconnect-to-reconnect window.
        bool _isRelayDisconnecting = false;

        // Shared RTT-enable mechanism. RTTComms.EnableRTT silently no-ops (neither callback
        // fires) when RTT is already connected or already connecting, so anything that needs
        // RTT (chat bootstrap from the Main Menu, matchmaking from the Play button) must funnel
        // through here rather than each calling EnableRTT directly — otherwise whichever caller
        // comes second gets stuck waiting on a callback that will never arrive.
        bool _rttConnecting = false;
        List<Action> _rttEnableWaiters = new List<Action>();

        // Global chat
        bool _chatRTTRegistered = false;
        string _chatChannelId = null;
        bool _chatChannelResolving = false;
        long _chatChannelRetryAtMs = 0;

        // Match summary / leaderboard posting
        long _lastResultsPollMs = 0;
        List<Dictionary<string, object>> _pendingMatchResult = new List<Dictionary<string, object>>();

        public GameApp() { }

        public void Update()
        {
            if (m_bcWrapper != null)
            {
                m_bcWrapper.Update();

                if (State.screenState == ScreenState.JoiningLobby)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (State.lobbySearchStartTime > 0)
                        State.form.UpdateLobbyTimer(now - State.lobbySearchStartTime);
                }

                if (State.screenState == ScreenState.Starting)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long from = State.lobbyStatusStartTime > 0 ? State.lobbyStatusStartTime : State.lobbySearchStartTime;
                    if (from > 0)
                        State.form.UpdateStartingTimer(now - from);
                }

                // Deferred END_MATCH: safely disconnect relay after callback has returned
                if (_pendingEndMatch)
                {
                    Console.WriteLine("[APP] _pendingEndMatch firing — disconnecting relay, _isRelayDisconnecting=true");
                    _pendingEndMatch = false;
                    _isRelayDisconnecting = true;   // suppress DieWithMessage until next relay connect
                    m_bcWrapper.RelayService.DeregisterRelayCallback();
                    m_bcWrapper.RelayService.DeregisterSystemCallback();
                    m_bcWrapper.RelayService.Disconnect();
                    m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
                    m_bcWrapper.RTTService.RegisterRTTLobbyCallback(OnLobbyEvent);
                    // RTT connection itself stays up — only the callback registrations were
                    // cleared above, so chat needs re-registering too (not a fresh channel fetch).
                    EnableChatRTT();

                    // Explicitly re-send our (already-false, set in OnMatchEnded) ready state.
                    // Everyone starts the summary screen not-ready and queues for rematch
                    // explicitly (MatchSummaryScreen's Queue-for-Rematch button / 45s auto-queue)
                    // — replaces the old auto non-host-ready quirk that predated this port.
                    if (State.lobby != null && State.user != null)
                    {
                        m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, BuildExtraJson(State.user.colorIndex));
                        Console.WriteLine($"[APP] UpdateReady({State.user.isReady}) sent");
                    }
                }

                if (State.screenState == ScreenState.Game)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    // Flush pending move at 60 fps
                    if (_pendingMoveSend && now - _lastMoveSendTime >= 16)
                        FlushPendingMove();

                    // Broadcast relay RTT to all players every 2 seconds
                    if (now - _lastPingBroadcastTime >= 2000)
                    {
                        _lastPingBroadcastTime = now;
                        BroadcastRelayPing();
                    }

                    // Update visual effects and clean up expired splotches
                    State.form.UpdateEffects();

                    // Throttled coverage recompute (mirrors cpp/Java's debounced, recompute-on-change tick)
                    TickCoverageRecompute();

                    // Host: broadcast match_result at MATCH_DURATION_SEC, then actually end the
                    // match RESULT_GRACE_SEC later — mirrors cpp/Java's Running -> ResultsBroadcast
                    // -> Ended timeline so every client has results before the relay tears down.
                    if (State.gameStartTime > 0 &&
                        State.lobby?.ownerCxId == State.user?.cxId)
                    {
                        int elapsed = (int)((now - State.gameStartTime) / 1000);
                        if (elapsed >= MATCH_DURATION_SEC)
                            BroadcastMatchResults(); // internally idempotent per round
                        if (elapsed >= MATCH_DURATION_SEC + RESULT_GRACE_SEC)
                            EndMatch();
                    }
                }

                if (State.screenState == ScreenState.MatchSummary)
                {
                    TickMatchResultsPoll();
                    long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - State.matchSummaryArrivalTime;
                    if (elapsed >= MatchSummaryRematchMs)
                    {
                        OnContinueFromSummary();
                    }
                    else
                    {
                        State.form.UpdateMatchSummaryCountdown(elapsed);
                    }
                }
            }
        }


        public void LogOut()
        {
            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
            m_bcWrapper.RTTService.DisableRTT();
            m_bcWrapper.Logout(true);
            ResetState();
        }

        public void Exit()
        {
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }

        public void Login(string username, string password)
        {
            InitBC();
            ChangeScreen(ScreenState.LoggingIn);
            m_bcWrapper.AuthenticateUniversal(username, password, true, HandlePlayerState, DieWithMessage, "Login Failed");
        }

        public void CheckReconnect()
        {
            InitBC();
            if (m_bcWrapper.CanReconnect())
                m_bcWrapper.Reconnect(HandlePlayerState, DieWithMessage);
        }

        public void RefreshClientVersion()
        {
            if (m_bcWrapper != null)
                State.form?.SetClientVersion(m_bcWrapper.Client.BrainCloudClientVersion);
        }

        public void Play(BrainCloud.RelayConnectionType protocol, string lobbyType)
        {
            Settings.protocol = protocol;
            Settings.lobbyType = lobbyType;
            State.user.colorIndex = Settings.colorIndex;
            State.lobbySearchStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ChangeScreen(ScreenState.JoiningLobby);
            m_bcWrapper.RTTService.RegisterRTTLobbyCallback(OnLobbyEvent);
            EnsureRTTEnabled(ProceedToFindLobby);
        }

        public void CloseGame()
        {
            bool wasInRelay = m_bcWrapper.RelayService.IsConnected();

            _isRelayDisconnecting = true;
            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            _isRelayDisconnecting = false;  // RTT is also shutting down, so no relay reconnect expected
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();

            // If we were in the lobby (not yet in relay), explicitly tell the server we're
            // leaving so other members see the departure immediately (mirrors C++ app_cancelLobby).
            if (!wasInRelay && State.lobby != null)
                m_bcWrapper.LobbyService.LeaveLobby(State.lobby.lobbyId);

            m_bcWrapper.RTTService.DisableRTT();
            // DisableRTT() drops the connection entirely, taking the chat channel
            // subscription with it — reset both so the re-enable below re-resolves and
            // re-subscribes fresh instead of trusting a stale channel id.
            _chatRTTRegistered = false;
            _chatChannelId = null;
            _chatChannelResolving = false;
            _chatChannelRetryAtMs = 0;

            State.lobby = null;
            State.server = null;
            State.shockwaves = new List<Shockwave>();
            State.splotches = new List<Splotch>();
            State.gameStartTime = 0;
            State.lobbySearchStartTime = 0;
            State.lobbyStatusStartTime = 0;
            State.mouseX = 0;
            State.mouseY = 0;
            State.matchResult = new MatchResult();
            State.coverage = new List<Coverage.CoverageEntry>();
            State.lobbyJoinedAtMs = 0;
            ChangeScreen(ScreenState.MainMenu);
            EnableChatRTT();
        }

        public void StartGame()
        {
            State.user.isReady = true;
            ChangeScreen(ScreenState.Starting);
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, BuildExtraJson(State.user.colorIndex));
        }

        // Non-host ready toggle. Starting the round is still host-only (via StartGame/the
        // Start button, which has no readiness gate at all — always available as an
        // early-start option), but every other member needs a way to signal readiness.
        // Unlike StartGame this only flips the local flag, it never forces true and never
        // starts anything itself.
        public void OnToggleReady()
        {
            State.user.isReady = !State.user.isReady;
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, BuildExtraJson(State.user.colorIndex));
            State.form.UpdateLobby();
        }

        public void ChangeUserColor(int colorIndex)
        {
            State.user.colorIndex = colorIndex;
            foreach (var member in State.lobby.members)
            {
                if (State.user.cxId == member.cxId)
                {
                    member.colorIndex = colorIndex;
                    break;
                }
            }
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, BuildExtraJson(colorIndex));
        }

        // Build the extra dict for lobby join/updateReady calls.
        // Always includes colorIndex; includes per-region pings when available.
        Dictionary<string, object> BuildExtraJson(int colorIndex)
        {
            var extra = new Dictionary<string, object> { ["colorIndex"] = colorIndex };
            if (State.pingData.Count > 0)
                extra["pings"] = State.pingData;
            return extra;
        }

        // Broadcast our current relay RTT to all players. Called every 2 seconds while in game.
        void BroadcastRelayPing()
        {
            if (m_bcWrapper?.RelayService == null || !m_bcWrapper.RelayService.IsConnected()) return;
            int ping = (int)(m_bcWrapper.RelayService.LastPing * 0.0001f);

            // Update own entry immediately
            if (State.lobby != null)
                foreach (var member in State.lobby.members)
                    if (member.cxId == State.user?.cxId) { member.activePing = ping; break; }

            var json = new Dictionary<string, object>
            {
                ["op"] = "relay_ping",
                ["data"] = new Dictionary<string, object> { ["ping"] = ping }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS,
                false, false, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_1);
        }

        public void MouseMoved(Point pos)
        {
            State.user.isAlive = true;
            State.user.pos = pos;
            foreach (var user in State.lobby.members)
            {
                if (State.user.cxId == user.cxId)
                {
                    user.isAlive = true;
                    user.pos = pos;
                    break;
                }
            }

            _pendingMoveSend = true;
            _pendingMovePos = pos;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastMoveSendTime >= 16)
                FlushPendingMove();
        }

        void FlushPendingMove()
        {
            if (!_pendingMoveSend) return;
            _pendingMoveSend = false;

            var json = new Dictionary<string, object>
            {
                ["op"] = "move",
                ["data"] = new Dictionary<string, object> { ["x"] = _pendingMovePos.X, ["y"] = _pendingMovePos.Y }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS,
                Settings.sendReliable, Settings.sendOrdered, Settings.sendChannel);
            _lastMoveSendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void Shockwave(Point pos)
        {
            // Pick a rotation once and send it so every client renders this splotch the same.
            double angle = m_random.NextDouble() * Math.PI * 2.0;

            var json = new Dictionary<string, object>
            {
                ["op"] = "shockwave",
                ["data"] = new Dictionary<string, object> { ["x"] = pos.X, ["y"] = pos.Y, ["angle"] = angle }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));

            ulong playerMask = BuildSendMask();
            m_bcWrapper.RelayService.SendToPlayers(data, playerMask,
                true, false, Settings.sendChannel);

            // Add locally (sender doesn't receive their own relay message)
            AddShockwaveAndSplotch(pos, State.user.colorIndex, angle);
        }

        // Host-only: end the current match and return all players to the lobby.
        public void EndMatch()
        {
            if (State.lobby?.ownerCxId != State.user?.cxId) return;
            // Zero out gameStartTime immediately so the auto-end loop in Update()
            // cannot fire EndMatch() again before the END_MATCH system callback arrives.
            State.gameStartTime = 0;
            m_bcWrapper.RelayService.EndMatch(new Dictionary<string, object>());
        }


        // Fetches top-5 + the local player's own rank for the given board/period combo.
        // All bcWrapper calls stay in GameApp per this file's convention; MainWindow just
        // renders whatever comes back.
        public void FetchLeaderboard(bool coverage, bool quarterly, Action<List<LeaderboardEntry>> onTop5, Action<LeaderboardEntry> onSelf)
        {
            string leaderboardId = coverage
                ? (quarterly ? State.coverageLeaderboardIdQuarterly : State.coverageLeaderboardId)
                : (quarterly ? State.pointsLeaderboardIdQuarterly : State.pointsLeaderboardId);

            LeaderboardEntry ParseEntry(Dictionary<string, object> entry)
            {
                var e = new LeaderboardEntry();
                e.score = entry.ContainsKey("score") ? Convert.ToInt64(entry["score"]) : 0;
                e.rank = entry.ContainsKey("rank") ? Convert.ToInt32(entry["rank"]) : 0;
                var data = entry.ContainsKey("data") ? entry["data"] as Dictionary<string, object> : null;
                string name = data != null && data.ContainsKey("name") ? data["name"] as string ?? "" : "";
                e.name = string.IsNullOrEmpty(name) ? "Player" : name;
                return e;
            }

            m_bcWrapper.SocialLeaderboardService.GetGlobalLeaderboardPage(
                leaderboardId, BrainCloudSocialLeaderboard.SortOrder.HIGH_TO_LOW, 0, 4,
                (response, cbObj) =>
                {
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        var arr = data["leaderboard"] as object[];
                        var top = new List<LeaderboardEntry>();
                        if (arr != null)
                            foreach (var item in arr)
                                top.Add(ParseEntry(item as Dictionary<string, object>));
                        onTop5(top);
                    }
                    catch { onTop5(new List<LeaderboardEntry>()); }
                },
                (status, reasonCode, jsonError, cbObj) => onTop5(new List<LeaderboardEntry>()));

            // beforeCount=0/afterCount=0 on GetGlobalLeaderboardView returns just the current
            // player's own entry.
            m_bcWrapper.SocialLeaderboardService.GetGlobalLeaderboardView(
                leaderboardId, BrainCloudSocialLeaderboard.SortOrder.HIGH_TO_LOW, 0, 0,
                (response, cbObj) =>
                {
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        var arr = data["leaderboard"] as object[];
                        onSelf(arr != null && arr.Length > 0 ? ParseEntry(arr[0] as Dictionary<string, object>) : null);
                    }
                    catch { onSelf(null); }
                },
                (status, reasonCode, jsonError, cbObj) => onSelf(null));
        }

        public string GetAppVersion()
        {
            if (m_bcWrapper != null) return m_bcWrapper.Client.GetAppVersion();
            if (!string.IsNullOrEmpty(m_appVersion)) return m_appVersion;
            // Read directly from ids.txt if not yet initialized
            string idsPath = Path.Combine(AppContext.BaseDirectory, "ids.txt");
            if (File.Exists(idsPath))
            {
                foreach (var line in File.ReadAllLines(idsPath))
                    if (line.StartsWith("appVersion=")) return line["appVersion=".Length..].Trim();
            }
            return "N/A";
        }
        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        void InitBC()
        {
            if (m_bcWrapper == null)
                m_bcWrapper = new BrainCloudWrapper("RelayTestApp");

            string url = "", appId = "", appSecret = "", appVersion = "";
            string idsPath = Path.Combine(AppContext.BaseDirectory, "ids.txt");
            using (var reader = new StreamReader(idsPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("serverUrl=")) url = line.Substring("serverUrl=".Length).Trim();
                    else if (line.StartsWith("appId=")) appId = line.Substring("appId=".Length).Trim();
                    else if (line.StartsWith("secret=")) appSecret = line.Substring("secret=".Length).Trim();
                    else if (line.StartsWith("appVersion=")) appVersion = line.Substring("appVersion=".Length).Trim();
                }
            }

            m_appVersion = appVersion;
            m_bcWrapper.Init(url, appSecret, appId, appVersion);
            m_bcWrapper.Client.EnableLogging(true);
            State.form?.SetClientVersion(m_bcWrapper.Client.BrainCloudClientVersion);
        }

        void ReadGlobalProperties()
        {
            m_bcWrapper.GlobalAppService.ReadProperties(
                (response, cbObj) =>
                {
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        if (data != null && data.ContainsKey("SplotchDuration"))
                        {
                            var prop = data["SplotchDuration"] as Dictionary<string, object>;
                            if (prop != null && prop.ContainsKey("value"))
                                int.TryParse(prop["value"]?.ToString(), out State.splotchDurationSec);
                        }
                        if (data != null && data.ContainsKey("AllLobbyTypes"))
                        {
                            var prop = data["AllLobbyTypes"] as Dictionary<string, object>;
                            if (prop != null && prop.ContainsKey("value"))
                            {
                                // value is a JSON string: { "key": { "lobby": "TypeName" }, ... }
                                var lobbyMap = JsonReader.Deserialize<Dictionary<string, object>>(
                                    prop["value"]?.ToString() ?? "{}");
                                State.appLobbies.Clear();
                                foreach (var entry in lobbyMap.Values)
                                {
                                    var entryDict = entry as Dictionary<string, object>;
                                    if (entryDict != null && entryDict.ContainsKey("lobby"))
                                        State.appLobbies.Add(entryDict["lobby"]?.ToString() ?? "");
                                }
                                State.form.UpdateMainMenu();
                            }
                        }
                        if (data != null && data.ContainsKey("Colors"))
                        {
                            var prop = data["Colors"] as Dictionary<string, object>;
                            if (prop != null && prop.ContainsKey("value"))
                            {
                                try
                                {
                                    var hexArray = JsonReader.Deserialize<string[]>(prop["value"]?.ToString() ?? "[]");
                                    if (hexArray != null && hexArray.Length > 0)
                                    {
                                        var newColors = new Avalonia.Media.Color[hexArray.Length];
                                        for (int i = 0; i < hexArray.Length; i++)
                                        {
                                            string hex = hexArray[i].TrimStart('#');
                                            byte red   = Convert.ToByte(hex.Substring(0, 2), 16);
                                            byte green = Convert.ToByte(hex.Substring(2, 2), 16);
                                            byte blue  = Convert.ToByte(hex.Substring(4, 2), 16);
                                            newColors[i] = Avalonia.Media.Color.FromRgb(red, green, blue);
                                        }
                                        CursorColor.COLORS = newColors;
                                    }
                                }
                                catch { }
                            }
                        }

                        string ReadStringProp(string key, string fallback)
                        {
                            if (data != null && data.ContainsKey(key) && data[key] is Dictionary<string, object> p
                                && p.ContainsKey("value"))
                            {
                                string v = p["value"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(v)) return v;
                            }
                            return fallback;
                        }
                        State.pointsLeaderboardId = ReadStringProp("PointsLeaderboardId", State.pointsLeaderboardId);
                        State.pointsLeaderboardIdQuarterly = ReadStringProp("PointsLeaderboardIdQuarterly", State.pointsLeaderboardIdQuarterly);
                        State.coverageLeaderboardId = ReadStringProp("CoverageLeaderboardId", State.coverageLeaderboardId);
                        State.coverageLeaderboardIdQuarterly = ReadStringProp("CoverageLeaderboardIdQuarterly", State.coverageLeaderboardIdQuarterly);
                    }
                    catch { }
                },
                null, null);
        }

        void HandlePlayerState(string jsonResponse, object cbObject)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var data = response["data"] as Dictionary<string, object>;

            State.user = new User();

            string playerName = data.ContainsKey("playerName") ? data["playerName"] as string : null;
            if (string.IsNullOrEmpty(playerName))
                SubmitName(Settings.username);
            else
            {
                State.user.name = playerName;
                OnLoggedIn(jsonResponse, cbObject);
            }
        }

        void ChangeScreen(ScreenState screen)
        {
            State.screenState = screen;
            State.form.ShowScreen(screen);
        }

        void OnLoggedIn(string jsonResponse, object cbObject)
        {
            ReadGlobalProperties();
            FetchServerVersion();
            ChangeScreen(ScreenState.MainMenu);
            State.form.UpdateMainMenu();
            EnableChatRTT();
        }

        void FetchServerVersion()
        {
            m_bcWrapper.Client.AuthenticationService.getServerVersion(
                (response, _) =>
                {
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        if (data != null && data.ContainsKey("serverVersion"))
                            State.form.SetServerVersion(data["serverVersion"]?.ToString() ?? "");
                    }
                    catch { }
                },
                null);
        }

        void SubmitName(string username)
        {
            State.user.name = username;
            m_bcWrapper.PlayerStateService.UpdateUserName(username, OnLoggedIn, DieWithMessage, "Failed to update username");
        }

        void OnRTTDisconnected(int status, int reasonCode, string jsonError, object cbObject)
        {
            if (jsonError == "DisableRTT Called") return;
            DieWithMessage(status, reasonCode, jsonError, cbObject);
        }

        void DieWithMessage(int status, int reasonCode, string jsonError, object cbObject)
        {
            if (_isRelayDisconnecting)
            {
                Console.WriteLine($"[APP] DieWithMessage suppressed (voluntary disconnect) — {jsonError}");
                return;
            }

            // Race guard: the relay server closes the WebSocket immediately after broadcasting
            // END_MATCH, so ConnectFailure (RS_ENDMATCH_REQUESTED) can arrive in the same SDK
            // event batch as SocketData(END_MATCH binary).  If ConnectFailure is processed first
            // it clears the pending System(END_MATCH) event before OnRelaySystemMessage fires,
            // so _isRelayDisconnecting is never set.  Detect this case and treat it as a graceful
            // END_MATCH rather than a fatal error.
            if (reasonCode == BrainCloud.ReasonCodes.RS_ENDMATCH_REQUESTED &&
                State.screenState == ScreenState.Game)
            {
                Console.WriteLine($"[APP] DieWithMessage: RS_ENDMATCH_REQUESTED in Game — treating as END_MATCH (race guard)");
                _isRelayDisconnecting = true;
                OnMatchEnded();
                return;
            }

            Console.WriteLine($"[APP] DieWithMessage — status={status} reasonCode={reasonCode} | {jsonError}");

            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
            m_bcWrapper.RTTService.DisableRTT();

            // resetCommunication-equivalent: reset the RTT/chat bootstrap flags too so a
            // fresh login doesn't trust stale state.
            _rttConnecting = false;
            _rttEnableWaiters.Clear();
            _chatRTTRegistered = false;
            _chatChannelId = null;
            _chatChannelResolving = false;
            _chatChannelRetryAtMs = 0;

            string message = cbObject as string;
            State.form.ShowError((message ?? "Error") + ": " + jsonError);
            ResetState();
        }

        void ResetState()
        {
            State.user = null;
            State.lobby = null;
            State.server = null;
            State.shockwaves = new List<Shockwave>();
            State.splotches = new List<Splotch>();
            State.gameStartTime = 0;
            State.lobbySearchStartTime = 0;
            State.lobbyStatusStartTime = 0;
            State.mouseX = 0;
            State.mouseY = 0;
            ChangeScreen(ScreenState.Login);
        }

        // Shared RTT-enable helper — see the field comment above for why this exists.
        // State.user.cxId is set once here (on the connect success path), then every
        // queued waiter (chat bootstrap, Play's lobby search, ...) runs in order.
        void EnsureRTTEnabled(Action onReady)
        {
            if (m_bcWrapper.RTTService.IsRTTEnabled())
            {
                onReady();
                return;
            }
            _rttEnableWaiters.Add(onReady);
            if (_rttConnecting) return;
            _rttConnecting = true;

            m_bcWrapper.RTTService.EnableRTT(
                (jsonResponse, cbObject) =>
                {
                    _rttConnecting = false;
                    State.user.cxId = m_bcWrapper.RTTService.getRTTConnectionID();
                    var waiters = new List<Action>(_rttEnableWaiters);
                    _rttEnableWaiters.Clear();
                    foreach (var w in waiters) w();
                },
                (status, reasonCode, jsonError, cbObject) =>
                {
                    _rttConnecting = false;
                    _rttEnableWaiters.Clear();
                    OnRTTDisconnected(status, reasonCode, jsonError, cbObject);
                });
        }

        void ProceedToFindLobby()
        {
            var algo = new Dictionary<string, object>
            {
                ["strategy"] = "ranged-absolute",
                ["alignment"] = "center",
                ["ranges"] = new System.Collections.Generic.List<int> { 1000 }
            };

            void DoFindLobby(bool withPingData)
            {
                var extra = BuildExtraJson(State.user.colorIndex);
                if (withPingData)
                    m_bcWrapper.LobbyService.FindOrCreateLobbyWithPingData(
                        Settings.lobbyType, 0, 1, algo,
                        new Dictionary<string, object>(),
                        false, extra, "all",
                        new Dictionary<string, object>(),
                        null, null, DieWithMessage, "Failed to find lobby");
                else
                    m_bcWrapper.LobbyService.FindOrCreateLobby(
                        Settings.lobbyType, 0, 1, algo,
                        new Dictionary<string, object>(),
                        false, extra, "all",
                        new Dictionary<string, object>(),
                        null, null, DieWithMessage, "Failed to find lobby");
            }

            if (Settings.usePingData)
            {
                m_bcWrapper.LobbyService.GetRegionsForLobbies(
                    new string[] { Settings.lobbyType },
                    (response, _) =>
                    {
                        m_bcWrapper.LobbyService.PingRegions(
                            (response2, _) =>
                            {
                                State.pingData.Clear();
                                var pingDataRaw = m_bcWrapper.LobbyService.PingData;
                                if (pingDataRaw != null)
                                    foreach (var kv in pingDataRaw)
                                    {
                                        State.pingData[kv.Key] = (int)kv.Value;
                                    }
                                DoFindLobby(true);
                            },
                            (status, code, err, _) =>
                            {
                                DoFindLobby(false);
                            });
                    },
                    (status, code, err, _) =>
                    {
                        DoFindLobby(false);
                    });
            }
            else
            {
                DoFindLobby(false);
            }
        }

        // -----------------------------------------------------------------------
        // Global chat
        // -----------------------------------------------------------------------

        void EnableChatRTT()
        {
            if (!_chatRTTRegistered)
            {
                _chatRTTRegistered = true;
                m_bcWrapper.RTTService.RegisterRTTChatCallback(OnChatRTTEvent);
            }
            EnsureRTTEnabled(EnsureChatChannel);
        }

        void EnsureChatChannel()
        {
            if (_chatChannelId != null || _chatChannelResolving) return;
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < _chatChannelRetryAtMs) return;
            _chatChannelResolving = true;

            m_bcWrapper.ChatService.GetChannelId("gl", "gl",
                (response, cbObj) =>
                {
                    string channelId;
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        channelId = data["channelId"] as string;
                    }
                    catch
                    {
                        _chatChannelResolving = false;
                        _chatChannelRetryAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ChatChannelRetryMs;
                        return;
                    }

                    m_bcWrapper.ChatService.ChannelConnect(channelId, 30,
                        (connectResponse, cbObj2) =>
                        {
                            _chatChannelId = channelId;
                            _chatChannelResolving = false;
                            try
                            {
                                var r2 = JsonReader.Deserialize<Dictionary<string, object>>(connectResponse);
                                var data2 = r2["data"] as Dictionary<string, object>;
                                var messages = data2.ContainsKey("messages") ? data2["messages"] as object[] : null;
                                State.chatMessagesGlobal.Clear();
                                if (messages != null)
                                    foreach (var m in messages)
                                        State.chatMessagesGlobal.Add(ParseChatMessage(m as Dictionary<string, object>));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Failed to parse chat history: " + ex.Message);
                            }
                            State.form.UpdateChat();
                        },
                        (status, reasonCode, jsonError, cbObj2) =>
                        {
                            _chatChannelResolving = false;
                            _chatChannelRetryAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ChatChannelRetryMs;
                        });
                },
                (status, reasonCode, jsonError, cbObj) =>
                {
                    _chatChannelResolving = false;
                    _chatChannelRetryAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ChatChannelRetryMs;
                });
        }

        ChatMessage ParseChatMessage(Dictionary<string, object> m)
        {
            var msg = new ChatMessage();
            msg.msgId = m.ContainsKey("msgId") ? m["msgId"] as string ?? "" : "";
            var from = m.ContainsKey("from") ? m["from"] as Dictionary<string, object> : null;
            string fromName = from != null && from.ContainsKey("name") ? from["name"] as string ?? "" : "";
            msg.fromName = string.IsNullOrEmpty(fromName) ? "Player" : fromName;
            var content = m.ContainsKey("content") ? m["content"] as Dictionary<string, object> : null;
            msg.text = content != null && content.ContainsKey("text") ? content["text"] as string ?? "" : "";
            return msg;
        }

        // operation: INCOMING (new message) / UPDATE (edited) / DELETE (removed), all keyed
        // by msgId. The event's "operation" is a SIBLING of "data", not nested inside it —
        // getting this wrong silently swallows every incoming chat message (confirmed the
        // hard way porting this to Java first).
        void OnChatRTTEvent(string jsonResponse)
        {
            try
            {
                var eventJson = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
                if (!eventJson.ContainsKey("service") || eventJson["service"] as string != "chat") return;
                string operation = eventJson["operation"] as string;
                var data = eventJson["data"] as Dictionary<string, object>;

                if (operation == "DELETE")
                {
                    string msgId = data["msgId"] as string;
                    State.chatMessagesGlobal.RemoveAll(m => m.msgId == msgId);
                }
                else
                {
                    var msg = ParseChatMessage(data);
                    int idx = State.chatMessagesGlobal.FindIndex(m => m.msgId == msg.msgId);
                    if (idx >= 0) State.chatMessagesGlobal[idx] = msg;
                    else State.chatMessagesGlobal.Add(msg);
                }
                State.form.UpdateChat();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to parse chat RTT event: " + ex.Message);
            }
        }

        public void SendGlobalChatMessage(string text)
        {
            if (_chatChannelId == null || string.IsNullOrEmpty(text)) return;
            m_bcWrapper.ChatService.PostChatMessageSimple(_chatChannelId, text, true);
        }

        // Sends a chat message to everyone currently in this lobby, via the Lobby service's
        // SendSignal (not the Chat service — rides the RTT connection the lobby already has,
        // no separate channel/registration needed). Appends locally right away; the receive
        // handler (OnLobbyEvent, "SIGNAL" operation) skips the echo of our own signal that the
        // server sends back to us too.
        public void SendLobbySignalChat(string text)
        {
            if (string.IsNullOrEmpty(text) || State.lobby == null) return;
            m_bcWrapper.LobbyService.SendSignal(State.lobby.lobbyId, new Dictionary<string, object> { ["text"] = text });

            State.lobby.chatMessages.Add(new ChatMessage { fromName = State.user.name, text = text });
            State.form.UpdateLobby();
        }

        // -----------------------------------------------------------------------
        // Coverage / match end / summary / leaderboard posting
        // -----------------------------------------------------------------------

        // Recomputes State.coverage only when the splotch set has actually changed
        // (State.splotchGeneration) and at least CoverageRecomputeMs has elapsed since the
        // last compute — mirrors the cpp reference client's debounced, recompute-on-change
        // coverage tick rather than a fixed-interval poll.
        void TickCoverageRecompute()
        {
            if (State.lobby == null) return;
            if (State.coverageComputedGen == State.splotchGeneration) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - State.coverageComputedAtMs < CoverageRecomputeMs) return;

            State.coverageComputedAtMs = now;
            State.coverageComputedGen = State.splotchGeneration;
            State.coverage = Coverage.Compute(State.splotches, State.lobby.members);
            State.form.UpdateCoverageSidebar();
        }

        // Called on every client when the relay server's END_MATCH system event arrives
        // (from OnRelaySystemMessage) or is inferred via the RS_ENDMATCH_REQUESTED race
        // guard in DieWithMessage — shared reset + transition to Match Summary.
        void OnMatchEnded()
        {
            // Fallback: if the host's match_result broadcast never arrived (dropped, or the
            // host disconnected mid-broadcast), compute a local snapshot so the summary
            // screen has something to show. No cloud posting from this fallback path — only
            // the host posts to the leaderboards.
            if ((!State.matchResult.valid || State.matchResult.round != State.roundNumber) && State.lobby != null)
            {
                var coverage = Coverage.Compute(State.splotches, State.lobby.members);
                State.matchResult = BuildMatchResult(State.roundNumber, coverage);
            }

            State.user.isAlive = false;
            State.user.isReady = false;
            State.shockwaves = new List<Shockwave>();
            State.splotches = new List<Splotch>();
            State.splotchGeneration++;
            State.coverage = new List<Coverage.CoverageEntry>();
            State.gameStartTime = 0;
            State.lobbyStatusStartTime = 0;
            State.lobbyStatusText = "";
            _pendingMoveSend = false;
            foreach (var member in State.lobby?.members ?? new List<User>())
            {
                member.isAlive = false;
                member.isReady = false;
            }

            State.matchSummaryArrivalTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastResultsPollMs = 0;

            // Defer relay disconnect — cannot safely call from inside a relay callback
            _pendingEndMatch = true;
            ChangeScreen(ScreenState.MatchSummary);
            State.form.UpdateMatchSummary();
        }

        // Called when the local player continues past Match Summary — either by clicking
        // the button or via the 45s auto-timeout. The host's Start button in the Lobby has
        // no readiness gate at all (always available as an early-start option), so the
        // host never auto-readies here — they just land back in the Lobby where Start is
        // waiting for them. Everyone else marks themselves ready (so the ready count the
        // host sees builds up) and lands in the same place.
        public void OnContinueFromSummary()
        {
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            if (!isHost)
            {
                State.user.isReady = true;
                if (State.lobby != null)
                    m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, true, BuildExtraJson(State.user.colorIndex));
            }
            ChangeScreen(ScreenState.Lobby);
            State.form.UpdateLobby();
        }

        MatchResult BuildMatchResult(int round, List<Coverage.CoverageEntry> coverage)
        {
            var result = new MatchResult { round = round, valid = true };
            foreach (var c in coverage)
            {
                result.entries.Add(new MatchResult.Entry
                {
                    cxId = c.cxId,
                    rank = c.rank,
                    coveragePct = c.coveragePct,
                    beaten = c.beaten
                });
            }
            return result;
        }

        void ApplyMatchResult(int round, List<Dictionary<string, object>> entries)
        {
            if (State.matchResult.valid && State.matchResult.round == round) return; // idempotent — guards a duplicate broadcast (e.g. a migrated host)
            var result = new MatchResult { round = round, valid = true };
            foreach (var e in entries)
            {
                result.entries.Add(new MatchResult.Entry
                {
                    cxId = e["cx"] as string,
                    rank = Convert.ToInt32(e["r"]),
                    coveragePct = Convert.ToInt32(e["c"]) / 100.0f, // basis points -> percent
                    beaten = Convert.ToInt32(e["b"])
                });
            }
            State.matchResult = result;
            State.form.UpdateMatchSummary();
        }

        // Host-only: computes the final coverage snapshot, broadcasts it to everyone (relay
        // op match_result) and posts it to the leaderboards via cloud code. Guarded per-round
        // since the points leaderboard is cumulative — a duplicate post would silently and
        // permanently inflate a lifetime total.
        void BroadcastMatchResults()
        {
            if (State.lobby == null) return;
            if (State.leaderboardPostedRound == State.roundNumber) return;
            State.leaderboardPostedRound = State.roundNumber;

            var coverage = Coverage.Compute(State.splotches, State.lobby.members);
            var result = BuildMatchResult(State.roundNumber, coverage);
            State.matchResult = result;
            State.form.UpdateMatchSummary();

            SendMatchResultToAll(result);
            HostPostMatchResultsToCloud(result);
        }

        void SendMatchResultToAll(MatchResult result)
        {
            if (result.entries.Count == 0) return;
            bool isFirst = true;
            var batch = new List<Dictionary<string, object>>();
            int currentSize = 80; // envelope overhead estimate

            for (int i = 0; i <= result.entries.Count; i++)
            {
                Dictionary<string, object> entry = null;
                string entryStr = null;
                if (i < result.entries.Count)
                {
                    var e = result.entries[i];
                    entry = new Dictionary<string, object>
                    {
                        ["cx"] = e.cxId,
                        ["r"] = e.rank,
                        ["c"] = (int)Math.Round(e.coveragePct * 100.0f),
                        ["b"] = e.beaten
                    };
                    entryStr = JsonWriter.Serialize(entry);
                }

                bool isLastIteration = (i == result.entries.Count);
                bool flush = isLastIteration || (entry != null
                    && currentSize + entryStr.Length + 1 > MaxRelayBytes && batch.Count > 0);
                if (flush && batch.Count > 0)
                {
                    var msg = new Dictionary<string, object>
                    {
                        ["op"] = "match_result",
                        ["data"] = new Dictionary<string, object>
                        {
                            ["round"] = result.round,
                            ["first"] = isFirst,
                            ["last"] = isLastIteration,
                            ["e"] = batch.ToArray()
                        }
                    };
                    byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(msg));
                    // Reliable AND ordered (unlike splotch_sync's reliable/unordered) — a
                    // chunk-reassembly race is cosmetic for splotches but would corrupt a
                    // posted score here.
                    m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS,
                        true, true, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_1);
                    isFirst = false;
                    batch = new List<Dictionary<string, object>>();
                    currentSize = 80;
                }
                if (entry != null)
                {
                    batch.Add(entry);
                    currentSize += entryStr.Length + 1;
                }
            }
        }

        // The PostMatchResults cloud-code script itself is server-side (already deployed
        // against this brainCloud app — the cpp/react/godot/Java clients already call it) and
        // posts on our behalf via postScoreToLeaderboardOnBehalfOf, since individual clients
        // can no longer post directly.
        void HostPostMatchResultsToCloud(MatchResult result)
        {
            var payload = new Dictionary<string, object>
            {
                ["round"] = result.round,
                ["lobbyId"] = State.lobby.lobbyId,
                ["pointsLeaderboardId"] = State.pointsLeaderboardId,
                ["pointsLeaderboardIdQuarterly"] = State.pointsLeaderboardIdQuarterly,
                ["coverageLeaderboardId"] = State.coverageLeaderboardId,
                ["coverageLeaderboardIdQuarterly"] = State.coverageLeaderboardIdQuarterly
            };

            var entries = new List<Dictionary<string, object>>();
            foreach (var e in result.entries)
            {
                var member = MemberByCxId(e.cxId);
                if (member == null || string.IsNullOrEmpty(member.profileId)) continue; // can't post server-side without a profileId
                entries.Add(new Dictionary<string, object>
                {
                    ["profileId"] = member.profileId,
                    ["name"] = member.name,
                    ["points"] = e.beaten + 1,
                    ["coverageBasisPoints"] = (int)Math.Round(e.coveragePct * 100.0f)
                });
            }
            payload["entries"] = entries.ToArray();

            int round = result.round;
            m_bcWrapper.ScriptService.RunScript("PostMatchResults", JsonWriter.Serialize(payload),
                (response, cbObj) =>
                {
                    try
                    {
                        // The script's return value sits at data.response (a sibling of
                        // runTimeData/success), not directly at data.results.
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        var resp = data["response"] as Dictionary<string, object>;
                        var results = resp["results"] as object[];
                        ApplyLeaderboardResultsFromCloud(round, results);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to parse PostMatchResults response: " + ex.Message);
                    }
                },
                (status, reasonCode, jsonError, cbObj) =>
                {
                    Console.WriteLine("PostMatchResults failed: " + jsonError);
                });
        }

        // Non-host clients don't get the leaderboard delta via relay (no such op exists) —
        // they poll a GlobalEntity the cloud script writes, indexed by "<lobbyId>:<round>",
        // since a host that disconnects right after posting would otherwise leave everyone
        // else waiting forever even though the post itself already succeeded.
        public void TickMatchResultsPoll()
        {
            if (!State.matchResult.valid || State.lobby == null) return;
            if (State.lobby.ownerCxId == State.user?.cxId) return; // host posts directly, no need to poll

            foreach (var e in State.matchResult.entries)
                if (e.lbDelta.ready) return; // already applied this round

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastResultsPollMs < ResultsPollIntervalMs) return;
            _lastResultsPollMs = now;

            string indexedId = State.lobby.lobbyId + ":" + State.matchResult.round;
            int round = State.matchResult.round;
            m_bcWrapper.GlobalEntityService.GetListByIndexedId(indexedId, 1,
                (response, cbObj) =>
                {
                    try
                    {
                        var r = JsonReader.Deserialize<Dictionary<string, object>>(response);
                        var data = r["data"] as Dictionary<string, object>;
                        var entityList = data["entityList"] as object[];
                        if (entityList == null || entityList.Length == 0) return;
                        var entity = entityList[0] as Dictionary<string, object>;
                        var edata = entity["data"] as Dictionary<string, object>;
                        var results = edata["results"] as object[];
                        ApplyLeaderboardResultsFromCloud(round, results);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to parse match-results GlobalEntity: " + ex.Message);
                    }
                },
                (status, reasonCode, jsonError, cbObj) => { /* silently retry next tick */ });
        }

        // Shared by both the host's direct script response and the GlobalEntity poll.
        void ApplyLeaderboardResultsFromCloud(int round, object[] results)
        {
            if (!State.matchResult.valid || State.matchResult.round != round || results == null) return;
            foreach (var item in results)
            {
                var r = item as Dictionary<string, object>;
                if (r == null) continue;
                string profileId = r.ContainsKey("profileId") ? r["profileId"] as string ?? "" : "";
                var member = MemberByProfileId(profileId);
                if (member == null) continue;
                foreach (var e in State.matchResult.entries)
                {
                    if (e.cxId != member.cxId) continue;
                    e.lbDelta.ready = true;
                    ParsePeriodDelta(e.lbDelta.pointsLifetime, r.ContainsKey("pointsLifetime") ? r["pointsLifetime"] as Dictionary<string, object> : null);
                    ParsePeriodDelta(e.lbDelta.pointsQuarterly, r.ContainsKey("pointsQuarterly") ? r["pointsQuarterly"] as Dictionary<string, object> : null);
                    ParsePeriodDelta(e.lbDelta.coverageLifetime, r.ContainsKey("coverageLifetime") ? r["coverageLifetime"] as Dictionary<string, object> : null);
                    ParsePeriodDelta(e.lbDelta.coverageQuarterly, r.ContainsKey("coverageQuarterly") ? r["coverageQuarterly"] as Dictionary<string, object> : null);
                    break;
                }
            }
            State.form.UpdateMatchSummary();
        }

        void ParsePeriodDelta(MatchResult.PeriodDelta delta, Dictionary<string, object> period)
        {
            if (period == null) return;
            delta.improved = period.ContainsKey("improved") && period["improved"] is bool b && b;
            delta.rankBefore = period.ContainsKey("before") ? Convert.ToInt32(period["before"]) : -1;
            delta.rankAfter = period.ContainsKey("after") ? Convert.ToInt32(period["after"]) : -1;
        }

        User MemberByCxId(string cxId)
        {
            if (State.lobby == null) return null;
            foreach (var m in State.lobby.members) if (m.cxId == cxId) return m;
            return null;
        }

        User MemberByProfileId(string profileId)
        {
            if (State.lobby == null || string.IsNullOrEmpty(profileId)) return null;
            foreach (var m in State.lobby.members) if (m.profileId == profileId) return m;
            return null;
        }

        void OnLobbyEvent(string jsonResponse)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var jsonData = response["data"] as Dictionary<string, object>;

            if (jsonData.ContainsKey("lobby"))
            {
                var carryForwardChat = State.lobby?.chatMessages;
                State.lobby = new Lobby(jsonData["lobby"] as Dictionary<string, object>,
                                        jsonData["lobbyId"] as string);
                if (carryForwardChat != null) State.lobby.chatMessages = carryForwardChat;
                if (State.lobbyJoinedAtMs == 0) State.lobbyJoinedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (State.screenState == ScreenState.JoiningLobby)
                    ChangeScreen(ScreenState.Lobby);
                State.form.UpdateLobby();
            }

            if (response.ContainsKey("operation"))
            {
                switch (response["operation"] as string)
                {
                    case "SIGNAL":
                    {
                        // This-lobby chat, via SendSignal. Real wire shape: data: { lobbyId,
                        // from: {id,name,pic,cxId}, signalData: <our own payload> }. "from" is
                        // the server's authoritative sender info.
                        var fromJson = jsonData.ContainsKey("from") ? jsonData["from"] as Dictionary<string, object> : null;
                        string fromCxId = fromJson != null && fromJson.ContainsKey("cxId") ? fromJson["cxId"] as string ?? "" : "";
                        string fromName = fromJson != null && fromJson.ContainsKey("name") ? fromJson["name"] as string ?? "" : "";
                        var signalData = jsonData.ContainsKey("signalData") ? jsonData["signalData"] as Dictionary<string, object> : null;
                        string text = signalData != null && signalData.ContainsKey("text") ? signalData["text"] as string ?? "" : "";

                        // Skip echoes of our own signal — SendLobbySignalChat already appended
                        // it locally on send. Compared by cxId (not name) since two players
                        // could share a display name.
                        if (!string.IsNullOrEmpty(text) && fromCxId != State.user?.cxId && State.lobby != null)
                        {
                            State.lobby.chatMessages.Add(new ChatMessage
                            {
                                fromName = string.IsNullOrEmpty(fromName) ? "Player" : fromName,
                                text = text
                            });
                            State.form.UpdateLobby();
                        }
                        break;
                    }

                    case "ROOM_ASSIGNED":
                        State.lobbyStatusText = "Server assigned...";
                        State.form.UpdateLobbyStatus(State.lobbyStatusText);
                        State.form.UpdateStartingStatus(State.lobbyStatusText);
                        break;

                    case "ROOM_PROGRESS":
                        {
                            string progressText;
                            if (jsonData.ContainsKey("curStep"))
                            {
                                int curStep = Convert.ToInt32(jsonData["curStep"]);
                                int ofStep = jsonData.ContainsKey("ofStep") ? Convert.ToInt32(jsonData["ofStep"]) : 0;
                                string msg = jsonData.ContainsKey("msg") ? jsonData["msg"] as string ?? "" : "";
                                progressText = curStep > 0
                                    ? $"{curStep}/{ofStep}: {msg}"
                                    : (string.IsNullOrEmpty(msg) ? "Starting server..." : msg);
                            }
                            else if (jsonData.ContainsKey("progress"))
                            {
                                var prog = jsonData["progress"] as Dictionary<string, object>;
                                progressText = prog != null && prog.ContainsKey("status")
                                    ? prog["status"] as string ?? "" : "";
                            }
                            else progressText = "Starting server...";

                            State.lobbyStatusText = progressText;
                            State.form.UpdateLobbyStatus(progressText);
                            State.form.UpdateStartingStatus(progressText);
                            break;
                        }

                    case "DISBANDED":
                        {
                            var reason = jsonData["reason"] as Dictionary<string, object>;
                            if (Convert.ToInt32(reason["code"]) != BrainCloud.ReasonCodes.RTT_ROOM_READY)
                                CloseGame();
                            break;
                        }

                    case "STARTING":
                        Settings.colorIndex = State.user.colorIndex;
                        Settings.SaveConfigs();
                        State.lobbyStatusStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        ChangeScreen(ScreenState.Starting);
                        State.form.UpdateStartingStatus("Provisioning server...");
                        State.form.UpdateStartingLobbyId(State.lobby?.lobbyId ?? "");
                        break;

                    case "ROOM_READY":
                        State.server = new Server(jsonData, Settings.protocol);
                        State.form.UpdateStartingStatus("Connecting...");
                        ConnectRelay();
                        break;

                    case "STATUS_UPDATE":
                        // Lobby state already refreshed above via the "lobby" key check
                        if (State.screenState == ScreenState.Game)
                            State.form.UpdateGameViewport();
                        break;
                }
            }
        }

        void ConnectRelay()
        {
            m_bcWrapper.RelayService.RegisterRelayCallback(OnRelayMessage);
            m_bcWrapper.RelayService.RegisterSystemCallback(OnRelaySystemMessage);

            m_bcWrapper.RelayService.Connect(State.server.connectionType,
                new RelayConnectOptions(false, State.server.host, State.server.port,
                    State.server.passcode, State.server.lobbyId),
                OnRelayConnectSuccess, DieWithMessage, "Failed to connect to server");
        }

        void OnRelayConnectSuccess(string jsonResponse, object cbObject)
        {
            Console.WriteLine("[APP] Relay connected — _isRelayDisconnecting reset to false");
            _isRelayDisconnecting = false;  // old relay WebSocket close can no longer cause harm
            GoToGameScreen();
        }

        void GoToGameScreen()
        {
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;

            State.matchResult = new MatchResult();
            State.coverage = new List<Coverage.CoverageEntry>();
            State.coverageComputedGen = -1;

            ChangeScreen(ScreenState.Game);
            State.form.UpdateGameViewport();

            if (isHost)
            {
                State.roundNumber++;
                State.gameStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SendGameStart(BrainCloudRelay.TO_ALL_PLAYERS);
            }
            // Non-host: gameStartTime/roundNumber set when game_start relay message arrives
        }

        // Sends game_start (host → recipients). Also used for JIP re-sync.
        void SendGameStart(ulong toMask)
        {
            var json = new Dictionary<string, object>
            {
                ["op"] = "game_start",
                ["data"] = new Dictionary<string, object>
                {
                    ["startTime"] = State.gameStartTime,
                    ["round"] = State.roundNumber
                }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.SendToPlayers(data, toMask,
                true, true, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_2);
        }

        // Sends the full splotch canvas to a single joining-in-progress player.
        // Chunks the array so each packet stays under the relay MAX_PACKETSIZE (1024 bytes).
        // First chunk carries "first":true so the receiver clears its canvas before appending.
        void SendSplotchSync(ulong toMask)
        {
            const int maxChunkBytes = 900; // conservative headroom below relay MAX_PACKETSIZE (1024)
            var splotches = State.splotches;
            bool isFirst = true;
            int i = 0;

            // Always send at least one packet (even when the canvas is empty) so the
            // receiver clears its local splotch list.
            do
            {
                var batch = new List<Dictionary<string, object>>();
                byte[] packet = null;

                while (i < splotches.Count)
                {
                    var s = splotches[i];
                    batch.Add(new Dictionary<string, object>
                    {
                        ["x"] = s.pos.X,
                        ["y"] = s.pos.Y,
                        ["c"] = s.colorIndex,
                        ["a"] = s.angle,
                        ["t"] = s.startTimeMs
                    });

                    byte[] candidate = BuildSplotchSyncPacket(isFirst, batch);
                    if (candidate.Length > maxChunkBytes && batch.Count > 1)
                    {
                        // This entry pushed the packet over the limit — back it out and
                        // flush the current batch. i is not incremented so it opens the
                        // next chunk.
                        batch.RemoveAt(batch.Count - 1);
                        break;
                    }
                    packet = candidate;
                    i++;
                }

                // packet is null only when the canvas is empty (batch is also empty).
                packet ??= BuildSplotchSyncPacket(isFirst, batch);

                m_bcWrapper.RelayService.SendToPlayers(packet, toMask,
                    true, true, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_2);
                isFirst = false;

            } while (i < splotches.Count);
        }

        byte[] BuildSplotchSyncPacket(bool isFirst, List<Dictionary<string, object>> batch)
        {
            var json = new Dictionary<string, object>
            {
                ["op"] = "splotch_sync",
                ["data"] = new Dictionary<string, object>
                {
                    ["first"] = isFirst,
                    ["splotches"] = batch.ToArray()
                }
            };
            return Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
        }

        // Builds the outbound player mask for shockwaves, respecting per-player allowSendTo flags.
        ulong BuildSendMask()
        {
            ulong mask = 0;
            foreach (var member in State.lobby.members)
            {
                if (!member.allowSendTo) continue;
                short netId = m_bcWrapper.RelayService.GetNetIdForCxId(member.cxId);
                if (netId >= 0 && netId < 64)
                    mask |= 1ul << netId;
            }
            return mask;
        }

        void AddShockwaveAndSplotch(Point pos, int colorIndex, double angle)
        {
            State.shockwaves.Add(new Shockwave
            {
                pos = pos,
                colorIndex = colorIndex,
                startTime = DateTime.Now
            });
            State.splotches.Add(new Splotch
            {
                pos = pos,
                colorIndex = colorIndex,
                angle = angle,
                startTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            State.splotchGeneration++;
        }

        // -----------------------------------------------------------------------
        // Relay callbacks
        // -----------------------------------------------------------------------

        void OnRelayMessage(short netId, byte[] jsonResponse)
        {
            var json = JsonReader.Deserialize<Dictionary<string, object>>(
                Encoding.ASCII.GetString(jsonResponse));

            var op = json.ContainsKey("op") ? json["op"] as string : "";
            var data = json.ContainsKey("data") ? json["data"] as Dictionary<string, object> : null;

            // --- Ops that don't require a sender lookup ---

            if (op == "game_start" && data != null)
            {
                State.gameStartTime = Convert.ToInt64(data["startTime"]);
                State.roundNumber = Convert.ToInt32(data["round"]);
                return;
            }

            if (op == "splotch_sync" && data != null)
            {
                bool first = data.ContainsKey("first") && data["first"] is bool b && b;
                if (first) State.splotches.Clear();

                if (data.ContainsKey("splotches"))
                {
                    var arr = data["splotches"] as object[];
                    if (arr != null)
                    {
                        foreach (var entry in arr)
                        {
                            var sd = entry as Dictionary<string, object>;
                            if (sd == null) continue;
                            State.splotches.Add(new Splotch
                            {
                                pos = new Point(Convert.ToDouble(sd["x"]), Convert.ToDouble(sd["y"])),
                                colorIndex = Convert.ToInt32(sd["c"]),
                                // "a" = synced rotation (default random if absent); "t" = original timestamp
                                angle = sd.ContainsKey("a") ? Convert.ToDouble(sd["a"]) : m_random.NextDouble() * Math.PI * 2.0,
                                startTimeMs = Convert.ToInt64(sd["t"])
                            });
                        }
                    }
                }
                State.splotchGeneration++;
                return;
            }

            if (op == "clear_splotches")
            {
                State.splotches.Clear();
                State.splotchGeneration++;
                return;
            }

            if (op == "match_result" && data != null)
            {
                int round = Convert.ToInt32(data["round"]);
                bool first = data.ContainsKey("first") && data["first"] is bool fb && fb;
                if (first) _pendingMatchResult = new List<Dictionary<string, object>>();

                if (data.ContainsKey("e"))
                {
                    var arr = data["e"] as object[];
                    if (arr != null)
                        foreach (var item in arr)
                            _pendingMatchResult.Add(item as Dictionary<string, object>);
                }

                bool last = data.ContainsKey("last") && data["last"] is bool lb && lb;
                if (last) ApplyMatchResult(round, _pendingMatchResult);
                return;
            }

            if (op == "relay_ping" && data != null)
            {
                int ping = Convert.ToInt32(data["ping"]);
                var senderCxId = m_bcWrapper.RelayService.GetCxIdForNetId(netId);
                foreach (var member in State.lobby?.members ?? new List<User>())
                    if (member.cxId == senderCxId) { member.activePing = ping; break; }
                return;
            }

            // --- Per-player ops (move, shockwave) ---

            var memberCxId = m_bcWrapper.RelayService.GetCxIdForNetId(netId);
            if (memberCxId == null) return; // netId not yet mapped (e.g. JIP race)
            foreach (var member in State.lobby.members)
            {
                if (member.cxId != memberCxId) continue;

                if (op == "move" && data != null)
                {
                    member.isAlive = true;
                    member.pos = new Point(Convert.ToDouble(data["x"]), Convert.ToDouble(data["y"]));
                }
                else if (op == "shockwave" && data != null)
                {
                    // Use the sender's synced rotation (default random if an older client omits it)
                    double rAngle = data.ContainsKey("angle") ? Convert.ToDouble(data["angle"]) : m_random.NextDouble() * Math.PI * 2.0;
                    AddShockwaveAndSplotch(
                        new Point(Convert.ToDouble(data["x"]), Convert.ToDouble(data["y"])),
                        member.colorIndex,
                        rAngle);
                }
                break;
            }
        }

        void OnRelaySystemMessage(string jsonResponse)
        {
            var json = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var op = json["op"] as string;
            Console.WriteLine($"[SYS] op={op}");

            if (op == "DISCONNECT")
            {
                var cxId = json.ContainsKey("cxId") ? json["cxId"] as string : null;
                Console.WriteLine($"[SYS] DISCONNECT cxId={cxId}");
                foreach (var member in State.lobby?.members ?? new List<User>())
                {
                    if (member.cxId == cxId) { member.isAlive = false; break; }
                }
            }
            else if (op == "END_MATCH")
            {
                Console.WriteLine("[SYS] END_MATCH — resetting round state, queueing relay disconnect");
                // Arm the disconnecting guard immediately — the relay server closes the WebSocket
                // right after broadcasting END_MATCH, so ConnectFailure can arrive in the same
                // RunCallbacks batch as this System event.  Setting the flag here (rather than
                // waiting for _pendingEndMatch to fire next frame) ensures DieWithMessage is
                // suppressed even in that race window.
                _isRelayDisconnecting = true;
                OnMatchEnded();
                Console.WriteLine("[SYS] END_MATCH done — screen=MatchSummary _pendingEndMatch=true");
            }
            else if (op == "CONNECT")
            {
                var newCxId = json.ContainsKey("cxId") ? json["cxId"] as string : null;
                Console.WriteLine($"[SYS] CONNECT cxId={newCxId} isHost={State.lobby?.ownerCxId == State.user?.cxId}");
                // Host re-syncs game state to a joining-in-progress player
                if (State.lobby?.ownerCxId == State.user?.cxId && newCxId != null)
                {
                    short newNetId = m_bcWrapper.RelayService.GetNetIdForCxId(newCxId);
                    if (newNetId >= 0 && newNetId < 64)
                    {
                        ulong playerMask = 1ul << newNetId;
                        SendGameStart(playerMask);
                        SendSplotchSync(playerMask);
                    }
                }
            }
            else
            {
                Console.WriteLine($"[SYS] unhandled op={op}");
            }
        }
    }
}
