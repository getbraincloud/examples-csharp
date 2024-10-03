using BrainCloud;
using BrainCloud.JsonFx.Json;
using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace RelayExampleApp
{
    class Program
    {
        static BrainCloudWrapper bc;
        static bool isRunning = true;
        static RelayConnectOptions connectOptions = 
            new RelayConnectOptions();
        // Change this to try different connection type
        static RelayConnectionType connectionType = RelayConnectionType.TCP;
        static int returnCode = 1;

        static int Main(string[] args)
        {
            bc = new BrainCloudWrapper("RelayExampleAppProd");
            bc.ResetStoredProfileId();

            // Comment this line, and uncomment the next one. Fill in you ids
            InitBCFromIdsTXT();

            bc.Client.EnableLogging(true);
            bc.AuthenticateAnonymous(onAuthenticated, onFailed);

            // Main event loop running at 60fps as a game would.
            var startTime = DateTime.Now;
            while (isRunning)
            {
                bc.Update();
                Thread.Sleep(16);
                if ((DateTime.Now - startTime).TotalSeconds >= 120.0) // Run for 2mins
                {
                    isRunning = false;
                }
            }

            return returnCode;
        }

        static void InitBCFromIdsTXT()
        {
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

            bc.Init(url, appSecret, appId, "1.0");
        }

        static void onFailed(int status, int reasonCode, 
                             string jsonError, object cbObject)
        {
            Console.WriteLine("Error: " + jsonError);
            isRunning = false;
            returnCode = 1;
        }

        static void onAuthenticated(string jsonResponse, object cbObject)
        {
            bc.RTTService.RegisterRTTLobbyCallback(onLobbyEvent);
            bc.RTTService.EnableRTT(onRTTEnabled, onFailed, RTTConnectionType.WEBSOCKET);
        }

        static void onRTTEnabled(string jsonResponse, object cbObject)
        {
            var algo = new Dictionary<string, object>();
            algo["strategy"] = "ranged-absolute";
            algo["alignment"] = "center";
            List<int> ranges = new List<int>();
            ranges.Add(1000);
            algo["ranges"] = ranges;
            bc.LobbyService.FindOrCreateLobby(
                "CursorPartyV2", 0, 1, algo, 
                new Dictionary<string, object>(), 0, true, 
                new Dictionary<string, object>(), "all", 
                new Dictionary<string, object>(), null, null, onFailed);
        }

        static void onLobbyEvent(string json)
        {
            var response = 
                JsonReader.Deserialize<Dictionary<string, object>>(json);
            var data = response["data"] as Dictionary<string, object>;

            switch (response["operation"] as string)
            {
                case "DISBANDED":
                    var reason = data["reason"] 
                        as Dictionary<string, object>;
                    var reasonCode = (int)reason["code"];
                    if (reasonCode != ReasonCodes.RTT_ROOM_READY) // Disbanded for another reason than room ready
                        onFailed(0, 0, "DISBANDED != RTT_ROOM_READY", null);
                    break;

                // ROOM_READY, connect to the server
                case "ROOM_READY":
                    var connectData = data["connectData"] 
                        as Dictionary<string, object>;
                    var ports = connectData["ports"] 
                        as Dictionary<string, object>;

                    connectOptions.ssl = false;
                    connectOptions.host = connectData["address"] as string;

                    if (connectionType == RelayConnectionType.WEBSOCKET)
                        connectOptions.port = (int)ports["ws"];
                    else if (connectionType == RelayConnectionType.TCP)
                        connectOptions.port = (int)ports["tcp"];
                    else if (connectionType == RelayConnectionType.UDP)
                        connectOptions.port = (int)ports["udp"];

                    connectOptions.passcode = data["passcode"] as string;
                    connectOptions.lobbyId = data["lobbyId"] as string;

                    connectToRelay();
                    break;
            }
        }

        static void connectToRelay()
        {
            bc.RelayService.RegisterSystemCallback(systemCallback);
            bc.RelayService.RegisterRelayCallback(relayCallback);
            bc.RelayService.Connect(connectionType, connectOptions, 
                                    onRelayConnected, onFailed);
        }

        static void onRelayConnected(string jsonResponse, object cbObject)
        {
            Console.WriteLine("On Relay Connected");

            short myNetId = bc.RelayService.GetNetIdForProfileId(
                bc.Client.AuthenticationService.ProfileId);
            byte[] bytes = Encoding.ASCII.GetBytes("Hello World!");
            bc.RelayService.Send(bytes, (ulong)myNetId, true, true, 
                                 BrainCloudRelay.CHANNEL_HIGH_PRIORITY_1);
        }

        static void systemCallback(string json)
        {
            Console.WriteLine("systemCallback: " + json);
        }

        static void relayCallback(short netId, byte[] data)
        {
            string message = Encoding.ASCII.GetString(data, 0, data.Length);
            Console.WriteLine("relayCallback: " + message);

            returnCode = 0; // We succeeded the test
        }
    }
}
