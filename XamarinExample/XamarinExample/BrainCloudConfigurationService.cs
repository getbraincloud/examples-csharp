//using System;
//using System.Collections.Generic;
//using Newtonsoft.Json;

//using BrainCloud;

//using Goozby.ViewModels;
//using Goozby.Models;

//namespace Goozby.Services
//{
//    public class BrainCloudConfigurationService
//    {
//        private BrainCloudWrapper wrapper;
//        private BrainCloudClient client;

//        private AppConfig config;
//        private bool remote_loading_in_progress = false;
//        private bool is_authorised = false;
//        private static string version = "1.0.0";

//        public BrainCloudConfigurationService()
//        {
//            /*
//            string server_url = "https://sharedprod.braincloudservers.com/dispatcherv2";
//            string secret = "79c8b3d1-0b6d-47cd-93aa-c6631bf01c75";
//            string app_id = "12450";
//            */

//            UseDebugServer = false;

//            First initialise the config object from defaults
//           config = new AppConfig();
//            config.InitDefaults();
//            Now see if there's anything in local storage to override the defaults
//            config.LoadFromDeviceStorage();

//            Finally initiate the process of loading from braincloud sever

//           InitWrapper();

//            client = wrapper.Client;
//        }

//        private string Secret
//        {
//            get
//            {
//                if (UseDebugServer)
//                    return "9c599876-425a-4050-aa97-ee336c04a59c";
//                else
//                    return "79c8b3d1-0b6d-47cd-93aa-c6631bf01c75";
//            }
//        }

//        private string AppId
//        {
//            get
//            {
//                if (UseDebugServer)
//                    return "12597";
//                else
//                    return "12450";
//            }
//        }

//        private string ServerUrl
//        {
//            get
//            {
//                if (UseDebugServer)
//                    return "https://sharedprod.braincloudservers.com/dispatcherv2";
//                else
//                    return "https://sharedprod.braincloudservers.com/dispatcherv2";
//            }
//        }

//        public void InitWrapper()
//        {
//            if (!remote_loading_in_progress)
//            {
//                wrapper = new BrainCloudWrapper();
//                wrapper.Init(ServerUrl, Secret, AppId, version);
//                remote_loading_in_progress = true;

//                wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
//                Xamarin.Forms.Device.StartTimer(TimeSpan.FromSeconds(0.1), RunCallbacks);
//            }
//        }

//        public void CheckRefreshGlobalProperties()
//        {
//            if (DateTime.Today > TimeOfLastRefresh.Date)
//            {
//                RefreshGlobalProperties();
//            }
//            //Goozby.Models.Remote.ConfigDebug.RequestLatest();
//            //Goozby.Models.Remote.ConfigDebug.CheckConfig();
//        }

//        public void RefreshGlobalProperties()
//        {
//            if (remote_loading_in_progress) return;

//            remote_loading_in_progress = true;
//            Xamarin.Forms.Device.StartTimer(TimeSpan.FromSeconds(0.1), RunCallbacks);

//            if (!is_authorised)
//            {
//                wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
//                return;
//            }
//            else
//            {
//                FetchGlobalProperties();
//            }
//        }

//        private void FetchGlobalProperties()
//        {
//            wrapper.GlobalAppService.ReadProperties(OnConfigSuccess, OnConfigFail);
//        }

//        public bool RunCallbacks()
//        {
//            wrapper.Update();
//            return remote_loading_in_progress;
//        }

//        public void OnConfigSuccess()
//        {

//        }

//        public void OnConfigFail()
//        {

//        }

//        public string PropertyValueAsStringRaw(string name)
//        {
//            if (config.ContainsKey(name))
//            {
//                String s = config.PerformStringSubstitutions(config[name]);
//                return s;
//            }
//            else
//                throw new Exception(String.Format("ERROR: Undefined property '{0}'", name));
//        }

//        public string PropertyValueAsString(string name)
//        {
//            name = TargetedPropertyName(name);
//            if (config.ContainsKey(name))
//            {
//                String s = config.PerformStringSubstitutions(config[name]);
//                return s;
//            }
//            else
//                throw new Exception(String.Format("ERROR: Undefined property '{0}'", name));
//        }

//        public string PropertyValueAsStringWithSubstitution(string name, string sub_01)
//        {
//            name = TargetedPropertyName(name);
//            if (config.ContainsKey(name))
//            {
//                String s = config.PerformStringSubstitutions(config[name]);
//                s = s.Replace("[sub.01]", sub_01);
//                return s;
//            }
//            else
//                throw new Exception(String.Format("ERROR: Undefined property '{0}'", name));
//        }

//        public string PropertyValueAsStringWithSubstitutions(string name, string sub_01, string sub_02)
//        {
//            name = TargetedPropertyName(name);
//            if (config.ContainsKey(name))
//            {
//                String s = config.PerformStringSubstitutions(config[name]);
//                s = s.Replace("[sub.01]", sub_01);
//                s = s.Replace("[sub.02]", sub_02);
//                return s;
//            }
//            else
//                throw new Exception(String.Format("ERROR: Undefined property '{0}'", name));
//        }

//        public int PropertyValueAsIntRaw(string name)
//        {
//            int i = int.Parse(PropertyValueAsStringRaw(name));
//            return i;
//        }

//        public int PropertyValueAsInt(string name)
//        {
//            int i = int.Parse(PropertyValueAsString(name));
//            return i;
//        }

//        public float PropertyValueAsFloat(string name)
//        {
//            float f = float.Parse(PropertyValueAsString(name));
//            return f;
//        }

//        public bool PropertyValueAsBool(string name)
//        {
//            bool b = bool.Parse(PropertyValueAsString(name));
//            return b;
//        }

//        public DateTime PropertyValueAsDateTime(string name)
//        {
//            DateTime d = DateTime.Parse(PropertyValueAsString(name));
//            return d;
//        }

//        public TimeSpan PropertyValueAsTimeSpan(string name)
//        {
//            TimeSpan t = TimeSpan.Parse(PropertyValueAsString(name));
//            return t;
//        }

//        //
//        // Authorization callbacks
//        //
//        public void OnAuthorizationSuccess(string json_response, object callback_object)
//        {
//            AppModel.Debug(string.Format("[Authorization Success] {0}", json_response));
//            is_authorised = true;
//            FetchGlobalProperties();
//        }

//        public void OnAuthorizationFail(int status, int reason_code, string json_error, object callback_object)
//        {
//            AppModel.Debug(string.Format("[Authorization Failed] {0}  {1}  {2}", status, reason_code, json_error));
//            remote_loading_in_progress = false;
//        }

//        //
//        // Global config callbacks
//        //
//        public void OnConfigSuccess(string json_response, object callback_object)
//        {
//            AppModel.Debug(string.Format("[Config Success] {0}", json_response));

//            try
//            {
//                Dictionary<string, object> response = JsonConvert.DeserializeObject<Dictionary<string, object>>(json_response);
//                Dictionary<string, object> config_raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(response["data"].ToString());

//                foreach (string key in config_raw.Keys)
//                {
//                    string property_json = config_raw[key].ToString();
//                    Dictionary<string, object> property = JsonConvert.DeserializeObject<Dictionary<string, object>>(property_json);

//                    string name = property["name"].ToString();
//                    string value = property["value"].ToString();

//                    if (config.ContainsKey(name))
//                    {
//                        config.Remove(name);
//                    }
//                    config.Add(name, value);
//                }
//            }
//            catch (Exception ex)
//            {
//                AppModel.Debug(ex.StackTrace);
//            }

//            // Save these config parameters as the new defaults
//            // config is initialised in constructor with default params.
//            config.TimeLastRefresh = DateTime.Today;
//            config.SaveToDeviceStorage();
//            remote_loading_in_progress = false;

//            AppModel.OnConfigRefreshed();
//            AppModel.Debug("done, at last!");
//        }

//        public void OnConfigFail(int status, int reason_code, string json_error, object callback_object)
//        {
//            AppModel.Debug(string.Format("[Config Failed] {0}  {1}  {2}", status, reason_code, json_error));
//            remote_loading_in_progress = false;
//        }

//        public string InterestsSuffix
//        {
//            get { return String.Format("_int{0}", AppModel.UserSettings.InterestId.ToString("D2")); }
//        }
//        public string GenderSuffix
//        {
//            get
//            {
//                switch (AppModel.UserSettings.Gender)
//                {
//                    case Gender.Female:
//                        return "_genF";
//                    case Gender.Male:
//                        return "_genM";
//                    default:
//                        return "_genX";
//                }
//            }
//        }
//        public int AgeBand
//        {
//            get
//            {
//                int age_band = 3;
//                int age = AppModel.UserSettings.Age;
//                if (age < AppConfig.AgeLow)
//                {
//                    age_band = 0;
//                }
//                else if (age < AppConfig.AgeMid)
//                {
//                    age_band = 1;
//                }
//                else if (age < AppConfig.AgeHigh)
//                {
//                    age_band = 2;
//                }
//                return age_band;
//            }
//        }
//        public string AgeSuffix
//        {
//            get
//            {

//                return String.Format("_age{0}", AgeBand.ToString("D2"));
//            }
//        }
//        public string TargetedPropertyName(string name)
//        {
//            UserSettings settings = AppModel.UserSettings;
//            if (settings == null) return name;

//            string tname;

//            tname = name + InterestsSuffix + AgeSuffix + GenderSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + InterestsSuffix + AgeSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + InterestsSuffix + GenderSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + AgeSuffix + GenderSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + InterestsSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + AgeSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            tname = name + GenderSuffix;
//            if (config.ContainsKey(tname)) return tname;

//            return name;
//        }
//        public DateTime TimeOfLastRefresh
//        {
//            get
//            {
//                return config.TimeLastRefresh;
//            }
//        }

//        public bool UseDebugServer
//        {
//            get;
//            set;
//        }
//    }
//}
