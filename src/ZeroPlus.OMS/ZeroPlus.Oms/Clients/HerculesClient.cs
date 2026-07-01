using NLog;
using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Clients
{
    public class HerculesClient : SubscriptionProvider
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private IHerculesClient _herculesClient;
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

        public void Initialize(IHerculesClient HerculesClient)
        {
            _herculesClient = HerculesClient;
            _herculesClient.ClientConnected += OnClient_ClientConnected;
            _herculesClient.ClientDisconnected += OnClient_ClientDisconnected;
            _herculesClient.FirmOrderAndTradeSummary += OnFirmOrderAndTradeSummary;
        }

        private void OnFirmOrderAndTradeSummary(FirmOrderAndTradeSummary summary)
        {
            Update(summary.Id, SubscriptionFieldType.FirmOrderAndTradeSummary, summary, true);
        }

        private void OnClient_ClientConnected()
        {
            IsConnected = true;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                RegisterClient();
            }
            Resubscribe();
        }

        private void RegisterClient()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (OmsCore.User == null)
            {
                _herculesClient.RegisterClient("Excel", "ZeroPlus OMS AddIn", version!, Dns.GetHostName());
            }
        }

        private void OnClient_ClientDisconnected()
        {
            IsConnected = false;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
        }

        #region PublicMethods

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            await Task.Run(() => _herculesClient?.ConnectAndStart());
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() => _herculesClient?.DisconnectAndStop());
        }
        #endregion

        protected override void Subscribe(SubscriptionKey subscription)
        {
            _herculesClient.Subscribe(subscription.Symbol, subscription.Type);
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            _herculesClient.Unsubscribe(subscription.Symbol, subscription.Type);
        }
    }
}
