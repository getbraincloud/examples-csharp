using System;
using System.IO;
using System.Text.Json;

namespace RelayTestApp
{
    static class Settings
    {
        static public string username = "";
        static public string password = "";
        static public int colorIndex = 0;
        static public int sendChannel = 0;
        static public bool sendReliable = false;
        static public bool sendOrdered = true;
        static public BrainCloud.RelayConnectionType protocol = BrainCloud.RelayConnectionType.UDP;
        static public string lobbyType = "CursorPartyV2";

        static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

        static public void LoadConfigs()
        {
            if (!File.Exists(ConfigPath)) return;
            try
            {
                var doc  = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("username",   out var u))  username  = u.GetString() ?? "";
                if (root.TryGetProperty("password",   out var p))  password  = p.GetString() ?? "";
                if (root.TryGetProperty("colorIndex", out var c))  colorIndex = c.GetInt32();
                if (root.TryGetProperty("protocol",   out var pr)) protocol  = (BrainCloud.RelayConnectionType)pr.GetInt32();
                if (root.TryGetProperty("lobbyType",  out var lt)) lobbyType = lt.GetString() ?? "CursorPartyV2";
            }
            catch { }
        }

        static public void SaveConfigs()
        {
            try
            {
                var data = new
                {
                    username,
                    password,
                    colorIndex,
                    protocol = (int)protocol,
                    lobbyType
                };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
