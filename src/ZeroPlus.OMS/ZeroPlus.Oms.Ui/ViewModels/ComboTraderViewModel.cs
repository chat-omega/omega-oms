using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Charts;
using DevExpress.Xpf.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ComboTraderViewModel : ViewModelBase, IEmaConfig, IOmsDataSubscriber, IOrderInfoUpdateHandler
    {
        private const int initialVisiblePointsCount = 180;
        private const int maxVisiblePointsCount = 800;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly EmaCalculator _emaCalculator;
        private bool _initRange;
        private TradingData _lastPoint;
        private readonly IntervalTimer _intervalTimer;
        private ChartDataPointModel _lastChartPoint;


        private readonly object _orderLock = new();
        private readonly object _stopLossLock = new();
        private readonly PortfolioManagerModel _portfolioManagerModel;
        public event ResetEmaEventHandler ResetEmaEvent;

        public object MinVisibleDate { get; set; }
        public bool AnnotationEditing { get; set; }
        public string OrderId { get; private set; }
        public string ContraOrderId { get; private set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public Dispatcher Dispatcher { get; private set; }
        public string InstanceId => "CT-" + OmsCore.User.Username + "-" + Symbol;
        [Bindable]
        public partial ObservableCollection<StockModel> Symbols { get; set; }
        [Bindable]
        public partial bool CheckForSize { get; set; }
        [Bindable]
        public partial bool RestOrders { get; set; }
        [Bindable]
        public partial ObservableCollection<PresetTriggerModel> OrderTriggers { get; set; }
        [Bindable]
        public partial ObservableCollection<ChartDataPointModel> ChartDataPoints { get; set; }
        [Bindable]
        public partial ObservableCollectionCore<TradingData> ChartDataSource { get; set; }

        [Bindable]
        public partial ChartIntervalItem SelectedInterval { get; set; }
        [Bindable]
        public partial List<ChartIntervalItem> IntervalsSource { get; set; }
        [Bindable]
        public partial bool EmaTriggerEnabled { get; set; }
        [Bindable]
        public partial bool PresetTriggerEnabled { get; set; }
        [Bindable]
        public partial bool ShowCandles { get; set; }
        [Bindable]
        public partial bool OrderEnabled { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial double CurrentPrice { get; set; }
        [Bindable]
        public partial double Low { get; set; }
        [Bindable]
        public partial double High { get; set; }
        [Bindable]
        public partial double Bid { get; set; }
        [Bindable]
        public partial double Ask { get; set; }
        [Bindable]
        public partial double BidEmaSignal { get; set; }
        [Bindable]
        public partial double AskEmaSignal { get; set; }
        [Bindable(Default = 0.15)]
        public partial double BuyTrigger { get; set; }
        [Bindable]
        public partial double SellTrigger { get; set; }
        [Bindable(Default = 1)]
        public partial double SellMinProfit { get; set; }
        [Bindable(Default = -100)]
        public partial double MaxLossStop { get; set; }
        [Bindable(Default = 10)]
        public partial int BuyMaxQty { get; set; }
        [Bindable]
        public partial Color PriceIndicatorColor { get; set; }
        [Bindable]
        public partial int Pos { get; set; }
        [Bindable]
        public partial double Ema { get; set; }
        [Bindable(Default = 30)]
        public partial double SmaPeriods { get; set; }
        [Bindable(Default = 30)]
        public partial double EmaPeriods { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double AdjPnl { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial string CrosshairCurrentFinancialText { get; set; }
        [Bindable]
        public partial string CrosshairCurrentVolumeText { get; set; }
        [Bindable]
        public partial bool EmaEnabled { get; set; }
        [Bindable]
        public partial EmaType SelectedEmaType { get; set; }
        [Bindable]
        public partial double PercentVegaThreshold { get; set; }
        [Bindable]
        public partial double EmaSmoothing { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> AccountsList { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [Bindable]
        public partial string Account { get; set; }
        [Bindable]
        public partial string OrderRoute { get; set; }
        [Bindable]
        public partial string Locate { get; set; }
        [Bindable]
        public partial double EmaInterval { get; set; }
        [Bindable]
        public partial double MaxBidDeviation { get; set; }
        [Bindable]
        public partial double MaxAskDeviation { get; set; }
        [Bindable]
        public partial int LcdPosition { get; set; }
        [Bindable]
        public partial PresetTriggerModel PresetTriggerModel { get; set; }
        [Bindable]
        public partial double RestingOrderCancelDelay { get; set; }
        [Bindable]
        public partial double RestingOrderCloseEdge { get; set; }
        [Bindable]
        public partial string StatusSell { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusModeSell { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial OrderType OrderType { get; set; }
        [Bindable]
        public partial DataType DataType { get; set; }
        public IEnumerable<PairSide> PairSides { get; } = ((PairSide[])Enum.GetValues(typeof(PairSide))).ToList();
        public IEnumerable<OrderType> OrderTypes { get; } = ((OrderType[])Enum.GetValues(typeof(OrderType))).ToList();
        public IEnumerable<DataType> DataTypes { get; } = ((DataType[])Enum.GetValues(typeof(DataType))).ToList();
        public bool IsDisposed { get; set; }
        public string Uid { get; internal set; }
        public string Name { get; internal set; }

        public ComboTraderViewModel(PortfolioManagerModel portfolioManagerModel)
        {
            _portfolioManagerModel = portfolioManagerModel;
            _intervalTimer = new IntervalTimer();
            _intervalTimer.OnTickChanged += OnTickChanged;
            EmaSmoothing = 2;
            EmaPeriods = 20;
            EmaInterval = 1000;
            RestingOrderCancelDelay = 5000;
            RestingOrderCloseEdge = 0.01;
            _emaCalculator = new EmaCalculator(this, SubscriptionFieldType.MidPoint);
            _emaCalculator.EmaUpdatedEvent += OnEmaUpdatedEvent;
            Symbols = new ObservableCollection<StockModel>
            {
                new(this)
                {
                    Symbol = "QQQ",
                    Ratio = 1,
                },
                new(this)
                {
                    Symbol = "TQQQ",
                    Ratio = -3,
                },
            };
            ChartDataSource = new ObservableCollectionCore<TradingData>();
            PresetTriggerModel = new PresetTriggerModel();
            OrderTriggers = new ObservableCollection<PresetTriggerModel>
            {
                PresetTriggerModel
            };
            ChartDataPoints = new ObservableCollection<ChartDataPointModel>();
            IntervalsSource = new List<ChartIntervalItem>();
            InitIntervals();
            SelectedInterval = IntervalsSource[0];
        }

        public async void LoadDefaultAccount()
        {
            string defaultAccount = OmsCore.Config.DefaultAccount;
            if (!AccountsList.Contains(defaultAccount))
            {
                Dispatcher.Invoke(() => AccountsList.Add(defaultAccount));
            }
            List<Account> instAccounts = OmsCore.AutoTraderClient.Accounts;
            foreach (Account account in instAccounts)
            {
                if (!AccountsList.Contains(account.Acronym))
                {
                    Dispatcher.Invoke(() => AccountsList.Add(account.Acronym));
                }
            }

            HashSet<string> uniqueRoutes = new(StringComparer.OrdinalIgnoreCase);
            var currentBroker = OmsCore.Config.DefaultBroker;

            var routeLookup = OmsCore.OrderClient?.RouteLookup;
            var ogRoutes = !string.IsNullOrWhiteSpace(currentBroker)
                ? (routeLookup?.GetRoutesForBroker(currentBroker) ?? Array.Empty<string>())
                : (routeLookup?.GetRoutes() ?? Array.Empty<string>());
            foreach (var route in ogRoutes)
            {
                uniqueRoutes.Add(route);
            }

            await Dispatcher.BeginInvoke(new Action(() =>
                {
                    RoutesList.Clear();

                    foreach (string account in OmsCore.User.Accounts)
                    {
                        if (!AccountsList.Contains(account))
                        {
                            AccountsList.Add(account);
                        }
                    }

                    foreach (string route in uniqueRoutes.OrderBy(x => x))
                    {
                        RoutesList.Add(route);
                    }
                }));

            Account = !string.IsNullOrWhiteSpace(defaultAccount) && OmsCore.User.Accounts.Any(x => x.Equals(defaultAccount, StringComparison.OrdinalIgnoreCase)) ? defaultAccount : AccountsList.FirstOrDefault();
        }

        private void OnEmaUpdatedEvent(double ema)
        {
            Ema = ema;
            UpdateSignals();
            if (EmaTriggerEnabled && !PresetTriggerEnabled)
            {
                lock (_orderLock)
                {
                    CheckForOrder();
                }
            }
        }

        private void CheckForOrder()
        {
            try
            {
                UpdatePositionAndPnl();
                if (OrderEnabled && EmaTriggerEnabled)
                {
                    if (!RestOrders)
                    {
                        if (BidEmaSignal > SellTrigger && UnrealPnl > SellMinProfit && LcdPosition > 0)
                        {
                            SendSellOrder();
                        }
                        else if (AskEmaSignal > BuyTrigger && LcdPosition < BuyMaxQty)
                        {
                            SendBuyOrder();
                        }
                    }
                    else
                    {
                        if (LcdPosition == 0 && AskEmaSignal > BuyTrigger && LcdPosition < BuyMaxQty)
                        {
                            SendBuyOrder();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() => MessageBoxService.ShowMessage(ex.Message, Symbol, MessageButton.OK, MessageIcon.Warning));
                OrderEnabled = false;
            }
        }

        private void UpdatePositionAndPnl()
        {
            RealPnl = Symbols.Sum(x => x.RealPnl);
            UnrealPnl = Symbols.Sum(x => x.UnrealPnl);
            if (Symbols.Count == 1)
            {
                LcdPosition = Symbols.First().FilledQty;
            }
            else if (Symbols.Count > 1 && Symbols.Count(x => Math.Abs(x.FilledQty) >= x.Ratio) == Symbols.Count)
            {
                if ((Symbols.Count(x => (x.FilledQty < 0 && x.Ratio > 0) || (x.FilledQty > 0 && x.Ratio < 0)) == Symbols.Count) ||
                    (Symbols.Count(x => (x.FilledQty > 0 && x.Ratio > 0) || (x.FilledQty < 0 && x.Ratio < 0)) == Symbols.Count))
                {
                    int divisor = Symbols.Min(x => Math.Abs(x.FilledQty));
                    StockModel sample = Symbols.First();
                    if ((sample.FilledQty < 0 && sample.Ratio > 0) || (sample.FilledQty > 0 && sample.Ratio < 0))
                    {
                        LcdPosition = -divisor;
                    }
                    else
                    {
                        LcdPosition = divisor;
                    }
                }
                else
                {
                    LcdPosition = 0;
                }
            }
            else
            {
                LcdPosition = 0;
            }

            lock (_stopLossLock)
            {
                if (UnrealPnl < 0 && UnrealPnl <= MaxLossStop && OrderEnabled)
                {
                    foreach (StockModel symbol in Symbols)
                    {
                        symbol.CancelResume();
                    }
                    OrderEnabled = false;
                    foreach (StockModel symbol in Symbols)
                    {
                        symbol.ClosePositions();
                    }
                    Dispatcher.BeginInvoke(() => MessageBoxService.ShowMessage("Max Loss Stop Reached! Orders Stopped.", Symbol, MessageButton.OK, MessageIcon.Exclamation));
                }
            }
        }

        [Command]
        public void SendPairOrderCommand()
        {
            if (CheckForSize)
            {
                foreach (StockModel symbol in Symbols)
                {
                    if (!symbol.CheckForBuy())
                    {
                        Status = "Qty requirement not met";
                        StatusMode = StatusMode.CancelledSell;
                        return;
                    }
                }
            }
            if (OmsCore.AutoTraderClient.TryGetAccount(Account, out Account accountModel))
            {
                if (Symbols.Count == 1)
                {
                    StockModel symbol = Symbols[0];
                    OrderId = OmsCore.OrderClient.GetNextOrderId();
                    SingleOrderRequest singleOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        ClientOrderId = OrderId,
                        Locate = Locate ?? "",
                        OrderType = OrderType,
                        Symbol = symbol.Symbol,
                        Quantity = Math.Abs(symbol.Ratio),
                        Tag = OmsCore.User.Username,
                        Side = symbol.Ratio < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                    };
                    OmsCore.AutoTraderClient.SendSingleOrder(singleOrderRequest, this);
                }
                else if (Symbols.Count == 2)
                {
                    StockModel leg1 = Symbols[0];
                    StockModel leg2 = Symbols[1];
                    OrderId = OmsCore.OrderClient.GetNextOrderId();
                    PairOrderRequest pairOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        Tag = OmsCore.User.Username,
                        ClientOrderId = OrderId,
                        Locate = Locate ?? "",
                        Leg1Symbol = leg1.Symbol,
                        Leg1Quantity = Math.Abs(leg1.Ratio),
                        Leg1Side = leg1.PairSide,
                        Leg2Symbol = leg2.Symbol,
                        Leg2Quantity = Math.Abs(leg2.Ratio),
                        Leg2Side = leg2.PairSide,
                        OrderType = OrderType,
                    };
                    OmsCore.AutoTraderClient.SendPairOrder(pairOrderRequest, this);
                }
            }
            else
            {
                Status = "Account/Route not valid for pair trade.";
            }
        }

        [Command]
        public void SendBuyOrder()
        {
            if (CheckForSize)
            {
                foreach (StockModel symbol in Symbols)
                {
                    if (!symbol.CheckForBuy())
                    {
                        Status = "Qty requirement not met";
                        StatusMode = StatusMode.CancelledSell;
                        return;
                    }
                }
            }
            if (OmsCore.AutoTraderClient.TryGetAccount(Account, out Account accountModel))
            {
                if (Symbols.Count == 1)
                {
                    StockModel symbol = Symbols[0];
                    OrderId = OmsCore.OrderClient.GetNextOrderId();
                    SingleOrderRequest singleOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        ClientOrderId = OrderId,
                        Locate = Locate ?? "",
                        OrderType = OrderType,
                        Symbol = symbol.Symbol,
                        Quantity = Math.Abs(symbol.Ratio),
                        Tag = OmsCore.User.Username,
                        Side = symbol.Ratio < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                    };
                    OmsCore.AutoTraderClient.SendSingleOrder(singleOrderRequest, this);
                }
                else if (Symbols.Count == 2)
                {
                    StockModel leg1 = Symbols[0];
                    StockModel leg2 = Symbols[1];
                    OrderId = OmsCore.OrderClient.GetNextOrderId();
                    PairOrderRequest pairOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        Tag = OmsCore.User.Username,
                        ClientOrderId = OrderId,
                        Locate = Locate ?? "",
                        Leg1Symbol = leg1.Symbol,
                        Leg1Quantity = Math.Abs(leg1.Ratio),
                        Leg1Side = leg1.Ratio < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        Leg2Symbol = leg2.Symbol,
                        Leg2Quantity = Math.Abs(leg2.Ratio),
                        Leg2Side = leg2.Ratio < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        OrderType = OrderType,
                    };
                    OmsCore.AutoTraderClient.SendPairOrder(pairOrderRequest, this);
                }
            }
            else
            {
                foreach (StockModel symbol in Symbols)
                {
                    symbol.SendBuyOrder();
                }
            }
        }

        [Command]
        public void SendSellOrder()
        {
            if (CheckForSize)
            {
                foreach (StockModel symbol in Symbols)
                {
                    if (!symbol.CheckForSell())
                    {
                        Status = "Qty requirement not met";
                        StatusMode = StatusMode.CancelledSell;
                        return;
                    }
                }
            }
            if (OmsCore.AutoTraderClient.TryGetAccount(Account, out Account accountModel))
            {
                if (Symbols.Count == 1)
                {
                    StockModel symbol = Symbols[0];
                    ContraOrderId = OmsCore.OrderClient.GetNextOrderId();
                    SingleOrderRequest singleOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        Locate = Locate ?? "",
                        ClientOrderId = ContraOrderId,
                        OrderType = OrderType,
                        Symbol = symbol.Symbol,
                        Quantity = Math.Abs(symbol.Ratio),
                        Tag = OmsCore.User.Username,
                        Side = symbol.Ratio > 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                    };
                    OmsCore.AutoTraderClient.SendSingleOrder(singleOrderRequest, this);
                }
                else if (Symbols.Count == 2)
                {
                    StockModel leg1 = Symbols[0];
                    StockModel leg2 = Symbols[1];
                    ContraOrderId = OmsCore.OrderClient.GetNextOrderId();
                    PairOrderRequest pairOrderRequest = new()
                    {
                        Account = accountModel.Acronym,
                        Route = OrderRoute,
                        Tag = OmsCore.User.Username,
                        Locate = Locate ?? "",
                        ClientOrderId = ContraOrderId,
                        Leg1Symbol = leg1.Symbol,
                        Leg1Quantity = Math.Abs(leg1.Ratio),
                        Leg1Side = leg1.Ratio > 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        Leg2Symbol = leg2.Symbol,
                        Leg2Quantity = Math.Abs(leg2.Ratio),
                        Leg2Side = leg2.Ratio > 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        OrderType = OrderType,
                    };
                    OmsCore.AutoTraderClient.SendPairOrder(pairOrderRequest, this);
                }
            }
            else
            {
                foreach (StockModel symbol in Symbols)
                {
                    symbol.SendSellOrder();
                }
            }
        }

        private void OnTickChanged(object sender, EventArgs e)
        {
            AddNewLastPoint();
        }

        internal void ShowMessage(string message)
        {
            Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage(message, Symbol, MessageButton.OK, MessageIcon.Warning));
        }

        private void InitIntervals()
        {
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "250 ms", MeasureUnit = DateTimeMeasureUnit.Millisecond, MeasureUnitMultiplier = 250 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 sec", MeasureUnit = DateTimeMeasureUnit.Second, MeasureUnitMultiplier = 1 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "5 sec", MeasureUnit = DateTimeMeasureUnit.Second, MeasureUnitMultiplier = 5 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "10 sec", MeasureUnit = DateTimeMeasureUnit.Second, MeasureUnitMultiplier = 10 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "30 sec", MeasureUnit = DateTimeMeasureUnit.Second, MeasureUnitMultiplier = 30 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 min", MeasureUnit = DateTimeMeasureUnit.Minute, MeasureUnitMultiplier = 1 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "5 min", MeasureUnit = DateTimeMeasureUnit.Minute, MeasureUnitMultiplier = 5 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "15 min", MeasureUnit = DateTimeMeasureUnit.Minute, MeasureUnitMultiplier = 15 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "30 min", MeasureUnit = DateTimeMeasureUnit.Minute, MeasureUnitMultiplier = 30 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "45 min", MeasureUnit = DateTimeMeasureUnit.Minute, MeasureUnitMultiplier = 45 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 hour", MeasureUnit = DateTimeMeasureUnit.Hour, MeasureUnitMultiplier = 1 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "2 hour", MeasureUnit = DateTimeMeasureUnit.Hour, MeasureUnitMultiplier = 2 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "6 hour", MeasureUnit = DateTimeMeasureUnit.Hour, MeasureUnitMultiplier = 6 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "12 hour", MeasureUnit = DateTimeMeasureUnit.Hour, MeasureUnitMultiplier = 12 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 day", MeasureUnit = DateTimeMeasureUnit.Day, MeasureUnitMultiplier = 1 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 week", MeasureUnit = DateTimeMeasureUnit.Week, MeasureUnitMultiplier = 1 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "2 weeks", MeasureUnit = DateTimeMeasureUnit.Week, MeasureUnitMultiplier = 2 });
            IntervalsSource.Add(new ChartIntervalItem() { Caption = "1 month", MeasureUnit = DateTimeMeasureUnit.Month, MeasureUnitMultiplier = 1 });
        }

        [Command]
        public void ChartScroll(XYDiagram2DScrollEventArgs eventArgs)
        {
            if (eventArgs.AxisX != null)
            {
                if ((DateTime)eventArgs.AxisX.ActualVisualRange.ActualMinValue < (DateTime)eventArgs.AxisX.ActualWholeRange.ActualMinValue)
                {
                    AppendChartData();
                }
            }
        }

        private void AppendChartData()
        {
            AppendHistoricalData(SelectedInterval);
        }

        [Command]
        public void EmaTriggerStateChangedCommand()
        {
            if (EmaTriggerEnabled && PresetTriggerEnabled)
            {
                PresetTriggerEnabled = false;
            }
            PresetTriggerModel.Reset();
        }

        [Command]
        public void ResetPresetPrices()
        {
            PresetTriggerModel.Reset();
        }

        [Command]
        public void PresetTriggerStateChangedCommand()
        {
            if (EmaTriggerEnabled && PresetTriggerEnabled)
            {
                EmaTriggerEnabled = false;
            }
            PresetTriggerModel.Reset();
        }

        [Command]
        public void ChartZoom(XYDiagram2DZoomEventArgs eventArgs)
        {
            if (eventArgs.AxisX.DateTimeScaleOptions is ManualDateTimeScaleOptions scaleOptions)
            {
                TimeSpan measureUnitInterval = DateTimeHelper.GetInterval(scaleOptions.MeasureUnit, scaleOptions.MeasureUnitMultiplier);
                DateTime max = (DateTime)eventArgs.AxisX.ActualVisualRange.ActualMaxValue;
                DateTime min = (DateTime)eventArgs.AxisX.ActualVisualRange.ActualMinValue;
                TimeSpan duration = max - min;
                double visibleUnitsCount = duration.TotalSeconds / measureUnitInterval.TotalSeconds;
                if (visibleUnitsCount > maxVisiblePointsCount)
                {
                    eventArgs.AxisX.VisualRange.SetMinMaxValues(eventArgs.OldXRange.MinValue, eventArgs.OldXRange.MaxValue);
                }
            }
        }

        [Command]
        public void ShowElementPropertiesCommand(object args)
        {
            try
            {
                if (PresetTriggerEnabled && args != null && args is DiagramCoordinates chartHitInfo)
                {
                    double price = Math.Round(chartHitInfo.NumericalValue, 2);
                    if (price > CurrentPrice)
                    {
                        PresetTriggerModel.SellPrice = price;
                    }
                    else if (price < CurrentPrice)
                    {
                        PresetTriggerModel.BuyPrice = price;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public void AddSymbolCommand()
        {
            try
            {
                Symbols.Add(new StockModel(this));
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public void AddOrderTriggerCommand()
        {
            try
            {
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public void RemoveCommand(StockModel model)
        {
            try
            {
                model.Dispose();
                Symbols.Remove(model);
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public void AnnotationsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && e.NewItems.Count > 0)
            {
                foreach (Annotation annotation in e.NewItems)
                {
                    if (annotation.Content is EditableTextContent)
                    {
                        EditableTextContent textContent = annotation.Content as EditableTextContent;
                        textContent.SetBinding(EditableTextContent.EditModeEnabledProperty, new Binding("AnnotationEditing") { Mode = BindingMode.OneWayToSource });
                    }
                }
            }
        }

        [Command]
        public void DataChanged(RoutedEventArgs e)
        {
            if (e.Source is ChartControl chart)
            {
                InitChartRange(chart);
            }
        }

        [Command]
        public void CustomDrawCrosshair(CustomDrawCrosshairEventArgs e)
        {
            if (ShowCandles)
            {
                foreach (CrosshairLegendElement legendElement in e.CrosshairLegendElements)
                {
                    Color color = ((TradingData)legendElement.SeriesPoint.Tag).VolumeColor;
                    color.A = 255;
                    legendElement.Foreground = new SolidColorBrush(color);
                }
            }
        }

        [Command]
        public void LiquidateCommand()
        {
            MessageResult response = MessageBoxService.ShowMessage("Are you sure you want to close all positions?", Symbol, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
            if (response == MessageResult.Yes)
            {
                foreach (StockModel symbol in Symbols)
                {
                    symbol.ClosePositions();
                }
            }
        }

        [Command]
        public void Load()
        {
            foreach (StockModel symbol in Symbols)
            {
                if (string.IsNullOrWhiteSpace(symbol.Symbol))
                {
                    MessageBoxService.ShowMessage("Invalid symbol found.");
                    return;
                }
            }
            string symbolString = "";
            foreach (StockModel symbol in Symbols)
            {
                if (symbolString == "" && symbol.Ratio == 1)
                {
                    symbolString = symbol.Symbol;
                }
                else if (symbolString != "" && symbol.Ratio == 1)
                {
                    symbolString = symbolString + "*" + symbol.Symbol;
                }
                else if (symbolString != "" && symbol.Ratio > 1)
                {
                    symbolString = symbolString + "+" + symbol.Ratio + "*" + symbol.Symbol;
                }
                else
                {
                    symbolString = symbolString + symbol.Ratio + "*" + symbol.Symbol;
                }
                symbol.Init();
            }
            Symbol = symbolString;
            ModuleTitle = symbolString;
#if DEBUG
            SelectedInterval = IntervalsSource[0];
#else
            SelectedInterval = IntervalsSource[4];
#endif
            OnSelectedIntervalChanged();
            _portfolioManagerModel.Subscribe(InstanceId, SubscriptionFieldType.FirmInstancePosition, this);
            EmaEnabled = true;
        }

        [Command]
        public void Stop()
        {
            OrderEnabled = false;
            PresetTriggerModel.Reset();
            EmaEnabled = false;
            _intervalTimer.Stop();

            foreach (StockModel symbol in Symbols)
            {
                symbol.Stop();
            }

            Bid = double.NaN;
            Ask = double.NaN;
            BidEmaSignal = double.NaN;
            AskEmaSignal = double.NaN;
            Low = double.NaN;
            High = double.NaN;
            Ema = double.NaN;

            ResetEmaEvent?.Invoke();
            _emaCalculator.Reset();

            _portfolioManagerModel.Unsubscribe(InstanceId, SubscriptionFieldType.FirmInstancePosition, this);
            Dispatcher.BeginInvoke(() => ChartDataSource.Clear());
        }

        public bool Dispose()
        {
            OrderEnabled = false;
            EmaEnabled = false;

            foreach (StockModel symbol in Symbols)
            {
                symbol.Dispose();
            }

            Bid = double.NaN;
            Ask = double.NaN;
            BidEmaSignal = double.NaN;
            AskEmaSignal = double.NaN;
            Low = double.NaN;
            High = double.NaN;
            Ema = double.NaN;

            _portfolioManagerModel.Unsubscribe(Symbol, SubscriptionFieldType.FirmInstancePosition, this);
            OnSelectedIntervalChanged();

            return false;
        }

        [Command]
        public void OnSelectedIntervalChanged()
        {
            ReinitChartRange();
            GenerateInitialData();
        }

        public void Update()
        {
            try
            {
                double mid = 0.0;
                double volume = 0.0;
                double totalBid = 0.0;
                double totalAsk = 0.0;
                double totalLow = 0.0;
                double totalHigh = 0.0;
                for (int i = 0; i < Symbols.Count; i++)
                {
                    StockModel symbol = Symbols[i];

                    double ratioAbs = Math.Abs(symbol.Ratio);
                    int side = (int)(symbol.Ratio / ratioAbs);

                    double bid = ratioAbs * symbol.Bid;
                    double ask = ratioAbs * symbol.Ask;

                    double low = ratioAbs * symbol.Low;
                    double high = ratioAbs * symbol.High;

                    if (side == 1)
                    {
                        totalBid += side * bid;
                        totalAsk += side * ask;
                        totalLow += side * low;
                        totalHigh += side * high;
                    }
                    else
                    {
                        totalBid += side * ask;
                        totalAsk += side * bid;
                        totalLow += side * high;
                        totalHigh += side * low;
                    }

                    mid += symbol.Ratio * symbol.Mid;
                    volume += symbol.Volume;
                }

                if (!double.IsNaN(mid))
                {
                    CurrentPrice = mid;
                    _emaCalculator.AddUpdate(mid);

                    double hedgeMid = Math.Abs(mid);

                    if (LcdPosition > 0)
                    {
                        UnrealPnl = hedgeMid * LcdPosition;
                    }
                    else if (LcdPosition < 0)
                    {
                        UnrealPnl = hedgeMid * -LcdPosition;
                    }
                    else
                    {
                        UnrealPnl = double.NaN;
                    }
                }

                Bid = totalBid;
                Ask = totalAsk;

                UpdateSignals();

                Low = totalLow;
                High = totalHigh;

                UpdateLastPoint(mid, volume);

                UpdatePositionAndPnl();

                if (PresetTriggerEnabled && OrderEnabled)
                {
                    lock (_orderLock)
                    {
                        double buyPx = PresetTriggerModel.BuyPrice;
                        double sellPx = PresetTriggerModel.SellPrice;

                        if (!double.IsNaN(buyPx) && !double.IsNaN(sellPx))
                        {
                            if (Bid > sellPx)
                            {
                                bool send = true;
                                foreach (StockModel symbol in Symbols)
                                {
                                    if (symbol.WorkingQty != 0)
                                    {
                                        send = false;
                                        break;
                                    }
                                    if ((symbol.FilledQty >= 0 || symbol.Ratio >= 0) && (symbol.FilledQty <= 0 || symbol.Ratio <= 0))
                                    {
                                        send = false;
                                        break;
                                    }
                                }
                                if (send)
                                {
                                    SendSellOrder();
                                }
                            }
                            else if (Ask < buyPx)
                            {
                                bool send = true;
                                foreach (StockModel symbol in Symbols)
                                {
                                    if (symbol.WorkingQty != 0)
                                    {
                                        send = false;
                                        break;
                                    }
                                    if ((symbol.FilledQty < 0 && symbol.Ratio < 0) || (symbol.FilledQty > 0 && symbol.Ratio > 0))
                                    {
                                        send = false;
                                        break;
                                    }
                                }
                                if (send)
                                {
                                    SendBuyOrder();
                                }
                            }
                        }
                    }
                }
            }
            finally
            {

            }
        }

        private void UpdateSignals()
        {
            BidEmaSignal = Bid - Ema;
            AskEmaSignal = Ema - Ask;
        }

        private void InitChartRange(ChartControl chart)
        {
            if (!_initRange)
            {
                ((XYDiagram2D)chart.Diagram).ActualAxisX.ActualVisualRange.SetAuto();
                MinVisibleDate = DateTime.Now - DateTimeHelper.ConvertInterval(SelectedInterval, initialVisiblePointsCount);
                _initRange = true;
            }
        }

        private void ReinitChartRange()
        {
            _initRange = false;
        }

        private void UpdateCrosshairText()
        {
            if (_lastPoint != null)
            {
                //CrosshairCurrentFinancialText = $"O{_lastPoint.Open:f2}\tH{_lastPoint.High:f2}\tL{_lastPoint.Low:f2}\tC{_lastPoint.Close:f2}\t";
                //CrosshairCurrentVolumeText = $"{_lastPoint.Volume:F0}";
            }
        }

        private void GenerateInitialData()
        {
            if (Symbol != null)
            {
                Init(SelectedInterval, CurrentPrice);
            }
        }

        public void Init(ChartIntervalItem interval, double basePrice)
        {
            ChartDataSource.Clear();
            ChartDataPoints.Clear();
            DateTime date = DateTimeHelper.GetInitialDate(interval);
            InsertPoints(interval, basePrice, date);
            _intervalTimer.SetInterval(interval);
        }

        protected void OnAnnotationEditingChanged()
        {
            if (AnnotationEditing)
            {
                SuspendUpdate();
            }
            else
            {
                ResumeUpdate();
            }
        }

        private void AddNewLastPoint()
        {
            DateTime timeStamp = DateTime.Now;
            TradingData lastData = _lastPoint;
            if (_lastPoint != null)
            {
                double value = lastData.Close;

                _lastPoint = new TradingData(timeStamp, value, value, value, value, 0);
                ChartDataSource.Add(_lastPoint);

                ChartDataPointModel prevPoint = _lastChartPoint;
                if (prevPoint != null)
                {
                    _lastChartPoint = new ChartDataPointModel()
                    {
                        Timestamp = timeStamp,
                        Bid = prevPoint.Bid,
                        Ask = prevPoint.Ask,
                        Mid = prevPoint.Mid,
                        Ema = prevPoint.Ema,
                    };
                }
                else
                {
                    _lastChartPoint = new ChartDataPointModel()
                    {
                        Timestamp = timeStamp,
                    };
                }

                ChartDataPoints.Add(_lastChartPoint);

                if (lastData.UpdateSuspended)
                {
                    lastData.ResumeUpdate();
                    _lastPoint.SuspendUpdate();
                }
            }
        }

        private DateTime GetPreviousPointDate(DateTime pointDate, ChartIntervalItem interval)
        {
            DateTime newDate;
            switch (interval.MeasureUnit)
            {
                case DateTimeMeasureUnit.Second:
                    newDate = pointDate.AddSeconds(-interval.MeasureUnitMultiplier);
                    if (newDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        newDate = newDate.Date.AddSeconds(-interval.MeasureUnitMultiplier);
                    }

                    if (newDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        newDate = newDate.Date.AddDays(-1).AddSeconds(-interval.MeasureUnitMultiplier);
                    }

                    return newDate;
                case DateTimeMeasureUnit.Minute:
                    newDate = pointDate.AddMinutes(-interval.MeasureUnitMultiplier);
                    if (newDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        newDate = newDate.Date.AddMinutes(-interval.MeasureUnitMultiplier);
                    }

                    if (newDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        newDate = newDate.Date.AddDays(-1).AddMinutes(-interval.MeasureUnitMultiplier);
                    }

                    return newDate;
                case DateTimeMeasureUnit.Hour:
                    newDate = pointDate.AddHours(-interval.MeasureUnitMultiplier);
                    if (newDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        newDate = newDate.Date.AddHours(-interval.MeasureUnitMultiplier);
                    }

                    if (newDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        newDate = newDate.Date.AddDays(-1).AddHours(-interval.MeasureUnitMultiplier);
                    }

                    return newDate;
                case DateTimeMeasureUnit.Day:
                    newDate = pointDate.AddDays(-interval.MeasureUnitMultiplier);
                    if (newDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        newDate = newDate.Date.AddDays(-interval.MeasureUnitMultiplier);
                    }

                    if (newDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        newDate = newDate.Date.AddDays(-(interval.MeasureUnitMultiplier + 1));
                    }

                    return newDate;
                case DateTimeMeasureUnit.Week:
                    newDate = pointDate.AddDays(-interval.MeasureUnitMultiplier * 7);
                    return newDate;
                case DateTimeMeasureUnit.Month:
                    newDate = pointDate.AddMonths(-interval.MeasureUnitMultiplier);
                    newDate = new DateTime(newDate.Year, newDate.Month, 1);
                    if (newDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        newDate = newDate.Date.AddDays(2);
                    }

                    if (newDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        newDate = newDate.Date.AddDays(1);
                    }

                    return newDate;
            }
            return pointDate;
        }

        private void InsertPoints(ChartIntervalItem interval, double currentPrice, DateTime date)
        {
            double open = currentPrice;
            double close = currentPrice;
            double low = currentPrice;
            double high = currentPrice;
            ChartDataSource.BeginUpdate();
            _lastPoint = new TradingData(date, currentPrice, currentPrice, currentPrice, currentPrice, 0);
            ChartDataSource.Insert(0, _lastPoint);
            _lastChartPoint = new ChartDataPointModel()
            {
                Timestamp = date,
            };

            Dispatcher.BeginInvoke(() =>
            {
                ChartDataPoints.Insert(0, _lastChartPoint);
                ChartDataSource.EndUpdate();
            });
        }

        public void AppendHistoricalData(ChartIntervalItem interval)
        {
            DateTime date = GetPreviousPointDate(ChartDataSource[0].Date, interval);
            InsertPoints(interval, ChartDataSource[0].Low, ChartDataSource[0].Date);
        }

        public void UpdateLastPoint(double price, double volume)
        {
            if (_lastPoint != null)
            {
                double high = _lastPoint.High;
                if (price > high)
                {
                    high = price;
                }

                double low = _lastPoint.Low;
                if (price < low)
                {
                    low = price;
                }

                Dispatcher.BeginInvoke(() =>
                {
                    _lastPoint.Low = low;
                    _lastPoint.High = high;
                    _lastPoint.Close = price;
                    _lastPoint.Volume = volume;

                    _lastChartPoint.Bid = Bid;
                    _lastChartPoint.Ask = Ask;
                    _lastChartPoint.Ema = Ema;
                    _lastChartPoint.Mid = CurrentPrice;
                });
            }
        }

        public void SuspendUpdate()
        {
            _lastPoint?.SuspendUpdate();
        }
        public void ResumeUpdate()
        {
            _lastPoint?.ResumeUpdate();
        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            LoadDefaultAccount();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                switch (key.Type)
                {
                    case SubscriptionFieldType.FirmInstancePosition:
                        HandlePositionUpdate(value as IPosition);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        public void HandlePositionUpdate(IPosition instancePosition)
        {
            try
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePositionUpdate));
            }
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
            if (update.ClientOrderId == OrderId)
            {
                Status = update.CurrentStatus + " " + update.Volume + "@" + update.Price;
                StatusMode = update.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? StatusMode.CancelledBuy : StatusMode.CancelledSell;
            }
            else if (update.ClientOrderId == ContraOrderId)
            {
                StatusSell = update.CurrentStatus + " " + update.Volume + "@" + update.Price;
                StatusModeSell = update.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? StatusMode.CancelledBuy : StatusMode.CancelledSell;
            }
        }

        public void OrderUpdated(OrderUpdateValues orderUpdate)
        {
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
        }

        public void AutomationStateChanged(bool running)
        {
        }
    }
}
