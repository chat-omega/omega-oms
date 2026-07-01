using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.OrderRouting;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Exceptions;
using RoutingVenue = ZeroPlus.Models.Data.Enums.Venue;

namespace ZeroPlus.Oms.Clients
{
    public class AutoTraderClient : INotifyPropertyChanged
    {
        private readonly IOrderUpdateManager _orderUpdateManager;
        public event PropertyChangedEventHandler PropertyChanged;
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        public event Action AccountsAndRoutesLoaded;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, SmartStrategyData> _matrixSmartRoutes = new();

        private bool _isConnected;
        private readonly HashSet<string> _brokers = [];
        private readonly Dictionary<string, HashSet<string>> _brokerToRoutes = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<int>> _routeToOrderTypeIds = [];
        private readonly ConcurrentDictionary<string, ConcurrentBag<OrderRoutingInfoModel>> _routeToRoutingInfos = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<RouteFilterKey, ClassifiedRoutes> _filteredRoutesCache = new();
        private readonly ConcurrentDictionary<string, int> _venueNameToId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _internalBrokers = new(StringComparer.OrdinalIgnoreCase);
        private uint? _userId;
        private IOrderInfoUpdateHandler _globalOrderInfoUpdate;

        public IAutoTraderClient Client { get; set; }
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

        public List<Account> Accounts => Client.Accounts;
        public IReadOnlyDictionary<string, int> VenueNameToId => _venueNameToId;
        public IReadOnlyCollection<string> InternalBrokers => _internalBrokers;

        public AutoTraderClient(IOrderUpdateManager orderUpdateManager)
        {
            _orderUpdateManager = orderUpdateManager;
        }

        public void Initialize(IAutoTraderClient orderGatewayClient)
        {
            _orderUpdateManager.RegisterClient(orderGatewayClient);
            Client = orderGatewayClient;
            Client.ClientConnected += OnClient_ClientConnected;
            Client.ClientDisconnected += OnClient_ClientDisconnected;
            Client.AccountsAndRoutesInitialized += async () =>
            {
                await LoadAccountsAndRoutes();
                AccountsAndRoutesLoaded?.Invoke();
            };
        }

        private async void OnClient_ClientConnected()
        {
            IsConnected = true;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                RegisterClient();
                await LoadAccountsAndRoutes();
                await LoadMatrixSmartStrategyRoutes();
                WarmFilteredRoutesCache();
            }
        }

        private void WarmFilteredRoutesCache()
        {
            try
            {
                var brokers = new List<string> { null };
                var defaultBroker = OmsCore.Config?.DefaultBroker;
                if (!string.IsNullOrWhiteSpace(defaultBroker)
                    && !brokers.Contains(defaultBroker, StringComparer.OrdinalIgnoreCase))
                {
                    brokers.Add(defaultBroker);
                }
                foreach (var b in _brokers)
                {
                    if (!brokers.Contains(b, StringComparer.OrdinalIgnoreCase))
                    {
                        brokers.Add(b);
                    }
                }

                var accounts = new List<string> { null };
                var userAccounts = OmsCore.User?.Accounts;
                if (userAccounts != null)
                {
                    foreach (var acc in userAccounts)
                    {
                        if (!string.IsNullOrWhiteSpace(acc)
                            && !accounts.Contains(acc, StringComparer.OrdinalIgnoreCase))
                        {
                            accounts.Add(acc);
                        }
                    }
                }

                var orderTypeFilters = new OrderType[][]
                {
                    null,
                    [OrderType.Option],
                    [OrderType.Complex],
                    [OrderType.Stock],
                };

                var venues = new List<RoutingVenue?> { null };
                foreach (RoutingVenue v in Enum.GetValues(typeof(RoutingVenue)))
                {
                    venues.Add(v);
                }

                foreach (var broker in brokers)
                {
                    foreach (var orderTypes in orderTypeFilters)
                    {
                        foreach (var account in accounts)
                        {
                            foreach (var venue in venues)
                            {
                                var key = new RouteFilterKey(broker, orderTypes, account, venue, activeOnly: true);
                                GetOrComputeRoutes(key, k =>
                                    string.IsNullOrWhiteSpace(k.Broker)
                                        ? ComputeAllRoutes(k)
                                        : ComputeRoutesForBroker(k));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn(ex, nameof(WarmFilteredRoutesCache));
            }
        }

        private async Task LoadAccountsAndRoutes()
        {
            _brokers.Clear();
            _brokerToRoutes.Clear();
            _routeToOrderTypeIds.Clear();
            _routeToRoutingInfos.Clear();
            _filteredRoutesCache.Clear();
            _venueNameToId.Clear();
            _internalBrokers.Clear();

            var accounts = await Client.RequestAccountsAsync();
            foreach (var account in accounts)
            {
                foreach (var route in account.Routes)
                {
                    var name = route?.Route;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    AddRouteInfo(route);

                    if (IsSorRouteType(route.RouteType))
                    {
                        continue;
                    }

                    var broker = route.Broker;
                    if (string.IsNullOrWhiteSpace(broker))
                    {
                        continue;
                    }

                    _brokers.Add(broker);
                    if (!_brokerToRoutes.TryGetValue(broker, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _brokerToRoutes[broker] = set;
                    }
                    set.Add(name);

                    if (!_routeToOrderTypeIds.TryGetValue(name, out var orderTypeIdSet))
                    {
                        orderTypeIdSet = new HashSet<int>();
                        _routeToOrderTypeIds[name] = orderTypeIdSet;
                    }
                    orderTypeIdSet.Add(route.OrderTypeId);
                }
            }

            DetectInternalBrokers();
            PublishBrokersToConfig();
        }

        private void DetectInternalBrokers()
        {
            var brokerToInfos = new Dictionary<string, List<OrderRoutingInfoModel>>(StringComparer.OrdinalIgnoreCase);
            foreach (var bag in _routeToRoutingInfos.Values)
            {
                if (bag == null) continue;
                foreach (var info in bag)
                {
                    if (info == null || string.IsNullOrEmpty(info.Broker)) continue;
                    if (!brokerToInfos.TryGetValue(info.Broker, out var list))
                    {
                        list = new List<OrderRoutingInfoModel>();
                        brokerToInfos[info.Broker] = list;
                    }
                    list.Add(info);
                }
            }
            foreach (var (broker, list) in brokerToInfos)
            {
                if (list.Count > 0 && list.All(IsSyntheticEntry))
                {
                    _internalBrokers.Add(broker);
                }
            }
        }

        private static bool IsSyntheticEntry(OrderRoutingInfoModel info)
        {
            if (info == null || string.IsNullOrEmpty(info.Broker) || string.IsNullOrEmpty(info.Route))
            {
                return false;
            }
            var expected = $"{info.Broker}-{info.Route}";
            return string.Equals(info.ExpectedName, expected, StringComparison.OrdinalIgnoreCase)
                && string.Equals(info.FixExpectedName, expected, StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<OrderRoutingInfoModel> EnumerateRouteInfos()
        {
            foreach (var bag in _routeToRoutingInfos.Values)
            {
                if (bag == null) continue;
                foreach (var info in bag)
                {
                    if (info != null) yield return info;
                }
            }
        }

        public bool IsInternalBroker(string broker)
        {
            return !string.IsNullOrEmpty(broker) && _internalBrokers.Contains(broker);
        }

        public bool TryGetRouteInfos(string route, out IReadOnlyCollection<OrderRoutingInfoModel> infos)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                infos = null;
                return false;
            }
            if (_routeToRoutingInfos.TryGetValue(route, out var bag) && bag != null && !bag.IsEmpty)
            {
                infos = bag;
                return true;
            }
            infos = null;
            return false;
        }

        private void AddRouteInfo(OrderRoutingInfoModel route)
        {
            if (string.IsNullOrWhiteSpace(route?.Route))
            {
                return;
            }

            var routeInfos = _routeToRoutingInfos.GetOrAdd(route.Route, _ => new ConcurrentBag<OrderRoutingInfoModel>());
            routeInfos.Add(route);
            
            if (!string.IsNullOrWhiteSpace(route.Venue))
            {
                _venueNameToId.TryAdd(route.Venue, route.VenueId);
            }
        }

        private void PublishBrokersToConfig()
        {
            try
            {
                OmsCore.Config?.SetAvailableBrokers(_brokers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PublishBrokersToConfig));
            }
        }

        private async Task LoadMatrixSmartStrategyRoutes()
        {
            var scrapeConfigs = LoadMatrixSmartStrategyConfigs<ScrapeStrategyData>(ScrapeStrategyData.ConfigId);
            var seekerConfigs = LoadMatrixSmartStrategyConfigs<SeekerStrategyData>(SeekerStrategyData.ConfigId);
            var seekerSpreadConfigs = LoadMatrixSmartStrategyConfigs<SeekerSpreadStrategyData>(SeekerSpreadStrategyData.ConfigId);
            var syntheticSpreadConfigs = LoadMatrixSmartStrategyConfigs<SyntheticSpreadStrategyData>(SyntheticSpreadStrategyData.ConfigId);

            await Task.WhenAll(scrapeConfigs, seekerConfigs, seekerSpreadConfigs, syntheticSpreadConfigs);
        }

        private async Task LoadMatrixSmartStrategyConfigs<T>(int id) where T : SmartStrategyData
        {
            try
            {
                var configs = await OmsCore.GatewayClient.RequestConfigsAsync(id);
                if (configs != null)
                {
                    foreach (var config in configs)
                    {
                        await LoadStrategyDataFromConfig<T>(config);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadMatrixSmartStrategyConfigs));
            }
        }

        private async Task LoadStrategyDataFromConfig<T>(ConfigSave scrapeConfig) where T : SmartStrategyData
        {
            try
            {
                var fullConfig = await OmsCore.GatewayClient.RequestConfigDataAsync(scrapeConfig.Id);
                var config = JsonConvert.DeserializeObject<T>(fullConfig.ConfigJson);
                _matrixSmartRoutes[fullConfig.Title] = config;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadStrategyDataFromConfig));
            }
        }

        public ICollection<string> GetRoutes(
            OrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return GetClassifiedRoutes(orderTypes, account, venue, activeOnly).Combined;
        }

        public ICollection<string> GetBrokers()
        {
            return _brokers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public ICollection<string> GetRoutesForBroker(
            string broker,
            OrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            return GetClassifiedRoutesForBroker(broker, orderTypes, account, venue, activeOnly).Combined;
        }

        public ClassifiedRoutes GetClassifiedRoutes(
            OrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            var key = new RouteFilterKey(broker: null, orderTypes, account, venue, activeOnly);
            return GetOrComputeRoutes(key, ComputeAllRoutes);
        }

        public ClassifiedRoutes GetClassifiedRoutesForBroker(
            string broker,
            OrderType[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false)
        {
            var key = new RouteFilterKey(broker, orderTypes, account, venue, activeOnly);
            return GetOrComputeRoutes(key, ComputeRoutesForBroker);
        }

        private ClassifiedRoutes GetOrComputeRoutes(RouteFilterKey key, Func<RouteFilterKey, ClassifiedRoutes> compute)
        {
            if (_filteredRoutesCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var result = compute(key);
            if (!result.IsEmpty)
            {
                _filteredRoutesCache.TryAdd(key, result);
            }

            return result;
        }

        private ClassifiedRoutes ComputeAllRoutes(RouteFilterKey key)
        {
            var dma = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (route, infos) in _routeToRoutingInfos)
            {
                if (HasSorInfo(infos)
                    && SorInfoMatchesFilters(infos, key.OrderTypes, key.Account, key.Venue, key.ActiveOnly))
                {
                    sor.Add(route);
                }

                if (NonSorInfoMatchesFilters(infos, key.OrderTypes, key.Account, key.Venue, key.ActiveOnly))
                {
                    dma.Add(route);
                }
            }

            return new ClassifiedRoutes(SortAlphabetic(dma), SortAlphabetic(sor));
        }

        private ClassifiedRoutes ComputeRoutesForBroker(RouteFilterKey key)
        {
            var dma = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (route, infos) in _routeToRoutingInfos)
            {
                if (HasSorInfo(infos)
                    && SorInfoMatchesFilters(infos, key.OrderTypes, key.Account, key.Venue, key.ActiveOnly))
                {
                    sor.Add(route);
                }
            }

            if (!string.IsNullOrWhiteSpace(key.Broker) && _brokerToRoutes.TryGetValue(key.Broker, out var brokerRoutes))
            {
                foreach (var route in brokerRoutes)
                {
                    if (_routeToRoutingInfos.TryGetValue(route, out var infos)
                        && NonSorInfoMatchesFilters(infos, key.OrderTypes, key.Account, key.Venue, key.ActiveOnly, key.Broker))
                    {
                        dma.Add(route);
                    }
                }
            }

            return new ClassifiedRoutes(SortAlphabetic(dma), SortAlphabetic(sor));
        }

        private static string[] SortAlphabetic(HashSet<string> items)
        {
            if (items.Count == 0)
            {
                return Array.Empty<string>();
            }
            return items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public readonly struct ClassifiedRoutes
        {
            public static ClassifiedRoutes Empty { get; } =
                new(Array.Empty<string>(), Array.Empty<string>());

            public string[] Dma { get; }
            public string[] Sor { get; }
            public string[] Combined { get; }

            public ClassifiedRoutes(string[] dma, string[] sor)
            {
                Dma = dma ?? Array.Empty<string>();
                Sor = sor ?? Array.Empty<string>();
                Combined = MergeSorted(Dma, Sor);
            }

            public bool IsEmpty => Dma.Length == 0 && Sor.Length == 0;

            private static string[] MergeSorted(string[] dma, string[] sor)
            {
                if (dma.Length == 0)
                {
                    return sor;
                }
                if (sor.Length == 0)
                {
                    return dma;
                }
                var merged = new HashSet<string>(
                    dma.Length + sor.Length,
                    StringComparer.OrdinalIgnoreCase);
                foreach (var r in dma)
                {
                    merged.Add(r);
                }
                foreach (var r in sor)
                {
                    merged.Add(r);
                }
                return merged
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        private readonly struct RouteFilterKey : IEquatable<RouteFilterKey>
        {
            public string Broker { get; }
            public int[] OrderTypes { get; }
            private string OrderTypesKey { get; }
            public string Account { get; }
            public RoutingVenue? Venue { get; }
            public bool ActiveOnly { get; }

            public RouteFilterKey(
                string broker,
                OrderType[] orderTypes,
                string account,
                RoutingVenue? venue,
                bool activeOnly)
            {
                Broker = broker;
                OrderTypes = NormalizeOrderTypes(orderTypes);
                OrderTypesKey = OrderTypes.Length == 0
                    ? string.Empty
                    : string.Join(",", OrderTypes);
                Account = account;
                Venue = venue;
                ActiveOnly = activeOnly;
            }

            public bool Equals(RouteFilterKey other) =>
                string.Equals(Broker, other.Broker, StringComparison.OrdinalIgnoreCase)
                && string.Equals(OrderTypesKey, other.OrderTypesKey, StringComparison.Ordinal)
                && string.Equals(Account, other.Account, StringComparison.OrdinalIgnoreCase)
                && Venue == other.Venue
                && ActiveOnly == other.ActiveOnly;

            public override bool Equals(object obj) => obj is RouteFilterKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(
                Broker?.ToLowerInvariant(),
                OrderTypesKey,
                Account?.ToLowerInvariant(),
                Venue,
                ActiveOnly);

            public override string ToString()
            {
                return $"{Broker}-[{OrderTypesKey}]-{Account}-{Venue}-{ActiveOnly}";
            }

            private static int[] NormalizeOrderTypes(OrderType[] orderTypes)
            {
                if (orderTypes == null || orderTypes.Length == 0)
                {
                    return Array.Empty<int>();
                }

                return orderTypes
                    .Select(x => (int)x)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();
            }
        }

        private static bool HasSorInfo(ConcurrentBag<OrderRoutingInfoModel> infos)
        {
            if (infos == null)
            {
                return false;
            }
            foreach (var info in infos)
            {
                if (info != null && IsSorRouteType(info.RouteType))
                {
                    return true;
                }
            }
            return false;
        }

        private bool SorInfoMatchesFilters(
            ConcurrentBag<OrderRoutingInfoModel> infos,
            int[] orderTypes,
            string account,
            RoutingVenue? venue,
            bool activeOnly)
        {
            if (infos == null)
            {
                return false;
            }
            foreach (var info in infos)
            {
                if (info != null
                    && IsSorRouteType(info.RouteType)
                    && RouteInfoMatchesFilters(info, orderTypes, account, venue, activeOnly))
                {
                    return true;
                }
            }
            return false;
        }

        private bool NonSorInfoMatchesFilters(
            ConcurrentBag<OrderRoutingInfoModel> infos,
            int[] orderTypes,
            string account,
            RoutingVenue? venue,
            bool activeOnly,
            string broker = null)
        {
            if (infos == null)
            {
                return false;
            }
            foreach (var info in infos)
            {
                if (info != null
                    && !IsSorRouteType(info.RouteType)
                    && RouteInfoMatchesFilters(info, orderTypes, account, venue, activeOnly, broker))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSorRouteType(string routeType)
        {
            return string.Equals(routeType, nameof(RouteType.SOR), StringComparison.OrdinalIgnoreCase);
        }

        private bool RouteInfoMatchesFilters(
            OrderRoutingInfoModel routeInfo,
            int[] orderTypes = null,
            string account = null,
            RoutingVenue? venue = null,
            bool activeOnly = false,
            string broker = null)
        {
            if (routeInfo == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(broker) &&
                !string.Equals(routeInfo.Broker, broker, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (orderTypes != null
                && orderTypes.Length > 0
                && !orderTypes.Contains(routeInfo.OrderTypeId))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(account) &&
                !string.Equals(routeInfo.Acronym, account, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (venue.HasValue && !VenueMatches(routeInfo, venue.Value))
            {
                return false;
            }

            if (activeOnly && !routeInfo.Active)
            {
                return false;
            }

            return true;
        }

        private bool VenueMatches(OrderRoutingInfoModel routeInfo, RoutingVenue venue)
        {
            var name = venue.ToString();
            if (_venueNameToId.TryGetValue(name, out var id))
            {
                return routeInfo.VenueId == id;
            }

            return string.Equals(routeInfo.Venue, name, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsMatrixSmartRoute(string route, out SmartStrategyData strategyData)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                strategyData = null;
                return false;
            }

            return _matrixSmartRoutes.TryGetValue(route, out strategyData);
        }

        public bool IsZpSmartRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return false;
            }

            return _routeToRoutingInfos.TryGetValue(route, out var infos) && HasSorInfo(infos);
        }

        public RouteType GetRouteKind(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return RouteType.DMA;
            }

            if (_routeToRoutingInfos.TryGetValue(route, out var infos) && HasSorInfo(infos))
            {
                return RouteType.SOR;
            }

            return RouteType.DMA;
        }

        private void RegisterClient()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (OmsCore.User != null)
            {
                Client.AuthenticateClient(OmsCore.User.ID, OmsCore.User.Username, Guid.NewGuid().ToString(), "ZeroPlus OMS App", version!, Dns.GetHostName());
            }
            else
            {
                Client.DisconnectAndStop();
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
            await Task.Run(() => Client?.ConnectAndStart());
            return false;
        }

        public async Task StopAsync()
        {
            await Task.Run(() =>
            {
                Client?.DisconnectAndStop();
            });
        }
        #endregion

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public bool TryGetAccount(string account, out Account accountModel)
        {
            accountModel = Client?.Accounts.FirstOrDefault(x => string.Equals(x.Acronym, account, StringComparison.OrdinalIgnoreCase));
            return accountModel != null;
        }

        public void SendSingleOrder(SingleOrderRequest singleOrderRequest, IOrderInfoUpdateHandler orderInfoUpdateHandler)
        {
            CheckConnection();
            _orderUpdateManager.RegisterListener(singleOrderRequest.ClientOrderId, orderInfoUpdateHandler);
            Client.SendSingleOrder(singleOrderRequest);
        }

        public void SendPairOrder(PairOrderRequest pairOrderRequest, IOrderInfoUpdateHandler orderInfoUpdateHandler)
        {
            CheckConnection();
            _orderUpdateManager.RegisterListener(pairOrderRequest.ClientOrderId, orderInfoUpdateHandler);
            pairOrderRequest.UserId = GetUserId();
            Client.SendPairOrder(pairOrderRequest);
        }

        public void SendAutoTraderConfig(AutoTraderConfig config)
        {
            Client.SendAutoTraderConfig(config);
        }

        public void RegisterOrderUpdateHandler(IOrderInfoUpdateHandler orderInfoUpdateHandler)
        {
            _globalOrderInfoUpdate = orderInfoUpdateHandler;
        }

        public void SendOrder(IOrderSlim order)
        {
            if (_globalOrderInfoUpdate == null)
            {
                throw new SlimException("Order Handler Missing");
            }
            CheckConnection();
            RegisterSender(_globalOrderInfoUpdate, order.LocalID);
            order.UserId = GetUserId();
            LogSlimOrderToWire(nameof(SendOrder), order);
            Client.SendOrder(order);
        }

        public void SendOrder(IOrder order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.LocalID);
            order.UserId = GetUserId();
            LogSlimOrderToWire(nameof(SendOrder), order);
            Client.SendOrder(order);
        }

        public void SendSlimOrder(IOrderSlim order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.LocalID);
            order.UserId = GetUserId();
            LogSlimOrderToWire(nameof(SendSlimOrder), order);
            Client.SendOrder(order);
        }

        public void SendOrder(SyntheticSpread order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.ClientGuid);
            order.UserId = GetUserId();
            Client.SendOrder(order);
        }

        public void SendOrder(Scrape order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.ClientGuid);
            order.UserId = GetUserId();
            Client.SendOrder(order);
        }

        public void SendOrder(Seeker order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.ClientGuid);
            order.UserId = GetUserId();
            Client.SendOrder(order);
        }

        public void SendOrder(SeekerSpread order, IOrderInfoUpdateHandler orderInfoUpdate)
        {
            CheckConnection();
            RegisterSender(orderInfoUpdate, order.ClientGuid);
            order.UserId = GetUserId();
            Client.SendOrder(order);
        }

        private static void LogSlimOrderToWire(string method, IOrderSlim order)
        {
            if (order == null) return;
            try
            {
                _log.Info($"{method} AUTOTRADER -> Symbol: {order.Symbol}, Route: {order.Route}, Px: {order.Price}, Qty: {order.Quantity}, SubType: {order.SubType}, Local Id: {order.LocalID}");
            }
            catch (Exception ex)
            {
                _log.Warn(ex, $"{method} AUTOTRADER -> wire log failed");
            }
        }

        public void CancelOrder(CancelRequest request)
        {
            CheckConnection();
            request.UserId = GetUserId();
            Client.SendCancelRequest(request);
        }

        public void SendMassCancelRequest(MassCancelRequest massCancelRequest)
        {
            CheckConnection();
            Client.SendMassCancelRequest(massCancelRequest);
        }

        public void ModifyOrder(ModifyRequest request)
        {
            CheckConnection();
            request.UserId = GetUserId();
            Client.SendModifyRequest(request);
        }

        public void ModifyOrder(ModifySmartRequest request)
        {
            CheckConnection();
            request.UserId = GetUserId();
            Client.SendModifySmartRequest(request);
        }

        private void CheckConnection()
        {
            if (!Client.IsClientConnected)
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

        public void CancelGroup(string token)
        {
            Client.SendCancelRequest(token);
        }

        private void RegisterSender(IOrderInfoUpdateHandler orderInfoUpdate, string localId)
        {
            _orderUpdateManager.RegisterListener(localId, orderInfoUpdate);
        }

        public string GetLoLaRoute(string initiatorRoute)
        {
            if (string.IsNullOrWhiteSpace(initiatorRoute))
            {
                return initiatorRoute;
            }
            return initiatorRoute;
        }
    }
}
