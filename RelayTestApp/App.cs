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
        const int MATCH_DURATION_SEC = 90;
        const int COUNTDOWN_FROM_SEC = 80;

        BrainCloudWrapper m_bcWrapper;
        string m_appVersion = "";

        // Move throttle — flush at most once per ~16 ms (~60 fps)
        long _lastMoveSendTime = 0;
        bool _pendingMoveSend = false;
        Point _pendingMovePos;

        // Deferred END_MATCH disconnect — cannot safely call Disconnect() from inside a relay callback
        bool _pendingEndMatch = false;

        // Guards DieWithMessage during voluntary relay disconnect (END_MATCH / CloseGame).
        // Stays true from disconnect until next successful relay Connect — mirrors Java's _disconnecting
        // and C++'s isDisconnecting, but kept across the async disconnect-to-reconnect window.
        bool _isRelayDisconnecting = false;

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

                    // Mirror C++ pendingEndMatch:
                    //   Non-host → UpdateReady(true):  auto-ready so the lobby threshold is met as
                    //              soon as the host clicks Start, which fires STARTING for everyone.
                    //   Host     → UpdateReady(false): host controls when the next round begins.
                    if (State.lobby != null && State.user != null)
                    {
                        bool isHost = State.lobby.ownerCxId == State.user.cxId;
                        State.user.isReady = !isHost;
                        var extra = new Dictionary<string, object> { ["colorIndex"] = State.user.colorIndex };
                        m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, extra);
                        Console.WriteLine($"[APP] UpdateReady({State.user.isReady}) sent — isHost={isHost}");
                    }
                }

                if (State.screenState == ScreenState.Game)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    // Flush pending move at 60 fps
                    if (_pendingMoveSend && now - _lastMoveSendTime >= 16)
                        FlushPendingMove();

                    // Update visual effects and clean up expired splotches
                    State.form.UpdateEffects();

                    // Host: auto-end match after MATCH_DURATION_SEC
                    if (State.gameStartTime > 0 &&
                        State.lobby?.ownerCxId == State.user?.cxId)
                    {
                        int elapsed = (int)((now - State.gameStartTime) / 1000);
                        if (elapsed >= MATCH_DURATION_SEC)
                            EndMatch();
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
            m_bcWrapper.RTTService.EnableRTT(OnRTTConnected, OnRTTDisconnected);
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

            State.lobby = null;
            State.server = null;
            State.shockwaves = new List<Shockwave>();
            State.splotches = new List<Splotch>();
            State.gameStartTime = 0;
            State.lobbySearchStartTime = 0;
            State.lobbyStatusStartTime = 0;
            State.mouseX = 0;
            State.mouseY = 0;
            ChangeScreen(ScreenState.MainMenu);
        }

        public void StartGame()
        {
            State.user.isReady = true;
            ChangeScreen(ScreenState.Starting);

            var extra = new Dictionary<string, object> { ["colorIndex"] = State.user.colorIndex };
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, extra);
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
            var extra = new Dictionary<string, object> { ["colorIndex"] = State.user.colorIndex };
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, extra);
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
            var json = new Dictionary<string, object>
            {
                ["op"] = "shockwave",
                ["data"] = new Dictionary<string, object> { ["x"] = pos.X, ["y"] = pos.Y }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));

            ulong playerMask = BuildSendMask();
            m_bcWrapper.RelayService.SendToPlayers(data, playerMask,
                true, false, Settings.sendChannel);

            // Add locally (sender doesn't receive their own relay message)
            AddShockwaveAndSplotch(pos, State.user.colorIndex);
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

        // Host-only: wipe all splotches on every client.
        public void ClearSplotches()
        {
            if (State.lobby?.ownerCxId != State.user?.cxId) return;
            State.splotches.Clear();
            var json = new Dictionary<string, object> { ["op"] = "clear_splotches" };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS,
                true, true, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_2);
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
                State.user.isAlive = false;
                State.user.isReady = false;
                State.shockwaves = new List<Shockwave>();
                State.splotches = new List<Splotch>();
                State.gameStartTime = 0;
                State.lobbyStatusStartTime = 0;
                State.lobbyStatusText = "";
                _pendingMoveSend = false;
                foreach (var member in State.lobby?.members ?? new List<User>())
                {
                    member.isAlive = false;
                    member.isReady = false;
                }
                _pendingEndMatch = true;
                ChangeScreen(ScreenState.Lobby);
                State.form.UpdateLobby();
                return;
            }

            Console.WriteLine($"[APP] DieWithMessage — status={status} reasonCode={reasonCode} | {jsonError}");

            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
            m_bcWrapper.RTTService.DisableRTT();

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

        void OnRTTConnected(string jsonResponse, object cbObject)
        {
            var algo = new Dictionary<string, object>
            {
                ["strategy"] = "ranged-absolute",
                ["alignment"] = "center",
                ["ranges"] = new System.Collections.Generic.List<int> { 1000 }
            };
            State.user.cxId = m_bcWrapper.RTTService.getRTTConnectionID();

            var extra = new Dictionary<string, object> { ["colorIndex"] = State.user.colorIndex };

            m_bcWrapper.LobbyService.FindOrCreateLobby(
                Settings.lobbyType, 0, 1, algo,
                new Dictionary<string, object>(),
                false, extra, "all",
                new Dictionary<string, object>(),
                null, null, DieWithMessage, "Failed to find lobby");
        }

        void OnLobbyEvent(string jsonResponse)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var jsonData = response["data"] as Dictionary<string, object>;

            if (jsonData.ContainsKey("lobby"))
            {
                State.lobby = new Lobby(jsonData["lobby"] as Dictionary<string, object>,
                                        jsonData["lobbyId"] as string);
                if (State.screenState == ScreenState.JoiningLobby)
                    ChangeScreen(ScreenState.Lobby);
                State.form.UpdateLobby();
            }

            if (response.ContainsKey("operation"))
            {
                switch (response["operation"] as string)
                {
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
        void SendSplotchSync(ulong toMask)
        {
            var splotchArray = new List<Dictionary<string, object>>();
            foreach (var s in State.splotches)
            {
                splotchArray.Add(new Dictionary<string, object>
                {
                    ["x"] = s.pos.X,
                    ["y"] = s.pos.Y,
                    ["c"] = s.colorIndex,
                    ["t"] = s.startTimeMs
                });
            }

            var json = new Dictionary<string, object>
            {
                ["op"] = "splotch_sync",
                ["data"] = new Dictionary<string, object>
                {
                    ["first"] = true,
                    ["splotches"] = splotchArray.ToArray()
                }
            };
            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.SendToPlayers(data, toMask,
                true, true, BrainCloudRelay.CHANNEL_HIGH_PRIORITY_2);
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

        void AddShockwaveAndSplotch(Point pos, int colorIndex)
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
                startTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
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
                                startTimeMs = Convert.ToInt64(sd["t"])
                            });
                        }
                    }
                }
                return;
            }

            if (op == "clear_splotches")
            {
                State.splotches.Clear();
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
                    AddShockwaveAndSplotch(
                        new Point(Convert.ToDouble(data["x"]), Convert.ToDouble(data["y"])),
                        member.colorIndex);
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

                // Reset per-round state immediately (mirrors Java onGameScreenToLobby / JS onSystemMessage)
                State.user.isAlive = false;
                State.user.isReady = false;
                State.shockwaves = new List<Shockwave>();
                State.splotches = new List<Splotch>();
                State.gameStartTime = 0;
                State.lobbyStatusStartTime = 0;
                State.lobbyStatusText = "";
                _pendingMoveSend = false;
                foreach (var member in State.lobby?.members ?? new List<User>())
                {
                    member.isAlive = false;
                    member.isReady = false;
                }

                // Defer relay disconnect — cannot safely call from inside a relay callback
                _pendingEndMatch = true;
                ChangeScreen(ScreenState.Lobby);
                State.form.UpdateLobby();
                Console.WriteLine("[SYS] END_MATCH done — screen=Lobby _pendingEndMatch=true");
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
