using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BrainCloud;
using BrainCloud.JsonFx.Json;

namespace Client
{
    class Client
    {
        static BrainCloudWrapper bc;
        static bool isRunning = true;
        static int returnCode = 0;

        static int Main(string[] args)
        {
            bc = new BrainCloudWrapper("DebuggingRoomServerProd");
            bc.ResetStoredProfileId();

            // Comment this line, and uncomment the next one. Fill in you ids
            InitBCFromIdsTXT();
            //bc.Init("https://sharedprod.braincloudservers.com/dispatcherv2", "your app secret", "your app id", "1.0");

            bc.Client.EnableLogging(true);
            bc.AuthenticateAnonymous(onAuthenticated, onFailed);

            // Main event loop running at 60fps as a game would.
            var startTime = DateTime.Now;
            while (isRunning)
            {
                bc.Update();
                Thread.Sleep(16); // 60 fps
                if ((DateTime.Now - startTime).TotalSeconds >= 60.0 * 5.0) // Run for 5mins
                {
                    isRunning = false;
                    returnCode = 1;
                    Console.WriteLine("Error: app timeout");
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
            bc.RTTService.EnableRTT(RTTConnectionType.WEBSOCKET, onRTTEnabled, onFailed);
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
                "LocalDebugging", 0, 1, algo,
                new Dictionary<string, object>(), 0, true,
                new Dictionary<string, object>(), "all",
                new Dictionary<string, object>(), null, null, onFailed);
        }

        static void onLobbyEvent(string json)
        {
            var response = JsonReader.Deserialize<Dictionary<string, object>>(json);
            var data = response["data"] as Dictionary<string, object>;

            switch (response["operation"] as string)
            {
                case "DISBANDED":
                    var reason = data["reason"]
                        as Dictionary<string, object>;
                    var reasonCode = (int)reason["code"];
                    if (reasonCode != ReasonCodes.RTT_ROOM_READY)
                        onFailed(0, 0, "DISBANDED != RTT_ROOM_READY", null);
                    break;

                case "ROOM_READY":
                    connectToServer();
                    break;
            }
        }

        static void connectToServer()
        {
            // This is our succes path. We don't have to actually connect to the
            // server. We just had to prove it started.
            isRunning = false;
            returnCode = 0; // Success
        }
    }
}
