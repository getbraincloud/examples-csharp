using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using BrainCloud;

namespace XamarinExample
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }
        int count = 0;

        private BrainCloudWrapper wrapper;
        private BrainCloudClient client;

        private static string version = "1.0.0";
        string ServerUrl = "https://sharedprod.braincloudservers.com/dispatcherv2";
        string Secret = "f1bbf51b-4fc9-4563-9c2b-24e5e68dd70f";
        string AppId = "12701";

        private bool remote_loading_in_progress = false;
        private bool is_authorised = false;

        void Button_Clicked(object sender, System.EventArgs e)
        {
            count++;
            ((Button)sender).Text = $"You clicked {count} times.";
            InitWrapper();
            client = wrapper.Client;
        }

        public void InitWrapper()
        {
            if (!remote_loading_in_progress)
            {
                wrapper = new BrainCloudWrapper();
                wrapper.Init(ServerUrl, Secret, AppId, version);
                remote_loading_in_progress = true;
                wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
                Xamarin.Forms.Device.StartTimer(TimeSpan.FromSeconds(0.1), RunCallbacks);
            }
            //wrapper.Update();

            Content = new StackLayout
            {
                Children = { new Label { Text = "TEST1" } }
            };
            Console.WriteLine("woooooooork");
            System.Diagnostics.Debug.WriteLine("Logs work!");
        }

        public bool RunCallbacks()
        {
            wrapper.Update();
            return remote_loading_in_progress;
        }
        private void FetchGlobalProperties()
        {
            wrapper.GlobalAppService.ReadProperties(OnConfigSuccess, OnConfigFail);
        }

        public void OnAuthorizationSuccess(string responseData, object cbObject)
        {
            //takea  look at responseData
            System.Diagnostics.Debug.WriteLine("AUTH SUCCESS " + responseData);
            FetchGlobalProperties();
            is_authorised = true;
        }

        public void OnAuthorizationFail(int statusCode, int reasonCode, string statusMessage, object cbObject)
        {
            System.Diagnostics.Debug.WriteLine("AUTH FAIL " + statusMessage);
            remote_loading_in_progress = false;
        }
        public void OnConfigSuccess(string json_response, object callback_object)
        {
            System.Diagnostics.Debug.WriteLine("CONFIG SUCCESS " + json_response);
        }
        public void OnConfigFail(int status, int reason_code, string json_error, object callback_object)
        {
            System.Diagnostics.Debug.WriteLine("CONFIG FAIL " + json_error);
        }
    }
}


