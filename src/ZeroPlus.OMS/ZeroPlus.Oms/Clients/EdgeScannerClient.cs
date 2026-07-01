using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.EdgeScanner.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Responses;

namespace ZeroPlus.Oms.Clients
{
    public class EdgeScannerClient : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        public IEdgeScannerClient ScannerClient { get; private set; }
        private bool _isConnected;
        private bool _subscribedToTradeFeed;
        private bool _subscribedToAllTradeFeed;

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

        public void Initialize(IEdgeScannerClient client)
        {
            ScannerClient = client;
            ScannerClient.ClientConnected += OnClient_ClientConnected;
            ScannerClient.ClientDisconnected += OnClient_ClientDisconnected;
        }

        #region PublicMethods

        public void SubscribeToSymbolStats()
        {
            ScannerClient.Subscribe("*", SubscriptionFieldType.SymbolStat);
        }

        public void SubscribeToEdgeScanFeed(string id, SubscriptionFieldType type)
        {
            ScannerClient.Subscribe(id, type);
        }

        public void UnsubscribeEdgeScanFeed(string id, SubscriptionFieldType type)
        {
            ScannerClient.Subscribe(id, type);
        }

        public void SubscribeToTradeFeed(bool all)
        {
            _subscribedToAllTradeFeed = all;
            ScannerClient.Subscribe(_subscribedToAllTradeFeed ? "ALL" : "*", SubscriptionFieldType.TradeFeed);
            _subscribedToTradeFeed = true;
        }

        public void UnsubscribeToTradeFeed()
        {
            ScannerClient.Unsubscribe("*", SubscriptionFieldType.TradeFeed);
            _subscribedToTradeFeed = false;
        }

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            await Task.Run(() =>
            {
                ScannerClient?.ConnectAndStart(openSharedMemory: false);
            });
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                ScannerClient?.DisconnectAndStop();
            });
        }

        public async Task<List<OptionSnapshot>> RequestOptionSnapshotsAsync(string symbol, DateTime expiration, double delta, DateTime startDateTime, DateTime endDateTime)
        {
            return await ScannerClient.RequestOptionSnapshotsAsync(symbol, expiration, delta, startDateTime, endDateTime);
        }

        public List<OptionSnapshot> RequestOptionSnapshots(string symbol, DateTime expiration, double delta, DateTime startDateTime, DateTime endDateTime)
        {
            List<OptionSnapshot> results = ScannerClient.RequestOptionSnapshots(symbol, expiration, delta, startDateTime, endDateTime);
            return results;
        }

        public List<MarketCrossScanResult> RequestMarketCrossScan(double lookbackInSeconds, double minMarketCross, double currentMarketWidth)
        {
            List<MarketCrossScanResult> results = ScannerClient.RequestMarketCrossScan(lookbackInSeconds, minMarketCross, currentMarketWidth);
            return results;
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
            if (OmsCore.User != null)
            {
                ScannerClient.RegisterClient(OmsCore.User.Username, "ZeroPlus OMS App", version!, Dns.GetHostName());
                if (_subscribedToTradeFeed)
                {
                    SubscribeToTradeFeed(_subscribedToAllTradeFeed);
                }
                SubscribeToSymbolStats();
            }
            else
            {
                ScannerClient.RegisterClient("Excel", "ZeroPlus OMS AddIn", version!, Dns.GetHostName());
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
