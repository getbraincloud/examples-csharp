using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

using BrainCloud;

namespace RelayTestApp
{
    static class Settings
    {
        static public string username;
        static public string password;
        static public int colorIndex = 0;
        static public int sendChannel = 0;
        static public bool sendReliable = false;
        static public bool sendOrdered = true;
        static public BrainCloud.RelayConnectionType protocol = BrainCloud.RelayConnectionType.UDP;

        static public void LoadConfigs()
        {
            var appSettings = ConfigurationManager.AppSettings;

            username = appSettings["username"] ?? "";
            password = appSettings["password"] ?? "";

            string colorIndexStr = appSettings["colorIndex"] ?? "0";
            colorIndex = Int32.Parse(colorIndexStr);

            string protocolStr = appSettings["protocol"] ?? "3";
            protocol = (BrainCloud.RelayConnectionType)Int32.Parse(protocolStr);
        }

        static public void SaveConfigs()
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;

            if (settings["username"] == null) settings.Add("username", username);
            else settings["username"].Value = username;

            if (settings["password"] == null) settings.Add("password", password);
            else settings["password"].Value = password;

            string colorIndexStr = colorIndex.ToString();
            if (settings["colorIndex"] == null) settings.Add("colorIndex", colorIndexStr);
            else settings["colorIndex"].Value = colorIndexStr;

            string protocolStr = ((int)protocol).ToString();
            if (settings["protocol"] == null) settings.Add("protocol", protocolStr);
            else settings["protocol"].Value = protocolStr;

            configFile.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
        }
    }
}
