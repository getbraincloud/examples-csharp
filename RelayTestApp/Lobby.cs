using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RelayTestApp
{
    class Lobby
    {
        public string lobbyId;
        public string ownerId;
        public List<User> members = new List<User>();

        public Lobby(Dictionary<string, object> lobbyJson, string in_lobbyId)
        {
            lobbyId = in_lobbyId;
            ownerId = lobbyJson["owner"] as string;
            var jsonMembers = lobbyJson["members"] as Dictionary<string, object>[];
            for (int i = 0; i < jsonMembers.Length; ++i)
            {
                var jsonMember = jsonMembers[i] as Dictionary<string, object>;
                var user = new User(jsonMember);
                if (user.id == State.user.id) user.allowSendTo = false;
                members.Add(user);
            }
        }
    }
}
