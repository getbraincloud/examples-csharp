using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BrainCloud;

using Xamarin.Forms;

namespace XamarinExample
{
    public class Page1 : ContentPage
    {
        private BrainCloudWrapper wrapper;
        private BrainCloudClient client;

        //private static string version = "1.0.0";
        //string ServerUrl = "https://sharedprod.braincloudservers.com/dispatcherv2";
        //string Secret = "79c8b3d1-0b6d-47cd-93aa-c6631bf01c75";
        //string AppId = "12450";

        private static string version = "1.0.0";
        string ServerUrl = "https://internal.braincloudservers.com/dispatcherv2";
        string Secret = "155794b0-aebf-4013-b5d9-6d5d19afcdf3";
        string AppId = "23419";
        public Page1()
        {
            InitWrapper();
            client = wrapper.Client;
            //Content = new StackLayout
            //{
            //    Children = {
            //        new Label { Text = "Welcome to Xamarin.Forms!" }
            //    }
            //};
        }
        public void InitWrapper()
        {
            wrapper = new BrainCloudWrapper();
            wrapper.Init(ServerUrl, Secret, AppId, version);
            Content = new StackLayout
            {
                Children = { new Label { Text = "TEST" } }
            };
            wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
            Console.WriteLine("woooooooork");
        }

        public void OnAuthorizationSuccess(string responseData, object cbObject)
        {
            int a = 1;
            //breakpoints don't work need to log somehow
        }

        public void OnAuthorizationFail(int statusCode, int reasonCode, string statusMessage, object cbObject)
        {
            int b = 3;
        }
    }
}