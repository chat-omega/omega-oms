using EMAServer.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.MessageObjects;
using ZeroPlus.Oms.Config;
using ZeroPlusUtilities.QueueUtilities;

namespace ZeroPlus.Oms.Clients
{
    public class EmaServerClientModel : SubscriptionProvider, IDisposable
    {
        private EMAServerClient Client { get; set; }
        internal OmsCore OmsCore { get; set; }
        public bool IsDisposed { get; set; }

        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        public void Initialize(EMAServerClient client)
        {
            Client = client;
            Client.ReadyStateChanged += Client_ReadyStateChangedEvent;
            Client.OnEmaRTDPush -= OnUpdate;
            Client.OnEmaRTDPush += OnUpdate;
        }

        private void Client_ReadyStateChangedEvent(object sender, ReadyStateChangedEventArgs e)
        {
            ConnectionStatusChangedEvent?.Invoke(e.IsReady);
        }

        private void OnUpdate(object sender, APIEMAData clientMessage)
        {
            SubscriptionKey key = ConvertCommsToSubscriptionKey(clientMessage.Type, clientMessage.Symbol);
            Update(clientMessage.Symbol, key.Type, clientMessage, true);
        }

        public bool Start()
        {
            try
            {
                ClientInfo clientInfo = new(OmsCore.Config);
                clientInfo.SetClientInfo(OmsConfig.GatewayGuid);
                var user = OmsCore.User ?? throw new ApplicationException("Invalid Oms User configured");
                int serverPort = int.Parse(OmsCore.Config.EmaServerClientConfig.ServerEMAPort);
                string serverAddress = OmsCore.Config.EmaServerClientConfig.ServerEMAAddress;
                Client.StartAsync(clientInfo.GetRegistrationString(), serverAddress, serverPort);
                var clientVersion = Version.Parse(clientInfo?.ClientVersion);
                Client.SendSetClientNameMsg("ZeroPlus OMS", user.Username, clientVersion);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Stop()
        {
            Client?.Disconnect();
        }
        public void Restart()
        {
            Stop();
            Start();
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            var (type, underlyingSymbol) = ConvertSubscriptionKeyToComms(subscription);
            Client?.Subscribe(subscription.Symbol, underlyingSymbol, type);
        }

        private static (captureType, string) ConvertSubscriptionKeyToComms(SubscriptionKey subscription)
        {
            captureType type = subscription.Type switch
            {
                Models.Data.Enums.SubscriptionFieldType.Ema => captureType.midpoint,
                Models.Data.Enums.SubscriptionFieldType.SpreadEma => captureType.spread,
                Models.Data.Enums.SubscriptionFieldType.DeltaAdjEma => captureType.deltaadjoption,
                Models.Data.Enums.SubscriptionFieldType.VolaEma => captureType.deltaadjvolatheo,
                Models.Data.Enums.SubscriptionFieldType.BidEma => captureType.bid,
                Models.Data.Enums.SubscriptionFieldType.AskEma => captureType.ask,
                _ => captureType.none,
            };
            string underlyingSymbol = new SymbolLib.SymbolCodec(subscription.Symbol).UnderlyingSymbol();
            // FIXME: DUMB HACK - EMAServer underlying should match spec in SymbolCodec
            switch (underlyingSymbol)
            {
                case "$SPX":
                    underlyingSymbol = "SPY";
                    break;
                case "$NDX":
                    underlyingSymbol = "QQQ";
                    break;
                case "$RUT":
                    underlyingSymbol = "IWM";
                    break;
            }
            return (type, underlyingSymbol);
        }

        private static SubscriptionKey ConvertCommsToSubscriptionKey(captureType captureType, string symbol)
        {
            var type = captureType switch
            {
                captureType.midpoint => Models.Data.Enums.SubscriptionFieldType.Ema,
                captureType.spread => Models.Data.Enums.SubscriptionFieldType.SpreadEma,
                captureType.deltaadjoption => Models.Data.Enums.SubscriptionFieldType.DeltaAdjEma,
                captureType.deltaadjvolatheo => Models.Data.Enums.SubscriptionFieldType.VolaEma,
                captureType.bid => Models.Data.Enums.SubscriptionFieldType.BidEma,
                captureType.ask => Models.Data.Enums.SubscriptionFieldType.AskEma,
                _ => Models.Data.Enums.SubscriptionFieldType.Token,
            };
            return new SubscriptionKey(symbol, type);
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            var (type, underlyingSymbol) = ConvertSubscriptionKeyToComms(subscription);
            Client?.Unsubscribe(subscription.Symbol, underlyingSymbol, type);
        }

        #region Setup Options from OMS Config
        
        #endregion

        public void Dispose()
        {
            Client.ReadyStateChanged -= Client_ReadyStateChangedEvent;
            Client.OnEmaRTDPush -= OnUpdate;
        }
    }
}
