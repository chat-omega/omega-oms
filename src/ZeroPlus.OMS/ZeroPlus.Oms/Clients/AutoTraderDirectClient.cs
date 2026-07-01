using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Exceptions;

namespace ZeroPlus.Oms.Clients
{
    public class AutoTraderDirectClient : INotifyPropertyChanged
    {
        private readonly IOrderUpdateManager _orderUpdateManager;
        public event PropertyChangedEventHandler PropertyChanged;
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private bool _isConnected;
        private uint? _userId;

        public IAutoTraderClient AutoTraderClient { get; set; }
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

        public List<Account> Accounts => AutoTraderClient.Accounts;

        public AutoTraderDirectClient(IOrderUpdateManager orderUpdateManager)
        {
            _orderUpdateManager = orderUpdateManager;
        }

        public void Initialize(IAutoTraderClient orderGatewayClient)
        {
            _orderUpdateManager.RegisterClient(orderGatewayClient);
            AutoTraderClient = orderGatewayClient;
            AutoTraderClient.ClientConnected += OnClient_ClientConnected;
            AutoTraderClient.ClientDisconnected += OnClient_ClientDisconnected;
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
                AutoTraderClient.AuthenticateClient(OmsCore.User.ID, OmsCore.User.Username, Guid.NewGuid().ToString(), "ZeroPlus OMS App", version!, Dns.GetHostName());
            }
            else
            {
                AutoTraderClient.DisconnectAndStop();
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
            await Task.Run(() => AutoTraderClient?.ConnectAndStart());
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                AutoTraderClient?.DisconnectAndStop();
            });
        }
        #endregion

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public bool TryGetAccount(string account, out Account accountModel)
        {
            accountModel = AutoTraderClient?.Accounts.FirstOrDefault(x => string.Equals(x.Acronym, account, StringComparison.OrdinalIgnoreCase));
            return accountModel != null;
        }

        public void SendOrder(IOrderSlim order, IOrderInfoUpdateHandler subscriber)
        {
            CheckConnection();
            RegisterSender(subscriber, order.LocalID);
            order.UserId = GetUserId();
            try
            {
                _log.Info($"{nameof(SendOrder)} AUTOTRADER LOCAL -> Symbol: {order?.Symbol}, Route: {order?.Route}, Px: {order?.Price}, Qty: {order?.Quantity}, SubType: {order?.SubType}, Local Id: {order?.LocalID}");
            }
            catch (Exception ex)
            {
                _log.Warn(ex, $"{nameof(SendOrder)} AUTOTRADER LOCAL -> wire log failed");
            }
            AutoTraderClient.SendOrder(order);
        }

        public void CancelOrder(CancelRequest request)
        {
            CheckConnection();
            request.UserId = GetUserId();
            AutoTraderClient.SendCancelRequest(request);
        }

        public void ModifyOrder(ModifyRequest request)
        {
            CheckConnection();
            request.UserId = GetUserId();
            AutoTraderClient.SendModifyRequest(request);
        }

        private void CheckConnection()
        {
            if (!AutoTraderClient.IsClientConnected)
            {
                throw new SlimException("Auto Trader not connected!");
            }
        }

        private uint GetUserId()
        {
            if (OmsCore.User == null)
            {
                throw new SlimException("User Not Logged In!");
            }

            _userId ??= (uint)OmsCore.User.ID;
            return _userId.Value;
        }

        private void RegisterSender(IOrderInfoUpdateHandler orderInfoUpdate, string localId)
        {
            _orderUpdateManager.RegisterListener(localId, orderInfoUpdate);
        }
    }
}
