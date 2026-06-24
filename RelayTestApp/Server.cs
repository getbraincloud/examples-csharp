using System;
using System.Collections.Generic;
using BrainCloud;

namespace RelayTestApp
{
    class Server
    {
        public string host;
        public int port = -1;
        public RelayConnectionType connectionType;
        public string passcode;
        public string lobbyId;

        public Server(Dictionary<string, object> serverJson, RelayConnectionType requestedType)
        {
            var connectData = serverJson["connectData"] as Dictionary<string, object>;
            var ports       = connectData["ports"] as Dictionary<string, object>;

            host     = connectData["address"] as string;
            passcode = serverJson["passcode"] as string;
            lobbyId  = serverJson["lobbyId"]  as string;

            // GameLift and i3D only expose a single WebSocket port — force WEBSOCKET
            if (TryGetPort(ports, "gamelift", out int glPort))
            {
                port           = glPort;
                connectionType = RelayConnectionType.WEBSOCKET;
            }
            else if (TryGetPort(ports, "i3d", out int i3dPort))
            {
                port           = i3dPort;
                connectionType = RelayConnectionType.WEBSOCKET;
            }
            else
            {
                connectionType = requestedType;
                string key = requestedType switch
                {
                    RelayConnectionType.WEBSOCKET => "ws",
                    RelayConnectionType.TCP       => "tcp",
                    _                             => "udp"
                };
                port = TryGetPort(ports, key, out int p) ? p : -1;
            }
        }

        static bool TryGetPort(Dictionary<string, object> ports, string key, out int port)
        {
            port = -1;
            if (!ports.ContainsKey(key) || ports[key] == null) return false;
            port = Convert.ToInt32(ports[key]);
            return port > 0;
        }
    }
}
