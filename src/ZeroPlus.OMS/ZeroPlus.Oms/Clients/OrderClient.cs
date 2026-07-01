using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Subscription;
using ExecutionType = ZeroPlus.Models.Data.Enums.ExecutionType;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Clients
{
    public class OrderClient
    {
        public static readonly string OPENING_ID = "<";
        public static readonly string CLOSING_ID = ">";
        public const ulong OMS_RANGE = 0x0000020000000000;
        public string TYPE = "OMS";
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        public event ConnectionStatusChangedEventHandler PositionConnectionStatusChangedEvent;
        private readonly OmsConfig _config;
        private readonly User _user;
        private readonly AutoTraderDirectClient _autoTraderDirectClient;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly CommsClient _commsClient;
        private readonly CommsClient _positionClient;
        private readonly ConcurrentDictionary<string, string> _orderIdToExchangeIdMap = new();
        private readonly ConcurrentDictionary<string, List<KeyValuePair<string, string>>> _orderIdToTagsMap = new();
        private readonly ConcurrentDictionary<string, IOmsOrderUpdateSubscriber> _orderIdToOrderUpdateSubscriberMap = new();
        private readonly object _orderSubmissionRateTrackerLock = new();
        private readonly Queue<long> _orderSubmissionRateTracker = new();
        private readonly SlimException _orderRateEx = new("Max order submission limit breached.");
        private static int _localOrderID;
        private System.Timers.Timer _timer;
        private System.Timers.Timer _healthCheckTimer;
        private DateTime _lastStatusReceiveTime;
        private DateTime _lastOrderSendTime;
        private bool _startDelayCounter;
        private int _orderAfterDelay;
        private int _delayReconnectCount;
        private readonly string _localIdPrefix;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, OmsPosition>> _accountAndSymbolToPositionMap = new();
        private readonly ConcurrentDictionary<string, DateTime> _symbolToLastOrderTimeMap = new();

        private readonly ulong _baseInstanceId;
        private int _sequenceNumber;

        private readonly ConcurrentDictionary<Tuple<string, string>, PositionSubscribers> _subscriptionKeyToSubscribersMap = new();
        private readonly ConcurrentDictionary<IOmsPositionSubscriber, ConcurrentDictionary<Tuple<string, string>, byte>> _subscriberToSubscriptionsMap = new();
        protected IPositionUpdateSubscriber PositionUpdateSubscriber;
        private bool _subscribedToAllPositions;

        public IPortfolio TraderPortfolio { get; set; }
        public AccountsLookup AccountsLookup { get; } = new();
        public RouteLookup RouteLookup { get; }
        public bool IsConnected { get; set; }
        public bool IsPositionConnected { get; set; }

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public bool BlockSubmission { get; set; }

        public OrderClient(OmsConfig config, User user, OmsCore omsCore)
        {
            _config = config;
            _user = user;
            _autoTraderDirectClient = omsCore.AutoTraderDirectClient;
            RouteLookup = new RouteLookup(omsCore.AutoTraderClient, config);
            _commsClient = new CommsClient(OmsConfig.OpsGuid, config, HandleMessage, omsCore);
            _positionClient = new CommsClient(OmsConfig.PositionGuid, config, HandleMessage, omsCore);
            _commsClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
            _positionClient.ConnectionStatusChangedEvent += OnPositionConnectionStatusChangedEvent;
            int processId = Process.GetCurrentProcess().Id;
            _localIdPrefix = $"{_user.Username}:{processId}";
            _baseInstanceId = GenerateBaseInstanceId();
            SetupBlockResetTimer();
            StartHealthCheckTimer();
        }

        #region PublicMethods
        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task RestartPositionsAsync()
        {
            await StopPositionsAsync();
            await StartPositionsAsync();
        }

        public async Task<bool> StartAsync()
        {
            _log.Info(nameof(StartAsync));
            _commsClient.UpdateClientId(OmsConfig.OpsGuid);
            return await Task.Run(() => _commsClient.Start(_config.OrderAddress, _config.OrderPort));
        }

        public async Task<bool> StartPositionsAsync()
        {
            _log.Info(nameof(StartPositionsAsync));
            _positionClient.UpdateClientId(OmsConfig.PositionGuid);
            return await Task.Run(() => _positionClient.Start(_config.PositionAddress, _config.PositionPort));
        }

        public async Task StopAsync()
        {
            _log.Info(nameof(StopAsync));
            await Task.Run(() => _commsClient.Stop());
        }

        public async Task StopPositionsAsync()
        {
            _log.Info(nameof(StopPositionsAsync));
            await Task.Run(() => _positionClient.Stop());
        }

        public async Task<List<ZPAccount>> GetAccountAndRoutesAsync(string symbol)
        {
            try
            {
                _log.Info($"{nameof(GetAccountAndRoutesAsync)} -> Symbol: {symbol}");
                List<ZPAccount> routes = await Task.Run(() => GetAccountAndRoutes(symbol));
                return routes;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetAccountAndRoutesAsync)} -> Exception getting symbols");
                return new List<ZPAccount>();
            }
        }

        public List<ZPAccount> GetAccountAndRoutes(string symbol)
        {
            try
            {
                List<ZPAccount> routes = _commsClient.GetAccountAndRoutes(symbol);
                string message = $"{nameof(GetAccountAndRoutes)} -> Symbol: {symbol}, Routes: {string.Join(", ", routes.SelectMany(x => x.Routes.Select(y => $"[{x.Acronym}] {y.RouteName}")))}";
                _log.Info(message);
                if (symbol == "MLEG")
                {
                    AccountsLookup.Add(AccountsLookup.AccountsType.MLeg, routes);
                }
                else if (symbol != null && symbol.StartsWith("."))
                {
                    AccountsLookup.Add(AccountsLookup.AccountsType.Options, routes);
                }
                else
                {
                    AccountsLookup.Add(AccountsLookup.AccountsType.Equity, routes);
                }
                return routes;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(GetAccountAndRoutes)} -> Exception getting account and routes");
                return new List<ZPAccount>();
            }
        }

        public string GetNextOrderId()
        {
            return $"{_localIdPrefix}:{Interlocked.Increment(ref _localOrderID)}";
        }

        public ulong GetNextSharedId()
        {
            return _baseInstanceId + (ulong)Interlocked.Increment(ref _sequenceNumber);
        }

        /// <summary>
        /// Register a subscriber so it is notified when an order Execution Report or Cancel Reject message is received for the given order ID.
        /// </summary>
        public void RegisterHandler(string orderId, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber)
        {
            _orderIdToOrderUpdateSubscriberMap[orderId] = omsOrderUpdateSubscriber;
        }

        #region Order Handling
        public List<KeyValuePair<string, string>> RequestOrderDetailsAsync(Models.Data.Trading.Interfaces.IOrder order)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(order.LocalID) && _orderIdToTagsMap.TryGetValue(order.LocalID, out List<KeyValuePair<string, string>> tags))
                {
                    return tags;
                }

                if (!string.IsNullOrWhiteSpace(order.PermID) && _orderIdToTagsMap.TryGetValue(order.PermID, out tags))
                {
                    return tags;
                }

                if (!string.IsNullOrWhiteSpace(order.OrderID) && _orderIdToTagsMap.TryGetValue(order.OrderID, out tags))
                {
                    return tags;
                }

                return null;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestOrderDetailsAsync));
                return null;
            }
        }

        #region SendOrder Logic

        /// <param name="omsOrderUpdateSubscriber">Instance that will handle Execution Reports and Order Cancel Reject messages.</param>
        /// <exception cref="SlimException"></exception>
        public async Task<string> SendOrderAsync(OpsOrderModel omsOrder, InstanceMode instanceMode, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber, bool checkForOrderRate, double multiplier, bool checkForDuplicate = true, List<KeyValuePair<string, string>> tags = null)
        {
            if (!_config.RouteOpsOrdersToAutoTraderDirect)
            {
                return await SendOpsOrderAsync(omsOrder, instanceMode, omsOrderUpdateSubscriber, checkForOrderRate, multiplier, checkForDuplicate, tags);
            }
            else if (omsOrder.Security != null)
            {

                SendAutoTraderDirectOrder(omsOrder, omsOrderUpdateSubscriber);
                return omsOrder.LocalID;
            }
            else
            {
                throw new SlimException("Module is not configured to send to AutoTrader Direct.");
            }
        }

        private async Task<string> SendOpsOrderAsync(OpsOrderModel omsOrder, InstanceMode instanceMode, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber, bool checkForOrderRate, double multiplier, bool checkForDuplicate = true, List<KeyValuePair<string, string>> tags = null)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                var (riskCheckTime, time) = PrepareToSendOrder(omsOrder, omsOrderUpdateSubscriber, checkForOrderRate, multiplier, checkForDuplicate, tags, timer);
                ResolveOpsRoute(omsOrder, instanceMode);
                string orderId;
                Comms.Models.Data.Venue venue = ConvertInstanceModeToVenue(instanceMode);
                Func<Task<string>> sendAction;
                string venueName;
                switch (venue)
                {
                    case Comms.Models.Data.Venue.Silexx:
                        sendAction = () => _commsClient.SendOrderAsync(omsOrder);
                        venueName = "SILEXX";
                        break;
                    case Comms.Models.Data.Venue.TB:
                        sendAction = () => _commsClient.SendOrderFixAsync(omsOrder, useZpFix: false);
                        venueName = "TB";
                        break;
                    case Comms.Models.Data.Venue.ZP:
                        sendAction = () => _commsClient.SendOrderFixAsync(omsOrder, useZpFix: true);
                        venueName = "ZP";
                        break;
                    default:
                        throw new SlimException($"Routing Venue not found for {omsOrder.Route}");
                }
                orderId = await sendAction();
                long submitTime = timer.ElapsedMilliseconds - riskCheckTime;
                _log.Info($"{nameof(SendOrderAsync)} OrderClient {venueName} -> Symbol: {omsOrder.Symbol}, Route: {omsOrder.Route}, Px: {omsOrder.Price}, Qty: {omsOrder.Qty}, SubType: {omsOrder.SubType}, Submit Time: {submitTime:N2}, Risk Check Time: {riskCheckTime:N2}, Local Id: {omsOrder.LocalID}, Order Id: {orderId}, Time: {time}");
                if (submitTime > OmsCore.Config.OrderDelayForBlocking)
                {
                    BlockFurtherSubmission();
                }

                SendTagModel(orderId, omsOrder.OrderTag);
                AddToLookup(omsOrder);
                return orderId;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <param name="omsOrderUpdateSubscriber">Instance that will handle Execution Reports and Order Cancel Reject messages.</param>
        /// <exception cref="SlimException"></exception>
        public string SendOrder(OpsOrderModel omsOrder, InstanceMode? instanceMode, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber, bool checkForOrderRate, double multiplier, bool checkForDuplicate = true, List<KeyValuePair<string, string>> tags = null)
        {
            if (!_config.RouteOpsOrdersToAutoTraderDirect)
            {
                return SendOpsOrder(omsOrder, instanceMode, omsOrderUpdateSubscriber, checkForOrderRate, multiplier, checkForDuplicate, tags);
            }
            else if (omsOrder.Security != null)
            {
                SendAutoTraderDirectOrder(omsOrder, omsOrderUpdateSubscriber);
                return omsOrder.LocalID;
            }
            else
            {
                throw new SlimException("Module is not configured to send to AutoTrader Direct.");
            }
        }

        private string SendOpsOrder(OpsOrderModel omsOrder, InstanceMode? instanceMode, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber, bool checkForOrderRate, double multiplier, bool checkForDuplicate = true, List<KeyValuePair<string, string>> tags = null)
        {
            var (_, time) = PrepareToSendOrder(omsOrder, omsOrderUpdateSubscriber, checkForOrderRate, multiplier, checkForDuplicate, tags);
            ResolveOpsRoute(omsOrder, instanceMode);
            string orderId;
            Comms.Models.Data.Venue venue = ConvertInstanceModeToVenue(instanceMode);
            Func<string> sendAction;
            string venueName;
            switch (venue)
            {
                case Comms.Models.Data.Venue.Silexx:
                    sendAction = () => _commsClient.SendOrder(omsOrder);
                    venueName = "SILEXX";
                    break;
                case Comms.Models.Data.Venue.TB:
                    sendAction = () => _commsClient.SendOrderFix(omsOrder, Models.Data.Enums.Venue.TB);
                    venueName = "TB";
                    break;
                case Comms.Models.Data.Venue.ZP:
                    sendAction = () => _commsClient.SendOrderFix(omsOrder, Models.Data.Enums.Venue.ZpFix);
                    venueName = "ZP";
                    break;
                default:
                    throw new SlimException($"Routing Venue not found for {omsOrder.Route}");
            }
            var timer = Stopwatch.StartNew();
            orderId = sendAction();
            _log.Info($"{nameof(SendOrder)} OrderClient {venueName} -> Symbol: {omsOrder.Symbol}, Route: {omsOrder.Route}, Px: {omsOrder.Price}, Qty: {omsOrder.Qty}, SubType: {omsOrder.SubType}, Elapsed: {timer.ElapsedMilliseconds} ms, Local Id: {omsOrder.LocalID}, Order Id: {orderId}, Time: {time}");
            if (timer.ElapsedMilliseconds > OmsCore.Config.OrderDelayForBlocking)
            {
                BlockFurtherSubmission();
            }
            timer.Stop();
            SendTagModel(orderId, omsOrder.OrderTag);
            AddToLookup(omsOrder);
            return orderId;
        }

        private void SendAutoTraderDirectOrder(OpsOrderModel order, IOmsOrderUpdateSubscriber orderUpdateSubscriber)
        {
            var tagModel = order.OrderTag;
            order.Bid = tagModel.Bid;
            order.Ask = tagModel.Ask;
            order.DeltaAdjustedTheo = tagModel.Theo;
            order.Ema = tagModel.Ema;
            order.TagEdge = tagModel.Edge;
            order.UnderBid = tagModel.UnderBid;
            order.UnderAsk = tagModel.UnderAsk;
            order.SubType = tagModel.OrderSubType;
            order.VolaTheo = tagModel.VolaTheo;
            order.VolaTheoAdj = tagModel.VolaTheoAdj;
            order.VolaIv = tagModel.VolaIv;
            order.TheoBid = tagModel.TheoBid;
            order.TheoAsk = tagModel.TheoAsk;
            order.DigBid = tagModel.DigBid;
            order.DigAsk = tagModel.DigAsk;
            order.DigBidSize = tagModel.DigBidSize;
            order.DigAskSize = tagModel.DigAskSize;
            order.WeightedVega = tagModel.WeightedVega;
            order.SharedId = tagModel.SharedId;
            order.Sequence = tagModel.Sequence;
            order.TypeId = tagModel.ModuleType;
            order.SubTypeId = tagModel.SubType;
            order.SubTypeSequence = tagModel.SubTypeSequence;
            order.EdgeType = tagModel.EdgeType;
            order.OrderSource = OrderSource.AutoTraderLocal;
            order.Tag = order.OrderTag.Trader;
            _log.Info($"{nameof(SendAutoTraderDirectOrder)} OrderClient -> AUTOTRADER LOCAL Symbol: {order.Symbol}, Route: {order.Route}, Px: {order.Price}, Qty: {order.Qty}, SubType: {order.SubType}, Local Id: {order.LocalID}");
            _autoTraderDirectClient.SendOrder(order, orderUpdateSubscriber);
        }

        private static Comms.Models.Data.Venue ConvertInstanceModeToVenue(InstanceMode? instanceMode)
        {
            return instanceMode switch
            {
                InstanceMode.OPS_TB => Comms.Models.Data.Venue.TB,
                InstanceMode.OPS_ZPFIX => Comms.Models.Data.Venue.ZP,
                _ => Comms.Models.Data.Venue.Silexx,
            };
        }

        private void ResolveOpsRoute(OpsOrderModel omsOrder, InstanceMode? instanceMode)
        {
            if (omsOrder == null || string.IsNullOrWhiteSpace(omsOrder.Route)) return;
            if (RouteLookup == null) return;

            var routingVenue = instanceMode switch
            {
                InstanceMode.OPS_TB => Models.Data.Enums.Venue.TB,
                InstanceMode.OPS_ZPFIX => Models.Data.Enums.Venue.ZpFix,
                _ => Models.Data.Enums.Venue.Silexx,
            };

            var orderType = omsOrder.IsComplexOrder
                ? Models.Data.Models.OrderRouting.OrderType.Complex
                : (omsOrder.Security != null && omsOrder.Security.SecurityType == Models.Data.Enums.SecurityType.Option
                    ? Models.Data.Models.OrderRouting.OrderType.Option
                    : Models.Data.Models.OrderRouting.OrderType.Stock);

            if (RouteLookup.TryGetCorrectRouteName(routingVenue, omsOrder.Route, orderType, out var wireRoute, out _))
            {
                if (!string.IsNullOrWhiteSpace(wireRoute))
                {
                    omsOrder.Route = wireRoute;
                }
            }
        }

        private static void SendTagModel(string orderId, OrderTagModel tagModel)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(orderId) && tagModel != null)
                {
                    tagModel.PermId = orderId;
                    OmsCore.HerculesClient.SendOrderTag(tagModel);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendTagModel));
            }
        }

        #region Private SendOrder Helpers
        private (long RiskCheckTime, string Time) PrepareToSendOrder(OpsOrderModel omsOrder, IOmsOrderUpdateSubscriber omsOrderUpdateSubscriber, bool checkForOrderRate, double multiplier, bool checkForDuplicate, List<KeyValuePair<string, string>> tags, Stopwatch timer = null)
        {
            if (BlockSubmission)
            {
                throw new SlimException("Order submission blocked because of delay in previous order.");
            }

#if RELEASE
            if (!IsConnected)
            {
                throw new SlimException("Order client not connected.");
            }
#endif

            bool isSingleLeg = omsOrder.Legs.Count <= 1;
            bool isStock = isSingleLeg && omsOrder.Symbol != null && omsOrder.Symbol.Length > 0 && omsOrder.Symbol[0] != '.';
            bool isMarket = omsOrder.OMSOrderType == "MARKET";
            Side side = isSingleLeg ? omsOrder.OMSSide == Side.Buy.ToString().ToUpper() ? Side.Buy : Side.Sell : omsOrder.Price > 0 ? Side.Buy : Side.Sell;
            RiskCheck(omsOrder, isSingleLeg, isStock, side, multiplier, isMarket, checkForDuplicate);

            if (checkForOrderRate)
            {
                CheckForSubmissionRate();
            }

            long riskCheckTime = timer?.ElapsedMilliseconds ?? default;
            if (string.IsNullOrWhiteSpace(omsOrder.LocalID))
            {
                omsOrder.LocalID = GetNextOrderId();
            }

            string time = DateTime.Now.ToString("hh:mm:ss:ffff");
            _orderIdToOrderUpdateSubscriberMap[omsOrder.LocalID] = omsOrderUpdateSubscriber;

            if (tags != null)
            {
                _orderIdToTagsMap[omsOrder.LocalID] = tags;
            }

            return (riskCheckTime, time);
        }

        private void CheckForSubmissionRate()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            long oneSecondAgo = now - System.Diagnostics.Stopwatch.Frequency;

            lock (_orderSubmissionRateTrackerLock)
            {
                while (_orderSubmissionRateTracker.Count > 0 && _orderSubmissionRateTracker.Peek() < oneSecondAgo)
                {
                    _orderSubmissionRateTracker.Dequeue();
                }

                if (_orderSubmissionRateTracker.Count > OmsCore.Config.MaxOrderSubmissionPerSecondLimit)
                {
                    throw _orderRateEx;
                }

                _orderSubmissionRateTracker.Enqueue(now);
            }
        }

        private void AddToLookup(OpsOrderModel omsOrder)
        {
            try
            {
                _lastOrderSendTime = DateTime.Now;
                if (_startDelayCounter)
                {
                    _orderAfterDelay++;
                }
                if (!string.IsNullOrWhiteSpace(omsOrder.Symbol))
                {
                    if (OmsCore.Config.BlockDuplicateByTimeV2)
                    {
                        _symbolToLastOrderTimeMap[omsOrder.Symbol] = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddToLookup));
            }
        }

        private void BlockFurtherSubmission()
        {
            if (!BlockSubmission && OmsCore.Config.OrderDelayBlockingDelay > 0)
            {
                BlockSubmission = true;
                _timer.Interval = OmsCore.Config.OrderDelayBlockingDelay;
                _timer.Start();
            }
        }
#endregion

#endregion

        public void CancelReplaceOrder(ModifyRequest modifyRequest, bool singleLeg, Side side, double multiplier, bool isStock)
        {
            RiskCheck(modifyRequest.Price, modifyRequest.Quantity, singleLeg, isStock, side, multiplier);
            if (!_config.RouteOpsOrdersToAutoTraderDirect)
            {
                CancelReplaceOpsOrder(modifyRequest.OrderId, modifyRequest.Price, modifyRequest.Quantity);
            }
            else if (modifyRequest != null)
            {
                _autoTraderDirectClient.ModifyOrder(modifyRequest);
            }
            else
            {
                throw new SlimException("Module is not configured to send modify requests to AutoTrader Direct.");
            }
        }

        private void CancelReplaceOpsOrder(string orderId, double price, int qty)
        {
            if (orderId.StartsWith("i"))
            {
                if (_orderIdToExchangeIdMap.TryGetValue(orderId, out string exchangeId))
                {
                    _commsClient.CancelReplaceOrder(exchangeId, price, qty);
                    _log.Info($"{nameof(CancelReplaceOrder)} -> Order id: {orderId}, Exchange id: {exchangeId}, Price: {price}, Qty: {qty}.");
                }
                else
                {
                    _commsClient.CancelReplaceOrder(orderId, price, qty);
                    _log.Warn($"{nameof(CancelReplaceOrder)} -> Order id: {orderId}, Exchange id: <>, Price: {price}, Qty: {qty}.");
                }
            }
            else
            {
                _log.Info($"{nameof(CancelReplaceOrder)} -> Exchange id: {orderId}, Price: {price}, Qty: {qty}.");
                _commsClient.CancelReplaceOrder(orderId, price, qty);
            }
        }

        public void CancelOrder(CancelRequest cancelRequest)
        {
            if (!_config.RouteOpsOrdersToAutoTraderDirect)
            {
                _commsClient.CancelOrder(cancelRequest.OrderId);
                _log.Info($"{nameof(CancelOrder)} -> Exchange id: {cancelRequest.OrderId}");
            }
            else
            {
                _autoTraderDirectClient.CancelOrder(cancelRequest);
            }
        }

        public void ClearOrder(string symbol)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    _symbolToLastOrderTimeMap.TryRemove(symbol, out _);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearOrder));
            }
        }

        #region Private Risk Check Helpers
        private void RiskCheck(OpsOrderModel order, bool singleLeg, bool isStock, Side side, double multiplier, bool isMarket = false, bool checkForDuplicate = true)
        {
            RiskCheck(order.Price, order.Qty, singleLeg, isStock, side, multiplier, isMarket, order.Route);
            if (checkForDuplicate)
            {
                if (!string.IsNullOrWhiteSpace(order.Symbol))
                {
                    if (OmsCore.Config.BlockDuplicateByTimeV2)
                    {
                        if (_symbolToLastOrderTimeMap.TryGetValue(order.Symbol, out DateTime time))
                        {
                            double totalMilliseconds = (DateTime.Now - time).TotalMilliseconds;
                            if (totalMilliseconds < OmsCore.Config.BlockDuplicateThresholdV2)
                            {
                                throw new SlimException($"[Risk] Same order was submitted {totalMilliseconds:N2}ms ago.", ErrorType.DuplicateOrderFound);
                            }
                        }
                    }
                }
            }
        }

        private void RiskCheck(double price, int qty, bool singleLeg, bool isStock, Side side, double multiplier, bool isMarket = false, string route = "")
        {
            bool ignorePriceCheck = singleLeg && isStock && isMarket;
            if (double.IsNaN(price) && !ignorePriceCheck)
            {
                throw new SlimException("[Risk] Price can not be NaN.");
            }
            if (singleLeg && price <= 0 && !isMarket && !ignorePriceCheck)
            {
                throw new SlimException("[Risk] Invalid price.");
            }
            if (isStock && _user.LimitByStockLegMaxQty && qty > _user.SingleStockMaxQty)
            {
                throw new SlimException("[Warn] Qty beyond set limit for stocks.");
            }
            if (!isStock && _user.LimitByMaxQty && qty > _user.MaxQty)
            {
                throw new SlimException("[Warn] Qty beyond set limit.");
            }
            if (!isStock && singleLeg && _user.LimitBySingleLegMaxQty && qty > _user.SingleLegMaxQty)
            {
                throw new SlimException("[Warn] Qty beyond set limit for single legs.");
            }
            if (!isMarket)
            {
                double notional = Math.Abs(price) * qty * multiplier;
                if (side == Side.Buy && _user.LimitByMaxLongNotional)
                {
                    if (notional > _user.MaxLongNotional)
                    {
                        throw new SlimException("[Warn] Qty beyond set notional for longs.");
                    }
                }
                else if (side == Side.Sell && _user.LimitByMaxShortNotional)
                {
                    if (notional > _user.MaxShortNotional)
                    {
                        throw new SlimException("[Warn] Qty beyond set notional for shorts.");
                    }
                }
            }
            if (TraderPortfolio != null)
            {
                // To do: Optimize open spreads count calculation
                //if (_user.LimitByMaxSpreadCount && TraderPortfolio.GetTraderOpenPositionSpreadsCount() > _user.MaxSpreadCount)
                //{
                //    throw new SlimException("[Warn] User unique spread count beyond set limit.");
                //}
                if (_user.LimitByMaxDelta && Math.Abs(TraderPortfolio.NetDelta) > _user.MaxDelta)
                {
                    throw new SlimException("[Warn] User delta beyond set limit.");
                }
                if (_user.LimitByMaxRealizedPnl && TraderPortfolio.RealizedPnl < 0 && Math.Abs(TraderPortfolio.RealizedPnl) > Math.Abs(_user.MaxRealizedPnl))
                {
                    throw new SlimException("[Warn] User realized loss beyond limit.");
                }
                if (_user.LimitByMaxUnRealizedPnl && TraderPortfolio.UnrealizedPnl < 0 && Math.Abs(TraderPortfolio.UnrealizedPnl) > Math.Abs(_user.MaxUnRealizedPnl))
                {
                    throw new SlimException("[Warn] User unrealized loss beyond limit.");
                }
            }
        }
        #endregion

#endregion

        #region Position Handling
        public List<OmsPosition> GetAllPositions(string symbol)
        {
            List<OmsPosition> positions = _accountAndSymbolToPositionMap.Values.SelectMany(x => x.Values).Where(x => x.Instrument.underlyingSymbol == symbol).DistinctBy(x => x.Symbol).ToList();
            return positions;
        }

        public Comms.Models.Data.Responses.AdjustPositionResponse SendPositionAdjustmentRequest(string account, string symbol, int netQtyDelta, double openingPrice)
        {
            try
            {
                return _positionClient.SendPositionAdjustmentRequest(account, symbol, netQtyDelta, openingPrice);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendPositionAdjustmentRequest));
                return null;
            }
        }

        public void SubscribePosition(string symbol, string account, IOmsPositionSubscriber subscriber)
        {
            try
            {
                Tuple<string, string> subscriptionKey = Tuple.Create(symbol, account);
                AddToLookupMaps(subscriber, subscriptionKey);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscribePosition)}");
            }
        }

        public void UnsubscribePosition(string symbol, string account, IOmsPositionSubscriber subscriber)
        {
            UnsubscribeAllPosition(subscriber);
        }

        public void UnsubscribeAllPosition(IOmsPositionSubscriber subscriber)
        {
            try
            {
                if (_subscriberToSubscriptionsMap.TryRemove(subscriber, out ConcurrentDictionary<Tuple<string, string>, byte> subscriptions))
                {
                    foreach (KeyValuePair<Tuple<string, string>, byte> subscription in subscriptions)
                    {
                        if (_subscriptionKeyToSubscribersMap.TryGetValue(subscription.Key, out PositionSubscribers subscribers))
                        {
                            subscribers.Remove(subscriber);
                            if (subscribers.IsEmpty() && !_subscribedToAllPositions)
                            {
                                _positionClient.UnsubscribePosition(subscription.Key.Item1, subscription.Key.Item2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeAllPosition)}");
            }
        }

        public void SetPositionUpdateSubscriber(IPositionUpdateSubscriber positionUpdateSubscriber)
        {
            PositionUpdateSubscriber = positionUpdateSubscriber;
        }

        public void FirstPortfolioWindowOpened()
        {
            _positionClient.SendAllPositionsRequestMessage(subscribe: true);
            _subscribedToAllPositions = true;
            List<OmsPosition> positions = _accountAndSymbolToPositionMap.Values.SelectMany(x => x.Values).Distinct().ToList();
            PositionUpdateSubscriber?.AddMultipleUpdatedPosition(positions);
        }

        public void AllPortfolioWindowsClosed()
        {
            _positionClient.SendAllPositionsRequestMessage(subscribe: false);
            _subscribedToAllPositions = false;

            // Resubscribe to individual positions
            foreach (var kvp in _subscriptionKeyToSubscribersMap)
            {
                var subscriptionKey = kvp.Key;
                var subscribers = kvp.Value;

                if (subscribers.SubscribersCount > 0)
                    _positionClient.SubscribePosition(subscriptionKey.Item1, subscriptionKey.Item2);
            }
        }
        #endregion

#endregion

        #region Private methods

        private static ulong GenerateBaseInstanceId()
        {
            int processId = Process.GetCurrentProcess().Id;
            string id = $"{OmsCore.User.ID}-{processId}-{DateTime.Today:yyMMdd}";
            using SHA256 hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(id));
            string hex = BitConverter.ToString(bytes).Replace("-", "").ToLower();
            ulong suffix = System.Convert.ToUInt64(hex.Substring(0, 8), 16) % 256;
            return OMS_RANGE + (suffix << 32);
        }

        private void SetupBlockResetTimer()
        {
            _timer = new System.Timers.Timer()
            {
                AutoReset = false
            };
            _timer.Elapsed += (_, _) => BlockSubmission = false;
        }

        private void StartHealthCheckTimer()
        {
            _healthCheckTimer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = 1000,
            };
            _healthCheckTimer.Elapsed += OnHealthCheckTimerElapsed;
            _healthCheckTimer.Start();
        }

        private void OnHealthCheckTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (_lastOrderSendTime <= _lastStatusReceiveTime)
                {
                    if (_orderAfterDelay > 0)
                    {
                        _log.Info($"Resetting status delay monitor. " +
                            $"Last Order: {_lastOrderSendTime:HH:mm:ss.fffffff}, " +
                            $"Last Status: {_lastStatusReceiveTime:HH:mm:ss.fffffff}, " +
                            $"Orders After Delay: {_orderAfterDelay:F0}");
                        _startDelayCounter = false;
                        _orderAfterDelay = 0;
                        _delayReconnectCount = 0;
                    }
                }
                else
                {
                    double diff = (_lastOrderSendTime - _lastStatusReceiveTime).TotalSeconds;
                    double threshold = OmsCore.Config.MaxStatusDelaySeconds;
                    _log.Warn($"Status delayed detected. " +
                        $"Last Order: {_lastOrderSendTime:HH:mm:ss.fffffff}, " +
                        $"Last Status: {_lastStatusReceiveTime:HH:mm:ss.fffffff}, " +
                        $"Diff: {diff:F2}, " +
                        $"Threshold: {threshold:F2}, " +
                        $"Orders After Delay: {_orderAfterDelay:F0}");
                    if (diff > threshold)
                    {
                        _startDelayCounter = true;
                        if (diff > threshold * 2 && _orderAfterDelay > 10)
                        {
                            _startDelayCounter = default;
                            _orderAfterDelay = default;
                            _lastOrderSendTime = default;
                            _lastStatusReceiveTime = default;
                            if (_delayReconnectCount++ < 3)
                            {
                                _log.Warn($"Status delayed detected. Reconnecting order client. Attempt: {_delayReconnectCount}");
                                _ = RestartAsync();
                            }
                            else
                            {
                                _log.Warn($"Status delayed detected. Stopping order client. Attempt: {_delayReconnectCount}");
                                _ = StopAsync();
                            }
                        }
                    }
                }

                lock (_orderSubmissionRateTrackerLock)
                {
                    long oneSecondAgo = System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency;
                    while (_orderSubmissionRateTracker.Count > 0 && _orderSubmissionRateTracker.Peek() < oneSecondAgo)
                    {
                        _orderSubmissionRateTracker.Dequeue();
                    }
                }
            }
            finally
            {
                _healthCheckTimer.Start();
            }
        }

        #region Connection Status Changed Event Handlers
        private void OnConnectionStatusChangedEvent(bool connected)
        {
            _log.Info($"Connection status changed. Connected: {connected}");
            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                _commsClient.SendMdsClientRegistrationMsg();
                _commsClient.SendSetOmsClientNameMsg();
                GetDefaultAccountAndRoutes();
            }
        }

        private void OnPositionConnectionStatusChangedEvent(bool connected)
        {
            _log.Info($"Position connection status changed. Connected: {connected}");
            IsPositionConnected = connected;
            PositionConnectionStatusChangedEvent?.Invoke(IsPositionConnected);
            if (IsPositionConnected)
            {
                _positionClient.SendMdsClientRegistrationMsg();
                _positionClient.SendSetOmsClientNameMsg();
            }
        }
        #endregion

        private async void GetDefaultAccountAndRoutes()
        {
            await Task.Run(async () =>
            {
                await GetAccountAndRoutesAsync("MLEG");
                await GetAccountAndRoutesAsync("AAPL");
                await OmsCore.QuoteClient.GetSymbolsWithAttemptAsync("AAPL", 3).ContinueWith(async t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        string optionSymbol = t.Result.FirstOrDefault()?.OptionSymbol;
                        await GetAccountAndRoutesAsync(optionSymbol);
                    }
                });
            });
        }

        private void AddToLookupMaps(IOmsPositionSubscriber subscriber, Tuple<string, string> subscriptionKey)
        {
            if (!_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionKey, out PositionSubscribers subscribers))
            {
                subscribers = new PositionSubscribers(subscriptionKey);
                _subscriptionKeyToSubscribersMap.TryAdd(subscriptionKey, subscribers);
            }

            if (subscribers.SubscribersCount == 0)
            {
                _positionClient.SubscribePosition(subscriptionKey.Item1, subscriptionKey.Item2);
            }

            subscribers.AddAndInitSubscriber(subscriber);

            if (!_subscriberToSubscriptionsMap.TryGetValue(subscriber, out ConcurrentDictionary<Tuple<string, string>, byte> subscriptions))
            {
                subscriptions = new ConcurrentDictionary<Tuple<string, string>, byte>();
                _subscriberToSubscriptionsMap.TryAdd(subscriber, subscriptions);
            }
            subscriptions[subscriptionKey] = byte.MinValue;
        }

        #region Message Event Handling
        private void HandleMessage(Message message)
        {
            switch (message.Template.TemplateType)
            {
                case TemplateType.OMSUpdateOrderID:
                    OMSUpdateOrderID omsUpdateOrderID = MessageFactory.DecodeOMSUpdateOrderIDMessage(message);
                    HandleOrderIdUpdate(omsUpdateOrderID);
                    break;
                case TemplateType.OMSExecReport:
                    OMSExecReport omsExecReport = MessageFactory.DecodeOMSExecReportMessage(message);
                    HandleOrderExecutionReport(omsExecReport, DateTime.Now);
                    break;
                case TemplateType.OMSSendPosition:
                    OMSSendPosition omsSendPosition = MessageFactory.DecodeOMSSendPosition(message);
                    HandlePositionUpdate(omsSendPosition);
                    break;
                case TemplateType.OMSOrderCancelReject:
                    OMSOrderCancelReject orderCancelReject = MessageFactory.DecodeOMSOrderCancelReject(message);
                    HandleOrderCancelReject(orderCancelReject);
                    break;
            }
        }

        private void HandleOrderIdUpdate(OMSUpdateOrderID omsUpdateOrderID)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(omsUpdateOrderID.OrderID))
                {
                    _orderIdToExchangeIdMap[omsUpdateOrderID.OrigOrderID] = omsUpdateOrderID.OrderID;
                    if (_orderIdToTagsMap.TryGetValue(omsUpdateOrderID.LocalID, out List<KeyValuePair<string, string>> tags))
                    {
                        _orderIdToTagsMap[omsUpdateOrderID.OrderID] = tags;
                    }
                }
                _log.Info($"{nameof(HandleOrderIdUpdate)} -> Mds Id: {omsUpdateOrderID.OrigOrderID}, Exchange Id: {omsUpdateOrderID.OrderID}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOrderIdUpdate));
            }
        }

        private void HandleOrderExecutionReport(OMSExecReport omsExecReport, DateTime receiveTime)
        {
            try
            {
                _lastStatusReceiveTime = receiveTime;
                _log.Info($"{nameof(HandleOrderExecutionReport)} -> Updating Local Id: {omsExecReport.LocalOrderID}, Mds Id: {omsExecReport.OrigOrderID}, Exchange Id: {omsExecReport.OrderID}, Leg Id: {omsExecReport.LegID}, Status: {omsExecReport.OrderStatus}, Submit Time:{omsExecReport.TimeSubmitted}, Last Update Time:{omsExecReport.LastTimeUpdated}, Timestamp: {omsExecReport.Timestamp}");

                if (!string.IsNullOrWhiteSpace(omsExecReport.LegID))
                {
                    return;
                }

                NotifySubscribers(omsExecReport.LocalOrderID, omsExecReport.OrderID, omsExecReport.OrigOrderID, s => s?.HandleExecutionReport(Convert(omsExecReport), receiveTime));
                _log.Info($"{nameof(HandleOrderExecutionReport)} -> Updated Local Id: {omsExecReport.LocalOrderID}, Mds Id: {omsExecReport.OrigOrderID}, Exchange Id: {omsExecReport.OrderID}, Status: {omsExecReport.OrderStatus}, Submit Time:{omsExecReport.TimeSubmitted}, Last Update Time:{omsExecReport.LastTimeUpdated}, Timestamp: {omsExecReport.Timestamp}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOrderExecutionReport));
            }
        }

        private OrderUpdateModel Convert(OMSExecReport omsExecReport)
        {
            var execTypeParsed = Enum.TryParse(omsExecReport.ExecType, out ExecutionType executionType);
            var orderStatusParsed = Enum.TryParse(omsExecReport.OrderStatus, out OrderStatus status);
            Side? side = null;
            if (omsExecReport.Side != null)
            {
                if (Enum.TryParse(omsExecReport.Side, out Side parsedSide))
                {
                    side = parsedSide;
                }
            }

            if (!execTypeParsed)
                _log.Error($"{nameof(HandleOrderExecutionReport)} Status parsing failed. Id: {omsExecReport.OrderID}, Status: {omsExecReport.OrderStatus}");
            if (!orderStatusParsed)
                _log.Error($"{nameof(HandleOrderExecutionReport)} Exec Type parsing failed. Id: {omsExecReport.OrderID}, ExecType: {omsExecReport.ExecType}");

            var isSingleLeg = !omsExecReport.ComplexOrder;
            return new OrderUpdateModel()
            {
                OrderStatus = status,
                ExecutionType = executionType,
                Side = side,

                Price = omsExecReport.Price,
                AvgPrice = isSingleLeg ? omsExecReport.AvePx : omsExecReport.ComplexAvePx,
                LastPx = isSingleLeg ? omsExecReport.LastPx : omsExecReport.ComplexLastPx,
                LastQty = isSingleLeg ? omsExecReport.LastQty : omsExecReport.ComplexLastQty,
                CumQty = isSingleLeg ? omsExecReport.CumQty : omsExecReport.ComplexCumQty,
                LeavesQty = isSingleLeg ? omsExecReport.LeavesQty : omsExecReport.ComplexLeavesQty,
                Qty = isSingleLeg ? omsExecReport.Qty : omsExecReport.ComplexQty,
                LastUpdateTime = omsExecReport.LastTimeUpdated,
                IsCancelReject = omsExecReport.OrderUpdateType == OrderUpdateType.OrderCancelReject,

                ClientOrderId = omsExecReport.LocalOrderID,
                PrevClientOrderId = omsExecReport.LocalOrderID,
                OrigOrderId = omsExecReport.OrigOrderID,
                OrderId = omsExecReport.OrderID,
                LastExchange = omsExecReport.LastExchange,
                Message = omsExecReport.Comment,
                Route = omsExecReport.Route
            };
        }

        private void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            try
            {
                Task.Run(() => NotifySubscribers(orderCancelReject.LocalOrderID, orderCancelReject.OrderID, orderCancelReject.OrigOrderID, (s) => s?.HandleOrderCancelReject(orderCancelReject)));
                _log.Info($"{nameof(HandleOrderCancelReject)} -> Local Id: {orderCancelReject.LocalOrderID}, Mds Id: {orderCancelReject.OrigOrderID}, Exchange Id: {orderCancelReject.OrderID}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOrderCancelReject));
            }
        }

        private void HandlePositionUpdate(OMSSendPosition omsSendPosition)
        {
            try
            {
                Tuple<string, string> subscriptionKey = Tuple.Create(omsSendPosition.Symbol, omsSendPosition.AccountAcronym);
                if (!_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionKey, out PositionSubscribers subscribers))
                {
                    subscribers = new PositionSubscribers(subscriptionKey);
                    _subscriptionKeyToSubscribersMap.TryAdd(subscriptionKey, subscribers);
                }
                subscribers.UpdateValues(omsSendPosition);

                OmsPosition position = GetPosition(omsSendPosition.AccountAcronym, omsSendPosition.Symbol);
                position.Update(omsSendPosition);
                HandlePositionUpdate(position);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePositionUpdate));
            }
        }

        private OmsPosition GetPosition(string account, string symbol)
        {
            if (!_accountAndSymbolToPositionMap.TryGetValue(account, out ConcurrentDictionary<string, OmsPosition> symbolToPositionMap))
            {
                symbolToPositionMap = new ConcurrentDictionary<string, OmsPosition>();
                _accountAndSymbolToPositionMap[account] = symbolToPositionMap;
            }

            if (!symbolToPositionMap.TryGetValue(symbol, out OmsPosition position))
            {
                position = new OmsPosition(symbol);
                symbolToPositionMap[symbol] = position;
            }

            return position;
        }

        private void HandlePositionUpdate(OmsPosition position)
        {
            try
            {
                PositionUpdateSubscriber?.AddUpdatedPosition(position);
            }
            catch (SlimException e)
            {
                _log.Warn(e, $"{nameof(HandlePositionUpdate)} -> SlimException when updating position for Symbol: {position.Symbol}, Account: {position.AccountAcronym}.");
            }
        }

        private void NotifySubscribers(string localOrderId, string orderID, string origOrderID, Action<IOmsOrderUpdateSubscriber> action)
        {
            try
            {
                if (_orderIdToOrderUpdateSubscriberMap.TryGetValue(localOrderId, out IOmsOrderUpdateSubscriber subscriber) ||
                    _orderIdToOrderUpdateSubscriberMap.TryGetValue(orderID, out subscriber) ||
                    _orderIdToOrderUpdateSubscriberMap.TryGetValue(origOrderID, out subscriber))
                {
                    action(subscriber);
                }
                else
                {
                    _log.Error($"{nameof(NotifySubscribers)} Order id: {localOrderId} not found in subscribers lookup map.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(NotifySubscribers));
            }
        }
        #endregion

        #endregion
    }
}
