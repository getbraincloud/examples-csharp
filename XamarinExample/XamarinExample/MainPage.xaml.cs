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

        void Button_Clicked(object sender, System.EventArgs e)
        {
            count++;
            ((Button)sender).Text = $"You clicked {count} times.";
            InitWrapper();
            client = wrapper.Client;
        }

        public void InitWrapper()
        {
            wrapper = new BrainCloudWrapper();
            wrapper.Init(ServerUrl, Secret, AppId, version);
            wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
            wrapper.Update();
            Content = new StackLayout
            {
                Children = { new Label { Text = "TEST1" } }
            };
            Console.WriteLine("woooooooork");
            System.Diagnostics.Debug.WriteLine("HI!");
        }

        public void OnAuthorizationSuccess(string responseData, object cbObject)
        {  
        }

        public void OnAuthorizationFail(int statusCode, int reasonCode, string statusMessage, object cbObject)
        {
        }
    }

}


