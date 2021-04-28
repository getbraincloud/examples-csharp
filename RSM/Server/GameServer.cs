using BrainCloud.JsonFx.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal class GameServer
    {
        bool _isRunning = true;
        BrainCloudS2S _s2s;
        string _lobbyId;

        public GameServer(string appId,
                          string serverName,
                          string serverSecret,
                          string s2sUrl,
                          string lobbyId)
        {
            _lobbyId = lobbyId;

            _s2s = new BrainCloudS2S();
            _s2s.Init(appId, serverName, serverSecret, false, s2sUrl);
            _s2s.LoggingEnabled = true;
            _s2s.Authenticate(OnAuthenticated);
        }

        public void Run()
        {
            Console.WriteLine("Game Server is Running...");

            while (_isRunning)
            {
                Update();
                Thread.Sleep(16); // Simulate 60 fps
            }
        }

        public void Update()
        {
            if (_s2s != null) _s2s.RunCallbacks();
        }

        void OnAuthenticated(string responseString)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(responseString);
            int status = (int)response["status"];
            if (status != 200)
            {
                _isRunning = false;
                return;
            }

            // Send request to get the lobby data. This will tell us who we are 
            // expecting to get connection from and their passcode. We technically
            // already have this from the RSM, but we just assumed this game
            // server was launched without an RSM.
            var request = new Dictionary<string, object>
            {
                { "service", "lobby" },
                { "operation", "GET_LOBBY_DATA" },
                { "data", new Dictionary<string, object>
                {
                    { "lobbyId", _lobbyId }
                }}
            };
            _s2s.Request(request, OnLobbyData);
        }

        void OnLobbyData(string responseString)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(responseString);
            int status = (int)response["status"];
            if (status != 200)
            {
                _isRunning = false;
                return;
            }

            // Start the server
            // ...

            // Tell brainCloud we are ready to accept connections
            var request = new Dictionary<string, object>
            {
                { "service", "lobby" },
                { "operation", "SYS_ROOM_READY" },
                { "data", new Dictionary<string, object>
                {
                    { "lobbyId", _lobbyId }
                }}
            };
            _s2s.Request(request, OnRoomReady);
        }

        void OnRoomReady(string responseString)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(responseString);
            int status = (int)response["status"];
            if (status != 200)
            {
                _isRunning = false;
                return;
            }

            _s2s = null; // We will not need this anymore
        }
    }
}
