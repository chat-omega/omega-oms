using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void NewClosedOrderRowAddedEventHandler(OmsOrderModel orderModel);
    public delegate void TradeFeedReceivedEventHandler(TradeFeedModel tradeFeed);
    public delegate void BlockUiEventHandler();
    public delegate void UnblockUiEventHandler();
    public delegate void CloseSubsUpdatedHandler(OmsOrderModel model);

    public class TransactionConsumerModel : SubscriptionProvider, IOrderFactory, IStatsProcessor
    {
        private const int CHECK_FOR_DUPLICATE_RESTING_ORDERS_INTERVAL = 5;
        private const int LIMIT = 1000;
        private const int INTERVAL = 1000;
        private const string SPX_BUCKET_ID = "$SPX BUCKET";
        private const string UNIVERSAL = "*";
        private readonly object _bufferLock = new();
        private readonly Queue<OmsOrderModel> _buffer = new();

        public event TradeFeedReceivedEventHandler TradeFeedReceivedEvent;
        public event NewClosedOrderRowAddedEventHandler ClosedOrderRowAddedEvent;
        public event BlockUiEventHandler BlockUiEvent;
        public event UnblockUiEventHandler UnblockUiEvent;
        public event Action<WinningTradeModel> WinningTradeAdded;
        public event CloseSubsUpdatedHandler CloseSubsUpdated;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly BlockingCollection<OmsOrderModel> _messageQueue;
        private readonly Thread _transactionConsumerThread;
        private readonly HashSet<string> _loadedWorkingOrderIds = new();
        private readonly HashSet<string> _loadedClosedOrderIds = new();
        private readonly HashSet<string> _loadedFilledOrderIds = new();
        private readonly HashSet<string> _loadedUniqueFillSpreadIds = new();
        private readonly HashSet<string> _loadedUniqueOrderSpreadIds = new();
        private readonly Dictionary<string, OmsOrderModel> _closedOrderIndex = new();
        private readonly Dictionary<string, OmsOrderModel> _filledOrderIndex = new();

        private readonly ConcurrentDictionary<string, OmsOrderModel> _orderIdToOrderModelMap = new();
        private readonly ConcurrentDictionary<string, SynchronizedList<OmsOrderModel>> _spreadIdToOrders = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _spreadIdToLastUpdateMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, WinningTradeModel> _spreadIdToWinningTradeModelMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<int, IOrderArchiveReceiver> _requestIdToRequesterMap = new();

        private readonly ConcurrentDictionary<string, EdgeScanFeedStatsSummary> _instanceIdToInstanceModelMap = new();
        private readonly ConcurrentDictionary<string, List<ChartValueModel>> _instanceIdToInstanceChartValueContainerMap = new();

        private readonly Timer _checkTimer;
        private readonly TimeSpan _timerInterval;
        private readonly IAbstractFactory<OmsOrderModel> _orderModelFactory;
        private readonly IAbstractFactory<WinningTradeModel> _winningTradeModelFactory;
        private readonly IHerculesClientConfig _herculesClientConfig;
        private readonly ConcurrentDictionary<string, HashSet<OmsOrderModel>> _traderWorkingOrders = new();
        private readonly NotificationManager _notificationManager;
        private readonly HashSet<string> _instances = new() { SPX_BUCKET_ID };
        private readonly object _tradeFeedCopyLock = new();
        private readonly ConcurrentQueue<TradeFeedModel> _tradeFeedTempQueue = new();

        private bool _blocked;
        private System.Timers.Timer _tradeFeedUpdateTimer;
        private List<TradeFeedModel> _tradeFeedUpdates = new();
        private List<TradeFeedModel> _tradeFeedUpdatesSwap = new();
        private DispatcherTimer _dispatcherTimer;
        private OmsCore _omsCore;
        public OmsCore OmsCore => _omsCore ??= ServiceLocator.GetService<OmsCore>();

        private List<string> _domInstancesCache;
        private int _domInstancesVersion;
        private int _domInstancesCachedVersion = -1;
        public List<string> DomInstances
        {
            get
            {
                if (_domInstancesCache == null || _domInstancesCachedVersion != _domInstancesVersion)
                {
                    _domInstancesCache = _instances.OrderBy(x => x).ToList();
                    _domInstancesCachedVersion = _domInstancesVersion;
                }
                return _domInstancesCache;
            }
        }

        public bool IsSubscribedToAllOrOwnAndAll => _herculesClientConfig.TransactionSubscriptionMode is TransactionSubscriptionMode.All or TransactionSubscriptionMode.OwnAndFills;

        public Dispatcher Dispatcher { get; private set; }
        public FastObservableCollection<OmsOrderModel> ClosedOrdersCollection { get; } = new FastObservableCollection<OmsOrderModel>();
        public FastObservableCollection<OmsOrderModel> WorkingOrdersCollection { get; } = new FastObservableCollection<OmsOrderModel>();
        public FastObservableCollection<OmsOrderModel> UniqueOrdersCollection { get; } = new FastObservableCollection<OmsOrderModel>();
        public FastObservableCollection<OmsOrderModel> FilledOrdersCollection { get; } = new FastObservableCollection<OmsOrderModel>();
        public FastObservableCollection<OmsOrderModel> UniqueFillsCollection { get; } = new FastObservableCollection<OmsOrderModel>();
        public FastObservableCollection<TradeFeedModel> TradeFeedModels { get; } = new FastObservableCollection<TradeFeedModel>();
        public FastObservableCollection<ChartSeriesModel> EdgeScanFeedStatChartValues { get; } = new FastObservableCollection<ChartSeriesModel>();
        public FastObservableCollection<EdgeScanFeedStatsSummary> EdgeScanFeedStatsSummary { get; } = new FastObservableCollection<EdgeScanFeedStatsSummary>();
        public FastObservableCollection<WinningTradeModel> WinningTrades { get; } = new FastObservableCollection<WinningTradeModel>();

        public List<EdgeScannerType> EdgeFeedScanners { get; } = ((EdgeScannerType[])Enum.GetValues(typeof(EdgeScannerType))).ToList();
        public List<object> SelectedEdgeFeedScanners { get; set; } = ((EdgeScannerType[])Enum.GetValues(typeof(EdgeScannerType))).Select(x => (object)x).ToList();

        public event Action<DateTime, List<ContraPartyReportModel>> ContrapartyReportsAdded;

        public TransactionConsumerModel(IAbstractFactory<OmsOrderModel> orderModelFactory,
                                        IAbstractFactory<WinningTradeModel> winningTradeModelFactory,
                                        IHerculesClientConfig herculesClientConfig,
                                        NotificationManager notificationManager)
        {
            _notificationManager = notificationManager;
            _orderModelFactory = orderModelFactory;
            _winningTradeModelFactory = winningTradeModelFactory;
            _herculesClientConfig = herculesClientConfig;
            _timerInterval = TimeSpan.FromSeconds(CHECK_FOR_DUPLICATE_RESTING_ORDERS_INTERVAL);
            _checkTimer = new Timer(CheckOrdersAndTrades, null, Timeout.Infinite, Timeout.Infinite);
            _messageQueue = new BlockingCollection<OmsOrderModel>();
            _transactionConsumerThread = new Thread(UpdateProcessor);
        }

        private void UpdateProcessor()
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
            while (true)
            {
                try
                {
                    OmsOrderModel orderModel = _messageQueue.Take();
                    if (orderModel.RemoveFromTodaysOrderbook)
                        RemoveOrderFromCollections(orderModel);
                    else
                        AddOrderToCollections(orderModel);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(UpdateProcessor));
                }
            }
        }

        private void CheckOrdersAndTrades(object state)
        {
            try
            {
                CheckForRestingOrders();
                UpdateWinningTrades();
            }
            finally
            {
                _checkTimer.Change(_timerInterval, Timeout.InfiniteTimeSpan);
            }
        }

        private void UpdateWinningTrades()
        {
            try
            {
                for (var index = WinningTrades.Count - 1; index >= 0; index--)
                {
                    var winningTrade = WinningTrades[index];
                    winningTrade.Update();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateWinningTrades));
            }
        }

        private void CheckForRestingOrders()
        {
            try
            {
                if (OmsCore.Config.PlayDuplicateRestingOrdersNotificationV2 ||
                    OmsCore.Config.ShowDuplicateRestingOrdersNotificationV2)
                {
                    foreach (HashSet<OmsOrderModel> traderRestingOrders in _traderWorkingOrders.Values)
                    {
                        if (traderRestingOrders.Count > OmsCore.Config.DuplicateRestingOrdersNotificationCount)
                        {
                            foreach (OmsOrderModel transaction in traderRestingOrders)
                            {
                                if (DateTime.Now - transaction.LastUpdateTime >= TimeSpan.FromMilliseconds(OmsCore.Config.DuplicateRestingOrdersNotificationPeriod))
                                {
                                    if (OmsCore.Config.PlayDuplicateRestingOrdersNotificationV2)
                                    {
                                        SoundManager.Play(OmsCore.Config.DuplicateRestingOrdersNotificationSound);
                                    }

                                    if (OmsCore.Config.ShowDuplicateRestingOrdersNotificationV2)
                                    {
                                        _notificationManager.AddAlert("Duplicate Resting Orders Alert!\n" + transaction.SpreadId, DateTime.Now, "Portfolio", transaction.SpreadId);
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForRestingOrders));
            }
        }

        public void CancelRestingOrders(string username, string spreadId, Side? side)
        {
            try
            {
                if (!_spreadIdToOrders.TryGetValue(spreadId, out var orders))
                {
                    return;
                }

                OmsOrderModel[] snapshot = orders.ToArray();
                for (var index = snapshot.Length - 1; index >= 0; index--)
                {
                    var order = snapshot[index];
                    if (!order.OrderStatus.IsClosed() &&
                        order.Tag == username &&
                        order.Side == side)
                    {
                        OmsCore.OrderClient.CancelOrder(new CancelRequest
                        {
                            OrderId = order.OrderID,
                            Venue = order.Venue,
                            LocalId = order.LocalID,
                            PermId = order.PermID,
                            Account = order.AccountAcronym,
                            UserId = order.UserId,
                            RiskCheckId = order.RiskCheckId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelRestingOrders));
            }
        }

        public void OrderAdded(IOrder order)
        {
            OmsOrderModel orderModel = (OmsOrderModel)order;
            orderModel.RemoveFromTodaysOrderbook = false;
            if (orderModel != null)
            {
                AddOrder(order, orderModel);
                _notificationManager.AddIndicatorUpdate(orderModel);
            }
        }

        public void OrderRemoved(string permId)
        {
            if (!_orderIdToOrderModelMap.TryGetValue(permId, out var orderModel))
            {
                return;
            }

            orderModel.RemoveFromTodaysOrderbook = true;
            _messageQueue.Add(orderModel);
        }

        private void AddOrder(IOrder order, OmsOrderModel orderModel)
        {
            if (order.PermID != null)
            {
                _orderIdToOrderModelMap[order.PermID] = orderModel;
            }
            else
            {
                _log.Error("Order with no perm id found");
            }
            SetSubTypeSummary(orderModel);
            _messageQueue.Add(orderModel);
            NotifyOrderUpdateSubscribers(orderModel);
            _notificationManager.AddTransaction(orderModel);
            AddToLookup(orderModel);
        }

        private static void SetSubTypeSummary(OmsOrderModel orderModel)
        {
            try
            {
                switch (orderModel.SubTypeId)
                {
                    case SubType.FishOpen:
                        orderModel.SubTypeSummary = $"Fish {orderModel.SubTypeSequence} Open";
                        break;
                    case SubType.FishClose:
                        orderModel.SubTypeSummary = $"Fish {orderModel.SubTypeSequence} Close";
                        break;
                    case SubType.LoopOpen:
                        orderModel.SubTypeSummary = $"Loop {orderModel.SubTypeSequence} Open";
                        break;
                    case SubType.LoopClose:
                        orderModel.SubTypeSummary = $"Loop {orderModel.SubTypeSequence} Close";
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetSubTypeSummary));
            }
        }

        public void MultipleOrderAdded(int requestId, ref List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            if (requestId == 0)
            {
                AddMultipleOrders(orders, totalQueued, lastMessageIndex);
            }
            else
            {
                if (_requestIdToRequesterMap.TryGetValue(requestId, out IOrderArchiveReceiver requester))
                {
                    requester.AddMultipleOrders(orders, totalQueued, lastMessageIndex);
                }
            }
        }

        private void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            if (totalQueued == orders.Count)
            {
                if (totalQueued < 10)
                {
                    foreach (IOrder order in orders)
                    {
                        OmsOrderModel orderModel = (OmsOrderModel)order;
                        if (orderModel != null)
                        {
                            AddOrder(order, orderModel);
                        }
                    }
                }
                else
                {
                    List<OmsOrderModel> buffered = new();

                    foreach (IOrder order in orders)
                    {
                        OmsOrderModel orderModel = (OmsOrderModel)order;
                        if (orderModel != null)
                        {
                            if (order.PermID != null)
                            {
                                _orderIdToOrderModelMap[order.PermID] = orderModel;
                            }
                            else
                            {
                                _log.Error("Order with no perm id found");
                            }
                            buffered.Add(orderModel);
                            NotifyOrderUpdateSubscribers(orderModel);
                        }
                    }

                    AddMultipleOrdersToCollection(buffered);
                }
            }
            else
            {
                if (lastMessageIndex - orders.Count == 0)
                {
                    lock (_bufferLock)
                    {
                        _buffer.Clear();
                    }
                }

                lock (_bufferLock)
                {
                    foreach (IOrder order in orders)
                    {
                        OmsOrderModel orderModel = (OmsOrderModel)order;
                        if (orderModel != null)
                        {
                            if (order.PermID != null)
                            {
                                _orderIdToOrderModelMap[order.PermID] = orderModel;
                            }
                            else
                            {
                                _log.Error("Order with no perm id found");
                            }
                            _buffer.Enqueue(orderModel);
                            NotifyOrderUpdateSubscribers(orderModel);
                        }
                    }
                }

                if (totalQueued == _buffer.Count)
                {
                    List<OmsOrderModel> buffered = null;

                    lock (_bufferLock)
                    {
                        buffered = _buffer.ToList();
                        _buffer.Clear();
                    }

                    if (buffered != null)
                    {
                        AddMultipleOrdersToCollection(buffered);
                    }

                    if (totalQueued > 100_000)
                    {
                        GC.Collect(2, GCCollectionMode.Optimized, false);
                    }
                }
            }
        }

        public void OrderUpdated(IOrder model)
        {
            OmsOrderModel orderModel = (OmsOrderModel)model;
            if (orderModel != null)
            {
                _messageQueue.Add(orderModel);
                NotifyOrderUpdateSubscribers(orderModel);
                _notificationManager.AddTransaction(orderModel);
                _notificationManager.AddIndicatorUpdate(orderModel);
            }
        }

        private void NotifyOrderUpdateSubscribers(OmsOrderModel orderModel)
        {
            try
            {
                if (orderModel != null)
                {
                    if (orderModel.CumulativeQuantity > 0 && orderModel.Tag != null && !orderModel.Tag.Equals(OmsCore.User.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        if (orderModel.SpreadId != null && (!_spreadIdToLastUpdateMap.TryGetValue(orderModel.SpreadId, out DateTime updateTime) || updateTime < orderModel.LastUpdateTime))
                        {
                            _spreadIdToLastUpdateMap[orderModel.SpreadId] = orderModel.LastUpdateTime;
                        }
                    }
                    if (orderModel.OrderStatus.IsClosed() && orderModel.FilledQty > 0)
                    {
                        if (orderModel.SubType != null && orderModel.SubType == OrderSubType.Dominator)
                        {
                            if (orderModel.Comment != null)
                            {
                                string instance = orderModel.Comment.Split(':')[0].Trim().ToUpper();
                                if (!double.IsNaN(orderModel.LastEdge))
                                {
                                    Update(instance, SubscriptionFieldType.OrderUpdate, orderModel);
                                }
                                if (_instances.Add(instance))
                                    Interlocked.Increment(ref _domInstancesVersion);
                            }
                        }
                        if (orderModel.UnderlyingSymbol == "$SPX" &&
                            orderModel.SubType == OrderSubType.BasketAutoPerm)
                        {
                            if (!double.IsNaN(orderModel.LastEdge))
                            {
                                Update(SPX_BUCKET_ID, SubscriptionFieldType.OrderUpdate, orderModel);
                            }
                        }
                    }

                    if (orderModel.Tag == OmsCore.User.Username && orderModel.SubType == OrderSubType.Ticket)
                    {
                        Update(orderModel.SpreadId, SubscriptionFieldType.OrderUpdate, orderModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(NotifyOrderUpdateSubscribers));
            }
        }

        public bool TryGetLastTradeTime(string spreadId, out DateTime lastUpdateTime)
        {
            try
            {
                return _spreadIdToLastUpdateMap.TryGetValue(spreadId, out lastUpdateTime);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetLastTradeTime));
                lastUpdateTime = default;
                return false;
            }
        }

        public void OrderIndicatorUpdated(IOrder model)
        {
            OmsOrderModel orderModel = (OmsOrderModel)model;
            if (orderModel != null)
            {
                _notificationManager.AddIndicatorUpdate(orderModel);
                orderModel.NotifyOfIndicatorUpdate();
                NotifyOrderUpdateSubscribers(orderModel);
                if (!double.IsNaN(orderModel.CloseSubs))
                {
                    CloseSubsUpdated?.Invoke(orderModel);
                }
            }
        }

        public void OrderTagUpdated(IOrder model)
        {
            OmsOrderModel orderModel = (OmsOrderModel)model;
            orderModel.NotifyOfTagUpdate();
        }

        public bool GetExistingOrder(string orderId, out IOrder order)
        {
            if (_orderIdToOrderModelMap.TryGetValue(orderId, out OmsOrderModel model))
            {
                order = model;
                return true;
            }
            else
            {
                order = null;
                return false;
            }
        }

        public async void HandleOrderDetailsUpdate(IOrder order, string json)
        {
            try
            {
                if (order is OmsOrderModel model)
                {
                    model.LoadingDetails = false;
                    List<KeyValuePair<string, string>> list = await Task.Run(() => JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(json));
                    if (list != null)
                    {
                        Dispatcher?.BeginInvoke(() =>
                        {
                            model.Details ??= [];
                            foreach (KeyValuePair<string, string> kvp in list)
                            {
                                model.Details.Add(kvp);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOrderDetailsUpdate));
            }
        }

        public void EdgeScanFeedStatsUpdate(IEdgeScanFeedStatisticsSummary model)
        {
            ((EdgeScanFeedStatsModel)model).Updated = true;
        }

        public IEdgeScanFeedStatisticsSummary GetEdgeScanFeedStatisticsModel(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = "<UNKNOWN>";
            }

            if (_instanceIdToInstanceModelMap.TryGetValue(instanceId, out var model))
            {
                return model.Model;
            }

            model = new EdgeScanFeedStatsSummary();
            _instanceIdToInstanceModelMap[instanceId] = model;
            Dispatcher.BeginInvoke(() => EdgeScanFeedStatsSummary.AddItem(model));
            return model.Model;
        }

        public void HandleUpdate(string id, SubscriptionFieldType type, List<ChartValueModel> updatesList)
        {
            switch (type)
            {
                case SubscriptionFieldType.EdgeScanFeedSubmissionStats:
                    Dispatcher?.BeginInvoke(() => EdgeScanFeedStatChartValues.Add(new ChartSeriesModel(id, updatesList.Where(x => x.Timestamp.Date == DateTime.Today).ToList())));
                    break;
            }
        }

        public List<IOrder> GetAllOrders()
        {
            return _orderIdToOrderModelMap.Values.Cast<IOrder>().ToList();
        }

        public IOrder GetOrder(bool isComplex, string orderId = null)
        {
            return GetOrCreateOrder(isComplex, orderId, out _);
        }

        public IOrder GetOrCreateOrder(bool isComplex, string orderId, [UnscopedRef] out bool isNew)
        {
            IOrder orderModel;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                orderModel = _orderModelFactory.Create();
                ((OmsOrderModel)orderModel).IsComplexOrder = isComplex;
                isNew = true;
            }
            else
            {
                if (_orderIdToOrderModelMap.TryGetValue(orderId, out OmsOrderModel existing))
                {
                    orderModel = existing;
                    isNew = false;
                }
                else
                {
                    orderModel = _orderModelFactory.Create();
                    ((OmsOrderModel)orderModel).IsComplexOrder = isComplex;
                    ((OmsOrderModel)orderModel).PermID = orderId;
                    isNew = true;
                }
            }
            return orderModel;
        }

        public IOrderSlim GetOrderSlim(bool isComplex, string orderId = null)
        {
            return GetOrder(isComplex, orderId);
        }

        public void MultipleContrapartyReportsAdded(DateTime targetDate, List<ContraPartyReportModel> reports)
        {
            // Add todays contraparty reports to the orderbook
            if (targetDate.Date == DateTime.Now.Date)
            {
                foreach (var report in reports)
                {
                    if (_orderIdToOrderModelMap.TryGetValue(report.ClOrdID, out var order))
                    {
                        order.AddContraPartyReport(report);
                    }
                }
            }
            // If it's not today's orderbook, then it was requested for the empty orderbook
            else
            {
                ContrapartyReportsAdded?.Invoke(targetDate, reports);
            }
        }

        private void AddToLookup(OmsOrderModel orderModel)
        {
            try
            {
                if (!OmsCore.Config.TrackOrdersBySpreadV2)
                {
                    return;
                }

                if (orderModel != null)
                {
                    var orderModelSpreadId = orderModel.SpreadId?.Trim();
                    if (!string.IsNullOrWhiteSpace(orderModelSpreadId))
                    {
                        if (OmsCore.Config.CancelRestingOrdersOnCombinedTickets)
                        {
                            var orders = _spreadIdToOrders.GetOrAdd(orderModelSpreadId, _ => new SynchronizedList<OmsOrderModel>());
                            orders.Add(orderModel);
                        }

#if RELEASE
                        if (orderModel.LastEdge > 0)
#endif
                        {
                            if (!_spreadIdToWinningTradeModelMap.TryGetValue(orderModelSpreadId, out var winningTradeModel))
                            {
                                winningTradeModel = _winningTradeModelFactory.Create();
                                winningTradeModel.SpreadId = orderModelSpreadId;
                                winningTradeModel.Symbol = orderModel.Symbol;
                                winningTradeModel.Underlying = orderModel.UnderlyingSymbol;
                                winningTradeModel.HardSideKey = orderModel.GetHardSideKey();
                                winningTradeModel.Subscribe();
                                _spreadIdToWinningTradeModelMap[orderModelSpreadId] = winningTradeModel;
                                Dispatcher?.BeginInvoke(() => WinningTrades.AddItem(winningTradeModel));
                                WinningTradeAdded?.Invoke(winningTradeModel);
                            }

                            double closeUnderMid = (orderModel.CloseUnderBid + orderModel.CloseUnderAsk) / 2;
                            winningTradeModel.AddTrader(orderModel.Tag);

                            winningTradeModel.LastTradeSide = orderModel.Side;
                            winningTradeModel.LastEdgeTradeTime = orderModel.LastUpdateTime;
                            winningTradeModel.LastTradeDelta = orderModel.CloseDelta;
                            winningTradeModel.LastTradeUnder = closeUnderMid;

                            if (double.IsNaN(winningTradeModel.HighestEdge) ||
                                winningTradeModel.HighestEdge < orderModel.LastEdge)
                            {
                                winningTradeModel.HighestEdge = orderModel.LastEdge;
                            }

                            switch (orderModel.Side)
                            {
                                case ZeroPlus.Models.Data.Enums.Side.Buy:
                                    winningTradeModel.BuyUnderPrice = closeUnderMid;
                                    winningTradeModel.BuyPrice = orderModel.AveragePrice;
                                    winningTradeModel.BuyEdgeToTheo = orderModel.TagEdgeToTheo;
                                    break;
                                case ZeroPlus.Models.Data.Enums.Side.Sell:
                                    winningTradeModel.SellUnderPrice = closeUnderMid;
                                    winningTradeModel.SellPrice = orderModel.AveragePrice;
                                    winningTradeModel.SellEdgeToTheo = orderModel.TagEdgeToTheo;
                                    break;
                            }
                        }
#if RELEASE
                        else
#endif
                        {
                            if (_spreadIdToWinningTradeModelMap.TryGetValue(orderModelSpreadId, out var winningTradeModel))
                            {
                                if (orderModel.CumulativeQuantity > 0)
                                {
                                    winningTradeModel.LastTradeTime = orderModel.LastUpdateTime;
                                }
                                else if (orderModel.Side == winningTradeModel.HardSide)
                                {
                                    winningTradeModel.LastHardSideAttemptTime = orderModel.LastUpdateTime;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddToLookup));
            }
        }

        internal void SetDispatcherAndStart(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            LoadEdgeScanFeedFilterModels();
            OmsCore.HerculesClient.ClientDisconnected += OnHerculesDisconnected;
            OmsCore.EdgeScannerClient.ConnectionStatusChangedEvent += OnEdgeScannerConnectionStatusChange;
            OmsCore.EdgeScanFeedRunnerClient.RunnerStateChanged += OnEdgeScanFeedRunnerStateChanged;
            _checkTimer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
            _transactionConsumerThread.Start();
            _tradeFeedUpdateTimer = new System.Timers.Timer
            {
                Interval = INTERVAL,
                AutoReset = false,
            };
            _tradeFeedUpdateTimer.Elapsed += TimerTickAsync;
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.ContextIdle, UpdateStats,
                Dispatcher);
            _dispatcherTimer.Start();
        }

        private void UpdateStats(object sender, EventArgs e)
        {
            for (var index = EdgeScanFeedStatsSummary.Count - 1; index >= 0; index--)
            {
                var scanner = EdgeScanFeedStatsSummary[index];
                if (scanner.Model.Updated)
                {
                    scanner.Update();
                }
            }
        }

        private void OnEdgeScannerConnectionStatusChange(bool connected)
        {
            if (connected)
            {
                foreach (var scanner in EdgeFeedScanners)
                {
                    Resubscribe((SubscriptionFieldType)scanner);
                }
            }
        }

        private void OnEdgeScanFeedRunnerStateChanged(string runnerId, EdgeScanFeedRunnerState state)
        {
            Update(runnerId, SubscriptionFieldType.EdgeScanFeedRunnerState, state);
        }

        private async void TimerTickAsync(object sender, EventArgs e)
        {
            try
            {
                _tradeFeedUpdateTimer.Stop();
                if (_tradeFeedUpdates.Count > 0)
                {
                    lock (_tradeFeedCopyLock)
                    {
                        (_tradeFeedUpdates, _tradeFeedUpdatesSwap) = (_tradeFeedUpdatesSwap, _tradeFeedUpdates);
                    }
                    TradeFeedModel feed = _tradeFeedUpdatesSwap.LastOrDefault();
                    if (Dispatcher != null)
                    {
                        await Dispatcher.BeginInvoke(() =>
                        {
                            bool blocked = false;
                            if (_tradeFeedUpdatesSwap.Count > LIMIT)
                            {
                                BlockUiEvent?.Invoke();
                                blocked = true;
                            }

                            TradeFeedModels.AddRange(_tradeFeedUpdatesSwap);
                            TradeFeedReceivedEvent?.Invoke(feed);

                            if (blocked)
                            {
                                UnblockUiEvent?.Invoke();
                            }
                        });
                    }
                    _tradeFeedUpdatesSwap.Clear();
                }
            }
            finally
            {
                _tradeFeedUpdateTimer.Start();
            }
        }

        internal void AddRequester(int requestId, IOrderArchiveReceiver receiver)
        {
            _requestIdToRequesterMap[requestId] = receiver;
        }

        private void OnHerculesDisconnected()
        {
            ClearTables();
        }

        private void AddMultipleOrdersToCollection(List<OmsOrderModel> orders)
        {
            try
            {
                var isSubscribedToAllOrOwnAndAll = IsSubscribedToAllOrOwnAndAll;
                foreach (OmsOrderModel transaction in orders)
                {
                    transaction.Subscribe();
                    if (transaction.OrderStatus is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.Rejected)
                    {
                        if (transaction.FilledQty > 0)
                        {
                            if (_loadedFilledOrderIds.Add(transaction.PermID))
                            {
                                _loadedUniqueFillSpreadIds.Add(transaction.SpreadId);
                            }
                        }
                        if (isSubscribedToAllOrOwnAndAll && _loadedClosedOrderIds.Add(transaction.PermID))
                        {
                            _loadedUniqueOrderSpreadIds.Add(transaction.SpreadId);
                        }
                        _loadedWorkingOrderIds.Remove(transaction.PermID);
                    }
                    else if (transaction.OrderStatus is OrderStatus.New or OrderStatus.PendingCancel or OrderStatus.PendingNew or OrderStatus.PendingReplace or OrderStatus.Replaced)
                    {
                        _loadedWorkingOrderIds.Add(transaction.PermID);
                    }
                    else if (transaction.OrderStatus is OrderStatus.PartiallyFilled)
                    {
                        _loadedWorkingOrderIds.Add(transaction.PermID);
                        if (isSubscribedToAllOrOwnAndAll && _loadedClosedOrderIds.Add(transaction.PermID))
                        {
                            _loadedUniqueOrderSpreadIds.Add(transaction.SpreadId);
                        }
                        if (_loadedFilledOrderIds.Add(transaction.PermID))
                        {
                            _loadedUniqueFillSpreadIds.Add(transaction.SpreadId);
                        }
                    }
                    AddToLookup(transaction);
                }

                var workingOrders = new List<OmsOrderModel>();
                var closedOrders = isSubscribedToAllOrOwnAndAll ? new List<OmsOrderModel>() : null;
                var filledOrders = new List<OmsOrderModel>();
                var uniqueFillSpreadIds = new HashSet<string>();
                var uniqueFills = new List<OmsOrderModel>();
                var uniqueOrders = isSubscribedToAllOrOwnAndAll ? new List<OmsOrderModel>() : null;
                var uniqueOrderSpreadIds = isSubscribedToAllOrOwnAndAll ? new HashSet<string>() : null;

                foreach (OmsOrderModel transaction in orders)
                {
                    bool isClosed = transaction.OrderStatus is OrderStatus.PartiallyFilled || transaction.Done;

                    if (!transaction.Done)
                        workingOrders.Add(transaction);

                    if (isClosed)
                    {
                        closedOrders?.Add(transaction);
                        if (isSubscribedToAllOrOwnAndAll && uniqueOrderSpreadIds.Add(transaction.SpreadId))
                            uniqueOrders.Add(transaction);

                        if (transaction.FilledQty > 0)
                        {
                            filledOrders.Add(transaction);
                            if (uniqueFillSpreadIds.Add(transaction.SpreadId))
                                uniqueFills.Add(transaction);
                        }
                    }
                }

                if (orders.Count > LIMIT)
                {
                    BlockUiEvent?.Invoke();
                    _blocked = true;
                }

                if (_blocked)
                {
                    Dispatcher.Invoke(AddToCollections);
                }
                else
                {
                    Dispatcher.BeginInvoke(AddToCollections);
                }

                void AddToCollections()
                {
                    WorkingOrdersCollection.AddRange(workingOrders);
                    FilledOrdersCollection.AddRange(filledOrders);
                    UniqueFillsCollection.AddRange(uniqueFills);

                    if (isSubscribedToAllOrOwnAndAll)
                    {
                        ClosedOrdersCollection.AddRange(closedOrders);
                        UniqueOrdersCollection.AddRange(uniqueOrders);
                    }
                }
            }
            finally
            {
                if (_blocked)
                {
                    UnblockUiEvent?.Invoke();
                    _blocked = false;
                }
            }
        }

        private void AddOrderToCollections(OmsOrderModel transaction)
        {
            if (!transaction.Subscribed)
            {
                transaction.Subscribe();
            }
            else
            {
                transaction.NotifyOfUpdate();
            }

            bool addToFilled = false;
            bool addToUniqueFills = false;
            bool addToClosed = false;
            bool addToUnique = false;
            bool removeWorking = false;

            if (!transaction.Done && transaction.OrderStatus is OrderStatus.PartiallyFilled)
            {
                if (_loadedFilledOrderIds.Add(transaction.PermID))
                {
                    addToFilled = true;
                    _filledOrderIndex[transaction.PermID] = transaction;

                    if (_loadedUniqueFillSpreadIds.Add(transaction.SpreadId))
                    {
                        addToUniqueFills = true;
                    }
                }

                if (IsSubscribedToAllOrOwnAndAll && _loadedClosedOrderIds.Add(transaction.PermID))
                {
                    addToClosed = true;
                    _closedOrderIndex[transaction.PermID] = transaction;

                    if (_loadedUniqueOrderSpreadIds.Add(transaction.SpreadId))
                    {
                        addToUnique = true;
                    }
                }

                if (addToFilled || addToClosed)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (addToFilled)
                        {
                            FilledOrdersCollection.AddItem(transaction);
                        }
                        if (addToUniqueFills)
                        {
                            UniqueFillsCollection.AddItem(transaction);
                        }
                        if (addToClosed)
                        {
                            ClosedOrdersCollection.AddItem(transaction);
                        }
                        if (addToUnique)
                        {
                            UniqueOrdersCollection.AddItem(transaction);
                        }
                        ClosedOrderRowAddedEvent?.Invoke(transaction);
                    });
                }
            }
            else if (transaction.Done)
            {
                if (transaction.FilledQty > 0)
                {
                    if (_loadedFilledOrderIds.Add(transaction.PermID))
                    {
                        addToFilled = true;
                        _filledOrderIndex[transaction.PermID] = transaction;

                        if (_loadedUniqueFillSpreadIds.Add(transaction.SpreadId))
                        {
                            addToUniqueFills = true;
                        }
                    }
                }
                if (IsSubscribedToAllOrOwnAndAll && _loadedClosedOrderIds.Add(transaction.PermID))
                {
                    addToClosed = true;
                    _closedOrderIndex[transaction.PermID] = transaction;

                    if (_loadedUniqueOrderSpreadIds.Add(transaction.SpreadId))
                    {
                        addToUnique = true;
                    }
                }
                if (_loadedWorkingOrderIds.Contains(transaction.PermID))
                {
                    removeWorking = true;

                    _loadedWorkingOrderIds.Remove(transaction.PermID);
                    if (OmsCore.Config.PlayDuplicateRestingOrdersNotificationV2 ||
                        OmsCore.Config.ShowDuplicateRestingOrdersNotificationV2)
                    {
                        if (string.Equals(OmsCore.User.Username, transaction.Trader, StringComparison.OrdinalIgnoreCase))
                        {
                            if (_traderWorkingOrders.TryGetValue(transaction.SpreadId, out HashSet<OmsOrderModel> transactions))
                            {
                                transactions.Remove(transaction);
                                if (transactions.Count == 0)
                                {
                                    _traderWorkingOrders.TryRemove(transaction.SpreadId, out _);
                                }
                            }
                        }
                    }
                }

                if (addToFilled || addToClosed || removeWorking)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (addToFilled)
                        {
                            FilledOrdersCollection.AddItem(transaction);
                        }
                        if (addToUniqueFills)
                        {
                            UniqueFillsCollection.AddItem(transaction);
                        }
                        if (addToClosed)
                        {
                            ClosedOrdersCollection.AddItem(transaction);
                        }
                        if (addToUnique)
                        {
                            UniqueOrdersCollection.AddItem(transaction);
                        }
                        if (removeWorking)
                        {
                            WorkingOrdersCollection.Remove(transaction);
                        }
                        if (addToFilled || addToClosed)
                        {
                            ClosedOrderRowAddedEvent?.Invoke(transaction);
                        }
                    });
                }

                if (!double.IsNaN(transaction.CloseSubs))
                {
                    CloseSubsUpdated?.Invoke(transaction);
                }
            }
            else if (transaction.OrderStatus is OrderStatus.New or OrderStatus.PendingCancel or OrderStatus.PendingNew or OrderStatus.PendingReplace or OrderStatus.Replaced)
            {
                if (_loadedWorkingOrderIds.Add(transaction.PermID))
                {
                    Dispatcher.BeginInvoke(() => WorkingOrdersCollection.AddItem(transaction));

                    if (OmsCore.Config.PlayDuplicateRestingOrdersNotificationV2 ||
                        OmsCore.Config.ShowDuplicateRestingOrdersNotificationV2)
                    {
                        if (string.Equals(OmsCore.User.Username, transaction.Trader, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_traderWorkingOrders.TryGetValue(transaction.SpreadId, out HashSet<OmsOrderModel> transactions))
                            {
                                transactions = new HashSet<OmsOrderModel>();
                                _traderWorkingOrders[transaction.SpreadId] = transactions;
                            }
                            transactions.Add(transaction);
                        }
                    }
                }
            }

        }

        private void RemoveOrderFromCollections(OmsOrderModel transaction)
        {
            bool removeFilled = false;
            bool removeClosed = _loadedClosedOrderIds.Remove(transaction.PermID);

            if (transaction.FilledQty > 0)
            {
                removeFilled = _loadedFilledOrderIds.Remove(transaction.PermID);
            }

            OmsOrderModel filledModel = removeFilled && _filledOrderIndex.Remove(transaction.PermID, out var f) ? f : null;
            OmsOrderModel closedModel = removeClosed && _closedOrderIndex.Remove(transaction.PermID, out var c) ? c : null;

            if (filledModel != null || closedModel != null)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (filledModel != null)
                    {
                        FilledOrdersCollection.Remove(filledModel);
                    }
                    if (closedModel != null)
                    {
                        ClosedOrdersCollection.Remove(closedModel);
                    }
                });
            }
        }

        private void ClearTables()
        {
            _loadedWorkingOrderIds.Clear();
            _loadedClosedOrderIds.Clear();
            _loadedUniqueOrderSpreadIds.Clear();
            _loadedFilledOrderIds.Clear();
            _loadedUniqueFillSpreadIds.Clear();
            _closedOrderIndex.Clear();
            _filledOrderIndex.Clear();
            _traderWorkingOrders.Clear();

            Dispatcher.BeginInvoke(() =>
            {
                ClosedOrdersCollection.Clear();
                WorkingOrdersCollection.Clear();
                UniqueOrdersCollection.Clear();
                FilledOrdersCollection.Clear();
                UniqueFillsCollection.Clear();
            });

            GC.Collect(2, GCCollectionMode.Optimized, false);
        }

        public void EdgeScanUpdate(IEdgeScanFeedModel model)
        {
            EdgeScanFeedModel feed = (EdgeScanFeedModel)model;
            DateTime time = feed.BuyTime > feed.SellTime ? feed.BuyTime : feed.SellTime;

            if (feed.EdgeScannerType == EdgeScannerType.IvChangeDeltaAdjLoopFinder)
            {
                _log.Info("IV change finder received. Id: {} Buy: {}, Sell: {}", feed.SpreadId, feed.BuyTime, feed.SellTime);
            }

            feed.Latency = DateTime.Now.ToEastern() - time;
            feed.BuyBidPercent = Math.Round((feed.BuyTradeBid - feed.BuyPrice) / (feed.BuyTradeBid - feed.BuyTradeAsk), 2);
            feed.SellBidPercent = Math.Round((feed.SellTradeBid - feed.SellPrice) / (feed.SellTradeBid - feed.SellTradeAsk), 2);
            feed.Width = Math.Abs(Math.Abs(feed.BuyPrice) - Math.Abs(feed.SellPrice));
            if (!double.IsNaN(model.Ttl))
            {
                feed.FinderLatency = TimeSpan.FromMilliseconds(model.Ttl);
            }
            EdgeScanFeedModel sell = null;
            if (feed.EdgeScannerType == EdgeScannerType.SideScan || feed.EdgeScannerType == EdgeScannerType.EqSideScan)
            {
                if (feed.AdjSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    feed.Side = ZeroPlus.Models.Data.Enums.Side.Sell;
                    feed.AbsDelta = Math.Abs(feed.SellTradeDelta);
                }
                else
                {
                    feed.Side = ZeroPlus.Models.Data.Enums.Side.Buy;
                    feed.AbsDelta = Math.Abs(feed.BuyTradeDelta);
                }
            }
            else
            {
                feed.AbsDelta = Math.Abs(feed.BuyTradeDelta);
                feed.Side = ZeroPlus.Models.Data.Enums.Side.Buy;
                if (feed.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence)
                {
                    sell = new EdgeScanFeedModel(feed, invert: true);
                }
            }

            if (feed.Latency.TotalMilliseconds <= 10000 || OmsCore.Config.IsDevMode)
            {
                SubscriptionFieldType type = (SubscriptionFieldType)feed.EdgeScannerType;
                var pair = Tuple.Create(feed, sell);

                if (!string.IsNullOrWhiteSpace(feed.SessionId))
                {
                    Update(feed.SessionId, SubscriptionFieldType.EdgeScanFeed, pair);
                }
                else
                {
                    Update(UNIVERSAL, type, pair);
                    Update(feed.UnderSymbol, type, pair);
                }
            }
        }

        public void TradeFeedUpdate(int id, List<ITradeFeedModel> models, bool isLast)
        {
            if (TradeFeedReceivedEvent != null)
            {
                if (isLast && _tradeFeedTempQueue.Count == 0)
                {
                    List<TradeFeedModel> list = models.Select(x => (TradeFeedModel)x).ToList();
                    lock (_tradeFeedCopyLock)
                    {
                        _tradeFeedUpdates.AddRange(list);
                    }
                }
                else
                {
                    foreach (ITradeFeedModel model in models)
                    {
                        _tradeFeedTempQueue.Enqueue((TradeFeedModel)model);
                    }
                    if (isLast)
                    {
                        List<TradeFeedModel> feeds = _tradeFeedTempQueue.ToList();
                        lock (_tradeFeedCopyLock)
                        {
                            _tradeFeedUpdates.AddRange(feeds);
                        }
                        _tradeFeedTempQueue.Clear();
                    }
                }
            }
        }

        public ITradeFeedModel GetTradeFeedModel()
        {
            return new TradeFeedModel();
        }

        public IEdgeScanFeedModel GetEdgeScanFeedModel()
        {
            return new EdgeScanFeedModel();
        }

        public IPriceChainModel GetPriceChainModel()
        {
            return new EdgeScanFeedModel();
        }

        internal async void LoadEdgeScanFeedFilterModels()
        {
            try
            {
                List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedFilter);
                string json = OmsCore.Config.GetEdgeScanFeedFilterConfigs();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    List<EdgeScanFeedTradeFilterModel> models = JsonConvert.DeserializeObject<List<EdgeScanFeedTradeFilterModel>>(json);
                    foreach (EdgeScanFeedTradeFilterModel model in models)
                    {
                        model.NormalizeAfterLoad();
                        model.Creator = OmsCore.User.Username;

                        ConfigSave sameConfig = config?.FirstOrDefault(x => x.Title == model.Title && x.OwnerId == OmsCore.User.ID);

                        if (sameConfig == null)
                        {
                            ConfigSave configSave = new()
                            {
                                Id = model.Id,
                                OwnerId = OmsCore.User.ID,
                                Username = OmsCore.User.Username,
                                Module = (int)Module.EdgeScanFeedFilter,
                                ConfigJson = model.GetAsJson(),
                                Group = "",
                                SaveTime = DateTime.Now,
                                Title = model.Title,
                            };

                            OmsCore.GatewayClient.SaveConfig(configSave);
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadEdgeScanFeedFilterModels));
            }
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            string id = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;
            if (type.IsEdgeScanFeedSubscription())
            {
                OmsCore.EdgeScannerClient.SubscribeToEdgeScanFeed(id, type);
            }
            else if (type == SubscriptionFieldType.TradeFeed)
            {
                OmsCore.EdgeScannerClient.SubscribeToTradeFeed(id == "ALL");
                _tradeFeedUpdateTimer?.Start();
            }
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            string id = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;

            if (type.IsEdgeScanFeedSubscription())
            {
                OmsCore.EdgeScannerClient.UnsubscribeEdgeScanFeed(id, type);
            }
            else if (type == SubscriptionFieldType.TradeFeed)
            {
                OmsCore.EdgeScannerClient.UnsubscribeToTradeFeed();
                if (_tradeFeedUpdateTimer.Enabled)
                {
                    lock (_tradeFeedCopyLock)
                    {
                        _tradeFeedUpdates.Clear();
                        _tradeFeedUpdatesSwap.Clear();
                    }
                    _tradeFeedUpdateTimer?.Stop();
                    if (TradeFeedModels.Any())
                    {
                        Dispatcher?.BeginInvoke(() => TradeFeedModels.Clear());
                    }
                }
            }
        }

        public void RegisterServerRunner(EdgeScanFeedRunnerStartRequest request, IOmsDataSubscriber subscriber)
        {
            Subscribe(request.RunnerId, SubscriptionFieldType.EdgeScanFeed, subscriber);
            Subscribe(request.RunnerId, SubscriptionFieldType.EdgeScanFeedRunnerState, subscriber);
            OmsCore.EdgeScanFeedRunnerClient.FeedRunnerClient.StartRunner(request);
        }

        public void UnregisterServerRunner(string id, IOmsDataSubscriber subscriber)
        {
            Unsubscribe(id, SubscriptionFieldType.EdgeScanFeed, subscriber);
            OmsCore.EdgeScanFeedRunnerClient.FeedRunnerClient.StopRunner(id);
        }
    }
}
