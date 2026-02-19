using System;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPlatformXamarinExample
{
	public class BCService
	{
        private static BCService _instance;
        private BrainCloudWrapper _bc;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _updateTask;
        private bool _isRunning = false;
        

        public static BCService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new BCService();
                return _instance;
            }
        }

        private BCService()
        {
            _bc = new BrainCloudWrapper();

            RedirectConsoleToLogger();
        }

        public BrainCloudWrapper BrainCloud => _bc;

        public void Initialize(string appId, string secret, string appVersion)
        {
            string url = "https://api.internal.braincloudservers.com";
            _bc.Init(url, secret, appId, appVersion);
            _bc.Client.EnableLogging(true);
            _bc.Client.RegisterLogDelegate(Logger.Log);
            StartUpdateLoop();
        }

        private void StartUpdateLoop()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            _updateTask = Task.Run(async () =>
            {
                Logger.Log("BrainCloud update loop started");

                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        _bc.Update(); 
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Update error", ex);
                    }

                    // Wait 100ms between updates
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }

                Logger.Log("BrainCloud update loop stopped");
            }, _cancellationTokenSource.Token);
        }

        public void StopUpdateLoop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            Logger.Log("Stopping BrainCloud update loop...");
        }

        public void Shutdown()
        {
            StopUpdateLoop();
            _cancellationTokenSource?.Dispose();
        }

        public void Dispose()
        {
            Shutdown();
        }

        private void RedirectConsoleToLogger()
        {
            //Console.SetOut(new LoggerTextWriter(Console.Out));
        }
    }
}

