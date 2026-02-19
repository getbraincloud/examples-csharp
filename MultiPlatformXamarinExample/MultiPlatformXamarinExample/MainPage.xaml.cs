using System;
using Xamarin.Forms;

namespace MultiPlatformXamarinExample
{
    public partial class MainPage : ContentPage
    {
        private BCService _bcService;

        public MainPage()
        {
            InitializeComponent();
            _bcService = BCService.Instance;
            Logger.Log("App started");
        }

        private void OnInitClicked(object sender, EventArgs e)
        {
            try
            {
                Logger.Log("Initializing BrainCloud...");

                string appId = "your-app-id";
                string secret = "your-secret";
                string appVersion = "1.0.0";

                _bcService.Initialize(appId, secret, appVersion);

                PrintResult("BrainCloud initialized!");

                PrintResult("Platform: " + _bcService.BrainCloud.Client.ReleasePlatform);
            }
            catch (Exception ex)
            {
                Logger.LogError("Init failed", ex);
                PrintResult($"Init Error: {ex.Message}");
            }
        }

        private void OnAuthenticateClicked(object sender, EventArgs e)
        {
            try
            {
                Logger.Log("Starting authentication...");
                PrintResult("Authenticating...");

                _bcService.BrainCloud.AuthenticateAnonymous(OnAuthSuccess, OnAuthFailure);
            }
            catch (Exception ex)
            {
                Logger.LogError("Authentication request failed", ex);
                PrintResult($"Auth Error: {ex.Message}");
            }
        }

        private void OnAuthSuccess(string jsonResponse, object cbObject)
        {
            Logger.Log($"Authentication SUCCESS: {jsonResponse}");
            PrintResult($"Authentication successful!\n{jsonResponse}");
        }

        private void OnAuthFailure(int statusCode, int reasonCode, string message, object cbObject)
        {
            Logger.LogError($"Authentication FAILED - Status: {statusCode}, Reason: {reasonCode}, Message: {message}");
            PrintResult($"Auth failed: [{statusCode}] {message}");
        }

        private void PrintResult(string message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                ResultLabel.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n\n{ResultLabel.Text}";
            });
        }

        private void CallIncrementXP(object sender, EventArgs e)
        {
            try
            {
                PrintResult("Sending IncrementXP request...");

                _bcService.BrainCloud.PlayerStatisticsService.IncrementExperiencePoints(10, OnAPICallSuccess, OnAPICallFailure);
            }
            catch (Exception ex)
            {
                Logger.LogError("API request failed", ex);
                PrintResult($"API request failed: {ex.Message}");
            }
        }

        private void CallIdentities(object sender, EventArgs e)
        {
            try
            {
                PrintResult("Sending Identity request...");

                _bcService.BrainCloud.IdentityService.GetIdentities(OnAPICallSuccess, OnAPICallFailure);
            }
            catch (Exception ex)
            {
                Logger.LogError("API request failed", ex);
                PrintResult($"API request failed: {ex.Message}");
            }
        }

        private void OnAPICallSuccess(string jsonResponse, object cbObject)
        {
            Logger.Log($"API Call Success: {jsonResponse}");
            PrintResult($"API Call Success: \n{jsonResponse}");
        }

        private void OnAPICallFailure(int statusCode, int reasonCode, string message, object cbObject)
        {
            Logger.Log($"API Call Failure: {message}");
            PrintResult($"API Call Failure: \n{message}");
        }
    }
}

