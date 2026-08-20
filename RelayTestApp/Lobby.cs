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
        public string ownerCxId;
        public List<User> members = new List<User>();
        // This-lobby chat (via Lobby service SendSignal). Lobby is rebuilt from
        // scratch on every RTT lobby event (joins/updates, not just chat), so the
        // caller must copy this field forward from the outgoing Lobby instance or
        // every member update silently wipes the chat history.
        public List<ChatMessage> chatMessages = new List<ChatMessage>();

        public Lobby(Dictionary<string, object> lobbyJson, string in_lobbyId)
        {
            lobbyId = in_lobbyId;
            ownerCxId = lobbyJson["ownerCxId"] as string;
            var jsonMembers = lobbyJson["members"] as Dictionary<string, object>[];
            for (int i = 0; i < jsonMembers.Length; ++i)
            {
                var jsonMember = jsonMembers[i] as Dictionary<string, object>;
                var user = new User(jsonMember);
                if (State.user != null && user.cxId == State.user.cxId) user.allowSendTo = false;

                // Parse per-region ping data shared by this member via lobby extra
                var extra = jsonMember.ContainsKey("extra") ? jsonMember["extra"] as Dictionary<string, object> : null;
                if (extra != null && extra.ContainsKey("pings"))
                {
                    var pingsDict = extra["pings"] as Dictionary<string, object>;
                    if (pingsDict != null)
                        foreach (var kv in pingsDict)
                            user.pings[kv.Key] = Convert.ToInt32(kv.Value);
                }

                members.Add(user);
            }
        }
    }
}
