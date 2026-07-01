using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    [POCOViewModel]
    public partial class ComplexOrderTicketViewModel : OrderTicket, IModuleViewModel
    {
        public event ReadyEventHandler Ready;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private string _moduleTitle = Module.ComplexOrderTicket.ToString().FromCamelCase();
        private DispatcherTimer _uiUpdateTimer;
        private bool _promptVisible;
        private bool _showDepthBook;
        private DispatcherTimer _submitWithDelayCountdownTimer;
        private int _remainingForSubmitWithDelay;
        private readonly Dictionary<string, double> _legSymbolToVolumeMap = new();
        private int _positionSet;
        private DelegateCommand _openInNagbotBasketTraderCommand;
        private DelegateCommand<object> _sendToNagBotCommand;
        private readonly Dictionary<string, string> _routeLookup = new()
        {
            {"AMEX","BAMEX" },
            {"NYSEARCA","BARCA" },
            {"BATS","BBATS" },
            {"C2","BC2" },
            {"CBOE","BCBOE" },
            {"EDGX","BEDGX" },
            {"EMLD","BEMLD" },
            {"GMNI","BGMNI" },
            {"ISE","BISE" },
            {"MCRY","BMCRY" },
            {"MIAX","BMIAX" },
            {"PEARL","BPEARL" },
            {"NASDAQ","BNASDAQ" },
            {"NQBX","BNQBX" },
            {"PHLX","BPHLX" },
        };

        private readonly IModuleFactory _moduleFactory;
        private readonly DelayedTicketsManager _delayedTicketsManager;


        private readonly DepthItemComparer _askComparer = new(false);
        private readonly DepthItemComparer _bidComparer = new(true);
        private readonly HashSet<IOrder> _buyOrders = new();
        private readonly HashSet<IOrder> _sellOrders = new();
        private bool _updateBids;
        private bool _updateAsks;
        private RbboUpdateModel _rbboUpdate;

        public DominatorsManagerModel DominatorsManagerModel { get; set; }
        public BasketGroupManagerModel BasketGroupManagerModel { get; set; }

        public FastObservableCollection<IDynamicConfigModel> MatrixConfigModels { get; } = [];
        public IEnumerable<Venue> Venues { get; } = Enum.GetValues<Venue>().Where(x => x is not ZeroPlus.Models.Data.Enums.Venue.Matrix and not ZeroPlus.Models.Data.Enums.Venue.OPS);
        public IEnumerable<Side> SideOptions { get; } = Enum.GetValues<Side>();
        public IEnumerable<InstanceMode> InstanceModes { get; } = Enum.GetValues<InstanceMode>();
        public IEnumerable<MatrixStrategy> MatrixStrategies { get; } = Enum.GetValues<MatrixStrategy>();
        private IVerificationService VerificationService => GetService<IVerificationService>();
        private IUpdateSummaryService UpdateSummaryService => GetService<IUpdateSummaryService>();

        protected override bool ConformBuySide => _showDepthBook;
        public DateTime CreationTime { get; set; }
        public string Uid { get; internal set; }
        public bool IsReady { get; private set; }
        public string ModuleTitle
        {
            get => _moduleTitle ?? _moduleTitle;
            set => SetValue(ref _moduleTitle, value);
        }
        [Bindable(Default = true)]
        public partial bool AllowSave { get; set; }
        [Bindable]
        public partial bool EnablePriceTrackBar { get; set; }
        [Bindable]
        public partial bool ShowQuickRoutes { get; set; }
        [Bindable]
        public partial bool ShowSubmitWithDelayPanel { get; set; }
        [Bindable]
        public partial bool ShowStopLossPanel { get; set; }
        [Bindable]
        public partial bool ShowSpeedTrader { get; set; }
        [Bindable]
        public partial bool EnableControlPxKey { get; set; }
        [Bindable]
        public partial double TemplateEdgeToTheo { get; set; }
        [Bindable]
        public partial bool EdgeToTheoLocked { get; set; }
        [Bindable]
        public partial ObservableCollection<Tuple<string, string>> QuickRoutes { get; set; }
        [Bindable]
        public partial TimeSpan SubmitWithDelayCountDown { get; set; }
        [Bindable]
        public partial int SubmitWithDelayInterval { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayPercentBidEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayPercentBid { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayTheoReferenceEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayEdgeToTheo { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayDeltaAdjustEnabled { get; set; }
        [Bindable]
        public partial Side SubmitWithDelaySide { get; set; }
        [Bindable]
        public partial double SubmitWithDelayDeltaAdjLevel { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayBidRangeEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMinBid { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMaxBid { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayAskRangeEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMinAsk { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMaxAsk { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayPriceRangeEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMinPrice { get; set; }
        [Bindable]
        public partial double SubmitWithDelayMaxPrice { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayCancelOnUserPositionChangeEnabled { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayDeltaAdjCurrentPxEnabled { get; set; }
        [Bindable]
        public partial double SubmitWithDelayDeltaAdjCurrentPxEdge { get; set; }
        [Bindable]
        public partial double SubmitWithDelayDeltaAdjCurrentPx { get; set; }
        [Bindable]
        public partial double SubmitWithDelayDeltaAdjCurrentUnderlying { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayCancelOnLegVolumeChangeEnabled { get; set; }
        [Bindable]
        public partial int SubmitWithDelayCancelOnVolumeChange { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayPlayPreSubmitNotification { get; set; }
        [Bindable]
        public partial int SubmitWithDelayPreSubmitNotificationSeconds { get; set; }
        [Bindable]
        public partial string SubmitWithDelayMessage { get; set; }
        [Bindable]
        public partial bool ShowMatrixAlgoPanel { get; set; }
        [Bindable]
        public partial bool UseMatrixAlgo { get; set; }
        [Bindable]
        public partial MatrixStrategy MatrixStrategy { get; set; }
        [Bindable]
        public partial IDynamicConfigModel MatrixConfigModel { get; set; }
        public MatrixStrategyConfigModel MatrixStrategyConfigModel { get; set; }
        public int SyntheticSpreadConfigModelId { get; set; }
        public bool ShowDepthBook
        {
            get => _showDepthBook;
            set
            {
                if (_showDepthBook != value)
                {
                    SetValue(ref _showDepthBook, value);
                    if (value)
                    {
                        ContraQtyLocked = false;
                        SubscribeDepthBook();
                    }
                    else
                    {
                        UnsubscribeDepthBook();
                    }
                }
            }
        }
        public FastObservableCollection<DepthItemModel> AskDepthItems { get; set; } = [];
        public FastObservableCollection<DepthItemModel> BidDepthItems { get; set; } = [];
        public ICommand OpenInNagbotBasketTraderCommand
        {
            get
            {
                _openInNagbotBasketTraderCommand ??= new DelegateCommand(OpenInNagBasketTrader);
                return _openInNagbotBasketTraderCommand;
            }
        }
        public ICommand SendToNagBotCommand
        {
            get
            {
                _sendToNagBotCommand ??= new DelegateCommand<object>(SendToNagBot);
                return _sendToNagBotCommand;
            }
        }

        public ComplexOrderTicketViewModel(IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
                                           IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
                                           IAbstractFactory<RouteSelectionViewModel> routeSelectionViewFactory,
                                           TransactionConsumerModel transactionConsumer,
                                           DominatorsManagerModel dominatorsManagerModel,
                                           NotificationManager notificationManager,
                                           PortfolioManagerModel portfolioManagerModel,
                                           BasketGroupManagerModel basketGroupManagerModel,
                                           OmsCore oms,
                                           IModuleFactory moduleFactory,
                                           DelayedTicketsManager delayedTicketsManager = null)
            : base(ticketFactory,
                   threeWayCloserFactory,
                   routeSelectionViewFactory,
                   transactionConsumer,
                   notificationManager,
                   portfolioManagerModel,
                   oms)
        {
            _moduleFactory = moduleFactory;
            DominatorsManagerModel = dominatorsManagerModel;
            _delayedTicketsManager = delayedTicketsManager;
            BasketGroupManagerModel = basketGroupManagerModel;
            SubType = OrderSubType.Ticket;
            QuickRoutes = new ObservableCollection<Tuple<string, string>>();
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
            OmsCore.Config.ConfigChangedEvent += OnConfigChangedEvent;
            OnConfigChangedEvent(OmsCore.Config, false);
            TemplateEdgeToTheo = double.NaN;
            EdgeToTheoLocked = false;
            CreationTime = DateTime.Now;
            SubmitWithDelayResetQtyEnabled = true;
            SubmitWithDelayPlayPreSubmitNotification = true;
            SubmitWithDelayPreSubmitNotificationSeconds = 10;
            SubmitWithDelayInterval = 15;

            StartUiUpdateTimer();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            Dispatcher?.Invoke(() =>
            _submitWithDelayCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            });
            _submitWithDelayCountdownTimer.Tick += (_, a) => OnSubmitWithDelayTimerTick();
            OnConfigChangedEvent(OmsCore.Config, false);
        }

        private void StartUiUpdateTimer()
        {
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(OmsCore.Config.TicketUiUpdateInterval),
            };
            _uiUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _uiUpdateTimer.Tick += (_, _) => UpdateUiProperties();
            _uiUpdateTimer.Start();
        }

        public override void Dispose()
        {
            isDisposing = true;
            _log.Info(nameof(Dispose) + " Disposing order model for " + SpreadId);
            base.Dispose();
            _uiUpdateTimer?.Stop();
            OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
            OmsCore.Config.ConfigChangedEvent -= OnConfigChangedEvent;
        }

        public override void ShowMessage(string message, string title, bool canBeSilenced = true)
        {
            try
            {
                if (OmsCore.Config.SilentTicketNotifications && canBeSilenced)
                {
                    if (TicketStyle == OrderTicketStyle.Complex)
                    {
                        ContraStatus = message;
                        ContraStatusMode = StatusMode.NewSell;
                    }
                    else
                    {
                        Status = message;
                        StatusMode = StatusMode.NewSell;
                    }
                }
                else
                {
                    if (!_promptVisible)
                    {
                        if (Dispatcher != null && MessageBoxService != null)
                        {
                            _promptVisible = true;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                MessageBoxService?.ShowMessage(message, title ??= "ZeroPlus OMS", MessageButton.OK, MessageIcon.Information);
                                _promptVisible = false;
                            }));
                        }
                    }
                }
                _log.Info("Message: " + message + ", Title: " + title);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowMessage));
                _promptVisible = false;
            }
        }

        protected override async Task ProcessAutomation(OrderUpdateModel execReport, DateTime receiveTime, OrderUpdateValues orderUpdateValues, bool isMainOrder, bool isContraOrder)
        {
            if (CxlReplaceCloser.Enabled && isContraOrder)
            {
                if (IsLooping && (orderUpdateValues.OrderStatus is OrderStatus.New or OrderStatus.Replaced))
                {
                    _ = CxlReplaceCloser.ContClose();
                }
                else if (orderUpdateValues.OrderStatus is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.PartiallyFilled or OrderStatus.Rejected || execReport.ExecutionType.IsFilled())
                {
                    IsLooping = false;
                }
            }
            else if (Closer.Enabled && isContraOrder)
            {
                if (IsLooping && (orderUpdateValues.OrderStatus is OrderStatus.Canceled or OrderStatus.Rejected))
                {
                    int leavesQty = Math.Abs(execReport.LeavesQty);
                    Closer.ContClose(leavesQty);
                }
                else if (orderUpdateValues.OrderStatus is OrderStatus.Filled or OrderStatus.PartiallyFilled or OrderStatus.Rejected || execReport.ExecutionType.IsFilled())
                {
                    IsLooping = false;
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
            else if (orderUpdateValues.OrderStatus == OrderStatus.Filled && SpeedTraderClosingType != SpeedTraderClosingType.Off)
            {
                int filledQty = Math.Abs(execReport.LastQty);
                if (isMainOrder)
                {
                    LastFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastFillAdjTheo = NetDeltaAdjTheo;
                    if (Status == execReport.OrderStatus.ToString())
                    {
                        _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                    }
                    else
                    {
                        if (SpeedTraderClosingType == SpeedTraderClosingType.Loop)
                        {
                            if (PartiallyFilled)
                            {
                                UpdateQty(CumulativeQty + filledQty);
                                CumulativeQty = 0;
                                PartiallyFilled = false;
                                LeavesQty = 0;
                            }
                            Looper.StartClosingLoop(receiveTime);
                        }
                        else
                        {
                            int qty = Math.Abs(execReport.CumQty);
                            Closer.StartCloser(orderUpdateValues.AveragePrice,
                                qty,
                                ContraFishEdge,
                                LoopMaxLoss,
                                ContraFishPriceIncrement,
                                ContraFishInterval);
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
                    if (ContraOrderStatus == orderUpdateValues.OrderStatus)
                    {
                        LastEdge = double.NaN;
                        IsLooping = false;
                        _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                    }
                    else
                    {
                        if (SpeedTraderClosingType == SpeedTraderClosingType.Loop)
                        {
                            if (ContraPartiallyFilled)
                            {
                                UpdateQty(ContraCumulativeQty + filledQty);
                                ContraCumulativeQty = 0;
                                ContraPartiallyFilled = false;
                                ContraLeavesQty = 0;
                            }
                            LoopIterationCounterAfterSizeup++;
                            if (LoopIterationCounter++ >= SpeedTraderMaxLoopCount)
                            {
                                LastEdge = double.NaN;
                                DeltaAdjLastEdge = double.NaN;
                                IsLooping = false;
                                Looper.RemoveFromLoopInstances();
                            }
                            else
                            {
                                if (LastContraFillPx > 0 && LastFillPx > 0 && IsSingleLeg)
                                {
                                    if (!Side.HasValue)
                                    {
                                        _log.Info(nameof(HandleExecutionReport) + " Invalid edge." +
                                                  " Id: " + SpreadId + "," +
                                                  " Last Fill Px: " + LastFillPx + "," +
                                                  " Last Contra Fill Px: " + LastContraFillPx + ",");
                                        LastEdge = double.NaN;
                                        DeltaAdjLastEdge = double.NaN;
                                        IsLooping = false;
                                        Looper.RemoveFromLoopInstances();
                                    }
                                    else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        LastEdge = LastContraFillPx - LastFillPx;
                                    }
                                    else
                                    {
                                        LastEdge = LastFillPx - LastContraFillPx;
                                    }
                                }
                                else if (LastContraFillPx < 0 && LastFillPx > 0)
                                {
                                    LastEdge = Math.Abs(LastContraFillPx) - LastFillPx;
                                }
                                else if (LastFillPx < 0 && LastContraFillPx > 0)
                                {
                                    LastEdge = Math.Abs(LastFillPx) - LastContraFillPx;
                                }
                                else if (LastFillPx < 0 && LastContraFillPx < 0)
                                {
                                    LastEdge = Math.Abs(LastFillPx + LastContraFillPx);
                                }
                                else
                                {
                                    _log.Info(nameof(HandleExecutionReport) + " Invalid edge." +
                                              " Id: " + SpreadId + "," +
                                              " Last Fill Px: " + LastFillPx + "," +
                                              " Last Exch: " + LastExchange + "," +
                                              " Exchanges: " + Exchanges + "," +
                                              " Last Contra Exch: " + LastContraExchange + "," +
                                              " Last Contra Fill Px: " + LastContraFillPx + ",");
                                    LastEdge = double.NaN;
                                    DeltaAdjLastEdge = double.NaN;
                                    IsLooping = false;
                                    Looper.RemoveFromLoopInstances();
                                }

                                if (!double.IsNaN(LastEdge))
                                {
                                    NotionalLastEdge = LastEdge;
                                    if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        LastBuyEdge = LastEdge;
                                    }
                                    else
                                    {
                                        LastSellEdge = LastEdge;
                                    }
                                }

                                if ((LastEdge > 0 && double.IsNaN(BestEdge)) ||
                                    LastEdge > BestEdge)
                                {
                                    BestEdge = LastEdge;
                                }

                                if (!TrySelectRoute(isContra: true, lookupOnly: true, out string contraRoute, out _))
                                {
                                    contraRoute = string.IsNullOrWhiteSpace(ContraRoute) ? Route : ContraRoute;
                                }
                                if (!TrySelectRoute(isContra: false, lookupOnly: true, out string route, out _))
                                {
                                    route = Route;
                                }

                                double fees = GetTotalFeesForTicket(route) + GetTotalFeesForTicket(contraRoute, reverse: true);

                                if (LastEdge - fees >= LoopMinEdge)
                                {
                                    Looper.StartLoop(receiveTime);
                                }
                                else
                                {
                                    LastEdge = double.NaN;
                                    DeltaAdjLastEdge = double.NaN;
                                    IsLooping = false;
                                    Looper.RemoveFromLoopInstances();
                                    NotifyOrderCloseWaitHandlers(true, null);
                                    _log.Info($"{nameof(HandleExecutionReport)} Edge below min setting. " +
                                              $"Id: {SpreadId}, " +
                                              $"Last Edge: {LastEdge:f2}, " +
                                              $"Fees: {fees:f2}, " +
                                              $"Last Fill Px: {LastFillPx:f2}, " +
                                              $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                                              $"Last Exch: {LastExchange}, " +
                                              $"Exchanges: {Exchanges}, " +
                                              $"Last Contra Exch: {LastContraExchange}");
                                }
                            }
                        }
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled &&
                     PartiallyFilled &&
                     SpeedTraderClosingType != SpeedTraderClosingType.Off &&
                     isMainOrder)
            {
                if (MainOrderStatus == orderUpdateValues.OrderStatus)
                {
                    _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                }
                else
                {
                    if (CloseStyle == Enums.CloseStyle.OutOfMarketLoop)
                    {
                        UpdateQty(CumulativeQty);
                        CumulativeQty = 0;
                        PartiallyFilled = false;
                        LeavesQty = 0;
                        LegOutCloser?.ClosePosition(Lcd);
                    }
                    else if (SpeedTraderClosingType == SpeedTraderClosingType.Loop)
                    {
                        UpdateQty(CumulativeQty);
                        CumulativeQty = 0;
                        PartiallyFilled = false;
                        LeavesQty = 0;
                        Looper.StartClosingLoop(receiveTime);
                    }
                    else if (SpeedTraderClosingType == SpeedTraderClosingType.Close)
                    {
                        int qty = Math.Abs(execReport.CumQty);
                        Closer.StartCloser(orderUpdateValues.AveragePrice,
                            qty,
                            ContraFishEdge,
                            LoopMaxLoss,
                            ContraFishPriceIncrement,
                            ContraFishInterval);
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled &&
                     ContraPartiallyFilled &&
                     SpeedTraderClosingType != SpeedTraderClosingType.Off &&
                     isContraOrder)
            {
                if (ContraOrderStatus == orderUpdateValues.OrderStatus)
                {
                    _log.Warn(nameof(HandleExecutionReport) + " Possible duplicate status update, stopping automation.");
                }
                else
                {
                    if (SpeedTraderClosingType == SpeedTraderClosingType.Loop)
                    {
                        UpdateQty(ContraLeavesQty);
                        ContraCumulativeQty = 0;
                        ContraPartiallyFilled = false;
                        ContraLeavesQty = 0;
                    }
                }
            }
            else if (orderUpdateValues.OrderStatus == OrderStatus.Canceled)
            {
                if (SpeedTraderClosingType == SpeedTraderClosingType.Loop && IsLooping)
                {
                    if (isMainOrder)
                    {
                        await Looper.ContLoopAsync(receiveTime);
                    }
                    else if (isContraOrder)
                    {
                        Looper.ContClose(receiveTime);
                    }
                }
            }
            else if (execReport.ExecutionType == ExecutionType.PartiallyFilled || execReport.ExecutionType == ExecutionType.Trade)
            {
                int filledQty = Math.Abs(execReport.LastQty);
                int cumulativeQty = Math.Abs(execReport.CumQty);
                int leavesQty = Math.Abs(execReport.LeavesQty);
                if (isMainOrder)
                {
                    LastFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastFillAdjTheo = NetDeltaAdjTheo;
                    PartiallyFilled = true;
                    CumulativeQty += filledQty;
                    LeavesQty = leavesQty;
                    double fillPercent = (double)CumulativeQty / Lcd;
                    _log.Info("Ticket partial fill received. [Open] " +
                              "Loop enabled: " + SpeedTraderClosingType + ", " +
                              "Spread ID: " + SpreadId + ", " +
                              "Last fill px: " + LastFillPx + ", " +
                              "Last fill qty: " + filledQty + ", " +
                              "Last order cumulative qty: " + cumulativeQty + ", " +
                              "Leaves qty: " + leavesQty + ", " +
                              "Total cumulative: " + CumulativeQty + ", " +
                              "Filled percent: " + fillPercent + ".");
                }
                else if (isContraOrder)
                {
                    LastContraFillPx = orderUpdateValues.AveragePrice;
                    LastFillUnderBidPx = UnderBid;
                    LastFillUnderPx = UnderMid;
                    LastFillUnderAskPx = UnderAsk;
                    LastContraFillAdjTheo = NetDeltaAdjTheo;
                    ContraPartiallyFilled = true;
                    ContraCumulativeQty += filledQty;
                    ContraLeavesQty = leavesQty;
                    double fillPercent = (double)ContraCumulativeQty / Lcd;
                    _log.Info("Ticket partial fill received. [Close] " +
                              "Loop enabled: " + SpeedTraderClosingType + ", " +
                              "Spread ID: " + SpreadId + ", " +
                              "Last fill px: " + LastContraFillPx + ", " +
                              "Last fill qty: " + filledQty + ", " +
                              "Last order cumulative qty: " + cumulativeQty + ", " +
                              "Leaves qty: " + leavesQty + ", " +
                              "Total cumulative: " + ContraCumulativeQty + ", " +
                              "Filled percent: " + fillPercent + ".");
                }

                if (OmsCore.Config.TicketCancelOnPartialFill)
                {
                    RequestCancel(execReport);
                }
            }
        }

        public override async Task<bool> GetVerificationAsync(string message, string title)
        {
            bool ok = false;
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                ok = MessageBoxService?.Show(message,
                                             title,
                                             MessageButton.YesNo,
                                             MessageIcon.Exclamation,
                                             MessageResult.No) == MessageResult.Yes;
            }));
            return ok;
        }

        public override RiskWarningMessageResponse GetRiskVerification(string message, string title)
        {
            RiskWarningMessageResponse response = RiskWarningMessageResponse.CancelAll;
            Dispatcher?.Invoke(() =>
            {
                response = VerificationService.GetRiskVerification(message, title);
            });
            return response;
        }

        protected override void UpdateSummary()
        {
            UpdateSummaryService?.UpdateSummary();
        }
        #region Commands

        [Command]
        public void LoadQuickRoute(string parameter)
        {
            try
            {
                Route = parameter;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadQuickRoute));
            }
        }

        [Command]
        public void LoadContraQuickRoute(string parameter)
        {
            try
            {
                ContraRoute = parameter;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadContraQuickRoute));
            }
        }

        [Command]
        public void ShowSpeedTraderValueChecked()
        {
            if (ShowSpeedTrader)
            {
                double defaultContraEdge = GetDefaultContraEdge();
                ContraRoute = Route;
                ContraFishEdge = defaultContraEdge;
                LoopMinEdge = defaultContraEdge;
                ContraFishPriceIncrement = 0.01;
                ContraFishInterval = 1100;
            }
            else
            {
                SpeedTraderClosingType = SpeedTraderClosingType.Off;
            }
        }

        [Command]
        public void FlatCommand()
        {
            try
            {
                CancelSpeedTrader();
                if (ShowDepthBook && IsSingleLeg)
                {
                    TicketLegModel leg = Legs[0];

                    if (leg.NetQty >= 0)
                    {
                        Lcd = leg.Quantity;
                        Qty = Lcd;
                        ContraQty = 1;
                    }
                    else
                    {
                        Lcd = 1;
                        Qty = Lcd;
                        ContraQty = leg.Quantity;
                    }

                    return;
                }
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Single:
                        if (EdgeProjector != null && OmsCore.Config.ForThreeWayTicketsUseThreeWayFlat)
                        {
                            IEnumerable<TicketLegModel> posLegs = Legs.Where(x => !double.IsNaN(Math.Abs(x.UserNetQty)) && Math.Abs(x.UserNetQty) != 0)
                                              .Where(x => (x.Side == ZeroPlus.Models.Data.Enums.Side.Buy && x.UserNetQty < 0) || (x.Side == ZeroPlus.Models.Data.Enums.Side.Sell && x.UserNetQty > 0));
                            TicketLegModel lcdLeg = posLegs.OrderBy(x => x.UserNetQty).FirstOrDefault();
                            if (lcdLeg != null)
                            {
                                int lcd = Math.Abs(Convert.ToInt32(Math.Floor(lcdLeg.UserNetQty) / lcdLeg.Ratio));
                                bool flatWithLcd = lcd != 0;
                                if (flatWithLcd)
                                {
                                    foreach (TicketLegModel leg in Legs)
                                    {
                                        leg.Flat(lcd * leg.Ratio);
                                    }
                                    UpdateLCD();
                                    UpdateDescription();
                                    UpdateTicketValues();
                                    ValidateTicket();
                                    return;
                                }
                            }
                        }
                        if (Legs.Any(x => double.IsNaN(x.NetQty) || x.NetQty == 0))
                        {
                            foreach (TicketLegModel leg in Legs)
                            {
                                leg.Flat(leg.Ratio);
                            }
                        }
                        else
                        {
                            foreach (TicketLegModel leg in Legs)
                            {
                                leg.Flat();
                            }
                        }

                        break;
                    default:
                        if (EdgeProjector != null && OmsCore.Config.ForThreeWayTicketsUseThreeWayFlat)
                        {
                            IEnumerable<TicketLegModel> posLegs = null;
                            if (SuggestTradingMain)
                            {
                                posLegs = Legs.Where(x => !double.IsNaN(Math.Abs(x.UserNetQty)) && Math.Abs(x.UserNetQty) != 0)
                                              .Where(x => (x.Side == ZeroPlus.Models.Data.Enums.Side.Buy && x.UserNetQty < 0) || (x.Side == ZeroPlus.Models.Data.Enums.Side.Sell && x.UserNetQty > 0));
                            }
                            else if (SuggestTradingContra)
                            {
                                posLegs = Legs.Where(x => !double.IsNaN(Math.Abs(x.UserNetQty)) && Math.Abs(x.UserNetQty) != 0)
                                              .Where(x => (x.Side == ZeroPlus.Models.Data.Enums.Side.Sell && x.UserNetQty < 0) || (x.Side == ZeroPlus.Models.Data.Enums.Side.Buy && x.UserNetQty > 0));
                            }

                            TicketLegModel lcdLeg = posLegs?.OrderBy(x => x.UserNetQty).FirstOrDefault();
                            if (lcdLeg != null)
                            {
                                int lcd = Math.Abs(Convert.ToInt32(Math.Floor(lcdLeg.UserNetQty) / lcdLeg.Ratio));
                                bool flatWithLcd = lcd != 0;
                                if (flatWithLcd)
                                {
                                    foreach (TicketLegModel leg in Legs)
                                    {
                                        leg.Flat(lcd * leg.Ratio);
                                    }
                                    UpdateLCD();
                                    UpdateDescription();
                                    UpdateTicketValues();
                                    ValidateTicket();
                                    return;
                                }
                            }
                        }
                        UpdateQty(LcdPosition == 0 ? 1 : Math.Abs(LcdPosition));
                        break;
                }
                UpdateLCD();
                UpdateDescription();
                UpdateTicketValues();
                ValidateTicket();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FlatCommand));
            }
        }

        [Command]
        public void Clone(object parameter)
        {
            try
            {
                CancelSpeedTrader();

                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        switch (TicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Single:
                                window = new OrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Dual:
                                window = new DualOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                        }

                        ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        viewModel.InstanceMode = InstanceMode;
                        viewModel.BrokerOverride = BrokerOverride;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        _ = viewModel.LoadFromTicketAsync(this, false, true);
                        if (parameter is object[] values)
                        {
                            try
                            {
                                double width = (double)values[0];
                                double height = (double)values[1];
                                double left = (double)values[2];
                                double top = (double)values[3];
                                window.Loaded += (s, e) =>
                                {
                                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                                    window.Width = width;
                                    window.Height = height;
                                    window.Left = left;
                                    window.Top = top - height;
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(Clone));
                            }
                        }
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                        windowView.Loaded += (sender, args) =>
                        {
                            viewModel.SetModule(Module.ComplexOrderTicketLayout);
                        };
                        break;
                    case OrderTicketStyle.Combined:
                        windowView.Loaded += (sender, args) =>
                        {
                            viewModel.SetModule(Module.CombinedOrderTicketLayout);
                        };
                        break;
                    case OrderTicketStyle.Single:
                        windowView.Loaded += (sender, args) =>
                        {
                            viewModel.SetModule(Module.OrderTicketLayout);
                        };
                        break;
                    case OrderTicketStyle.Dual:
                        windowView.Loaded += (sender, args) =>
                        {
                            viewModel.SetModule(Module.OrderTicketLayout);
                        };
                        break;
                }

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void Contra(object parameter)
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) && (TicketStyle == OrderTicketStyle.Complex || TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual))
                {
                    CancelSpeedTrader();
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        try
                        {
                            switch (TicketStyle)
                            {
                                case OrderTicketStyle.Single:
                                    window = new OrderTicketView()
                                    {
                                        Contra = true
                                    };
                                    break;
                                case OrderTicketStyle.Dual:
                                    window = new DualOrderTicketView()
                                    {
                                        Contra = true
                                    };
                                    break;
                                case OrderTicketStyle.Complex:
                                    window = new ComplexOrderTicketView()
                                    {
                                        Contra = true
                                    };
                                    break;
                            }

                            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                            viewModel.InstanceMode = InstanceMode;
                            viewModel.BrokerOverride = BrokerOverride;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            _ = viewModel.LoadContraFromTemplateAsync(this);
                            if (parameter is not null and object[] values)
                            {
                                try
                                {
                                    double width = (double)values[0];
                                    double height = (double)values[1];
                                    double left = (double)values[2];
                                    double top = (double)values[3];
                                    Window contraSourceWindow = (Window)values[4];
                                    window.Loaded += (s, e) =>
                                    {
                                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                                        window.Width = width;
                                        window.Height = height;
                                        window.Top = top;
                                        window.Left = left + width;
                                        bool isSellTicket = (!IsSingleLeg && Price < 0) || (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell);
                                        if (OmsCore.Config.HaveContraBuysOnTheLeft && isSellTicket)
                                        {
                                            window.Left = left;
                                            contraSourceWindow.Dispatcher.Invoke(new Action(() => contraSourceWindow.Left = left + width));
                                        }
                                    };
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex, nameof(Clone));
                                }
                            }

                            window.Show();

                            Dispatcher.Run();
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, nameof(Contra));
                            window?.Close();
                            ShowMessage("Error occured when creating a ticket\nCreate a new one.", "ZeroPlus OMS");
                        }
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Contra));
            }
        }

        [Command]
        public void BidDepthRowDoubleClickCommand(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is DepthItemModel depthItem)
            {
                SetBidFromDepthItem(depthItem);
            }
        }

        [Command]
        public void AskDepthRowDoubleClickCommand(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is DepthItemModel depthItem)
            {
                SetAskFromDepthItem(depthItem);
            }
        }

        [Command]
        public void OpenInBasketTrader()
        {
            try
            {

                CancelSpeedTrader();
                if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
                    {
                        ViewModel: BasketTraderViewModel viewModel
                    } view)
                {
                    if (viewModel.IsReady)
                    {
                        Task.Run(() => OnReady(viewModel));
                    }
                    else
                    {
                        viewModel.Ready += OnReady;

                    }

                    void OnReady(IModuleViewModel _)
                    {
                        viewModel.Ready -= OnReady;
                        viewModel.LoadFromTicketAsync(this);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
            }
        }

        [Command]
        public void OpenInNagBasketTrader()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    CancelSpeedTrader();
                    if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
                        {
                            ViewModel: BasketTraderViewModel viewModel
                        } view)
                    {
                        if (viewModel.IsReady)
                        {
                            Task.Run(() => OnReady(viewModel));
                        }
                        else
                        {
                            viewModel.Ready += OnReady;

                        }

                        void OnReady(IModuleViewModel _)
                        {
                            viewModel.Ready -= OnReady;
                            viewModel.LoadNagbotFromTicketAsync(this);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInNagBasketTrader));
            }
        }

        [Command]
        public async Task ExpirationUp()
        {
            try
            {
                CancelSpeedTrader();
                string summaryMessage = await ExpirationUpAsync(PermSide.All);

                if (!string.IsNullOrWhiteSpace(summaryMessage))
                {
                    MessageBoxService?.ShowMessage(summaryMessage, "Expiration Down Summary - ZeroPlus OMS", MessageButton.OK, MessageIcon.Information);
                }

                if (EdgeToTheoLocked)
                {
                    await UseEdgeToTheoAsync(TemplateEdgeToTheo);
                }
            }
            catch (SlimException ex)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage(ex.Message, "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
                _log.Error(ex, nameof(ExpirationUp));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationUp));
            }
        }

        [Command]
        public async Task ExpirationDown()
        {
            try
            {
                CancelSpeedTrader();
                string summaryMessage = await ExpirationDownAsync(PermSide.All);

                if (!string.IsNullOrWhiteSpace(summaryMessage))
                {
                    _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage(summaryMessage, "Expiration Up Summary - ZeroPlus OMS", MessageButton.OK, MessageIcon.Information)));
                }

                if (EdgeToTheoLocked)
                {
                    await UseEdgeToTheoAsync(TemplateEdgeToTheo);
                }
            }
            catch (SlimException ex)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage(ex.Message, "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
                _log.Error(ex, nameof(ExpirationDown));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationDown));
            }
        }

        [Command]
        public async Task StrikeUp()
        {
            try
            {
                CancelSpeedTrader();
                await StrikeUpAsync(PermSide.All);
                if (EdgeToTheoLocked)
                {
                    await UseEdgeToTheoAsync(TemplateEdgeToTheo);
                }
            }
            catch (SlimException ex)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage(ex.Message, "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
                _log.Error(ex, nameof(StrikeUp));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikeUp));
            }
        }

        [Command]
        public async Task StrikeDown()
        {
            try
            {
                CancelSpeedTrader();
                await StrikeDownAsync(PermSide.All);
                if (EdgeToTheoLocked)
                {
                    await UseEdgeToTheoAsync(TemplateEdgeToTheo);
                }
            }
            catch (SlimException ex)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage(ex.Message, "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
                _log.Error(ex, nameof(StrikeDown));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikeDown));
            }
        }

        [Command]
        public void LockLowPriceCommand()
        {
            try
            {
                if (LockLowPrice)
                {
                    ResetPriceLock();
                }
                else
                {
                    ResetPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to Bid?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockLowPrice = true;
                        SetPriceMinimal(Low);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockLowPriceCommand));
            }
        }

        [Command]
        public void LockMidPriceCommand()
        {
            try
            {
                if (LockMidPrice)
                {
                    ResetPriceLock();
                }
                else
                {
                    ResetPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to Mid?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockMidPrice = true;
                        SetPriceMinimal(Mid);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockMidPriceCommand));
            }
        }

        [Command]
        public void LockHighPriceCommand()
        {
            try
            {
                if (LockHighPrice)
                {
                    ResetPriceLock();
                }
                else
                {
                    ResetPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to High?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockHighPrice = true;
                        SetPriceMinimal(High);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockHighPriceCommand));
            }
        }

        [Command]
        public void LockContraLowPriceCommand()
        {
            try
            {
                if (LockContraLowPrice)
                {
                    ResetContraPriceLock();
                }
                else
                {
                    ResetContraPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to Bid?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockContraLowPrice = true;
                        SetPriceMinimal(Low);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockContraLowPriceCommand));
            }
        }

        [Command]
        public void LockContraMidPriceCommand()
        {
            try
            {
                if (LockContraMidPrice)
                {
                    ResetContraPriceLock();
                }
                else
                {
                    ResetContraPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to Mid?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockContraMidPrice = true;
                        SetPriceMinimal(Mid);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockContraMidPriceCommand));
            }
        }

        [Command]
        public void LockContraHighPriceCommand()
        {
            try
            {
                if (LockContraHighPrice)
                {
                    ResetContraPriceLock();
                }
                else
                {
                    ResetContraPriceLock();

                    bool response = MessageBoxService.ShowMessage("Are you sure you want to lock your price to High?\n\nWarning: This will modify resting orders!", SpreadId, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No) == MessageResult.Yes;
                    if (response)
                    {
                        LockContraHighPrice = true;
                        SetPriceMinimal(High);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockContraHighPriceCommand));
            }
        }

        [Command]
        public void SetPriceToLow()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Combined:
                    case OrderTicketStyle.Single:
                        SetPrice(Low);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(Low);
                        SetContraPrice(Low);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToLow));
            }
        }

        [Command]
        public void SetPriceToLowInt()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Combined:
                    case OrderTicketStyle.Single:
                        SetPrice(LowInt);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(LowInt);
                        SetContraPrice(LowInt);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToLowInt));
            }
        }

        [Command]
        public void SetPriceToHigh()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Single:
                        SetPrice(High);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(High);
                        SetContraPrice(High);
                        break;
                    default:
                        SetContraPrice(High);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToHigh));
            }
        }

        [Command]
        public void SetPriceToHighestBid()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Combined:
                    case OrderTicketStyle.Single:
                        SetPrice(HighestBid);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(HighestBid);
                        SetContraPrice(HighestBid);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToHighestBid));
            }
        }

        [Command]
        public void SetPriceToLowestAsk()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Single:
                        SetPrice(LowestAsk);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(LowestAsk);
                        SetContraPrice(LowestAsk);
                        break;
                    default:
                        SetContraPrice(LowestAsk);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToLowestAsk));
            }
        }

        [Command]
        public void SetPriceToHighInt()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Single:
                    case OrderTicketStyle.Complex:
                        SetPrice(HighInt);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(HighInt);
                        SetContraPrice(HighInt);
                        break;
                    default:
                        SetContraPrice(HighInt);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToHighInt));
            }
        }

        [Command]
        public void SetPriceToMid()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(Mid - OmsCore.Config.DefaultMidEdge);
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Combined:
                        if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                        {
                            SetContraPrice(Mid - OmsCore.Config.DefaultMidEdge);
                        }
                        else
                        {
                            SetContraPrice(Mid + OmsCore.Config.DefaultMidEdge);
                        }
                        break;
                    case OrderTicketStyle.Dual:
                        SetContraPrice(Mid + OmsCore.Config.DefaultMidEdge);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToMid));
            }
        }

        [Command]
        public void SetPriceToMidInt()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(MidInt - OmsCore.Config.DefaultMidEdge);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetContraPrice(MidInt - OmsCore.Config.DefaultMidEdge);
                    }
                    else
                    {
                        SetContraPrice(MidInt + OmsCore.Config.DefaultMidEdge);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToMidInt));
            }
        }

        [Command]
        public void SetPriceToLowDerived()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(LowDerived);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToLowDerived));
            }
        }

        [Command]
        public void SetPriceToLowIntDerived()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(LowIntDerived);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToLowIntDerived));
            }
        }

        [Command]
        public void SetPriceToHighDerived()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Single:
                        SetPrice(HighDerived);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(HighDerived);
                        SetContraPrice(HighDerived);
                        break;
                    default:
                        SetContraPrice(HighDerived);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToHighDerived));
            }
        }

        [Command]
        public void SetPriceToHighIntDerived()
        {
            try
            {
                CancelSpeedTrader();
                switch (TicketStyle)
                {
                    case OrderTicketStyle.Complex:
                    case OrderTicketStyle.Single:
                        SetPrice(HighIntDerived);
                        break;
                    case OrderTicketStyle.Dual:
                        SetPrice(HighIntDerived);
                        SetContraPrice(HighIntDerived);
                        break;
                    default:
                        SetContraPrice(HighIntDerived);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToHighIntDerived));
            }
        }

        [Command]
        public void SetPriceToMidDerived()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(MidDerived - OmsCore.Config.DefaultMidEdge);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetContraPrice(MidDerived - OmsCore.Config.DefaultMidEdge);
                    }
                    else
                    {
                        SetContraPrice(MidDerived + OmsCore.Config.DefaultMidEdge);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToMid));
            }
        }

        [Command]
        public void SetPriceToMidIntDerived()
        {
            try
            {
                CancelSpeedTrader();
                SetPrice(MidIntDerived - OmsCore.Config.DefaultMidEdge);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetContraPrice(MidIntDerived - OmsCore.Config.DefaultMidEdge);
                    }
                    else
                    {
                        SetContraPrice(MidIntDerived + OmsCore.Config.DefaultMidEdge);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToMidIntDerived));
            }
        }

        [Command]
        public void SetPriceToTheo()
        {
            try
            {
                CancelSpeedTrader();
                var result = GetCorrectEdgeForOrderType(NetTheo, OmsCore.Config.DefaultTheoEdge);
                SetPriceAndContraPrice(result);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToTheo));
            }
        }

        [Command]
        public void SetPriceToDeltaAdjTheo()
        {
            try
            {
                CancelSpeedTrader();
                var result = GetCorrectEdgeForOrderType(NetDeltaAdjTheo, OmsCore.Config.DefaultTheoEdge);
                SetPriceAndContraPrice(result);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToDeltaAdjTheo));
            }
        }

        [Command]
        public void SetPriceToDeltaAdjPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(DeltaAdjPx))
                {
                    SetPrice(DeltaAdjPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToDeltaAdjPx));
            }
        }

        [Command]
        public void SetPriceToEmaCommand()
        {
            try
            {
                CancelSpeedTrader();
                double ema = GetEma(true);
                if (!double.IsNaN(ema))
                {
                    SetPrice(ema);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToEmaCommand));
            }
        }

        [Command]
        public void SetContraPriceToDeltaAdjPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(DeltaAdjContraPx))
                {
                    SetContraPrice(DeltaAdjContraPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToDeltaAdjPx));
            }
        }

        [Command]
        public void SetPriceToBestDeltaAdjPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(BestDeltaAdjPx))
                {
                    SetPrice(BestDeltaAdjPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToBestDeltaAdjPx));
            }
        }

        [Command]
        public void SetContraPriceToBestDeltaAdjPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(BestDeltaAdjContraPx))
                {
                    SetContraPrice(BestDeltaAdjContraPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToBestDeltaAdjPx));
            }
        }

        [Command]
        public void SetPriceToPermAdjPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(PermAdjPx))
                {
                    SetPrice(PermAdjPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetPriceToPermAdjPx));
            }
        }

        [Command]
        public void SetContraPriceToPermAdjContraPx()
        {
            try
            {
                CancelSpeedTrader();
                if (!double.IsNaN(PermAdjContraPx))
                {
                    SetContraPrice(PermAdjContraPx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetContraPriceToPermAdjContraPx));
            }
        }

        [Command]
        public void IncrementPrice()
        {
            if (EnableControlPxKey)
            {
                Price = Math.Round(Price + Convert.ToDouble(TicketPriceIncrement), 2, MidpointRounding.AwayFromZero);
            }
        }

        [Command]
        public void DecrementPrice()
        {
            if (EnableControlPxKey)
            {
                Price = Math.Round(Price - Convert.ToDouble(TicketPriceIncrement), 2, MidpointRounding.AwayFromZero);
            }
        }

        [Command]
        public void UpdateQty()
        {
            if (Legs.Count == 0)
            {
                return;
            }

            if (Qty == 0)
            {
                Qty = Lcd;
                return;
            }

            if (Lcd != Qty)
            {
                foreach (TicketLegModel leg in Legs)
                {
                    int ratio = leg.Ratio;
                    if (ratio < 1)
                    {
                        ratio = 1;
                    }
                    leg.Quantity = ratio * Qty;
                }
                QuantityChanged(Legs[0]);
            }
        }

        [Command]
        public void CustomSummary(RowSummaryArgs args)
        {
            if (!args.IsTotalSummary || args.SummaryProcess != SummaryProcess.Finalize)
            {
                return;
            }

            bool use3DecimalPlacesForGreeks = false;
            if (OmsCore != null)
            {
                use3DecimalPlacesForGreeks = OmsCore.Config.DecimalPlacesForGreeks == 3;
            }

            switch (args.SummaryItem.PropertyName)
            {
                case "Quantity":
                    args.TotalValue = Legs.Count > 0 ? $"Qty:{Lcd:n0}" : "N/A";
                    break;
                case "Delta":
                    if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        args.TotalValue = $"Delta:{TotalDelta * 100:N0}";
                    }
                    else
                    {
                        args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalDelta:#,###.###}" : $"{TotalDelta:#,###.##}";
                    }
                    break;
                case "Gamma":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalGamma:#,###.###}" : $"{TotalGamma:#,###.##}";
                    break;
                case "Theta":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalTheta:#,###.###}" : $"{TotalTheta:#,###.##}";
                    break;
                case "NetGamma":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{NetGamma:#,###.###}" : $"{NetGamma:#,###.##}";
                    break;
                case "NetTheta":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{NetTheta:#,###.###}" : $"{NetTheta:#,###.##}";
                    break;
                case "Vega":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalVega:#,###.###}" : $"{TotalVega:#,###.##}";
                    break;
                case "Rho":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalRho:#,###.###}" : $"{TotalRho:#,###.##}";
                    break;
                case "Implied":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalImplied:#,###.###}" : $"{TotalImplied:#,###.##}";
                    break;
                case "Theo":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalTheo:#,###.###}" : $"{TotalTheo:#,###.##}";
                    break;
                case "DeltaAdjTheo":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalDeltaAdjTheo:#,###.###}" : $"{TotalDeltaAdjTheo:#,###.##}";
                    break;
                case "WeightedVega":
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{WeightedVega:#,###.###}" : $"{WeightedVega:#,###.##}";
                    break;
                case "HighestBid":
                    args.TotalValue = $"{HighestBid:#,###.##}";
                    break;
                case "LowestAsk":
                    args.TotalValue = $"{LowestAsk:#,###.##}";
                    break;
            }
        }

        [Command]
        public void StartStopSubmitWithDelayCommand()
        {
            if (!SubmitWithDelayEnabled)
            {
                SubmitWithDelayMessage = "";

                if (_delayedTicketsManager.SetTicketIfNotExists(this))
                {
                    SetSubmitWithDelayMessage("Ticket already exists for " + SpreadId);
                    return;
                }

                SubmitWithDelayEnabled = true;

                if (SubmitWithDelayCancelOnLegVolumeChangeEnabled)
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        _legSymbolToVolumeMap[leg.Symbol] = leg.Volume;
                    }
                }

                if (SubmitWithDelayCancelOnUserPositionChangeEnabled)
                {
                    _positionSet = LcdPosition;
                }

                if (SubmitWithDelayDeltaAdjCurrentPxEnabled)
                {
                    SubmitWithDelayDeltaAdjCurrentUnderlying = UnderMid;
                    switch (SubmitWithDelaySide)
                    {
                        case ZeroPlus.Models.Data.Enums.Side.Buy:
                            if (IsSellOrder)
                            {
                                if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg)
                                {
                                    SubmitWithDelayDeltaAdjCurrentPx = -ContraPrice;
                                }
                                else
                                {
                                    SubmitWithDelayDeltaAdjCurrentPx = ContraPrice;
                                }
                            }
                            else
                            {
                                SubmitWithDelayDeltaAdjCurrentPx = Price;
                            }
                            break;
                        case ZeroPlus.Models.Data.Enums.Side.Sell:
                            if (IsSellOrder)
                            {
                                SubmitWithDelayDeltaAdjCurrentPx = Price;
                            }
                            else
                            {
                                if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg)
                                {
                                    SubmitWithDelayDeltaAdjCurrentPx = -ContraPrice;
                                }
                                else
                                {
                                    SubmitWithDelayDeltaAdjCurrentPx = ContraPrice;
                                }
                            }
                            break;
                    }

                    if (IsSingleLeg && SubmitWithDelaySide == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        SubmitWithDelayDeltaAdjCurrentPx += SubmitWithDelayDeltaAdjCurrentPxEdge;
                    }
                    else
                    {
                        SubmitWithDelayDeltaAdjCurrentPx -= SubmitWithDelayDeltaAdjCurrentPxEdge;
                    }
                }

                _submitWithDelayCountdownTimer?.Stop();
                _remainingForSubmitWithDelay = SubmitWithDelayInterval;
                SubmitWithDelayCountDown = TimeSpan.FromSeconds(_remainingForSubmitWithDelay);
                _submitWithDelayCountdownTimer.Start();
            }
            else
            {
                StopSubmitWithDelayTimer();
            }
        }

        [Command]
        public void TrackMidToFillCommand()
        {
            Tracker.StartTracking(PriceLevel.Mid);
        }

        [Command]
        public void TrackMidToFillRestingCommand()
        {
            Tracker.StartTracking(PriceLevel.Mid, resting: true);
        }

        [Command]
        public void CancelTrackerCommand()
        {
            Tracker.Stop();
        }

        [Command]
        public void CopySymbolCommand()
        {
            if (Symbol != null)
            {
                Clipboard.SetText(Symbol);
                ShowMessage("Symbol Copied!", SpreadId, true);
            }
            else
            {
                ShowMessage("Symbol Not Found!", SpreadId, true);
            }
        }

        #endregion

        protected override void OnPreUpdateReset()
        {
            base.OnPreUpdateReset();
            UnsubscribeDepthBook();
        }

        protected override void OnDescriptionUpdated()
        {
            base.OnDescriptionUpdated();
            SubscribeDepthBook();
        }

        protected override void OnClearTicket()
        {
            base.OnClearTicket();
            _rbboUpdate = null;
            BidDepthItems.Clear();
            AskDepthItems.Clear();
        }

        public override void OnUiUpdate()
        {
            base.OnUiUpdate();
            UpdateDepthBook();
        }

        protected override void OnTicketReset()
        {
            base.OnTicketReset();
            _rbboUpdate = null;
            if (_showDepthBook)
            {
                UnsubscribeDepthBook();
            }
        }

        private void SubscribeDepthBook()
        {
            if (_showDepthBook && IsSingleLeg && Legs[0].IsValid)
            {
                if (IsSellOrder)
                {
                    Reverse();
                }
                OmsCore.UpdateManager.Subscribe(Legs[0].Symbol, SubscriptionFieldType.Depth, this);
                _transactionConsumer.Subscribe(SpreadId, SubscriptionFieldType.OrderUpdate, this);
            }
        }

        private void UnsubscribeDepthBook()
        {
            try
            {
                foreach (TicketLegModel leg in Legs)
                {
                    OmsCore.UpdateManager.Unsubscribe(leg.Symbol, SubscriptionFieldType.Depth, this);
                    _transactionConsumer.Unsubscribe(SpreadId, SubscriptionFieldType.OrderUpdate, this);
                }
                _rbboUpdate = null;
                Dispatcher?.BeginInvoke(() =>
                {
                    BidDepthItems.Clear();
                    AskDepthItems.Clear();
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeDepthBook));
            }
        }

        protected override void HandleUnknownUpdate(SubscriptionKey key, object value)
        {
            try
            {
                string symbol = key.Symbol;
                switch (key.Type)
                {
                    case SubscriptionFieldType.Depth:
                        if (_showDepthBook)
                        {
                            if (value is RbboUpdateModel rbbo)
                            {
                                HandleDepthUpdate(rbbo);
                            }
                        }
                        else
                        {
                            UnsubscribeDepthBook();
                        }
                        break;
                    case SubscriptionFieldType.OrderUpdate:
                        if (_showDepthBook)
                        {
                            if (value is IOrder order)
                            {
                                HandleOrderUpdate(order);
                            }
                        }
                        else
                        {
                            UnsubscribeDepthBook();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUnknownUpdate));
            }
        }

        private void HandleOrderUpdate(IOrder order)
        {
            if (order.Side.HasValue)
            {
                switch (order.Side.Value)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                    case ZeroPlus.Models.Data.Enums.Side.BuyToCover:
                        if (order.OrderStatus is OrderStatus.Filled or
                            OrderStatus.Canceled or
                            OrderStatus.Rejected)
                        {
                            _buyOrders.Remove(order);
                        }
                        else
                        {
                            _buyOrders.Add(order);
                        }
                        _updateBids = true;
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                    case ZeroPlus.Models.Data.Enums.Side.SellShort:
                        if (order.OrderStatus is OrderStatus.Filled or
                            OrderStatus.Canceled or
                            OrderStatus.Rejected)
                        {
                            _sellOrders.Remove(order);
                        }
                        else
                        {
                            _sellOrders.Add(order);
                        }
                        _updateAsks = true;
                        break;
                }
            }
        }

        private void HandleDepthUpdate(RbboUpdateModel rbbo)
        {
            _rbboUpdate = rbbo;
            _updateBids = true;
            _updateAsks = true;
        }

        private void UpdateDepthBook()
        {
            try
            {
                if (_showDepthBook && _rbboUpdate != null)
                {
                    RbboUpdateModel rbbo = _rbboUpdate;
                    UpdateBidDepthBook(rbbo);
                    UpdateAskDepthBook(rbbo);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateDepthBook));
            }
        }

        private void UpdateBidDepthBook(RbboUpdateModel rbbo)
        {
            if (_updateBids)
            {
                _updateBids = false;
                int level = 0;
                bool update = false;

                var bids = new List<(string MMID, double Price, int Size, bool CustFlag)>();
                foreach (RbboSlotModel slot in rbbo.GetActiveSlots())
                {
                    if (slot.BidPrice > 0)
                        bids.Add((ExchangeHelper.GetExchangeName(slot.Mcid), slot.BidPrice, (int)slot.BidQty, (slot.Flags & 0x01) != 0));
                }
                bids.Sort((a, b) => b.Price.CompareTo(a.Price));

                lock (_buyOrders)
                {
                    if (BidDepthItems.Count != bids.Count + _buyOrders.Count)
                    {
                        BidDepthItems.Clear();
                        for (int index = 0; index < bids.Count; ++index)
                        {
                            var bid = bids[index];
                            if (bid.Price != 0.0 || !IsSingleLeg)
                            {
                                if (index == 0)
                                {
                                    BidDepthItems.Add(new DepthItemModel()
                                    {
                                        MMID = bid.MMID,
                                        Price = bid.Price,
                                        Size = bid.Size,
                                        Level = level,
                                        CustFlag = bid.CustFlag
                                    });
                                }
                                else
                                {
                                    if (bids[index].Price != bids[index - 1].Price)
                                    {
                                        ++level;
                                    }

                                    BidDepthItems.Add(new DepthItemModel()
                                    {
                                        MMID = bid.MMID,
                                        Price = bid.Price,
                                        Size = bid.Size,
                                        Level = level,
                                        CustFlag = bid.CustFlag
                                    });
                                }
                            }
                        }
                        if (_buyOrders.Count > 0)
                        {
                            foreach (IOrder buyOrder in _buyOrders)
                            {
                                BidDepthItems.Add(new DepthItemModel()
                                {
                                    MMID = buyOrder.Route,
                                    Price = buyOrder.Price,
                                    Size = buyOrder.Quantity,
                                    Level = -1,
                                    IsOrder = true,
                                    Order = buyOrder
                                });
                            }

                            BidDepthItems.Sort(_bidComparer);
                        }
                        update = true;
                    }
                    else
                    {
                        int num5 = 0;
                        for (int index = 0; index < BidDepthItems.Count; ++index)
                        {
                            DepthItemModel bidDepthItem1 = BidDepthItems[index];
                            if (!bidDepthItem1.IsOrder)
                            {
                                var bid = bids[index - num5];
                                if (bidDepthItem1.MMID != bid.MMID || bidDepthItem1.Price != bid.Price || bidDepthItem1.Size != bid.Size || bidDepthItem1.CustFlag != bid.CustFlag)
                                {
                                    bidDepthItem1.MMID = bid.MMID;
                                    bidDepthItem1.Price = bid.Price;
                                    bidDepthItem1.Size = bid.Size;
                                    bidDepthItem1.CustFlag = bid.CustFlag;
                                    bidDepthItem1.Refresh = true;
                                }
                                if (index == 0)
                                {
                                    level = 0;
                                }

                                if (index != 0)
                                {
                                    int num6 = index;
                                    while (num6 > 1 && BidDepthItems[num6 - 1].IsOrder)
                                    {
                                        --num6;
                                    }

                                    if (!BidDepthItems[num6 - 1].IsOrder && bidDepthItem1.Price != BidDepthItems[num6 - 1].Price)
                                    {
                                        ++level;
                                    }
                                }
                                if (bidDepthItem1.Level != level)
                                {
                                    bidDepthItem1.Level = level;
                                    bidDepthItem1.Refresh = true;
                                    if (_buyOrders.Count > 0)
                                    {
                                        update = true;
                                    }
                                }
                            }
                            else
                            {
                                if (bidDepthItem1.Price != bidDepthItem1.Order.Price || bidDepthItem1.Size != bidDepthItem1.Order.Quantity)
                                {
                                    bidDepthItem1.Price = bidDepthItem1.Order.Price;
                                    bidDepthItem1.Size = bidDepthItem1.Order.Quantity;
                                    bidDepthItem1.Refresh = true;
                                }
                                if (!update)
                                {
                                    if (index > 0)
                                    {
                                        DepthItemModel bidDepthItem2 = BidDepthItems[index - 1];
                                        if (bidDepthItem2.Price <= bidDepthItem1.Order.Price && !bidDepthItem2.IsOrder)
                                        {
                                            update = true;
                                        }
                                    }
                                    if (index < BidDepthItems.Count - 1)
                                    {
                                        DepthItemModel bidDepthItem3 = BidDepthItems[index + 1];
                                        if (bidDepthItem3.Price > bidDepthItem1.Order.Price && !bidDepthItem3.IsOrder)
                                        {
                                            update = true;
                                        }
                                    }
                                }
                                ++num5;
                            }
                        }
                        if (_buyOrders.Count > 0 & update)
                        {
                            BidDepthItems.Sort(_bidComparer);
                        }
                    }
                }
                if (!update)
                {
                    for (int index = 0; index < BidDepthItems.Count; ++index)
                    {
                        DepthItemModel bidDepthItem = BidDepthItems[index];
                        if (bidDepthItem.Refresh)
                        {
                            bidDepthItem.Refresh = false;
                            bidDepthItem.RefreshItems();
                        }
                    }
                }
                BidSizeCaption = string.Format("S:{0:n0}", BidDepthItems.Where(x => x.Level == 0 && !x.IsOrder).Sum(x => x.Size));
            }
        }

        private void UpdateAskDepthBook(RbboUpdateModel rbbo)
        {
            if (_updateAsks)
            {
                _updateAsks = false;
                int level = 0;
                bool update = false;

                var asks = new List<(string MMID, double Price, int Size, bool CustFlag)>();
                foreach (RbboSlotModel slot in rbbo.GetActiveSlots())
                {
                    if (slot.AskPrice > 0)
                        asks.Add((ExchangeHelper.GetExchangeName(slot.Mcid), slot.AskPrice, (int)slot.AskQty, (slot.Flags & 0x02) != 0));
                }
                asks.Sort((a, b) => a.Price.CompareTo(b.Price));

                lock (_sellOrders)
                {
                    if (AskDepthItems.Count != asks.Count + _sellOrders.Count)
                    {
                        AskDepthItems.Clear();
                        for (int index = 0; index < asks.Count; ++index)
                        {
                            var ask = asks[index];
                            if (ask.Price != 0.0 || !IsSingleLeg)
                            {
                                if (index == 0)
                                {
                                    AskDepthItems.Add(new DepthItemModel()
                                    {
                                        MMID = ask.MMID,
                                        Price = ask.Price,
                                        Size = ask.Size,
                                        Level = level,
                                        CustFlag = ask.CustFlag
                                    });
                                }
                                else
                                {
                                    if (asks[index].Price != asks[index - 1].Price)
                                    {
                                        ++level;
                                    }

                                    AskDepthItems.Add(new DepthItemModel()
                                    {
                                        MMID = ask.MMID,
                                        Price = ask.Price,
                                        Size = ask.Size,
                                        Level = level,
                                        CustFlag = ask.CustFlag
                                    });
                                }
                            }
                        }
                        if (_sellOrders.Count > 0)
                        {
                            foreach (IOrder sellOrder in _sellOrders)
                            {
                                AskDepthItems.Add(new DepthItemModel()
                                {
                                    MMID = sellOrder.Route,
                                    Price = sellOrder.Price,
                                    Size = sellOrder.Quantity,
                                    Level = -2,
                                    IsOrder = true,
                                    Order = sellOrder
                                });
                            }

                            AskDepthItems.Sort(_askComparer);
                        }
                        update = true;
                    }
                    else
                    {
                        int num8 = 0;
                        for (int index = 0; index < AskDepthItems.Count; ++index)
                        {
                            DepthItemModel askDepthItem1 = AskDepthItems[index];
                            if (!askDepthItem1.IsOrder)
                            {
                                var ask = asks[index - num8];
                                if (askDepthItem1.MMID != ask.MMID || askDepthItem1.Price != ask.Price || askDepthItem1.Size != ask.Size || askDepthItem1.CustFlag != ask.CustFlag)
                                {
                                    askDepthItem1.MMID = ask.MMID;
                                    askDepthItem1.Price = ask.Price;
                                    askDepthItem1.Size = ask.Size;
                                    askDepthItem1.CustFlag = ask.CustFlag;
                                    askDepthItem1.Refresh = true;
                                }
                                if (index == 0)
                                {
                                    level = 0;
                                }

                                if (index != 0)
                                {
                                    int num9 = index;
                                    while (num9 > 1 && AskDepthItems[num9 - 1].IsOrder)
                                    {
                                        --num9;
                                    }

                                    if (!AskDepthItems[num9 - 1].IsOrder && askDepthItem1.Price != AskDepthItems[num9 - 1].Price)
                                    {
                                        ++level;
                                    }
                                }
                                if (askDepthItem1.Level != level)
                                {
                                    askDepthItem1.Level = level;
                                    askDepthItem1.Refresh = true;
                                    if (_sellOrders.Count > 0)
                                    {
                                        update = true;
                                    }
                                }
                            }
                            else
                            {
                                if (askDepthItem1.Price != askDepthItem1.Order.Price || askDepthItem1.Size != askDepthItem1.Order.Quantity)
                                {
                                    askDepthItem1.Price = askDepthItem1.Order.Price;
                                    askDepthItem1.Size = askDepthItem1.Order.Quantity;
                                    askDepthItem1.Refresh = true;
                                }
                                if (!update)
                                {
                                    if (index > 0)
                                    {
                                        DepthItemModel askDepthItem2 = AskDepthItems[index - 1];
                                        if (askDepthItem2.Price >= askDepthItem1.Order.Price && !askDepthItem2.IsOrder)
                                        {
                                            update = true;
                                        }
                                    }
                                    if (index < AskDepthItems.Count - 1)
                                    {
                                        DepthItemModel askDepthItem3 = AskDepthItems[index + 1];
                                        if (askDepthItem3.Price < askDepthItem1.Order.Price && !askDepthItem3.IsOrder)
                                        {
                                            update = true;
                                        }
                                    }
                                }
                                ++num8;
                            }
                        }
                        if (_sellOrders.Count > 0 & update)
                        {
                            AskDepthItems.Sort(_askComparer);
                        }
                    }
                }
                if (!update)
                {
                    for (int index = 0; index < AskDepthItems.Count; ++index)
                    {
                        DepthItemModel askDepthItem = AskDepthItems[index];
                        if (askDepthItem.Refresh)
                        {
                            askDepthItem.Refresh = false;
                            askDepthItem.RefreshItems();
                        }
                    }
                }
                AskSizeCaption = string.Format("S:{0:n0}", AskDepthItems.Where(x => x.Level == 0 && !x.IsOrder).Sum(x => x.Size));
            }
        }

        private void OnSubmitWithDelayTimerTick()
        {
            if (!SubmitWithDelayEnabled || IsDisposed)
            {
                StopSubmitWithDelayTimer();
            }
            else if (--_remainingForSubmitWithDelay <= 0)
            {
                _submitWithDelayCountdownTimer.Stop();
                SubmitWithDelayCountDown = TimeSpan.FromSeconds(0);
                SubmitWithDelayEnabled = false;
                _ = UpdatePrice(submit: true);
            }
            else
            {
                SubmitWithDelayCountDown = TimeSpan.FromSeconds(_remainingForSubmitWithDelay);
                _ = UpdatePrice(submit: false);
                if (SubmitWithDelayPlayPreSubmitNotification)
                {
                    if (_remainingForSubmitWithDelay == SubmitWithDelayPreSubmitNotificationSeconds + 1)
                    {
                        _notificationManager.PlayTts("Submitting " + Underlying + " " + SpreadType + " order in 10.");
                    }
                    else if (_remainingForSubmitWithDelay <= SubmitWithDelayPreSubmitNotificationSeconds - 1 && _remainingForSubmitWithDelay > 1)
                    {
                        _notificationManager.PlayTts((_remainingForSubmitWithDelay - 1).ToString());
                    }
                }
            }
        }

        public void StopSubmitWithDelayTimer()
        {
            SubmitWithDelayEnabled = false;
            _submitWithDelayCountdownTimer.Stop();
            SubmitWithDelayCountDown = TimeSpan.FromSeconds(SubmitWithDelayInterval);
        }

        public void SetBidFromDepthItem(DepthItemModel depthItem)
        {
            Price = depthItem.Price;
            if (IsStockTicket)
            {
                var route = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
                if (!string.IsNullOrWhiteSpace(route))
                {
                    Route = route;
                }
            }
            else
            {
                if (depthItem.MMID != null && _routeLookup.TryGetValue(depthItem.MMID?.ToUpper() ?? string.Empty, out string route))
                {
                    Route = route;
                }
            }
        }

        public void SetAskFromDepthItem(DepthItemModel depthItem)
        {
            ContraPrice = depthItem.Price;
            if (IsStockTicket)
            {
                var route = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
                if (!string.IsNullOrWhiteSpace(route))
                {
                    ContraRoute = route;
                }
            }
            else
            {
                if (depthItem.MMID != null && _routeLookup.TryGetValue(depthItem.MMID?.ToUpper(), out string route))
                {
                    ContraRoute = route;
                }
            }
        }

        private void SendToNagBot(object parameter)
        {
            try
            {
                if (parameter is BasketTraderViewModel basket && basket != null)
                {
                    basket.LoadNagbotFromTicketAsync(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToNagBot));
            }
        }

        private async Task UpdatePrice(bool submit = false)
        {
            bool submitMain = (SubmitWithDelaySide == ZeroPlus.Models.Data.Enums.Side.Buy && !IsSellOrder) || (SubmitWithDelaySide == ZeroPlus.Models.Data.Enums.Side.Sell && IsSellOrder);
            if (submit)
            {
                if (SubmitWithDelayBidRangeEnabled)
                {
                    if (SubmitWithDelayMinBid > Low || Low > SubmitWithDelayMaxBid)
                    {
                        _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Bid outside range. Min: " + SubmitWithDelayMinBid + ", Max: " + SubmitWithDelayMaxBid + ", Bid: " + Low + ", Side: " + SubmitWithDelaySide);
                        SetSubmitWithDelayMessage("Bid outside range!");
                        return;
                    }
                }

                if (SubmitWithDelayAskRangeEnabled)
                {
                    if (SubmitWithDelayMinAsk > High || High > SubmitWithDelayMaxAsk)
                    {
                        _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Ask outside range. Min: " + SubmitWithDelayMinAsk + ", Max: " + SubmitWithDelayMaxAsk + ", Ask: " + High + ", Side: " + SubmitWithDelaySide);
                        SetSubmitWithDelayMessage("Ask outside range!");
                        return;
                    }
                }

                if (SubmitWithDelayPriceRangeEnabled)
                {
                    if (submitMain)
                    {
                        if (SubmitWithDelayMinPrice > Price || Price > SubmitWithDelayMaxPrice)
                        {
                            _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Price outside range. Min: " + SubmitWithDelayMinPrice + ", Max: " + SubmitWithDelayMaxPrice + ", Price: " + Price + ", Side: " + SubmitWithDelaySide);
                            SetSubmitWithDelayMessage("Price outside range!");
                            return;
                        }
                    }
                    else
                    {
                        if (SubmitWithDelayMinPrice > ContraPrice || ContraPrice > SubmitWithDelayMaxPrice)
                        {
                            _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Price outside range. Min: " + SubmitWithDelayMinPrice + ", Max: " + SubmitWithDelayMaxPrice + ", Price: " + ContraPrice + ", Side: " + SubmitWithDelaySide);
                            SetSubmitWithDelayMessage("Price outside range!");
                            return;
                        }
                    }
                }

                if (SubmitWithDelayCancelOnLegVolumeChangeEnabled)
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        if (!_legSymbolToVolumeMap.TryGetValue(leg.Symbol, out double volume) && leg.Volume - volume > SubmitWithDelayCancelOnVolumeChange)
                        {
                            _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Leg volume changed. Symbol: " + leg.Symbol + ", Prev Vol: " + volume + ", New Vol: " + leg.Volume);
                            SetSubmitWithDelayMessage("Leg volume changed!");
                            return;
                        }
                    }
                }

                if (SubmitWithDelayCancelOnUserPositionChangeEnabled && _positionSet != LcdPosition)
                {
                    _log.Info("Submit with delay for " + SpreadId + "Submit with delay check. Position changed. Prev Pos: " + _positionSet + ", New Pos: " + LcdPosition);
                    SetSubmitWithDelayMessage("Position changed!");
                    return;
                }
            }
            if (SubmitWithDelayResetQtyEnabled)
            {
                ResetQty();
            }

            EdgePriceCalculationResult pricingResults = GetBestPrice();
            if (submitMain)
            {
                if (double.IsNaN(pricingResults.Price))
                {
                    SetSubmitWithDelayMessage("Invalid price!", submit);
                    return;
                }
                else
                {
                    SetPrice(pricingResults.Price);
                    if (submit)
                    {
                        await SubmitAsync();
                        SetSubmitWithDelayMessage("submitting at " + Price);
                    }
                }
            }
            else
            {
                if (double.IsNaN(pricingResults.ContraPrice))
                {
                    SetSubmitWithDelayMessage("Invalid price!", submit);
                    return;
                }
                else
                {
                    if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg)
                    {
                        pricingResults.ContraPrice *= -1;
                    }

                    SetContraPrice(pricingResults.ContraPrice);
                    if (submit)
                    {
                        await SubmitContraAsync();
                        SetSubmitWithDelayMessage("submitting at " + ContraPrice);
                    }
                }
            }
        }

        private void ResetQty()
        {
            UpdateQty(1);
            ContraQty = Qty;
        }

        private void SetSubmitWithDelayMessage(string message, bool withTts = true)
        {
            SubmitWithDelayMessage = message;
            if (withTts)
            {
                _notificationManager.PlayTts(Underlying + " " + SpreadType + " " + message);
            }
        }

        private EdgePriceCalculationResult GetBestPrice()
        {
            List<EdgePriceCalculationResult> edgeCalculationResults = new();
            if (SubmitWithDelayPercentBidEnabled)
            {
                EdgePriceCalculationResult priceResult = CalculateBidPercent(SubmitWithDelayPercentBid);
                edgeCalculationResults.Add(priceResult);
                _log.Info("Submit with delay for " + SpreadId + "Bid percent price. Price: " + priceResult.Price + ", New Pos: " + priceResult.ContraPrice);
            }
            if (SubmitWithDelayTheoReferenceEnabled)
            {
                EdgePriceCalculationResult priceResult = CalculateEdgeToAdjTheo(NetDeltaAdjTheo, SubmitWithDelayEdgeToTheo);
                edgeCalculationResults.Add(priceResult);
                _log.Info("Submit with delay for " + SpreadId + "Adj Theo Reference price. Price: " + priceResult.Price + ", New Pos: " + priceResult.ContraPrice);
            }
            if (SubmitWithDelayDeltaAdjustEnabled)
            {
                DeltaAdjPrice();
                EdgePriceCalculationResult priceResult = new()
                {
                    Price = DeltaAdjPx,
                    ContraPrice = DeltaAdjContraPx,
                };
                edgeCalculationResults.Add(priceResult);
                _log.Info("Submit with delay for " + SpreadId + "Delta adjusted last fill price. Price: " + priceResult.Price + ", New Pos: " + priceResult.ContraPrice);
            }
            if (SubmitWithDelayDeltaAdjCurrentPxEnabled)
            {
                EdgePriceCalculationResult priceResult = new();
                double underMid = UnderMid;
                double totalDelta = TotalDelta;
                double contraDelta = IsSingleLeg ? TotalDelta : -TotalDelta;
                switch (SubmitWithDelaySide)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        if (IsSellOrder)
                        {
                            double deltaAdjPx = ((underMid - SubmitWithDelayDeltaAdjCurrentUnderlying) * contraDelta) + SubmitWithDelayDeltaAdjCurrentPx;
                            priceResult.ContraPrice = deltaAdjPx;
                            priceResult.Price = double.NaN;
                        }
                        else
                        {
                            double deltaAdjPx = ((underMid - SubmitWithDelayDeltaAdjCurrentUnderlying) * totalDelta) + SubmitWithDelayDeltaAdjCurrentPx;
                            priceResult.Price = deltaAdjPx;
                            priceResult.ContraPrice = double.NaN;
                        }
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                        if (IsSellOrder)
                        {
                            double deltaAdjPx = ((underMid - SubmitWithDelayDeltaAdjCurrentUnderlying) * totalDelta) + SubmitWithDelayDeltaAdjCurrentPx;
                            priceResult.Price = deltaAdjPx;
                            priceResult.ContraPrice = double.NaN;
                        }
                        else
                        {
                            double deltaAdjPx = ((underMid - SubmitWithDelayDeltaAdjCurrentUnderlying) * contraDelta) + SubmitWithDelayDeltaAdjCurrentPx;
                            priceResult.ContraPrice = deltaAdjPx;
                            priceResult.Price = double.NaN;
                        }
                        break;
                }
                edgeCalculationResults.Add(priceResult);
                _log.Info("Submit with delay for " + SpreadId + "Delta adjusted current price. Initial Price: " + SubmitWithDelayDeltaAdjCurrentPx + ", Under Price: " + SubmitWithDelayDeltaAdjCurrentUnderlying + ", Price: " + priceResult.Price + ", New Pos: " + priceResult.ContraPrice);
            }

            EdgePriceCalculationResult result = new();
            if (IsSingleLeg)
            {
                if (!IsSellOrder)
                {
                    List<double> prices = edgeCalculationResults.Select(x => x.Price).Where(x => !double.IsNaN(x)).ToList();
                    result.Price = prices.Count > 0 ? prices.Min() : double.NaN;
                    List<double> contraPrices = edgeCalculationResults.Select(x => x.ContraPrice).Where(x => !double.IsNaN(x)).ToList();
                    result.ContraPrice = contraPrices.Count > 0 ? contraPrices.Max() : double.NaN;
                }
                else
                {
                    List<double> prices = edgeCalculationResults.Select(x => x.Price).Where(x => !double.IsNaN(x)).ToList();
                    result.Price = prices.Count > 0 ? prices.Max() : double.NaN;
                    List<double> contraPrices = edgeCalculationResults.Select(x => x.ContraPrice).Where(x => !double.IsNaN(x)).ToList();
                    result.ContraPrice = contraPrices.Count > 0 ? contraPrices.Min() : double.NaN;
                }
            }
            else
            {
                List<double> prices = edgeCalculationResults.Select(x => x.Price).Where(x => !double.IsNaN(x)).ToList();
                result.Price = prices.Count > 0 ? prices.Min() : double.NaN;
                List<double> contraPrices = edgeCalculationResults.Select(x => x.ContraPrice).Where(x => !double.IsNaN(x)).ToList();
                result.ContraPrice = contraPrices.Count > 0 ? contraPrices.Min() : double.NaN;
            }

            _log.Info("Submit with delay for " + SpreadId + "Best price selected. Price: " + result.Price + ", New Pos: " + result.ContraPrice);
            return result;
        }

        internal async Task LoadContraFromTemplateAsync(OrderTicket complexOrderTicketViewModel)
        {
            await LoadFromTicketAsync(complexOrderTicketViewModel, false, false, true);
            double price = Price;
            Reverse();
            double defaultContraEdge = GetDefaultContraEdge();
            if (IsSingleLeg)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    SetPrice(price - defaultContraEdge);
                }
                else
                {
                    SetPrice(price + defaultContraEdge);
                }
            }
            else
            {
                SetPrice(-price);
                SetPrice(Price - defaultContraEdge);
            }
            RiskCheckEnabled = false;
        }

        private void OnConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            RiskCheckEnabled = config.GlobalTicketRiskControlEnabledV2;

            EnablePriceTrackBar = config.EnablePriceTrackBar;
            ShowQuickRoutes = config.ShowQuickRoutes;
            Dispatcher?.BeginInvoke(new Action(() =>
            {
                LoadAutoCloseConfigModels();
                QuickRoutes.Clear();
                foreach (Tuple<string, string> quickRouteTuple in config.QuickRoutes)
                {
                    QuickRoutes.Add(Tuple.Create(quickRouteTuple.Item1, quickRouteTuple.Item2));
                }
            }));
        }

        internal async Task LoadViewModelConfigAsync(string uid)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(ComplexOrderTicketConfig)}.xml");
                string defaultConfigExportPath = Path.Combine(layoutDir, $"{nameof(ComplexOrderTicketConfig)}.xml");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = await File.ReadAllTextAsync(configExportPath);
                    ComplexOrderTicketConfig config = await Task.Run(() => JsonConvert.DeserializeObject<ComplexOrderTicketConfig>(myFileStream));
                    LoadConfig(config);
                    _ = LoadFromOrderAsync(config.Order);
                }
                else if (File.Exists(defaultConfigExportPath))
                {
                    string myFileStream = await File.ReadAllTextAsync(defaultConfigExportPath);
                    ComplexOrderTicketConfig config = await Task.Run(() => JsonConvert.DeserializeObject<ComplexOrderTicketConfig>(myFileStream));
                    LoadConfig(config);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadViewModelConfigAsync));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private void LoadConfig(ComplexOrderTicketConfig config)
        {
            SubmitWithDelayPercentBidEnabled = config.SubmitWithDelayPercentBidEnabled;
            SubmitWithDelayPercentBid = config.SubmitWithDelayPercentBid;
            SubmitWithDelayTheoReferenceEnabled = config.SubmitWithDelayTheoReferenceEnabled;
            SubmitWithDelayEdgeToTheo = config.SubmitWithDelayEdgeToTheo;
            SubmitWithDelayDeltaAdjustEnabled = config.SubmitWithDelayDeltaAdjustEnabled;
            SubmitWithDelaySide = config.SubmitWithDelaySide;
            SubmitWithDelayDeltaAdjLevel = config.SubmitWithDelayDeltaAdjLevel;
            SubmitWithDelayBidRangeEnabled = config.SubmitWithDelayBidRangeEnabled;
            SubmitWithDelayMinBid = config.SubmitWithDelayMinBid;
            SubmitWithDelayMaxBid = config.SubmitWithDelayMaxBid;
            SubmitWithDelayAskRangeEnabled = config.SubmitWithDelayAskRangeEnabled;
            SubmitWithDelayMinAsk = config.SubmitWithDelayMinAsk;
            SubmitWithDelayMaxAsk = config.SubmitWithDelayMaxAsk;
            SubmitWithDelayCancelOnUserPositionChangeEnabled = config.SubmitWithDelayCancelOnUserPositionChangeEnabled;
            SubmitWithDelayCancelOnLegVolumeChangeEnabled = config.SubmitWithDelayCancelOnLegVolumeChangeEnabled;
            SubmitWithDelayCancelOnVolumeChange = config.SubmitWithDelayCancelOnVolumeChange;
            SubmitWithDelayPlayPreSubmitNotification = config.SubmitWithDelayPlayPreSubmitNotification;
            SubmitWithDelayPreSubmitNotificationSeconds = config.SubmitWithDelayPreSubmitNotificationSeconds;
            EnableControlPxKey = config.EnableControlPxKey;
            IsReady = true;
            Ready?.Invoke(this);
        }

        internal void SaveViewModelConfig()
        {
            try
            {
                ComplexOrderTicketConfig config = new()
                {
                    SubmitWithDelayPercentBidEnabled = SubmitWithDelayPercentBidEnabled,
                    SubmitWithDelayPercentBid = SubmitWithDelayPercentBid,
                    SubmitWithDelayTheoReferenceEnabled = SubmitWithDelayTheoReferenceEnabled,
                    SubmitWithDelayEdgeToTheo = SubmitWithDelayEdgeToTheo,
                    SubmitWithDelayDeltaAdjustEnabled = SubmitWithDelayDeltaAdjustEnabled,
                    SubmitWithDelaySide = SubmitWithDelaySide,
                    SubmitWithDelayDeltaAdjLevel = SubmitWithDelayDeltaAdjLevel,
                    SubmitWithDelayBidRangeEnabled = SubmitWithDelayBidRangeEnabled,
                    SubmitWithDelayMinBid = SubmitWithDelayMinBid,
                    SubmitWithDelayMaxBid = SubmitWithDelayMaxBid,
                    SubmitWithDelayAskRangeEnabled = SubmitWithDelayAskRangeEnabled,
                    SubmitWithDelayMinAsk = SubmitWithDelayMinAsk,
                    SubmitWithDelayMaxAsk = SubmitWithDelayMaxAsk,
                    SubmitWithDelayCancelOnUserPositionChangeEnabled = SubmitWithDelayCancelOnUserPositionChangeEnabled,
                    SubmitWithDelayCancelOnLegVolumeChangeEnabled = SubmitWithDelayCancelOnLegVolumeChangeEnabled,
                    SubmitWithDelayCancelOnVolumeChange = SubmitWithDelayCancelOnVolumeChange,
                    SubmitWithDelayPlayPreSubmitNotification = SubmitWithDelayPlayPreSubmitNotification,
                    SubmitWithDelayPreSubmitNotificationSeconds = SubmitWithDelayPreSubmitNotificationSeconds,
                    EnableControlPxKey = EnableControlPxKey,
                };

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                string configExportPath = Path.Combine(layoutDir, $"{nameof(ComplexOrderTicketConfig)}.xml");
                File.WriteAllText(configExportPath, configJson);

                config.Order = ToOrder();
                configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(ComplexOrderTicketConfig)}.xml");
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
                Dispatcher?.Invoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async Task BrokerChangedCommand()
        {
            await ApplyInstanceModeChangesCommand();
        }

        [Command]
        public async Task ApplyInstanceModeChangesCommand()
        {
            string route = Route;
            string contraRoute = ContraRoute;
            await ReloadAccountsAndRoutesList();
            Route = CheckForDirectRoute(route);
            ContraRoute = CheckForDirectRoute(contraRoute);
        }

        [Command]
        public async void RefreshMatrixConfigsCommand()
        {
            if (UseMatrixAlgo)
            {
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.MatrixSmartConfig);

                if (configs != null)
                {
                    List<IDynamicConfigModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            var model = LoadModel(details);
                            models.Add(model);
                        }
                    }
                    LoadSelection(models);
                }
            }
        }

        [Command]
        public void LoadMatrixConfigCommand(IDynamicConfigModel config)
        {
            if (config == null)
            {
                MatrixStrategyConfigModel = null;
                SyntheticSpreadConfigModelId = 0;
            }
            else
            {
                MatrixStrategyConfigModel = (MatrixStrategyConfigModel)config;
                SyntheticSpreadConfigModelId = config.Id;
            }
        }

        private IDynamicConfigModel LoadModel(Comms.Models.Data.Oms.Config.ConfigSave details)
        {
            MatrixStrategyConfigModel model = null;
            if (!string.IsNullOrWhiteSpace(details.ConfigJson))
            {
                model = JsonConvert.DeserializeObject<MatrixStrategyConfigModel>(details.ConfigJson);
            }

            model ??= new MatrixStrategyConfigModel()
            {
                Creator = OmsCore.User.Username,
                LastUpdateTime = DateTime.Now,
                SyntheticSpreadStrategyData = new(),
                ScrapeStrategyData = new(),
                SeekerStrategyData = new(),
                SeekerSpreadStrategyData = new(),
            };

            model.Id = details.Id;
            string configJson = JsonConvert.SerializeObject(details);
            model.Details = JsonConvert.DeserializeObject<ConfigSave>(configJson);
            model.Load();
            return model;
        }

        private void LoadSelection(List<IDynamicConfigModel> models)
        {
            if (models.Count > 0)
            {
                IDynamicConfigModel config = SyntheticSpreadConfigModelId > 0 ? models.FirstOrDefault(x => x.Id == SyntheticSpreadConfigModelId) : null;
                Dispatcher?.BeginInvoke(() =>
                {
                    MatrixConfigModels.Clear();
                    MatrixConfigModels.AddRange(models);
                    LoadMatrixConfigCommand(config);
                });
            }
        }

        protected override bool TrySendToMatrix()
        {
            if (UseMatrixAlgo)
            {
                switch (MatrixStrategy)
                {
                    case MatrixStrategy.Scrape:
                        SendMatrixScrape(MatrixStrategyConfigModel.ScrapeStrategyData);
                        return true;
                    case MatrixStrategy.Seeker:
                        if (IsSingleLeg)
                        {
                            SendMatrixSeeker(MatrixStrategyConfigModel.SeekerStrategyData);
                        }
                        else
                        {
                            SendMatrixSeekerSpread(MatrixStrategyConfigModel.SeekerSpreadStrategyData);
                        }

                        return true;
                    case MatrixStrategy.Synthetic:
                        SendMatrixSyntheticSpread(MatrixStrategyConfigModel.SyntheticSpreadStrategyData);
                        return true;
                }
            }

            return CheckForMatrixRoute();
        }

        protected override void ModifyAutoTraderOrder()
        {
            if (!UseMatrixAlgo || MatrixStrategy != MatrixStrategy.Scrape || MatrixStrategyConfigModel == null)
            {
                base.ModifyAutoTraderOrder();
                return;
            }

            ModifySmartRequest modifyRequest = new()
            {
                Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account,
                LocalId = LocalId ?? "",
                PermId = PermID ?? "",
                OrderId = OrderId ?? "",
                Price = Price,
                Quantity = Lcd,
                Venue = Venue,
            };
            modifyRequest.ScrapeStrategyData.CopyFrom(MatrixStrategyConfigModel.ScrapeStrategyData);
            OmsCore.AutoTraderClient.ModifyOrder(modifyRequest);
        }
    }
}