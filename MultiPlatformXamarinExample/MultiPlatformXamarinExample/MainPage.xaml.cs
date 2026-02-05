using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using BrainCloud;

namespace MultiPlatformXamarinExample
{
    public partial class MainPage : ContentPage
    {
        private BrainCloudWrapper _bc;

        public MainPage()
        {
            InitializeComponent();

            _bc = new BrainCloudWrapper();
        }


        private void OnInitClicked(object sender, EventArgs e)
        {
            // Use BrainCloud here
            string appId = "22319";//"your-app-id";
            string secret = "d528be92-c147-4c55-b041-f655d7018c0c";//"your-secret-id";
            string appVersion = "1.0.0";
            string url = "https://api.internal.braincloudservers.com/dispatcherv2";

            _bc.Init(url, appId, secret, appVersion);
            PrintResult("BrainCloud initialized!");
        }

        private void OnAuthenticateClicked(object sender, EventArgs e)
        {
            // Use BrainCloud here
            _bc.AuthenticateAnonymous(OnAuthSuccess, OnAuthFailure);
            PrintResult("Authenticating...");
        }

        private void OnAuthSuccess(string jsonResponse, object cbObject)
        {
            PrintResult($"Success: {jsonResponse}");
        }

        private void OnAuthFailure(int statusCode, int reasonCode, string message, object cbObject)
        {
            PrintResult($"Failed: {message}");
        }

        private void PrintResult(string message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                ResultLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n\n{ResultLabel.Text}";
            });
        }
    }
}

