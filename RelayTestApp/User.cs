using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RelayTestApp
{
    public class User
    {
        public string id;
        public string name;
        public int colorIndex = 7;
        public bool isReady = false;
        public bool isAlive = false;
        public bool allowSendTo = true;
        public Point pos = new Point(0, 0);

        public User() { }

        public User(Dictionary<string, object> userJson)
        {
            id = userJson["profileId"] as string;
            name = userJson["name"] as string;

            var extra = userJson["extra"] as Dictionary<string, object>;
            colorIndex = (int)extra["colorIndex"];
        }
    }
}
