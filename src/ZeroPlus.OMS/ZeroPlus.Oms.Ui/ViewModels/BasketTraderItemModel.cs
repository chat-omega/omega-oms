using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void MessageTriggeredEventHandler(string message, string title, bool canBeSilenced);
    public delegate void EdgeAcquiredEventHandler(BasketTraderItemModel order, double lastEdgeBeforeFees, double lastEdgeAfterFees);

    public partial class BasketTraderItemModel : OrderTicket
    {
        public event EdgeAcquiredEventHandler EdgeAcquiredEvent;

        private const double PX_TOLERANCE = .001;

        public MessageTriggeredEventHandler MessageTriggeredEvent;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private DateTime _lastAutoModified;
        internal LegInHandler LegInHandler;
        private int _lockTraderResubmitCount;

        [Bindable]
        public partial int CloseSpecificQty { get; set; }
        [Bindable]
        public partial double CloseSpecificEdgeToMid { get; set; }
        [Bindable]
        public partial bool AlertTriggered { get; set; }
        public override InstanceMode InstanceMode
        {
            get => BasketTraderViewModel?.GetInstanceMode() ?? OmsCore.Config.InstanceModeV3;
            set => _ = value;
        }
        public override string BrokerOverride
        {
            get => BasketTraderViewModel.BrokerOverride ?? OmsCore.Config.DefaultBroker;
            set => _ = value;
        }
        internal DateTime CreationTime { get; set; }
        public List<DateTime> Expirations => GetExpirations();
        protected override bool CanSubscribeToUnderlying => BasketSettings?.SubscribeToUnderlying ?? false;
        protected override bool CanSubscribeToHedge => BasketSettings?.SubscribeToHedgeUnderlying ?? false;
        protected override bool CanSubscribeToGlobalEdgeToTheo => BasketSettings?.SubscribeToGlobalEdgeToTheo ?? false;
        protected override bool CanSubscribeToFirmSummary => BasketSettings?.SubscribeToFirmSummary ?? false;
        public override bool SubscribedToWeightedVega => BasketSettings?.SubscribeToHanweck ?? false;
        public override bool SubscribedToNetTheo => BasketSettings?.SubscribeToHanweck ?? false;
        public override bool SubscribedToNetHistoricBest => BasketSettings?.SubscribeToFirmSummary ?? false;
        public override bool SubscribedToNetAdjTheo => BasketSettings?.SubscribeToDerivatives ?? false;
        public override bool SubscribedToLow => BasketSettings?.SubscribeToMarketData ?? false;
        public override bool SubscribedToLowestBid => BasketSettings?.SubscribeToDerivedValues ?? false;
        public override bool SubscribedToHighestOffer => BasketSettings?.SubscribeToDerivedValues ?? false;
        public override bool SubscribedToHighestBidLowestAsk => BasketSettings?.SubscribeToDerivedValues ?? false;
        public override bool SubscribedToHigh => BasketSettings?.SubscribeToMarketData ?? false;
        public override bool SubscribedToSize => BasketSettings?.SubscribeToMarketData ?? false;
        public override bool SubscribedToIbQuote => BasketTraderViewModel?.IsEdgeScanFeedAutoTrader ?? false;
        public override bool SubscribedToMark => BasketSettings?.SubscribeToMarketData ?? false;
        public override bool SubscribedToBestMark => BasketSettings?.SubscribeToHanweck ?? false;
        public override bool SubscribedToEma => BasketSettings?.SubscribeToEma ?? false;
        public override bool SubscribedToAdjEma => BasketSettings?.SubscribeToEma ?? false;
        public override bool SubscribedToBidEma => BasketSettings?.SubscribeToEma ?? false;
        public override bool SubscribedToAskEma => BasketSettings?.SubscribeToEma ?? false;
        public override bool SubscribedToUnder => BasketSettings?.SubscribeToUnderlying ?? false;
        public override bool SubscribedToHedgeUnder => BasketSettings?.SubscribeToHedgeUnderlying ?? false;
        public override string DestinationUid => BasketTraderViewModel.Uid;
        public override uint DestinationSequence
        {
            get => BasketTraderViewModel.ConfigSequence;
            set => _ = value;
        }

        public BasketTraderItemModel(BasketTraderViewModel basketTraderViewModel,
                                     Dispatcher dispatcher,
                                     OmsCore omsCore)
            : base(basketTraderViewModel.TicketFactory,
                   basketTraderViewModel.ThreeWayCloserFactory,
                   basketTraderViewModel.RouteSelectionViewFactory,
                   basketTraderViewModel.TransactionConsumer,
                   basketTraderViewModel.NotificationManager,
                   basketTraderViewModel.PortfolioManagerModel,
                   omsCore)
        {
            LegInHandler = new(this);
            SubType = basketTraderViewModel.ModuleType;
            Dispatcher = dispatcher;
            CreationTime = DateTime.Now;
            Active = true;
            IsBasketOrder = true;
            DisableDuplicateSubmissions = true;
            BasketTraderViewModel = basketTraderViewModel;
            BasketSettings = basketTraderViewModel.BasketSettings;
            MessageTriggeredEvent += basketTraderViewModel.ShowMessageFromItem;
        }

        protected override async Task ProcessAutomation(OrderUpdateModel execReport, DateTime receiveTime, OrderUpdateValues orderUpdateValues, bool isMainOrder, bool isContraOrder)
        {
            int filledQty = Math.Abs(execReport.LastQty);
            if (execReport.ExecutionType.IsFilled() && isContraOrder)
            {
                if (IsLooping && Looper.IcebergRunning)
                {
                    if (filledQty > 0)
                    {
                        Looper.IcebergTotalQty -= filledQty;
                        if (Looper.IcebergTotalQty <= 0)
                        {
                            Looper.IcebergTotalQty = 0;
                            Looper.IcebergRunning = false;
                        }
                        else if (Lcd > Looper.IcebergTotalQty)
                        {
                            Lcd = Looper.IcebergTotalQty;
                        }
                    }
                    else
                    {
                        Looper.IcebergTotalQty = 0;
                        Looper.IcebergRunning = false;
                        Looper.Stop();
                        _log.Warn(nameof(HandleExecutionReport) + " Invalid execution report.");
                    }
                }
            }

            AutomationConfigModel automationConfigModel = GetAutomationConfig();

            if (CxlReplaceCloser.Enabled && isContraOrder)
            {
                if (IsLooping && (orderUpdateValues.OrderStatus is OrderStatus.New or OrderStatus.Replaced))
                {
                    bool closeCont = await CxlReplaceCloser.ContClose();
                    if (!closeCont && !CxlReplaceCloser.Manual && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                    {
                        BasketTraderViewModel.CreateComplexOrderTicket(this);
                    }
                }
                else if (orderUpdateValues.OrderStatus is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.PartiallyFilled or OrderStatus.Rejected || execReport.ExecutionType.IsFilled())
                {
                    IsLooping = false;
                    StopLossEnabled = false;
                }
            }
            else if (Closer.Enabled && isContraOrder)
            {
                if (IsLooping && (orderUpdateValues.OrderStatus is OrderStatus.Canceled or OrderStatus.Rejected))
                {
                    int leavesQty = Math.Abs(execReport.LeavesQty);
                    bool closeCont = Closer.ContClose(leavesQty);
                    if (!closeCont && !Closer.Manual && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                    {
                        BasketTraderViewModel.CreateComplexOrderTicket(this);
                    }
                }
                else if (orderUpdateValues.OrderStatus is OrderStatus.Filled or OrderStatus.PartiallyFilled || execReport.ExecutionType.IsFilled())
                {
                    IsLooping = false;
                    StopLossEnabled = false;
                }
            }
            else if (Fisher.IsRunning && isMainOrder && orderUpdateValues.OrderStatus == OrderStatus.Canceled)
            {
                if (IsLooping)
                {
                    if (PartiallyFilled)
                    {
                        if (LeavesQty > 0)
                        {
                            Fisher.ContFish(receiveTime, LeavesQty);
                        }
                        else
                        {
                            _log.Info("Invalid Partial Fill. [Fish] " +
                                      "Loop enabled: " + automationConfigModel.LoopingEnabled + ", " +
                                      "Spread ID: " + SpreadId + ", " +
                                      "Last fill px: " + LastFillPx + ", " +
                                      "Leaves qty: " + LeavesQty + ", " +
                                      "Latency Timer: " + _latencyTimer.ElapsedMilliseconds + ", " +
                                      "Total cumulative: " + CumulativeQty + ".");
                        }
                    }
                    else
                    {
                        Fisher.ContFish(receiveTime);
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Filled && (automationConfigModel.AutoLegEnabled || automationConfigModel.GoFishAutoCloseEnabled || (BasketTraderViewModel.BasketType == BasketType.LockTrader && automationConfigModel.LockTraderResubmitOnFillEnabled)))
            {
                if (isMainOrder)
                {
                    IsFreeLooking = false;
                    LastFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastFillAdjTheo = NetDeltaAdjTheo;

                    if (automationConfigModel.LoopingEnabled &&
                        automationConfigModel.LooperDynamicRouting &&
                        automationConfigModel.ExchToRouteMap != null &&
                        execReport.LastExchange != null &&
                        automationConfigModel.ExchToRouteMap.TryGetValue(execReport.LastExchange, out var routeMap))
                    {
                        LastLoopRoute = routeMap;
                    }
                    else
                    {
                        LastLoopRoute = null;
                    }

                    if (MainOrderStatus == orderUpdateValues.OrderStatus)
                    {
                        _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                    }
                    else
                    {
                        if (automationConfigModel.AutoLegEnabled)
                        {
                            var qty = Lcd;
                            if (PartiallyFilled)
                            {
                                qty = CumulativeQty + filledQty;
                                CumulativeQty = 0;
                                LeavesQty = 0;
                                PartiallyFilled = false;
                            }

                            _log.Info("Starting auto-leg closer. Spread: " + SpreadId + ", OrderId:" + orderUpdateValues.OriginalOrderId + ", Status:" + execReport.OrderStatus + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                            bool closerStarted = AutoLegCloser!.ClosePosition(qty);

                            if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                            {
                                BasketTraderViewModel?.CreateComplexOrderTicket(this);
                            }
                        }
                        else if (automationConfigModel.LoopingEnabled)
                        {
                            if (PartiallyFilled)
                            {
                                if (!ResetSize)
                                {
                                    ResetSize = true;
                                    PrevQty = Lcd;
                                }
                                UpdateQty(CumulativeQty + filledQty);
                                CumulativeQty = 0;
                                LeavesQty = 0;
                                PartiallyFilled = false;
                            }
                            _log.Info("Starting loop closer. Spread: " + SpreadId + ", OrderId:" + orderUpdateValues.OriginalOrderId + ", Status:" + execReport.OrderStatus + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                            if (!Fisher.IsRunning &&
                                automationConfigModel.SizeUpOnClosingLoop &&
                                automationConfigModel.LoopSizeupType == LoopSizeupType.Static &&
                                automationConfigModel.LoopCountBeforeSizeup <= LoopIterationCounter &&
                                automationConfigModel.LoopSizeupQty > Lcd)
                            {
                                int prevQty = Lcd;
                                int interval = Looper.LoopCloseInterval;
                                double edge = Looper.ClosingEdge;
                                double lastFillPx = Looper.GetLastFillPx();
                                double slooperMinEdge = GetLoopMinEdge();
                                double fillPx = IsSingleLeg
                                    ? lastFillPx
                                    : -lastFillPx;
                                double increment = IsSingleLegSell
                                    ? Looper.GetLoopIncrement(fillPx, IncrementDirection.Down, Width)
                                    : Looper.GetLoopIncrement(fillPx, IncrementDirection.Up, Width);

                                Reverse();

                                PrevQty = prevQty;
                                ResetSize = true;
                                UpdateQty(automationConfigModel.LoopSizeupQty - prevQty);

                                Fisher.StartFisher(basePrice: fillPx,
                                    underlyingAtBase: LastFillUnderPx,
                                    qty: automationConfigModel.LoopSizeupQty,
                                    fishEdge: edge,
                                    fishMaxLoss: -slooperMinEdge,
                                    priceIncrement: increment,
                                    interval: interval,
                                    manual: true,
                                    type: OrderSubType.Slooper);

                                throw new SlimException(OrderSubType.Slooper + " Started");
                            }

                            bool closerStarted = true;
                            if (CloseStyle == Enums.CloseStyle.OutOfMarketLoop)
                            {
                                LegOutCloser?.ClosePosition(Lcd);
                            }
                            else if (CloseStyle == Enums.CloseStyle.SweepTrade)
                            {
                                SweepCloser?.Initiate(Lcd, LastFillPx);
                            }
                            else if (BasketSettings.LegInEnabled && IsSingleLeg)
                            {
                                var legInHandler = LegInHandler;
                                legInHandler.LockPrices(AveragePrice, LastMainUnderMidAtFill, filledQty);
                                legInHandler.Start();
                            }
                            else
                            {
                                closerStarted = await Looper.StartClosingLoop(receiveTime);
                            }

                            if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                            {
                                BasketTraderViewModel?.CreateComplexOrderTicket(this);
                            }
                        }
                        else if (BasketTraderViewModel.BasketType == BasketType.LockTrader && automationConfigModel.LockTraderResubmitOnFillEnabled)
                        {
                            if (_lockTraderResubmitCount < automationConfigModel.LockTraderResubmitOnFillMaxCount)
                            {
                                _lockTraderResubmitCount++;
                                if (automationConfigModel.LockTraderResetQtyOnResubmit)
                                {
                                    UpdateQty(1);
                                }

                                await SubmitOrder();
                            }
                        }
                        else
                        {
                            _log.Info("Starting auto close. Spread: " + SpreadId + ", OrderId:" + orderUpdateValues.OriginalOrderId + ", Status:" + execReport.OrderStatus + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                            int qty = Math.Abs(execReport.CumQty);
                            double averagePrice = orderUpdateValues.AveragePrice;
                            double edge = automationConfigModel.ContraFishEdge;
                            double maxLoss = automationConfigModel.LoopMaxLoss;
                            double increment = automationConfigModel.ContraFishPriceIncrement;
                            int interval = automationConfigModel.ContraFishInterval;
                            _ = Task.Run(async () =>
                            {
                                var started = false;
                                if (automationConfigModel.ClosingMode == ClosingTypes.ThreeWay)
                                {
                                    var generated = await PermCloser.GeneratePermsAsync(automationConfigModel.AttemptRegularCloseIn3Way,
                                        automationConfigModel.ThreeWayCloseMaxSpacing,
                                        automationConfigModel.ThreeWayCloseMaxPerms);
                                    if (generated)
                                    {
                                        started = await PermCloser.StartAsync(qty: qty,
                                            fillPx: averagePrice,
                                            edge: edge,
                                            maxLoss: maxLoss,
                                            increment: increment,
                                            secondaryIncrement: (double)GetPriceIncrement(),
                                            secondaryMaxResubmit: automationConfigModel.ThreeWayVerticalResubmit,
                                            interval: interval,
                                            useRawHwTheo: !automationConfigModel.UseMatchingHwTheosForPricing3WayVerticals);
                                    }
                                }
                                if (!started)
                                {
                                    if (automationConfigModel.ClosingMode == ClosingTypes.CxlReplace)
                                    {
                                        CxlReplaceCloser.StartCloser(lastFillPx: averagePrice,
                                            qty: qty,
                                            closingEdge: edge,
                                            closeMaxLoss: maxLoss,
                                            priceIncrement: increment,
                                            closeInterval: interval);
                                    }
                                    else if (automationConfigModel.ClosingMode == ClosingTypes.CxlResubmit)
                                    {
                                        Closer.StartCloser(lastFillPx: averagePrice,
                                            qty: qty,
                                            closingEdge: edge,
                                            closeMaxLoss: maxLoss,
                                            priceIncrement: increment,
                                            closeInterval: interval);
                                    }
                                }
                            });
                        }
                    }
                }
                else if (isContraOrder)
                {
                    LastContraFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastContraFillAdjTheo = NetDeltaAdjTheo;

                    if (automationConfigModel.LoopingEnabled &&
                        automationConfigModel.LooperDynamicRouting &&
                        automationConfigModel.ExchToRouteMap != null &&
                        execReport.LastExchange != null &&
                        automationConfigModel.ExchToRouteMap.TryGetValue(execReport.LastExchange, out var routeMap))
                    {
                        LastLoopContraRoute = routeMap;
                    }
                    else
                    {
                        LastLoopContraRoute = null;
                    }

                    if (ContraOrderStatus == orderUpdateValues.OrderStatus)
                    {
                        LastEdge = double.NaN;
                        DeltaAdjLastEdge = double.NaN;
                        IsLooping = false;
                        ResetLoopSize();
                        _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                    }
                    else
                    {
                        if (automationConfigModel.LoopingEnabled)
                        {
                            if (ContraPartiallyFilled)
                            {
                                if (!ResetSize)
                                {
                                    ResetSize = true;
                                    PrevQty = Lcd;
                                }
                                UpdateQty(ContraCumulativeQty + filledQty);
                                ContraCumulativeQty = 0;
                                ContraLeavesQty = 0;
                                ContraPartiallyFilled = false;
                            }

                            SetLastEdge();
                            double lastAdjEdgeBeforeFees = DeltaAdjLastEdge;
                            GetFeesForBothSide(out var openingFee, out var closingFee);
                            double fees = openingFee + closingFee;
                            double lastAdjEdgeAfterFees = lastAdjEdgeBeforeFees - fees;

                            LoopIterationCounterAfterSizeup++;
                            if (LoopIterationCounter++ >= automationConfigModel.MaxLoopCount)
                            {
                                int size = Lcd;
                                ResetLoopSize();
                                CheckToEnableNag(size);

                                LastEdge = double.NaN;
                                DeltaAdjLastEdge = double.NaN;
                                IsLooping = false;
                                Looper.RemoveFromLoopInstances();
                                _log.Info($"{nameof(HandleExecutionReport)} Max Loop Iteration Count reached. " +
                                          $"Id: {SpreadId}, " +
                                          $"Loop Iteration Counter: {LoopIterationCounter}, " +
                                          $"Max Loop Count: {automationConfigModel.MaxLoopCount}, " +
                                          $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                                          $"Last Exch: {LastExchange}, " +
                                          $"Exchanges: {Exchanges}, " +
                                          $"Last Contra Exch: {LastContraExchange}");
                            }
                            else
                            {
                                double loopMinEdge = GetLoopMinEdge();
                                string lastExchange = execReport.LastExchange;

                                await CheckForLoopAsync(receiveTime, openingFee, closingFee, loopMinEdge, lastExchange);
                            }

                            _ = Task.Run(() => EdgeAcquiredEvent?.Invoke(this, lastAdjEdgeBeforeFees, lastAdjEdgeAfterFees));
                        }
                        else if (automationConfigModel.ClosingMode == ClosingTypes.ThreeWay && CanCreateThreeWay() && !ThreeWayComplete)
                        {
                            SubmitClosingOrderAsync(orderUpdateValues.AveragePriceAfterFees, automationConfigModel.LoopMaxLoss, automationConfigModel.ContraFishEdge, automationConfigModel.ClosingMode);
                        }
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled &&
                     PartiallyFilled &&
                     (automationConfigModel.AutoLegEnabled ||
                      automationConfigModel.GoFishAutoCloseEnabled) &&
                     isMainOrder)
            {
                if (MainOrderStatus == orderUpdateValues.OrderStatus)
                {
                    _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                }
                else
                {
                    if (automationConfigModel.AutoLegEnabled)
                    {
                        var qty = CumulativeQty;
                        CumulativeQty = 0;
                        PartiallyFilled = false;
                        LeavesQty = 0;

                        _log.Info("Starting auto-leg closer. Spread: " + SpreadId + ", OrderId:" + orderUpdateValues.OriginalOrderId + ", Status:" + execReport.OrderStatus + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        bool closerStarted = AutoLegCloser!.ClosePosition(qty);

                        if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                        {
                            BasketTraderViewModel?.CreateComplexOrderTicket(this);
                        }
                    }
                    else if (automationConfigModel.LoopingEnabled)
                    {
                        double fillPercent = (double)CumulativeQty / Lcd;
                        bool closerStarted = true;
                        if (!IsLooping ||
                            fillPercent >= automationConfigModel.AutomationRequiredPartialFillPercentage ||
                            Looper.ResubmitCounter >= automationConfigModel.AutomationPartialResubmitCount)
                        {
                            if (!ResetSize)
                            {
                                ResetSize = true;
                                PrevQty = Lcd;
                            }
                            UpdateQty(CumulativeQty);
                            CumulativeQty = 0;
                            PartiallyFilled = false;
                            WasPartiallyFilled = true;
                            LeavesQty = 0;

                            if (CloseStyle == Enums.CloseStyle.OutOfMarketLoop)
                            {
                                LegOutCloser?.ClosePosition(Lcd);
                            }
                            else if (CloseStyle == Enums.CloseStyle.SweepTrade)
                            {
                                SweepCloser?.Initiate(Lcd, LastFillPx);
                            }
                            else if (BasketSettings.LegInEnabled && IsSingleLeg)
                            {
                                var legInHandler = LegInHandler;
                                legInHandler.LockPrices(AveragePrice, LastMainUnderMidAtFill, Lcd);
                                closerStarted = await legInHandler.Start();
                            }
                            else
                            {
                                closerStarted = await Looper.StartClosingLoop(receiveTime);
                            }
                            if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                            {
                                BasketTraderViewModel.CreateComplexOrderTicket(this);
                            }
                        }
                        else
                        {
                            if (!ResetSize)
                            {
                                ResetSize = true;
                                PrevQty = Lcd;
                            }
                            UpdateQty(LeavesQty);
                            MainNotFilled = true;
                            bool contd = await Looper.ContLoopAsync(receiveTime);
                            if (!contd)
                            {
                                UpdateQty(CumulativeQty);
                                CumulativeQty = 0;
                                PartiallyFilled = false;
                                LeavesQty = 0;

                                if (CloseStyle == Enums.CloseStyle.OutOfMarketLoop)
                                {
                                    LegOutCloser?.ClosePosition(Lcd);
                                }
                                else if (CloseStyle == Enums.CloseStyle.SweepTrade)
                                {
                                    SweepCloser?.Initiate(Lcd, LastFillPx);
                                }
                                else if (BasketSettings.LegInEnabled && IsSingleLeg)
                                {
                                    var legInHandler = LegInHandler;
                                    legInHandler.LockPrices(AveragePrice, LastMainUnderMidAtFill, Lcd);
                                    closerStarted = await legInHandler.Start();
                                }
                                else
                                {
                                    closerStarted = await Looper.StartClosingLoop(receiveTime);
                                }
                                if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                                {
                                    BasketTraderViewModel.CreateComplexOrderTicket(this);
                                }
                            }
                        }
                    }
                    else
                    {
                        int qty = CumulativeQty;
                        double averagePrice = orderUpdateValues.AveragePrice;
                        double edge = automationConfigModel.ContraFishEdge;
                        double maxLoss = automationConfigModel.LoopMaxLoss;
                        double increment = automationConfigModel.ContraFishPriceIncrement;
                        int interval = automationConfigModel.ContraFishInterval;
                        _ = Task.Run(async () =>
                        {
                            var started = false;
                            if (automationConfigModel.ClosingMode == ClosingTypes.ThreeWay)
                            {
                                var generated = await PermCloser.GeneratePermsAsync(automationConfigModel.AttemptRegularCloseIn3Way,
                                    automationConfigModel.ThreeWayCloseMaxSpacing,
                                    automationConfigModel.ThreeWayCloseMaxPerms);
                                if (generated)
                                {
                                    started = await PermCloser.StartAsync(qty: qty,
                                        fillPx: averagePrice,
                                        edge: edge,
                                        maxLoss: maxLoss,
                                        increment: increment,
                                        secondaryIncrement: (double)GetPriceIncrement(),
                                        secondaryMaxResubmit: automationConfigModel.ThreeWayVerticalResubmit,
                                        interval: interval,
                                        useRawHwTheo: !automationConfigModel.UseMatchingHwTheosForPricing3WayVerticals);
                                }
                            }
                            if (!started)
                            {
                                if (automationConfigModel.ClosingMode == ClosingTypes.CxlReplace)
                                {
                                    CxlReplaceCloser.StartCloser(lastFillPx: averagePrice,
                                        qty: qty,
                                        closingEdge: edge,
                                        closeMaxLoss: maxLoss,
                                        priceIncrement: increment,
                                        closeInterval: interval);
                                }
                                else if (automationConfigModel.ClosingMode == ClosingTypes.CxlResubmit)
                                {
                                    Closer.StartCloser(lastFillPx: averagePrice,
                                        qty: qty,
                                        closingEdge: edge,
                                        closeMaxLoss: maxLoss,
                                        priceIncrement: increment,
                                        closeInterval: interval);
                                }
                            }
                        });
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled &&
                     ContraPartiallyFilled &&
                     automationConfigModel.GoFishAutoCloseEnabled &&
                     isContraOrder)
            {
                if (ContraOrderStatus == orderUpdateValues.OrderStatus)
                {
                    _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                }
                else
                {
                    if (automationConfigModel.LoopingEnabled)
                    {
                        if (!ResetSize)
                        {
                            ResetSize = true;
                            PrevQty = Lcd;
                        }
                        UpdateQty(ContraLeavesQty);
                        ContraNotFilled = true;
                        bool closerStarted = await Looper.ContClose(receiveTime);
                        if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                        {
                            BasketTraderViewModel.CreateComplexOrderTicket(this);
                        }
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled)
            {
                if (automationConfigModel.LoopingEnabled && !IsLooping)
                {
                    var curSize = Lcd;
                    var preSize = Looper.LoopResubmitWithPrevSize == ResubmitSizeOption.OneLot ? 1 : Math.Max(1, PrevQty);
                    if (IsFreeLooking && isMainOrder)
                    {
                        double loopMinEdge = GetLoopMinEdge();
                        _log.Info(nameof(HandleExecutionReport) + " Freelook resubmit." +
                                  " Id: " + SpreadId + "," +
                                  " Last Edge: " + LastEdge + "," +
                                  " Loop Min Edge: " + loopMinEdge + "," +
                                  " Last Fill Px: " + LastFillPx + "," +
                                  " Last Contra Fill Px: " + LastContraFillPx + "," +
                                  " Latency Timer: " + _latencyTimer.ElapsedMilliseconds + "," +
                                  " Last Exch: " + LastExchange + "," +
                                  " Exchanges: " + Exchanges + "," +
                                  " Last Contra Exch: " + LastContraExchange);
                        if (!Looper.SizeUpLocked)
                        {
                            Looper.LoopResubmitWithPrevSize = await CheckLoopSizeUpAsync(loopMinEdge, savePrevSize: true, allowReverse: true);
                        }
                        Looper.StartLoop(receiveTime, isRecon: true);
                    }
                    else if (Looper.LoopResubmitWithPrevSize != ResubmitSizeOption.Off &&
                             curSize > preSize &&
                             isMainOrder &&
                             automationConfigModel.LoopFreeLook)
                    {
                        _log.Info(nameof(HandleExecutionReport) + " Freelook resubmit with no size." +
                                  " Id: " + SpreadId + "," +
                                  " Last Edge: " + LastEdge + "," +
                                  " Last Fill Px: " + LastFillPx + "," +
                                  " Last Contra Fill Px: " + LastContraFillPx + "," +
                                  " Latency Timer: " + _latencyTimer.ElapsedMilliseconds + "," +
                                  " Last Exch: " + LastExchange + "," +
                                  " Exchanges: " + Exchanges + "," +
                                  " Last Contra Exch: " + LastContraExchange);
                        UpdateQty(preSize);
                        IsFreeLooking = true;
                        Looper.LoopResubmitWithPrevSize = ResubmitSizeOption.Off;
                        Looper.StartLoop(receiveTime, isRecon: true);
                    }
                    else
                    {
                        int size = Lcd;
                        ResetLoopSize();
                        if (isMainOrder && _resubmitWhenReceivingCancelStatus && BasketSettings.ResubmitAfterCancel && ++ResubmitCount <= OmsCore.Config.AutoCancelMaxResubmit)
                        {
                            Resubmit();
                        }
                        else if (isMainOrder && ++ResubmitCount <= TotalResubmitCount)
                        {
                            if (ResubmitWithRegularRoute)
                            {
                                RouteOverride = null;
                                ResubmitWithRegularRoute = false;
                            }

                            _ = Task.Run(() => SubmitOrderAsync(isContra: false, resting: false));
                            _log.Info("Basket Resubmit Order. Total Resubmit: " + TotalResubmitCount + ", Resubmit Count: " + ResubmitCount);
                        }
                        else
                        {
                            CheckToEnableNag(size);
                            NotifyOrderCloseWaitHandlers(true, null);
                        }
                    }
                }
                else if (automationConfigModel.LoopingEnabled && IsLooping)
                {
                    if (isMainOrder)
                    {
                        await Looper.ContLoopAsync(receiveTime);
                    }
                    else if (isContraOrder)
                    {
                        bool closerStarted = await Looper.ContClose(receiveTime);
                        if (!closerStarted && BasketSettings.OpenTicketForFailedClose && !BasketSettings.OpenTicketForFills)
                        {
                            BasketTraderViewModel.CreateComplexOrderTicket(this);
                        }
                    }
                }
                else if (isMainOrder && _resubmitWhenReceivingCancelStatus && BasketSettings.ResubmitAfterCancel && ++ResubmitCount <= OmsCore.Config.AutoCancelMaxResubmit)
                {
                    Resubmit();
                }
                else if (isMainOrder && ++ResubmitCount <= TotalResubmitCount)
                {
                    if (CloseStyle == Enums.CloseStyle.SweepTrade)
                    {
                        _ = Task.Run(() => JoinSweep(TotalResubmitCount));
                    }
                    else
                    {
                        _ = Task.Run(() => SubmitOrderAsync(isContra: false, resting: false));
                    }
                    _log.Info("Basket Resubmit Order. Total Resubmit: " + TotalResubmitCount + ", Resubmit Count: " + ResubmitCount);
                }
            }
            else if (execReport.ExecutionType == ExecutionType.PartiallyFilled || execReport.ExecutionType == ExecutionType.Trade)
            {
                int cumulativeQty = Math.Abs(execReport.CumQty);
                int leavesQty = Math.Abs(execReport.LeavesQty);
                if (isMainOrder)
                {
                    IsFreeLooking = false;
                    LastFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastFillAdjTheo = NetDeltaAdjTheo;

                    if (automationConfigModel.LoopingEnabled &&
                        automationConfigModel.LooperDynamicRouting &&
                        automationConfigModel.ExchToRouteMap != null &&
                        execReport.LastExchange != null &&
                        automationConfigModel.ExchToRouteMap.TryGetValue(execReport.LastExchange, out var routeMap))
                    {
                        LastLoopRoute = routeMap;
                    }
                    else
                    {
                        LastLoopRoute = null;
                    }

                    PartiallyFilled = true;
                    CumulativeQty += filledQty;
                    LeavesQty = leavesQty;
                    double fillPercent = (double)CumulativeQty / Lcd;
                    _log.Info("Basket partial fill received. [Open] " +
                              "Loop enabled: " + automationConfigModel.LoopingEnabled + ", " +
                              "Spread ID: " + SpreadId + ", " +
                              "Last fill px: " + LastFillPx + ", " +
                              "Last fill qty: " + filledQty + ", " +
                              "Last order cumulative qty: " + cumulativeQty + ", " +
                              "Leaves qty: " + leavesQty + ", " +
                              "Latency Timer: " + _latencyTimer.ElapsedMilliseconds + ", " +
                              "Total cumulative: " + CumulativeQty + ", " +
                              "Filled percent: " + fillPercent + ".");
                    if (automationConfigModel.LoopingEnabled)
                    {
                        if (fillPercent >= automationConfigModel.AutomationRequiredPartialFillPercentage)
                        {
                            if (orderUpdateValues.OrderStatus != OrderStatus.PendingCancel)
                            {
                                CancelMain();
                                _log.Info("Auto cancel triggered by partial fill." +
                                          ", Spread: " + SpreadId +
                                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                            }
                            else
                            {
                                _log.Info("Auto cancel triggered by partial fill order already pending cancel." +
                                          ", Spread: " + SpreadId +
                                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                            }
                        }
                    }
                }
                else if (isContraOrder)
                {
                    LastContraFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastContraFillAdjTheo = NetDeltaAdjTheo;

                    if (automationConfigModel.LoopingEnabled &&
                        automationConfigModel.LooperDynamicRouting &&
                        automationConfigModel.ExchToRouteMap != null &&
                        execReport.LastExchange != null &&
                        automationConfigModel.ExchToRouteMap.TryGetValue(execReport.LastExchange, out var routeMap))
                    {
                        LastLoopContraRoute = routeMap;
                    }
                    else
                    {
                        LastLoopContraRoute = null;
                    }

                    ContraPartiallyFilled = true;
                    ContraCumulativeQty += filledQty;
                    ContraLeavesQty = leavesQty;
                    double fillPercent = (double)ContraCumulativeQty / Lcd;
                    _log.Info("Basket partial fill received. [Close] " +
                              "Loop enabled: " + automationConfigModel.LoopingEnabled + ", " +
                              "Spread ID: " + SpreadId + ", " +
                              "Last fill px: " + LastContraFillPx + ", " +
                              "Last fill qty: " + filledQty + ", " +
                              "Last order cumulative qty: " + cumulativeQty + ", " +
                              "Leaves qty: " + leavesQty + ", " +
                              "Total cumulative: " + ContraCumulativeQty + ", " +
                              "Latency Timer: " + _latencyTimer.ElapsedMilliseconds + ", " +
                              "Filled percent: " + fillPercent + ".");
                }
            }
        }

        public override async Task CheckForLegOutLoopAsync(double avgClosingPrice, DateTime receiveTime)
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (automationConfigModel.LoopingEnabled)
            {
                double lastFillPx = LastFillPx;
                SetLastEdge(lastFillPx, avgClosingPrice, lastFillPx, avgClosingPrice);
                double lastAdjEdgeBeforeFees = DeltaAdjLastEdge;
                GetFeesForBothSide(out var openingFee, out var closingFee);
                double fees = openingFee + closingFee;
                double lastAdjEdgeAfterFees = lastAdjEdgeBeforeFees - fees;

                LoopIterationCounterAfterSizeup++;
                if (LoopIterationCounter++ >= automationConfigModel.MaxLoopCount)
                {
                    int size = Lcd;
                    ResetLoopSize();
                    CheckToEnableNag(size);

                    LastEdge = double.NaN;
                    DeltaAdjLastEdge = double.NaN;
                    IsLooping = false;
                    Looper.RemoveFromLoopInstances();
                    _log.Info($"{nameof(CheckForLegOutLoopAsync)} Max Loop Iteration Count reached. " +
                        $"Id: {SpreadId}, " +
                        $"Loop Iteration Counter: {LoopIterationCounter}, " +
                        $"Max Loop Count: {automationConfigModel.MaxLoopCount}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");
                }
                else
                {
                    LastContraFillPx = avgClosingPrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastContraFillAdjTheo = NetDeltaAdjTheo;

                    double loopMinEdge = GetLoopMinEdge();

                    await CheckForLoopAsync(receiveTime, openingFee, closingFee, loopMinEdge, null);
                }

                _ = Task.Run(() => EdgeAcquiredEvent?.Invoke(this, lastAdjEdgeBeforeFees, lastAdjEdgeAfterFees));
            }
        }

        [Command]
        public async Task CloseSpecifiedPositionsAsyncCommand()
        {
            try
            {
                if (!double.IsNaN(Mid) && !IsLooping)
                {
                    int spreadPosition = _spreadPosition;

                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition < 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition > 0))
                    {
                        Fisher.StartFisher(basePrice: Mid,
                                           underlyingAtBase: UnderMid,
                                           qty: Math.Min(Math.Abs(CloseSpecificQty), Math.Abs(spreadPosition)),
                                           fishEdge: CloseSpecificEdgeToMid,
                                           fishMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseButtonPxIncrement,
                                           interval: OmsCore.Config.CloseButtonInterval,
                                           manual: true);
                    }
                    else if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition < 0))
                    {
                        Closer.StartCloser(lastFillPx: Mid,
                                           qty: Math.Min(Math.Abs(CloseSpecificQty), Math.Abs(spreadPosition)),
                                           closingEdge: CloseSpecificEdgeToMid,
                                           closeMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseButtonPxIncrement,
                                           closeInterval: OmsCore.Config.CloseButtonInterval,
                                           manualClose: true);
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseSpecifiedPositionsAsyncCommand));
                ShowMessage(ex.Message, "Close Positions");
            }
        }

        [Command]
        public override async Task<bool> SubmitAsync(string args = null)
        {
            if (IsActive || IsDisposed)
            {
                return false;
            }

            BasketSettings basketSettings = BasketSettings;
            if (basketSettings == null)
            {
                return false;
            }

            if (basketSettings.InitQtyEnabled)
            {
                UpdateQty(basketSettings.InitQty);
            }

            return await SubmitOrder(!string.IsNullOrWhiteSpace(args));
        }

        public override void ShowMessage(string message, string title, bool canBeSilenced = true)
        {
            Task.Run(() => MessageTriggeredEvent?.Invoke(message, title, canBeSilenced));
            _log.Info("Message: " + message + ", Title: " + title);
        }

        public override async Task<bool> GetVerificationAsync(string message, string title)
        {
            return await BasketTraderViewModel.GetVerificationAsync(message, title);
        }

        public override RiskWarningMessageResponse GetRiskVerification(string message, string title)
        {
            RiskWarningMessageResponse response = RiskWarningMessageResponse.CancelAll;
            Dispatcher?.Invoke(() =>
            {
                response = BasketTraderViewModel.VerificationService.GetRiskVerification(message, title, showCancelAll: true);
                if (response == RiskWarningMessageResponse.CancelAll)
                {
                    _log.Info("Cancelling all orders after prompt");
                    BasketTraderViewModel.CancelQueuedSubmitWithDelay();
                }
            });
            return response;
        }

        internal async Task LoadFromOrder(OmsOrder orderModel)
        {
            Underlying = orderModel.UnderlyingSymbol;
            EdgeOverride = orderModel.EdgeOverride;
            AdjustedEdgeOverride = orderModel.AdjustedEdgeOverride;
            EdgeCurveAdjustment = orderModel.EdgeCurveAdjustment;
            SetDefaultValues();
            SetPrice(orderModel.Price);

            var loadTasks = new List<Task>(orderModel.Legs.Count);
            if (orderModel.Legs.Count > 0)
            {
                foreach (OmsOrderLeg leg in orderModel.Legs)
                {
                    Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                    string type = leg.Symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
                    var side = leg.Side;
                    TicketLegModel newLeg = new(OmsCore, Underlying, Account, BasketTraderViewModel, BasketTraderViewModel.PortfolioManagerModel)
                    {
                        Side = side,
                        ContraSide = side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        Ratio = leg.Ratio,
                        Quantity = leg.Ratio, // Set quantity to ratio to set the min value 
                        Type = type,
                        Position = Positions.AUTO.ToString(),
                        ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol)
                    };
                    newLeg.ExpirationsList.Add(newLeg.ExpirationInfo);
                    newLeg.Strike = new StrikeInfoModel(option.Strike);
                    newLeg.StrikesList.Add(newLeg.Strike);
                    newLeg.LegUpdatedEvent -= UpdateTicketValues;
                    newLeg.LegUpdatedEvent += UpdateTicketValues;
                    Legs.Add(newLeg);
                    var task = newLeg.ValidateLegAsync(true);
                    loadTasks.Add(task);
                }
            }
            else
            {
                var codec = new SymbolLib.SymbolCodec(orderModel.Symbol);
                for (int i = 0; i < codec.LegCount; i++)
                {
                    var leg = codec.GetLeg(i);
                    Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.symbol);
                    string type = leg.symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";

                    if (string.IsNullOrEmpty(Underlying))
                    {
                        Underlying = option?.UnderlyingSymbol;
                    }

                    var side = codec.LegCount == 1 && orderModel.Side.HasValue ? orderModel.Side : leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                    if (option != null)
                    {
                        TicketLegModel newLeg = new(OmsCore, Underlying, Account, BasketTraderViewModel, BasketTraderViewModel.PortfolioManagerModel)
                        {
                            Side = side,
                            ContraSide = side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                            Ratio = leg.ratio,
                            Quantity = leg.ratio, // Set quantity to ratio to set the min value 
                            Type = type,
                            Position = Positions.AUTO.ToString(),
                            ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol)
                        };
                        newLeg.ExpirationsList.Add(newLeg.ExpirationInfo);
                        newLeg.Strike = new StrikeInfoModel(option.Strike);
                        newLeg.StrikesList.Add(newLeg.Strike);
                        newLeg.LegUpdatedEvent -= UpdateTicketValues;
                        newLeg.LegUpdatedEvent += UpdateTicketValues;
                        Legs.Add(newLeg);
                        var task = newLeg.ValidateLegAsync(true);
                        loadTasks.Add(task);
                    }
                }
            }

            await Task.WhenAll(loadTasks);

            UpdateLCD();
            RatioLocked = true;
            SubscribeDataAsync();
            UpdateDescription();
            SetBestRoute();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal override async Task LoadFromTicketAsync(OrderTicket orderTicket, bool flipCP = false, bool copyPosEffect = false, bool forContra = false)
        {
            Underlying = orderTicket.Underlying;
            LoadDefaultAccount();
            TimeInForce = orderTicket.TimeInForce;

            foreach (TicketLegModel leg in orderTicket.Legs)
            {
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                    Type = !flipCP ? leg.Type : leg.Type == Types.CALL.ToString() ? Types.PUT.ToString() : leg.Type == Types.PUT.ToString() ? Types.CALL.ToString() : leg.Type,
                    Position = copyPosEffect ? leg.Position : Positions.AUTO.ToString()
                };

                ExpirationInfoModel expirationInfoModel = leg.ExpirationInfo?.Clone();
                if (expirationInfoModel != null)
                {
                    legClone.ExpirationsList.Add(expirationInfoModel);
                }
                legClone.ExpirationInfo = expirationInfoModel;
                legClone.StrikesList.Add(leg.Strike);
                legClone.Strike = leg.Strike;

                Legs.Add(legClone);
                await legClone.ValidateLegAsync(true);
            }

            await UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            bool reversed = UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();

            LoadOrderStateFromTicket(orderTicket, reversed);
        }

        internal async Task LoadPermFromTicketAsync(OrderTicket orderTicketViewModel, DateTime expiration)
        {
            Underlying = orderTicketViewModel.Underlying;
            LoadDefaultAccount();
            TimeInForce = orderTicketViewModel.TimeInForce;

            List<Data.Securities.Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
            List<double> strikes = OmsCore.QuoteClient.OptionsLookup.GetOptionsWithExpiration(Underlying, expiration).Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();

            foreach (TicketLegModel leg in orderTicketViewModel.Legs)
            {
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                    Type = leg.Type,
                    Position = Positions.AUTO.ToString()
                };

                Data.Securities.Option legOption = options.FirstOrDefault(x => x.Expiration.Date == expiration.Date && x.RootSymbol == leg.ExpirationInfo.RootSymbol);
                if (legOption != null)
                {
                    ExpirationInfoModel expModel = new(legOption.Expiration, leg.ExpirationInfo.RootSymbol);
                    legClone.ExpirationInfo = expModel;
                    legClone.ExpirationsList.Add(expModel);
                    legClone.Strike = new StrikeInfoModel(false, strikes.MinBy(x => Math.Abs(x - leg.Strike.Strike)));
                    legClone.StrikesList.Add(legClone.Strike);

                    legClone.UpdateStrikeVisibility();
                }

                Legs.Add(legClone);
                await legClone.ValidateLegAsync(true);
            }

            await UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal async Task LoadPermFromTicketAsync(OrderTicket orderTicketViewModel, DateTime expiration, double strike)
        {
            Underlying = orderTicketViewModel.Underlying;
            LoadDefaultAccount();
            TimeInForce = orderTicketViewModel.TimeInForce;

            List<Data.Securities.Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
            List<double> strikes = OmsCore.QuoteClient.OptionsLookup.GetOptionsWithExpiration(Underlying, expiration).Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();

            foreach (TicketLegModel leg in orderTicketViewModel.Legs)
            {
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                    Type = leg.Type,
                    Position = Positions.AUTO.ToString()
                };

                Data.Securities.Option legOption = options.FirstOrDefault(x => x.Expiration.Date == expiration.Date && x.RootSymbol == leg.ExpirationInfo.RootSymbol);
                if (legOption != null)
                {
                    ExpirationInfoModel expModel = new(legOption.Expiration, leg.ExpirationInfo.RootSymbol);
                    legClone.ExpirationInfo = expModel;
                    legClone.ExpirationsList.Add(expModel);
                    legClone.Strike = new StrikeInfoModel(false, strikes.MinBy(x => Math.Abs(x - strike)));
                    legClone.StrikesList.Add(legClone.Strike);

                    legClone.UpdateStrikeVisibility();
                }

                Legs.Add(legClone);
                await legClone.ValidateLegAsync(true);
            }

            await UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal void ClearEdgeOverride()
        {
            AdjustedEdgeOverride = double.NaN;
        }

        internal void LoadEdgeOverride()
        {
        }

        internal void CorrectIfDuplicateLastPx()
        {
            if (Math.Abs(Price - LastPx) < .01)
            {
                Price -= Math.Abs(decimal.ToDouble(PriceIncrement));
            }
        }

        internal void Resubscribe(string source)
        {
            UnsubscribeData(source);
            SubscribeToData(source);
        }

        internal void SubscribeToData(string source)
        {
            if (isDisposing || IsDisposed) return;
            try
            {
                if (source?.ToUpper() == "UNDERLYING")
                {
                    SubscribeToUnderlying();
                }
                else if (source?.ToUpper() == "HEDGE")
                {
                    SubscribeToHedgeUnderlying();
                }
                else if (source?.ToUpper() == "GLOBALEDGETOTHEO")
                {
                    SubscribeToGlobalEdgeToTheo();
                }
                else if (source?.ToUpper() == "FIRMSUMMARY")
                {
                    SubscribeToFirmOrderAndTradeSummary();
                }
                else
                {
                    SubscribeToLegField(source);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeToData));
                UnsubscribeData(source);
            }
        }

        internal void UnsubscribeData(string source)
        {
            try
            {
                if (source?.ToUpper() == "UNDERLYING")
                {
                    UnsubscribeUnderlying();
                }
                else if (source?.ToUpper() == "HEDGE")
                {
                    UnsubscribeFromHedgeUnderlying();
                }
                else if (source?.ToUpper() == "GLOBALEDGETOTHEO")
                {
                    UnsubscribeFromGlobalEdgeToTheo();
                }
                else if (source?.ToUpper() == "FIRMSUMMARY")
                {
                    UnsubscribeFirmOrderAndTradeSummary();
                }
                else
                {
                    UnsubscribeFromLegField(source);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeData));
            }
        }

        internal bool ContainsCheapo()
        {
            return Legs.Any(x => x.IsCheapo);
        }

        internal async Task AddCheapo(int index)
        {
            PreUpdate();
            var leg = await GetCheapoLeg(index);
            if (leg != null)
            {
                Dispatcher?.Invoke(() =>
                {
                    Legs.Add(leg);
                    _ = UpdateAccountsAndRoutes();
                });
            }
            PostUpdate();
        }

        private async Task<TicketLegModel> GetCheapoLeg(int index)
        {
            if (Legs == null || Legs.Count == 0)
            {
                return null;
            }
            List<Data.Securities.Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
            var sampleLeg = Legs[0];
            string type = sampleLeg.Type?.ToUpper();
            List<Option> filtered = options.Where(x => x.Type.ToString().ToUpper() == type && (x.Expiration.Date - DateTime.Today).TotalDays >= BasketSettings.CheapoDteRangeMin && (x.Expiration.Date - DateTime.Today).TotalDays <= BasketSettings.CheapoDteRangeMax).Select(x => OmsCore.SecurityBook.GetSecurity(x.OptionSymbol)).Cast<Option>().ToList();
            Dictionary<Option, double> deltaMap = await FilterCheapoByDeltaRange(filtered);
            Dictionary<Option, double> midMap = await FilterCheapoByMarketRange(filtered);

            Option selected = filtered
                .OrderBy(x => x.Expiration)
                .ThenBy(x => deltaMap[x])
                .ThenBy(x => midMap[x])
                .ElementAtOrDefault(index);

            if (selected == null)
            {
                return default;
            }

            TicketLegModel leg = new(OmsCore, Underlying, sampleLeg.Account, BasketTraderViewModel, _portfolioManagerModel)
            {
                IsCheapo = true,
                Symbol = selected.Symbol,
                Quantity = sampleLeg.Quantity,
                Ratio = sampleLeg.Ratio,
                Side = ZeroPlus.Models.Data.Enums.Side.Buy,
            };
            ProcessLegForAdd(false, leg);
            return leg;
        }

        private async Task<Dictionary<Option, double>> FilterCheapoByDeltaRange(List<Option> filtered)
        {
            DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
            deltaStore.GetHanweckDataFor(filtered, SubscriptionFieldType.Delta);
            Dictionary<Option, double> map = new();
            try
            {
                for (var i = filtered.Count - 1; i >= 0; i--)
                {
                    var option = filtered[i];
                    var delta = await deltaStore.GetDataAsync(option.Symbol, false);
                    delta = Math.Abs(delta);
                    if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < BasketSettings.CheapoDeltaRangeMin || delta > BasketSettings.CheapoDeltaRangeMax)
                    {
                        filtered.Remove(option);
                        _log.Info($"{option.Symbol} removed from cheapo consideration. Delta: {delta}, Delta Range: [{BasketSettings.CheapoDeltaRangeMin}-{BasketSettings.CheapoDeltaRangeMax}]");
                    }
                    else
                    {
                        map[option] = delta;
                    }
                }

                return map;
            }
            finally
            {
                deltaStore.Dispose();
            }
        }

        private async Task<Dictionary<Option, double>> FilterCheapoByMarketRange(List<Option> filtered)
        {
            DataStore bidStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
            DataStore askStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
            Dictionary<Option, double> map = new();
            try
            {
                bidStore.GetQuoteDataFor(filtered, SubscriptionFieldType.Bid);
                askStore.GetQuoteDataFor(filtered, SubscriptionFieldType.Ask);
                for (var i = filtered.Count - 1; i >= 0; i--)
                {
                    var option = filtered[i];
                    var bid = await bidStore.GetDataAsync(option.Symbol, false);
                    var ask = await askStore.GetDataAsync(option.Symbol, false);
                    var mid = (bid + ask) / 2;
                    var width = ask - bid;
                    if (double.IsNaN(mid) || double.IsInfinity(mid) || mid < BasketSettings.CheapoMarketRangeMin || mid > BasketSettings.CheapoMarketRangeMax || width > BasketSettings.CheapoLegMaxWidth)
                    {
                        filtered.Remove(option);
                        _log.Info($"{option.Symbol} removed from cheapo consideration. Mkt: [{bid}X{ask}], Mkt Range: [{BasketSettings.CheapoMarketRangeMin}-{BasketSettings.CheapoMarketRangeMax}], Max Width: {BasketSettings.CheapoLegMaxWidth}");
                    }
                    else
                    {
                        map[option] = mid;
                    }
                }
                return map;
            }
            finally
            {
                bidStore.Dispose();
                askStore.Dispose();
            }
        }

        internal void RemoveCheapo()
        {
            PreUpdate();
            foreach (TicketLegModel leg in Legs.ToList())
            {
                if (leg.IsCheapo)
                {
                    RemoveLegFromOrder(leg);
                }
            }
            PostUpdate();
        }

        private List<DateTime> GetExpirations()
        {
            return Legs.Where(x => x.ExpirationInfo != null && x.ExpirationInfo.Expiration != default).Select(x => x.ExpirationInfo.Expiration).ToList();
        }

        protected override void OnLoss()
        {
            if (BasketSettings.SubmitOnTriggerEnabled && BasketSettings.DisableSubmitOnTriggerOnLoss)
            {
                BasketSettings.SubmitOnTriggerEnabled = false;
            }

            if (BasketSettings.CancelOnLoss)
            {
                BasketTraderViewModel.CancelAllNoCheck();
            }
        }

        protected override async Task<string> CheckForRiskAsync()
        {
            var skewAdjCheckResult = await IsValidEdgeToSkewAdjMarketAsync(Price);
            if (!skewAdjCheckResult.IsValid)
            {
                if (BasketSettings.AdjustAfterMinEdgeToSkewMarketCheck)
                {
                    SetPriceMinimal(skewAdjCheckResult.NewPrice);
                }
                else
                {
                    return $"Outside Skew Adj Mkt. Px: {Price:n2}, Skew Mkt: [{HighestBid:n2}X{LowestAsk:n2}]";
                }
            }

            var skewAdjMktCrossResult = await IsValidEdgeToSkewAdjMarketCrossAsync(Price, GetClosingEdge(false));
            if (!skewAdjMktCrossResult.IsValid)
            {
                if (BasketSettings.AdjustAfterMinEdgeToSkewMarketCrossCheck)
                {
                    SetPriceMinimal(skewAdjMktCrossResult.NewPrice);
                }
                else
                {
                    return $"Outside Skew Adj Mkt Cross. Px: {Price:n2}, Skew Mkt: [{HighestBid:n2}X{LowestAsk:n2}]";
                }
            }

            if (!await IsWidthBelowThresholdAsync())
            {
                return $"Outside Max Width. Width: {Width:n2}, Limit: {BasketSettings.MaxWidthCheckPx:n2}";
            }

            if (!await IsSizeAboveThresholdAsync())
            {
                return
                    $"Size Check Failed. Size: {BidSize}X{AskSize}, Limit: {BasketSettings.MinBidAskSize}X{BasketSettings.MinBidAskSize}";
            }

            if (!await IsBelowMinTheoEdge())
            {
                return "Outside Min Theo Edge.";
            }

            if (!await IsBelowMinHwTheoEdge())
            {
                return "Outside Min HW Theo Edge.";
            }

            if (!await IsBelowMinV0TheoEdge())
            {
                return "Outside Min V0 Theo Edge.";
            }

            if (!await IsNotOnTheoJump())
            {
                return "Theo Jump Detected.";
            }

            if (!await IsBelowMinEmaWidthPercentEdgeToTheo())
            {
                var width = AskEmaAdj - BidEmaAdj;
                var edgeToTheo = Math.Round(width * BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge, 2);
                Reason = $"Width: {width}, Req Edge: {edgeToTheo}";
                return "Outside Min Width % Theo Edge.";
            }

            if (!await IsBelowMinMidEdge())
            {
                return "Outside Min Mid Edge.";
            }

            if (!await IsBelowMinEmaEdge())
            {
                return "Outside Min Ema Edge.";
            }

            if (!await IsAboveBidPercent())
            {
                return "Outside Min Bid %.";
            }

            if (!await IsBelowBidPercent())
            {
                return "Outside Max Bid %.";
            }

            if (!await IsBidAboveMinBid(BasketSettings.MinBidCheckBidValue))
            {
                return $"Min Bid Check Failed. Min: {BasketSettings.MinBidCheckBidValue},  Mkt: [{Low:n2}X{High:n2}]";
            }

            if (!await IsTheoAboveMinTheo(BasketSettings.MinTheoCheckTheoValue))
            {
                return $"Min Theo Check Failed. Min: {BasketSettings.MinTheoCheckTheoValue},  Theo: [{NetDeltaAdjTheo:n2}]";
            }

            if (BasketSettings.BlockZeroPrice && Math.Abs(Price) < .01)
            {
                return $"Zero Px Found. Mkt: [{Low:n2}X{High:n2}]";
            }

            if (!await IsWidthWithinGlobalRangeAsync())
            {
                return $"Outside Global Width Range. Width: {Width}";
            }

            if (BasketSettings.RiskCheckEnabled)
            {
                return await RunOptionalRiskChecks();
            }

            return "";
        }

        private async Task<string> RunOptionalRiskChecks()
        {
            if (!IsWithinStrikeCapAsync())
            {
                return "Outside Strike Cap.";
            }

            if (!await IsWithinDeltaCapAsync())
            {
                return "Outside Delta Cap.";
            }

            if (!await IsWithinWidthCapAsync())
            {
                return "Outside Width Cap.";
            }

            if (!await IsWithinMarketCap())
            {
                return $"Outside Mkt. Px: {Price:n2}, Mkt: [{Low:n2}X{High:n2}]";
            }

            if (!await IsWidthAboveThresholdAsync())
            {
                return $"Outside Min Width. Width: {Width}, Limit: {BasketSettings.MaxWidthCheckPx}";
            }

            if (!await CheckEdgeRiskParametersAsync(preSubmit: true))
            {
                return "Outside Edge.";
            }

            return "";
        }

        protected override async Task CheckAutoCancel()
        {
            bool allBasketChecksAreDown = _riskModel.OverrideEdgeCheck &&
                                          !BasketSettings.RiskCheckEnabled &&
                                          !BasketSettings.CancelWithEdgeToTheoEnabled &&
                                          !BasketSettings.CancelWithEdgeToAdjTheoEnabled &&
                                          !BasketSettings.CancelWithEdgeToMidEnabled &&
                                          !BasketSettings.CancelWithWidthEnabled &&
                                          !BasketSettings.CancelWithUnderlyingDeltaPxEnabled &&
                                          !BasketSettings.CancelWithUnderlyingPxEnabled &&
                                          !BasketSettings.MaxWidthCheckEnabled &&
                                          !BasketSettings.MinTheoEdgeCheckEnabled &&
                                          !BasketSettings.MinHwTheoEdgeCheckEnabled &&
                                          !BasketSettings.MinV0TheoEdgeCheckEnabled &&
                                          !BasketSettings.MinMidEdgeCheckEnabled &&
                                          !BasketSettings.MinBidCheckEnabled &&
                                          !BasketSettings.MinTheoCheckEnabled &&
                                          !BasketSettings.MinPercentBidCheckEnabled &&
                                          !BasketSettings.MaxPercentBidCheckEnabled &&
                                          !BasketSettings.MaxDigPercentBidCheckEnabled &&
                                          !BasketSettings.MinBidAskSizeCheckEnabled &&
                                          !BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled &&
                                          !BasketSettings.MinEmaEdgeCheckEnabled &&
                                          !BasketSettings.CancelWithMaxSizeEnabled &&
                                          !BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled &&
                                          !BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled;

            if (allBasketChecksAreDown)
            {
                return;
            }

            var logPassed = OmsCore.Config.LogAutoCancelAndFishLossPass;
            if (!await IsWidthAboveThresholdAsync())
            {
                string message = "Auto cancel triggered by Min Width change" +
                                 ", Enabled: " + BasketSettings.CancelWithWidthEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Change: " + Width +
                                 ", Threshold: " + BasketSettings.CancelWithWidthThreshold;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Width change" +
                                 ", Enabled: " + BasketSettings.CancelWithWidthEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Change: " + Width +
                                 ", Threshold: " + BasketSettings.CancelWithWidthThreshold;
                _log.Info(message);
            }

            if (!await IsWidthBelowThresholdAsync())
            {
                string message = "Auto cancel triggered by Max Width change" +
                                 ", Enabled: " + BasketSettings.MaxWidthCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Change: " + Width +
                                 ", Threshold: " + BasketSettings.MaxWidthCheckPx;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Max Width change" +
                                 ", Enabled: " + BasketSettings.MaxWidthCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Change: " + Width +
                                 ", Threshold: " + BasketSettings.MaxWidthCheckPx;
                _log.Info(message);
            }

            if (!await IsSizeAboveThresholdAsync())
            {
                string message = "Auto cancel triggered by Min Size check" +
                                 ", Enabled: " + BasketSettings.MinBidAskSizeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Size: " + BidSize + "X" + AskSize +
                                 ", Threshold: " + BasketSettings.MinBidAskSize;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Size check" +
                                 ", Enabled: " + BasketSettings.MinBidAskSizeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Size: " + BidSize + "X" + AskSize +
                                 ", Threshold: " + BasketSettings.MinBidAskSize;
                _log.Info(message);
            }

            if (!await IsSizeBelowThresholdAsync())
            {
                string message = "Auto cancel triggered by Max Size check" +
                                 ", Enabled: " + BasketSettings.CancelWithMaxSizeEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Size: " + BidSize + "X" + AskSize +
                                 ", Threshold: " + BasketSettings.CancelWithMaxSizeLimit;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Max Size check" +
                                 ", Enabled: " + BasketSettings.CancelWithMaxSizeEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Size: " + BidSize + "X" + AskSize +
                                 ", Threshold: " + BasketSettings.CancelWithMaxSizeLimit;
                _log.Info(message);
            }

            if (!await IsBelowMinTheoEdge())
            {
                string message = "Auto cancel triggered by Min Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinTheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {NetDeltaAdjTheo: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinTheoEdgeCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinTheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {NetDeltaAdjTheo: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinTheoEdgeCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowMinHwTheoEdge())
            {
                var result = await GetTheoAsync(TheoModel.Hanw);
                var theo = result.NetTheo;
                var adjTheo = result.NetDeltaAdjTheo;
                string message = "Auto cancel triggered by Min Hw Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinHwTheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {theo: n2}" +
                                 $", Adj_Theo: {adjTheo: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinHwTheoEdgeCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Hw Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinHwTheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Threshold: " + BasketSettings.MinHwTheoEdgeCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowMinV0TheoEdge())
            {
                var result = await GetTheoAsync(TheoModel.VolaV0);
                var theo = result.NetTheo;
                var adjTheo = result.NetDeltaAdjTheo;
                string message = "Auto cancel triggered by Min V0 Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinV0TheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {theo: n2}" +
                                 $", Adj_Theo: {adjTheo: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinV0TheoEdgeCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min V0 Theo Edge check" +
                                 ", Enabled: " + BasketSettings.MinV0TheoEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 ", Threshold: " + BasketSettings.MinV0TheoEdgeCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowMinEmaWidthPercentEdgeToTheo())
            {
                var width = AskEmaAdj - BidEmaAdj;
                var edgeToTheo = Math.Round(width * BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge, 2);
                string message = "Auto cancel triggered by Min Ema Width Percent Edge To Theo" +
                                 ", Enabled: " + BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{BidEmaAdj: n2}X{AskEmaAdj: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + edgeToTheo;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                var width = AskEmaAdj - BidEmaAdj;
                var edgeToTheo = Math.Round(width * BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge, 2);
                string message = "Auto cancel not triggered by Min Ema Width Percent Edge To Theo" +
                                 ", Enabled: " + BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{BidEmaAdj: n2}X{AskEmaAdj: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + edgeToTheo;
                _log.Info(message);
            }

            if (!await IsBelowMinMidEdge())
            {
                string message = "Auto cancel triggered by Min Mid Edge check" +
                                 ", Enabled: " + BasketSettings.MinMidEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinMidEdgeCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Mid Edge check" +
                                 ", Enabled: " + BasketSettings.MinMidEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinMidEdgeCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowMinEmaEdge())
            {
                string message = "Auto cancel triggered by Min Ema Edge check" +
                                 ", Enabled: " + BasketSettings.MinEmaEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {GetEma(): n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinEmaEdgeCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Ema Edge check" +
                                 ", Enabled: " + BasketSettings.MinEmaEdgeCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {GetEma(): n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MinEmaEdgeCheckEdge;
                _log.Info(message);
            }

            if (!await IsAboveBidPercent(BasketSettings.MaxPercentBidCheckUseBestQuote))
            {
                string message = "Auto cancel triggered by Min Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MinPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Use Best Quote: " + BasketSettings.MaxPercentBidCheckUseBestQuote +
                                 ", Threshold: " + BasketSettings.MinPercentBidCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Min Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MinPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Use Best Quote: " + BasketSettings.MaxPercentBidCheckUseBestQuote +
                                 ", Threshold: " + BasketSettings.MinPercentBidCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowBidPercent(BasketSettings.MaxPercentBidCheckUseBestQuote))
            {
                string message = "Auto cancel triggered by Max Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MaxPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Use Best Quote: " + BasketSettings.MaxPercentBidCheckUseBestQuote +
                                 ", Threshold: " + BasketSettings.MaxPercentBidCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Max Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MaxPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Mkt: [{Low: n2}X{High: n2}]" +
                                 $", Px: {Price:F2}" +
                                 ", Use Best Quote: " + BasketSettings.MaxPercentBidCheckUseBestQuote +
                                 ", Threshold: " + BasketSettings.MaxPercentBidCheckEdge;
                _log.Info(message);
            }

            if (!await IsBelowDigBidPercent())
            {
                string message = "Auto cancel triggered by Max Dig Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MaxDigPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MaxDigPercentBidCheckEdge;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by Max Dig Bid Percent check" +
                                 ", Enabled: " + BasketSettings.MaxDigPercentBidCheckEnabled +
                                 ", Spread: " + SpreadId +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.MaxDigPercentBidCheckEdge;
                _log.Info(message);
            }

            if (!await IsBidAboveMinBid(BasketSettings.MinBidCheckBidValue))
            {
                string message =
                    "Auto cancel triggered by Min Bid check" +
                    ", Enabled: " + BasketSettings.MinBidCheckEnabled +
                    $", Spread: {SpreadId}" +
                    $", Mkt: [{Low: n2}X{High: n2}]" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.MinBidCheckBidValue}";
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message =
                    "Auto cancel not triggered by Min Bid check" +
                    ", Enabled: " + BasketSettings.MinBidCheckEnabled +
                    $", Spread: {SpreadId}" +
                    $", Mkt: [{Low: n2}X{High: n2}]" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.MinBidCheckBidValue}";
                _log.Info(message);
            }

            if (!await IsTheoAboveMinTheo(BasketSettings.MinTheoCheckTheoValue))
            {
                string message =
                    "Auto cancel triggered by Min Theo check" +
                    ", Enabled: " + BasketSettings.MinTheoCheckEnabled +
                    $", Spread: {SpreadId}" +
                    $", Theo: {NetDeltaAdjTheo:F2}" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.MinTheoCheckTheoValue}";
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message =
                    "Auto cancel not triggered by Min Theo check" +
                    ", Enabled: " + BasketSettings.MinTheoCheckEnabled +
                    $", Spread: {SpreadId}" +
                    $", Theo: {NetDeltaAdjTheo:F2}" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.MinTheoCheckTheoValue}";
                _log.Info(message);
            }

            if (BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled && !await IsPriceBelowAdjTheoEdge(BasketSettings.CancelWithOrderPriceEdgeToTheo, TheoModel.Hanw))
            {
                string message =
                    "Auto cancel triggered by Edge To Theo check, " +
                    ", Enabled: " + BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled +
                    $", Spread: {SpreadId}" +
                    $", Theo: {NetDeltaAdjTheo: n2}" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.CancelWithOrderPriceEdgeToTheo}";
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message =
                    "Auto cancel not triggered by Edge To Theo check, " +
                    ", Enabled: " + BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled +
                    $", Spread: {SpreadId}" +
                    $", Theo: {NetDeltaAdjTheo: n2}" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.CancelWithOrderPriceEdgeToTheo}";
                _log.Info(message);
            }

            if (BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled && !await IsPriceBelowAdjTheoEdge(BasketSettings.CancelWithOrderPriceEdgeToModelTheo, BasketSettings.AutoCancelTheoModel))
            {
                var result = await GetTheoAsync(BasketSettings.AutoCancelTheoModel);
                var theo = result.NetTheo;
                var adjTheo = result.NetDeltaAdjTheo;
                string message =
                    "Auto cancel triggered by Edge To Model Theo check, " +
                    ", Enabled: " + BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled +
                    $", Spread: {SpreadId}" +
                    $", Model: {BasketSettings.AutoCancelTheoModel}" +
                    $", Theo: {theo:F2}" +
                    $", Adj Theo: {adjTheo:F2}" +
                    $", Px: {Price:F2}" +
                    $", Threshold: {BasketSettings.CancelWithOrderPriceEdgeToModelTheo}";
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message =
                    "Auto cancel not triggered by Edge To Model Theo check, " +
                    ", Enabled: " + BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled +
                    $", Spread: {SpreadId}" +
                    $", Model: {BasketSettings.AutoCancelTheoModel}" +
                    $", Threshold: {BasketSettings.CancelWithOrderPriceEdgeToModelTheo}";
                _log.Info(message);
            }

            if (!await IsPriceValidEdgeToTheo())
            {
                string message = "Auto cancel triggered by theo change" +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {NetDeltaAdjTheo: n2}" +
                                 $", Theo: {_theoToWatchFor: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.CancelWithTheoEdge +
                                 ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by theo change" +
                                 ", Spread: " + SpreadId +
                                 $", Theo: {NetDeltaAdjTheo: n2}" +
                                 $", Theo: {_theoToWatchFor: n2}" +
                                 $", Px: {Price:F2}" +
                                 ", Threshold: " + BasketSettings.CancelWithTheoEdge +
                                 ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                _log.Info(message);
            }

            if (!await IsPriceValidEdgeToDeltaAdjTheo())
            {
                string message = "Auto cancel triggered by adj theo change" +
                                 ", Spread: " + SpreadId +
                                 ", Threshold: " + BasketSettings.CancelWithAdjTheoEdge +
                                 ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                if (CancelFromEdgeCheck(message))
                {
                    return;
                }
            }
            else if (logPassed)
            {
                string message = "Auto cancel not triggered by adj theo change" +
                                 ", Spread: " + SpreadId +
                                 ", Threshold: " + BasketSettings.CancelWithAdjTheoEdge +
                                 ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                _log.Info(message);
            }

            if (BasketSettings.CancelWithEdgeToMidEnabled && MarkLoaded)
            {
                double delta = _midToWatchFor - Mid;

                if (_midToWatchFor < 0)
                {
                    delta *= -1;
                }

                if (delta >= BasketSettings.CancelWithMidEdge)
                {
                    string message = "Auto cancel triggered by mid change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + delta +
                                     ", Mid: " + Mid +
                                     ", Mid C: " + _midToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithMidEdge +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                        return;
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by mid change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + delta +
                                     ", Mid: " + Mid +
                                     ", Mid C: " + _midToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithMidEdge +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }

            if (BasketSettings.CancelWithWidthEnabled && MarkLoaded)
            {
                if (Width < BasketSettings.CancelWithWidthThreshold)
                {
                    string message = "Auto cancel triggered by width" +
                                     ", Spread: " + SpreadId +
                                     ", Width: " + Width +
                                     ", Threshold: " + BasketSettings.CancelWithWidthThreshold +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                        return;
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by width" +
                                     ", Spread: " + SpreadId +
                                     ", Width: " + Width +
                                     ", Threshold: " + BasketSettings.CancelWithWidthThreshold +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }

            if (BasketSettings.CancelWithUnderlyingPxEnabled && !BasketSettings.UseHedgeUnderlyingForAutoCancel && UnderLoaded && NetTheoLoaded)
            {
                double priceDelta = _underToWatchFor - UnderMid;

                double price = BasketSettings.FishModeEnabled ? FishPrice : Price;

                // TODO what about when TotalDelta == 0?
                if ((price >= 0 && TotalDelta > 0) || (price < 0 && TotalDelta < 0))
                {
                    priceDelta *= -1;
                }

                if (priceDelta >= BasketSettings.CancelWithUnderlyingPx)
                {
                    string message = "Auto cancel triggered by underlying change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + priceDelta +
                                     ", Under: " + UnderMid +
                                     ", Under_W: " + _underToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithUnderlyingPx +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                        return;
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by underlying change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + priceDelta +
                                     ", Under: " + UnderMid +
                                     ", Under_W: " + _underToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithUnderlyingPx +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }

            if (BasketSettings.CancelWithUnderlyingPxEnabled && BasketSettings.UseHedgeUnderlyingForAutoCancel && HedgeUnderLoaded && NetTheoLoaded)
            {
                double priceDelta = _underToWatchFor - HedgeMid;

                double price = BasketSettings.FishModeEnabled ? FishPrice : Price;

                // TODO what about when TotalDelta == 0?
                if ((price >= 0 && TotalDelta > 0) || (price < 0 && TotalDelta < 0))
                {
                    priceDelta *= -1;
                }

                if (priceDelta >= BasketSettings.CancelWithUnderlyingPx)
                {
                    string message = "Auto cancel triggered by hedge underlying change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + priceDelta +
                                     ", Under: " + HedgeMid +
                                     ", Under_W: " + _underToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithUnderlyingPx +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                        return;
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by hedge underlying change" +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + priceDelta +
                                     ", Under: " + HedgeMid +
                                     ", Under_W: " + _underToWatchFor +
                                     ", Threshold: " + BasketSettings.CancelWithUnderlyingPx +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }

            if (BasketSettings.CancelWithUnderlyingDeltaPxEnabled && !BasketSettings.UseHedgeUnderlyingForAutoCancel && UnderLoaded && NetTheoLoaded)
            {
                double changeInUnder = Math.Abs(_lastDeltaToWatchFor - UnderMid);
                double threshold = Math.Abs(BasketSettings.CancelWithUnderlyingDeltaPx * 1 / TotalDelta);

                if (changeInUnder >= threshold)
                {
                    string message = "Auto cancel triggered by underlying delta change. " +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + changeInUnder +
                                     ", Threshold: " + threshold +
                                     ", Under: " + UnderMid +
                                     ", Under_W: " + _lastDeltaToWatchFor +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                        return;
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by underlying delta change. " +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + changeInUnder +
                                     ", Threshold: " + threshold +
                                     ", Under: " + UnderMid +
                                     ", Under_W: " + _lastDeltaToWatchFor +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }

            if (BasketSettings.CancelWithUnderlyingDeltaPxEnabled && BasketSettings.UseHedgeUnderlyingForAutoCancel && HedgeUnderLoaded && NetTheoLoaded)
            {
                double changeInUnder = Math.Abs(_lastDeltaToWatchFor - HedgeMid);
                double threshold = Math.Abs(BasketSettings.CancelWithUnderlyingDeltaPx * 1 / TotalDelta);

                if (changeInUnder >= threshold)
                {
                    string message = "Auto cancel triggered by hedge underlying delta change. " +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + changeInUnder +
                                     ", Threshold: " + threshold +
                                     ", Under: " + HedgeMid +
                                     ", Under_W: " + _lastDeltaToWatchFor +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    if (CancelFromEdgeCheck(message))
                    {
                    }
                }
                else if (logPassed)
                {
                    string message = "Auto cancel not triggered by hedge underlying delta change. " +
                                     ", Spread: " + SpreadId +
                                     ", Change: " + changeInUnder +
                                     ", Threshold: " + threshold +
                                     ", Under: " + HedgeMid +
                                     ", Under_W: " + _lastDeltaToWatchFor +
                                     ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                    _log.Info(message);
                }
            }
        }

        protected bool CancelFromEdgeCheck(string message)
        {
            if (!_canAutoCancel)
            {
                return true;
            }

            bool retVal = false;

            _log.Info("Message: {}, Cancel Sent: {}", message, _cancelRequestSent);
            if (!_cancelRequestSent)
            {
                _cancelRequestSent = true;
                if (BasketSettings.ResubmitAfterCancel)
                {
                    _resubmitWhenReceivingCancelStatus = true;
                }
                CancelMain();
                retVal = true;
            }
            return retVal;
        }

        public async Task<bool> IsWidthWithinGlobalRangeAsync()
        {
            var config = BasketTraderViewModel.IsEdgeScanFeedAutoTrader ?
                OmsCore.Config.EdgeScanFishLossConfig :
                OmsCore.Config.BasketFishLossConfig;

            if (config is { MarketWidthCheckEnabled: true })
            {
                if (!MarkLoaded)
                {
                    await WaitForMarkLoad();
                }

                var isValid = Width >= config.MinMarketWidth && Width <= config.MaxMarketWidth;
                if (!isValid)
                {
                    _log.Info("Failed global fish loss width check! Id: {}, Width: {}, Range: {}-{}, Market: [{}X{}]", SpreadId, Width, config.MinMarketWidth, config.MaxMarketWidth, Low, High);
                }
                return isValid;
            }

            return true;
        }

        public async Task<bool> IsWidthAboveThresholdAsync()
        {
            if ((BasketSettings.CancelWithWidthEnabled))
            {
                if (!MarkLoaded)
                {
                    await WaitForMarkLoad();
                }
                bool valid = Width >= BasketSettings.CancelWithWidthThreshold;
                return valid;
            }
            return true;
        }

        public async Task<bool> IsWidthBelowThresholdAsync()
        {
            if ((BasketSettings.MaxWidthCheckEnabled))
            {
                if (!MarkLoaded)
                {
                    await WaitForMarkLoad();
                }
                return Width <= BasketSettings.MaxWidthCheckPx;
            }

            return true;
        }

        public async Task<bool> IsSizeAboveThresholdAsync()
        {
            if ((BasketSettings.MinBidAskSizeCheckEnabled))
            {
                if (!SizeLoaded)
                {
                    await WaitForSizeLoadAsync();
                }
                return BidSize >= BasketSettings.MinBidAskSize && AskSize >= BasketSettings.MinBidAskSize;
            }

            return true;
        }

        public async Task<bool> IsSizeBelowThresholdAsync()
        {
            if ((BasketSettings.CancelWithMaxSizeEnabled))
            {
                if (!SizeLoaded)
                {
                    await WaitForSizeLoadAsync();
                }
                return !IsSingleLegSell ? BidSize <= BasketSettings.CancelWithMaxSizeLimit : AskSize <= BasketSettings.CancelWithMaxSizeLimit;
            }

            return true;
        }

        protected async Task<bool> IsBelowMinTheoEdge()
        {
            if (BasketSettings.MinTheoEdgeCheckEnabled)
            {
                return await IsPriceBelowAdjTheoEdge(BasketSettings.MinTheoEdgeCheckEdge);
            }

            return true;
        }

        protected async Task<bool> IsBelowMinHwTheoEdge()
        {
            if (BasketSettings.MinHwTheoEdgeCheckEnabled)
            {
                return await IsPriceBelowAdjTheoEdge(BasketSettings.MinHwTheoEdgeCheckEdge, TheoModel.Hanw);
            }

            return true;
        }

        protected async Task<bool> IsBelowMinV0TheoEdge()
        {
            if (BasketSettings.MinV0TheoEdgeCheckEnabled)
            {
                return await IsPriceBelowAdjTheoEdge(BasketSettings.MinV0TheoEdgeCheckEdge, TheoModel.VolaV0);
            }

            return true;
        }

        protected async Task<bool> IsNotOnTheoJump()
        {
            if (BasketSettings.BlockSubmissionOnTheoJump)
            {
                if (!NetAdjTheoLoaded)
                {
                    await WaitForAdjTheoLoadAsync();
                }

                return !TheoJumpDetected;
            }

            return true;
        }

        protected async Task<bool> IsBelowMinEmaWidthPercentEdgeToTheo()
        {
            if (BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled)
            {
                var width = AskEmaAdj - BidEmaAdj;
                var edgeToTheo = Math.Round(width * BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge, 2);
                return await IsPriceBelowAdjTheoEdge(edgeToTheo);
            }

            return true;
        }

        protected async Task<bool> IsBelowMinMidEdge()
        {
            if (BasketSettings.MinMidEdgeCheckEnabled)
            {
                return await IsPriceBelowMidEdge(BasketSettings.MinMidEdgeCheckEdge);
            }

            return true;
        }

        protected async Task<bool> IsBelowMinEmaEdge()
        {
            if (BasketSettings.MinEmaEdgeCheckEnabled)
            {
                return await IsPriceBelowEmaEdge(BasketSettings.MinEmaEdgeCheckEdge);
            }

            return true;
        }

        protected async Task<bool> IsAboveBidPercent(bool useBestQuote = false)
        {
            if (BasketSettings.MinPercentBidCheckEnabled)
            {
                return await IsPriceAboveBidPercent(BasketSettings.MinPercentBidCheckEdge, useBestQuote);
            }

            return true;
        }

        protected async Task<bool> IsBelowBidPercent(bool useBestQuote = false)
        {
            if (BasketSettings.MaxPercentBidCheckEnabled)
            {
                return await IsPriceBelowBidPercent(BasketSettings.MaxPercentBidCheckEdge, useBestQuote);
            }

            return true;
        }

        protected async Task<bool> IsBelowDigBidPercent()
        {
            if (BasketSettings.MaxDigPercentBidCheckEnabled)
            {
                return await IsPriceBelowDigBidPercent(BasketSettings.MaxDigPercentBidCheckEdge);
            }

            return true;
        }

        public async Task<RiskCheckResult> IsValidEdgeToSkewAdjMarketAsync(double price, bool checkInverted = false)
        {
            if (!BasketSettings.MinEdgeToSkewMarketCheckEnabled)
            {
                return new RiskCheckResult(true, price);
            }

            var edge = BasketSettings.MinEdgeToSkewMarketCheckEdge;
            if (IsSingleLeg)
            {
                await SetPriceIncrementAsync();
            }

            await WaitForHighestBidLowestAskDataLoad();

            if (!checkInverted)
            {
                if (IsSingleLegSell)
                {
                    var target = LowestAsk + edge;
                    if (price < target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
                else
                {
                    var target = HighestBid - edge;
                    if (price > target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
            }
            else
            {
                if (IsSingleLeg)
                {
                    if (!IsSingleLegSell)
                    {
                        var target = LowestAsk + edge;
                        if (price < target)
                        {
                            return new RiskCheckResult(false, target);
                        }
                    }
                    else
                    {
                        var target = HighestBid - edge;
                        if (price > target)
                        {
                            return new RiskCheckResult(false, target);
                        }
                    }
                }
                else
                {
                    var target = -LowestAsk - edge;
                    if (price > target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
            }

            return new RiskCheckResult(true, price);
        }

        public async Task<RiskCheckResult> IsValidEdgeToSkewAdjMarketCrossAsync(double price, double edge, bool checkInverted = false)
        {
            if (!BasketSettings.MinEdgeToSkewMarketCrossCheckEnabled)
            {
                return new RiskCheckResult(true, price);
            }

            edge += BasketSettings.MinEdgeToSkewMarketCrossCheckEdge;

            await WaitForHighestBidLowestAskDataLoad();

            double minTick = (double)PriceIncrement;
            if (edge < minTick)
            {
                edge = minTick;
            }

            if (!checkInverted)
            {
                if (IsSingleLegSell)
                {
                    var target = HighestBid + edge;
                    if (price < target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
                else
                {
                    var target = LowestAsk - edge;
                    if (price > target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
            }
            else
            {
                if (IsSingleLeg)
                {
                    if (!IsSingleLegSell)
                    {
                        var target = HighestBid + edge;
                        if (price < target)
                        {
                            return new RiskCheckResult(false, target);
                        }
                    }
                    else
                    {
                        var target = LowestAsk - edge;
                        if (price > target)
                        {
                            return new RiskCheckResult(false, target);
                        }
                    }
                }
                else
                {
                    var target = -HighestBid - edge;
                    if (price > target)
                    {
                        return new RiskCheckResult(false, target);
                    }
                }
            }

            return new RiskCheckResult(true, price);
        }

        protected override TimeInForce GetTif()
        {
            if (BasketTraderViewModel.BasketType == BasketType.LockTrader && !string.IsNullOrWhiteSpace(OmsCore.Config.LockTraderTif))
            {
                return Enum.Parse<TimeInForce>(OmsCore.Config.LockTraderTif);
            }

            return TimeInForce;
        }

        protected override async Task<bool> CanSubmit()
        {
            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition < 0))
                {
                    ShowMessage("Basket Delta Limit Reached.",
                        "ZeroPlus OMS");
                    return false;
                }
            }

            if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                ShowMessage("Basket Long Position Limit Reached.",
                    "ZeroPlus OMS");
                return false;
            }

            if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                ShowMessage("Basket Short Position Limit Reached.",
                    "ZeroPlus OMS");
                return false;
            }

            if (BasketSettings.AdjustPriceBeforeSubmit)
            {
                DateTime time = DateTime.Now;
                await SetEdgeAsync(ignoreAdjTheoRiskCheck: false);
                if (DateTime.Now - time > RiskTimeSpan)
                {
                    ShowMessage("Set Edge Timeout! " + SpreadId, "Set Edge Timeout!");
                    return false;
                }
            }

            return true;
        }

        public override async Task<bool> IsValidOrder()
        {
            var result = await IsWithinDeltaCapAsync() && IsWithinStrikeCapAsync() && await IsWithinWidthCapAsync();
            return result;
        }

        public async Task<bool> IsWithinDeltaCapAsync()
        {
            if (BasketSettings.DeltaCapEnabled)
            {
                await WaitForTheoLoadAsync();
                double totalDelta = Math.Abs(TotalDelta);
                bool valid = totalDelta >= BasketSettings.DeltaCapLowerBound && totalDelta <= BasketSettings.DeltaCapUpperBound;
                return valid;
            }
            return true;
        }

        public bool IsWithinStrikeCapAsync()
        {
            if (BasketSettings.StrikeCapEnabled)
            {
                double minStrike = Legs.Min(x => x.Strike.Strike);
                double maxStrike = Legs.Max(x => x.Strike.Strike);
                bool valid = minStrike >= BasketSettings.StrikeCapLowerBound && maxStrike <= BasketSettings.StrikeCapUpperBound;
                return valid;
            }
            return true;
        }

        public async Task<bool> IsWithinWidthCapAsync()
        {
            if (BasketSettings.WidthCapEnabled)
            {
                await WaitForMarkLoad();
                double width = Math.Abs(High - Low);
                bool valid = width >= BasketSettings.WidthCapLowerBound && width <= BasketSettings.WidthCapUpperBound;
                return valid;
            }
            return true;
        }

        protected override double CheckForEdgeOverride(double edge, bool overrideEdge)
        {
            if (overrideEdge)
            {
                edge = base.CheckForEdgeOverride(edge, overrideEdge: true);
            }

            return edge;
        }

        public override void Dispose()
        {
            _log.Info("Disposing basket model for {}", SpreadId);
            isDisposing = true;
            base.Dispose();
            LegInHandler?.Dispose();
            OmsCore.QuoteClient.UnsubscribeAll(this);
            BasketTraderViewModel = null;
        }

        protected override async Task<bool> CheckForEdgeRisk(bool preSubmit)
        {
            if ((!_riskModel.DontTradeThroughEdge || !preSubmit) && (!_riskModel.AutoCancelWhenThroughEdge || preSubmit))
            {
                return true;
            }

            if (BasketSettings.UseEdgeToTheo)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowTheoEdge(BasketSettings.EdgeToTheo);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToHistoricBest)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowHistoricBestEdge(BasketSettings.EdgeToHistoricBest);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToAdjTheo)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowAdjTheoEdge(BasketSettings.EdgeToAdjTheo);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseTheoToMarketSpreadPx)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowTheoToMarketSpreadEdge(BasketSettings.EdgeToTheoToMarketSpread);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseLastFillAdjPx)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowLastFillAdjPx(BasketSettings.LastFillAdjEdge);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToMid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowMidEdge(BasketSettings.EdgeToMid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToEma)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowEmaEdge(BasketSettings.EdgeToEma);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToTheoAndMid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowTheoAndMidEdge(BasketSettings.EdgeToTheoAndMid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToTheoStopMid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowTheoStopMidEdge(BasketSettings.EdgeToTheoStopMid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToEmaStopMid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowEmaStopMidEdge(BasketSettings.EdgeToEmaStopMid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToMidStopEma)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowMidStopEmaEdge(BasketSettings.EdgeToMidStopEma);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEma)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowBidPercentStopEmaEdge(BasketSettings.EdgeToBidPercentStopEma);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowBidPercentStopEmaStopTheoEdge(BasketSettings.EdgeToBidPercentStopEmaStopTheo);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowEmaBidPercentStopEmaStopTheoEdge(BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowDerivedBidPercentStopEmaStopMidEdge(BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseBidPercent)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowBidPercent(BasketSettings.BidPercent);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToEmaBid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowEmaBidEdge(BasketSettings.EdgeToEmaBid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UseEdgeToBid)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowBidEdgeAsync(BasketSettings.EdgeToBid);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            else if (BasketSettings.UsePermAdjPx)
            {
                bool priceBelowEdgeCheckTask = await IsPriceBelowPermAdjPxAsync(BasketSettings.PermAdjEdge);
                if (!priceBelowEdgeCheckTask)
                {
                    return false;
                }
            }
            return true;
        }

        public override Task<DateTime> SetEdgeAsync(bool ignoreAdjTheoRiskCheck = true, double? edgeOverride = null)
        {
            return Task.Run(async Task<DateTime> () =>
            {
                DateTime time = DateTime.Now;
                if (BasketSettings.UseEdgeToTheo)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToTheo;
                    EdgeType = EdgeType.EdgeToTheo;
                    Edge = edge;
                    await UseEdgeToTheoAsync(edge);
                }
                else if (BasketSettings.UseEdgeToAdjTheo)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToAdjTheo;
                    EdgeType = EdgeType.EdgeToAdjustedTheo;
                    Edge = edge;
                    time = await UseEdgeToAdjTheoAsync(edge, ignoreAdjTheoRiskCheck);
                }
                else if (BasketSettings.UseEdgeToAdjTheoWithOverride)
                {
                    EdgeType = EdgeType.EdgeToAdjTheoWithOverride;
                    Edge = !BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage ? BasketSettings.EdgeToAdjTheoWithOverrideStatic : BasketSettings.EdgeToAdjTheoWithOverridePercent;
                    await UseEdgeToAdjTheoWithOverrideAsync(BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage, BasketSettings.EdgeToAdjTheoWithOverrideStatic, BasketSettings.EdgeToAdjTheoWithOverridePercent, ignoreAdjTheoRiskCheck);
                }
                else if (BasketSettings.UseLastFillAdjPx)
                {
                    double edge = edgeOverride ?? BasketSettings.LastFillAdjEdge;
                    EdgeType = EdgeType.LastFillAdjEdge;
                    Edge = edge;
                    await UseEdgeToLastFillAdjPx(edge);
                }
                else if (BasketSettings.UseEdgeToMid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToMid;
                    EdgeType = EdgeType.EdgeToMid;
                    Edge = edge;
                    await UseEdgeToMid(edge);
                    _log.Info("Set price using edge to mid. Mid: " + Mid + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToEma)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToEma;
                    EdgeType = EdgeType.EdgeToEma;
                    Edge = edge;
                    await UseEdgeToEma(edge);
                    _log.Info("Set price using edge to Ema. Ema: " + GetEma() + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToTheoAndMid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToTheoAndMid;
                    EdgeType = EdgeType.EdgeToTheoAndMid;
                    Edge = edge;
                    await UseEdgeToTheoAndMid(edge);
                    _log.Info("Set price using edge to theo and mid. Mid: " + Mid + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToTheoStopMid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToTheoStopMid;
                    EdgeType = EdgeType.EdgeToTheoStopMid;
                    Edge = edge;
                    await UseEdgeToTheoStopMid(edge);
                    _log.Info("Set price using edge to theo stop mid." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToEmaStopMid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToEmaStopMid;
                    EdgeType = EdgeType.EdgeToEmaStopMid;
                    Edge = edge;
                    await UseEdgeToEmaStopMid(edge);
                    _log.Info("Set price using edge to Ema stop mid." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToMidStopEma)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToMidStopEma;
                    EdgeType = EdgeType.EdgeToMidStopEma;
                    Edge = edge;
                    await UseEdgeToMidStopEma(edge);
                    _log.Info("Set price using edge to mid stop ema." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToBidPercentStopEma)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToBidPercentStopEma;
                    EdgeType = EdgeType.EdgeToBidPercentStopEma;
                    Edge = edge;
                    await UseEdgeToBidPercentStopEma(edge);
                    _log.Info("Set price using edge to bid percent stop ema." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToBidPercentStopEmaStopTheo;
                    EdgeType = EdgeType.EdgeToBidPercentStopEmaStopTheo;
                    Edge = edge;
                    await UseEdgeToBidPercentStopEmaStopTheo(edge);
                    _log.Info("Set price using edge to bid percent stop ema stop theo." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo;
                    EdgeType = EdgeType.EdgeToEmaBidPercentStopEmaStopTheo;
                    Edge = edge;
                    await UseEdgeToEmaBidPercentStopEmaStopTheo(edge);
                    _log.Info("Set price using edge to ema bid percent stop ema stop theo." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid;
                    EdgeType = EdgeType.EdgeToDerivedBidPercentStopEmaStopMid;
                    Edge = edge;
                    await UseEdgeToDerivedBidPercentStopEmaStopMid(edge);
                    _log.Info("Set price using edge to derived bid percent stop ema stop theo." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToEmaBid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToEmaBid;
                    EdgeType = EdgeType.EdgeToEmaBid;
                    Edge = edge;
                    await UseEdgeToEmaBid(edge);
                    _log.Info("Set price using edge to theo ema and bid." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseEdgeToBid)
                {
                    double edge = edgeOverride ?? BasketSettings.EdgeToBid;
                    EdgeType = EdgeType.EdgeToBid;
                    Edge = edge;
                    await UseEdgeToBid(edge);
                    _log.Info("Set price using edge to bid." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseBidPercent)
                {
                    double bidPercent = edgeOverride ?? BasketSettings.BidPercent;
                    EdgeType = EdgeType.BidPercent;
                    Edge = bidPercent;
                    if (bidPercent >= OmsCore.Config.MinimumBidPercentLimit && bidPercent <= OmsCore.Config.BidPercentLimit)
                    {
                        await UseBidPercent(bidPercent);
                        _log.Info("Set price using bid percent." + ", Edge: " + bidPercent + GetStats());
                    }
                    else
                    {
                        ResetPriceAndContraPrice();
                        _log.Info("Set price using bid percent failed." + ", Edge: " + bidPercent + GetStats());
                    }
                }
                else if (BasketSettings.UseTheoBidPercent)
                {
                    double bidPercent = edgeOverride ?? BasketSettings.TheoBidPercent;
                    EdgeType = EdgeType.TheoBidPercent;
                    Edge = bidPercent;
                    if (bidPercent >= OmsCore.Config.MinimumBidPercentLimit && bidPercent <= OmsCore.Config.BidPercentLimit)
                    {
                        await UseTheoBidPercent(bidPercent);
                        _log.Info("Set price using theo bid percent." + ", Edge: " + bidPercent + GetStats());
                    }
                    else
                    {
                        ResetPriceAndContraPrice();
                        _log.Info("Set price using theo bid percent failed." + ", Edge: " + bidPercent + GetStats());
                    }
                }
                else if (BasketSettings.UsePermAdjPx)
                {
                    double edge = edgeOverride ?? BasketSettings.PermAdjEdge;
                    EdgeType = EdgeType.PermAdjEdge;
                    Edge = edge;
                    await UsePermAdjPx(edge);
                    _log.Info("Set price using perm adj px." + ", Edge: " + edge + GetStats());
                }
                else if (BasketSettings.UseCustomFunctionEdge)
                {
                    EdgeType = EdgeType.CustomEdgeFormula;
                    Edge = double.NaN;
                    EdgeFormula = BasketSettings.CustomFunctionEdgeFormula;
                    await UseCustomEdgeFormula();
                    _log.Info("Set price using custom formula." + ", Edge: " + EdgeFormula + GetStats());
                }
                else if (BasketSettings.UseDomStyleEdge)
                {
                    EdgeType = EdgeType.EdgeToAdjustedTheo;
                    double domEdge = BasketSettings.DominatorConfiguration.DomStyleEdge(this);
                    time = await UseEdgeToAdjTheoAsync(domEdge, ignoreAdjTheoRiskCheck);
                    _log.Info("Set price using DomStyle Config." + ", Edge: " + domEdge + GetStats());
                }
                else if (BasketSettings.UseTheoToMarketSpreadPx)
                {
                    double edge = edgeOverride ?? BasketSettings.LastFillAdjEdge;
                    EdgeType = EdgeType.TheoToMarketSpread;
                    Edge = edge;
                    await UseEdgeToTheoToMarketSpread(edge);
                }
                else if (BasketSettings.UseBestOfEdge)
                {
                    EdgeType = EdgeType.UseBestOfEdge;
                    Edge = double.NaN;
                    await UseBestOfEdge();
                }

                SetStats();
                return time;
            });
        }

        internal async Task SetEdgeToTheoBidPercent(double bidPercent)
        {
            EdgeType = EdgeType.TheoBidPercent;
            Edge = bidPercent;
            await UseTheoBidPercent(bidPercent);
        }

        internal async Task UseBestOfEdge()
        {
            double bestPrice = double.NaN;
            bool isSell = IsSingleLegSell;
            var logDetail = new StringBuilder();
            var allValid = true;

            void Consider(EdgeType edgeType, double edge, double price)
            {
                if (logDetail.Length > 0)
                {
                    logDetail.Append("; ");
                }
                logDetail.Append('[').Append(edgeType).Append(" Edge=").Append(edge).Append(" Price=").Append(price).Append(']');
                if (double.IsNaN(price))
                {
                    allValid = false;
                }
                else if (double.IsNaN(bestPrice))
                {
                    bestPrice = price;
                    EdgeType = edgeType;
                    Edge = edge;
                }
                else
                {
                    if (isSell)
                    {
                        if (price > bestPrice)
                        {
                            bestPrice = price;
                            EdgeType = edgeType;
                            Edge = edge;
                        }
                    }
                    else
                    {
                        if (price < bestPrice)
                        {
                            bestPrice = price;
                            EdgeType = edgeType;
                            Edge = edge;
                        }
                    }
                }
            }

            if (BasketSettings.BestOfAdjTheoEnabled)
            {
                if (!NetAdjTheoLoaded) await WaitForAdjTheoLoadAsync();
                var result = await GetTheoAsync(BasketSettings.BestOfAdjTheoModel);
                var adjTheo = result.NetDeltaAdjTheo;
                var edge = BasketSettings.BestOfAdjTheoEdge;
                Consider(EdgeType.BestOfHwAdjTheo, edge, CalculateEdgeToAdjTheo(adjTheo, edge, overrideEdge: false).Price);
            }

            if (BasketSettings.BestOfHwTheoEnabled)
            {
                if (!NetAdjTheoLoaded) await WaitForAdjTheoLoadAsync();
                var result = await GetTheoAsync(TheoModel.Hanw);
                var adjTheo = result.NetDeltaAdjTheo;
                var edge = BasketSettings.BestOfHwTheoEdge;
                Consider(EdgeType.BestOfHwTheo, edge, CalculateEdgeToAdjTheo(adjTheo, edge, overrideEdge: false).Price);
            }

            if (BasketSettings.BestOfV0TheoEnabled)
            {
                if (!NetAdjTheoLoaded) await WaitForAdjTheoLoadAsync();
                var result = await GetTheoAsync(TheoModel.VolaV0);
                var adjTheo = result.NetDeltaAdjTheo;
                var edge = BasketSettings.BestOfV0TheoEdge;
                Consider(EdgeType.BestOfV0AdjTheo, edge, CalculateEdgeToAdjTheo(adjTheo, edge, overrideEdge: false).Price);
            }

            if (BasketSettings.BestOfMidEnabled)
            {
                if (!MarkLoaded) await WaitForMarkLoad();
                var edge = BasketSettings.BestOfMidEdge;
                Consider(EdgeType.BestOfMid, edge, CalculateEdgeToMid(edge).Price);
            }

            if (BasketSettings.BestOfEmaEnabled)
            {
                if (!MarkLoaded) await WaitForMarkLoad();
                var edge = BasketSettings.BestOfEmaEdge;
                Consider(EdgeType.BestOfEma, edge, CalculateEdgeToEma(edge).Price);
            }

            if (BasketSettings.BestOfBidPercentEnabled)
            {
                if (!MarkLoaded) await WaitForMarkLoad();
                var edge = BasketSettings.BestOfBidPercentEdge;
                Consider(EdgeType.BestOfBidPercent, edge, CalculateBidPercent(edge).Price);
            }

            if (BasketSettings.BestOfDigBidPercentEnabled)
            {
                if (!DigLoaded) await WaitForDigLoad();
                var edge = BasketSettings.BestOfDigBidPercentEdge;
                Consider(EdgeType.BestOfDigBidPercent, edge, CalculateDigBidPercent(edge).Price);
            }

            if (!double.IsNaN(bestPrice) && allValid)
            {
                logDetail.Append(" => IsSell=").Append(isSell).Append(" BestPrice=").Append(bestPrice);
                _log.Info("UseBestOfEdge. SpreadId: {}, {}", SpreadId, logDetail.ToString());
                SetPriceMinimal(bestPrice);
            }
            else
            {
                ResetPriceAndContraPrice();
                _log.Info("UseBestOfEdge invalid price. SpreadId: {}, Considered: {}", SpreadId, logDetail.Length > 0 ? logDetail.ToString() : "none");
            }
        }

        protected override async Task<bool> SubmitOrderAsync(bool resting, OrderSubType? module, double restOverride, TimeSpan span)
        {
            bool success;
            TypeId = BasketTraderViewModel.IsEdgeScanFeedAutoTrader
                ? ModuleType.EdgeScanFeed
                : ModuleType.Basket;

            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (automationConfigModel.AutoLegEnabled)
            {
                success = await Task.Run(function: () => SubmitAutoLegOrder(resting: resting, restOverride: restOverride, automationConfigModel: automationConfigModel));
                _log.Info(message: Status + " Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Span: " + span + GetStats());

            }
            else if (BasketSettings.LegInEnabled)
            {
                success = await Task.Run(() => LegInHandler.Start(module, restOverride));
            }
            else if (BasketSettings.FishModeEnabled)
            {
                success = await Task.Run(function: () => SubmitFishOrder(module: module));
            }
            else
            {
                success = await Task.Run(function: () => SubmitOrderAsync(isContra: false, resting: resting, module, cancelDelay: restOverride));
            }
            if (!success)
            {
                Status = !string.IsNullOrWhiteSpace(Reason) ? Reason : "An error occured sending order!";
                StatusMode = StatusMode.CancelledSell;
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, orderStatus: null);
            }
            return success;
        }

        protected async Task<bool> SubmitAutoLegOrder(bool resting, double restOverride, AutomationConfigModel automationConfigModel)
        {
            if (Legs.Count != 2)
            {
                Status = "Leg count not supported for Auto Leg!";
                return false;
            }
            else
            {
                var easyLeg = Legs.MinBy(x => x.Ask - x.Bid);
                if ((easyLeg.Ask - easyLeg.Bid) > automationConfigModel.AutoLegMaxWidth)
                {
                    Status = "Max width check failed for Auto Leg!";
                    return false;
                }
                else
                {
                    var hardLeg = Legs.First(x => x != easyLeg);
                    if (Math.Abs(hardLeg.Delta) < Math.Abs(easyLeg.Delta))
                    {
                        Status = "Delta check failed for Auto Leg!";
                        return false;
                    }
                    else
                    {
                        if (AutoLegCloser.HardLeg != hardLeg)
                        {
                            List<Data.Securities.Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                            Data.Securities.Option option = options.FirstOrDefault(x => x.OptionSymbol == hardLeg.Symbol);
                            if (option == null || double.IsNaN(option.MinimumTick))
                            {
                                Status = "Min tick load failed for Auto Leg!";
                                return false;
                            }
                            AutoLegCloser.SetMinIncrement(option.MinimumTick);
                            AutoLegCloser.HardLegMinimumTickStyle = (MinimumTickStyle)option.TickType;
                        }

                        AutoLegCloser.HardLeg = hardLeg;
                        AutoLegCloser.EasyLeg = easyLeg;
                        AutoLegCloser.Config = automationConfigModel;
                        await SubmitOrderAsync(isContra: false, resting: resting, OrderSubType.AutoLeg, cancelDelay: restOverride);
                        return true;
                    }
                }
            }
        }

        protected bool SubmitFishOrder(OrderSubType? module)
        {
            try
            {
                Closing = false;
                ThreeWayStarted = false;
                ThreeWayComplete = false;
                ValidateAccount();
                List<TicketLegModel> validLegs = Legs.ToList().Where(leg => leg.IsValid).ToList();

                if (validLegs.Count == 0)
                {
                    throw new SlimException("No valid legs.");
                }

                AutomationConfigModel automationConfigModel = GetAutomationConfig();

                Fisher.StartFisher(basePrice: Price,
                                   underlyingAtBase: UnderMid,
                                   qty: Lcd,
                                   fishEdge: automationConfigModel.FishEdge,
                                   fishMaxLoss: 0,
                                   priceIncrement: automationConfigModel.FishPriceIncrement,
                                   interval: automationConfigModel.FishInterval,
                                   manual: false,
                                   type: module);

                SetAutoCancelTriggers();

                OrderIsClosed = false;

                return true;
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitFishOrder));
                ShowMessage(ex.Message, "Order Submission Failed.");
                return false;
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitFishOrder));
                ShowMessage(ex.Message, "Order Submission Failed.");
                return false;
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message, "Order Submission Failed.");
                _log.Error(ex, nameof(SubmitFishOrder));
                return false;
            }
        }

        protected override void SetAutoCancelTriggers()
        {
            if (IsDisposed)
            {
                return;
            }
            SetAutoCancelWithMidChange();
            SetAutoCancelWithWidthChange();
            SetAutoCancelWithTheoChange();
            SetAutoCancelWithAdjTheoChange();
            SetAutoCancelWithUnderPxChange();
            SetAutoCancelWithUnderDeltaPxChange();
            _log.Info($"Auto Cancel Triggers Set. Latency Timer: {_latencyTimer.ElapsedMilliseconds}{GetStats()}");
        }

        #region Auto Cancel

        public async void SetAutoCancelWithTheoChange()
        {
            if (BasketSettings.CancelWithEdgeToTheoEnabled)
            {
                await WaitForTheoLoadAsync();
                _theoToWatchFor = NetTheo;
                _ = CheckForAutoCancel();
            }
            else
            {
                _theoToWatchFor = double.NaN;
            }
        }

        public async void SetAutoCancelWithMaxSize()
        {
            if (BasketSettings.CancelWithMaxSizeEnabled)
            {
                await WaitForSizeLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithAdjTheoChange()
        {
            if (BasketSettings.CancelWithEdgeToAdjTheoEnabled)
            {
                await WaitForAdjTheoLoadAsync();
                _adjTheoToWatchFor = NetDeltaAdjTheo;
                _ = CheckForAutoCancel();
            }
            else
            {
                _adjTheoToWatchFor = double.NaN;
            }
        }

        public async void SetAutoCancelWithMidChange()
        {
            if (BasketSettings.CancelWithEdgeToMidEnabled)
            {
                await WaitForMarkLoad();
                _midToWatchFor = Mid;
                _ = CheckForAutoCancel();
            }
            else
            {
                _midToWatchFor = double.NaN;
            }
        }

        public async void SetAutoCancelWithMaxWidthChange()
        {
            if (BasketSettings.MaxWidthCheckEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinTheoEdgeChange()
        {
            if (BasketSettings.MinTheoEdgeCheckEnabled)
            {
                await WaitForAdjTheoLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinHwTheoEdgeChange()
        {
            if (BasketSettings.MinHwTheoEdgeCheckEnabled)
            {
                await WaitForAdjTheoLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinV0TheoEdgeChange()
        {
            if (BasketSettings.MinV0TheoEdgeCheckEnabled)
            {
                await WaitForAdjTheoLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinEmaWidthPercentEdgeToTheo()
        {
            if (BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled)
            {
                await WaitForAdjEmaLoad();
                await WaitForAdjTheoLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinMidEdgeChange()
        {
            if (BasketSettings.MinMidEdgeCheckEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinPercentBid()
        {
            if (BasketSettings.MinPercentBidCheckEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMaxPercentBid()
        {
            if (BasketSettings.MaxPercentBidCheckEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMaxDigPercentBid()
        {
            if (BasketSettings.MaxDigPercentBidCheckEnabled)
            {
                await WaitForDigLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinBid()
        {
            if (BasketSettings.MinBidCheckEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinBidAskSize()
        {
            if (BasketSettings.MinBidAskSizeCheckEnabled)
            {
                await WaitForSizeLoadAsync();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithMinEmaEdgeChange()
        {
            if (BasketSettings.MinEmaEdgeCheckEnabled)
            {
                if (BasketSettings.SubscribeToEma)
                {
                    await WaitForEmaLoad();
                    _ = CheckForAutoCancel();
                }
            }
        }

        public async void SetAutoCancelWithWidthChange()
        {
            if (BasketSettings.CancelWithWidthEnabled)
            {
                await WaitForMarkLoad();
                _ = CheckForAutoCancel();
            }
        }

        public async void SetAutoCancelWithUnderPxChange()
        {
            if (BasketSettings.CancelWithUnderlyingPxEnabled)
            {
                if (!BasketSettings.UseHedgeUnderlyingForAutoCancel)
                {
                    await WaitForUnderMidLoadAsync();
                    _underToWatchFor = UnderMid;
                }
                else
                {
                    await WaitForHedgeLastLoadAsync();
                    _underToWatchFor = HedgeMid;
                }

                _ = CheckForAutoCancel();
            }
            else
            {
                _underToWatchFor = double.NaN;
            }
        }

        public async void SetAutoCancelWithUnderDeltaPxChange()
        {
            if (BasketSettings.CancelWithUnderlyingDeltaPxEnabled)
            {
                if (!BasketSettings.UseHedgeUnderlyingForAutoCancel)
                {
                    await WaitForUnderMidLoadAsync();
                    _lastDeltaToWatchFor = UnderMid;
                }
                else
                {
                    await WaitForHedgeLastLoadAsync();
                    _lastDeltaToWatchFor = HedgeMid;
                }

                _ = CheckForAutoCancel();
            }
            else
            {
                _lastDeltaToWatchFor = double.NaN;
            }
        }

        #endregion

        #region Auto Hedge

        public override bool CheckForAutoHedge(double attemptedEdge, bool lastAttempt = false)
        {
            try
            {
                AutomationConfigModel config = GetAutomationConfig();
                if (config == null || !config.AutoHedgeOnClose)
                {
                    return false;
                }

                if (config.AutoHedgeOnCloseSizeOnly && Lcd == 1)
                {
                    _log.Info($"{nameof(CheckForAutoHedge)}. Qty below auto hedge limit. " +
                              $"Id: {SpreadId}, " +
                              $"Attempted edge: {attemptedEdge}, " +
                              $"Min Hedge Edge: {config.MinHedgeHouseEdge}, " +
                              $"Underlying At Fill: {LastMainUnderMidAtFill}, " +
                              $"Underlying: {UnderBid}X{UnderAsk}.");
                    return false;
                }

                if (IsValidForHedging())
                {
                    if ((lastAttempt && config.AutoHedgeOnFailure) || attemptedEdge <= config.MinHedgeHouseEdge)
                    {
                        double deltaAdjustedLastFillPrice = Math.Round(((UnderMid - LastMainUnderMidAtFill) * TotalDelta) + LastFillPx, 2);
                        if (!IsSingleLeg)
                        {
                            deltaAdjustedLastFillPrice *= -1;
                        }

                        double deltaAdjustedEdge = CalculateAttemptedEdgeOnClose(deltaAdjustedLastFillPrice);

                        int requiredStocksPerContract = Math.Abs(CalculateRequiredHedgeQty(1));
                        double underWidth = UnderAsk - UnderBid;
                        double costOfHedging = Math.Round(requiredStocksPerContract * underWidth / Multiplier, 2);

                        double totalEdgeFromHedge = Math.Round(deltaAdjustedEdge - costOfHedging, 2);

                        int pos = Lcd;
                        if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            pos *= -1;
                        }
                        int requiredStocks = CalculateRequiredHedgeQty(pos);
                        double hedgeEstNotional = Math.Round(Math.Abs(requiredStocks > 0 ? requiredStocks * UnderAsk : requiredStocks * UnderBid), 2);

                        _log.Info($"{nameof(CheckForAutoHedge)}. " +
                                  $"Id: {SpreadId}, " +
                                  $"Attempted edge: {attemptedEdge}, " +
                                  $"Total edge with hedge: {totalEdgeFromHedge}, " +
                                  $"Delta Adj Edge: {deltaAdjustedEdge}, " +
                                  $"Hedge cost: {costOfHedging}, " +
                                  $"Required Hedge Per Contract: {requiredStocksPerContract}, " +
                                  $"Adj Last Fill: {deltaAdjustedLastFillPrice}, " +
                                  $"Required Stocks: {requiredStocks}, " +
                                  $"Hedge Est Notional: {hedgeEstNotional}, " +
                                  $"Delta: {TotalDelta}, " +
                                  $"Last Fill: {LastFillPx}, " +
                                  $"Min Hedge Edge: {config.MinHedgeHouseEdge}, " +
                                  $"Underlying At Fill: {LastMainUnderMidAtFill}, " +
                                  $"Underlying: {UnderBid}X{UnderAsk}.");

                        if (hedgeEstNotional <= OmsCore.Config.BasketHedgeHouseMaxNotionalV2)
                        {
                            if (Math.Abs(requiredStocks) <= OmsCore.Config.BasketHedgeHouseMaxQtyV2)
                            {
                                HedgeWithStockAsync(requiredStocks);
                                ReleaseHedgingInstance();
                                return true;
                            }
                            else if (lastAttempt && config.AutoHedgeOnFailure && config.AutoHedgePartial)
                            {
                                HedgeWithStockAsync(Math.Min(requiredStocks, OmsCore.Config.BasketHedgeHouseMaxQtyV2));
                                ReleaseHedgingInstance();
                                return true;
                            }
                            else
                            {
                                _log.Info($"{nameof(CheckForAutoHedge)}. Attempted hedge above risk limit. " +
                                          $"Id: {SpreadId}, " +
                                          $"Req Stock: {requiredStocks}, " +
                                          $"Est Notional: {hedgeEstNotional}, " +
                                          $"Max Qty: {OmsCore.Config.BasketHedgeHouseMaxQtyV2}, " +
                                          $"Max Notional: {OmsCore.Config.BasketHedgeHouseMaxNotionalV2}.");
                            }
                        }
                        else
                        {
                            _log.Info($"{nameof(CheckForAutoHedge)}. Attempted hedge above risk limit. " +
                                      $"Id: {SpreadId}, " +
                                      $"Req Stock: {requiredStocks}, " +
                                      $"Est Notional: {hedgeEstNotional}, " +
                                      $"Max Qty: {OmsCore.Config.BasketHedgeHouseMaxQtyV2}, " +
                                      $"Max Notional: {OmsCore.Config.BasketHedgeHouseMaxNotionalV2}.");
                        }
                    }
                    else
                    {
                        _log.Info($"{nameof(CheckForAutoHedge)}. Attempted edge above min hedge edge. " +
                                  $"Id: {SpreadId}, " +
                                  $"Attempted edge: {attemptedEdge}, " +
                                  $"Min Hedge Edge: {config.MinHedgeHouseEdge}, " +
                                  $"Underlying At Fill: {LastMainUnderMidAtFill}, " +
                                  $"Underlying: {UnderBid}X{UnderAsk}.");
                    }
                }
                else
                {
                    _log.Info($"{nameof(CheckForAutoHedge)}. Underlying load failed. " +
                              $"Id: {SpreadId}, " +
                              $"Attempted edge: {attemptedEdge}, " +
                              $"Min Hedge Edge: {config.MinHedgeHouseEdge}, " +
                              $"Underlying At Fill: {LastMainUnderMidAtFill}, " +
                              $"Underlying: {UnderBid}X{UnderAsk}.");
                }

                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForAutoHedge));
                return false;
            }
        }

        #endregion

        internal async Task UseEdgeToHistoricBestAsync(double edge)
        {
            try
            {
                if (await WaitForHistoricBestLoadAsync())
                {
                    SetEdgeToHistoricBestPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToHistoricBestAsync));
            }
            finally
            {
                _log.Info("Set price using edge to HistoricBest. Historic Best: " + BestEdgeBid + "x" + BestEdgeAsk + ", Edge: " + edge + GetStats());

            }
        }

        internal async Task UseEdgeToAdjTheoWithOverrideAsync(bool useStatic, double staticAddition, double percentage, bool ignoreAdjTheoRiskCheck = true)
        {
            try
            {
                var edge = EdgeOverride;
                if (double.IsNaN(edge))
                {
                    ResetPriceAndContraPrice();
                    _log.Info($"Edge Override not loaded! {GetStats()}");
                }

                if (useStatic)
                {
                    edge += staticAddition;
                }
                else
                {
                    edge *= percentage;
                }

                await UseEdgeToAdjTheoAsync(edge, ignoreAdjTheoRiskCheck, false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToAdjTheoWithOverrideAsync));
            }
            finally
            {
                _log.Info("Set price using edge to adj theo with override. Use Static: " + useStatic + ", Static: " + staticAddition + ", Percentage: " + percentage + GetStats());
            }
        }

        internal async Task<bool> UseEdgeToBestLastFillAdjPx(double edge)
        {
            try
            {
                if (await WaitForTheoLoadAsync() &&
                    await WaitForUnderMidLoadAsync())
                {
                    SetEdgeToBestLastFillAdjPx(edge);
                    return true;
                }
                else
                {
                    ResetPriceAndContraPrice();
                    return false;

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToBestLastFillAdjPx));
                return false;
            }
            finally
            {
                _log.Info("Set price using edge to adjusted best last fill px. Edge: " + edge + GetStats());

            }
        }

        internal async Task UseEdgeToTheoAndMid(double edge)
        {
            try
            {
                if (await WaitForTheoLoadAsync() && await WaitForMarkLoad())
                {
                    SetEdgeToTheoAndMidPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToTheoAndMid));
            }
        }

        internal async Task UseEdgeToTheoStopMid(double edge)
        {
            try
            {
                if (await WaitForTheoLoadAsync() && await WaitForMarkLoad())
                {
                    SetEdgeToTheoStopMidPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToTheoStopMid));
            }
        }

        internal async Task UseEdgeToEmaStopMid(double edge)
        {
            try
            {
                if (await WaitForEmaLoad() && await WaitForMarkLoad())
                {
                    SetEdgeToEmaStopMidPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToEmaStopMid));
            }
        }

        internal async Task UseEdgeToMidStopEma(double edge)
        {
            try
            {
                if (await WaitForMarkLoad() && await WaitForEmaLoad())
                {
                    SetEdgeToMidStopEmaPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToMidStopEma));
            }
        }

        internal async Task UseEdgeToBidPercentStopEma(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForEmaLoad())
                {
                    SetEdgeToBidPercentStopEmaPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToBidPercentStopEma));
            }
        }

        internal async Task UseEdgeToBidPercentStopEmaStopTheo(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForEmaLoad() && await WaitForAdjTheoLoadAsync())
                {
                    SetEdgeToBidPercentStopEmaStopTheoPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToBidPercentStopEmaStopTheo));
            }
        }

        internal async Task UseEdgeToEmaBidPercentStopEmaStopTheo(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForEmaLoad() && await WaitForAdjTheoLoadAsync())
                {
                    SetEdgeToEmaBidPercentStopEmaStopTheoPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToEmaBidPercentStopEmaStopTheo));
            }
        }

        internal async Task UseEdgeToDerivedBidPercentStopEmaStopMid(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForEmaLoad() && await WaitForAdjTheoLoadAsync())
                {
                    SetEdgeToDerivedBidPercentStopEmaStopMidPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToDerivedBidPercentStopEmaStopMid));
            }
        }

        internal async Task UseEdgeToBid(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync())
                {
                    SetEdgeToBid(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToBid));
            }
        }

        internal async Task UsePermAdjPx(double edge)
        {
            try
            {
                if (await WaitForMarkLoad() && await WaitForTheoLoadAsync())
                {
                    SetPermAdjPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UsePermAdjPx));
            }
        }

        internal async Task UseCustomEdgeFormula()
        {
            try
            {

                await SetPriceUsingCustomEdgeFormula();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseCustomEdgeFormula));
            }
        }

        internal async Task UseEdgeToEmaBid(double edge)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForHighLoad() && await WaitForBidEmaLoad() && await WaitForAskEmaLoadAsync())
                {
                    SetEdgeToEmaBid(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToEmaBid));
            }
        }

        protected void SetEdgeToHistoricBestPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToHistoricBest(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToBestLastFillAdjPx(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToBestLastFillAdjPx(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToTheoAndMidPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToTheoAndMid(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToTheoStopMidPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToTheoStopMid(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToEmaStopMidPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToEmaStopMid(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToMidStopEmaPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToMidStopEma(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToBidPercentStopEmaPrice(double percent)
        {
            EdgePriceCalculationResult edgeResult = CalculateBidPercentStopEma(percent);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToBidPercentStopEmaStopTheoPrice(double percent)
        {
            EdgePriceCalculationResult edgeResult = CalculateBidPercentStopEmaStopTheo(percent);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToEmaBidPercentStopEmaStopTheoPrice(double percent)
        {
            EdgePriceCalculationResult edgeResult = CalculateEmaBidPercentStopEmaStopTheo(percent);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToDerivedBidPercentStopEmaStopMidPrice(double percent)
        {
            EdgePriceCalculationResult edgeResult = CalculateDerivedBidPercentStopEmaStopMid(percent);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToEmaBid(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToBestOfEmaAndMkt(edge, edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToBid(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToBid(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected async Task SetPriceUsingCustomEdgeFormula()
        {
            EdgePriceCalculationResult edgeResult = await Task.Run(CalculateCustomEdgePrice);
            SetPriceAndContraPrice(edgeResult);
        }

        public async Task<bool> IsPriceBelowTheoEdge(double edge)
        {
            if (!NetTheoLoaded)
            {
                await WaitForTheoLoadAsync();
            }

            var result = await GetTheoAsync(BasketSettings.FishLossTheoModel);
            var netTheo = result.NetTheo;
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToTheo(netTheo, edge, overrideEdge: false).Price : Price <= CalculateEdgeToTheo(netTheo, edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowHistoricBestEdge(double edge)
        {
            if (!NetHistoricBestLoaded)
            {
                await WaitForHistoricBestLoadAsync();
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToHistoricBest(edge, overrideEdge: false).Price : Price <= CalculateEdgeToHistoricBest(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowAdjTheoEdge(double edge, TheoModel? theoModel = null)
        {
            if (!NetAdjTheoLoaded)
            {
                await WaitForAdjTheoLoadAsync();
            }

            theoModel ??= BasketSettings.FishLossTheoModel;
            var result = await GetTheoAsync(theoModel);
            var netDeltaAdjTheo = result.NetDeltaAdjTheo;
            double target = CalculateEdgeToAdjTheo(netDeltaAdjTheo, edge, overrideEdge: false).Price;
            bool valid = IsSingleLegSell ? Price >= target : Price <= target;
            return valid;
        }

        public async Task<bool> IsPriceBelowTheoToMarketSpreadEdge(double edge)
        {
            if (!NetAdjTheoLoaded)
            {
                await WaitForAdjTheoLoadAsync();
            }

            double target = CalculateEdgeToTheoToMarketSpread(edge, overrideEdge: false).Price;
            bool valid = IsSingleLegSell ? Price >= target : Price <= target;
            return valid;
        }

        private async Task<bool> IsPriceValidEdgeToTheo()
        {
            if (!NetAdjTheoLoaded)
            {
                await WaitForTheoLoadAsync();
            }

            var valid = true;
            if (BasketSettings.CancelWithEdgeToTheoEnabled && NetTheoLoaded)
            {
                double delta = _theoToWatchFor - NetTheo;

                if (_theoToWatchFor < 0)
                {
                    delta *= -1;
                }

                valid = !(delta >= BasketSettings.CancelWithTheoEdge);

            }

            return valid;
        }

        private async Task<bool> IsPriceValidEdgeToDeltaAdjTheo()
        {
            if (!NetAdjTheoLoaded)
            {
                await WaitForAdjTheoLoadAsync();
            }

            var valid = true;
            if (BasketSettings.CancelWithEdgeToAdjTheoEnabled && NetAdjTheoLoaded)
            {
                double delta = _adjTheoToWatchFor - NetDeltaAdjTheo;

                if (_adjTheoToWatchFor < 0)
                {
                    delta *= -1;
                }

                valid = !(delta >= BasketSettings.CancelWithAdjTheoEdge);

            }

            return valid;
        }

        public async Task<bool> IsPriceBelowLastFillAdjPx(double edge)
        {
            if (!MarkLoaded)
            {
                await WaitForMarkLoad();
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToLastFillAdjPx(edge, overrideEdge: false).Price : Price <= CalculateEdgeToLastFillAdjPx(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowMidEdge(double edge)
        {
            if (!MarkLoaded)
            {
                await WaitForMarkLoad();
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToMid(edge, overrideEdge: false).Price : Price <= CalculateEdgeToMid(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowEmaEdge(double edge)
        {
            if (!MarkLoaded)
            {
                await WaitForMarkLoad();
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToEma(edge, overrideEdge: false).Price : Price <= CalculateEdgeToEma(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsBidAboveMinBid(double minBid)
        {
            if (BasketSettings.MinBidCheckEnabled)
            {
                if (!MarkLoaded)
                {
                    await WaitForMarkLoad();
                }

                return Low >= Math.Round(minBid, 2);
            }
            return true;
        }

        public async Task<bool> IsTheoAboveMinTheo(double minTheo)
        {
            if (BasketSettings.MinTheoCheckEnabled)
            {
                if (!NetAdjTheoLoaded)
                {
                    await WaitForAdjTheoLoadAsync();
                }

                return Math.Abs(NetDeltaAdjTheo) >= Math.Round(minTheo, 2);
            }
            return true;
        }

        public async Task<bool> IsPriceBelowTheoAndMidEdge(double edge)
        {
            if (!(NetTheoLoaded && MarkLoaded))
            {
                if (await WaitForTheoLoadAsync())
                {
                    await WaitForMarkLoad();
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToTheoAndMid(edge, overrideEdge: false).Price : Price <= CalculateEdgeToTheoAndMid(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowTheoStopMidEdge(double edge)
        {
            if (!(NetTheoLoaded && MarkLoaded))
            {
                if (await WaitForTheoLoadAsync())
                {
                    await WaitForMarkLoad();
                }
            }

            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToTheoStopMid(edge, overrideEdge: false).Price : Price <= CalculateEdgeToTheoStopMid(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowEmaStopMidEdge(double edge)
        {
            if (!(EmaLoaded && MarkLoaded))
            {
                if (await WaitForEmaLoad())
                {
                    await WaitForMarkLoad();
                }
            }

            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToEmaStopMid(edge, overrideEdge: false).Price : Price <= CalculateEdgeToEmaStopMid(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowMidStopEmaEdge(double edge)
        {
            if (!(EmaLoaded && MarkLoaded))
            {
                if (await WaitForMarkLoad())
                {
                    await WaitForEmaLoad();
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToMidStopEma(edge, overrideEdge: false).Price : Price <= CalculateEdgeToMidStopEma(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowBidPercentStopEmaEdge(double percent)
        {
            if (!(EmaLoaded && MarkLoaded))
            {
                if (await WaitForLowLoadAsync())
                {
                    await WaitForEmaLoad();
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateBidPercentStopEma(percent, overrideEdge: false).Price : Price <= CalculateBidPercentStopEma(percent, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowBidPercentStopEmaStopTheoEdge(double percent)
        {
            if (!(EmaLoaded && MarkLoaded && NetAdjTheoLoaded))
            {
                if (await WaitForLowLoadAsync())
                {
                    if (await WaitForEmaLoad())
                    {
                        await WaitForAdjTheoLoadAsync();
                    }
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateBidPercentStopEmaStopTheo(percent, overrideEdge: false).Price : Price <= CalculateBidPercentStopEmaStopTheo(percent, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowEmaBidPercentStopEmaStopTheoEdge(double percent)
        {
            if (!(EmaLoaded && MarkLoaded && NetAdjTheoLoaded))
            {
                if (await WaitForLowLoadAsync())
                {
                    if (await WaitForEmaLoad())
                    {
                        await WaitForAdjTheoLoadAsync();
                    }
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEmaBidPercentStopEmaStopTheo(percent, overrideEdge: false).Price : Price <= CalculateEmaBidPercentStopEmaStopTheo(percent, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowDerivedBidPercentStopEmaStopMidEdge(double percent)
        {
            if (!(EmaLoaded && MarkLoaded))
            {
                if (await WaitForLowLoadAsync())
                {
                    await WaitForEmaLoad();
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateDerivedBidPercentStopEmaStopMid(percent, overrideEdge: false).Price : Price <= CalculateDerivedBidPercentStopEmaStopMid(percent, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowEmaBidEdge(double edge)
        {
            if (!(LowLoaded && HighLoaded))
            {
                if (await WaitForLowLoadAsync())
                {
                    if (await WaitForHighLoad())
                    {
                        if (await WaitForBidEmaLoad())
                        {
                            await WaitForAskEmaLoadAsync();
                        }
                    }
                }
            }
            bool valid = IsSingleLegSell ? Price >= CalculateEdgeToBestOfEmaAndMkt(edge, edge, overrideEdge: false).Price : Price <= CalculateEdgeToBestOfEmaAndMkt(edge, edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowBidEdgeAsync(double edge)
        {
            if (!LowLoaded)
            {
                await WaitForLowLoadAsync();
            }
            bool valid = Price <= CalculateEdgeToBid(edge, overrideEdge: false).Price;
            return valid;
        }

        public async Task<bool> IsPriceBelowPermAdjPxAsync(double edge)
        {

            if (!(NetTheoLoaded && MarkLoaded))
            {
                if (await WaitForTheoLoadAsync())
                {
                    await WaitForMarkLoad();
                }
            }
            bool valid = Price <= CalculatePermAdjPrice(edge, overrideEdge: false).Price;
            return valid;
        }


        #region Size Up


        internal override async Task<ResubmitSizeOption> CheckLoopSizeUpAsync(double edge, bool savePrevSize, bool allowReverse)
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (PreSizeCheck != null)
            {
                bool canSizeUp = PreSizeCheck.Invoke();
                if (!canSizeUp)
                {
                    ResetLoopSize();
                    _log.Info("Check Loop Size-Up Failed. " +
                                  "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                  "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                  "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                  "Lcd Before Size-Up: " + PrevQty + ", " +
                                  "Edge: " + edge + ", " +
                                  "Lcd: " + Lcd + ", " +
                                  "Id: " + SpreadId);
                }
            }

            ResubmitAfterLastLoopCount = 0;
            switch (automationConfigModel.LoopSizeupType)
            {
                case LoopSizeupType.Off:
                    _log.Info("Check Loop Size-Up OFF. " +
                              "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                              "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                              "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                              "Lcd Before Size-Up: " + PrevQty + ", " +
                              "Edge: " + edge + ", " +
                              "Lcd: " + Lcd + ", " +
                              "Id: " + SpreadId);
                    break;
                case LoopSizeupType.Static:
                    if (automationConfigModel.LoopCountBeforeSizeup <= LoopIterationCounter &&
                        automationConfigModel.LoopSizeupQty > Lcd)
                    {
                        if (automationConfigModel.SizeUpOnHardSideOnly &&
                            HardSide.HasValue &&
                            Side != HardSide)
                        {
                            _log.Info("Size up not allowed on easy size. " +
                                  "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                  "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                  "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                  "Lcd Before Size-Up: " + PrevQty + ", " +
                                  "Edge: " + edge + ", " +
                                  "Will Reverse: " + allowReverse + ", " +
                                  "Lcd: " + Lcd + ", " +
                                  "Id: " + SpreadId);
                            if (allowReverse)
                            {
                                ReverseWhileMaintainingState();
                            }
                            else
                            {
                                ResetLoopSize();
                                break;
                            }
                        }

                        PrevQty = Lcd;
                        ResetSize = true;
                        UpdateQty(automationConfigModel.LoopSizeupQty);
                    }
                    _log.Info("Check Loop Static Size-Up. " +
                              "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                              "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                              "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                              "Lcd Before Size-Up: " + PrevQty + ", " +
                              "Edge: " + edge + ", " +
                              "HardSide: " + HardSide + ", " +
                              "Lcd: " + Lcd + ", " +
                              "Id: " + SpreadId);
                    break;
                case LoopSizeupType.Dynamic:
                    if (automationConfigModel.SizeupConfig != null)
                    {
                        if (automationConfigModel.SizeUpOnHardSideOnly &&
                            HardSide.HasValue &&
                            Side != HardSide)
                        {
                            _log.Info("Size up not allowed on easy size. " +
                                  "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                  "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                  "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                  "Lcd Before Size-Up: " + PrevQty + ", " +
                                  "Edge: " + edge + ", " +
                                  "HardSide: " + HardSide + ", " +
                                  "Will Reverse: " + allowReverse + ", " +
                                  "Lcd: " + Lcd + ", " +
                                  "Id: " + SpreadId);
                            if (allowReverse)
                            {
                                ReverseWhileMaintainingState();
                            }
                            else
                            {
                                ResetLoopSize();
                                break;
                            }
                        }
                        ObservableCollection<SizeupConfigModel> sizeUpConfigs = automationConfigModel.SizeupConfig.SizeUpConfigs;
                        if (sizeUpConfigs != null)
                        {
                            if (WasPartiallyFilled)
                            {
                                WasPartiallyFilled = false;
                                if (automationConfigModel.SizeupConfig.MatchFilledSizeAfterPartialFill)
                                {
                                    _log.Info("Using partial fill size. " +
                                              "Lcd: " + Lcd + ", " +
                                              "Id: " + SpreadId);
                                    return ResubmitSizeOption.Off;
                                }
                            }

                            var count = sizeUpConfigs.Count();
                            edge = Math.Round(edge, 2);
                            for (int i = 0; i < sizeUpConfigs.Count; i++)
                            {
                                SizeupConfigModel config = sizeUpConfigs[i];
                                double configEdge = config.Edge;
                                if (config.AdditionalEdgePerContract > 0)
                                {
                                    int contracts = Legs.Sum(x => Math.Abs(x.Ratio));
                                    if (contracts > 1)
                                    {
                                        configEdge += (contracts - 1) * config.AdditionalEdgePerContract;
                                    }
                                }
                                configEdge = Math.Round(configEdge, 2);
                                double underwidth = Math.Round(Math.Abs(UnderAsk - UnderBid), 2);
                                double underMid = (UnderBid + UnderAsk) / 2;
                                double absDelta = Math.Round(Math.Abs(TotalDelta), 2);
                                bool incrementCheckPassed = !config.MinIncrementEnabled ||
                                                            (double)GetPriceIncrement(Price) <= config.MinIncrement;
                                bool edgeCheckPassed = configEdge <= edge && edge > 0;
                                bool requiredLoopCheckPassed = config.RequiredLoop <= LoopIterationCounter;
                                bool deltaCheckPassed = config.MaxAbsDelta > .99 || absDelta <= Math.Round(config.MaxAbsDelta, 2);
                                bool widthCheckPassed = config.MaxUnderWidth < .01 || underwidth <= Math.Round(config.MaxUnderWidth, 2);
                                if (incrementCheckPassed && edgeCheckPassed && requiredLoopCheckPassed && deltaCheckPassed && widthCheckPassed)
                                {
                                    bool minEdgeToEmaCheckPassed = true;
                                    if (config.MinEdgeToEmaEnabled)
                                    {
                                        minEdgeToEmaCheckPassed = await IsPriceBelowEmaEdge(config.MinEdgeToEma, LastFillPx);
                                    }
                                    if (minEdgeToEmaCheckPassed)
                                    {
                                        bool maxEdgeToEmaBidCheckPassed = true;
                                        if (config.MaxEmaBidPercentEnabled)
                                        {
                                            maxEdgeToEmaBidCheckPassed = await IsPriceBelowEmaBidPercentEdge(config.MaxEmaBidPercent, LastFillPx);
                                        }
                                        if (maxEdgeToEmaBidCheckPassed)
                                        {
                                            bool minEmaWidthPercentEdgeToTheoCheckPassed = true;
                                            if (config.MinEmaWidthPercentEdgeToTheoCheckEnabled)
                                            {
                                                minEmaWidthPercentEdgeToTheoCheckPassed = await IsPriceBelowMinEmaWidthPercentEdgeToTheo(config.MinEmaWidthPercentEdgeToTheoCheckEdge, LastFillPx);
                                            }
                                            if (minEmaWidthPercentEdgeToTheoCheckPassed)
                                            {
                                                int size = config.Size;

                                                if (config.SizeScaleEnabled)
                                                {
                                                    if (underMid > config.SizeScaleUnderMin &&
                                                        underMid < config.SizeScaleUnderMax &&
                                                        config.SizeScaleUnderMin < config.SizeScaleUnderMax)
                                                    {
                                                        double pct = (underMid - config.SizeScaleUnderMin) / (config.SizeScaleUnderMax - config.SizeScaleUnderMin);
                                                        double factor = (1 - pct) * config.SizeScaleFactor;
                                                        int newSize = (int)(size * factor);
                                                        size = Math.Min(newSize, config.SizeScaleMax);
                                                    }
                                                }

                                                if (IsBasketOrder &&
                                                    BasketTraderViewModel != null &&
                                                    BasketTraderViewModel.IsEdgeScanFeedAutoTrader && config.MatchSignalQtyLimit > size && SizeOveride > size)
                                                {
                                                    size = Math.Min(config.MatchSignalQtyLimit, SizeOveride);
                                                }
                                                SizeOveride = 0;

                                                if (config.UnhedgeableEquityCheckEnabled && Underlying != null && !Underlying.StartsWith("$"))
                                                {
                                                    if (underwidth > OmsCore.Config.MaxHedgeWidthV2)
                                                    {
                                                        size = Math.Min((int)(size * config.UnhedgeableEquitySizePercentage), config.UnhedgeableEquityMaxSize);
                                                    }
                                                }

                                                if (automationConfigModel.LoopCountBeforeSizeup <= LoopIterationCounter)
                                                {
                                                    ResubmitAfterLastLoopCount = Math.Abs(config.ResubmitCount);
                                                    LoopIterationCounterAfterSizeup = 0;

                                                    if (PartiallyFilled)
                                                    {
                                                        size -= CumulativeQty;
                                                    }

                                                    if (size <= 0)
                                                    {
                                                        size = 1;
                                                    }

                                                    if (automationConfigModel.DynamicSizeExpansion != 1)
                                                    {
                                                        size = Math.Max(1, (int)Math.Floor(size * automationConfigModel.DynamicSizeExpansion));
                                                    }

                                                    if (savePrevSize)
                                                    {
                                                        PrevQty = Lcd;
                                                    }
                                                    ResetSize = true;

                                                    UpdateQty(size);
                                                    _log.Info("Check Loop Dynamic Size-Up. " +
                                                              "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                                              "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                                              "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                                              "Lcd Before Size-Up: " + PrevQty + ", " +
                                                              "Edge: " + edge + ", " +
                                                              "Lcd: " + Lcd + ", " +
                                                              "HardSide: " + HardSide + ", " +
                                                              "Selected Size: " + size + ", " +
                                                              "Resubmit With NoSize: " + config.ResubmitSizeOption + ", " +
                                                              "Default Size: " + config.Size + ", " +
                                                              "MatchSignalQtyLimit: " + config.MatchSignalQtyLimit + ", " +
                                                              "Id: " + SpreadId);
                                                    return config.ResubmitSizeOption;
                                                }
                                            }
                                            else
                                            {
                                                _log.Info("Failed Min Ema Width Percent Edge To Theo Check Enabled. " +
                                                          "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                                          "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                                          "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                                          "Lcd Before Size-Up: " + PrevQty + ", " +
                                                          "Under width: " + underwidth + ", " +
                                                          "Abs Delta: " + absDelta + ", " +
                                                          "Edge Check Passed: " + edgeCheckPassed + ", " +
                                                          "Required Loop Check Passed: " + requiredLoopCheckPassed + ", " +
                                                          "Delta Check Passed: " + deltaCheckPassed + ", " +
                                                          "Width Check Passed: " + widthCheckPassed + ", " +
                                                          "Config Size: " + config.Size + ", " +
                                                          "Configs: " + count + ", " +
                                                          "Config Edge: " + config.Edge + ", " +
                                                          "Config Full Edge: " + configEdge + ", " +
                                                          "Edge: " + edge + ", " +
                                                          "Lcd: " + Lcd + ", " +
                                                          "HardSide: " + HardSide + ", " +
                                                          "Last Fill Px: " + LastFillPx + ", " +
                                                          "Bid Ema: " + BidEmaAdj + ", " +
                                                          "Ask Ema: " + AskEmaAdj + ", " +
                                                          "Theo: " + NetDeltaAdjTheo + ", " +
                                                          "Width Percent: " + config.MinEmaWidthPercentEdgeToTheoCheckEdge + ", " +
                                                          "Id: " + SpreadId);
                                            }
                                        }
                                        else
                                        {
                                            _log.Info("Failed Max Ema Bid Percent Check. " +
                                                      "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                                      "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                                      "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                                      "Lcd Before Size-Up: " + PrevQty + ", " +
                                                      "Under width: " + underwidth + ", " +
                                                      "Abs Delta: " + absDelta + ", " +
                                                      "Edge Check Passed: " + edgeCheckPassed + ", " +
                                                      "Required Loop Check Passed: " + requiredLoopCheckPassed + ", " +
                                                      "Delta Check Passed: " + deltaCheckPassed + ", " +
                                                      "Width Check Passed: " + widthCheckPassed + ", " +
                                                      "Config Size: " + config.Size + ", " +
                                                      "Configs: " + count + ", " +
                                                      "Config Edge: " + config.Edge + ", " +
                                                      "Config Full Edge: " + configEdge + ", " +
                                                      "Edge: " + edge + ", " +
                                                      "Lcd: " + Lcd + ", " +
                                                      "HardSide: " + HardSide + ", " +
                                                      "Last Fill Px: " + LastFillPx + ", " +
                                                      "Max Ema Bid Percent: " + config.MaxEmaBidPercent + ", " +
                                                      "Id: " + SpreadId);
                                        }
                                    }
                                    else
                                    {
                                        _log.Info("Failed Min Edge To Ema Check. " +
                                                  "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                                  "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                                  "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                                  "Lcd Before Size-Up: " + PrevQty + ", " +
                                                  "Under width: " + underwidth + ", " +
                                                  "Abs Delta: " + absDelta + ", " +
                                                  "Edge Check Passed: " + edgeCheckPassed + ", " +
                                                  "Required Loop Check Passed: " + requiredLoopCheckPassed + ", " +
                                                  "Delta Check Passed: " + deltaCheckPassed + ", " +
                                                  "Width Check Passed: " + widthCheckPassed + ", " +
                                                  "Config Size: " + config.Size + ", " +
                                                  "Configs: " + count + ", " +
                                                  "Config Edge: " + config.Edge + ", " +
                                                  "Config Full Edge: " + configEdge + ", " +
                                                  "Edge: " + edge + ", " +
                                                  "Lcd: " + Lcd + ", " +
                                                  "HardSide: " + HardSide + ", " +
                                                  "Last Fill Px: " + LastFillPx + ", " +
                                                  "Min Edge To Ema: " + config.MinEdgeToEma + ", " +
                                                  "Id: " + SpreadId);
                                    }
                                }
                                else
                                {
                                    _log.Info("Failed Loop Dynamic Size-Up. " +
                                              "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                              "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                              "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                              "Lcd Before Size-Up: " + PrevQty + ", " +
                                              "Under width: " + underwidth + ", " +
                                              "Abs Delta: " + absDelta + ", " +
                                              "Edge Check Passed: " + edgeCheckPassed + ", " +
                                              "Required Loop Check Passed: " + requiredLoopCheckPassed + ", " +
                                              "Delta Check Passed: " + deltaCheckPassed + ", " +
                                              "Width Check Passed: " + widthCheckPassed + ", " +
                                              "Config Size: " + config.Size + ", " +
                                              "Configs: " + count + ", " +
                                              "Config Edge: " + config.Edge + ", " +
                                              "Config Full Edge: " + configEdge + ", " +
                                              "Edge: " + edge + ", " +
                                              "HardSide: " + HardSide + ", " +
                                              "Lcd: " + Lcd + ", " +
                                              "Id: " + SpreadId);
                                }
                            }
                            _log.Info("Failed Loop Dynamic Size-Up. " +
                                      "Mode: " + automationConfigModel.LoopSizeupType + ", " +
                                      "Loop Iteration Counter: " + LoopIterationCounter + ", " +
                                      "Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup + ", " +
                                      "Lcd Before Size-Up: " + PrevQty + ", " +
                                      "Edge: " + edge + ", " +
                                      "Configs: " + count + ", " +
                                      "HardSide: " + HardSide + ", " +
                                      "Lcd: " + Lcd + ", " +
                                      "Id: " + SpreadId);
                        }
                        else
                        {
                            _log.Error("Loop size-up list not found");
                        }
                    }
                    else
                    {
                        _log.Error("Loop size-up list not found");
                    }
                    break;
            }

            return ResubmitSizeOption.Off;
        }

        #endregion

        private void ReverseWhileMaintainingState()
        {
            try
            {
                lock (_legUpdateLock)
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        leg.LegUpdatedEvent -= UpdateTicketValues;
                    }
                    foreach (TicketLegModel leg in Legs)
                    {
                        leg.Reverse();
                    }
                }

                foreach (TicketLegModel leg in Legs)
                {
                    leg.LegUpdatedEvent += UpdateTicketValues;
                }
                UpdateTicketValues();
                EvaluationResult result = EvaluateStrategy();

                (LastExchange, LastContraExchange) = (LastContraExchange, LastExchange);
                (BestBuyEdgeToTheo, BestSellEdgeToTheo) = (BestSellEdgeToTheo, BestBuyEdgeToTheo);
                (WorstBuyEdgeToTheo, WorstSellEdgeToTheo) = (WorstSellEdgeToTheo, WorstBuyEdgeToTheo);
                (LastMainUnderPriceAtFill, LastContraUnderMidAtFill) = (LastContraUnderMidAtFill, LastMainUnderPriceAtFill);
                (LastMainUnderMidAtFill, LastContraUnderPriceAtFill) = (LastContraUnderPriceAtFill, LastMainUnderMidAtFill);
                (LastFillAdjTheo, LastContraFillAdjTheo) = (LastContraFillAdjTheo, LastFillAdjTheo);
                (LastMainTotalVolumeAtFill, LastContraTotalVolumeAtFill) = (LastContraTotalVolumeAtFill, LastMainTotalVolumeAtFill);
                (LastQuantity, ContraLastQuantity) = (ContraLastQuantity, LastQuantity);
                (Status, ContraStatus) = (ContraStatus, Status);
                (MainOrderStatus, ContraOrderStatus) = (ContraOrderStatus, MainOrderStatus);
                (StatusMode, ContraStatusMode) = (ContraStatusMode, StatusMode);
                (AveragePrice, ContraAveragePrice) = (ContraAveragePrice, AveragePrice);
                (Filled, ContraFilled) = (ContraFilled, Filled);
                (PartiallyFilled, ContraPartiallyFilled) = (ContraPartiallyFilled, PartiallyFilled);
                (CumulativeQty, ContraCumulativeQty) = (ContraCumulativeQty, CumulativeQty);
                (LeavesQty, ContraLeavesQty) = (ContraLeavesQty, LeavesQty);
                (LastFillPx, LastContraFillPx) = (LastContraFillPx, LastFillPx);
                if (!string.IsNullOrWhiteSpace(ContraRoute))
                {
                    (Route, ContraRoute) = (ContraRoute, Route);
                }
                Description = result.Description;
                SpreadId = result.GeneralDescription;
                SpreadType = result.BaseType;
                Symbol = GetTosFromLegs(Legs.ToList());
                SetSides();
                SetSpreadSymbol();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseWhileMaintainingState));
            }
        }

        public override void ShowAlert()
        {
            AlertTriggered = true;
            Task.Delay(OmsCore.Config.BasketAlertTimeout).ContinueWith(t => AlertTriggered = false);
        }

        protected override bool TrySendToMatrix()
        {
            if (BasketSettings.UseMatrixAlgo)
            {
                switch (BasketSettings.MatrixStrategy)
                {
                    case MatrixStrategy.Scrape:
                        SendMatrixScrape(BasketSettings.MatrixStrategyConfigModel.ScrapeStrategyData);
                        return true;
                    case MatrixStrategy.Seeker:
                        if (IsSingleLeg)
                        {
                            SendMatrixSeeker(BasketSettings.MatrixStrategyConfigModel.SeekerStrategyData);
                        }
                        else
                        {
                            SendMatrixSeekerSpread(BasketSettings.MatrixStrategyConfigModel.SeekerSpreadStrategyData);
                        }
                        return true;
                    case MatrixStrategy.Synthetic:
                        SendMatrixSyntheticSpread(BasketSettings.MatrixStrategyConfigModel.SyntheticSpreadStrategyData);
                        return true;
                    default:
                        return false;
                }
            }

            return CheckForMatrixRoute();
        }

        internal void ToggleTheoLock()
        {
            if (BasketSettings.LockTheos)
            {
                LockedTheo = double.NaN;
                LockedDeltaAdjTheo = double.NaN;
                foreach (var leg in Legs)
                {
                    leg.LockedDeltaAdjTheo = double.NaN;
                    switch (BasketSettings.TheoModel)
                    {
                        case TheoModel.Hanw:
                            leg.LockedTheo = leg.DeltaAdjTheo;
                            leg.LockedTheoUnderlying = leg.AdjTheoUnderlying;
                            break;
                        case TheoModel.VolaV0:
                            leg.LockedTheo = leg.VolaTheoAdjV0;
                            leg.LockedTheoUnderlying = leg.AdjTheoUnderlying;
                            break;
                        default:
                            leg.LockedTheo = double.NaN;
                            leg.LockedTheoUnderlying = double.NaN;
                            break;
                    }
                    leg.SubscribeToTopQuote();
                }
            }
            else
            {
                foreach (var leg in Legs)
                {
                    leg.LockedTheo = double.NaN;
                    leg.LockedTheoUnderlying = double.NaN;
                    leg.LockedDeltaAdjTheo = double.NaN;
                    leg.UnsubscribeTopQuote();
                }
                LockedTheo = double.NaN;
                LockedDeltaAdjTheo = double.NaN;
            }
        }

        protected override void DeltaAdjTheoLoaded()
        {
            base.DeltaAdjTheoLoaded();
            if (BasketSettings.LockTheos && double.IsNaN(LockedTheo))
            {
                ToggleTheoLock();
            }
        }

        internal override async Task<GetTheoResult> GetTheoAsync(TheoModel? theoModel = null, bool checkForLockedTheo = false, bool fetchNewUpdate = false)
        {
            theoModel ??= BasketSettings.TheoModel;

            if (checkForLockedTheo && BasketSettings is { LockTheos: true })
            {
                switch (theoModel)
                {
                    case TheoModel.Hanw:
                    case TheoModel.VolaV0:
                        return new GetTheoResult(LockedTheo, LockedDeltaAdjTheo);
                    default:
                        return new GetTheoResult(double.NaN, double.NaN);
                }
            }

            switch (theoModel)
            {
                case TheoModel.Hanw when fetchNewUpdate:
                    {
                        PricingResponseModel result = await GetFreshPricesAsync();
                        return new GetTheoResult(result.HwTheo, result.HwAdjTheo);
                    }
                case TheoModel.VolaV0 when fetchNewUpdate:
                    {
                        PricingResponseModel result = await GetFreshPricesAsync();
                        return new GetTheoResult(result.VolaTheo, result.VolaAdjTheo);
                    }
                case TheoModel.Hanw:
                    return new GetTheoResult(NetTheo, NetDeltaAdjTheo);
                case TheoModel.VolaV0:
                    return new GetTheoResult(VolaTheoV0, VolaTheoAdjV0);
                case TheoModel.VolaV1:
                    return new GetTheoResult(VolaTheoV1, VolaTheoAdjV1);
                case TheoModel.VolaV2:
                    return new GetTheoResult(VolaTheoV2, VolaTheoAdjV2);
                case TheoModel.VolaV3:
                    return new GetTheoResult(VolaTheoV3, VolaTheoAdjV3);
                default:
                    return new GetTheoResult(double.NaN, double.NaN);
            }
        }

        public override InstanceMode GetInstanceMode()
        {
            return BasketTraderViewModel.InstanceModeLocked ? OmsCore.Config.InstanceModeV3 : InstanceMode;
        }

        protected override void MidUpdated(double prevValue, double newValue)
        {
            if (BasketSettings.UseBidPercent &&
                BasketSettings.ModifyPxWithMktChange &&
                BasketSettings.AdjustPriceBeforeSubmit &&
                MainResting &&
                !OmsCore.Config.NonAutoCancelRoutes.Contains(Route) &&
                (DateTime.Now - _lastAutoModified).TotalMilliseconds > GetMinRestPeriod() &&
                Math.Abs(prevValue - newValue) > .01)
            {
                _lastAutoModified = DateTime.Now;
                SetEdgeAsync().ContinueWith(t =>
                {
                    if (MainResting && !double.IsNaN(LastPx) && Math.Abs(LastPx - Price) > PX_TOLERANCE)
                    {
                        ModifyAsync();
                    }
                });
            }
        }
    }
}
