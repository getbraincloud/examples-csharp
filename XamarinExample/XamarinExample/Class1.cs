using System;
using System.Collections.Generic;

using BrainCloud;

public class Class1
{
    private BrainCloudWrapper wrapper;
    private BrainCloudClient client;

    private static string version = "1.0.0";
    string ServerUrl = "https://sharedprod.braincloudservers.com/dispatcherv2";
    string Secret = "79c8b3d1-0b6d-47cd-93aa-c6631bf01c75";
    string AppId = "12450";

    public Class1()
    {
        InitWrapper();
        client = wrapper.Client;
    }

    public void InitWrapper()
    {
            wrapper = new BrainCloudWrapper();
            wrapper.Init(ServerUrl, Secret, AppId, version);

            wrapper.AuthenticateAnonymous(OnAuthorizationSuccess, OnAuthorizationFail);
    }

    public void OnAuthorizationSuccess(string responseData, object cbObject)
    {
        
    }

    public void OnAuthorizationFail(int statusCode, int reasonCode, string statusMessage, object cbObject)
    {

    }
}