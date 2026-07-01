using NLog;
using System;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SymbolHedgeManagerModel : OrderUpdateHandler, IOmsDataSubscriber
    {
        private const double MAX_DELTA_NEUTRAL_HEDGE_INTERVAL = 10;
        private const double MAX_DELTA_NEUTRAL_HEDGE_PERCENTAGE = .10;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly object _lock = new();
        private string _hedgeSubscriptionKey;
        private string _spreadDescription;


        private readonly IAbstractFactory<ComplexOrderTicketViewModel> _ticketFactory;

        private ZeroPlus.Models.Data.Enums.Side _closeSide;


        private bool _positionUpdated;

        private DateTime _lastEdgeDecrement;

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial bool IsRunning { get; set; }
        [Bindable]
        public partial string Underlying { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        public string Description
        {
            get => _spreadDescription;
            set => SetValue(ref _spreadDescription, value);
        }
        [Bindable]
        public partial double RequiredEdge { get; set; }
        [Bindable]
        public partial double MinRequiredEdge { get; set; }
        [Bindable]
        public partial double Decrement { get; set; }
        [Bindable]
        public partial double DecrementInterval { get; set; }
        [Bindable]
        public partial int HedgeNetQty { get; set; }
        [Bindable]
        public partial int HedgeReqQty { get; set; }
        [Bindable]
        public partial int HedgeWorkingQty { get; set; }
        [Bindable]
        public partial double HedgeRealPnl { get; set; }
        [Bindable]
        public partial double HedgeUnrealPnl { get; set; }
        [Bindable]
        public partial double HedgeNetPnl { get; set; }
        [Bindable]
        public partial double HedgeAveragePrice { get; set; }
        [Bindable]
        public partial int PositionNetQty { get; set; }
        [Bindable]
        public partial int PositionWorkingQty { get; set; }
        [Bindable]
        public partial double PositionRealPnl { get; set; }
        [Bindable]
        public partial double PositionUnrealPnl { get; set; }
        [Bindable]
        public partial double PositionNetPnl { get; set; }
        [Bindable]
        public partial double PackageNetPnl { get; set; }
        [Bindable]
        public partial int PosNet { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial double PositionAveragePrice { get; set; }
        [Bindable(Default = 1)]
        public partial double ClosePercentage { get; set; }
        [Bindable]
        public partial bool ResumeAfterPartialClose { get; set; }
        [Bindable(Default = 1)]
        public partial int MinQty { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial string OrderId { get; set; }
        [Bindable]
        public partial string HedgeOrderId { get; set; }
        [Bindable]
        public partial ComplexOrderTicketViewModel Ticket { get; set; }
        [Bindable]
        public partial bool RouteOverrideEnabled { get; set; }
        [Bindable]
        public partial bool DeltaNeutralEnabled { get; set; }
        [Bindable(Default = 1)]
        public partial double DeltaNeutralPercentage { get; set; }
        [Bindable(Default = 10)]
        public partial double DeltaNeutralMinQty { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial string HedgeStatus { get; set; }
        [Bindable]
        public partial StatusMode HedgeStatusMode { get; set; }
        [Bindable]
        public partial DateTime LastDeltaNeutralHedge { get; set; }
        public PortfolioManagerModel PortfolioManagerModel { get; }
        public TransactionConsumerModel TransactionConsumer { get; }
        public NotificationManager NotificationManager { get; }
        public bool IsDisposed { get; set; }
        public override OrderSubType? SubType { get; set; } = ZeroPlus.Models.Data.Enums.OrderSubType.HedgeHouse;
        public double HedgeQtyPerContract { get; private set; }
        public int HedgeResubmitCount { get; private set; }

        public SymbolHedgeManagerModel(IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
                                       PortfolioManagerModel portfolioManagerModel,
                                       NotificationManager notificationManager,
                                       TransactionConsumerModel transactionConsumer)
        {
            _ticketFactory = ticketFactory;
            PortfolioManagerModel = portfolioManagerModel;
            NotificationManager = notificationManager;
            TransactionConsumer = transactionConsumer;
        }

        internal async void Initialize(IPosition position)
        {
            RequiredEdge = .10;
            DecrementInterval = 5;
            _hedgeSubscriptionKey = position.Name.ToUpper();
            string spreadId = position.Name.Replace("HEDGE - " + OmsCore.User.Username.ToUpper() + " - ", "").Trim().ToUpper();
            Description = spreadId;
            Symbol = position.Symbol;
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                Ticket = _ticketFactory.Create();
                Ticket.CanNotHedge = true;
                Ticket.SetDispatcher(PortfolioManagerModel.Dispatcher);
                await Ticket.LoadLegsFromTosAsync(Symbol);
                Underlying = Ticket.Underlying;
            }
            PortfolioManagerModel?.Subscribe(_hedgeSubscriptionKey, SubscriptionFieldType.UserInstancePosition, this);
            PortfolioManagerModel?.Subscribe(spreadId, SubscriptionFieldType.UserSpreadPosition, this);
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            switch (key.Type)
            {
                case SubscriptionFieldType.UserInstancePosition:
                    if (symbol == _hedgeSubscriptionKey)
                    {
                        HandleHedgePositionUpdate(value as IPosition);
                    }
                    break;
                case SubscriptionFieldType.UserSpreadPosition:
                    if (symbol == _spreadDescription)
                    {
                        HandlePositionUpdate(value as IPosition);
                    }
                    break;
            }
        }

        private void HandlePositionUpdate(IPosition position)
        {
            try
            {
                lock (_lock)
                {
                    _positionUpdated = true;
                    PositionNetQty = position.NetQty;
                }
                PositionRealPnl = position.AdjustedPnl;
                if (PositionNetQty == 0)
                {
                    PositionAveragePrice = 0;
                }
                if (PositionNetQty == 0 && HedgeNetQty == 0)
                {
                    IsRunning = false;
                }
                else
                {
                    PositionAveragePrice = -Math.Round(position.OpenPositionAveragePrice, 2);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePositionUpdate));
            }
        }

        private void HandleHedgePositionUpdate(IPosition hedgePosition)
        {
            try
            {
                lock (_lock)
                {
                    HedgeNetQty = hedgePosition.NetQty;
                    HedgeRealPnl = hedgePosition.AdjustedPnl;
                }
                if (PositionNetQty == 0 && HedgeNetQty == 0)
                {
                    IsRunning = false;
                }
                HedgeAveragePrice = HedgeNetQty != 0 ? -Math.Round(hedgePosition.OpenPositionAveragePrice, 2) : 0;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleHedgePositionUpdate));
            }
        }

        internal void UpdateNetValues()
        {
            try
            {
                if (Ticket.MarkLoaded && Ticket.HedgeUnderLoaded)
                {
                    double hedgeAveragePrice = Math.Abs(HedgeAveragePrice);
                    if (HedgeNetQty > 0)
                    {
                        HedgeUnrealPnl = (Ticket.HedgeBid - hedgeAveragePrice) * HedgeNetQty;
                    }
                    else if (HedgeNetQty < 0)
                    {
                        HedgeUnrealPnl = (hedgeAveragePrice - Ticket.HedgeAsk) * Math.Abs(HedgeNetQty);
                    }
                    else
                    {
                        HedgeUnrealPnl = 0;
                    }
                    HedgeNetPnl = HedgeRealPnl + HedgeUnrealPnl;

                    double positionAveragePrice = Math.Abs(PositionAveragePrice);
                    double mid = Math.Abs(Ticket.Mid);
                    if (PositionNetQty > 0)
                    {
                        PositionUnrealPnl = (mid - positionAveragePrice) * PositionNetQty * Ticket.Multiplier;
                    }
                    else if (PositionNetQty < 0)
                    {
                        PositionUnrealPnl = (positionAveragePrice - mid) * Math.Abs(PositionNetQty) * Ticket.Multiplier;
                    }
                    else
                    {
                        PositionUnrealPnl = 0;
                    }
                    PositionNetPnl = PositionRealPnl + PositionUnrealPnl;
                    PackageNetPnl = HedgeNetPnl + PositionNetPnl;

                    PosNet = Ticket.CalculateRequiredHedgeQty(PositionNetQty);
                    HedgeReqQty = PosNet - HedgeNetQty;
                    NetDelta = (HedgeNetQty - PosNet) / Ticket.Multiplier;
                }
                else
                {
                    _log.Warn(nameof(UpdateNetValues) + $" Data not loaded. Under Loaded: {Ticket.HedgeUnderLoaded}, Option Loaded: {Ticket.MarkLoaded}");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateNetValues));
            }
        }

        internal void AttemptClose()
        {
            try
            {
                if (Ticket.MarkLoaded && Ticket.HedgeUnderLoaded)
                {
                    lock (_lock)
                    {
                        Ticket.SetHedgingInstance();
                        int baseQty = PositionNetQty + PositionWorkingQty;
                        int openQty = baseQty;

                        if (ClosePercentage is > 0 and < 1)
                        {
                            openQty = (int)(baseQty * ClosePercentage);
                            if (baseQty != 0 && openQty == 0)
                            {
                                openQty = baseQty > 0 ? 1 : -1;
                            }

                            var minQty = Math.Min(MinQty, Math.Abs(baseQty));

                            if (openQty > 0)
                            {
                                openQty = Math.Max(minQty, openQty);
                            }
                            else if (openQty < 0)
                            {
                                openQty = Math.Max(minQty, Math.Abs(openQty)) * -1;
                            }
                        }

                        if (PositionNetQty != 0 && openQty != 0 && HedgeNetQty != 0)
                        {
                            _closeSide = openQty > 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                            if (Ticket.Side != _closeSide)
                            {
                                Ticket.Reverse();
                            }
                            if (Ticket.Side == _closeSide)
                            {
                                double requiredEdge = RequiredEdge;
                                HedgeQtyPerContract = (double)HedgeNetQty / Math.Abs(baseQty);
                                double hedgeAveragePrice = Math.Abs(HedgeAveragePrice);
                                double hedgeMid = Math.Abs(Ticket.HedgeMid);

                                double hedgeCost;
                                if (HedgeNetQty > 0)
                                {
                                    hedgeCost = (Ticket.HedgeBid - hedgeAveragePrice) * Math.Abs(HedgeQtyPerContract);
                                }
                                else if (HedgeNetQty < 0)
                                {
                                    hedgeCost = (hedgeAveragePrice - Ticket.HedgeAsk) * Math.Abs(HedgeQtyPerContract);
                                }
                                else
                                {
                                    return;
                                }

                                requiredEdge -= hedgeCost / Ticket.Multiplier;

                                var order = Ticket.BuildOrder(false, SubType, Math.Abs(openQty));
                                if (Ticket.IsSingleLeg)
                                {
                                    double positionAveragePrice = Math.Abs(PositionAveragePrice);
                                    switch (_closeSide)
                                    {
                                        case ZeroPlus.Models.Data.Enums.Side.Buy:
                                            double newPrice = positionAveragePrice - requiredEdge;
                                            order.Price = Ticket.PriceNeedsPadding(newPrice) ? Ticket.PadForNickelOrDime(newPrice, floor: true) : Math.Round(newPrice, 2);
                                            break;
                                        case ZeroPlus.Models.Data.Enums.Side.Sell:
                                            newPrice = positionAveragePrice + requiredEdge;
                                            order.Price = Ticket.PriceNeedsPadding(newPrice) ? Ticket.PadForNickelOrDime(newPrice, floor: false) : Math.Round(newPrice, 2);
                                            break;
                                    }
                                }
                                else
                                {
                                    double positionAveragePrice = -PositionAveragePrice;
                                    double newPrice = positionAveragePrice - requiredEdge;
                                    order.Price = Ticket.PriceNeedsPadding(newPrice) ? Ticket.PadForNickelOrDime(newPrice, floor: true) : Math.Round(newPrice, 2);
                                }

                                if (!RouteOverrideEnabled)
                                {
                                    if (Ticket.IsSingleLeg)
                                    {
                                        var singleLegHedgeRoute = OmsCore.Config.DefaultHedgeHouseSingleLegRoute;
                                        if (!string.IsNullOrWhiteSpace(singleLegHedgeRoute))
                                        {
                                            order.Route = singleLegHedgeRoute;
                                        }
                                        else
                                        {
                                            var defaultSingleLegRoute = OmsCore.Config.DefaultSingleLegRoute(OmsCore.Config.InstanceModeV3);
                                            if (!string.IsNullOrWhiteSpace(defaultSingleLegRoute))
                                            {
                                                order.Route = defaultSingleLegRoute;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(OmsCore.Config.DefaultHedgeHouseSpreadRoute))
                                        {
                                            order.Route = OmsCore.Config.DefaultHedgeHouseSpreadRoute;
                                        }
                                        else
                                        {
                                            var defaultRoute = OmsCore.Config.DefaultRoute(OmsCore.Config.InstanceModeV3);
                                            if (!string.IsNullOrWhiteSpace(defaultRoute))
                                            {
                                                order.Route = defaultRoute;
                                            }
                                        }
                                    }

                                    order.Route = _ticket.CheckForDirectRoute(order.Route);
                                }
                                _positionUpdated = false;
                                order.LocalID = OmsCore.OrderClient.GetNextOrderId();
                                OrderId = order.LocalID;
                                OmsCore.OrderClient.SendOrder(omsOrder: order, Ticket.GetInstanceMode(), omsOrderUpdateSubscriber: this, false, multiplier: Ticket.Multiplier, checkForDuplicate: false);
                                PositionWorkingQty -= openQty;
                                _log.Info("Hedge House Order submitted. " +
                                          "Spread: " + Description + ", " +
                                          "Hedge Qty: " + HedgeNetQty + ", " +
                                          "Hedge Avg Px: " + HedgeAveragePrice + ", " +
                                          "Hedge Mkt: " + hedgeMid + ", " +
                                          "Pos Qty: " + PositionNetQty + ", " +
                                          "Pos Avg Px: " + PositionAveragePrice + ", " +
                                          "HedgeQtyPerContract: " + HedgeQtyPerContract + ", " +
                                          "OrderId:" + OrderId + ", " +
                                          "Current px:" + order.Price);
                            }
                        }
                        else
                        {
                            _log.Info("Hedge House Order Not Submitted. " +
                                         "Spread: " + Description + ", " +
                                         "Hedge Qty: " + HedgeNetQty + ", " +
                                         "Hedge Avg Px: " + HedgeAveragePrice + ", " +
                                         "Pos Qty: " + PositionNetQty + ", " +
                                         "Pos Avg Px: " + PositionAveragePrice + ", " +
                                         "HedgeQtyPerContract: " + HedgeQtyPerContract + ", ");
                        }
                    }
                }
                else
                {
                    Message = "Data Load Failed";
                    _log.Warn(nameof(AttemptClose) + $" Data not loaded. Id: {Ticket.SpreadId}, Under Loaded: {Ticket.HedgeUnderLoaded}, Option Loaded: {Ticket.MarkLoaded}");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AttemptClose));
            }
        }

        internal void CheckForEdgeDecrement()
        {
            try
            {
                if (Decrement > 0 &&
                    DecrementInterval > 0 &&
                    RequiredEdge > MinRequiredEdge &&
                    (DateTime.Now - _lastEdgeDecrement).TotalSeconds > DecrementInterval)
                {
                    RequiredEdge = Math.Max(MinRequiredEdge, RequiredEdge - Decrement);
                    _lastEdgeDecrement = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForEdgeDecrement));
            }
        }

        private void HedgeLastFill(OrderUpdateModel execReport)
        {
            int qty = Math.Abs(execReport.LastQty);
            int totalStocks = (int)(-HedgeQtyPerContract * qty);

            HedgeResubmitCount = 0;

            CheckAndSubmitHedge(totalStocks);
        }

        private void CheckAndSubmitHedge(int totalStocks)
        {
            if (totalStocks > 0)
            {
                if (HedgeNetQty < 0)
                {
                    if (Math.Abs(HedgeNetQty) < totalStocks ||
                        Math.Abs(HedgeNetQty) - totalStocks < 4)
                    {
                        totalStocks = Math.Abs(HedgeNetQty);
                    }
                }
                else
                {
                    Message = "[Risk] Hedge qty not valid for position.";
                    return;
                }
            }
            else if (totalStocks < 0)
            {
                if (HedgeNetQty > 0)
                {
                    if (HedgeNetQty < Math.Abs(totalStocks) ||
                        HedgeNetQty - Math.Abs(totalStocks) < 4)
                    {
                        totalStocks = -HedgeNetQty;
                    }
                }
                else
                {
                    Message = "[Risk] Hedge qty not valid for position.";
                    return;
                }
            }
            else
            {
                Message = "[Risk] Hedge qty not valid for position.";
                return;
            }
            SubmitHedge(totalStocks);
        }

        public void SubmitHedge(int totalStocks)
        {
            var order = _ticket.BuildStockHedgeOrderAsync(totalStocks, null, SubType?.ToSpacedString());

            if (!OmsCore.Config.InstanceModeV3.IsAutoTraderInstance() && !string.IsNullOrWhiteSpace(OmsCore.Config.DefaultHedgeHouseRouteRegular))
            {
                order.Route = OmsCore.Config.DefaultHedgeHouseRouteRegular;
            }
            else if (OmsCore.Config.InstanceModeV3.IsAutoTraderInstance() && !string.IsNullOrWhiteSpace(OmsCore.Config.DefaultHedgeHouseRouteAutoTraderV2))
            {
                order.Route = OmsCore.Config.DefaultHedgeHouseRouteAutoTraderV2;
            }
            else
            {
                var defaultHedgeRoute = OmsCore.Config.DefaultHedgeRoute(OmsCore.Config.InstanceModeV3);
                if (!string.IsNullOrWhiteSpace(defaultHedgeRoute))
                {
                    order.Route = defaultHedgeRoute;
                }
            }

            order.Route = _ticket.CheckForDirectRoute(order.Route);

            if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
            {
                if (double.IsNaN(order.Price))
                {
                    Message = "[Risk] Hedge price could not be determined.";
                    return;
                }
                if (order.Price * order.Qty > OmsCore.Config.MaxAutoHedgeNetCash)
                {
                    Message = "[Risk] Hedge price above risk limit.";
                    return;
                }
            }

            if (OmsCore.Config.MaxAutoHedgePositionEnabled)
            {
                if (order.Qty > OmsCore.Config.MaxAutoHedgePosition)
                {
                    Message = "[Risk] Hedge qty above risk limit.";
                    return;
                }
            }

            lock (_lock)
            {
                order.LocalID = OmsCore.OrderClient.GetNextOrderId();
                _log.Info("Order submitted. Spread: " + Description + ", OrderId:" + OrderId + ", Current px:" + order.Price);
                HedgeOrderId = order.LocalID;
                OmsCore.OrderClient.SendOrder(omsOrder: order, Ticket.GetInstanceMode(), omsOrderUpdateSubscriber: this, false, multiplier: 1, checkForDuplicate: false);
                HedgeWorkingQty += totalStocks;
            }
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;

                if (!IsRunning || IsDisposed)
                {
                    return;
                }

                if (execReport.ClientOrderId == OrderId)
                {
                    if (executionType.Value.IsFilled())
                    {
                        int lastQuantity = execReport.LastQty;
                        int qty = _closeSide == ZeroPlus.Models.Data.Enums.Side.Buy ? lastQuantity : -lastQuantity;

                        lock (_lock)
                        {
                            PositionWorkingQty -= qty;

                            if (!_positionUpdated)
                            {
                                PositionNetQty += qty;
                            }
                        }

                        HedgeLastFill(execReport);
                    }
                    else if (orderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                    {
                        int leavesQty = execReport.Qty - execReport.CumQty;
                        int qty = _closeSide == ZeroPlus.Models.Data.Enums.Side.Buy ? leavesQty : -leavesQty;

                        lock (_lock)
                        {
                            PositionWorkingQty -= qty;
                        }

                        CheckForDeltaNeutralTrade();
                    }

                    if (orderStatus == OrderStatus.Filled)
                    {
                        if (!ResumeAfterPartialClose || Math.Abs(ClosePercentage - 1) < .001 || PositionNetQty == 0)
                        {
                            IsRunning = false;
                        }
                    }

                    OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

                    Status = orderUpdateValues.Status;
                    StatusMode = orderUpdateValues.StatusMode;
                }
                else if (execReport.ClientOrderId == HedgeOrderId)
                {
                    if (execReport.Side != null)
                    {
                        var side = execReport.Side.Value;
                        if (executionType.Value.IsFilled())
                        {
                            int lastQuantity = execReport.LastQty;
                            int qty = side == ZeroPlus.Models.Data.Enums.Side.Buy ? lastQuantity : -lastQuantity;

                            lock (_lock)
                            {
                                HedgeWorkingQty -= qty;
                            }
                        }
                        else if (orderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                        {
                            int leavesQty = execReport.Qty - execReport.CumQty;
                            int qty = side == ZeroPlus.Models.Data.Enums.Side.Buy ? leavesQty : -leavesQty;

                            lock (_lock)
                            {
                                HedgeWorkingQty -= qty;
                            }

                            if (leavesQty != 0)
                            {
                                if (++HedgeResubmitCount > OmsCore.Config.HedgeHouseResubmitCount)
                                {
                                    _ = _ticket.OpenHedgeTicketAsync(side, Math.Abs(leavesQty));
                                }
                                else
                                {
                                    CheckAndSubmitHedge(qty);
                                }
                            }
                        }

                        OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport, parseAsSingle: true);

                        HedgeStatus = orderUpdateValues.Status;
                        HedgeStatusMode = orderUpdateValues.StatusMode;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleExecutionReport));
            }
        }

        public void CheckForDeltaNeutralTrade()
        {
            if (DeltaNeutralEnabled)
            {
                UpdateNetValues();
                if (HedgeReqQty != 0 && DateTime.Now - LastDeltaNeutralHedge > TimeSpan.FromSeconds(MAX_DELTA_NEUTRAL_HEDGE_INTERVAL))
                {
                    int req = (int)(HedgeReqQty * DeltaNeutralPercentage);
                    if (req != 0 &&
                        Math.Abs(req) >= DeltaNeutralMinQty &&
                        Math.Abs(req) <= Math.Abs(HedgeNetQty * MAX_DELTA_NEUTRAL_HEDGE_PERCENTAGE))
                    {
                        LastDeltaNeutralHedge = DateTime.Now;
                        SubmitHedge(req);
                    }
                }
            }
        }

        public override void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            Message = orderCancelReject.Comment;
        }

        internal void Start()
        {
            Ticket.SetHedgingInstance();
            IsRunning = true;
        }

        internal void Stop()
        {
            IsRunning = false;
            Ticket.ReleaseHedgingInstance();
        }
    }
}
