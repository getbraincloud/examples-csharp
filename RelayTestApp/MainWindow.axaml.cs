using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace RelayTestApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private TextBlock[] _lobbyPlayerLabels = new TextBlock[40];

        public MainWindow()
        {
            InitializeComponent();
            SetupColorButtons();
            SetupLobbyPlayerLabels();
            lblVersionApp.Text = "App:    " + State.app.GetAppVersion();
            lblVersionClient.Text = "Client: ...";
            lblVersionServer.Text = "Server: ...";

            txtUsername.Text = Settings.username;
            txtPassword.Text = Settings.password;
            cboProtocol.SelectedIndex = Settings.protocol switch
            {
                BrainCloud.RelayConnectionType.UDP => 0,
                BrainCloud.RelayConnectionType.TCP => 1,
                BrainCloud.RelayConnectionType.WEBSOCKET => 2,
                _ => 0
            };
            chkUsePingData.IsChecked = Settings.usePingData;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => State.app.Update();
            _timer.Start();

            State.app.CheckReconnect();
        }

        void SetupColorButtons()
        {
            // 4 rows × 10 columns grid
            for (int r = 0; r < 4; r++)
                panelColorButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < 10; c++)
                panelColorButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            for (int i = 0; i < CursorColor.COLORS.Length; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Width = 22,
                    Height = 22,
                    Padding = new Avalonia.Thickness(0),
                    Margin = new Avalonia.Thickness(1),
                    Background = new SolidColorBrush(CursorColor.COLORS[i]),
                    Tag = idx
                };
                btn.Click += (_, _) => State.app.ChangeUserColor(idx);
                Grid.SetRow(btn, i / 10);
                Grid.SetColumn(btn, i % 10);
                panelColorButtons.Children.Add(btn);
            }
        }

        void SetupLobbyPlayerLabels()
        {
            for (int i = 0; i < 40; i++)
            {
                var label = new TextBlock { Margin = new Avalonia.Thickness(4, 2), MinWidth = 120 };
                _lobbyPlayerLabels[i] = label;
                panelLobbyPlayers.Children.Add(label);
            }
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

            mnuLogOut.IsEnabled = screen != ScreenState.Login && screen != ScreenState.LoggingIn;
            mnuLeave.IsEnabled = screen == ScreenState.Lobby || screen == ScreenState.Starting || screen == ScreenState.Game;

            if (screen == ScreenState.Login)
                lblError.IsVisible = false;

            if (screen == ScreenState.JoiningLobby)
            {
                lblLobbyStatus.Text = "";
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
            btnStart.IsVisible = State.lobby?.ownerCxId == State.user?.cxId;

            for (int i = 0; i < 40; i++)
            {
                if (State.lobby != null && i < State.lobby.members.Count)
                {
                    var user = State.lobby.members[i];
                    _lobbyPlayerLabels[i].Text = user.name;
                    _lobbyPlayerLabels[i].Foreground = new SolidColorBrush(CursorColor.COLORS[user.colorIndex]);
                    _lobbyPlayerLabels[i].IsVisible = true;
                }
                else
                {
                    _lobbyPlayerLabels[i].IsVisible = false;
                }
            }

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

                    // Header
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
                            ? new SolidColorBrush(CursorColor.COLORS[member.colorIndex])
                            : (IBrush)dimBrush;
                        panelPingData.Children.Add(new TextBlock { Text = row, FontFamily = monoFont, FontSize = 11, Foreground = brush });
                    }
                }
            }
        }

        // --- Game viewport ---

        public void UpdateGameViewport()
        {
            lblGameLobbyId.Text = State.lobby?.lobbyId ?? "";
            panelPlayers.Children.Clear();
            foreach (var user in State.lobby.members)
            {
                string pingText = user.activePing < 0 ? "..." : user.activePing >= 999 ? "T/O" : $"{user.activePing} ms";
                var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
                var cb = new CheckBox
                {
                    Content = user.name,
                    Foreground = new SolidColorBrush(CursorColor.COLORS[user.colorIndex]),
                    IsChecked = user.allowSendTo
                };
                var captured = user;
                cb.IsCheckedChanged += (_, _) => captured.allowSendTo = cb.IsChecked == true;
                var lblPing = new TextBlock
                {
                    Text = pingText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#888888")),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                row.Children.Add(cb);
                row.Children.Add(lblPing);
                panelPlayers.Children.Add(row);
            }

            // Show host-only controls only for the lobby owner
            bool isHost = State.lobby?.ownerCxId == State.user?.cxId;
            panelHostControls.IsVisible = isHost;
        }

        public void UpdateEffects()
        {
            var now = DateTime.Now;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Remove expired shockwaves (1-second animation)
            for (int i = State.shockwaves.Count - 1; i >= 0; i--)
            {
                if ((now - State.shockwaves[i].startTime).TotalMilliseconds >= 1000.0)
                    State.shockwaves.RemoveAt(i);
            }

            // Remove expired splotches (if duration is configured)
            if (State.splotchDurationSec > 0)
            {
                long lifeMs = State.splotchDurationSec * 1000L;
                for (int i = State.splotches.Count - 1; i >= 0; i--)
                {
                    if (nowMs - State.splotches[i].startTimeMs >= lifeMs)
                        State.splotches.RemoveAt(i);
                }
            }

            // Update game timer display
            if (State.gameStartTime > 0)
            {
                int elapsed = (int)((nowMs - State.gameStartTime) / 1000);
                int secsLeft = Math.Max(0, 90 - elapsed);
                lblGameTimer.Text = $"{secsLeft / 60}:{secsLeft % 60:D2}";
            }
            else
            {
                lblGameTimer.Text = "";
            }

            viewport.InvalidateVisual();
        }

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
                0 => BrainCloud.RelayConnectionType.UDP,
                1 => BrainCloud.RelayConnectionType.TCP,
                _ => BrainCloud.RelayConnectionType.WEBSOCKET
            };
            string lobbyType = (cboLobbyType.SelectedItem is Avalonia.Controls.ComboBoxItem item)
                ? item.Content?.ToString() ?? Settings.lobbyType
                : Settings.lobbyType;
            State.app.Play(protocol, lobbyType);
        }

        // --- Lobby ---

        void btnStart_Click(object sender, RoutedEventArgs e) => State.app.StartGame();
        void btnLobbyLeave_Click(object sender, RoutedEventArgs e) => State.app.CloseGame();

        // --- Game: host-only ---

        void btnEndMatch_Click(object sender, RoutedEventArgs e) => State.app.EndMatch();
        void btnClearSplotches_Click(object sender, RoutedEventArgs e) => State.app.ClearSplotches();

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
