using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrainCloud;
using BrainCloud.JsonFx.Json;

namespace RelayTestApp
{
    class App
    {
        BrainCloudWrapper m_bcWrapper;
        bool m_dead = false;

        public App()
        {
        }

        // update brainCloud
        public void Update()
        {
            if (m_bcWrapper != null)
            {
                m_bcWrapper.Update();

                if (State.screenState == ScreenState.Game)
                {
                    // Update shockwaves
                    State.form.UpdateShockwaves();
                }
            }
            if (m_dead)
            {
                m_dead = false;

                // We differ destroying BC because we cannot destroy it within a callback
                UninitBC();
            }
        }

        // Uninitialize brainCloud
        void UninitBC()
        {
            if (m_bcWrapper != null)
            {
                m_bcWrapper.Client.ShutDown();
                m_bcWrapper = null;
            }
        }

        // Logs out the current user and goes back to login screen
        public void LogOut()
        {
            UninitBC();
            ResetState();
        }

        // Shutdowns the application
        public void Exit()
        {
            Application.Exit();
        }

        // Attempt login with the specific username/password
        public void Login(string username, string password)
        {
            InitBC();

            // Show loading screen
            ChangeScreen(ScreenState.LoggingIn);

            // Authenticate with brainCloud
            m_bcWrapper.AuthenticateUniversal(username, password, true, HandlePlayerState, DieWithMessage, "Login Failed");
        }

        // Find lobby
        public void Play(BrainCloud.RelayConnectionType protocol)
        {
            Settings.protocol = protocol;
            State.user.colorIndex = Settings.colorIndex;

            // Show loading screen
            ChangeScreen(ScreenState.JoiningLobby);

            // Enable RTT
            m_bcWrapper.RTTService.RegisterRTTLobbyCallback(OnLobbyEvent);
            m_bcWrapper.RTTService.EnableRTT(BrainCloud.RTTConnectionType.WEBSOCKET, OnRTTConnected, OnRTTDisconnected);
        }

        // Cleanly close the game. Go back to main menu but don't log 
        public void CloseGame()
        {
            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
            m_bcWrapper.RTTService.DisableRTT();

            // Reset state but keep the user around
            State.lobby = null;
            State.server = null;
            State.shockwaves = new List<Shockwave>();
            State.mouseX = 0;
            State.mouseY = 0;
            ChangeScreen(ScreenState.MainMenu);
        }

        // Ready up and signals RTT service we can start the game
        public void StartGame()
        {
            State.user.isReady = true;
            ChangeScreen(ScreenState.Starting);

            //
            var extra = new Dictionary<string, object>();
            extra["colorIndex"] = State.user.colorIndex;

            //
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, extra);
        }

        // User changes his player color
        public void ChangeUserColor(int colorIndex)
        {
            State.user.colorIndex = colorIndex;
            foreach (var member in State.lobby.members)
            {
                if (State.user.id == member.id)
                {
                    member.colorIndex = colorIndex;
                    break;
                }
            }

            //
            var extra = new Dictionary<string, object>();
            extra["colorIndex"] = State.user.colorIndex;

            //
            m_bcWrapper.LobbyService.UpdateReady(State.lobby.lobbyId, State.user.isReady, extra);
        }

        // User moved mouse in the play area
        public void MouseMoved(Point pos)
        {
            State.user.isAlive = true;
            State.user.pos = pos;
            User myUser = null;
            foreach (var user in State.lobby.members)
            {
                if (State.user.id == user.id)
                {
                    user.isAlive = true;
                    user.pos = pos;
                    myUser = user;
                    break;
                }
            }

            // Send to other players
            Dictionary<string, object> jsonData = new Dictionary<string, object>();
            jsonData["x"] = pos.X;
            jsonData["y"] = pos.Y;

            Dictionary<string, object> json = new Dictionary<string, object>();
            json["op"] = "move";
            json["data"] = jsonData;

            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS, Settings.sendReliable, Settings.sendOrdered, Settings.sendChannel);

            // Move our own sprite
            State.form.SetCursor(myUser, pos);
        }

        // User clicked mouse in the play area
        public void Shockwave(Point pos)
        {
            // Send to other players
            Dictionary<string, object> jsonData = new Dictionary<string, object>();
            jsonData["x"] = pos.X;
            jsonData["y"] = pos.Y;

            Dictionary<string, object> json = new Dictionary<string, object>();
            json["op"] = "shockwave";
            json["data"] = jsonData;

            byte[] data = Encoding.ASCII.GetBytes(JsonWriter.Serialize(json));
            m_bcWrapper.RelayService.Send(data, BrainCloudRelay.TO_ALL_PLAYERS, 
                true, // Reliable
                false, // Unordered
                Settings.sendChannel);

            // Create a local shockwave so we can see it
            var shockwave = new Shockwave();
            shockwave.pos = pos;
            shockwave.colorIndex = State.user.colorIndex;
            shockwave.startTime = DateTime.Now;
            State.shockwaves.Add(shockwave);
       }

        // Initialize brainCloud
        void InitBC()
        {
            if (m_bcWrapper == null)
            {
                m_bcWrapper = new BrainCloudWrapper("RelayTestApp");
            }
            m_dead = false;

            string url = "";
            string appId = "";
            string appSecret = "";
            using (var reader = new StreamReader("ids.txt"))
            {
                Console.WriteLine("Found ids.txt");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("serverUrl="))
                    {
                        url = line.Substring(("serverUrl=").Length);
                        url.Trim();
                    }
                    else if (line.StartsWith("appId="))
                    {
                        appId = line.Substring(("appId=").Length);
                        appId.Trim();
                    }
                    else if (line.StartsWith("secret="))
                    {
                        appSecret = line.Substring(("secret=").Length);
                        appSecret.Trim();
                    }
                }
            }

            m_bcWrapper.Init(url, appSecret, appId, "1.0");

            m_bcWrapper.Client.EnableLogging(true);
        }

        // User authenticated, handle the result
        void HandlePlayerState(string jsonResponse, object cbObject)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var data = response["data"] as Dictionary<string, object>;

            State.user = new User();
            State.user.id = data["profileId"] as string;

            // If no username is set for this user, ask for it
            if (!data.ContainsKey("playerName"))
            {
                SubmitName(Settings.username);
            }
            else
            {
                State.user.name = data["playerName"] as string;
                OnLoggedIn(jsonResponse, cbObject);
            }
        }

        void ChangeScreen(ScreenState screen)
        {
            State.screenState = screen;
            State.form.screens.SelectedTab = State.form.screens.TabPages[(int)State.screenState];
            State.form.UpdateMenuStates();
        }

        // User fully logged in. Enable RTT and listen for chat messages
        void OnLoggedIn(string jsonResponse, object cbObject)
        {
            // Go to main menu screen
            ChangeScreen(ScreenState.MainMenu);
        }

        // Submit user name to brainCloud to be assosiated with the current user
        void SubmitName(string username)
        {
            State.user.name = username;

            // Update name
            m_bcWrapper.PlayerStateService.UpdateUserName(username, OnLoggedIn, DieWithMessage, "Failed to update username to braincloud");
        }

        void OnRTTDisconnected(int status, int reasonCode, string jsonError, object cbObject)
        {
            if (jsonError == "DisableRTT Called") return; // Ignore
            DieWithMessage(status, reasonCode, jsonError, cbObject);
        }

        // Go back to login screen, with an error message
        void DieWithMessage(int status, int reasonCode, string jsonError, object cbObject)
        {
            if (m_dead) return;

            m_dead = true;

            m_bcWrapper.RelayService.DeregisterRelayCallback();
            m_bcWrapper.RelayService.DeregisterSystemCallback();
            m_bcWrapper.RelayService.Disconnect();
            m_bcWrapper.RTTService.DeregisterAllRTTCallbacks();
            m_bcWrapper.RTTService.DisableRTT();

            string message = cbObject as string;
            MessageBox.Show(message + ": " + jsonError);
            ResetState();
        }

        // Reset application state, back to login screen
        void ResetState()
        {
            State.user = null;
            State.lobby = null;
            State.server = null;
            State.shockwaves = new List<Shockwave>();
            State.mouseX = 0;
            State.mouseY = 0;
            ChangeScreen(ScreenState.Login);
        }

        // RTT connected. Try to create or join a lobby
        void OnRTTConnected(string jsonResponse, object cbObject)
        {
            // Find lobby
            var algo = new Dictionary<string, object>();
            algo["strategy"] = "ranged-absolute";
            algo["alignment"] = "center";
            List<int> ranges = new List<int>();
            ranges.Add(1000);
            algo["ranges"] = ranges;

            //
            var extra = new Dictionary<string, object>();
            extra["colorIndex"] = State.user.colorIndex;

            //
            var filters = new Dictionary<string, object>();

            //
            var settings = new Dictionary<string, object>();

            //
            m_bcWrapper.LobbyService.FindOrCreateLobby(
                "CursorPartyV2",// lobby type
                0,              // rating
                1,              // max steps
                algo,           // algorithm
                filters,        // filters
                0,              // Timeout
                false,          // ready
                extra,          // extra
                "all",          // team code
                settings,       // settings
                null,           // other users
                null,           // Success of lobby found will be in the event onLobbyEvent
                DieWithMessage, "Failed to find lobby");
        }

        // We received a lobby event through RTT
        void OnLobbyEvent(string jsonResponse)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            var jsonData = response["data"] as Dictionary<string, object>;

            // If there is a lobby object present in the message, update our lobby
            // state with it.
            if (jsonData.ContainsKey("lobby"))
            {
                State.lobby = new Lobby(jsonData["lobby"] as Dictionary<string, object>, 
                                        jsonData["lobbyId"] as string);

                // If we were joining lobby, show the lobby screen. We have the information to
                // display now.
                if (State.screenState == ScreenState.JoiningLobby)
                {
                    ChangeScreen(ScreenState.Lobby);
                }

                State.form.UpdateLobby();
            }

            if (response.ContainsKey("operation"))
            {
                var operation = response["operation"] as string;
                switch (operation)
                {
                    case "DISBANDED":
                    {
                        var reason = jsonData["reason"] as Dictionary<string, object>;
                        if ((int)reason["code"] != BrainCloud.ReasonCodes.RTT_ROOM_READY)
                        {
                            // Disbanded for any other reason than ROOM_READY, means we failed to launch the game.
                            CloseGame();
                        }
                        break;
                    }
                    case "STARTING":
                        // Save our picked color index
                        Settings.colorIndex = State.user.colorIndex;
                        Settings.SaveConfigs();

                        // Go to loading screen
                        ChangeScreen(ScreenState.Starting);
                        break;
                    case "ROOM_READY":
                        State.server = new Server(jsonData);
                        ConnectRelay();
                        break;
                }
            }
        }

        // Connect to the Relay server and start the game
        void ConnectRelay()
        {
            m_bcWrapper.RelayService.RegisterRelayCallback(OnRelayMessage);
            m_bcWrapper.RelayService.RegisterSystemCallback(OnRelaySystemMessage);

            int port = 0;
            switch (Settings.protocol)
            {
                case RelayConnectionType.WEBSOCKET:
                    port = State.server.wsPort;
                    break;
                case RelayConnectionType.TCP:
                    port = State.server.tcpPort;
                    break;
                case RelayConnectionType.UDP:
                    port = State.server.udpPort;
                    break;
            }

            m_bcWrapper.RelayService.Connect(Settings.protocol,
                new RelayConnectOptions(false, State.server.host, port, State.server.passcode, State.server.lobbyId),
                OnRelayConnectSuccess, 
                DieWithMessage, "Failed to connect to server");
        }

        void OnRelayConnectSuccess(string jsonResponse, object cbObject)
        {
            ChangeScreen(ScreenState.Game);
            State.form.UpdateGameViewport();
        }

        void OnRelayMessage(short netId, byte[] jsonResponse)
        {
            var memberProfileId = m_bcWrapper.RelayService.GetProfileIdForNetId(netId);
            string jsonMessage = Encoding.ASCII.GetString(jsonResponse);
            var json = JsonReader.Deserialize<Dictionary<string, object>>(jsonMessage);

            foreach (var member in State.lobby.members)
            {
                if (member.id == memberProfileId)
                {
                    var op = json["op"] as string;
                    if (op == "move")
                    {
                        var data = json["data"] as Dictionary<string, object>;

                        member.isAlive = true;
                        member.pos.X = (int)data["x"];
                        member.pos.Y = (int)data["y"];
                    }
                    else if (op == "shockwave")
                    {
                        var data = json["data"] as Dictionary<string, object>;

                        var shockwave = new Shockwave();
                        shockwave.pos.X = (int)data["x"];
                        shockwave.pos.Y = (int)data["y"];
                        shockwave.colorIndex = member.colorIndex;
                        shockwave.startTime = DateTime.Now;
                        State.shockwaves.Add(shockwave);
                    }
                    break;
                }
            }
        }

        void OnRelaySystemMessage(string jsonResponse)
        {
            var json = JsonReader.Deserialize<Dictionary<string, object>>(jsonResponse);
            if (json["op"] as string == "DISCONNECT")
            {
                var profileId = json["profileId"] as string;
                foreach (var member in State.lobby.members)
                {
                    if (member.id == profileId)
                    {
                        member.isAlive = false;
                        break;
                    }
                }
            }
        }
    }
}
