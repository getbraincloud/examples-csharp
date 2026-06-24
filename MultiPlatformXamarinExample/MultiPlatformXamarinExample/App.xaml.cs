using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MultiPlatformXamarinExample
{
    public partial class App : Application
    {
        public App ()
        {
            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override void OnStart ()
        {
        }

        protected override void OnSleep ()
        {
            // Stop update loop when app goes to background
            BCService.Instance.StopUpdateLoop();
        }

        protected override void OnResume ()
        {
        }
    }
}

