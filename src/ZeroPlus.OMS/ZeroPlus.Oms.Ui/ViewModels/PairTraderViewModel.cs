using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PairTraderViewModel : CustomizableTableViewModelBase, IOrderInfoUpdateHandler, IEmaConfig
    {
        private readonly IModuleFactory _moduleFactory;
        private const int initialVisiblePointsCount = 180;
        private const int maxVisiblePointsCount = 800;
        private const double BASE_CHANGE = .02;
        private const string OPENING_ID = "<";
        private const string CLOSING_ID = ">";
        private const int MAX_LOOP_COUNT = 50;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly List<string> PairAccounts = ["TBK1501002", "DDEMO40"];

        public IEnumerable<Currency> CurrencyOptions { get; } = ((Currency[])Enum.GetValues(typeof(Currency))).ToList();
        public IEnumerable<ExecutionStyle> ExecutionStyles { get; } = ((ExecutionStyle[])Enum.GetValues(typeof(ExecutionStyle))).ToList();
        public IEnumerable<CancelMode> CancelModes { get; } = ((CancelMode[])Enum.GetValues(typeof(CancelMode))).ToList();
        public IEnumerable<Side> Sides { get; } = ((Side[])Enum.GetValues(typeof(Side))).Where(x => x != ZeroPlus.Models.Data.Enums.Side.BuyToCover).ToList();
        public IEnumerable<InitSide> InitSides { get; } = ((InitSide[])Enum.GetValues(typeof(InitSide))).ToList();
        public IEnumerable<TimeInForce> TimeInForces { get; } = ((TimeInForce[])Enum.GetValues(typeof(TimeInForce))).ToList();
        public IEnumerable<PairOperator> PairOperators { get; } = ((PairOperator[])Enum.GetValues(typeof(PairOperator))).ToList();
        public IEnumerable<PairDirection> PairDirections { get; } = ((PairDirection[])Enum.GetValues(typeof(PairDirection))).ToList();
        public IEnumerable<PairSide> PairSides { get; } = ((PairSide[])Enum.GetValues(typeof(PairSide))).ToList();
        public IEnumerable<OrderType> OrderTypes { get; } = ((OrderType[])Enum.GetValues(typeof(OrderType))).ToList();
        public IEnumerable<DataType> DataTypes { get; } = ((DataType[])Enum.GetValues(typeof(DataType))).ToList();
        public IEnumerable<PairsType> TriggerMethods { get; } = ((PairsType[])Enum.GetValues(typeof(PairsType))).ToList();
        public IEnumerable<TriggerMethod> ManualTriggerMethods { get; } = ((TriggerMethod[])Enum.GetValues(typeof(TriggerMethod))).ToList();
        public IEnumerable<PairTriggerType> PairTriggerTypes { get; } = ((PairTriggerType[])Enum.GetValues(typeof(PairTriggerType))).ToList();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public ICurrentWindowService WindowService => GetService<ICurrentWindowService>();

        private InitSide _buyInitialSize = InitSide.Auto;
        private InitSide _sellInitialSize = InitSide.Auto;
        private ConcurrentDictionary<string, PairOrderModel> _orderIdToOrderModelMap = new();
        private ObservableCollection<string> _accountsList;
        private ObservableCollection<PairOrderModel> _allPairOrders;
        private ObservableCollection<PairOrderModel> _fillsOnlyPairOrders;


        private readonly object _closeTimeLookupLock;
        private readonly EmaCalculator _bidEmaCalculator;
        private readonly EmaCalculator _midEmaCalculator;
        private readonly EmaCalculator _askEmaCalculator;
        private readonly System.Timers.Timer _orderTimer;
        private double _currentPrice;


        private bool _reversed;
        private int _autoCancel = 900;
        private List<PairTriggerModel> _orderedBuyTriggers;
        private List<PairTriggerModel> _orderedSellTriggers;
        private double _rounding = 4;
        private double _lowestAskPrice = double.NaN;
        private Side? _warnSide;


        private readonly object _orderLock = new();
        private readonly object _stopLossLock = new();
        private double _minCloseSec = 120;

        public event ResetEmaEventHandler ResetEmaEvent;

        public Dispatcher Dispatcher { get; private set; }
        public ModelTradersManagerModel ManagerModel { get; }
        [Bindable]
        public partial List<PairLegModel> Symbols { get; set; }
        public double Rounding
        {
            get => _rounding;
            set
            {
                SetValue(ref _rounding, value);
                RoundingMask = "N" + value;
            }
        }
        [Bindable(Default = "N4")]
        public partial string RoundingMask { get; set; }
        [Bindable(Default = 3)]
        public partial int BuyTiersCount { get; set; }
        [Bindable(Default = .05)]
        public partial double BuyTiersSpacing { get; set; }
        [Bindable(Default = .05)]
        public partial double BuyTiersStart { get; set; }
        [Bindable(Default = .05)]
        public partial double BuyProfitStart { get; set; }
        [Bindable(Default = .05)]
        public partial double BuyTiersProfitSpacing { get; set; }
        [Bindable(Default = 3)]
        public partial int SellTiersCount { get; set; }
        [Bindable(Default = .05)]
        public partial double SellTiersSpacing { get; set; }
        [Bindable(Default = .05)]
        public partial double SellTiersStart { get; set; }
        [Bindable(Default = .05)]
        public partial double SellProfitStart { get; set; }
        [Bindable(Default = .05)]
        public partial double SellTiersProfitSpacing { get; set; }
        [Bindable(Default = .1)]
        public partial double BuyPercentChange { get; set; }
        [Bindable(Default = .1)]
        public partial double SellPercentChange { get; set; }
        [Bindable(Default = true)]
        public partial bool CloseOrders { get; set; }
        [Bindable(Default = true)]
        public partial bool RestOrders { get; set; }
        [Bindable]
        public partial bool CloseByAvgCloseTime { get; set; }
        [Bindable]
        public partial bool BlockReentryAfterAvgTimeClose { get; set; }
        [Bindable]
        public partial bool BlockReentryAfterStoploss { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AvgCloseTime { get; set; }
        [Bindable]
        public partial bool EmaTriggerEnabled { get; set; }
        [Bindable(Default = true)]
        public partial bool PresetTriggerEnabled { get; set; }
        [Bindable]
        public partial bool ManualMode { get; set; }
        [Bindable]
        public partial bool OrderEnabled { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string ReverseSymbol { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        public double Mid
        {
            get => _currentPrice;
            set => SetValue(ref _currentPrice, value);
        }
        [Bindable]
        public partial double Bid { get; set; }
        [Bindable]
        public partial double Ask { get; set; }
        [Bindable]
        public partial double HighestBid { get; set; }
        [Bindable]
        public partial double LowestAsk { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AvgBuyPrice { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AvgSellPrice { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double HighestBuyPrice { get; set; }
        public double LowestSellPrice
        {
            get => _lowestAskPrice;
            set => SetValue(ref _lowestAskPrice, value);
        }
        [Bindable(Default = .75)]
        public partial double StopLoss { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SpreadTriggerValue { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SpreadSellTriggerValue { get; set; }
        [Bindable(Default = 1)]
        public partial int BuyManualQty { get; set; }
        [Bindable(Default = 1)]
        public partial int SellManualQty { get; set; }
        [Bindable]
        public partial double BidEma { get; set; }
        [Bindable]
        public partial double Ema { get; set; }
        [Bindable]
        public partial double AskEma { get; set; }
        [Bindable(Default = 80)]
        public partial double EmaPeriods { get; set; }
        [Bindable]
        public partial double LastPnl { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double AdjPnl { get; set; }
        [Bindable]
        public partial int TotalQty { get; set; }
        [Bindable]
        public partial int TotalBuyQty { get; set; }
        [Bindable]
        public partial int TotalSellQty { get; set; }
        [Bindable]
        public partial double Fees { get; set; }
        [Bindable(Default = .0000278)]
        public partial double SecFees { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial bool EmaEnabled { get; set; }
        [Bindable]
        public partial EmaType SelectedEmaType { get; set; }
        [Bindable]
        public partial double PercentVegaThreshold { get; set; }
        [Bindable]
        public partial double EmaSmoothing { get; set; }
        [Bindable]
        public partial double EmaInterval { get; set; }
        [Bindable]
        public partial bool LockedEma { get; set; }
        [Bindable]
        public partial double LockedEmaValue { get; set; }
        [Bindable]
        public partial double MaxBidDeviation { get; set; }
        [Bindable]
        public partial double MaxAskDeviation { get; set; }
        [Bindable]
        public partial int LcdPosition { get; set; }
        public double _RestingOrderCancelDelay;
        [Bindable]
        public partial double RestingOrderCancelDelay { get; set; }
        [Bindable]
        public partial double RestingOrderCloseEdge { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial string ContraStatus { get; set; }
        [Bindable]
        public partial StatusMode ContraStatusMode { get; set; }
        DataType _DataType;
        private DateTime _lastCheck;

        [Bindable]
        public partial DataType DataType { get; set; }
        [Bindable]
        public partial Currency TriggerValueCurrency { get; set; }
        [Bindable(Default = PairsType.Spread)]
        public partial PairsType TriggerMethod { get; set; }
        [Bindable(Default = ZeroPlus.Models.Data.Enums.TriggerMethod.SBS)]
        public partial TriggerMethod BuyTriggerMethod { get; set; }
        [Bindable(Default = ZeroPlus.Models.Data.Enums.TriggerMethod.SSB)]
        public partial TriggerMethod SellTriggerMethod { get; set; }
        [Bindable(Default = PairTriggerType.EMA)]
        public partial PairTriggerType PairTriggerType { get; set; }
        [Bindable(Default = PairTriggerType.EMA)]
        public partial PairTriggerType PairProfitType { get; set; }
        [Bindable(Default = CancelMode.All)]
        public partial CancelMode CancelMode { get; set; }
        [Bindable(Default = true)]
        public partial bool AutoTriggerMethod { get; set; }
        [Bindable]
        public partial ExecutionStyle BuyExecutionStyle { get; set; }
        [Bindable]
        public partial ExecutionStyle SellExecutionStyle { get; set; }
        [Bindable]
        public partial ExecutionStyle BuyAutoExecutionStyle { get; set; }
        [Bindable]
        public partial ExecutionStyle SellAutoExecutionStyle { get; set; }
        [Bindable]
        public partial Side Side { get; set; }
        public InitSide BuyInitialSide
        {
            get => _buyInitialSize;
            set => SetValue(ref _buyInitialSize, value);
        }
        public InitSide SellInitialSide
        {
            get => _sellInitialSize;
            set => SetValue(ref _sellInitialSize, value);
        }
        [Bindable]
        public partial TimeInForce TimeInForce { get; set; }

        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> AccountsList { get; set; }
        [Bindable]
        public partial string Account { get; set; }
        [Bindable(Default = "APEX")]
        public partial string Locate { get; set; }
        [Bindable]
        public partial PairOrderModel LastPairOrder { get; set; }
        [Bindable]
        public partial bool IsLoaded { get; set; }
        [Bindable]
        public partial ObservableCollection<PairOrderModel> PairOrders { get; set; }
        [Bindable]
        public partial int SpreadQty { get; set; }
        public int AutoCancel
        {
            get => _autoCancel;
            set
            {
                if (value <= 0)
                {
                    value = 0;
                }
                else if (value <= 60)
                {
                    value = 60;
                }
                SetValue(ref _autoCancel, value);
            }
        }
        [Bindable]
        public partial double TriggerProximity { get; set; }

        [Bindable(Default = 15000)]
        public partial double TriggerTimer { get; set; }

        [Bindable(Default = .05)]
        public partial double CancelTrigger { get; set; }

        [Bindable(Default = false)]
        public partial bool ShowWarning { get; set; }

        [Bindable(Default = false)]
        public partial bool ShowBuyWarning { get; set; }

        [Bindable(Default = false)]
        public partial bool ShowSellWarning { get; set; }

        [Bindable]
        public partial Side? WarnSide { get; set; }

        [Bindable]
        public partial TriggerMethod ManualOrderTriggerMethod { get; set; }

        [Bindable]
        public partial double ManualOrderTriggerValue { get; set; }

        [Bindable]
        public partial ExecutionStyle ManualExecutionStyle { get; set; }
        [Bindable]
        public partial Currency ManualTriggerValueCurrency { get; set; }
        [Bindable]
        public partial InitSide ManualInitialSide { get; set; }
        [Bindable]
        public partial string ManualLeg1Symbol { get; set; }
        [Bindable]
        public partial Side ManualLeg1Side { get; set; }
        [Bindable]
        public partial int ManualLeg1Qty { get; set; }
        [Bindable]
        public partial string ManualLeg2Symbol { get; set; }
        [Bindable]
        public partial Side ManualLeg2Side { get; set; }
        [Bindable]
        public partial int ManualLeg2Qty { get; set; }
        [Bindable]
        public partial double ManualBuyTermsRatio { get; set; }
        [Bindable]
        public partial double ManualSellTermsRatio { get; set; }
        [Bindable]
        public partial DateTime StopTime { get; set; }
        [Bindable]
        public partial DateTime CloseTime { get; set; }
        [Bindable(Default = 3600)]
        public partial double AvgCloseTimeLookbackSeconds { get; set; }
        [Bindable(Default = 1)]
        public partial double AvgCloseTimeMultiplier { get; set; }
        public double MinCloseTimeSec
        {
            get => _minCloseSec;
            set => SetValue(ref _minCloseSec, value);
        }
        [Bindable(Default = 30)]
        public partial int HistoricAvgCloseTimeEmaPeriod { get; set; }

        public PairLegModel PairLeg1 { get; }
        public PairLegModel PairLeg2 { get; }
        public string Uid { get; internal set; }
        public string Name { get; internal set; }
        public int LoopCount { get; private set; }
        public DateTime LoopCountLastReset { get; private set; }
        public PairOrderModel ManualBuyOrderId { get; private set; }
        public PairOrderModel ManualSellOrderId { get; private set; }
        public List<Tuple<DateTime, double>> CloseTimes { get; }
        public FastObservableCollection<PairTriggerModel> BuyTriggers { get; } = new FastObservableCollection<PairTriggerModel>();
        public FastObservableCollection<PairTriggerModel> SellTriggers { get; } = new FastObservableCollection<PairTriggerModel>();
        public bool BuyEntryBlockedByAvgTimeClose { get; set; }
        public bool BuyEntryBlockedByStoploss { get; set; }
        public bool SellEntryBlockedByAvgTimeClose { get; set; }
        public bool SellEntryBlockedByStoploss { get; set; }
        public MacdContext MacdContext { get; set; }

        public PairTraderViewModel(ModelTradersManagerModel managerModel, IModuleFactory moduleFactory)
        {
            StopTime = (DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(50)).FromEastern();
            CloseTime = (DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(59)).FromEastern();
            _moduleFactory = moduleFactory;
            _closeTimeLookupLock = new object();
            _orderTimer = new System.Timers.Timer()
            {
                Interval = 2500,
                AutoReset = false
            };
            _orderTimer.Elapsed += OnOrderTimerElapsed;
            CloseTimes = new();
            ManualLeg1Side = ZeroPlus.Models.Data.Enums.Side.Buy;
            ManualLeg2Side = ZeroPlus.Models.Data.Enums.Side.Sell;

            ManualInitialSide = InitSide.Auto;

            ManualBuyTermsRatio = 1;
            ManualSellTermsRatio = 1;

            EmaSmoothing = 2;
            EmaPeriods = 80;
            EmaInterval = 5000;
            RestingOrderCancelDelay = 5000;
            RestingOrderCloseEdge = 0.01;

            Bid = double.NaN;
            Ask = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            BidEma = double.NaN;
            Ema = double.NaN;
            AskEma = double.NaN;
            Mid = double.NaN;

            ManagerModel = managerModel;
            ManagerModel?.AddTrader(this);

            _bidEmaCalculator = new EmaCalculator(this, SubscriptionFieldType.Bid);
            _midEmaCalculator = new EmaCalculator(this, SubscriptionFieldType.MidPoint);
            _askEmaCalculator = new EmaCalculator(this, SubscriptionFieldType.Ask);

            _bidEmaCalculator.EmaUpdatedEvent += OnBidEmaUpdatedEvent;
            _midEmaCalculator.EmaUpdatedEvent += OnEmaUpdatedEvent;
            _askEmaCalculator.EmaUpdatedEvent += OnAskEmaUpdatedEvent;

            PairLeg1 = new PairLegModel(this)
            {
                Symbol = "",
                Quantity = 1,
                Side = ZeroPlus.Models.Data.Enums.Side.Buy,
            };
            PairLeg2 = new PairLegModel(this)
            {
                Symbol = "",
                Quantity = 1,
                Side = ZeroPlus.Models.Data.Enums.Side.Sell,
            };
            Symbols = new List<PairLegModel>
            {
                PairLeg1,
                PairLeg2,
            };
            _allPairOrders = new ObservableCollection<PairOrderModel>();
            _fillsOnlyPairOrders = new ObservableCollection<PairOrderModel>();
            PairOrders = _allPairOrders;
        }

        public void SetupMacdAutoTrading()
        {
            MacdContext.MacdEntryLongEvent += MacdContext_MacdEntryLongEvent;
            MacdContext.MacdExitLongEvent += MacdContext_MacdExitLongEvent;
            MacdContext.MacdEntryShortEvent += MacdContext_MacdEntryShortEvent;
            MacdContext.MacdExitShortEvent += MacdContext_MacdExitShortEvent;

            _log.Debug("Tied Macd AutoTrading Event Handlers");
        }

        private void MacdContext_MacdAutoOrderFilled(PairOrderModel order, OrderStatus orderStatus)
        {
            if (orderStatus is OrderStatus.Filled or OrderStatus.PartiallyFilled)
            {
                if (order.Type == PositionEffect.Open)
                {
                    MacdContext.LivePositionOrder = order;
                }
                else if (order.Type == PositionEffect.Close)
                {
                    MacdContext.LivePositionOrder = null;
                }
            }
        }

        private void MacdContext_MacdExitShortEvent(object sender, EventArgs e)
        {
            var closingOrder = ExitShortAutoMacd(MacdContext.LivePositionOrder);
            closingOrder.OrderStatusChanged += MacdContext_MacdAutoOrderFilled;
        }
        private void MacdContext_MacdExitLongEvent(object sender, EventArgs e)
        {
            var closingOrder = ExitLongAutoMacd(MacdContext.LivePositionOrder);
            closingOrder.OrderStatusChanged += MacdContext_MacdAutoOrderFilled;
        }
        private void MacdContext_MacdEntryShortEvent(object sender, EventArgs e)
        {
            var openingOrder = EnterShortAutoMacd();
            openingOrder.OrderStatusChanged += MacdContext_MacdAutoOrderFilled;
        }
        private void MacdContext_MacdEntryLongEvent(object sender, EventArgs e)
        {
            var openingOrder = EnterLongAutoMacd();
            openingOrder.OrderStatusChanged += MacdContext_MacdAutoOrderFilled;
        }

        private PairOrderModel EnterLongAutoMacd() =>
            SendBuy(Ask, BuyManualQty, PositionEffect.Open,
            tag: $"Macd={MacdContext.MacdValue} OPEN",
            buyExecutionStyle: BuyAutoExecutionStyle);

        private PairOrderModel ExitLongAutoMacd(PairOrderModel open) =>
            SendSell(Bid, open.Quantity, PositionEffect.Close,
            tag: $"Macd={MacdContext.MacdValue} CLOSE",
            sellExecutionStyle: SellAutoExecutionStyle,
            openingOrder: open);

        private PairOrderModel EnterShortAutoMacd() =>
            SendSell(Bid, SellManualQty, PositionEffect.Open,
            tag: $"Macd={MacdContext.MacdValue} OPEN",
            sellExecutionStyle: SellAutoExecutionStyle);

        private PairOrderModel ExitShortAutoMacd(PairOrderModel open) =>
            SendBuy(Ask, open.Quantity, PositionEffect.Close,
            tag: $"Macd={MacdContext.MacdValue} CLOSE",
            buyExecutionStyle: BuyAutoExecutionStyle,
            openingOrder: open);

        private void OnOrderTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (IsLoaded)
                {
                    UpdatePositionAndPnl();
                    if ((DateTime.Now - _lastCheck).TotalMilliseconds > TriggerTimer)
                    {
                        _lastCheck = DateTime.Now;
                        CheckForOrder();
                    }
                }
            }
            finally
            {
                _orderTimer.Start();
            }
        }

        [Command]
        public void OpenEmaChartCommand()
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                EmaChartView window = new(_moduleFactory);
                EmaChartViewModel viewModel = (EmaChartViewModel)window.DataContext;
                viewModel.SetDispatcher(window.Dispatcher);

                viewModel.Ready += (IModuleViewModel module) =>
                {
                    viewModel.Symbol = Symbol;
                    _ = viewModel.SearchCommand();
                };

                window.Dispatcher.UnhandledException += (s, e) =>
                {
                    _log.Error(e.Exception, "DispatcherUnhandledException");
                    e.Handled = true;
                };

                window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                window.Show();

                Dispatcher.Run();
            });
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.Start();
        }

        [Command]
        public void RaiseBuysByPercentCommand()
        {
            BuyTiersSpacing += BuyTiersSpacing * BuyPercentChange;
            BuyTiersProfitSpacing += BuyTiersProfitSpacing * BuyPercentChange;
        }

        [Command]
        public void ReduceBuysByPercentCommand()
        {
            BuyTiersSpacing -= BuyTiersSpacing * BuyPercentChange;
            BuyTiersProfitSpacing += BuyTiersProfitSpacing * BuyPercentChange;
            if (BuyTiersSpacing < 0)
            {
                BuyTiersSpacing = 0;
            }
            if (BuyTiersProfitSpacing < 0)
            {
                BuyTiersProfitSpacing = 0;
            }
        }

        [Command]
        public void RaiseSellsByPercentCommand()
        {
            SellTiersSpacing += SellTiersSpacing * SellPercentChange;
            SellTiersProfitSpacing += SellTiersProfitSpacing * SellPercentChange;
        }

        [Command]
        public void ReduceSellsByPercentCommand()
        {
            SellTiersSpacing -= SellTiersSpacing * SellPercentChange;
            SellTiersProfitSpacing -= SellTiersProfitSpacing * SellPercentChange;
            if (SellTiersSpacing < 0)
            {
                SellTiersSpacing = 0;
            }
            if (SellTiersProfitSpacing < 0)
            {
                SellTiersProfitSpacing = 0;
            }
        }

        [Command]
        public void ResetHighBidLowAskCommand()
        {
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
        }

        [Command]
        public async void OnOrderStoppedCommand()
        {
            if (WorkingOrders().Count > 0)
            {
                if (DateTime.Now.TimeOfDay >= StopTime.TimeOfDay)
                {
                    CancelAllOpening();
                    return;
                }
                else
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        MessageResult results = MessageBoxService.ShowMessage("Do you want to cancel all opening orders?", Symbol, MessageButton.YesNo, MessageIcon.Question);
                        if (results == MessageResult.Yes)
                        {
                            CancelAllOpening();
                        }
                    });
                }
            }
        }

        [Command]
        public async Task AddBuyTriggerCommand()
        {
            double changeToEma = BuyTriggers.OrderBy(x => x.ChangeToEma).LastOrDefault()?.ChangeToEma ?? 0;
            PairTriggerModel model = new()
            {
                Side = ZeroPlus.Models.Data.Enums.Side.Buy,
                ChangeToEma = changeToEma + BASE_CHANGE,
                ProfitTarget = BASE_CHANGE,
            };
            await Dispatcher?.BeginInvoke(() => BuyTriggers.Add(model));
            UpdateTarget();
        }

        [Command]
        public async Task AddSellTriggerCommand()
        {
            double changeToEma = SellTriggers.OrderBy(x => x.ChangeToEma).LastOrDefault()?.ChangeToEma ?? 0;
            PairTriggerModel model = new()
            {
                Side = ZeroPlus.Models.Data.Enums.Side.Sell,
                ChangeToEma = changeToEma + BASE_CHANGE,
                ProfitTarget = BASE_CHANGE,
            };
            await Dispatcher?.BeginInvoke(() => SellTriggers.Add(model));
            UpdateTarget();
        }

        [Command]
        public void ResetCommand(PairTriggerModel model)
        {
            CancelFromTrigger(model);
            model.Reset();
            UpdateTarget();
        }

        [Command]
        public async Task RemoveBuyTriggerCommand(PairTriggerModel model)
        {
            model.Disposed = true;
            await Dispatcher?.BeginInvoke(() => BuyTriggers.Remove(model));
            UpdateTarget();
        }

        [Command]
        public async Task RemoveSellTriggerCommand(PairTriggerModel model)
        {
            model.Disposed = true;
            await Dispatcher?.BeginInvoke(() => SellTriggers.Remove(model));
            UpdateTarget();
        }

        [Command]
        public void SendManualOrder()
        {
            try
            {
                if (double.IsNaN(ManualOrderTriggerValue))
                {
                    Status = "Invalid Trigger Value";
                    StatusMode = StatusMode.CancelledSell;
                    return;
                }

                Status = "";
                StatusMode = StatusMode.Reset;

                string orderId = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId1 = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId2 = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();

                PairOrderRequest pairOrder = new()
                {
                    Account = Account,
                    TriggerMethod = ManualOrderTriggerMethod.ToString(),
                    TriggerValue = Math.Round(ManualOrderTriggerValue, 4),
                    Style = ManualExecutionStyle.ToString().ToUpper(),
                    ClientOrderId = orderId,
                    TriggerValueCurrency = ManualTriggerValueCurrency.ToString(),
                    InitSide = ManualInitialSide,
                    Locate = Locate,

                    ClientOrderIdLeg1 = orderId1,
                    Leg1Symbol = ManualLeg1Symbol,
                    Leg1Side = ManualLeg1Side,
                    Leg1Quantity = ManualLeg1Qty,

                    ClientOrderIdLeg2 = orderId2,
                    Leg2Symbol = ManualLeg2Symbol,
                    Leg2Side = ManualLeg2Side,
                    Leg2Quantity = ManualLeg2Qty,

                    TimeInForce = TimeInForce,

                    BuyTermsRatio = ManualBuyTermsRatio,
                    SellTermsRatio = ManualSellTermsRatio,
                };

                SendOrder(pairOrder, 1, PairLeg1.Side, PositionEffect.AUTO);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendManualOrder));
            }
        }

        [Command]
        public void SendBuyOrder()
        {
            ManualBuyOrderId = SendBuy(null, BuyManualQty, PositionEffect.AUTO, null, "Manual", BuyExecutionStyle);
        }

        [Command]
        public void SendSellOrder()
        {
            ManualSellOrderId = SendSell(null, SellManualQty, PositionEffect.AUTO, null, "Manual", SellExecutionStyle);
        }

        [Command]
        public void CancelOrder(PairOrderRequest pairOrder)
        {
            pairOrder.PairOrderRequestType = PairOrderRequestType.Cancel;
            _log.Error(nameof(CancelOrder), pairOrder.ClientOrderId);
            OmsCore.AutoTraderClient.SendPairOrder(pairOrder, this);
        }

        [Command]
        public void CancelAll()
        {
            foreach (PairOrderModel pairOrder in WorkingOrders())
            {
                CancelOrder(pairOrder.OrderRequest);
            }
        }

        [Command]
        public void BuyTierChangedCommand()
        {
            if (BuyTriggers.Count < BuyTiersCount)
            {
                double changeToEma = BuyTriggers.OrderBy(x => x.ChangeToEma).LastOrDefault()?.ChangeToEma ?? BuyTiersStart;
                double lastTierProfit = BuyTriggers.OrderBy(x => x.ProfitTarget).LastOrDefault()?.ProfitTarget ?? BuyProfitStart;
                int missing = BuyTiersCount - BuyTriggers.Count;
                for (int i = 0; i < missing; i++)
                {
                    lastTierProfit += BuyTiersProfitSpacing;
                    PairTriggerModel model = new()
                    {
                        Side = ZeroPlus.Models.Data.Enums.Side.Buy,
                        ChangeToEma = Math.Round(changeToEma, (int)Rounding),
                        ProfitTarget = Math.Round(lastTierProfit, (int)Rounding),
                    };
                    changeToEma += BuyTiersSpacing;
                    Dispatcher?.Invoke(() => BuyTriggers.Add(model));
                }
            }

            IOrderedEnumerable<PairTriggerModel> buyTriggers = BuyTriggers.OrderBy(x => x.ChangeToEma);
            double nextTier = BuyTiersStart;
            double nextTierProfit = BuyProfitStart;
            foreach (PairTriggerModel buyTrigger in buyTriggers)
            {
                buyTrigger.ChangeToEma = Math.Round(nextTier, (int)Rounding);
                buyTrigger.ProfitTarget = Math.Round(nextTierProfit, (int)Rounding);
                nextTier += BuyTiersSpacing;
                nextTierProfit += BuyTiersProfitSpacing;
            }

            UpdateTarget();
        }

        [Command]
        public void SellTierChangedCommand()
        {
            if (SellTriggers.Count < SellTiersCount)
            {
                double changeToEma = SellTriggers.OrderBy(x => x.ChangeToEma).LastOrDefault()?.ChangeToEma ?? SellTiersStart;
                double lastTierProfit = SellTriggers.OrderBy(x => x.ProfitTarget).LastOrDefault()?.ProfitTarget ?? SellProfitStart;
                int missing = SellTiersCount - SellTriggers.Count;
                for (int i = 0; i < missing; i++)
                {
                    lastTierProfit += SellTiersProfitSpacing;
                    PairTriggerModel model = new()
                    {
                        Side = ZeroPlus.Models.Data.Enums.Side.Sell,
                        ChangeToEma = Math.Round(changeToEma, (int)Rounding),
                        ProfitTarget = Math.Round(lastTierProfit, (int)Rounding),
                    };
                    changeToEma += SellTiersSpacing;
                    Dispatcher?.Invoke(() => SellTriggers.Add(model));
                }
            }

            IOrderedEnumerable<PairTriggerModel> sellTriggers = SellTriggers.OrderBy(x => x.ChangeToEma);
            double nextTier = SellTiersStart;
            double nextTierProfit = SellProfitStart;
            foreach (PairTriggerModel sellTrigger in sellTriggers)
            {
                sellTrigger.ChangeToEma = Math.Round(nextTier, (int)Rounding);
                sellTrigger.ProfitTarget = Math.Round(nextTierProfit, (int)Rounding);
                nextTier += SellTiersSpacing;
                nextTierProfit += SellTiersProfitSpacing;
            }

            UpdateTarget();
        }

        [Command]
        public void CancelAllOpening()
        {
            foreach (PairOrderModel pairOrder in WorkingOrders())
            {
                if (pairOrder.Tag.Contains("OPEN"))
                {
                    CancelOrder(pairOrder.OrderRequest);
                }
            }
        }

        [Command]
        public void CancelAllOpeningBuys()
        {
            foreach (PairOrderModel pairOrder in WorkingOrders())
            {
                if (pairOrder.Tag.Contains("OPEN") && pairOrder.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    CancelOrder(pairOrder.OrderRequest);
                }
            }
        }

        [Command]
        public void CancelAllOpeningSells()
        {
            foreach (PairOrderModel pairOrder in WorkingOrders())
            {
                if (pairOrder.Tag.Contains("OPEN") && pairOrder.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    CancelOrder(pairOrder.OrderRequest);
                }
            }
        }

        private List<PairOrderModel> WorkingOrders()
        {
            try
            {
                return _allPairOrders.Where(x => x.OrderStatus is not OrderStatus.Filled and not OrderStatus.Canceled and not OrderStatus.Rejected).ToList();
            }
            catch (Exception)
            {
                return new List<PairOrderModel>();
            }
        }

        private PairOrderModel SendBuy(double? triggerValue = null, int qty = 0, PositionEffect type = PositionEffect.AUTO, PairTriggerModel trigger = null, string tag = "", ExecutionStyle buyExecutionStyle = ExecutionStyle.Passive, double stopLoss = double.NaN, PairOrderModel openingOrder = null)
        {
            try
            {
                if (DateTime.Now.TimeOfDay >= CloseTime.TimeOfDay)
                {
                    ShowMessage("Outside Trading Hours!");
                    return null;
                }
                Status = "";
                StatusMode = StatusMode.Reset;

                string orderId = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId1 = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId2 = OPENING_ID + OmsCore.OrderClient.GetNextOrderId();

                Side leg1Side = PairLeg1.Side;
                Side leg2Side = PairLeg2.Side;

                int spreadQty = qty > 0 ? qty : SpreadQty;
                int leg1Qty = spreadQty * PairLeg1.Quantity;
                int leg2Qty = spreadQty * PairLeg2.Quantity;

                TriggerMethod triggerMethod;
                if (AutoTriggerMethod)
                {
                    if (TriggerMethod == PairsType.Ratio)
                    {
                        triggerMethod = ZeroPlus.Models.Data.Enums.TriggerMethod.RBS;
                    }
                    else
                    {
                        triggerMethod = ZeroPlus.Models.Data.Enums.TriggerMethod.SBS;
                    }
                }
                else
                {
                    triggerMethod = BuyTriggerMethod;
                }

                switch (triggerMethod)
                {
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.RSB:
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.RBS:
                        triggerValue ??= Math.Abs(SpreadTriggerValue);
                        break;
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.SSB:
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.SBS:
                        triggerValue ??= SpreadTriggerValue;
                        break;
                    default:
                        return null;
                }

                if (!triggerValue.HasValue || double.IsNaN(triggerValue.Value))
                {
                    Status = "Invalid Trigger Value";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }

                triggerValue = Math.Round(triggerValue.Value, 4);

                PairOrderRequest pairOrder = new()
                {
                    Account = Account,
                    TriggerMethod = triggerMethod.ToString(),
                    TriggerValue = triggerValue.Value,
                    Style = buyExecutionStyle.ToString().ToUpper(),
                    ClientOrderId = orderId,
                    TriggerValueCurrency = TriggerValueCurrency.ToString().ToUpper(),
                    InitSide = BuyInitialSide,
                    Locate = Locate,

                    ClientOrderIdLeg1 = orderId1,
                    Leg1Symbol = PairLeg1.Symbol,
                    Leg1Side = leg1Side == ZeroPlus.Models.Data.Enums.Side.Sell && PairLeg1.FilledQty < leg1Qty ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg1Side,
                    Leg1Quantity = leg1Qty,

                    ClientOrderIdLeg2 = orderId2,
                    Leg2Symbol = PairLeg2.Symbol,
                    Leg2Side = leg2Side == ZeroPlus.Models.Data.Enums.Side.Sell && PairLeg2.FilledQty < leg2Qty ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg2Side,
                    Leg2Quantity = leg2Qty,

                    TimeInForce = TimeInForce,

                    BuyTermsRatio = leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? PairLeg1.Quantity : PairLeg2.Quantity,
                    SellTermsRatio = leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? PairLeg1.Quantity : PairLeg2.Quantity,
                };

                PairLeg1.WorkingQty += leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? leg1Qty : -leg1Qty;
                PairLeg2.WorkingQty += leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? leg2Qty : -leg2Qty;

                return SendOrder(pairOrder, spreadQty, ZeroPlus.Models.Data.Enums.Side.Buy, type, trigger, tag, stopLoss, openingOrder);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendBuy));
                return null;
            }
        }

        private PairOrderModel SendSell(double? triggerValue = null, int qty = 0, PositionEffect type = PositionEffect.AUTO, PairTriggerModel trigger = null, string tag = "", ExecutionStyle sellExecutionStyle = ExecutionStyle.Passive, double stopLoss = double.NaN, PairOrderModel openingOrder = null)
        {
            try
            {
                if (DateTime.Now.TimeOfDay >= CloseTime.TimeOfDay)
                {
                    ShowMessage("Outside Trading Hours!");
                    return null;
                }
                ContraStatus = "";
                ContraStatusMode = StatusMode.Reset;

                string contraOrderId = CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();
                string contraOrderId1 = CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();
                string contraOrderId2 = CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();

                int spreadQty = qty > 0 ? qty : SpreadQty;

                int leg1Qty = spreadQty * PairLeg1.Quantity;
                int leg2Qty = spreadQty * PairLeg2.Quantity;

                Side leg1Side = PairLeg1.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                Side leg2Side = PairLeg2.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;

                TriggerMethod triggerMethod;
                if (AutoTriggerMethod)
                {
                    if (TriggerMethod == PairsType.Ratio)
                    {
                        triggerMethod = ZeroPlus.Models.Data.Enums.TriggerMethod.RSB;
                    }
                    else
                    {
                        triggerMethod = ZeroPlus.Models.Data.Enums.TriggerMethod.SSB;
                    }
                }
                else
                {
                    triggerMethod = SellTriggerMethod;
                }

                switch (triggerMethod)
                {
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.RSB:
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.RBS:
                        triggerValue ??= Math.Abs(SpreadSellTriggerValue);
                        break;
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.SSB:
                    case ZeroPlus.Models.Data.Enums.TriggerMethod.SBS:
                        triggerValue ??= SpreadSellTriggerValue;
                        break;
                    default:
                        return null;
                }

                if (!triggerValue.HasValue || double.IsNaN(triggerValue.Value))
                {
                    ContraStatus = "Invalid Trigger Value";
                    ContraStatusMode = StatusMode.CancelledSell;
                    return null;
                }

                triggerValue = Math.Round(triggerValue.Value, 4);

                PairOrderRequest pairOrder = new()
                {
                    Account = Account,
                    TriggerMethod = triggerMethod.ToString(),
                    TriggerValue = triggerValue.Value,
                    Style = sellExecutionStyle.ToString().ToUpper(),
                    ClientOrderId = contraOrderId,
                    TriggerValueCurrency = TriggerValueCurrency.ToString().ToUpper(),
                    InitSide = SellInitialSide,
                    Locate = Locate,

                    ClientOrderIdLeg1 = contraOrderId1,
                    Leg1Symbol = PairLeg1.Symbol,
                    Leg1Side = leg1Side == ZeroPlus.Models.Data.Enums.Side.Sell && PairLeg1.FilledQty < leg1Qty ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg1Side,
                    Leg1Quantity = leg1Qty,

                    ClientOrderIdLeg2 = contraOrderId2,
                    Leg2Symbol = PairLeg2.Symbol,
                    Leg2Side = leg2Side == ZeroPlus.Models.Data.Enums.Side.Sell && PairLeg2.FilledQty < leg2Qty ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg2Side,
                    Leg2Quantity = leg2Qty,

                    TimeInForce = TimeInForce,

                    BuyTermsRatio = leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? PairLeg1.Quantity : PairLeg2.Quantity,
                    SellTermsRatio = leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? PairLeg1.Quantity : PairLeg2.Quantity,
                };

                PairLeg1.WorkingQty += leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? leg1Qty : -leg1Qty;
                PairLeg2.WorkingQty += leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? leg2Qty : -leg2Qty;

                return SendOrder(pairOrder, spreadQty, ZeroPlus.Models.Data.Enums.Side.Sell, type, trigger, tag, stopLoss, openingOrder);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendSell));
                return null;
            }
        }

        private void UpdateTarget()
        {
            _orderedBuyTriggers = BuyTriggers.OrderBy(x => x.ChangeToEma).ToList();
            _orderedSellTriggers = SellTriggers.OrderBy(x => x.ChangeToEma).ToList();
            UpdateValues();
        }

        private void UpdateValues()
        {
            if (_orderedBuyTriggers != null)
            {
                int index = 1;
                foreach (PairTriggerModel item in _orderedBuyTriggers)
                {
                    UpdateBuyTriggerValues(index++, item);
                }
            }

            if (_orderedSellTriggers != null)
            {
                int index = 1;
                foreach (PairTriggerModel item in _orderedSellTriggers)
                {
                    UpdateSellTriggerValue(index++, item);
                }
            }
        }

        private void UpdateBuyTriggerValues(int index, PairTriggerModel item)
        {
            try
            {
                if (!item.Disposed)
                {
                    item.Level = index;
                    double basePrice = GetBasePriceForTarget(item);
                    item.Target = basePrice - item.ChangeToEma;
                    item.CloseTarget = basePrice + item.ProfitTarget;
                    item.TargetDiff = item.Target - Ask + TriggerProximity;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateBuyTriggerValues));
            }
        }

        private void UpdateSellTriggerValue(int index, PairTriggerModel item)
        {
            try
            {
                if (!item.Disposed)
                {
                    item.Level = index;
                    double basePrice = GetBasePriceForTarget(item);
                    item.Target = basePrice + item.ChangeToEma;
                    item.CloseTarget = basePrice - item.ProfitTarget;
                    item.TargetDiff = Bid - item.Target + TriggerProximity;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateSellTriggerValue));
            }
        }

        private double GetBasePriceForTarget(PairTriggerModel item)
        {
            if (PairTriggerType == PairTriggerType.EMA)
            {
                if (LockedEma)
                {
                    return LockedEmaValue;
                }
                else
                {
                    return Ema;
                }
            }
            else
            {
                return item.Base;
            }
        }

        private PairOrderModel SendOrder(PairOrderRequest pairOrder, int qty, Side side, PositionEffect type, PairTriggerModel trigger = null, string tag = "", double stopLoss = double.NaN, PairOrderModel openingOrder = null)
        {
            pairOrder.PairOrderRequestType = PairOrderRequestType.Send;
            PairOrderModel orderModel = new()
            {
                StopLoss = stopLoss,
                Symbol = side == ZeroPlus.Models.Data.Enums.Side.Buy ? Symbol : ReverseSymbol,
                TriggerValue = pairOrder.TriggerValue,
                TriggerMode = pairOrder.TriggerMethod,
                Quantity = qty,
                Trigger = trigger,
                Tag = tag,
                OpeningOrder = openingOrder,
            };
            _orderIdToOrderModelMap[pairOrder.ClientOrderId] = orderModel;

            if (DateTime.Now.TimeOfDay >= CloseTime.TimeOfDay)
            {
                orderModel.OrderStatus = OrderStatus.Rejected;
                orderModel.Reason = "Outside Trading Hours!";
            }
            else
            {
                OmsCore.AutoTraderClient.SendPairOrder(pairOrder, this);
            }

            orderModel.Init(pairOrder, side, type);
            Dispatcher.BeginInvoke(() =>
            {
                _allPairOrders.Add(orderModel);
                LastPairOrder = orderModel;
            });
            return orderModel;
        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            LoadDefaultAccount();
        }

        public void LoadDefaultAccount()
        {
            HashSet<string> accounts = OmsCore.User.Accounts.ToHashSet();

            foreach (string account in PairAccounts)
            {
                if (accounts.Contains(account))
                {
                    if (!AccountsList.Contains(account))
                    {
                        Dispatcher.Invoke(() => AccountsList.Add(account));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(Account))
            {
                Account = AccountsList.FirstOrDefault();
            }
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
            if (update.ClientOrderId.StartsWith(OPENING_ID))
            {
                Status = update.CurrentStatus + " " + update.Volume + "@" + update.Price + " [" + update.Reason + "]";
                bool isBuySide = update.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                StatusMode = update.OrderStatus switch
                {
                    OrderStatus.New => StatusMode.Reset,
                    OrderStatus.PendingNew => StatusMode.Pending,
                    OrderStatus.PartiallyFilled => isBuySide ? StatusMode.NewBuy : StatusMode.NewSell,
                    OrderStatus.Filled => isBuySide ? StatusMode.FilledBuy : StatusMode.FilledSell,
                    OrderStatus.Canceled => isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell,
                    OrderStatus.Rejected => isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell,
                    OrderStatus.Replaced => StatusMode.Reset,
                    _ => isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell,
                };
                if (!_orderIdToOrderModelMap.TryGetValue(update.ClientOrderId, out PairOrderModel orderModel))
                {
                    orderModel = new PairOrderModel();
                    _orderIdToOrderModelMap[update.ClientOrderId] = orderModel;
                    Dispatcher.BeginInvoke(() =>
                    {
                        _allPairOrders.Add(orderModel);
                        LastPairOrder = orderModel;
                    });
                }
            }
            else if (update.ClientOrderId.StartsWith(CLOSING_ID))
            {
                ContraStatus = update.CurrentStatus + " " + update.Volume + "@" + update.Price + " [" + update.Reason + "]";
                bool isBuySide = update.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                ContraStatusMode = update.OrderStatus switch
                {
                    OrderStatus.New => StatusMode.Reset,
                    OrderStatus.PendingNew => StatusMode.Pending,
                    OrderStatus.PartiallyFilled => isBuySide ? StatusMode.NewBuy : StatusMode.NewSell,
                    OrderStatus.Filled => isBuySide ? StatusMode.FilledBuy : StatusMode.FilledSell,
                    OrderStatus.Canceled => isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell,
                    OrderStatus.Rejected => isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell,
                    OrderStatus.Replaced => StatusMode.Reset,
                    _ => isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell,
                };
                if (!_orderIdToOrderModelMap.TryGetValue(update.ClientOrderId, out PairOrderModel orderModel))
                {
                    orderModel = new PairOrderModel();
                    _orderIdToOrderModelMap[update.ClientOrderId] = orderModel;
                    Dispatcher.BeginInvoke(() =>
                    {
                        _allPairOrders.Add(orderModel);
                        LastPairOrder = orderModel;
                    });
                }
            }
        }

        internal string GetConfigAsJson()
        {
            PairTraderConfig config = GetConfig();
            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        internal void LoadConfigFromJson(string config)
        {
            try
            {
                PairTraderConfig pairTraderConfig = JsonConvert.DeserializeObject<PairTraderConfig>(config);
                LoadFromConfig(pairTraderConfig);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJson));
            }
        }

        internal PairTraderConfig GetConfig()
        {
            return new PairTraderConfig()
            {
                Leg1Symbol = PairLeg1.Symbol,
                Leg1Side = PairLeg1.Side,
                Leg1Qty = PairLeg1.Quantity,
                Leg2Symbol = PairLeg2.Symbol,
                Leg2Side = PairLeg2.Side,
                Leg2Qty = PairLeg2.Quantity,
                SpreadQty = SpreadQty,

                DataType = DataType,
                Rounding = Rounding,
                EmaSmoothing = EmaSmoothing,
                EmaPeriods = EmaPeriods,
                EmaInterval = EmaInterval,

                AutoTriggerMethod = AutoTriggerMethod,
                BuyTriggerMethod = BuyTriggerMethod,
                BuyInitialSide = BuyInitialSide,
                SpreadTriggerValue = SpreadTriggerValue,
                BuyExecutionStyle = BuyExecutionStyle,
                BuyManualQty = BuyManualQty,
                TriggerMethod = TriggerMethod,
                SellTriggerMethod = SellTriggerMethod,
                SellInitialSide = SellInitialSide,
                SpreadSellTriggerValue = SpreadSellTriggerValue,
                SellExecutionStyle = SellExecutionStyle,
                SellManualQty = SellManualQty,

                PairTriggerType = PairTriggerType,
                PairProfitType = PairProfitType,
                PairCancelMode = CancelMode,
                TriggerTimer = TriggerTimer,
                BuyAutoExecutionStyle = BuyAutoExecutionStyle,
                TriggerProximity = TriggerProximity,
                AutoCancel = AutoCancel,
                CancelTrigger = CancelTrigger,
                StopLoss = StopLoss,
                SellAutoExecutionStyle = SellAutoExecutionStyle,
                CloseOrders = CloseOrders,
                RestOrders = RestOrders,

                CloseByAvgCloseTime = CloseByAvgCloseTime,
                BlockReentryAfterAvgTimeClose = BlockReentryAfterAvgTimeClose,
                BlockReentryAfterStoploss = BlockReentryAfterStoploss,
                AvgCloseTimeLookbackSeconds = AvgCloseTimeLookbackSeconds,
                AvgCloseTimeMultiplier = AvgCloseTimeMultiplier,
                MinCloseTimesec = MinCloseTimeSec,

                LockedEma = LockedEma,
                LockedEmaValue = LockedEmaValue,

                BuyTiersCount = BuyTiersCount,
                BuyTiersSpacing = BuyTiersSpacing,
                BuyTiersProfitSpacing = BuyTiersProfitSpacing,
                SellTiersCount = SellTiersCount,
                SellTiersSpacing = SellTiersSpacing,
                SellTiersProfitSpacing = SellTiersProfitSpacing,

                BuyProfitStart = BuyProfitStart,
                SellProfitStart = SellProfitStart,
                BuyTiersStart = BuyTiersStart,
                SellTiersStart = SellTiersStart,
            };
        }

        private void LoadFromConfig(PairTraderConfig config)
        {
            if (config == null)
            {
                return;
            }

            PairLeg1.Symbol = config.Leg1Symbol;
            PairLeg1.Side = config.Leg1Side;
            PairLeg1.Quantity = config.Leg1Qty;
            PairLeg2.Symbol = config.Leg2Symbol;
            PairLeg2.Side = config.Leg2Side;
            PairLeg2.Quantity = config.Leg2Qty;

            DataType = config.DataType;
            Rounding = config.Rounding;
            EmaSmoothing = config.EmaSmoothing;
            EmaPeriods = config.EmaPeriods;
            EmaInterval = config.EmaInterval;
            LockedEma = config.LockedEma;
            LockedEmaValue = config.LockedEmaValue;

            AutoTriggerMethod = config.AutoTriggerMethod;
            BuyTriggerMethod = config.BuyTriggerMethod;
            BuyInitialSide = config.BuyInitialSide;
            SpreadTriggerValue = config.SpreadTriggerValue;
            BuyExecutionStyle = config.BuyExecutionStyle;
            BuyManualQty = config.BuyManualQty;
            TriggerMethod = config.TriggerMethod;
            SellTriggerMethod = config.SellTriggerMethod;
            SellInitialSide = config.SellInitialSide;
            SpreadSellTriggerValue = config.SpreadSellTriggerValue;
            SellExecutionStyle = config.SellExecutionStyle;
            SellManualQty = config.SellManualQty;

            PairTriggerType = config.PairTriggerType;
            TriggerTimer = config.TriggerTimer;
            BuyAutoExecutionStyle = config.BuyAutoExecutionStyle;
            TriggerProximity = config.TriggerProximity;
            AutoCancel = config.AutoCancel;
            CancelTrigger = config.CancelTrigger;
            StopLoss = config.StopLoss;
            SellAutoExecutionStyle = config.SellAutoExecutionStyle;
            CloseOrders = config.CloseOrders;
            RestOrders = config.RestOrders;
            PairProfitType = config.PairProfitType;
            CancelMode = config.PairCancelMode;
            BlockReentryAfterAvgTimeClose = config.BlockReentryAfterAvgTimeClose;
            BlockReentryAfterStoploss = config.BlockReentryAfterStoploss;
            CloseByAvgCloseTime = config.CloseByAvgCloseTime;
            AvgCloseTimeLookbackSeconds = config.AvgCloseTimeLookbackSeconds;
            AvgCloseTimeMultiplier = config.AvgCloseTimeMultiplier;
            MinCloseTimeSec = config.MinCloseTimesec;

            BuyTiersCount = config.BuyTiersCount;
            BuyTiersSpacing = config.BuyTiersSpacing;
            BuyTiersProfitSpacing = config.BuyTiersProfitSpacing;
            BuyProfitStart = config.BuyProfitStart;
            SellProfitStart = config.SellProfitStart;
            BuyTiersStart = config.BuyTiersStart;
            SellTiersStart = config.SellTiersStart;
            SellTiersCount = config.SellTiersCount;
            SellTiersSpacing = config.SellTiersSpacing;
            SellTiersProfitSpacing = config.SellTiersProfitSpacing;

            OrderEnabled = false;

            UpdateTarget();

            Load();
            SpreadQty = config.SpreadQty;
        }

        private void OnBidEmaUpdatedEvent(double ema)
        {
            BidEma = ema;
            UpdateSignals();
        }

        private void OnEmaUpdatedEvent(double ema)
        {
            Ema = ema;
            UpdateSignals();
        }

        private void OnAskEmaUpdatedEvent(double ema)
        {
            AskEma = ema;
            UpdateSignals();
        }

        private void CheckForOrder()
        {
            try
            {
                double avgCloseTime = GetAvgCloseTime();
                lock (_orderLock)
                {
                    CheckForOpeningBuyOrders();
                    CheckForOpeningSellOrders();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForOrder));
            }
        }

        private void CheckForOpeningBuyOrders()
        {
            List<PairOrderModel> openingBuyOrders = _allPairOrders.Where(x => IsResting(x, ZeroPlus.Models.Data.Enums.Side.Buy)).OrderByDescending(x => x.TriggerValue).ToList();
            List<PairOrderModel> cxlList = new();
            List<PairOrderModel> copy = openingBuyOrders.ToList();
            bool cancelOverride = !OrderEnabled || DateTime.Now.TimeOfDay >= StopTime.TimeOfDay;
            for (int i = 0; i < copy.Count; i++)
            {
                PairOrderModel order = copy[i];
                PairTriggerModel trigger = order.Trigger;

                if (order.TriggerValue > Bid || i == 0)
                {
                    double change = trigger.Target - order.TriggerValue;
                    if (cancelOverride || change > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(change) > CancelTrigger))
                    {
                        int nextIndex = i + 1;
                        if (nextIndex < copy.Count)
                        {
                            PairOrderModel nextOrder = copy[nextIndex];
                            double nextChange = trigger.Target - nextOrder.TriggerValue;
                            if (nextChange > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(nextChange) > CancelTrigger))
                            {
                                continue;
                            }
                        }

                        order.Reason = cancelOverride ? "CXL by override!" : $"CXL by trigger! {change:f2}";
                        openingBuyOrders.Remove(order);
                        cxlList.Add(order);
                        continue;
                    }
                }

                double totalSeconds = (DateTime.Now - order.LastUpdateTime).TotalSeconds;
                if (RestOrders && AutoCancel >= 60 && order.OrderStatus == OrderStatus.New && totalSeconds > AutoCancel)
                {
                    order.Reason = $"CXL by timer! {totalSeconds:f0}";
                    openingBuyOrders.Remove(order);
                    cxlList.Add(order);
                    continue;
                }
            }

            if (openingBuyOrders.Any())
            {
                HighestBuyPrice = openingBuyOrders.Max(x => x.TriggerValue);
            }
            else
            {
                HighestBuyPrice = double.NaN;
            }

            VerifyTriggers(_orderedBuyTriggers);

            List<PairTriggerModel> openingBuyTriggers = _orderedBuyTriggers.Where(x => !x.IsClosing).OrderBy(x => x.ChangeToEma).ToList();
            int nextTriggerIndex = 0;
            for (; nextTriggerIndex < openingBuyOrders.Count && nextTriggerIndex < openingBuyTriggers.Count; nextTriggerIndex++)
            {
                PairOrderModel order = openingBuyOrders[nextTriggerIndex];
                PairTriggerModel trigger = openingBuyTriggers[nextTriggerIndex];
                AttachOpeningTrigger(order, trigger);
            }

            foreach (PairOrderModel order in cxlList)
            {
                if (nextTriggerIndex < openingBuyTriggers.Count)
                {
                    PairTriggerModel trigger = openingBuyTriggers[nextTriggerIndex++];
                    AttachOpeningTrigger(order, trigger);
                }
            }

            if (OrderEnabled && DateTime.Now.TimeOfDay < StopTime.TimeOfDay && !BuyEntryBlockedByAvgTimeClose && !BuyEntryBlockedByStoploss)
            {
                foreach (PairTriggerModel trigger in openingBuyTriggers)
                {
                    if (!trigger.Disposed && !trigger.IsClosing && !trigger.Sent)
                    {
                        trigger.Sent = true;
                        trigger.State = "Opening";
                        trigger.SendTime = DateTime.Now;
                        string tag = $"#{trigger.Level} OPEN";
                        trigger.Order = SendBuy(trigger.Target, trigger.Qty, PositionEffect.Open, trigger, tag, BuyAutoExecutionStyle);
                    }
                }
            }

            foreach (PairOrderModel order in cxlList)
            {
                CancelOrder(order.OrderRequest);
            }

            List<PairOrderModel> closingSellOrders = _allPairOrders.Where(x =>
                                    x.Side == ZeroPlus.Models.Data.Enums.Side.Sell &&
                                    x.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel &&
                                    (x.OrderStatus == OrderStatus.New || x.OrderStatus == OrderStatus.PendingNew) &&
                                    x.Type == PositionEffect.Close).OrderByDescending(x => x.TriggerValue).ToList();

            foreach (PairOrderModel order in closingSellOrders)
            {
                PairTriggerModel trigger = order.Trigger;
                if (trigger != null)
                {
                    double change = order.TriggerValue - trigger.CloseTarget;
                    if (PairProfitType == PairTriggerType.EMA && (change > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(change) > CancelTrigger)))
                    {
                        if (order.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel)
                        {
                            order.Reason = $"CXL by trigger! {change:f2}";
                            CancelOrder(order.OrderRequest);
                        }
                    }
                    else
                    {
                        CheckForStopLoss(order);
                    }
                }
            }
        }

        private void CheckForOpeningSellOrders()
        {
            List<PairOrderModel> openingSellOrders = _allPairOrders.Where(x => IsResting(x, ZeroPlus.Models.Data.Enums.Side.Sell)).OrderBy(x => x.TriggerValue).ToList();
            List<PairOrderModel> cxlList = new();
            List<PairOrderModel> copy = openingSellOrders.ToList();
            bool cancelOverride = !OrderEnabled || DateTime.Now.TimeOfDay >= StopTime.TimeOfDay;
            for (int i = 0; i < copy.Count; i++)
            {
                PairOrderModel order = copy[i];
                PairTriggerModel trigger = order.Trigger;

                if (order.TriggerValue < Ask || i == 0)
                {
                    double change = order.TriggerValue - trigger.Target;
                    if (cancelOverride || change > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(change) > CancelTrigger))
                    {
                        int nextIndex = i + 1;
                        if (nextIndex < copy.Count)
                        {
                            PairOrderModel nextOrder = copy[nextIndex];
                            double nextChange = nextOrder.TriggerValue - trigger.Target;
                            if (nextChange > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(nextChange) > CancelTrigger))
                            {
                                continue;
                            }
                        }

                        order.Reason = cancelOverride ? "CXL by override!" : $"CXL by trigger! {change:f2}";
                        openingSellOrders.Remove(order);
                        cxlList.Add(order);
                        continue;
                    }
                }

                double totalSeconds = (DateTime.Now - order.LastUpdateTime).TotalSeconds;
                if (RestOrders && AutoCancel >= 60 && order.OrderStatus == OrderStatus.New && totalSeconds > AutoCancel)
                {
                    order.Reason = $"CXL by timer! {totalSeconds:f0}";
                    openingSellOrders.Remove(order);
                    cxlList.Add(order);
                    continue;
                }
            }

            if (openingSellOrders.Any())
            {
                LowestSellPrice = openingSellOrders.Min(x => x.TriggerValue);
            }
            else
            {
                LowestSellPrice = double.NaN;
            }

            VerifyTriggers(_orderedSellTriggers);

            List<PairTriggerModel> openingSellTriggers = _orderedSellTriggers.Where(x => !x.IsClosing).OrderBy(x => x.ChangeToEma).ToList();
            int nextTriggerIndex = 0;
            for (; nextTriggerIndex < openingSellOrders.Count && nextTriggerIndex < openingSellTriggers.Count; nextTriggerIndex++)
            {
                PairOrderModel order = openingSellOrders[nextTriggerIndex];
                PairTriggerModel trigger = openingSellTriggers[nextTriggerIndex];
                AttachOpeningTrigger(order, trigger);
            }

            foreach (PairOrderModel order in cxlList)
            {
                if (nextTriggerIndex < openingSellTriggers.Count)
                {
                    PairTriggerModel trigger = openingSellTriggers[nextTriggerIndex++];
                    AttachOpeningTrigger(order, trigger);
                }
            }

            if (OrderEnabled && DateTime.Now.TimeOfDay < StopTime.TimeOfDay && !SellEntryBlockedByAvgTimeClose && !SellEntryBlockedByStoploss)
            {
                foreach (PairTriggerModel trigger in openingSellTriggers)
                {
                    if (!trigger.Disposed && !trigger.IsClosing && !trigger.Sent)
                    {
                        trigger.Sent = true;
                        trigger.State = "Opening";
                        trigger.SendTime = DateTime.Now;
                        string tag = $"#{trigger.Level} OPEN";
                        trigger.Order = SendSell(trigger.Target, trigger.Qty, PositionEffect.Open, trigger, tag, SellAutoExecutionStyle);
                    }
                }
            }

            foreach (PairOrderModel order in cxlList)
            {
                CancelOrder(order.OrderRequest);
            }

            List<PairOrderModel> closingBuyOrders = _allPairOrders.Where(x =>
                                        x.Side == ZeroPlus.Models.Data.Enums.Side.Buy &&
                                        x.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel &&
                                        (x.OrderStatus == OrderStatus.New || x.OrderStatus == OrderStatus.PendingNew) &&
                                        x.Type == PositionEffect.Close).OrderByDescending(x => x.TriggerValue).ToList();

            foreach (PairOrderModel order in closingBuyOrders)
            {
                PairTriggerModel trigger = order.Trigger;
                if (trigger != null)
                {
                    double change = trigger.CloseTarget - order.TriggerValue;
                    if (PairProfitType == PairTriggerType.EMA && (change > CancelTrigger || (CancelMode == CancelMode.All && Math.Abs(change) > CancelTrigger)))
                    {
                        if (order.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel)
                        {
                            order.Reason = $"CXL by trigger! {change:f2}";
                            CancelOrder(order.OrderRequest);
                        }
                    }
                    else
                    {
                        CheckForStopLoss(order);
                    }
                }
            }
        }

        private static bool IsResting(PairOrderModel order, Side side)
        {
            return order.Side == side &&
                   order.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel &&
                   order.Type == PositionEffect.Open &&
                   (order.OrderStatus == OrderStatus.New || order.OrderStatus == OrderStatus.PendingNew);
        }

        private void VerifyTriggers(List<PairTriggerModel> triggers)
        {
            try
            {
                foreach (var trigger in triggers)
                {
                    if (trigger.Sent &&
                        (trigger.Order == null || trigger.Order.OrderStatus.IsClosed()) &&
                        (trigger.ClosingOrder == null || trigger.ClosingOrder.OrderStatus.IsClosed()))
                    {
                        trigger.Reset();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VerifyTriggers));
            }
        }

        private static void AttachOpeningTrigger(PairOrderModel order, PairTriggerModel trigger)
        {
            try
            {
                if (trigger != null && !trigger.IsClosing && trigger != order.Trigger)
                {
                    trigger.Sent = true;
                    trigger.State = "Opening";
                    trigger.SendTime = DateTime.Now;
                    string tag = $"#{trigger.Level} OPEN";
                    order.Trigger = trigger;
                    order.Tag = tag;
                    trigger.Order = order;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AttachOpeningTrigger));
            }
        }

        private static PairTriggerModel GetNextOpeningTrigger(List<PairTriggerModel> triggers, ref int level)
        {
            for (; level < triggers.Count; level++)
            {
                PairTriggerModel trigger = triggers[level];
                if (!trigger.IsClosing)
                {
                    return trigger;
                }
            }
            return null;
        }

        private void CancelFromTrigger(PairTriggerModel trigger)
        {
            if (trigger.Sent)
            {
                PairOrderModel model = _allPairOrders.FirstOrDefault(x => x.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel && !x.OrderStatus.IsClosed() && (x == trigger.Order || x.Trigger == trigger));
                if (model != null)
                {
                    CancelOrder(model.OrderRequest);
                }
            }
        }

        private void CheckForStopLoss(PairOrderModel order)
        {
            PairTriggerModel trigger = order.Trigger;
            PairOrderModel openingOrderModel = order.OpeningOrder;
            if (trigger != null &&
                openingOrderModel != null &&
                order.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel)
            {
                switch (order.Side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        double stopLoss = openingOrderModel.AvgFillPx + StopLoss;
                        order.StopLoss = stopLoss;

                        if (Bid > stopLoss)
                        {
                            trigger.State = "Stop-Loss Cancel";
                            order.Reason = "CXL by stop-loss!";
                            CancelOrder(order.OrderRequest);
                        }
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                        stopLoss = openingOrderModel.AvgFillPx - StopLoss;
                        order.StopLoss = stopLoss;
                        if (Ask < stopLoss)
                        {
                            trigger.State = "Stop-Loss Cancel";
                            order.Reason = "CXL by stop-loss!";
                            CancelOrder(order.OrderRequest);
                        }
                        break;
                }
            }
        }

        private void UpdatePositionAndPnl()
        {
            try
            {
                RealPnl = Symbols.Sum(x => x.RealPnl);
                int totalQty = Symbols.Sum(x => x.TotalQty);
                double totalSecFees = Symbols.Sum(x => x.SellQty * x.AvgSell * SecFees);
                AdjPnl = RealPnl - (Fees * totalQty) - totalSecFees;
                UnrealPnl = Symbols.Sum(x => x.UnrealPnl);
                if (PairLeg1.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    AvgBuyPrice = (PairLeg1.Quantity * PairLeg1.AvgBuy) - (PairLeg2.Quantity * PairLeg2.AvgSell);
                    AvgSellPrice = (PairLeg2.Quantity * PairLeg2.AvgBuy) - (PairLeg1.Quantity * PairLeg1.AvgSell);
                }
                else
                {
                    AvgBuyPrice = (PairLeg2.Quantity * PairLeg2.AvgBuy) - (PairLeg1.Quantity * PairLeg1.AvgSell);
                    AvgSellPrice = (PairLeg1.Quantity * PairLeg1.AvgBuy) - (PairLeg2.Quantity * PairLeg2.AvgSell);
                }
                NetPnl = AdjPnl + UnrealPnl;
                TotalBuyQty = _fillsOnlyPairOrders.Where(x => x.Side == ZeroPlus.Models.Data.Enums.Side.Buy).Sum(x => x.Filled);
                TotalSellQty = _fillsOnlyPairOrders.Where(x => x.Side == ZeroPlus.Models.Data.Enums.Side.Sell).Sum(x => x.Filled);
                TotalQty = TotalBuyQty + TotalSellQty;
                if (Symbols.Count == 1)
                {
                    LcdPosition = Symbols.First().FilledQty;
                }
                else if (Symbols.Count > 1 && Symbols.Count(x => Math.Abs(x.FilledQty) >= x.Quantity) == Symbols.Count)
                {
                    if ((Symbols.Count(x => (x.FilledQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.FilledQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == Symbols.Count) ||
                        (Symbols.Count(x => (x.FilledQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.FilledQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == Symbols.Count))
                    {
                        int divisor = Symbols.Min(x => (int)Math.Ceiling(Math.Abs((double)x.FilledQty / x.Quantity)));
                        PairLegModel sample = Symbols.First();
                        if ((sample.FilledQty < 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (sample.FilledQty > 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Sell))
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
            }
            catch (Exception ex)
            {
                LcdPosition = 0;
                _log.Error(ex, nameof(UpdatePositionAndPnl));
            }
        }

        private void ClosePositions()
        {

        }

        internal void ShowMessage(string message)
        {
            Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage(message, Symbol, MessageButton.OK, MessageIcon.Warning));
        }

        [Command]
        public void EmaTriggerStateChangedCommand()
        {
            if (EmaTriggerEnabled && PresetTriggerEnabled)
            {
                PresetTriggerEnabled = false;
            }
            ResetPresetPrices();
        }

        [Command]
        public void ResetPresetPrices()
        {
            SpreadTriggerValue = double.NaN;
            SpreadSellTriggerValue = double.NaN;
        }

        [Command]
        public void PresetTriggerStateChangedCommand()
        {
            if (EmaTriggerEnabled && PresetTriggerEnabled)
            {
                EmaTriggerEnabled = false;
            }
            ResetPresetPrices();
        }

        [Command]
        public void LiquidateCommand()
        {
            MessageResult response = MessageBoxService.ShowMessage("Are you sure you want to close all positions?", Symbol, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
            if (response == MessageResult.Yes)
            {
                ClosePositions();
            }
        }

        [Command]
        public void Load()
        {
            Stop();
            foreach (PairLegModel symbol in Symbols)
            {
                if (string.IsNullOrWhiteSpace(symbol.Symbol))
                {
                    MessageBoxService.ShowMessage("Invalid symbol found.");
                    return;
                }
                else if (symbol.Quantity == 0)
                {
                    MessageBoxService.ShowMessage("Invalid qty. Symbol: " + symbol.Symbol);
                    return;
                }
            }

            List<int> qtyList = Symbols.Select(x => Math.Abs(x.Quantity)).ToList();
            int divisor = 1;
            if (qtyList.Count > 0)
            {
                List<int> lcdAdjustedList = Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out divisor);
                for (int index = 0; index < qtyList.Count; ++index)
                {
                    PairLegModel pairLegModel = Symbols[index];
                    int adjustedQty = lcdAdjustedList[index];
                    pairLegModel.Quantity = adjustedQty;
                }
            }

            SpreadQty = divisor;
            string symbolString = GetSpreadSymbol();
            Symbol = symbolString;
            ReverseSymbol = (!symbolString.StartsWith("-") && !symbolString.StartsWith("+") ? "-" : "") + symbolString.Replace("-", "^").Replace("+", "-").Replace("^", "+");
            ModuleTitle = symbolString;
            IsLoaded = true;
            EmaEnabled = true;
            MacdContext.Start();
            ResetAllEma();
            ResetHighBidLowAskCommand();
            UpdateTarget();
            LoadHistoricAvgCloseTimes();
            _orderTimer.Start();
        }

        private string GetSpreadSymbol()
        {
            string symbolString = "";
            foreach (PairLegModel symbol in Symbols)
            {
                if (symbolString == "" && symbol.Quantity == 1 && symbol.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    symbolString = symbol.Symbol;
                }
                else if (symbolString != "" && symbol.Quantity == 1 && symbol.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    symbolString = symbolString + "*" + symbol.Symbol;
                }
                else if (symbolString != "" && symbol.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    symbolString = symbolString + "+" + symbol.Quantity + "*" + symbol.Symbol;
                }
                else if (symbol.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    if (symbolString == "")
                    {
                        symbolString = symbol.Quantity + "*" + symbol.Symbol;
                    }
                    else
                    {
                        symbolString = symbolString + "+" + symbol.Quantity + "*" + symbol.Symbol;
                    }
                }
                else
                {
                    symbolString = symbolString + "-" + symbol.Quantity + "*" + symbol.Symbol;
                }
                symbol.Init();
            }

            return symbolString;
        }

        [Command]
        public void Stop()
        {
            _reversed = false;
            _orderTimer.Stop();
            OrderEnabled = false;
            ResetPresetPrices();
            EmaEnabled = false;
            MacdContext.Stop();
            IsLoaded = false;

            foreach (PairLegModel symbol in Symbols)
            {
                symbol.Stop();
            }

            Bid = double.NaN;
            Ask = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            Ema = double.NaN;
            Mid = double.NaN;
            ResetAllEma();
        }

        [Command]
        public void ReverseCommand()
        {
            ReverseSides(overrideCheck: true);
        }

        [Command]
        public void SwitchOrderbookToAllCommand()
        {
            PairOrders = _allPairOrders;
            LastPairOrder = PairOrders.LastOrDefault();
        }

        [Command]
        public void SwitchOrderbookToFillsCommand()
        {
            PairOrders = _fillsOnlyPairOrders;
            LastPairOrder = PairOrders.LastOrDefault();
        }

        [Command]
        public void ResetEmaCommand()
        {
            MessageResult response = Dispatcher.Invoke(() => MessageBoxService.ShowMessage("Are you sure you want to reset all EMA?", Name, MessageButton.YesNo, MessageIcon.Question, MessageResult.Yes));
            if (response == MessageResult.Yes)
            {
                ResetAllEma();
            }
        }

        [Command]
        public void Activate()
        {
            WindowService?.Show();
            WindowService?.Activate();
        }

        [Command]
        public void Hide()
        {
            WindowService?.Hide();
        }

        [Command]
        public void Close()
        {
            WindowService?.Close();
        }

        [Command]
        public void AvgTimeCloseConfigCommand()
        {
            AvgTimeCloseConfigView view = new();
            view.DataContext = this;
            view.Show();
        }

        private void ResetAllEma()
        {
            ResetEmaEvent?.Invoke();
            _bidEmaCalculator.Reset();
            _midEmaCalculator.Reset();
            _askEmaCalculator.Reset();
        }

        public bool Dispose()
        {
            MessageResult response = Dispatcher.Invoke(() => MessageBoxService.ShowMessage("Are you sure you want to close this instance?", Name, MessageButton.YesNo, MessageIcon.Question, MessageResult.Yes));
            switch (response)
            {
                case MessageResult.Cancel:
                    return true;
                case MessageResult.Yes:
                    break;
                case MessageResult.No:
                    return true;
            }

            OrderEnabled = false;
            EmaEnabled = false;
            MacdContext.Stop();

            foreach (PairLegModel symbol in Symbols)
            {
                symbol.Dispose();
            }

            Bid = double.NaN;
            Ask = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            Ema = double.NaN;
            Mid = double.NaN;

            ManagerModel?.RemoveTrader(this);

            return false;
        }

        public void Update()
        {
            try
            {
                double midPoint = 0.0;
                double spreadBid = 0.0;
                double spreadAsk = 0.0;

                if (Symbols[0].Side == Symbols[1].Side ||
                    Symbols.Any(x => double.IsNaN(x.Bid) || double.IsNaN(x.Ask)))
                {
                    return;
                }

                for (int i = 0; i < Symbols.Count; i++)
                {
                    PairLegModel symbol = Symbols[i];

                    double ratioAbs = Math.Abs(symbol.Quantity);
                    int side = symbol.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? 1 : -1;

                    double bid = ratioAbs * symbol.Bid;
                    double ask = ratioAbs * symbol.Ask;

                    if (side == 1)
                    {
                        spreadBid += side * bid;
                        spreadAsk += side * ask;
                    }
                    else
                    {
                        spreadBid += side * ask;
                        spreadAsk += side * bid;
                    }

                    midPoint += side * ratioAbs * symbol.Mid;
                }

                Bid = spreadBid;
                Mid = midPoint;
                Ask = spreadAsk;

                if (double.IsNaN(HighestBid) || spreadBid > HighestBid)
                {
                    HighestBid = spreadBid;
                }

                if (double.IsNaN(LowestAsk) || spreadAsk < LowestAsk)
                {
                    LowestAsk = spreadAsk;
                }

                if (!double.IsNaN(spreadBid))
                {
                    _bidEmaCalculator.AddUpdate(spreadBid);
                }

                if (!double.IsNaN(midPoint))
                {
                    _midEmaCalculator.AddUpdate(midPoint);
                    MacdContext.UpdateMacd(midPoint);
                }

                if (!double.IsNaN(spreadAsk))
                {
                    _askEmaCalculator.AddUpdate(spreadAsk);
                }


                UpdateSignals();

                if (OrderEnabled && DateTime.Now.TimeOfDay > StopTime.TimeOfDay)
                {
                    var orderEnabled = OrderEnabled;
                    OrderEnabled = false;
                    if (orderEnabled)
                    {
                        SaveAvgCloseTimes();
                    }
                }

                UpdatePositionAndPnl();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        private void SaveAvgCloseTimes()
        {
            if (CloseTimes.Any())
            {
                var json = JsonConvert.SerializeObject(CloseTimes);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    ConfigSave configSave = new()
                    {
                        OwnerId = OmsCore.User.ID,
                        Username = OmsCore.User.Username,
                        SaveTime = DateTime.Today,
                        Module = (int)Module.PairTraderAvgCloseTimeConfig,
                        ConfigJson = json,
                        Title = Symbol,
                        Group = OmsCore.User.Username,
                    };
                    OmsCore.GatewayClient.SaveConfig(configSave);
                }
            }
        }

        private async void LoadHistoricAvgCloseTimes()
        {
            List<ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.PairTraderAvgCloseTimeConfig);
            if (configs != null)
            {
                IEnumerable<ConfigSave> symbolConfigs = configs.Where(x => x.Title == Symbol);
                double alpha = 2.0 / (HistoricAvgCloseTimeEmaPeriod + 1);
                double ema = double.NaN;
                foreach (var config in symbolConfigs.OrderBy(x => x.SaveTime))
                {
                    ConfigSave content = await OmsCore.GatewayClient.RequestConfigDataAsync(config.Id);
                    if (content != null)
                    {
                        var times = JsonConvert.DeserializeObject<List<Tuple<DateTime, double>>>(content.ConfigJson);
                        if (times != null)
                        {
                            var avg = times.Average(x => x.Item2);
                            if (double.IsNaN(ema))
                            {
                                ema = avg;
                            }
                            else
                            {
                                ema = alpha * avg + (1 - alpha) * ema;
                            }
                        }
                    }
                }

                AvgCloseTime = ema;
            }
        }

        private void ReverseSides(bool overrideCheck = false)
        {
            if (!_reversed || overrideCheck)
            {
                Stop();
                _reversed = true;
                (Symbols[0].Symbol, Symbols[1].Symbol) = (Symbols[1].Symbol, Symbols[0].Symbol);
                (Symbols[0].Quantity, Symbols[1].Quantity) = (Symbols[1].Quantity, Symbols[0].Quantity);
                Load();
            }
        }

        private void UpdateSignals()
        {
            UpdateValues();
        }

        public void OrderUpdated(OrderUpdateValues update)
        {
            try
            {
                if (_orderIdToOrderModelMap.TryGetValue(update.ParentLocalOrderId, out PairOrderModel orderModel))
                {
                    orderModel.Update(update);
                    bool isFilled = update.OrderStatus.IsFilled();
                    bool isClosed = update.OrderStatus.IsClosed();
                    bool allLegsFilled = orderModel.Legs.All(x => x.Filled == x.Quantity);
                    bool packageUpdate = orderModel.Legs.All(x => x.OrderStatus == orderModel.OrderStatus);

                    if (isFilled)
                    {
                        StampValues(orderModel);
                    }

                    bool isOpeningSide = update.ParentLocalOrderId.StartsWith(OPENING_ID);
                    bool isClosingSide = update.ParentLocalOrderId.StartsWith(CLOSING_ID);

                    if (isOpeningSide || isClosingSide)
                    {
                        if (update.LocalOrderId == orderModel.Legs[0].ClientOrderId)
                        {
                            Symbols[0].Update(update, isClosingSide);
                        }
                        else if (update.LocalOrderId == orderModel.Legs[1].ClientOrderId)
                        {
                            Symbols[1].Update(update, isClosingSide);
                        }
                    }

                    lock (_orderLock)
                    {
                        PairTriggerModel trigger = orderModel.Trigger;
                        if (trigger != null)
                        {
                            if (orderModel.Type == PositionEffect.Open)
                            {
                                if (isFilled && allLegsFilled)
                                {
                                    trigger.OpenCloseCounter.AddToOpen(orderModel.Filled);
                                    if (CloseOrders)
                                    {
                                        string tag = $"#{trigger.Level} CLOSE";
                                        if (isOpeningSide)
                                        {
                                            double newTarget = orderModel.AvgFillPx + trigger.ProfitTarget;
                                            double stopLoss = orderModel.AvgFillPx - StopLoss;
                                            trigger.OpeningPx = orderModel.AvgFillPx;
                                            trigger.OpeningTime = orderModel.LastUpdateTime;
                                            trigger.State = "Closing";
                                            trigger.ClosingOrder = SendSell(newTarget, orderModel.Filled, PositionEffect.Close, trigger, tag, SellAutoExecutionStyle, stopLoss, orderModel);
                                            List<PairTriggerModel> oppTriggers = SellTriggers.Where(x => x.Target <= stopLoss).ToList();
                                            if (oppTriggers.Any())
                                            {
                                                string message;
                                                if (oppTriggers.Count == 1)
                                                {
                                                    PairTriggerModel oppTrigger = oppTriggers.FirstOrDefault();
                                                    message = $"#{trigger.Level} BUY entry could be stopped out by SELL Trigger #{oppTrigger.Level}.";
                                                }
                                                else
                                                {
                                                    message = $"#{trigger.Level} BUY entry could be stopped out by SELL Triggers {string.Join(", ", oppTriggers.Select(x => "#" + x.Level))}.";
                                                }
                                                Dispatcher?.BeginInvoke(() => MessageBoxService?.ShowMessage(message, Symbol, MessageButton.OK, MessageIcon.Warning));
                                            }
                                        }
                                        else if (isClosingSide)
                                        {
                                            double newTarget = orderModel.AvgFillPx - trigger.ProfitTarget;
                                            double stopLoss = orderModel.AvgFillPx + StopLoss;
                                            trigger.OpeningPx = orderModel.AvgFillPx;
                                            trigger.OpeningTime = orderModel.LastUpdateTime;
                                            trigger.State = "Closing";
                                            trigger.ClosingOrder = SendBuy(newTarget, orderModel.Filled, PositionEffect.Close, trigger, tag, BuyAutoExecutionStyle, stopLoss, orderModel);
                                            List<PairTriggerModel> oppTriggers = BuyTriggers.Where(x => x.Target >= stopLoss).ToList();
                                            if (oppTriggers.Any())
                                            {
                                                string message;
                                                if (oppTriggers.Count == 1)
                                                {
                                                    PairTriggerModel oppTrigger = oppTriggers.FirstOrDefault();
                                                    message = $"#{trigger.Level} SELL entry could be stopped out by BUY Trigger #{oppTrigger.Level}.";
                                                }
                                                else
                                                {
                                                    message = $"#{trigger.Level} SELL entry could be stopped out by BUY Triggers {string.Join(", ", oppTriggers.Select(x => "#" + x.Level))}.";
                                                }
                                                Dispatcher?.BeginInvoke(() => MessageBoxService?.ShowMessage(message, Symbol, MessageButton.OK, MessageIcon.Warning));
                                            }
                                        }
                                    }
                                    if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        SellEntryBlockedByAvgTimeClose = false;
                                        SellEntryBlockedByStoploss = false;
                                    }
                                    else if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                                    {
                                        BuyEntryBlockedByAvgTimeClose = false;
                                        BuyEntryBlockedByStoploss = false;
                                    }
                                }
                                else if (packageUpdate && update.OrderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                                {
                                    if (OrderEnabled && DateTime.Now.TimeOfDay < StopTime.TimeOfDay)
                                    {
                                        if (trigger != null && !trigger.Disposed)
                                        {
                                            if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                            {
                                                if (!BuyEntryBlockedByAvgTimeClose && !BuyEntryBlockedByStoploss)
                                                {
                                                    trigger.Sent = true;
                                                    trigger.State = "Opening";
                                                    trigger.SendTime = DateTime.Now;
                                                    string tag = $"#{trigger.Level} OPEN";
                                                    trigger.Order = SendBuy(trigger.Target, trigger.Qty, PositionEffect.Open, trigger, tag, BuyAutoExecutionStyle);
                                                }
                                                else
                                                {
                                                    trigger.Reset();
                                                }
                                            }
                                            if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                                            {
                                                if (!SellEntryBlockedByAvgTimeClose && !SellEntryBlockedByStoploss)
                                                {
                                                    trigger.Sent = true;
                                                    trigger.State = "Opening";
                                                    trigger.SendTime = DateTime.Now;
                                                    string tag = $"#{trigger.Level} OPEN";
                                                    trigger.Order = SendSell(trigger.Target, trigger.Qty, PositionEffect.Open, trigger, tag, SellAutoExecutionStyle);
                                                }
                                                else
                                                {
                                                    trigger.Reset();
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        trigger.Reset();
                                    }
                                }

                                if (BuyTriggers.Count > 1 &&
                                    BuyTriggers.All(x => x.ClosingOrder != null) &&
                                    !SellTriggers.All(x => x.ClosingOrder != null))
                                {
                                    ShowWarning = true;
                                    ShowBuyWarning = true;
                                    ShowSellWarning = false;
                                    WarnSide = Side = ZeroPlus.Models.Data.Enums.Side.Buy;
                                }
                                else if (SellTriggers.Count > 1 &&
                                         SellTriggers.All(x => x.ClosingOrder != null) &&
                                         !BuyTriggers.All(x => x.ClosingOrder != null))
                                {
                                    ShowWarning = true;
                                    ShowSellWarning = true;
                                    ShowBuyWarning = false;
                                    WarnSide = Side = ZeroPlus.Models.Data.Enums.Side.Sell;
                                }
                                else
                                {
                                    ShowWarning = false;
                                    ShowSellWarning = false;
                                    ShowBuyWarning = false;
                                    WarnSide = null;
                                }
                            }
                            else if (orderModel.Type == PositionEffect.Close)
                            {
                                if (isFilled && allLegsFilled)
                                {
                                    orderModel.StopLoss = double.NaN;
                                    trigger.OpenCloseCounter.AddToClose(orderModel.Filled);

                                    if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        if (orderModel.Tag.Contains("TIMER") && BlockReentryAfterAvgTimeClose)
                                        {
                                            SellEntryBlockedByAvgTimeClose = true;
                                        }
                                        else if (orderModel.Tag.Contains("STOP") && BlockReentryAfterStoploss)
                                        {
                                            SellEntryBlockedByStoploss = true;
                                        }
                                    }
                                    else if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                                    {
                                        if (orderModel.Tag.Contains("TIMER") && BlockReentryAfterAvgTimeClose)
                                        {
                                            BuyEntryBlockedByAvgTimeClose = true;
                                        }
                                        else if (orderModel.Tag.Contains("STOP") && BlockReentryAfterStoploss)
                                        {
                                            BuyEntryBlockedByStoploss = true;
                                        }
                                    }

                                    trigger.ClosingPx = orderModel.AvgFillPx;
                                    trigger.ClosingTime = orderModel.LastUpdateTime;
                                    trigger.Reset();


                                    if (trigger.ClosingTime.Date == DateTime.Today && trigger.OpeningTime.Date == DateTime.Today)
                                    {
                                        double totalMilliseconds = (trigger.ClosingTime - trigger.OpeningTime).TotalMilliseconds;
                                        trigger.CloseTimeSpan = totalMilliseconds;
                                        double lastEdge = trigger.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                                            ? trigger.ClosingPx - trigger.OpeningPx
                                            : trigger.OpeningPx - trigger.ClosingPx;
                                        trigger.TotalUnitFills++;
                                        trigger.TotalUnitPnl += lastEdge;
                                        trigger.AvgUnitPnl = trigger.TotalUnitPnl / trigger.TotalUnitFills;
                                        orderModel.LastEdge = lastEdge;
                                        orderModel.Pnl = lastEdge * orderModel.Filled;
                                        LastPnl = lastEdge * orderModel.Filled;
                                        if (lastEdge > 0)
                                        {
                                            Tuple<DateTime, double> newTime = Tuple.Create(orderModel.LastUpdateTime, totalMilliseconds);
                                            lock (_closeTimeLookupLock)
                                            {
                                                CloseTimes.Add(newTime);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        trigger.CloseTimeSpan = double.NaN;
                                    }

                                    if ((DateTime.Now - LoopCountLastReset).TotalSeconds > 300)
                                    {
                                        ResetLoopCounter();
                                    }
                                    LoopCount++;
                                    if (LoopCount > MAX_LOOP_COUNT)
                                    {
                                        OrderEnabled = false;
                                        Dispatcher.BeginInvoke(() =>
                                        {
                                            MessageResult results = MessageBoxService.ShowMessage("Max Loop count reached.\nWould you like to reset counter and proceed?", Symbol, MessageButton.YesNo, MessageIcon.Warning);
                                            if (results == MessageResult.Yes)
                                            {
                                                ResetLoopCounter();
                                                OrderEnabled = true;
                                            }
                                        });
                                    }
                                }

                                if (packageUpdate && update.OrderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                                {
                                    if (CloseOrders)
                                    {
                                        if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                                        {
                                            if (Ask < orderModel.StopLoss)
                                            {
                                                trigger.State = "Stop-Loss";
                                                trigger.OpeningTime = DateTime.Now;
                                                trigger.ClosingOrder = SendSell(Bid, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag.Replace("CLOSE", "STOP"), ExecutionStyle.Aggressive, orderModel.StopLoss, orderModel.OpeningOrder);
                                            }
                                            else if (CloseByAvgCloseTime &&
                                                (DateTime.Now - trigger.OpeningTime).TotalMilliseconds > GetAvgCloseTime() &&
                                                (DateTime.Now - trigger.OpeningTime).TotalSeconds > MinCloseTimeSec)
                                            {
                                                trigger.State = "Closing [T]";
                                                trigger.OpeningTime = DateTime.Now;
                                                trigger.ClosingOrder = SendSell(Bid, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag.Replace("CLOSE", "TIMER"), ExecutionStyle.Aggressive, orderModel.StopLoss, orderModel.OpeningOrder);
                                            }
                                            else
                                            {
                                                if (PairProfitType == PairTriggerType.Static)
                                                {
                                                    trigger.State = "Closing";
                                                    trigger.ClosingOrder = SendSell(orderModel.TriggerValue, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag, SellAutoExecutionStyle, orderModel.StopLoss, orderModel.OpeningOrder);
                                                }
                                                else
                                                {
                                                    trigger.State = "Closing";
                                                    trigger.ClosingOrder = SendSell(trigger.CloseTarget, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag, SellAutoExecutionStyle, orderModel.StopLoss, orderModel.OpeningOrder);
                                                }
                                            }
                                        }
                                        else if (orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                        {
                                            if (Bid > orderModel.StopLoss)
                                            {
                                                trigger.State = "Stop-Loss";
                                                trigger.OpeningTime = DateTime.Now;
                                                trigger.ClosingOrder = SendBuy(Ask, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag.Replace("CLOSE", "STOP"), ExecutionStyle.Aggressive, orderModel.StopLoss, orderModel.OpeningOrder);
                                            }
                                            else if (CloseByAvgCloseTime &&
                                                (DateTime.Now - trigger.OpeningTime).TotalMilliseconds > GetAvgCloseTime() &&
                                                (DateTime.Now - trigger.OpeningTime).TotalSeconds > MinCloseTimeSec)
                                            {
                                                trigger.State = "Closing [T]";
                                                trigger.OpeningTime = DateTime.Now;
                                                trigger.ClosingOrder = SendBuy(Ask, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag.Replace("CLOSE", "TIMER"), ExecutionStyle.Aggressive, orderModel.StopLoss, orderModel.OpeningOrder);
                                            }
                                            else
                                            {
                                                if (PairProfitType == PairTriggerType.Static)
                                                {
                                                    trigger.State = "Closing";
                                                    trigger.ClosingOrder = SendBuy(orderModel.TriggerValue, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag, BuyAutoExecutionStyle, orderModel.StopLoss, orderModel.OpeningOrder);
                                                }
                                                else
                                                {
                                                    trigger.State = "Closing";
                                                    trigger.ClosingOrder = SendBuy(trigger.CloseTarget, orderModel.Leaves, PositionEffect.Close, orderModel.Trigger, orderModel.Tag, BuyAutoExecutionStyle, orderModel.StopLoss, orderModel.OpeningOrder);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (allLegsFilled)
                    {
                        _ = Dispatcher.BeginInvoke(() => _fillsOnlyPairOrders.Add(orderModel));
                    }
                }
                else
                {
                    _log.Warn($"{nameof(OrderUpdated)} ordermodel not found! Id: {update.ParentLocalOrderId}, Symbol: {Symbol}");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OrderUpdated));
            }
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
        }

        public void AutomationStateChanged(bool running)
        {
        }

        private double GetAvgCloseTime()
        {
            try
            {
                if (CloseByAvgCloseTime)
                {
                    if (CloseTimes.Any())
                    {
                        lock (_closeTimeLookupLock)
                        {
                            IEnumerable<Tuple<DateTime, double>> validTimes = CloseTimes.Where(x => (DateTime.Now - x.Item1).TotalSeconds < AvgCloseTimeLookbackSeconds);
                            if (validTimes.Any())
                            {
                                AvgCloseTime = validTimes.Average(x => x.Item2) * AvgCloseTimeMultiplier;
                            }
                        }
                    }
                }

                return AvgCloseTime;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private async Task<bool> GetOutOfMarketConfirmation()
        {
            bool isValid = true;
            await Dispatcher.BeginInvoke(() =>
            {
                MessageResult response = MessageBoxService.ShowMessage("You are about to send an order with a triggger value that is outside of market.\nAre you sure you want to proceed?", Name, MessageButton.YesNo, MessageIcon.Warning, MessageResult.Yes);
                if (response != MessageResult.Yes)
                {
                    isValid = false;
                }
            });
            return isValid;
        }

        private void ResetLoopCounter()
        {
            LoopCount = 0;
            LoopCountLastReset = DateTime.Now;
        }

        private void StampValues(PairOrderModel orderModel)
        {
            orderModel.Bid = Bid;
            orderModel.Mid = Mid;
            orderModel.Ask = Ask;
            orderModel.HighestBid = HighestBid;
            orderModel.LowestAsk = LowestAsk;
            orderModel.BidEma = BidEma;
            orderModel.MidEma = Ema;
            orderModel.AskEma = AskEma;
        }
    }
}
