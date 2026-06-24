using System.Collections.Generic;
using Avalonia;

namespace RelayTestApp
{
    public class User
    {
        public string cxId;
        public string name;
        public int colorIndex = 7;
        public bool isReady = false;
        public bool isAlive = false;
        public bool allowSendTo = true;
        public Point pos = new Point(0, 0);
        public Dictionary<string, int> pings = new Dictionary<string, int>(); // pre-game region latencies shared via lobby extra (ms)
        public int activePing = -1; // live relay-server RTT broadcast during gameplay; -1 = not yet received

        public User() { }

        public User(Dictionary<string, object> userJson)
        {
            cxId = userJson["cxId"] as string;
            name = userJson["name"] as string;

            var extra = userJson["extra"] as Dictionary<string, object>;
            colorIndex = (int)extra["colorIndex"];
        }
    }
}
