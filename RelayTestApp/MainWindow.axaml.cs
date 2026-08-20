using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace RelayTestApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;

        // Chat: which sub-tab is active in the Lobby's Chat tab.
        private string _chatSubTab = "lobby";

        // Leaderboard: board (0=points,1=coverage) x period (0=lifetime,1=quarterly) toggle.
        private int _lbBoard = 0;
        private int _lbPeriod = 0;
        private int _lbFetchedKey = -1;
        private List<LeaderboardEntry> _lbTop = new List<LeaderboardEntry>();
        private LeaderboardEntry _lbSelf = null;

        // Main Menu's leaderboard/chat tabs are independent of the Lobby's — Avalonia
        // control names are window-scoped, so the two screens can't share named controls.
        private int _lbBoardMenu = 0;
        private int _lbPeriodMenu = 0;
        private int _lbFetchedKeyMenu = -1;
        private List<LeaderboardEntry> _lbTopMenu = new List<LeaderboardEntry>();
        private LeaderboardEntry _lbSelfMenu = null;

        public MainWindow()
        {
            InitializeComponent();
            lblVersionApp.Text = "App:    " + State.app.GetAppVersion();
            lblVersionClient.Text = "Client: ...";
            lblVersionServer.Text = "Server: ...";

            txtUsername.Text = Settings.username;
            txtPassword.Text = Settings.password;
            cboProtocol.SelectedIndex = Settings.protocol switch
            {
                BrainCloud.RelayConnectionType.WEBSOCKET => 0,
                BrainCloud.RelayConnectionType.TCP => 1,
                BrainCloud.RelayConnectionType.UDP => 2,
                _ => 0
            };
            chkUsePingData.IsChecked = Settings.usePingData;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) =>
            {
                State.app.Update();
                if (State.screenState == ScreenState.Lobby)
                    UpdateInfoTimeInLobby();
            };
            _timer.Start();

            State.app.CheckReconnect();
        }

        // --- Screen navigation ---

        public void ShowScreen(ScreenState screen)
        {
            screenLogin.IsVisible = screen == ScreenState.Login;
            screenLoggingIn.IsVisible = screen == ScreenState.LoggingIn;
            screenMainMenu.IsVisible = screen == ScreenState.MainMenu;
            screenJoiningLobby.IsVisible = screen == ScreenState.JoiningLobby;
            screenLobby.IsVisible = screen == ScreenState.Lobby;
            screenStarting.IsVisible = screen == ScreenState.Starting;
            screenGame.IsVisible = screen == ScreenState.Game;
            screenMatchSummary.IsVisible = screen == ScreenState.MatchSummary;

            mnuLogOut.IsEnabled = screen != ScreenState.Login && screen != ScreenState.LoggingIn;
            mnuLeave.IsEnabled = screen == ScreenState.Lobby || screen == ScreenState.Starting
                || screen == ScreenState.Game || screen == ScreenState.MatchSummary;

            if (screen == ScreenState.Login)
                lblError.IsVisible = false;

            if (screen == ScreenState.JoiningLobby)
            {
                lblLobbyStatus.Text = "";
            }

            if (screen == ScreenState.MainMenu)
            {
                UpdateChatMenu();
                FetchLeaderboardIfNeededMenu();
            }
        }

        public void SetClientVersion(string clientVersion)
        {
            lblVersionClient.Text = "Client: " + clientVersion;
        }

        public void SetServerVersion(string serverVersion)
        {
            lblVersionServer.Text = "Server: " + serverVersion;
        }

        public void ShowError(string message)
        {
            lblError.Text = message;
            lblError.IsVisible = true;
        }

        public bool GetRememberMeStatus() => chkRememberMe.IsChecked == true;

        // --- Lobby status (joining screen) ---

        public void UpdateLobbyStatus(string text)
        {
            lblLobbyStatus.Text = text;
        }

        public void UpdateLobbyTimer(long elapsedMs)
        {
            int totalSec = (int)(elapsedMs / 1000);
            lblSearchTimer.Text = $"{totalSec / 60}:{totalSec % 60:D2}";
        }

        public void UpdateStartingStatus(string subStatus)
        {
            lblStartingSubStatus.Text = subStatus;
        }

        public void UpdateStartingTimer(long elapsedMs)
        {
            int totalSec = (int)(elapsedMs / 1000);
            lblStartingTimer.Text = $"{totalSec / 60}:{totalSec % 60:D2}";
        }

        public void UpdateStartingLobbyId(string lobbyId)
        {
            lblStartingLobbyId.Text = lobbyId;
        }

        public void UpdateMainMenu()
        {
            cboLobbyType.Items.Clear();
            foreach (var lt in State.appLobbies)
                cboLobbyType.Items.Add(new Avalonia.Controls.ComboBoxItem { Content = lt });

            if (cboLobbyType.Items.Count == 0)
                cboLobbyType.Items.Add(new Avalonia.Controls.ComboBoxItem { Content = "CursorPartyV2" });

            // Select saved lobby type if present
            for (int i = 0; i < cboLobbyType.Items.Count; i++)
            {
                if (cboLobbyType.Items[i] is Avalonia.Controls.ComboBoxItem item &&
                    item.Content?.ToString() == Settings.lobbyType)
                {
                    cboLobbyType.SelectedIndex = i;
                    return;
                }
            }
            cboLobbyType.SelectedIndex = 0;
        }

        // --- Lobby ---

        public void UpdateLobby()
        {
            lblLobbyId.Text = "Lobby: " + (State.lobby?.lobbyId ?? "");
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            btnStart.IsVisible = isHost;
            btnReadyToggle.IsVisible = !isHost;
            if (!isHost && State.user != null)
                btnReadyToggle.Content = State.user.isReady ? "Not Ready" : "Ready Up";

            panelLobbyMembers.Children.Clear();
            if (State.lobby != null)
                foreach (var member in State.lobby.members)
                    panelLobbyMembers.Children.Add(BuildLobbyMemberRow(member));

            // Ping data table — only shown when usePingData is enabled
            panelPingData.Children.Clear();
            if (Settings.usePingData && State.lobby != null)
            {
                // Region quality label — derive region from lobbyId prefix (format: "region:Type:N")
                string lobbyIdStr = State.lobby.lobbyId ?? "";
                int colonPos = lobbyIdStr.IndexOf(':');
                string lobbyRegion = (colonPos > 0 && !System.Text.RegularExpressions.Regex.IsMatch(
                    lobbyIdStr[..colonPos], @"^\d+$")) ? lobbyIdStr[..colonPos] : "";
                if (lobbyRegion.Length > 0 && State.pingData.Count > 0)
                {
                    int bestPing = int.MaxValue;
                    foreach (var ms in State.pingData.Values) if (ms < bestPing) bestPing = ms;
                    bool isGood = State.pingData.TryGetValue(lobbyRegion, out int lobbyPing)
                        && (lobbyPing - bestPing) <= 30;
                    panelPingData.Children.Add(new TextBlock
                    {
                        Text = "Region: " + lobbyRegion,
                        Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(isGood ? "#44EE44" : "#EE4444")),
                        FontSize = 11,
                        Margin = new Avalonia.Thickness(0, 0, 0, 2)
                    });
                }

                var regionSet = new SortedSet<string>(State.pingData.Keys);
                foreach (var m in State.lobby.members)
                    foreach (var r in m.pings.Keys) regionSet.Add(r);

                if (regionSet.Count > 0)
                {
                    var regions = regionSet.ToList();
                    var monoFont = new Avalonia.Media.FontFamily("Courier New, Consolas, monospace");
                    var dimBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#8899AA"));

                    var header = "Player".PadRight(18) + string.Concat(regions.Select(r => r.PadRight(16)));
                    panelPingData.Children.Add(new TextBlock { Text = header, FontFamily = monoFont, FontSize = 11, Foreground = dimBrush });

                    foreach (var member in State.lobby.members)
                    {
                        var pings = member.pings.Count > 0 ? member.pings
                            : (member.cxId == State.user?.cxId && State.pingData.Count > 0 ? State.pingData : null);
                        if (pings == null) continue;

                        string nameCol = member.name;
                        if (member.cxId == State.lobby.ownerCxId) nameCol += " [H]";
                        var row = nameCol.PadRight(18) + string.Concat(regions.Select(r =>
                        {
                            if (!pings.TryGetValue(r, out int ms)) return "-".PadRight(16);
                            return (ms >= 999 ? "T/O" : ms.ToString()).PadRight(16);
                        }));

                        bool isMe = member.cxId == State.user?.cxId;
                        var brush = isMe
                            ? new SolidColorBrush(CursorColor.COLORS[member.colorIndex % CursorColor.COLORS.Length])
                            : (IBrush)dimBrush;
                        panelPingData.Children.Add(new TextBlock { Text = row, FontFamily = monoFont, FontSize = 11, Foreground = brush });
                    }
                }
            }

            UpdateChat();
            UpdateInfoTab();
            FetchLeaderboardIfNeeded();
        }

        // A member row: colour swatch (popup on own row only, via ShowColorPickerFlyout),
        // name, YOU/HOST badges, ready status — replaces the old always-visible colour
        // grid + plain WrapPanel name list.
        Control BuildLobbyMemberRow(User member)
        {
            bool isMe = member.cxId == State.user?.cxId;
            bool isHostMember = member.cxId == State.lobby?.ownerCxId;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*"), Margin = new Avalonia.Thickness(0, 2) };

            var swatch = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(11),
                Background = new SolidColorBrush(CursorColor.COLORS[member.colorIndex % CursorColor.COLORS.Length]),
                IsEnabled = isMe,
                Cursor = isMe ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : Avalonia.Input.Cursor.Default
            };
            if (isMe)
                swatch.Click += (_, _) => ShowColorPickerFlyout(swatch);
            Grid.SetColumn(swatch, 0);
            row.Children.Add(swatch);

            var infoStack = new StackPanel { Spacing = 1, Margin = new Avalonia.Thickness(8, 0, 0, 0) };
            var nameLine = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            nameLine.Children.Add(new TextBlock { Text = member.name, FontWeight = FontWeight.Bold });
            if (isMe)
                nameLine.Children.Add(BuildBadge("YOU", "#335588"));
            if (isHostMember)
                nameLine.Children.Add(BuildBadge("HOST", "#885522"));
            infoStack.Children.Add(nameLine);
            infoStack.Children.Add(new TextBlock
            {
                Text = member.isReady ? "Ready" : "Not ready",
                FontSize = 11,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(member.isReady ? "#66EE88" : "#8899AA"))
            });
            Grid.SetColumn(infoStack, 1);
            row.Children.Add(infoStack);

            return row;
        }

        Control BuildBadge(string text, string hexColor)
        {
            return new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse(hexColor)),
                Padding = new Avalonia.Thickness(4, 0),
                Child = new TextBlock { Text = text, FontSize = 9, Foreground = Brushes.White, FontWeight = FontWeight.Bold }
            };
        }

        // Opens a colour-picker Flyout anchored on the given swatch — the popup analog
        // (Avalonia's closest equivalent to a Swing JPopupMenu / ImGui popup / Godot
        // PopupPanel) replacing the old always-visible 4x10 colour grid.
        void ShowColorPickerFlyout(Control anchor)
        {
            var grid = new Grid();
            for (int r = 0; r < 4; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < 10; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var flyout = new Flyout();
            for (int i = 0; i < CursorColor.COLORS.Length; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Width = 22,
                    Height = 22,
                    Padding = new Avalonia.Thickness(0),
                    Margin = new Avalonia.Thickness(1),
                    Background = new SolidColorBrush(CursorColor.COLORS[i])
                };
                btn.Click += (_, _) =>
                {
                    State.app.ChangeUserColor(idx);
                    flyout.Hide();
                };
                Grid.SetRow(btn, i / 10);
                Grid.SetColumn(btn, i % 10);
                grid.Children.Add(btn);
            }
            flyout.Content = grid;
            flyout.ShowAt(anchor);
        }

        // --- Chat ---

        void btnChatSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string tag) _chatSubTab = tag;
            UpdateChat();
        }

        void txtChatInput_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter) SendChatInput();
        }

        void btnChatSend_Click(object sender, RoutedEventArgs e) => SendChatInput();

        void SendChatInput()
        {
            string text = txtChatInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (_chatSubTab == "global") State.app.SendGlobalChatMessage(text);
            else State.app.SendLobbySignalChat(text);
            txtChatInput.Text = "";
        }

        public void UpdateChat()
        {
            var messages = _chatSubTab == "global" ? State.chatMessagesGlobal : (State.lobby?.chatMessages ?? new List<ChatMessage>());
            var sb = new System.Text.StringBuilder();
            foreach (var m in messages) sb.AppendLine($"{m.fromName}: {m.text}");
            lblChatLog.Text = sb.ToString();
            scrollChatLog.ScrollToEnd();

            // App.cs only ever calls UpdateChat() (not UpdateChatMenu()) when new global
            // messages arrive, since it has no reason to know which screen is showing —
            // keep the Main Menu's independent chat log in sync here too.
            UpdateChatMenu();
        }

        // --- Leaderboards ---

        void btnLbBoard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string s && int.TryParse(s, out int v)) _lbBoard = v;
            FetchLeaderboardIfNeeded();
        }

        void btnLbPeriod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string s && int.TryParse(s, out int v)) _lbPeriod = v;
            FetchLeaderboardIfNeeded();
        }

        // Session-cached per (board,period) combo, so switching tabs back and forth
        // re-shows cached results rather than re-fetching every time.
        void FetchLeaderboardIfNeeded()
        {
            if (State.screenState != ScreenState.Lobby) return;
            int key = _lbBoard * 2 + _lbPeriod;
            if (_lbFetchedKey == key) { RenderLeaderboard(); return; }
            _lbFetchedKey = key;
            bool coverage = _lbBoard == 1;
            bool quarterly = _lbPeriod == 1;
            State.app.FetchLeaderboard(coverage, quarterly,
                top => { _lbTop = top; Dispatcher.UIThread.Post(RenderLeaderboard); },
                self => { _lbSelf = self; Dispatcher.UIThread.Post(RenderLeaderboard); });
            RenderLeaderboard(); // shows "Loading..." immediately
        }

        string FormatBoardScore(long v) => _lbBoard == 1 ? $"{v / 100.0:0.0}%" : v.ToString("N0");

        void RenderLeaderboard()
        {
            panelLeaderboard.Children.Clear();
            if (_lbFetchedKey < 0)
            {
                panelLeaderboard.Children.Add(new TextBlock { Text = "Loading...", Foreground = Brushes.Gray });
                return;
            }
            if (_lbTop.Count == 0)
            {
                panelLeaderboard.Children.Add(new TextBlock { Text = "No scores yet", Foreground = Brushes.Gray });
                return;
            }
            foreach (var e in _lbTop)
                panelLeaderboard.Children.Add(new TextBlock { Text = $"{e.rank}. {e.name}   {FormatBoardScore(e.score)}" });

            bool selfInTop = _lbSelf != null && _lbTop.Exists(e => e.rank == _lbSelf.rank);
            if (_lbSelf != null && !selfInTop)
            {
                panelLeaderboard.Children.Add(new TextBlock { Text = "...", Foreground = Brushes.Gray });
                panelLeaderboard.Children.Add(new TextBlock
                {
                    Text = $"{_lbSelf.rank}. {_lbSelf.name} (you)   {FormatBoardScore(_lbSelf.score)}",
                    Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#66CCFF"))
                });
            }
        }

        // --- Main Menu: Leaderboard tab (independent of the Lobby's, see field comment) ---

        void btnLbBoardMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string s && int.TryParse(s, out int v)) _lbBoardMenu = v;
            FetchLeaderboardIfNeededMenu();
        }

        void btnLbPeriodMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string s && int.TryParse(s, out int v)) _lbPeriodMenu = v;
            FetchLeaderboardIfNeededMenu();
        }

        void FetchLeaderboardIfNeededMenu()
        {
            if (State.screenState != ScreenState.MainMenu) return;
            int key = _lbBoardMenu * 2 + _lbPeriodMenu;
            if (_lbFetchedKeyMenu == key) { RenderLeaderboardMenu(); return; }
            _lbFetchedKeyMenu = key;
            bool coverage = _lbBoardMenu == 1;
            bool quarterly = _lbPeriodMenu == 1;
            State.app.FetchLeaderboard(coverage, quarterly,
                top => { _lbTopMenu = top; Dispatcher.UIThread.Post(RenderLeaderboardMenu); },
                self => { _lbSelfMenu = self; Dispatcher.UIThread.Post(RenderLeaderboardMenu); });
            RenderLeaderboardMenu(); // shows "Loading..." immediately
        }

        string FormatBoardScoreMenu(long v) => _lbBoardMenu == 1 ? $"{v / 100.0:0.0}%" : v.ToString("N0");

        void RenderLeaderboardMenu()
        {
            panelLeaderboardMenu.Children.Clear();
            if (_lbFetchedKeyMenu < 0)
            {
                panelLeaderboardMenu.Children.Add(new TextBlock { Text = "Loading...", Foreground = Brushes.Gray });
                return;
            }
            if (_lbTopMenu.Count == 0)
            {
                panelLeaderboardMenu.Children.Add(new TextBlock { Text = "No scores yet", Foreground = Brushes.Gray });
                return;
            }
            foreach (var e in _lbTopMenu)
                panelLeaderboardMenu.Children.Add(new TextBlock { Text = $"{e.rank}. {e.name}   {FormatBoardScoreMenu(e.score)}" });

            bool selfInTop = _lbSelfMenu != null && _lbTopMenu.Exists(e => e.rank == _lbSelfMenu.rank);
            if (_lbSelfMenu != null && !selfInTop)
            {
                panelLeaderboardMenu.Children.Add(new TextBlock { Text = "...", Foreground = Brushes.Gray });
                panelLeaderboardMenu.Children.Add(new TextBlock
                {
                    Text = $"{_lbSelfMenu.rank}. {_lbSelfMenu.name} (you)   {FormatBoardScoreMenu(_lbSelfMenu.score)}",
                    Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#66CCFF"))
                });
            }
        }

        // --- Main Menu: Chat tab (global only — no lobby exists yet) ---

        void txtChatInputMenu_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter) SendChatInputMenu();
        }

        void btnChatSendMenu_Click(object sender, RoutedEventArgs e) => SendChatInputMenu();

        void SendChatInputMenu()
        {
            string text = txtChatInputMenu.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            State.app.SendGlobalChatMessage(text);
            txtChatInputMenu.Text = "";
        }

        public void UpdateChatMenu()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in State.chatMessagesGlobal) sb.AppendLine($"{m.fromName}: {m.text}");
            lblChatLogMenu.Text = sb.ToString();
            scrollChatLogMenu.ScrollToEnd();
        }

        // --- Info tab ---

        void UpdateInfoTab()
        {
            string lobbyIdStr = State.lobby?.lobbyId ?? "";
            int colonPos = lobbyIdStr.IndexOf(':');
            string region = colonPos > 0 ? lobbyIdStr[..colonPos] : "-";
            lblInfoLobbyId.Text = "Lobby: " + lobbyIdStr;
            lblInfoRegion.Text = "Region: " + region;
            lblInfoPlayers.Text = "Players: " + (State.lobby?.members.Count ?? 0);
            UpdateInfoTimeInLobby();

            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            lblInfoStatus.Text = !string.IsNullOrEmpty(State.lobbyStatusText) ? State.lobbyStatusText
                : (isHost && State.user?.isReady != true ? "Press Start when ready." : "Waiting for host to start...");
        }

        void UpdateInfoTimeInLobby()
        {
            if (State.lobbyJoinedAtMs == 0) return;
            long elapsedSec = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - State.lobbyJoinedAtMs) / 1000;
            lblInfoTimeInLobby.Text = $"Time in lobby: {elapsedSec / 60:D2}:{elapsedSec % 60:D2}";
        }

        // --- Game viewport ---

        public void UpdateGameViewport()
        {
            panelSendMask.Children.Clear();
            foreach (var user in State.lobby.members)
            {
                var cb = new CheckBox
                {
                    Content = user.name,
                    Foreground = new SolidColorBrush(CursorColor.COLORS[user.colorIndex % CursorColor.COLORS.Length]),
                    IsChecked = user.allowSendTo,
                    Margin = new Avalonia.Thickness(0, 0, 8, 0)
                };
                var captured = user;
                cb.IsCheckedChanged += (_, _) => captured.allowSendTo = cb.IsChecked == true;
                panelSendMask.Children.Add(cb);
            }
            UpdateCoverageSidebar();
        }

        public void UpdateEffects()
        {
            var now = DateTime.Now;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = State.shockwaves.Count - 1; i >= 0; i--)
                if ((now - State.shockwaves[i].startTime).TotalMilliseconds >= 1000.0)
                    State.shockwaves.RemoveAt(i);

            if (State.splotchDurationSec > 0)
            {
                long lifeMs = State.splotchDurationSec * 1000L;
                for (int i = State.splotches.Count - 1; i >= 0; i--)
                    if (nowMs - State.splotches[i].startTimeMs >= lifeMs)
                        State.splotches.RemoveAt(i);
            }

            UpdateGameTimer(nowMs);
            UpdatePing();

            viewport.InvalidateVisual();
        }

        // Matches cpp's TIMER_URGENT_SEC/TIMER_WARN_SEC thresholds for the countdown colour.
        const int TimerUrgentSec = 10;
        const int TimerWarnSec = 30;

        void UpdateGameTimer(long nowMs)
        {
            if (State.gameStartTime == 0)
            {
                lblGameTimer.Text = "Waiting...";
                lblGameTimer.Foreground = Brushes.Gray;
                return;
            }

            int elapsed = (int)((nowMs - State.gameStartTime) / 1000);
            int remaining = Math.Max(0, GameApp.MATCH_DURATION_SEC - elapsed);
            // The host broadcasts match_result at MATCH_DURATION_SEC but doesn't actually
            // call EndMatch() until RESULT_GRACE_SEC later — gameplay (and this timer) stays
            // live for that extra window instead of a single hard cutoff.
            int totalSec = GameApp.MATCH_DURATION_SEC + GameApp.RESULT_GRACE_SEC;

            if (elapsed >= totalSec)
            {
                lblGameTimer.Text = "Match Over";
                lblGameTimer.Foreground = Brushes.Red;
            }
            else if (elapsed >= GameApp.MATCH_DURATION_SEC)
            {
                lblGameTimer.Text = "Finalizing...";
                lblGameTimer.Foreground = Brushes.Red;
            }
            else
            {
                lblGameTimer.Text = $"{remaining / 60:D2}:{remaining % 60:D2}";
                lblGameTimer.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(
                    remaining <= TimerUrgentSec ? "#EE5555"
                    : remaining <= TimerWarnSec ? "#EEAA44"
                    : "#FFFFFF"));
            }
        }

        void UpdatePing()
        {
            if (State.lobby == null) return;
            int ping = -1;
            foreach (var m in State.lobby.members)
                if (m.cxId == State.user?.cxId) { ping = m.activePing; break; }

            string text = ping < 0 ? "Ping: ..." : ping >= 999 ? "Ping: T/O" : $"Ping: {ping} ms";
            string hex = ping < 0 ? "#8899AA" : ping < 100 ? "#66EE88" : ping < 200 ? "#EECC44" : "#EE6666";
            lblGamePing.Text = "● " + text;
            lblGamePing.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(hex));
        }

        // Live RANK / PLAYER / COVERAGE board — replaces the old debug-only HUD sidebar.
        public void UpdateCoverageSidebar()
        {
            panelCoverageSidebar.Children.Clear();
            if (State.lobby == null) return;

            var entries = State.coverage.Count > 0 ? State.coverage : Coverage.Compute(State.splotches, State.lobby.members);
            foreach (var entry in entries)
            {
                bool isMe = entry.cxId == State.user?.cxId;
                string name = "?";
                foreach (var m in State.lobby.members) if (m.cxId == entry.cxId) { name = m.name; break; }

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*,Auto") };

                var lblRank = new TextBlock
                {
                    Text = $"#{entry.rank}",
                    Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#EEAA44")),
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetColumn(lblRank, 0);
                grid.Children.Add(lblRank);

                var lblName = new TextBlock
                {
                    Text = name + (isMe ? "  YOU" : ""),
                    Foreground = isMe ? new SolidColorBrush(Avalonia.Media.Color.Parse("#66EE88")) : Brushes.White,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetColumn(lblName, 1);
                grid.Children.Add(lblName);

                var lblPct = new TextBlock
                {
                    Text = $"{entry.coveragePct:0}%",
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetColumn(lblPct, 2);
                grid.Children.Add(lblPct);

                panelCoverageSidebar.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Avalonia.Media.Color.Parse(isMe ? "#24362D" : "#00000000")),
                    Padding = new Avalonia.Thickness(4, 2),
                    Child = grid
                });
            }
        }

        void btnExitMatch_Click(object sender, RoutedEventArgs e)
        {
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            if (isHost) State.app.EndMatch();
            else State.app.CloseGame();
        }

        // --- Match Summary ---

        public void UpdateMatchSummary()
        {
            int playerCount = State.lobby?.members.Count ?? 0;
            lblSummarySubtitle.Text = $"Lobby {State.lobby?.lobbyId ?? ""} · {playerCount} Players";

            var result = State.matchResult;
            panelSummaryRows.Children.Clear();

            if (!result.valid || result.entries.Count == 0)
            {
                lblSummaryWinner.Text = "Waiting for results...";
            }
            else
            {
                var winner = result.entries[0];
                lblSummaryWinner.Text = $"🏆 {NameForCxId(winner.cxId)} wins the round, covering {winner.coveragePct:0}% of the board.";

                foreach (var e in result.entries)
                    panelSummaryRows.Children.Add(BuildSummaryRow(e));
            }

            // Host: Start has no readiness gate at all, so this button never readies the
            // host up — it's just a shortcut back to the Lobby where Start is waiting.
            // Everyone else: readies up (so the host sees the count build) and returns too.
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            int readyCount = 0;
            int totalCount = State.lobby?.members.Count ?? 0;
            if (State.lobby != null) foreach (var m in State.lobby.members) if (m.isReady) readyCount++;
            btnRematch.Content = isHost ? "Return to Lobby" : $"Queue for Rematch {readyCount}/{totalCount}";
        }

        public void UpdateMatchSummaryCountdown(long elapsedMs)
        {
            long remaining = Math.Max(0, GameApp.MatchSummaryRematchMs - elapsedMs);
            lblSummaryCountdown.Text = $"🕐 Next Round: {remaining / 1000 / 60}:{(remaining / 1000) % 60:D2}";
        }

        string NameForCxId(string cxId)
        {
            if (State.lobby != null) foreach (var m in State.lobby.members) if (m.cxId == cxId) return m.name;
            return "Player";
        }

        int ColorIndexForCxId(string cxId)
        {
            if (State.lobby != null) foreach (var m in State.lobby.members) if (m.cxId == cxId) return m.colorIndex;
            return 0;
        }

        Control BuildSummaryRow(MatchResult.Entry e)
        {
            bool isMe = e.cxId == State.user?.cxId;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,260") };

            var left = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            left.Children.Add(new TextBlock { Text = $"#{e.rank}", Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#EEAA44")), FontWeight = FontWeight.Bold, Width = 30 });
            left.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(CursorColor.COLORS[ColorIndexForCxId(e.cxId) % CursorColor.COLORS.Length]) });
            left.Children.Add(new TextBlock { Text = NameForCxId(e.cxId) + (isMe ? " (you)" : ""), FontWeight = FontWeight.Bold, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var coverageCol = new StackPanel { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            coverageCol.Children.Add(new TextBlock { Text = $"{e.coveragePct:0}%", FontSize = 18, FontWeight = FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
            coverageCol.Children.Add(new TextBlock { Text = "COVERAGE", FontSize = 9, Foreground = Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
            Grid.SetColumn(coverageCol, 1);
            grid.Children.Add(coverageCol);

            var resultCol = new StackPanel { Spacing = 4, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            resultCol.Children.Add(BuildChip($"⏱ +{e.beaten + 1} pts", "#88BBFF", $"{e.beaten} beaten + 1 for playing"));

            if (e.lbDelta.ready)
            {
                bool pointsImproved = e.lbDelta.pointsLifetime.improved || e.lbDelta.pointsQuarterly.improved;
                bool coverageImproved = e.lbDelta.coverageLifetime.improved || e.lbDelta.coverageQuarterly.improved;
                if (pointsImproved)
                    resultCol.Children.Add(BuildChip("↑ Rank up · Opponents Beaten " + PeriodsText(e.lbDelta.pointsLifetime, e.lbDelta.pointsQuarterly), "#66EE88", null));
                if (coverageImproved)
                    resultCol.Children.Add(BuildChip("★ Personal best · Coverage % " + PeriodsText(e.lbDelta.coverageLifetime, e.lbDelta.coverageQuarterly), "#EECC66", null));
                if (!pointsImproved && !coverageImproved)
                    resultCol.Children.Add(BuildChip("No leaderboard rank change this round", "#8899AA", null));
            }
            else
            {
                long elapsedSinceArrival = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - State.matchSummaryArrivalTime;
                string txt = elapsedSinceArrival >= GameApp.LeaderboardTimeoutMs ? "Leaderboard unavailable" : "Updating leaderboards...";
                resultCol.Children.Add(BuildChip(txt, "#8899AA", null));
            }
            Grid.SetColumn(resultCol, 2);
            grid.Children.Add(resultCol);

            return new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse(isMe ? "#1D3025" : "#242833")),
                BorderBrush = isMe ? new SolidColorBrush(Avalonia.Media.Color.Parse("#3C8C53")) : null,
                BorderThickness = new Avalonia.Thickness(isMe ? 1 : 0),
                Padding = new Avalonia.Thickness(10),
                Child = grid
            };
        }

        Control BuildChip(string text, string hexColor, string caption)
        {
            var stack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#242833")),
                Padding = new Avalonia.Thickness(6, 2),
                Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse(hexColor)) }
            });
            if (!string.IsNullOrEmpty(caption))
                stack.Children.Add(new TextBlock { Text = caption, FontSize = 11, Foreground = Brushes.Gray, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            return stack;
        }

        string PeriodsText(MatchResult.PeriodDelta lifetime, MatchResult.PeriodDelta quarterly)
        {
            var parts = new List<string>();
            if (lifetime.improved) parts.Add($"Lifetime #{lifetime.rankBefore}→#{lifetime.rankAfter}");
            if (quarterly.improved) parts.Add($"Quarterly #{quarterly.rankBefore}→#{quarterly.rankAfter}");
            return string.Join(" · ", parts);
        }

        void btnRematch_Click(object sender, RoutedEventArgs e) => State.app.OnContinueFromSummary();
        void btnSummaryMainMenu_Click(object sender, RoutedEventArgs e) => State.app.CloseGame();

        public void SetCursor(User user, Avalonia.Point pos) { /* handled in viewport render */ }

        // --- Menu handlers ---

        void mnuLogOut_Click(object sender, RoutedEventArgs e) => State.app.LogOut();
        void mnuExit_Click(object sender, RoutedEventArgs e) => State.app.Exit();
        void mnuLeave_Click(object sender, RoutedEventArgs e) => State.app.CloseGame();

        // --- Login screen ---

        void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtUsername.Text) || string.IsNullOrEmpty(txtPassword.Text)) return;
            lblError.IsVisible = false;
            Settings.username = txtUsername.Text;
            Settings.password = txtPassword.Text;
            Settings.SaveConfigs();
            State.app.Login(Settings.username, Settings.password);
        }

        // --- Main menu ---

        void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            var protocol = cboProtocol.SelectedIndex switch
            {
                0 => BrainCloud.RelayConnectionType.WEBSOCKET,
                1 => BrainCloud.RelayConnectionType.TCP,
                _ => BrainCloud.RelayConnectionType.UDP
            };
            string lobbyType = (cboLobbyType.SelectedItem is Avalonia.Controls.ComboBoxItem item)
                ? item.Content?.ToString() ?? Settings.lobbyType
                : Settings.lobbyType;
            State.app.Play(protocol, lobbyType);
        }

        // --- Lobby ---

        void btnStart_Click(object sender, RoutedEventArgs e) => State.app.StartGame();
        void btnReadyToggle_Click(object sender, RoutedEventArgs e) => State.app.OnToggleReady();
        void btnLobbyLeave_Click(object sender, RoutedEventArgs e) => State.app.CloseGame();

        // --- Send options ---

        void chkUsePingData_Changed(object sender, RoutedEventArgs e)
        {
            Settings.usePingData = chkUsePingData.IsChecked == true;
            Settings.SaveConfigs();
        }

        void chkSendOption_Changed(object sender, RoutedEventArgs e)
        {
            if (chkReliable == null || chkOrdered == null) return;
            Settings.sendReliable = chkReliable.IsChecked == true;
            Settings.sendOrdered = chkOrdered.IsChecked == true;
        }

        void cboChannels_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb)
                Settings.sendChannel = cb.SelectedIndex;
        }
    }
}
