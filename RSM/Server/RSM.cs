using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using BrainCloud.JsonFx.Json;

namespace Server
{
    class RSM
    {
        static public int Main()
        {
            string appId      = Environment.GetEnvironmentVariable("APP_ID")      ?? "20001";
            string serverName = Environment.GetEnvironmentVariable("SERVER_NAME") ?? "TestServer";
            string serverSecret = Environment.GetEnvironmentVariable("SERVER_SECRET") ?? "2ddf8355-c516-48dd-a6b0-e35bd75fac80";
            string s2sUrl     = Environment.GetEnvironmentVariable("S2S_URL")     ?? "https://api.internal.braincloudservers.com/s2sdispatcher";
            int port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out int p) ? p : 7779;

            // Create an HTTP listener.
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");
            listener.Start();

            Console.WriteLine("Listening...");

            // Will wait here until we hear from a connection
            HttpListenerContext ctx = listener.GetContext();
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            // Read the data from the request
            string reqMessage = new StreamReader(req.InputStream).ReadToEnd();

            Console.WriteLine("RECV: " + reqMessage);

            // Do some validation. We are expecting a POST request with
            // the path /requestRoomServer
            if (req.HttpMethod != "POST" ||
                req.Url.LocalPath != "/requestRoomServer")
            {
                respond(resp, "Forbidden Request", 403);
                return 1;
            }

            // Extract the lobby id from the message
            var lobbyJson = JsonReader.Deserialize<Dictionary<string, object>>(reqMessage);
            string lobbyId = lobbyJson["id"] as string;

            // Reply back with our connection info
            var responseJson = new Dictionary<string, object>()
            {
                { "lobbyId", lobbyId },
                { "connectInfo", new Dictionary<string, object>
                {
                    { "roomId", lobbyId },
                    { "url", req.Url.Host }, // Host should contain our IP
                    //{ "tcpPort", port }
                }}
            };
            string responseString = JsonWriter.Serialize(responseJson);
            respond(resp, responseString, 200);

            // We can stop listening now
            listener.Stop();

            // Create the game server instance, and run it.
            GameServer gameServer = new GameServer(appId, serverName, serverSecret, s2sUrl, lobbyId);
            gameServer.Run();

            return 0;
        }

        static void respond(HttpListenerResponse resp, string message, int statusCode)
        {
            Console.WriteLine("SEND: " + message);

            // Write the response info
            byte[] data = Encoding.UTF8.GetBytes(message);
            resp.ContentType = "text/plain";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;
            resp.StatusCode = statusCode;

            // Write out to the response stream (asynchronously), then close it
            resp.OutputStream.Write(data, 0, data.Length);
            resp.Close();
        }
    }
}
