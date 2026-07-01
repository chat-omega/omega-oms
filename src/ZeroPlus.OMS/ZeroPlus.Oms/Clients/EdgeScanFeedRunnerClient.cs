using System;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.EdgeScanFeedRunner.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;

namespace ZeroPlus.Oms.Clients
{
    public class EdgeScanFeedRunnerClient : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        public event EdgeScanFeedRunnerChangedHandler RunnerStateChanged;

        public IEdgeScanFeedRunnerClient FeedRunnerClient { get; private set; }
        private bool _isConnected;

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public void Initialize(IEdgeScanFeedRunnerClient client)
        {
            FeedRunnerClient = client;
            FeedRunnerClient.ClientConnected += OnClient_ClientConnected;
            FeedRunnerClient.ClientDisconnected += OnClient_ClientDisconnected;
            FeedRunnerClient.RunnerStateChanged += OnFeedRunner_RunnerStateChanged;
        }

        private void OnFeedRunner_RunnerStateChanged(string runnerId, EdgeScanFeedRunnerState state)
        {
            RunnerStateChanged?.Invoke(runnerId, state);
        }

        #region PublicMethods

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            await Task.Run(() =>
            {
                FeedRunnerClient?.ConnectAndStart();
            });
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                FeedRunnerClient?.DisconnectAndStop();
            });
        }

        #endregion

        private void OnClient_ClientDisconnected()
        {
            IsConnected = false;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
        }

        private void OnClient_ClientConnected()
        {
            IsConnected = true;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                RegisterClient();
            }
        }

        private void RegisterClient()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string hostname = Dns.GetHostName();
            if (OmsCore?.User != null)
            {
                FeedRunnerClient.RegisterClient(OmsCore.User.Username, "ZeroPlus OMS App", version!, hostname);
                FeedRunnerClient.AuthenticateClient(OmsCore.User.ID, OmsCore.User.Username, Guid.NewGuid().ToString(), "ZeroPlus OMS App", version!, hostname);
            }
            else
            {
                FeedRunnerClient.DisconnectAndStop();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
