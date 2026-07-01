using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using ZeroPlus.Cob.Client.Interfaces;
using ZeroPlus.Cob.Client.Models;
using ZeroPlus.Databento.Client.Interfaces;
using ZeroPlus.EdgeScanner.Client.Interfaces;
using ZeroPlus.Ema.Client.Interfaces;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.IbGateway.Client.Interfaces;
using ZeroPlus.Interpolator.Client.Interfaces;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Utils;
using ZeroPlus.Raptor.Client.Config.Interfaces;
using ZeroPlus.Raptor.Client.Interfaces;
using ZeroPlus.HubTron.Client.Interfaces;
using ZeroPlus.Theos.Client.Interfaces;

namespace ZeroPlus.Oms.Clients
{
    public class UpdateManager : SubscriptionProvider, IUpdateManager
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private List<IRaptorClient> _raptorClients = [];
        private IEmaClient _emaClient;
        private IInterpolatorClient _interpolatorClient;
        private ITheosClient _theosClient;
        private IIbGatewayClient _ibGatewayClient;
        private IDatabentoClient _databentoClient;
        private IEdgeScannerClient _edgeScannerCacheClient;
        private bool _isConnected;
        private DateTime _serverOriginTime;
        private DateTime _serverTime;
        private IHerculesClient _herculesClient;
        private ICobClient _cobClient;
        private IHubTronClient _hubTronClient;
        private readonly ConcurrentDictionary<int, DerivedValueUpdateModel> _derivedValueUpdateModels = [];
        private readonly ConcurrentDictionary<byte, IRaptorClient> _modelIdToRaptorClientsMap = [];
        private static readonly ConcurrentDictionary<string, Tuple<BaseStrategy, string, string>> _strategyCache = new();
        public List<IRaptorClientConfig> RaptorClientConfigs { get; set; } = [];
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public double DatabentoLatencyMs { get; private set; } = double.NaN;

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime ServerOriginTime
        {
            get => _serverOriginTime;
            set
            {
                if (_serverOriginTime != value)
                {
                    _serverOriginTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime ServerTime
        {
            get => _serverTime;
            set
            {
                if (_serverTime != value)
                {
                    _serverTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Initialize(IEdgeScannerClient memCacheClient)
        {
            _edgeScannerCacheClient = memCacheClient;
            _edgeScannerCacheClient.ClientConnected += OnMemClient_ClientConnected;
            _edgeScannerCacheClient.EdgeToTheoUpdate += HandleUpdate;
        }

        public void Initialize(IEmaClient client)
        {
            _emaClient = client;
            _emaClient.ClientConnected += OnEmaClient_ClientConnected;
        }

        public void Initialize(IInterpolatorClient client)
        {
            _interpolatorClient = client;
            _interpolatorClient.ClientConnected += OnInterpolatorClient_ClientConnected;
            _interpolatorClient.TheoToMarketSpreadUpdate += OnTheoToMarketSpreadUpdate;
            _interpolatorClient.ImpliedQuoteUpdate += OnImpliedQuoteUpdate;
        }

        public void Initialize(ITheosClient client)
        {
            _theosClient = client;
        }

        private void OnImpliedQuoteUpdate(ImpliedQuoteUpdate update)
        {
            if (_interpolatorClient.TryGetSymbolFromId(update.Index, out var symbol))
            {
                update.Symbol = symbol;
                SendOutUpdate(update.Underlying, SubscriptionFieldType.ImpliedQuoteCross, update, false);
            }
        }

        private void OnTheoToMarketSpreadUpdate(TheoToMarketSpread update)
        {
            if (_interpolatorClient.TryGetSymbolFromId(update.TickerId, out var symbol))
            {
                SendOutUpdate(symbol, SubscriptionFieldType.TheoToMarketSpread, update);
            }
        }

        public void Initialize(IIbGatewayClient client)
        {
            _ibGatewayClient = client;
            _ibGatewayClient.IbQuoteUpdate += HandleUpdate;
            _ibGatewayClient.ClientConnected += OnIbGatewayClient_ClientConnected;
        }

        public void Initialize(IDatabentoClient client)
        {
            _databentoClient = client;
            _databentoClient.ClientConnected += OnDatabentoClient_ClientConnected;
            _databentoClient.FeedLatencyUpdated += OnFeedLatencyUpdated;
        }

        public void Initialize(IHubTronClient client)
        {
            _hubTronClient = client;
            _hubTronClient.RbboUpdateReceived += OnRbboUpdateReceived;
            _hubTronClient.ClientConnected += OnHubTronClient_ClientConnected;
        }

        public void Initialize(ICobClient client)
        {
            _cobClient = client;
            _cobClient.CobFeedUpdatedEvent += HandleUpdate;
            _cobClient.SpreadExchOrderUpdate += HandleUpdate;
            _cobClient.ClientConnected += OnCobClient_ClientConnected;
        }

        private void OnMemClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.TradeUpdate);
            Resubscribe(SubscriptionFieldType.TradeEdgeToTheo);
        }

        private void OnEmaClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.FullEma);
            _emaClient?.SetPerformanceMode(OmsCore.Config.PerformanceModeEnabled);
        }

        private void OnInterpolatorClient_ClientConnected()
        {
            _derivedValueUpdateModels.Clear();
            Resubscribe(SubscriptionFieldType.DerivedValues);
        }

        private void OnDatabentoClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.Mbp1);
            Resubscribe(SubscriptionFieldType.Cmbp1);
            _databentoClient?.SubscribeFeedLatency();
            _databentoClient?.SetPerformanceMode(OmsCore.Config.PerformanceModeEnabled);
        }

        private void OnFeedLatencyUpdated(double latencyMs)
        {
            DatabentoLatencyMs = latencyMs;
        }

        private void OnIbGatewayClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.IbAsk);
            Resubscribe(SubscriptionFieldType.IbBid);
            Resubscribe(SubscriptionFieldType.IbQuote);
            Resubscribe(SubscriptionFieldType.IbHistoricalData);
            Resubscribe(SubscriptionFieldType.IbData);
        }

        private void OnCobClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.Cob);
            Resubscribe(SubscriptionFieldType.CobOrders);
        }

        private void OnHubTronClient_ClientConnected()
        {
            Resubscribe(SubscriptionFieldType.Depth);
            Resubscribe(SubscriptionFieldType.Dig);
        }

        private void OnRbboUpdateReceived(string symbol, RbboUpdateModel model)
        {
            SendOutUpdate(symbol, SubscriptionFieldType.Depth, model);
        }

        public void Initialize(IHerculesClient herculesClient)
        {
            _herculesClient = herculesClient;
            _herculesClient.EdgeToTheoMappingHandler += OnEdgeToTheoMappingUpdate;
            _herculesClient.ClientConnected += () => Resubscribe(SubscriptionFieldType.PermEdgeToTheo);
        }

        public void Initialize(List<IRaptorClient> raptorClients)
        {
            _raptorClients = raptorClients;
            foreach (var raptorClient in raptorClients)
            {
                RaptorClientConfigs.Add(raptorClient.ClientConfig);
                raptorClient.ClientConnected += () => OnClient_ClientConnected(raptorClient);
                raptorClient.ClientDisconnected += () => OnClient_ClientDisconnected(raptorClient);
            }
        }

        public string GetModelDescription(int modelId)
        {
            _modelIdToRaptorClientsMap.TryGetValue((byte)modelId, out var client);
            return client?.ModelDescription ?? "Unknown Model";
        }

        private void OnClient_ClientConnected(IRaptorClient client)
        {
            IsConnected = _raptorClients[0].IsClientConnected;
            _modelIdToRaptorClientsMap[client.ModelId] = client;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            RegisterClient(client);
            Resubscribe();
            client.SetPerformanceMode(OmsCore.Config.PerformanceModeEnabled);
        }

        private void OnClient_ClientDisconnected(IRaptorClient client)
        {
            IsConnected = _raptorClients[0].IsClientConnected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
        }

        private void RegisterClient(IRaptorClient client)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (OmsCore.User != null)
            {
                client.RegisterClient(OmsCore.User.Username, "ZeroPlus OMS App", version!, Dns.GetHostName());
            }
            else
            {
                client.RegisterClient("Excel", "ZeroPlus OMS AddIn", version!, Dns.GetHostName());
            }
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
                foreach (var client in _raptorClients)
                {
                    try
                    {
                        client?.ConnectAndStart(openSharedMemory: false);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex,
                            nameof(StartAsync) + " " + client?.ClientConfig.ServerAddress + ":" +
                            client?.ClientConfig.ServerPort + " [" + client?.ModelId + "]");
                    }
                }
            });
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                foreach (var client in _raptorClients)
                {
                    try
                    {
                        client?.DisconnectAndStop();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex,
                            nameof(StopAsync) + " " + client?.ClientConfig.ServerAddress + ":" +
                            client?.ClientConfig.ServerPort + " [" + client?.ModelId + "]");
                    }
                }
            });
        }

        public async Task<HanweckUpdatesWithMatchingTimestampsResponse> RequestHanweckUpdatesWithMatchingTimestampsAsync(List<string> symbols)
        {
            var client = _raptorClients.FirstOrDefault();
            if (client == null)
            {
                return default;
            }
            return await client.RequestHanweckUpdatesWithMatchingTimestampsAsync(symbols);
        }

        #endregion

        private void OnEdgeToTheoMappingUpdate(string symbol, List<EdgeToTheoTrackerModel> mappings)
        {
            SendOutUpdate(symbol, SubscriptionFieldType.PermEdgeToTheo, mappings);
        }

        private void HandleUpdate(IbQuoteUpdateModel update)
        {
            SendOutUpdate(update.Symbol, SubscriptionFieldType.IbQuote, update);
        }

        private void HandleUpdate(OpenSpreadExchOrderModel model)
        {
            SendOutUpdate(model.Underlying, SubscriptionFieldType.CobOrders, model);
        }

        private void HandleUpdate(ICobData update)
        {
            if (update.Symbol != null)
            {
                if (string.IsNullOrWhiteSpace(update.SpreadDescription))
                {
                    if (!_strategyCache.TryGetValue(update.Symbol, out var cache))
                    {
                        OptionStrategy.TryIdentify(update.Symbol, out BaseStrategy baseStrategy, out string spreadId, out string spreadDescription);
                        cache = Tuple.Create(baseStrategy, spreadId, spreadDescription);
                        _strategyCache[update.Symbol] = cache;
                    }
                    update.BaseStrategy = cache.Item1;
                    update.SpreadId = cache.Item2;
                    update.SpreadDescription = cache.Item3;
                }
            }

            SendOutUpdate(update.Underlying, SubscriptionFieldType.Cob, update);
        }

        private void HandleUpdate(EdgeToTheoUpdateModel update)
        {
            SendOutUpdate(update.Symbol, SubscriptionFieldType.TradeEdgeToTheo, update);
        }

        public void HandleUpdate(ref DeltaAdjustedTheoDetailsModel update)
        {
            SendOutUpdate(update.Symbol, SubscriptionFieldType.DeltaAdjTheo, update.DeltaAdjustedTheo);
            SendOutUpdate(update.Symbol, SubscriptionFieldType.DeltaAdjTheoBase, $"Theo; {update.Theo}, Delta; {update.Delta}, Mid; {update.MidPrice}, Snapshot; {update.SnapShotTime:dd-MMM-yy hh:mm:ss.ffff}, AdjTheo; {update.DeltaAdjustedTheo}, BidTheo; {update.BidUpdate}, AskTheo; {update.AskUpdate}");
        }

        public void HandleUpdate(string symbol, SubscriptionFieldType updateType, double update, double bidUpdate, double askUpdate)
        {
            if (updateType == SubscriptionFieldType.DeltaAdjTheo)
            {
                SendOutUpdate(symbol, SubscriptionFieldType.DeltaAdjTheo, update);
            }
        }

        public void HandleUpdate(SubscriptionFieldType updateType, Dictionary<int, (double update, double bidUpdate, double askUpdate)> indexToUpdateMap)
        {
            if (updateType == SubscriptionFieldType.DeltaAdjTheo)
            {
                foreach (KeyValuePair<int, (double update, double bidUpdate, double askUpdate)> indexToUpdatePair in indexToUpdateMap)
                {
                    HandleUpdate(indexToUpdatePair);
                }
            }
        }

        public void HandleUpdate(int tickerId, SubscriptionFieldType updateType, double bidUpdate, double askUpdate, DateTime timestamp, Generated.QuoteChangeType bidChange, Generated.QuoteChangeType askChange, int bidSize, int askSize, double lastPrice, double latencyMs)
        {
            try
            {
                switch (updateType)
                {
                    case SubscriptionFieldType.Bar:
                    case SubscriptionFieldType.TopQuote:
                        if (_raptorClients[0].TryGetSymbolFromId(tickerId, out string symbol))
                        {
                            SendOutUpdate(symbol, updateType, new DoubleUpdateModel(timestamp, bidUpdate, askUpdate, bidChange, askChange, bidSize, askSize, lastPrice));
                        }
                        break;
                    case SubscriptionFieldType.Mbp1:
                    case SubscriptionFieldType.Cmbp1:
                        if (_databentoClient.TryGetSymbolFromId(tickerId, out symbol))
                        {
                            SendOutUpdate(symbol, updateType, new DoubleUpdateModel(timestamp, bidUpdate, askUpdate, bidChange, askChange, bidSize, askSize, lastPrice));
                        }
                        break;
                    case SubscriptionFieldType.ZpTheo:
                        if (_theosClient.TryGetSymbolFromId(tickerId, out symbol))
                        {
                            SendOutUpdate(symbol, updateType, new DoubleUpdateModel(timestamp, bidUpdate, askUpdate, bidChange, askChange, bidSize, askSize, lastPrice));
                        }
                        break;
                    case SubscriptionFieldType.Dig:
                        if (_hubTronClient.TryGetSymbolFromId(tickerId, out symbol))
                        {
                            SendOutUpdate(symbol, updateType, new DoubleUpdateModel(timestamp, bidUpdate, askUpdate, bidChange, askChange, bidSize, askSize, lastPrice));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUpdate));
            }
        }

        public void HandleUpdate(int tickerId, SubscriptionFieldType fieldType, uint updateSequence, ulong underlyingTimestamp, ulong snapshotTimestamp, ulong hanweckTimestamp, double theo, double delta, double gamma, double vega, double theta, double baseLine, double implied, double latestMidPrice, double snapshotMidPrice, double deltaAdjustedTheo, bool jumpDetected)
        {
            try
            {
                if (fieldType == SubscriptionFieldType.DebugValue)
                {
                    _log.Debug("DebugValue update received. TickerId: {}, UpdateSequence: {}, UnderlyingTimestamp: {}, SnapshotTimestamp: {}, HanweckTimestamp: {}, Theo: {}, Delta: {}, Gamma: {}, Vega: {}, Theta: {}, BaseLine: {}, Implied: {}, LatestMidPrice: {}, SnapshotMidPrice: {}, DeltaAdjustedTheo: {}, JumpDetected: {}",
                        tickerId, updateSequence, underlyingTimestamp, snapshotTimestamp, hanweckTimestamp, theo, delta, gamma, vega, theta, baseLine, implied, latestMidPrice, snapshotMidPrice, deltaAdjustedTheo, jumpDetected);
                }
                else
                {
                    if (_raptorClients[0].TryGetSymbolFromId(tickerId, out string symbol))
                    {
                        DeltaAdjTheo deltaAdjTheo = new(symbol,
                                                        updateSequence,
                                                        deltaAdjustedTheo,
                                                        baseLine,
                                                        latestMidPrice,
                                                        jumpDetected);
                        SendOutUpdate(symbol, SubscriptionFieldType.DeltaAdjTheo, deltaAdjTheo);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUpdate));
            }
        }

        public void HandleUpdate(ref AdjTheoUpdate theoUpdate)
        {
            if (_modelIdToRaptorClientsMap.TryGetValue(theoUpdate.ModelId, out var client) && client.TryGetSymbolFromId(theoUpdate.TickerId, out string symbol))
            {
                DeltaAdjTheo deltaAdjTheo = new(symbol,
                                                theoUpdate.Sequence,
                                                theoUpdate.Theo,
                                                theoUpdate.SmoothTheo,
                                                theoUpdate.Underlying,
                                                theoUpdate.JumpDetected,
                                                theoUpdate.SecondaryTheo,
                                                theoUpdate.SecondaryTheoAdj,
                                                theoUpdate.PriceMetric,
                                                theoUpdate.ModelId,
                                                theoUpdate.SecondaryVol,
                                                theoUpdate.ChangeInPremium,
                                                theoUpdate.SecondarySpot,
                                                theoUpdate.DaEma,
                                                theoUpdate.VolaEma);
                SendOutUpdate(symbol, SubscriptionFieldType.DeltaAdjTheo, deltaAdjTheo, theoUpdate.ModelId == 0);
            }
        }

        public void HandleUpdate(int tickerId, SubscriptionFieldType updateType, EmaUpdateModel emaUpdate)
        {
            try
            {
                switch (updateType)
                {
                    case SubscriptionFieldType.FullEma:
                        if (_emaClient.TryGetSymbolFromId(tickerId, out string symbol))
                        {
                            SendOutUpdate(symbol, updateType, emaUpdate);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUpdate));
            }
        }

        private void HandleUpdate(KeyValuePair<int, (double update, double bidUpdate, double askUpdate)> indexToUpdatePair)
        {
            try
            {
                int index = indexToUpdatePair.Key;
                if (_raptorClients[0].TryGetSymbolFromId(index, out string symbol))
                {
                    double update = indexToUpdatePair.Value.update;
                    SendOutUpdate(symbol, SubscriptionFieldType.DeltaAdjTheo, update);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUpdate));
            }
        }

        public void HandleUpdate(TimeFeedType timeFeedType, DateTime timeUpdate)
        {
            switch (timeFeedType)
            {
                case TimeFeedType.Origin:
                    ServerOriginTime = timeUpdate;
                    break;
                case TimeFeedType.Server:
                    ServerTime = timeUpdate;
                    break;
            }
        }

        public void HandleUpdate(GreekUpdateModel model)
        {
            int index = model.Index;
            if (_raptorClients[0].TryGetSymbolFromId(index, out string symbol))
            {
                SendOutUpdate(symbol, SubscriptionFieldType.FullHanweck, model);
            }
        }

        public void HandleUpdate(SlimGreekUpdateModel model)
        {
            if (_modelIdToRaptorClientsMap.TryGetValue(model.ModelId, out var raptorClient))
            {
                if (raptorClient.TryGetSymbolFromId(model.TickerId, out string symbol))
                {
                    SendOutUpdate(symbol, SubscriptionFieldType.VolaGreeks, model);
                }
            }
        }

        public void HandleUpdate(ref TradeUpdateModel update)
        {
            SendOutUpdate(update.SpreadId, SubscriptionFieldType.TradeUpdate, update);
        }

        private void SendOutUpdate(string symbol, SubscriptionFieldType type, object update, bool allowCaching = true)
        {
            try
            {
                SubscriptionKey subscriptionKey = new(symbol, type);
                if (TryGetSubscribers(subscriptionKey, out IDataSubscribers subscribers))
                {
                    subscribers.UpdateValues(update, isFromCache: false, allowCaching);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendOutUpdate));
            }
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;

            if (string.IsNullOrEmpty(symbol))
            {
                _log.Warn(nameof(Subscribe) + ". Symbol can not be empty. Type: " + type);
                return;
            }

            switch (type)
            {
                case SubscriptionFieldType.DeltaAdjTheo:
                case SubscriptionFieldType.DeltaAdjTheoBase:
                    foreach (var client in _raptorClients)
                    {
                        client.SubscribeDeltaAdjTheo(symbol);
                    }
                    break;
                case SubscriptionFieldType.VolaGreeks:
                    foreach (var client in _raptorClients)
                    {
                        client.Subscribe(symbol, type);
                    }
                    break;
                case SubscriptionFieldType.DebugValue:
                    _log.Debug("DebugValue subscription is not supported.");
                    break;
                case SubscriptionFieldType.Bar:
                case SubscriptionFieldType.TopQuote:
                case SubscriptionFieldType.FullHanweck:
                case SubscriptionFieldType.WeightedVega:
                    _raptorClients[0].Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.IbQuote:
                    _ibGatewayClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Cob:
                case SubscriptionFieldType.CobOrders:
                    _cobClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.TradeEdgeToTheo:
                    _edgeScannerCacheClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.ImpliedQuote:
                case SubscriptionFieldType.ImpliedQuoteCross:
                case SubscriptionFieldType.DerivedValues:
                case SubscriptionFieldType.TheoToMarketSpread:
                    _interpolatorClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.ZpTheo:
                    _theosClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.TradeUpdate:
                    // Todo: create new message that will support longer name for subscription key.
                    break;
                case SubscriptionFieldType.FullEma:
                    _emaClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.PermEdgeToTheo:
                    _herculesClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Mbp1:
                case SubscriptionFieldType.Cmbp1:
                    _databentoClient.Subscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Depth:
                case SubscriptionFieldType.Dig:
                    _hubTronClient.Subscribe(symbol, type);
                    break;
            }
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            if (string.IsNullOrEmpty(symbol))
            {
                _log.Warn(nameof(Unsubscribe) + ". Symbol can not be empty. Type: " + subscription);
                return;
            }
            SubscriptionFieldType type = subscription.Type;
            switch (type)
            {
                case SubscriptionFieldType.DeltaAdjTheo:
                case SubscriptionFieldType.DeltaAdjTheoBase:
                    foreach (var client in _raptorClients)
                    {
                        client.UnsubscribeDeltaAdjTheo(symbol);
                    }
                    break;
                case SubscriptionFieldType.VolaGreeks:
                    foreach (var client in _raptorClients)
                    {
                        client.Unsubscribe(symbol, type);
                    }
                    break;
                case SubscriptionFieldType.DebugValue:
                    _log.Debug("DebugValue subscription is not supported.");
                    break;
                case SubscriptionFieldType.Bar:
                case SubscriptionFieldType.TopQuote:
                case SubscriptionFieldType.FullHanweck:
                case SubscriptionFieldType.WeightedVega:
                    _raptorClients[0].Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.IbQuote:
                    _ibGatewayClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Cob:
                case SubscriptionFieldType.CobOrders:
                    _cobClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.TradeEdgeToTheo:
                    _edgeScannerCacheClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.ImpliedQuote:
                case SubscriptionFieldType.ImpliedQuoteCross:
                case SubscriptionFieldType.DerivedValues:
                case SubscriptionFieldType.TheoToMarketSpread:
                    _interpolatorClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.ZpTheo:
                    _theosClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.TradeUpdate:
                    // Todo: create new message that will support longer name for subscription key.
                    break;
                case SubscriptionFieldType.FullEma:
                    _emaClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.PermEdgeToTheo:
                    _herculesClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Mbp1:
                case SubscriptionFieldType.Cmbp1:
                    _databentoClient.Unsubscribe(symbol, type);
                    break;
                case SubscriptionFieldType.Depth:
                case SubscriptionFieldType.Dig:
                    _hubTronClient.Unsubscribe(symbol, type);
                    break;
            }
        }

        public UnderFitResult GetUnderFitResultModel(uint index)
        {
            return default;
        }

        public void HandleUpdate(UnderFitResult model)
        {
        }

        public DerivedValueUpdateModel GetDerivedValueUpdateModel(int tickerId)
        {
            return _derivedValueUpdateModels.GetOrAdd(tickerId, id => new DerivedValueUpdateModel() { TickerId = id });
        }

        public void HandleUpdate(DerivedValueUpdateModel model)
        {
            if (_interpolatorClient.TryGetSymbolFromId(model.TickerId, out string symbol))
            {
                SendOutUpdate(symbol, SubscriptionFieldType.DerivedValues, model);
            }
        }

        public string GetTheoConfig(TheoModel model, string symbol)
        {
            var index = (int)model - 1;
            var client = _raptorClients.ElementAtOrDefault(index);
            string config = null;
            if (client is { IsClientConnected: true })
            {
                config = client.GetVolaConfig(symbol).Content;
            }
            config ??= "{\"reason\": \"not connected\"}";

            return config.Trim().Trim('{', '}').Trim();
        }

        public void HandleUpdate(int tickerId, SubscriptionFieldType updateType, double value)
        {
            if (_raptorClients[0].TryGetSymbolFromId(tickerId, out string symbol))
            {
                SendOutUpdate(symbol, updateType, value);
            }
        }
    }
}
