using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.Xpf;
using DevExpress.Spreadsheet;
using DevExpress.Xpf.Editors;
using MathNet.Numerics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.OrderRouting;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Models.Structs;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Action = System.Action;
using Formatting = Newtonsoft.Json.Formatting;
using Module = ZeroPlus.Oms.Ui.Models.Module;
using Venue = ZeroPlus.Models.Data.Enums.Venue;
using Window = System.Windows.Window;

namespace ZeroPlus.Oms.Ui.ViewModels
{

    public partial class BasketTraderViewModel : ModuleViewModelBase, IBasket, IAutomationTrader, IOmsDataSubscriber, IDynamicConfigParentModule
    {
        #region  Constants
        internal const string AUTO_PERM_SUBTYPE = "BAP";
        protected readonly string MODULE_TITLE = "Basket Trader";
        private const int AUTO_CLEAN_WAIT_TIME = 1000;
        private const double REMOVE_ALL_INTERVAL = 30;
        private const int MAX_PERM_PER_OP = 200;
        #endregion
        #region Fields
        private readonly System.Timers.Timer _messageClearTimer;
        private readonly System.Timers.Timer _nagbotTimer;
        private readonly ConcurrentDictionary<string, OrderTicket> _activeOrderDescriptions = new();
        private readonly ConcurrentDictionary<string, Data.Trading.PositionModel> _spreadIdToPositionMap = new();
        private readonly ConcurrentDictionary<string, Window> _spreadIdToOpenedTicketsMap = new();
        private CancellationTokenSource _submitWithDelayCancellationTokenSource = new();
        private CancellationTokenSource _timerCts = new();
        private readonly Random _randomGenerator = new();
        private DispatcherTimer _resubmitCountdownTimer;
        private DispatcherTimer _modifyCountdownTimer;
        private bool _modifyAllRunning;
        private int _fillCount;
        private DateTime _startTime;
        private DateTime _lastActivityTime = DateTime.Now;
        private DispatcherTimer _basketTitleBarUpdateTimer;
        private DispatcherTimer _uiUpdateTimer;
        private bool _runningActivator;
        private readonly object _basketCleanLock = new();
        private readonly object _openTicketLock = new();
        private readonly ManualResetEventSlim _orderSubmitResetEvent = new(true);
        private readonly object _activeOrdersLock = new();
        private readonly ConcurrentQueue<OrderTicket> _cancelQueue = new();
        private readonly object _cancelQueueLock = new();
        private bool _cancelQueueSent;
        private DelegateCommand<string> _loadCustomPermCommand;
        private DelegateCommand<string> _loadCustomPermNewBasketCommand;
        private BasketType _basketType = BasketType.BasketTrader;
        private readonly ILogger<BasketTraderViewModel> _logger;
        private readonly IAbstractFactory<CustomEdgeFunctionEditorView> _customEdgeFunctionEditorViewFactory;
        private readonly VolTradersManager _volTradersManager;
        private readonly IModuleFactory _moduleFactory;
        private uint _autoTraderConfigSeq;
        private DelegateCommand<object> _sendToDominatorCommand;
        private TimeSpan _resubmitCountDown;
        private TimeSpan _modifyCountDown;
        private bool _timerUpdated;
        private int _submitIndex;
        private DateTime _lastRemoveAllRunTime = DateTime.Now;
        private DateTime _lastPriceSetAllRunTime = DateTime.Now;


        private bool _nagbotEnabled;
        private DateTime _lastBasketHealthCheck;
        private DropOutStack<BasketState> _undoStack;
        private DropOutStack<BasketState> _redoStack;
        private TimeSpan _elapsedModifyTime;
        private TimeSpan _elapsedResubmitTime;
        private int _resubmitCount;
        public bool[] _areSingles = { };

        #endregion
        public static List<string> EdgeTypes =>
        [
            "Theo",
            "Adj Theo",
            "Mid",
            "Theo & Mid",
            "Theo stop Mid",
            "Mid stop Ema",
            "Bid",
            "% Bid stop Ema",
            "% B 🛑 E 🛑 T",
            "% DB 🛑 E 🛑 M",
            "Theo Bid %",
            "Bid %",
        ];

        public DominatorsManagerModel DominatorsManagerModel { get; set; }
        public TransactionConsumerModel TransactionConsumer { get; set; }
        public PortfolioManagerModel PortfolioManagerModel { get; set; }
        public Notifications.NotificationManager NotificationManager { get; set; }
        public EmaCalculatorGenerator EmaCalculatorGenerator { get; }
        internal IVerificationService VerificationService => GetService<IVerificationService>();
        protected ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        public override Module Module { get; protected set; } = Module.BasketTrader;
        public BasketSettings BasketSettings { get; set; }
        public PxCrossOption[] PxCrossOptions { get; } = Enum.GetValues<PxCrossOption>();
        public LoopPricingMode[] LoopPricingModes { get; } = Enum.GetValues<LoopPricingMode>();
        public ResubmitSide[] ResubmitSides { get; } = Enum.GetValues<ResubmitSide>();
        public EmaType[] EmaTypes { get; } = Enum.GetValues<EmaType>();
        public LoopSizeupType[] LoopSizeupTypes { get; } = Enum.GetValues<LoopSizeupType>();
        public LoopIncrementType[] LoopIncrementTypes { get; } = Enum.GetValues<LoopIncrementType>();
        public LoopIntervalType[] LoopIntervalTypes { get; } = Enum.GetValues<LoopIntervalType>();
        public LoopCloseEdgeType[] LoopCloseEdgeTypes { get; } = Enum.GetValues<LoopCloseEdgeType>();
        public PermSide[] PermSides { get; } = Enum.GetValues<PermSide>();
        public ClosingTypes[] ClosingTypes { get; } = Enum.GetValues<ClosingTypes>();
        public MarketMakerOffsetType[] MarketMakerOffsetTypes { get; } = Enum.GetValues<MarketMakerOffsetType>();
        public DataType[] DataTypes { get; } = Enum.GetValues<DataType>();
        public Venue[] Venues { get; } = Enumerable.ToArray(Enum.GetValues<Venue>().Where(x => x is not Venue.OPS and not Venue.Matrix));
        public TheoModel[] TheoModels { get; } = Enum.GetValues<TheoModel>();
        public EmaModel[] EmaModels { get; } = Enum.GetValues<EmaModel>();
        public InstanceMode[] InstanceModes { get; } = Enum.GetValues<InstanceMode>();
        public PermType[] PermTypes { get; } = Enum.GetValues<PermType>();
        public SideOperation[] SideOperations { get; } = Enum.GetValues<SideOperation>();
        public AutoPermSelectionMode[] AutoPermSelectionModes { get; } = Enum.GetValues<AutoPermSelectionMode>();
        public AutoPermSubmissionStyle[] AutoPermSubmissionStyles { get; } = Enum.GetValues<AutoPermSubmissionStyle>();
        public InstanceMode[] BasketAutomationModes { get; } = Enum.GetValues<InstanceMode>();
        public MatrixStrategy[] MatrixStrategies { get; } = Enum.GetValues<MatrixStrategy>();
        public static TimeSpan RiskTimeSpan { get; set; } = TimeSpan.FromMilliseconds(500);
        public ObservableCollection<BasketGroupModel> BasketGroups { get; } = [];
        public string Username { get; set; }
        public string Host { get; set; }
        public string Setup { get; set; }
        public string List { get; set; }
        public string Tag { get; set; }
        public string SampleDescription
        {
            get => BasketItems.FirstOrDefault()?.Description;
            set => _ = value;
        }
        public uint ConfigSequence { get; private set; }
        public OrderSubType? ModuleType { get; internal set; } = OrderSubType.Basket;
        public double MinWidthForClear { get; set; } = double.NaN;
        public double MaxWidthForClear { get; set; } = double.NaN;
        public bool AutoConfigUpdated { get; set; }
        public BasketGroupManagerModel BasketGroupManagerModel { get; }
        public IAbstractFactory<ComplexOrderTicketViewModel> TicketFactory { get; internal set; }
        public IAbstractFactory<ThreeWayCloser> ThreeWayCloserFactory { get; internal set; }
        public IAbstractFactory<RouteSelectionViewModel> RouteSelectionViewFactory { get; internal set; }
        public int MaxAutoPermOrderCount => OmsCore.User.LimitByMaxAutoPermCount ? OmsCore.User.MaxAutoPermCount : 20;
        public int MaxAutoPermOrderInitialSize => OmsCore.User.LimitByMaxAutoPermSlamSize ? OmsCore.User.MaxAutoPermSlamSize : 20;
        public int MaxAutoPermMaxGeneration => OmsCore.User.LimitByMaxAutoPermGeneration ? OmsCore.User.MaxAutoPermGeneration : 20;

        internal BasketType BasketType
        {
            get => _basketType;
            set
            {
                _basketType = value;
                Module = BasketType == BasketType.BasketTrader ? Module.BasketTrader : Module.LockTrader;
            }
        }
        public string InstanceId
        {
            get => BasketSettings?.Uid;
            set => _ = value;
        }

        #region Bindable Properties

        public ObservableCollection<Comms.Models.Data.Oms.Config.ConfigSave> UserConfigs { get; } = new();
        [Bindable]
        public partial Comms.Models.Data.Oms.Config.ConfigSave Config { get; set; }
        [Bindable]
        public partial ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> UnderlyingToAutomationConfigModelLookup { get; set; }
        [Bindable]
        public partial bool IsEdgeScanFeedAutoTrader { get; set; }
        [Bindable(Default = true)]
        public partial bool LayoutLocked { get; set; }
        [Bindable]
        public partial BasketGroupModel SelectedBasketGroup { get; set; }
        [Bindable]
        public partial Brush BorderBrush { get; set; }
        [Bindable]
        public partial string UpTime { get; set; }
        [Bindable]
        public partial string Name { get; set; }
        [Bindable(Default = true)]
        public partial bool InstanceModeLocked { get; set; }
        [Bindable]
        public partial InstanceMode InstanceMode { get; set; }
        [Bindable(Default = true)]
        public partial bool BrokerLocked { get; set; }
        [Bindable]
        public partial string BrokerOverride { get; set; }
        public string EffectiveBroker => BrokerLocked || string.IsNullOrWhiteSpace(BrokerOverride) ? OmsCore.Config.DefaultBroker : BrokerOverride;

        // Mirrors OrderTicket.RoutesGoThroughAutoTrader so basket dropdowns and the
        // wire format stay aligned with the broker-prefix gate in ApplyBrokerPrefix.
        public bool RoutesGoThroughAutoTrader =>
            InstanceMode.IsAutoTraderInstance() || OmsCore.Config.RouteOpsOrdersToAutoTraderDirect;
        [Bindable]
        public partial bool BasketAutomationStatusReady { get; set; }
        public bool NagbotEnabled
        {
            get => _nagbotEnabled;
            set
            {
                SetValue(ref _nagbotEnabled, value);
                if (value)
                {
                    _nagbotTimer.Start();
                }
                else
                {
                    _nagbotTimer.Stop();
                }
            }
        }
        [Bindable]
        public partial BasketDescriptionModel ExpirationDescription { get; set; }
        [Bindable]
        public partial bool ShowBasketDeltaAdjLastFillPx { get; set; }
        [Bindable]
        public partial string Underlyings { get; set; }
        public double ServerCreep => Math.Round(OmsCore.QuoteClient.ServerCreepMs / 1000.0, 3, MidpointRounding.AwayFromZero);
        [Bindable]
        public partial bool ShowProgressBar { get; set; }
        [Bindable]
        public partial double ProgressValue { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial string FilePath { get; set; }
        [Bindable]
        public partial string FileName { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<BasketTraderItemModel> BasketItems { get; set; }
        [Bindable]
        public partial BasketTraderItemModel SelectedItem { get; set; }
        [Bindable(Initialize = true)]
        public partial List<object> SelectedItems { get; set; }
        [Bindable]
        public partial bool AvoidDuplicates { get; set; }
        [Bindable]
        public partial bool AvoidInvalid { get; set; }
        [Bindable]
        public partial bool PermSelf { get; set; }
        [Bindable]
        public partial int Count { get; set; }
        [Bindable]
        public partial PermType PermType { get; set; }
        [Bindable]
        public partial PermSide PermSide { get; set; }
        [Bindable]
        public partial bool MaintainBaseStrategyOnPerm { get; set; }
        [Bindable]
        public partial bool ContraEnabled { get; set; }
        [Bindable]
        public partial bool AlsoOpenContraTicketEnabled { get; set; }
        [Bindable]
        public partial bool AutoSave { get; set; }
        [Bindable]
        public partial bool AutoClean { get; set; }
        [Bindable]
        public partial bool Loaded { get; set; }
        [Bindable]
        public partial string EdgeDescription { get; set; }
        [Bindable]
        public partial bool ShowEdgeSettings { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowBestOfEdgeExpanded { get; set; }
        [Bindable]
        public partial bool BestOfEdgeLocked { get; set; }
        [Bindable]
        public partial bool BestOfEdgeLockFlash { get; set; }
        [Bindable]
        public partial bool ShowEdgeWarning { get; set; }
        [Bindable]
        public partial bool ShowBasketSettings { get; set; }
        [Bindable]
        public partial bool ShowMarketMakerSettings { get; set; }
        [Bindable]
        public partial bool ShowHedgeSettings { get; set; }
        [Bindable]
        public partial bool ShowPermSettings { get; set; }
        [Bindable]
        public partial bool ShowAdvancedPermSettings { get; set; }
        [Bindable]
        public partial bool ShowMorphSettings { get; set; }
        [Bindable]
        public partial bool ShowContraSettings { get; set; }
        [Bindable]
        public partial bool ShowSubmitWithDelaySettings { get; set; }
        [Bindable]
        public partial bool ShowFishSettings { get; set; }
        [Bindable]
        public partial bool ShowRouteSettings { get; set; }
        [Bindable]
        public partial bool ShowAdvancedRouteSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoCloseSettings { get; set; }
        [Bindable]
        public partial bool ShowHedgeHouseSettings { get; set; }
        [Bindable]
        public partial bool ShowNotificationSettings { get; set; }
        [Bindable]
        public partial bool ShowLoggingSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoLegSettings { get; set; }
        [Bindable]
        public partial bool ShowAlerts { get; set; }
        [Bindable]
        public partial bool ShowMatrixAlgos { get; set; }
        [Bindable]
        public partial bool ShowNagbotSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoCancelSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoPermSettings { get; set; }
        [Bindable]
        public partial bool ShowLegOutSettings { get; set; }
        [Bindable]
        public partial bool ShowLegInSettings { get; set; }
        [Bindable]
        public partial bool ShowSweepTradeSettings { get; set; }
        [Bindable]
        public partial bool ShowFishLossSettings { get; set; }
        [Bindable]
        public partial bool ShowEdgeToTheoModelSettings { get; set; }
        [Bindable]
        public partial bool ShowSubscriptionManager { get; set; }
        [Bindable]
        public partial bool ShowBlockListSettings { get; set; }
        [Bindable]
        public partial bool ShowBasketStats { get; set; }
        [Bindable]
        public partial bool ShowStockTiedSettings { get; set; }
        [Bindable]
        public partial bool ShowCheapoSettings { get; set; }
        [Bindable]
        public partial ResubmitSide ResubmitSide { get; set; }
        [Bindable]
        public partial bool ResubmitOnTimer { get; set; }
        [Bindable]
        public partial bool ActivateWindowOnResubmitFill { get; set; }
        [Bindable]
        public partial bool MinWidthFishLossVisible { get; set; }
        [Bindable]
        public partial bool MaxWidthFishLossVisible { get; set; }
        [Bindable]
        public partial bool TheoEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool HwTheoEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool V0TheoEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool MinTheoFishLossVisible { get; set; }
        [Bindable]
        public partial bool MinEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool EmaEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool MktEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool SkewMktEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool SkewCrossEdgeFishLossVisible { get; set; }
        [Bindable]
        public partial bool MinPercentBidFishLossVisible { get; set; }
        [Bindable]
        public partial bool MaxPercentBidFishLossVisible { get; set; }
        [Bindable]
        public partial bool MaxDigPercentBidFishLossVisible { get; set; }
        [Bindable]
        public partial bool MinBidFishLossVisible { get; set; }
        [Bindable]
        public partial bool MinBidAskSizeFishLossVisible { get; set; }
        [Bindable]
        public partial bool WidthPercentE2TFishLossVisible { get; set; }
        [Bindable]
        public partial bool FirmAttemptFishLossVisible { get; set; }
        [Bindable]
        public partial bool FirmTradeFishLossVisible { get; set; }
        [Bindable]
        public partial bool PermTimeFishLossVisible { get; set; }
        [Bindable]
        public partial bool PermLoserFishLossVisible { get; set; }
        [Bindable]
        public partial bool RecentAttemptFishLossVisible { get; set; }
        [Bindable]
        public partial bool PxCrossMktFishLossVisible { get; set; }
        [Bindable]
        public partial int ResubmitIntervalSec { get; set; }
        [Bindable]
        public partial int ResubmitIntervalCount { get; set; }
        public TimeSpan ResubmitCountDown
        {
            get => _resubmitCountDown;
            set
            {
                _resubmitCountDown = value;
                _timerUpdated = true;
            }
        }
        [Bindable]
        public partial bool ModifyOnTimer { get; set; }
        [Bindable]
        public partial double ModifyIntervalSec { get; set; }
        [Bindable]
        public partial bool ResetVolumeChange { get; set; }
        public TimeSpan ModifyCountDown
        {
            get => _modifyCountDown;
            set
            {
                _modifyCountDown = value;
                _timerUpdated = true;
            }
        }
        [Bindable]
        public partial bool RemoveAllOnInterval { get; set; }
        [Bindable]
        public partial bool RecalculatePriceOnInterval { get; set; }
        [Bindable]
        public partial Tuple<BasketTraderItemModel, double, AutoPermConfigModel, bool, List<Tuple<BasketTraderItemModel, BasketTraderItemModel>>> PermInput { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial bool ShowMessage { get; set; }
        [Bindable]
        public partial string MorphSymbolsQuery { get; set; }
        [Bindable]
        public partial string MorphSummary { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> MorphSymbols { get; set; }
        [Bindable]
        public partial double RealizedPnl { get; set; }
        [Bindable]
        public partial double AdjustedPnl { get; set; }
        [Bindable]
        public partial double UnrealizedPnl { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial int TotalPositions { get; set; }
        [Bindable]
        public partial bool TotalPositionsInitialized { get; set; }
        [Bindable]
        public partial double HedgeNetDelta { get; set; }
        [Bindable]
        public partial double TotalNetDelta { get; set; }
        [Bindable]
        public partial double NetWeightedVega { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> DmaRoutesList { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<string> SorRoutesList { get; set; }
        [Bindable]
        public partial int RowCount { get; set; }
        [Bindable]
        public partial int Fills { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<VolTraderViewModel> VolTraders { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<AutomationConfigModel> AutomationConfigModels { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<BasketLoopBlockListModel> BasketLoopBlockListModels { get; set; }
        [Bindable]
        public partial string LoadedEdgeToTheoModelName { get; set; }
        [Bindable]
        public partial string LoadedEdgeToTheoModelPath { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<BasketLayoutQuickAccessModel> BasketLayoutQuickAccess { get; set; }
        [Bindable]
        public partial bool AutoPermOnFill { get; set; }
        [Bindable(Initialize = true)]
        public partial ObservableCollection<PermOperationModel> PermOperationModels { get; set; }
        [Bindable]
        public partial bool SubmitAllRunning { get; set; }
        [Bindable]
        public partial bool SkipRestingOrders { get; set; }
        [Bindable]
        public partial int SubmittedCount { get; set; }
        [Bindable]
        public partial int FillsCount { get; set; }
        [Bindable]
        public partial int FailedCount { get; set; }
        [Bindable]
        public partial Brush MixedBasketBorderColor { get; set; }
        #endregion
        public IUIObjectService MixedBasketBorderService => GetService<IUIObjectService>("MixedBasketBorderService");

        public bool[] AreSingles
        {
            get => _areSingles;
            private set => SetValue(ref _areSingles, value);
        }

        void SetMixedBasketBorder(IEnumerable<bool> statuses)
        {
            Dispatcher?.BeginInvoke(() =>
            {
                if (statuses.Count() > 1)
                {
                    MixedBasketBorderColor =
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString(OmsCore.Config.MixedBorderColor));
                }
                else if (statuses.Contains(true))
                {
                    MixedBasketBorderColor =
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString(OmsCore.Config.SinglesLegBorderColor));
                }
                else if (statuses.Contains(false))
                {
                    MixedBasketBorderColor =
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString(OmsCore.Config.MultiLegBorderColor));
                }
                else
                {
                    MixedBasketBorderColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            });
        }

        public ICommand SendToDominatorCommand
        {
            get
            {
                _sendToDominatorCommand ??= new DelegateCommand<object>(SendToDominator);
                return _sendToDominatorCommand;
            }
        }
        public ICommand LoadCustomPermCommand
        {
            get
            {
                _loadCustomPermCommand ??= new DelegateCommand<string>(LoadCustomPerms);

                return _loadCustomPermCommand;
            }
        }
        public ICommand LoadCustomPermNewBasketCommand
        {
            get
            {
                _loadCustomPermNewBasketCommand ??= new DelegateCommand<string>(LoadCustomPermsNew);

                return _loadCustomPermNewBasketCommand;
            }
        }

        public BasketTraderViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) :
            base(configBrowserViewModel, omsCore)
        {
            InstanceMode = OmsCore.Config.InstanceModeV3;
            configBrowserViewModel.LoadConfig = LoadSavedConfig;
        }

        public BasketTraderViewModel(ILogger<BasketTraderViewModel> logger,
                                     IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
                                     IAbstractFactory<RouteSelectionViewModel> routeSelectionViewFactory,
                                     IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
                                     IAbstractFactory<CustomEdgeFunctionEditorView> customEdgeFunctionEditorViewFactory,
                                     VolTradersManager volTradersManager,
                                     TransactionConsumerModel transactionConsumer,
                                     PortfolioManagerModel portfolioManagerModel,
                                     Notifications.NotificationManager notificationManager,
                                     DominatorsManagerModel dominatorsManagerModel,
                                     ConfigBrowserViewModel configBrowserViewModel,
                                     BasketGroupManagerModel basketGroupManagerModel,
                                     OmsCore omsCore,
                                     IModuleFactory moduleFactory)
            : base(configBrowserViewModel, omsCore)
        {
            InstanceMode = OmsCore.Config.InstanceModeV3;
            _logger = logger;
            Username = OmsCore.User.Username;
            Host = Environment.MachineName;
            ModuleTitle = MODULE_TITLE;
            FileName = BasketType == BasketType.BasketTrader ? Module.BasketTrader.ToString() : Module.LockTrader.ToString();
            BasketSettings = new BasketSettings();
            ExpirationDescription = new BasketDescriptionModel();
            EmaCalculatorGenerator = new EmaCalculatorGenerator(BasketSettings);
            PortfolioManagerModel = portfolioManagerModel;
            NotificationManager = notificationManager;
            TransactionConsumer = transactionConsumer;
            DominatorsManagerModel = dominatorsManagerModel;
            BasketGroupManagerModel = basketGroupManagerModel;
            TicketFactory = ticketFactory;
            ThreeWayCloserFactory = threeWayCloserFactory;
            RouteSelectionViewFactory = routeSelectionViewFactory;
            _customEdgeFunctionEditorViewFactory = customEdgeFunctionEditorViewFactory;
            _volTradersManager = volTradersManager;
            _moduleFactory = moduleFactory;

            _nagbotTimer = new System.Timers.Timer(1000) { AutoReset = false };
            _messageClearTimer = new System.Timers.Timer(7000) { AutoReset = false };

            InitializeStacksAndMemory();
            InitializeUserInterface();
            InitializeEdgeSettings();
            AddBasketGroup();
            InitializeTimers();
            Count = 10;
            AvoidDuplicates = true;
            AvoidInvalid = true;
            ResubmitIntervalSec = 60;
            ResubmitIntervalCount = 99;
            ModifyIntervalSec = 60;
            BorderBrush = new SolidColorBrush(Colors.Transparent);

            foreach (var item in _volTradersManager.VolTraders)
            {
                VolTraders.Add(item);
            }
            _volTradersManager.VolTraderUpdatedEvent += VolTraderUpdatetd;
            OmsCore.Config.ConfigChangedEvent += OnConfigChangedEvent;
            SubscribePnl();

            StartUiUpdateTimer();
            StartGeneralBasketUpdateTimer();

            ConfigBrowserViewModel.LoadConfig = LoadSavedConfig;
            ConfigBrowserViewModel.Module = Module.BasketTraderLayout.ToString();
            ConfigBrowserViewModel.Refresh();
            LoadBasketSavedConfigs(OmsConfig.GetConfigDirectory());
            _ = UpdateRoutesListAsync();
            OmsCore.AutoTraderClient.AccountsAndRoutesLoaded += () => Task.Run(UpdateRoutesListAsync);
        }

        private void InitializeTimers()
        {
            _resubmitCountdownTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
            _modifyCountdownTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };

            _nagbotTimer.Elapsed += OnNagbotTimerElapsed;
            _messageClearTimer.Elapsed += OnMessageClearTimerElapsed;
        }

        private void OnMessageClearTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Message = "";
            ShowMessage = false;
        }

        private void InitializeStacksAndMemory()
        {
            _undoStack = new(OmsCore.Config.UndoCapacity);
            _redoStack = new(OmsCore.Config.UndoCapacity);
        }

        private void InitializeUserInterface()
        {
            ShowBasketSettings = true;
            ShowEdgeSettings = true;
            ShowMarketMakerSettings = true;
            ShowHedgeSettings = true;
            ShowPermSettings = true;
            ShowMorphSettings = true;
            ShowContraSettings = true;
            ShowSubmitWithDelaySettings = true;
            ShowFishSettings = true;
            ShowAutoCloseSettings = true;
            ShowHedgeHouseSettings = true;
            ShowNotificationSettings = true;
            ShowNagbotSettings = true;
            ShowAutoLegSettings = true;
            ShowAlerts = true;
            ShowAutoCancelSettings = true;
            ShowAutoPermSettings = true;
            ShowLegOutSettings = false;
            ShowLegInSettings = false;
            ShowSweepTradeSettings = false;
            ShowFishLossSettings = true;
            ShowEdgeToTheoModelSettings = true;
            ShowSubscriptionManager = true;
            ShowBlockListSettings = true;
            ShowBasketStats = true;
        }

        private void InitializeEdgeSettings()
        {
            BasketSettings.EdgeToTheo = 0;
            BasketSettings.EdgeToHistoricBest = 0;
            BasketSettings.EdgeToAdjTheo = 0;
            BasketSettings.EdgeToMid = 0;
            BasketSettings.EdgeToEma = 0;
            BasketSettings.LastFillAdjEdge = 0;
            BasketSettings.EdgeToTheoAndMid = 0;
            BasketSettings.EdgeToTheoStopMid = 0;
            BasketSettings.EdgeToEmaStopMid = 0;
            BasketSettings.EdgeToMidStopEma = 0;
            BasketSettings.EdgeToBidPercentStopEma = 0;
            BasketSettings.BidPercent = 0;
            BasketSettings.EdgeToEmaBid = 0;
            BasketSettings.EdgeToBid = 0;
            BasketSettings.PermAdjEdge = 0;
            BasketSettings.EdgeToAdjTheoWithOverrideStatic = 0;
            BasketSettings.EdgeToAdjTheoWithOverridePercent = 0;
            BasketSettings.UseEdgeToTheo = false;
            BasketSettings.UseCustomFunctionEdge = false;
            BasketSettings.UseDomStyleEdge = false;
            BasketSettings.UseEdgeToHistoricBest = false;
            BasketSettings.UseEdgeToAdjTheo = false;
            BasketSettings.UseBidPercent = false;
            BasketSettings.UseEdgeToEmaBid = false;
            BasketSettings.UseEdgeToBid = false;
            BasketSettings.UsePermAdjPx = false;
            BasketSettings.UseLastFillAdjPx = false;
            BasketSettings.UseTheoToMarketSpreadPx = false;
            BasketSettings.UseEdgeToMid = false;
            BasketSettings.UseEdgeToEma = false;
            BasketSettings.UseEdgeToTheoAndMid = false;
            BasketSettings.UseEdgeToTheoStopMid = false;
            BasketSettings.UseEdgeToEmaStopMid = false;
            BasketSettings.UseEdgeToMidStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = false;
            BasketSettings.UseEdgeToAdjTheoWithOverride = false;
            BasketSettings.UseBestOfEdge = false;
        }

        public override void OnSetDispatcher()
        {
            base.OnSetDispatcher();
            OnConfigChangedEvent(OmsCore.Config, false);
        }

        private void VolTraderUpdatetd(VolTraderViewModel model, bool added)
        {
            Dispatcher?.BeginInvoke(() =>
            {
                if (added)
                {
                    VolTraders.Add(model);
                }
                else
                {
                    VolTraders.Remove(model);
                }
            });
        }

        void SetBorderKeyBinding(string gesture)
        {
            Dispatcher?.BeginInvoke(() =>
            {
                try
                {
                    KeyGesture keyGesture = new KeyGestureConverter().ConvertFromString(gesture) as KeyGesture;
                    var inputBindings = MixedBasketBorderService?.Object?.InputBindings;
                    if (inputBindings != null && inputBindings.Count > 0)
                    {
                        var inputBinding = inputBindings[0];
                        if (inputBinding is KeyBinding keyBinding)
                        {
                            keyBinding.Gesture = keyGesture;
                        }
                    }
                }
                catch
                {
                    // Ignored
                }
            });
        }

        internal void QueueForCancel(OrderTicket ticket)
        {
            lock (_cancelQueueLock)
            {
                if (_cancelQueueSent)
                {
                    _cancelQueue.Enqueue(ticket);
                }
                else
                {
                    SendCancel(ticket);
                }
            }
        }

        private void SendCancel(OrderTicket ticket)
        {
            _cancelQueueSent = true;
            ticket.OrderClosedUpdateEvent += OnTicketClosedEvent;
            ticket.SendCancelRequest(true, default, default, default, true);
        }

        private void OnTicketClosedEvent(IOmsOrder order, OrderStatus orderStatus, OrderTicket ticket)
        {
            ticket.OrderClosedUpdateEvent -= OnTicketClosedEvent;

            if (_cancelQueue.IsEmpty)
            {
                _cancelQueueSent = false;
            }
            else
            {
                while (_cancelQueue.TryDequeue(out var nextOrder))
                {
                    if (!nextOrder.IsDisposed && nextOrder.MainResting)
                    {
                        SendCancel(nextOrder);
                        break;
                    }
                }
            }
        }

        private void OnNagbotTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            const double MIN_NAG_INTERVAL = 5;
            try
            {
                if (NagbotEnabled && BasketItems != null)
                {
                    NagbotIntervalModel nagbotIntervalModel = BasketSettings.NagbotIntervalModel;
                    if (nagbotIntervalModel != null && nagbotIntervalModel.Configs.Count > 0)
                    {
                        int count = BasketItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                BasketTraderItemModel item = BasketItems[i];
                                if (item != null &&
                                    item.Active &&
                                    item.NagEnabled)
                                {
                                    if (item.NextNagTime == default)
                                    {
                                        double? interval = nagbotIntervalModel.Configs.FirstOrDefault().Interval;
                                        _log.Info(nameof(OnNagbotTimerElapsed) + $" New Nag Stats. " +
                                                                                 $"Interval: {interval}, " +
                                                                                 $"Spread: {item.GetStats()}");
                                        if (interval.HasValue)
                                        {
                                            item.CurrentNagInterval = interval.Value;
                                            item.NextNagTime = DateTime.Now + TimeSpan.FromSeconds(Math.Max(interval.Value, MIN_NAG_INTERVAL));
                                        }
                                        else
                                        {
                                            item.NagEnabled = false;
                                        }
                                    }
                                    else if (DateTime.Now > item.NextNagTime)
                                    {
                                        item.NagCountdown = TimeSpan.Zero;
                                        double? interval = nagbotIntervalModel.Configs.FirstOrDefault(x => x.Interval > item.CurrentNagInterval)?.Interval;
                                        if (!interval.HasValue && nagbotIntervalModel.Repeat)
                                        {
                                            interval = nagbotIntervalModel.Configs.FirstOrDefault(x => x.Interval > MIN_NAG_INTERVAL)?.Interval;
                                        }
                                        if (interval.HasValue)
                                        {
                                            item.CurrentNagInterval = interval.Value;
                                            item.NextNagTime = DateTime.Now + TimeSpan.FromSeconds(Math.Max(interval.Value, MIN_NAG_INTERVAL));
                                            _log.Info(nameof(OnNagbotTimerElapsed) + $" Pre Nag Stats. " +
                                                                                     $"Interval: {interval}, " +
                                                                                     $"Spread: {item.GetStats()}");
                                            if (!double.IsNaN(item.LastMainUnderMidAtBestFill))
                                            {
                                                double change = Math.Round(Math.Abs(item.LastMainUnderMidAtBestFill - item.UnderMid), 2);
                                                double volChange = item.TotalVolume - item.LastMainTotalVolumeAtFill;
                                                _log.Info(nameof(OnNagbotTimerElapsed) + $" Nag Stats. " +
                                                                                         $"Interval: {interval}, " +
                                                                                         $"Under Change: {change}, " +
                                                                                         $"Vol Change: {volChange}, " +
                                                                                         $"Spread: {item.GetStats()}");
                                                if (change <= BasketSettings.NagbotMaxChangeInUnderlying && (double.IsNaN(volChange) || volChange <= BasketSettings.NagbotMaxChangeInVolume))
                                                {
                                                    item.UseEdgeToBestLastFillAdjPx(BasketSettings.NagBotEdge).ContinueWith(t =>
                                                    {
                                                        if (t.Result)
                                                        {
                                                            _ = item.SubmitOrder(resting: false, skipAdjPxBeforeSubmit: true, totalResubmitCount: 0, markForRemoval: false, doNotTradeThroughFillPrice: false, OrderSubType.NagBot);
                                                        }
                                                    });
                                                }
                                                else if (nagbotIntervalModel.StopOnFailure)
                                                {
                                                    item.NagEnabled = false;
                                                }
                                            }
                                            else
                                            {
                                                item.NagEnabled = false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        item.NagCountdown = TimeSpan.FromSeconds((int)(item.NextNagTime - DateTime.Now).TotalSeconds);
                                    }
                                }
                                else
                                {
                                    item.NagCountdown = TimeSpan.Zero;
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(OnNagbotTimerElapsed));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnNagbotTimerElapsed));
                if (BasketItems == null || BasketSettings == null || IsDisposed)
                {
                    NagbotEnabled = false;
                }
            }
            finally
            {
                if (NagbotEnabled && !IsDisposed)
                {
                    _nagbotTimer.Start();
                }
            }
        }

        private void SendToDominator(object parameter)
        {
            try
            {
                if (parameter is Tuple<BasketTraderItemModel, DominatorModel> tradeDomPair)
                {
                    BasketTraderItemModel model = tradeDomPair?.Item1;
                    DominatorModel dominator = tradeDomPair?.Item2;

                    if (model != null && dominator != null)
                    {
                        TradeForDom trade = new()
                        {
                            UnderSymbol = model.Underlying,
                            Exchange = model.LastExchange,
                            SpreadType = model.BaseStrategy.ToString(),
                            Quantity = model.Quantity,
                            Symbol = model.Symbol,
                            Bid = model.Bid,
                            Ask = model.Ask,
                            Price = model.Price,
                            UnderPrice = model.UnderMid,
                            MidMarket = model.Mid,
                            TradeDelta = model.Delta,
                        };
                        dominator.SendTradeToDominator(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToDominator));
            }
        }

        private void LoadBasketSavedConfigs(string configDirectory)
        {
            try
            {
                LoadAutomationConfigs(configDirectory);
                LoadSavedBasketLayoutConfigsCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadBasketSavedConfigs));
            }
        }

        [Command]
        public void BrokerLockedCommand()
        {
            BrokerLocked = !BrokerLocked;
            if (BrokerLocked)
            {
                BrokerOverride = null;
            }
        }

        [Command]
        public void ResetMaxRestingCommand()
        {
            if (!BasketSettings.MaxRestingSet)
            {
                BasketSettings.MaxRestingOrdersCount = BasketSettings.MaxRestingOrdersCountLastState;
                BasketSettings.MaxRestingOrdersEnabled = BasketSettings.MaxRestingOrdersEnabledLastState;
            }
            else
            {
                BasketSettings.MaxRestingOrdersCountLastState = BasketSettings.MaxRestingOrdersCount;
                BasketSettings.MaxRestingOrdersEnabledLastState = BasketSettings.MaxRestingOrdersEnabled;
                BasketSettings.MaxRestingOrdersCount = 1;
                BasketSettings.MaxRestingOrdersEnabled = true;
            }
        }

        [Command]
        public void InitQtyUncheckedCommand()
        {
            foreach (var basketItem in BasketItems)
            {
                try
                {
                    basketItem.UpdateQty(1);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(InitQtyUncheckedCommand));
                }
            }
        }

        [Command]
        public void LoadSavedBasketLayoutConfigsCommand()
        {
            if (OmsCore.Config.ShowBasketQuickLayoutPanel)
            {
                if (Dispatcher != null)
                {
                    Dispatcher.Invoke(() => LoadQuickAccessButtons());
                }
                else
                {
                    LoadQuickAccessButtons();
                }
            }
        }

        [Command]
        public void LoadEdgeDescriptionCommand()
        {
            if (BasketSettings.UseEdgeToTheo || BasketSettings.UseEdgeToAdjTheo)
            {
                switch (BasketSettings.TheoModel)
                {
                    case TheoModel.Hanw:
                        EdgeDescription = "Hanweck Theo Model";
                        break;
                    default:
                        int modelId = (int)BasketSettings.TheoModel - 1;
                        EdgeDescription = OmsCore.UpdateManager.GetModelDescription(modelId);
                        break;
                }
            }
        }

        [Command]
        public async void AdjustPriceBeforeDisabledCommand()
        {
            if (Dispatcher != null && BasketSettings != null && BasketItems != null && BasketItems.Any())
            {
                if (!BasketSettings.AdjustPriceBeforeSubmit)
                {
                    bool proceed = await GetVerificationAsync("Are you sure you want to disable adjusting px before submitting?", $"{ModuleTitle} - ZeroPlus OMS");
                    if (!proceed)
                    {
                        BasketSettings.AdjustPriceBeforeSubmit = true;
                    }
                }
            }
        }

        [Command]
        public async void SizeUpConfigChangedCommand()
        {
            if (OmsCore.Config.WarnAgainstLargeSizeUpConfigV2)
            {
                if (Dispatcher != null && BasketItems != null && BasketItems.Any())
                {
                    var automationConfig = GetAutomationConfig();
                    if (automationConfig != null)
                    {
                        if (automationConfig.LoopSizeupType == LoopSizeupType.Dynamic &&
                            automationConfig.SizeupConfig != null &&
                            automationConfig.SizeupConfig.SizeUpConfigs.Any() &&
                            automationConfig.SizeupConfig.SizeUpConfigs.Max(x => x.Size) >= OmsCore.Config.WarnAgainstLargeSizeUpQty)
                        {
                            bool proceed = await GetVerificationAsync($"Are you sure you want to use dynamic size up '{automationConfig.SizeupConfig?.Title}'?", $"{ModuleTitle} - ZeroPlus OMS");
                            if (!proceed)
                            {
                                automationConfig.LoopSizeupType = LoopSizeupType.Off;
                            }
                        }
                        else if (automationConfig.LoopSizeupType == LoopSizeupType.Static &&
                                 automationConfig.LoopSizeupQty >= OmsCore.Config.WarnAgainstLargeSizeUpQty)
                        {
                            bool proceed = await GetVerificationAsync($"Are you sure you want to use static size up of qty {automationConfig.LoopSizeupQty}?", $"{ModuleTitle} - ZeroPlus OMS");
                            if (!proceed)
                            {
                                automationConfig.LoopSizeupType = LoopSizeupType.Off;
                            }
                        }
                    }
                }
            }
        }

        [Command]
        public void ApplyAutoTraderChanges()
        {
            try
            {
                if (!GetInstanceMode().IsAutoTraderInstance() ||
                    !OmsCore.AutoTraderClient.IsConnected)
                {
                    if (AutoConfigUpdated)
                    {
                        AutoConfigUpdated = false;
                        BasketAutomationStatusReady = false;
                    }
                    return;
                }

                AutoTraderConfig autoTraderConfig = GetAutoTraderConfig();

                OmsCore.AutoTraderClient.SendAutoTraderConfig(autoTraderConfig);

                AutoConfigUpdated = true;
                BasketAutomationStatusReady = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ApplyAutoTraderChanges));
            }
        }

        public AutoTraderConfig GetAutoTraderConfig()
        {
            var automationConfig = GetAutomationConfig();
            ConfigSequence = Interlocked.Increment(ref _autoTraderConfigSeq);
            var config = new AutoTraderConfig()
            {
                Sequence = ConfigSequence,
                ConfigId = Uid,
                ConfigName = Name,
                UserId = (uint)OmsCore.User.ID,
                EdgeType = GetEdgeTypeEnum(),
                EdgeValue = GetEdge(),
                TheoModel = BasketSettings.TheoModel,
                AutoCancelTheoModel = BasketSettings.AutoCancelTheoModel,
                FishLossTheoModel = BasketSettings.FishLossTheoModel,
                EdgeToAdjTheoWithOverrideUsePercentage = BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage,
                EdgeToAdjTheoWithOverrideStatic = BasketSettings.EdgeToAdjTheoWithOverrideStatic,
                EdgeToAdjTheoWithOverridePercent = BasketSettings.EdgeToAdjTheoWithOverridePercent,
                SweepRoute = OmsCore.Config.DefaultSweepRoute(InstanceMode.AT_TB),
                ForMarketCrossPriceUseSweepEnabled = OmsCore.Config.ForCrossPriceUseRouteEnabled,

                CancelWithOrderPriceEdgeToTheoEnabled = BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled,
                CancelWithOrderPriceEdgeToTheo = BasketSettings.CancelWithOrderPriceEdgeToTheo,

                CancelWithOrderPriceEdgeToModelTheoEnabled = BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled,
                CancelWithOrderPriceEdgeToModelTheo = BasketSettings.CancelWithOrderPriceEdgeToModelTheo,

                CancelWithMaxSizeEnabled = BasketSettings.CancelWithMaxSizeEnabled,
                CancelWithMaxSizeLimit = BasketSettings.CancelWithMaxSizeLimit,

                CancelWithTimerEnabled = BasketSettings.CancelWithTimerEnabled,
                CancelWithTimer = BasketSettings.CancelWithTimer,
                CancelWithEdgeToTheoEnabled = BasketSettings.CancelWithEdgeToTheoEnabled,
                CancelWithTheoEdge = BasketSettings.CancelWithTheoEdge,
                CancelWithEdgeToAdjTheoEnabled = BasketSettings.CancelWithEdgeToAdjTheoEnabled,
                CancelWithAdjTheoEdge = BasketSettings.CancelWithAdjTheoEdge,
                CancelWithChangeInUnderlyingPxEnabled = BasketSettings.CancelWithUnderlyingPxEnabled,
                CancelWithUnderlyingPxThreshold = BasketSettings.CancelWithUnderlyingPx,
                CancelWithChangeInUnderlyingDeltaPxEnabled = BasketSettings.CancelWithUnderlyingDeltaPxEnabled,
                CancelWithUnderlyingDeltaPx = BasketSettings.CancelWithUnderlyingDeltaPx,
                CancelWithEdgeToMidEnabled = BasketSettings.CancelWithEdgeToMidEnabled,
                CancelWithMidEdge = BasketSettings.CancelWithMidEdge,
                CancelWithChangeInWidthEnabled = BasketSettings.CancelWithWidthEnabled,
                CancelWithWidthThreshold = BasketSettings.CancelWithWidthThreshold,
                CancelWithMaxWidthEnabled = BasketSettings.MaxWidthCheckEnabled,
                CancelWithMaxWidthThreshold = BasketSettings.MaxWidthCheckPx,
                MinEdgeToTheoCheckEnabled = BasketSettings.MinTheoEdgeCheckEnabled,
                MinEdgeToTheo = BasketSettings.MinTheoEdgeCheckEdge,
                MinEdgeToHwTheoCheckEnabled = BasketSettings.MinHwTheoEdgeCheckEnabled,
                MinEdgeToHwTheo = BasketSettings.MinHwTheoEdgeCheckEdge,
                MinEdgeToV0TheoCheckEnabled = BasketSettings.MinV0TheoEdgeCheckEnabled,
                MinEdgeToV0Theo = BasketSettings.MinV0TheoEdgeCheckEdge,
                MinEdgeToMidCheckEnabled = BasketSettings.MinMidEdgeCheckEnabled,
                MinEdgeToMid = BasketSettings.MinMidEdgeCheckEdge,
                MinEdgeToEmaCheckEnabled = BasketSettings.MinEmaEdgeCheckEnabled,
                MinEdgeToEma = BasketSettings.MinEmaEdgeCheckEdge,
                MinEdgeToMarketCheckEnabled = BasketSettings.MinEdgeToMarketCheckEnabled,
                MinEdgeToMarket = BasketSettings.MinEdgeToMarketCheckEdge,
                MinBidPercentCheckEnabled = BasketSettings.MinPercentBidCheckEnabled,
                MaxBidPercentCheckEnabled = BasketSettings.MaxPercentBidCheckEnabled,
                MaxBidPercent = BasketSettings.MaxPercentBidCheckEdge,
                MaxDigBidPercentCheckEnabled = BasketSettings.MaxDigPercentBidCheckEnabled,
                MaxDigBidPercent = BasketSettings.MaxDigPercentBidCheckEdge,
                MinBidPercent = BasketSettings.MinPercentBidCheckEdge,
                MinBidCheckEnabled = BasketSettings.MinBidCheckEnabled,
                MinBidCheckBidValue = BasketSettings.MinBidCheckBidValue,
                MinBidAskSizeCheckEnabled = BasketSettings.MinBidAskSizeCheckEnabled,
                MinBidAskSize = BasketSettings.MinBidAskSize,
                MinEmaWidthPercentEdgeToTheoCheckEnabled = BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled,
                MinEmaWidthPercentEdgeToTheoCheckEdge = BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge,
                MinTheoCheckEnabled = BasketSettings.MinTheoCheckEnabled,
                MinTheoCheckTheoValue = BasketSettings.MinTheoCheckTheoValue,

                CheckForRecentAttempt = BasketSettings.CheckForRecentAttempt,
                CheckForRecentAttemptTimespan = BasketSettings.CheckForRecentAttemptTimespan,
                CheckForRecentFill = BasketSettings.CheckForRecentFill,
                CheckForRecentFillTimespan = BasketSettings.CheckForRecentFillTimespan,

                MinSpxAuction = OrderTicket.SPX_AUCTION,
                MinSpxSpreadAuction = OrderTicket.SPX_SPREAD_AUCTION,

                MinSingleLegAuction = OrderTicket.SINGLE_LEG_AUCTION,
                MinSpreadAuction = OrderTicket.SPREAD_AUCTION,

                BestOfAdjTheoEnabled = BasketSettings.BestOfAdjTheoEnabled,
                BestOfAdjTheoEdge = BasketSettings.BestOfAdjTheoEdge,
                BestOfAdjTheoModel = (int)BasketSettings.BestOfAdjTheoModel,
                BestOfHwTheoEnabled = BasketSettings.BestOfHwTheoEnabled,
                BestOfHwTheoEdge = BasketSettings.BestOfHwTheoEdge,
                BestOfV0TheoEnabled = BasketSettings.BestOfV0TheoEnabled,
                BestOfV0TheoEdge = BasketSettings.BestOfV0TheoEdge,
                BestOfMidEnabled = BasketSettings.BestOfMidEnabled,
                BestOfMidEdge = BasketSettings.BestOfMidEdge,
                BestOfEmaEnabled = BasketSettings.BestOfEmaEnabled,
                BestOfEmaEdge = BasketSettings.BestOfEmaEdge,
                BestOfBidPercentEnabled = BasketSettings.BestOfBidPercentEnabled,
                BestOfBidPercentEdge = BasketSettings.BestOfBidPercentEdge,
                BestOfDigBidPercentEnabled = BasketSettings.BestOfDigBidPercentEnabled,
                BestOfDigBidPercentEdge = BasketSettings.BestOfDigBidPercentEdge,

                Venue = GetVenue(),
            };

            ApplyAutoPermSettings(config);
            TryUpdateAutomationConfig(config.DefaultAutomationConfig, automationConfig);
            UpdateSmartRoutes(config);

            if (UnderlyingToAutomationConfigModelLookup != null)
            {
                foreach (var kvp in UnderlyingToAutomationConfigModelLookup)
                {
                    var model = new AutomationConfig
                    {
                        ConfigKey = new ConfigKey()
                        {
                            Underlying = kvp.Key.Item1?.Replace(".", "")?.Trim()?.ToUpper(),
                            Increment = kvp.Key.Item2,
                        }
                    };
                    if (TryUpdateAutomationConfig(model, kvp.Value))
                    {
                        config.UnderlyingToAutomationConfigs.Add(model);
                    }
                }
            }

            return config;
        }

        private void ApplyAutoPermSettings(AutoTraderConfig config)
        {
            if (BasketSettings == null)
            {
                return;
            }

            config.AutoPermEnabled = BasketSettings.AutoPermEnabled;
            config.AutoPermMinEdge = BasketSettings.AutoPermMinEdge;
            config.AutoPermOrderCount = BasketSettings.AutoPermOrderCount;
            config.AutoPermMaxGeneration = BasketSettings.AutoPermMaxGeneration;
            config.AutoPermSubmissionStyle = BasketSettings.AutoPermSubmissionStyle;
            config.AutoPermOrderInitialSize = BasketSettings.AutoPermOrderInitialSize;
        }

        private bool TryUpdateAutomationConfig(AutomationConfig automationConfigModel, AutomationConfigModel automationConfig)
        {
            try
            {
                if (automationConfigModel == null || automationConfig == null)
                {
                    return false;
                }
                automationConfigModel.PxCrossOption = BasketSettings.PxCrossOption;
                automationConfigModel.LoopingEnabled = automationConfig.LoopingEnabled;
                automationConfigModel.OpenRoute = ApplyBrokerPrefix(automationConfig.LooperOpenRoute);
                automationConfigModel.CloseRoute = ApplyBrokerPrefix(automationConfig.LooperCloseRoute);
                automationConfigModel.OpenRouteSingleLeg = ApplyBrokerPrefix(automationConfig.LooperOpenRouteSingleLeg);
                automationConfigModel.CloseRouteSingleLeg = ApplyBrokerPrefix(automationConfig.LooperCloseRouteSingleLeg);
                automationConfigModel.OpenRouteSize = ApplyBrokerPrefix(automationConfig.LooperOpenRouteSize);
                automationConfigModel.CloseRouteSize = ApplyBrokerPrefix(automationConfig.LooperCloseRouteSize);
                automationConfigModel.OpenRouteSingleLegSize = ApplyBrokerPrefix(automationConfig.LooperOpenRouteSingleLegSize);
                automationConfigModel.CloseRouteSingleLegSize = ApplyBrokerPrefix(automationConfig.LooperCloseRouteSingleLegSize);
                automationConfigModel.CloseEdgeType = automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Static ? SelectionType.Static : SelectionType.Dynamic;
                automationConfigModel.StaticCloseEdge = automationConfig.ContraFishEdge;
                automationConfigModel.StaticMinLoopEdge = automationConfig.LoopMinEdgeUsePercentage ? Math.Max(automationConfig.LoopMinEdgePercentage * automationConfig.ContraFishEdge, 0) : automationConfig.LoopMinEdge;
                automationConfigModel.StaticMaxLoss = automationConfig.LoopMaxLoss;
                automationConfigModel.DynamicCloseEdge = automationConfig.DynamicEdgeModel?.GetConfig();

                automationConfigModel.LooperDynamicRouting = automationConfig.LooperDynamicRouting;
                automationConfigModel.AttemptIncrementUsingDynamicRoute = automationConfig.AttemptIncrementUsingDynamicRoute;
                automationConfigModel.EnableDynamicRouteForOpeningOrders = automationConfig.EnableDynamicRouteForOpeningOrders;
                automationConfigModel.EnableDynamicRouteForClosingOrders = automationConfig.EnableDynamicRouteForClosingOrders;
                automationConfigModel.ExchToRouteList = automationConfig.ExchToRouteMap?.Select(x => Tuple.Create(x.Key, ApplyBrokerPrefix(x.Value))).ToList() ?? new();

                automationConfigModel.CloseIntervalType = automationConfig.LoopIntervalType == LoopIntervalType.Static ? SelectionType.Static : SelectionType.Dynamic;
                automationConfigModel.StaticCloseInterval = automationConfig.ContraFishInterval;
                automationConfigModel.StaticCloseIntervalMax = automationConfig.ContraFishIntervalMax;
                automationConfigModel.StaticLoopInterval = automationConfig.LoopInterval;
                automationConfigModel.StaticLoopIntervalMax = automationConfig.LoopIntervalMax;
                automationConfigModel.DynamicCloseInterval = automationConfig.DynamicIntervalModel?.GetConfig();

                automationConfigModel.IncrementType = automationConfig.LoopIncrementType == LoopIncrementType.Static ? SelectionType.Static : SelectionType.Dynamic;
                automationConfigModel.StaticIncrement = automationConfig.ContraFishPriceIncrement;
                automationConfigModel.DynamicIncrement = automationConfig.LoopIncrementConfigModel?.DynamicIncrementConfigs?.Select(x => x.GetConfig()).ToList();

                automationConfigModel.SizeUpType = automationConfig.LoopSizeupType switch
                {
                    LoopSizeupType.Static => SelectionType.Static,
                    LoopSizeupType.Dynamic => SelectionType.Dynamic,
                    _ => SelectionType.Off,
                };

                automationConfigModel.StaticSizeUpLoopCountBeforeSizeup = automationConfig.LoopCountBeforeSizeup;
                automationConfigModel.StaticSizeUp = automationConfig.LoopSizeupQty;
                automationConfigModel.DynamicSizeUp = automationConfig.SizeupConfig?.GetConfig();

                automationConfigModel.AutoAggressorEnabled = automationConfig.AutoAggressorEnabled;
                automationConfigModel.AutoAggressorMode = automationConfig.AutoAggressorMode;
                automationConfigModel.AutoAggressorEdgeTightenMode = automationConfig.AutoAggressorEdgeTightenMode;
                automationConfigModel.AutoAggressorEdgeTightenPercentage = automationConfig.AutoAggressorEdgeTightenPercentage;

                automationConfigModel.ScratchOnLowDeltaSize = automationConfig.ScratchOnLowDeltaSize;
                automationConfigModel.ScratchOnLowDeltaMax = automationConfig.ScratchOnLowDeltaMax;
                automationConfigModel.ScratchOnLowDeltaMaxLoss = automationConfig.ScratchOnLowDeltaMaxLoss;
                automationConfigModel.ScratchOnLowDeltaMinSize = automationConfig.ScratchOnLowDeltaMinSize;

                automationConfigModel.FreeLookRequireMinFillTime = automationConfig.FreeLookRequireMinFillTime;
                automationConfigModel.FreeLookMinFillTime = automationConfig.FreeLookMinFillTime;

                automationConfigModel.FreeLookOnLosers = automationConfig.FreeLookOnLosers;
                automationConfigModel.FreeLookOnLosersMax = automationConfig.FreeLookOnLosersMax;

                automationConfigModel.FreeLookOnAll = automationConfig.LoopFreeLookOnAll;
                automationConfigModel.FreeLookAfterLastAttempt = automationConfig.LoopFreeLook;
                automationConfigModel.FreeWhenGettingCloseEdge = automationConfig.FreeLookWhenGettingCloseEdge;
                automationConfigModel.FreeLookBackUpIncrement = automationConfig.FreeLookOnAllIncrement;
                automationConfigModel.FreeLookOnAllWalkBackIncrement = automationConfig.FreeLookOnAllWalkBackIncrement;

                automationConfigModel.LoopFreeLookOnAllUsingTicks = automationConfig.LoopFreeLookOnAllUsingTicks;
                automationConfigModel.FreeLookOnAllIncrementTicks = automationConfig.FreeLookOnAllIncrementTicks;
                automationConfigModel.FreeLookOnAllWalkBackIncrementTicks = automationConfig.FreeLookOnAllWalkBackIncrementTicks;

                automationConfigModel.LoopFreeLookOnNickelNames = automationConfig.LoopFreeLookOnNickelNames;
                automationConfigModel.LoopFreeLookOnNickelNamesIncrement = automationConfig.LoopFreeLookOnNickelNamesIncrement;
                automationConfigModel.LoopFreeLookOnNickelNamesRoute = ApplyBrokerPrefix(automationConfig.LoopFreeLookOnNickelNamesRoute);
                automationConfigModel.LoopFreeLookOnDimeNames = automationConfig.LoopFreeLookOnDimeNames;
                automationConfigModel.LoopFreeLookOnDimeNamesIncrement = automationConfig.LoopFreeLookOnDimeNamesIncrement;
                automationConfigModel.LoopFreeLookOnDimeNamesRoute = ApplyBrokerPrefix(automationConfig.LoopFreeLookOnDimeNamesRoute);

                automationConfigModel.MaintainLastEdge = automationConfig.MaintainLastEdge;
                automationConfigModel.AttemptResubmitCount = automationConfig.AttemptResubmit;
                automationConfigModel.LastFillResubmitCount = automationConfig.LoopResubmit;
                automationConfigModel.MaxNumberOfLoops = automationConfig.MaxLoopCount;
                automationConfigModel.PartialFillPercentage = automationConfig.AutomationRequiredPartialFillPercentage;
                automationConfigModel.PartialFillResubmit = automationConfig.AutomationPartialResubmitCount;
                automationConfigModel.LoopPricingMode = automationConfig.LoopPricingMode;
                automationConfigModel.ClosePxCrossOption = automationConfig.AdjustClosingPriceToMarket ? PxCrossOption.SmartAdjust : PxCrossOption.Ignore;
                automationConfigModel.AdjustClosingPriceToMarketWinnersOnly = automationConfig.AdjustClosingPriceToMarketWinnersOnly;

                automationConfigModel.AutoHedgeOnClose = automationConfig.AutoHedgeOnClose;
                automationConfigModel.AutoHedgeOnCloseSizeOnly = automationConfig.AutoHedgeOnCloseSizeOnly;
                automationConfigModel.MinHedgeHouseEdge = automationConfig.MinHedgeHouseEdge;
                automationConfigModel.AutoHedgeOnFailure = automationConfig.AutoHedgeOnFailure;
                automationConfigModel.AutoHedgePartial = automationConfig.AutoHedgePartial;

                automationConfigModel.AutoLegEnabled = automationConfig.AutoLegEnabled;
                automationConfigModel.AutoLegMaxWidth = automationConfig.AutoLegMaxWidth;
                automationConfigModel.AutoLegCloseEdge = automationConfig.AutoLegCloseEdge;
                automationConfigModel.AutoLegMaxLoss = automationConfig.AutoLegMaxLoss;
                automationConfigModel.AutoLegCloseIncrement = automationConfig.AutoLegCloseIncrement;
                automationConfigModel.AutoLegCloseRoute = ApplyBrokerPrefix(automationConfig.AutoLegCloseRoute);
                automationConfigModel.AutoLegRestTime = automationConfig.AutoLegRestTime;

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryUpdateAutomationConfig));
                return false;
            }
        }

        private string ApplyBrokerPrefix(string route)
        {
            return OmsCore.OrderClient?.RouteLookup?.ApplyBrokerPrefix(route, EffectiveBroker) ?? route;
        }

        private static void UpdateSmartRoutes(AutoTraderConfig autoTraderConfig)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(autoTraderConfig.DefaultAutomationConfig.OpenRoute) && OmsCore.Config.SmartRoutes.TryGetValue(autoTraderConfig.DefaultAutomationConfig.OpenRoute, out Dictionary<int, Tuple<string, double>> openRouteMap))
                {
                    autoTraderConfig.OpenRouteSmartMap = openRouteMap.Select(x => x.Value).ToList();
                }
                if (!string.IsNullOrWhiteSpace(autoTraderConfig.DefaultAutomationConfig.CloseRoute) && OmsCore.Config.SmartRoutes.TryGetValue(autoTraderConfig.DefaultAutomationConfig.CloseRoute, out Dictionary<int, Tuple<string, double>> closeRouteMap))
                {
                    autoTraderConfig.CloseRouteSmartMap = closeRouteMap.Select(x => x.Value).ToList();
                }
                if (!string.IsNullOrWhiteSpace(autoTraderConfig.DefaultAutomationConfig.OpenRouteSingleLeg) && OmsCore.Config.SmartRoutes.TryGetValue(autoTraderConfig.DefaultAutomationConfig.OpenRouteSingleLeg, out Dictionary<int, Tuple<string, double>> openRouteSingleLegMap))
                {
                    autoTraderConfig.OpenRouteSingleLegSmartMap = openRouteSingleLegMap.Select(x => x.Value).ToList();
                }
                if (!string.IsNullOrWhiteSpace(autoTraderConfig.DefaultAutomationConfig.CloseRouteSingleLeg) && OmsCore.Config.SmartRoutes.TryGetValue(autoTraderConfig.DefaultAutomationConfig.CloseRouteSingleLeg, out Dictionary<int, Tuple<string, double>> closeRouteSingleLegMap))
                {
                    autoTraderConfig.CloseRouteSingleLegSmartMap = closeRouteSingleLegMap.Select(x => x.Value).ToList();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateSmartRoutes));
            }
        }

        [Command]
        public void ShowLoopAdvancedConfigsCommand()
        {
            try
            {
                LoopAdvancedConfigsView view = new();
                if (view.DataContext is LoopAdvancedConfigsViewModel viewModel)
                {
                    viewModel.BasketTrader = this;
                    viewModel.BasketSettings = BasketSettings;
                    view.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopAdvancedConfigsCommand));
            }
        }

        public List<AutomationConfigModel> GetAutomationConfigs(string underlying = null, double increment = 0)
        {
            List<AutomationConfigModel> configs = UnderlyingToAutomationConfigModelLookup?.Values?.ToList() ?? [];
            configs.Add(BasketSettings.AutomationConfig);
            return configs;
        }

        public AutomationConfigModel GetAutomationConfig(string underlying = null, double increment = 0)
        {
            if (UnderlyingToAutomationConfigModelLookup != null)
            {
                if (!string.IsNullOrWhiteSpace(underlying))
                {
                    underlying = underlying.Replace(".", "").Trim().ToUpper();
                    increment = Math.Abs(Math.Round(increment, 2));
                    Tuple<string, double> key = Tuple.Create(underlying, increment);
                    if (UnderlyingToAutomationConfigModelLookup.TryGetValue(key, out AutomationConfigModel config) && config != null)
                    {
                        return config;
                    }
                    key = Tuple.Create(underlying, 0.0);
                    if (UnderlyingToAutomationConfigModelLookup.TryGetValue(key, out config) && config != null)
                    {
                        return config;
                    }
                }
            }
            return BasketSettings?.AutomationConfig;
        }

        private void LoadQuickAccessButtons()
        {
            BasketLayoutQuickAccess.Clear();
            foreach (Tuple<int, string, Comms.Models.Data.Oms.Config.ConfigSave> layoutSave in OmsCore.Config.SavedBasketQuickAccessLayouts.OrderBy(x => x.Item1))
            {
                if (!string.IsNullOrWhiteSpace(layoutSave.Item3.ConfigJson))
                {
                    BasketLayoutQuickAccessModel model = new()
                    {
                        Index = layoutSave.Item1,
                        Title = layoutSave.Item2,
                        Layout = layoutSave.Item3,
                    };

                    if (model.IsValid())
                    {
                        BasketLayoutQuickAccess.Add(model);
                    }
                }
            }

            OmsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTraderLayout)
                .ContinueWith(t =>
                {
                    if (t.Result != null)
                    {
                        var userConfigs = t.Result.Where(x => string.Equals(OmsCore.User.Username, x.Username, StringComparison.OrdinalIgnoreCase)).ToList();

                        Dispatcher.BeginInvoke(() =>
                        {
                            UserConfigs.Clear();
                            foreach (var config in userConfigs)
                            {
                                UserConfigs.Add(config);
                            }
                        });
                    }
                });
        }

        private void LoadAutomationConfigs(string configDirectory)
        {
            try
            {
                ObservableCollection<AutomationConfigModel> automationConfigModels = null;
                string automationConfigFile = Path.Combine(configDirectory, $"AutomationConfigs.json");
                if (File.Exists(automationConfigFile))
                {
                    string content = File.ReadAllText(automationConfigFile);
                    List<AutomationConfigModel> automationConfigModelsList = DeserializeAutomationConfig(content);
                    if (automationConfigModelsList != null)
                    {
                        automationConfigModels = automationConfigModelsList.ToObservableCollection();
                    }
                }
                automationConfigModels ??= new ObservableCollection<AutomationConfigModel>();
                AutomationConfigModel automationConfig = GetAutomationConfig();
                if (automationConfig != null)
                {
                    if (string.IsNullOrWhiteSpace(automationConfig.Title))
                    {
                        automationConfig.Title = InstanceId[..Math.Min(10, InstanceId.Length)];
                    }

                    if (!automationConfigModels.Contains(automationConfig))
                    {
                        automationConfigModels.Add(automationConfig);
                    }
                }
                AutomationConfigModels = automationConfigModels;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadAutomationConfigs));
            }
        }

        private static List<AutomationConfigModel> DeserializeAutomationConfig(string content)
        {
            try
            {
                List<AutomationConfigModel> automationConfigModelsList = JsonConvert.DeserializeObject<List<AutomationConfigModel>>(content)?.Distinct()?.ToList();
                return automationConfigModelsList;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadAutomationConfigs));
                return null;
            }
        }

        public async Task<bool> GetVerificationAsync(string message, string title = "Verification")
        {
            bool ok = false;
            await Dispatcher?.BeginInvoke(new Action(() =>
            {
                var result = VerificationService.GetRiskVerification(message, title);
                ok = result == RiskWarningMessageResponse.Proceed;
            }));
            return ok;
        }

        private void AddBasketGroup()
        {
            try
            {
                SelectedBasketGroup = BasketGroupManagerModel.GetNextGroup(this);
                BasketGroups.Add(SelectedBasketGroup);
                BasketGroupManagerModel.AddToGroup(SelectedBasketGroup, this);

                foreach (BasketGroupModel item in BasketGroupManagerModel.GetAllGroups)
                {
                    if (item.Uid != SelectedBasketGroup.Uid)
                    {
                        BasketGroups.Add(item);
                    }
                }

                BasketGroupManagerModel.BasketGroupAddedEvent += BasketTraderViewModel_BasketGroupAddedEvent;
                BasketGroupManagerModel.BasketGroupRemovedEvent += BasketTraderViewModel_BasketGroupRemovedEvent;
            }
            catch (Exception) { }
        }

        private void BasketTraderViewModel_BasketGroupAddedEvent(BasketGroupModel basketGroupModel)
        {
            Dispatcher?.BeginInvoke(new Action(() =>
            {
                try
                {
                    BasketGroups.Add(new BasketGroupModel(basketGroupModel));
                }
                catch (Exception) { }
            }));
        }

        private void BasketTraderViewModel_BasketGroupRemovedEvent(BasketGroupModel basketGroupModel)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (BasketGroupModel group in BasketGroups.ToList())
                {
                    try
                    {
                        if (group.Uid == basketGroupModel.Uid)
                        {
                            BasketGroups.Remove(group);
                        }
                    }
                    catch (Exception) { }
                }
            }));
        }

        private void StartUiUpdateTimer()
        {
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(OmsCore.Config.BasketUiUpdateInterval),
            };
            _uiUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _uiUpdateTimer.Start();
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e) => UpdateUiProperties();

        private void UpdateUiProperties()
        {
            try
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    item.UpdateUiProperties();
                }
                UpdateTimerProperty();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateUiProperties));
            }
        }

        private void UpdateTimerProperty()
        {
            try
            {
                if (_timerUpdated)
                {
                    _timerUpdated = false;
                    RaisePropertyChanged(nameof(ResubmitCountDown));
                    RaisePropertyChanged(nameof(ModifyCountDown));
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateUiProperties));
            }
            finally
            {
                _timerUpdated = false;
            }
        }

        private void StartGeneralBasketUpdateTimer()
        {
            _startTime = DateTime.Now;
            _basketTitleBarUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _basketTitleBarUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _basketTitleBarUpdateTimer.Tick += BasketGeneralUpdateTimer_Tick;
            _basketTitleBarUpdateTimer.Start();
        }

        private void BasketGeneralUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                DateTime now = DateTime.Now;
                if (ResubmitOnTimer)
                {
                    DateTime nowEastern = now.ToEastern();
                    if (now.Date.Day != _startTime.Date.Day ||
                        nowEastern.TimeOfDay >= TimeHelper.MarketCloseEastern.TimeOfDay)
                    {
                        ResubmitOnTimer = false;
                    }
                }

                if (BasketItems.Any())
                {
                    if ((DateTime.Now - _lastBasketHealthCheck).TotalSeconds > 2)
                    {
                        _lastBasketHealthCheck = DateTime.Now;
                        for (var index = BasketItems.Count - 1; index >= 0; index--)
                        {
                            if (IsDisposed)
                            {
                                break;
                            }
                            var item = BasketItems[index];
                            item.CheckOnSubscriptions();
                        }
                    }

                    if (RemoveAllOnInterval)
                    {
                        if ((DateTime.Now - _lastRemoveAllRunTime).TotalSeconds > REMOVE_ALL_INTERVAL)
                        {
                            _lastRemoveAllRunTime = DateTime.Now;
                            if (!SubmitAllRunning && !ModifyOnTimer && !ResubmitOnTimer)
                            {
                                ObservableCollection<BasketTraderItemModel> basketItems = BasketItems;
                                if (basketItems != null && basketItems.Count > 0)
                                {
                                    HashSet<BasketTraderItemModel> itemsToRemove = new();
                                    int count = basketItems.Count;
                                    for (int i = 0; i < count; i++)
                                    {
                                        try
                                        {
                                            if (SubmitAllRunning)
                                            {
                                                itemsToRemove.Clear();
                                                break;
                                            }

                                            BasketTraderItemModel item = basketItems[i];
                                            if ((DateTime.Now - item.CreationTime).TotalSeconds > OmsCore.Config.BasketItemsTimeToLiveForAutoClear)
                                            {
                                                if (item.IsActive || item.NagEnabled)
                                                {
                                                    continue;
                                                }

                                                itemsToRemove.Add(item);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _log.Error(ex, nameof(BasketGeneralUpdateTimer_Tick));
                                            break;
                                        }
                                    }
                                    if (itemsToRemove.Count > 0)
                                    {
                                        _ = RemoveItems(withUndoPrompt: false, itemsToRemove, checkForResting: true);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _lastRemoveAllRunTime = DateTime.Now;
                    }
                }

                if (RecalculatePriceOnInterval)
                {
                    if ((DateTime.Now - _lastPriceSetAllRunTime).TotalSeconds > OmsCore.Config.RecalculateBasketPriceInterval)
                    {
                        _lastPriceSetAllRunTime = DateTime.Now;
                        foreach (BasketTraderItemModel basketItem in BasketItems)
                        {
                            _ = basketItem.SetEdgeAsync();
                        }
                    }
                }
                else
                {
                    _lastPriceSetAllRunTime = DateTime.Now;
                }

                TimeSpan delta = DateTime.Now - _startTime;
                UpTime = Math.Truncate(delta.TotalHours).ToString("00") + ":" + delta.Minutes.ToString("00") + ":" + delta.Seconds.ToString("00");
                TimeSpan activity = DateTime.Now - _lastActivityTime;
                bool resetVolume = false;
                if (activity.TotalMinutes > 1)
                {
                    _lastActivityTime = DateTime.Now;
                    resetVolume = ResetVolumeChange;
                }
                BasketItemPnlUpdatedEvent(resetVolume);
                UpdateBasketManager();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BasketGeneralUpdateTimer_Tick));
            }
        }

        internal void ShowMessageFromItem(string message, string title, bool canBeSilenced)
        {
            if (canBeSilenced)
            {
                if (_message != message)
                {
                    ShowMessage = true;
                    Message = message;
                    _messageClearTimer.Start();
                }
            }
            else
            {
                Dispatcher?.BeginInvoke(() => VerificationService.ShowMessage(message, title, showCancelAll: false));
            }
        }

        public bool GetIsPartOfVol()
        {
            try
            {
                return _volTradersManager.BasketIsPartOfVolTrader(this);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void RemoveFromBasketGroupManager()
        {
            try
            {
                BasketGroupManagerModel.RemoveFromBasketGroups(this);
                BasketGroupManagerModel.BasketGroupAddedEvent -= BasketTraderViewModel_BasketGroupAddedEvent;
                BasketGroupManagerModel.BasketGroupRemovedEvent -= BasketTraderViewModel_BasketGroupRemovedEvent;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveFromBasketGroupManager));
            }
        }

        [Command]
        public void ActiveStateChangedCommand(BasketTraderItemModel basketTraderItem)
        {
            try
            {
                if (_runningActivator)
                {
                    return;
                }
                if (basketTraderItem != null)
                {
                    _runningActivator = true;
                    if (SelectedItems != null && SelectedItems.Count > 0)
                    {
                        IEnumerable<object> items = SelectedItems.Where(x => x != basketTraderItem);
                        foreach (object item in items)
                        {
                            if (item is BasketTraderItemModel model)
                            {
                                model.Active = basketTraderItem.Active;
                            }
                        }
                    }
                }
            }
            finally
            {
                _runningActivator = false;
            }
        }

        [Command]
        public void SelectedBasketGroupChangedCommand()
        {
            if (SelectedBasketGroup != null)
            {
                UnsubscribePnlAsync();
                BasketGroupManagerModel.AddToGroup(SelectedBasketGroup, this);
                BasketSettings.Uid = SelectedBasketGroup.Uid;
                SubscribePnl();
            }
        }

        [Command]
        public async void LoadSavedConfig(Comms.Models.Data.Oms.Config.ConfigSave id)
        {
            try
            {
                if (id == null)
                {
                    return;
                }
                Comms.Models.Data.Oms.Config.ConfigSave configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(id.Id);
                if (configSave != null)
                {
                    var configSaveTitle = ModuleWindow.CleanTitle(configSave.Title);
                    ModuleTitle = configSaveTitle + " - " + MODULE_TITLE;
                    BasketTraderConfig config = await Task.Run(() => JsonConvert.DeserializeObject<BasketTraderConfig>(configSave.ConfigJson));
                    if (config != null)
                    {
                        LoadConfig(config);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadSavedConfig));
            }
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = BasketType == BasketType.BasketTrader ? Module.BasketTrader : Module.LockTrader;

                viewModel.Config = GetConfigSerialized(true);

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void Save()
        {
            try
            {
                if (Loaded)
                {
                    WriteToFile();
                }
                else
                {
                    SaveAs();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Save));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        [Command]
        public void SaveAs()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "json";
                SaveFileDialogService.DefaultFileName = $"{OmsCore.User.Username} Basket Trader - {DateTime.Now:MM-dd-yyyy hh.mm}";
                SaveFileDialogService.Filter = "Json|*.JSON";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    FileName = SaveFileDialogService.SafeFileName();
                    FilePath = SaveFileDialogService.GetFullFileName();
                    WriteToFile();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAs));
                Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK));
            }
        }

        [Command]
        public void Load()
        {
            try
            {
                OpenFileDialogService.Filter = "JSON files|*.JSON|Basket exports|*.JSON";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    FileName = file.Name;
                    FilePath = file.GetFullName();
                    LoadFromFileAsync(FilePath);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        [Command]
        public void LoadExcel()
        {
            try
            {
                OpenFileDialogService.Filter = "Excel files|*.XLS*|CSV|*.CSV";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    LoadFromFileAsync(file.GetFullName());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        [Command]
        public void SetInstanceTitleToWindowCommand()
        {
            ModuleTitle = Name;
        }

        [Command]
        public void ExportBasketSettingsCommand()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "json";
                SaveFileDialogService.DefaultFileName = $"{OmsCore.User.Username} Basket Settings - {DateTime.Now:MM-dd-yyyy hh.mm}";
                SaveFileDialogService.Filter = "Json|*.JSON";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    string filePath = SaveFileDialogService.GetFullFileName();
                    string config = GetConfigSerialized(withItems: false, onlyLayout: true);
                    File.WriteAllText(filePath, config);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportBasketSettingsCommand));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        [Command]
        public void ImportBasketSettingsCommand()
        {
            try
            {
                OpenFileDialogService.Filter = "Json|*.JSON";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    string filePath = file.GetFullName();
                    if (filePath != null)
                    {
                        string extention = Path.GetExtension(filePath);
                        if (string.Equals(extention, ".json", StringComparison.InvariantCultureIgnoreCase))
                        {
                            string fileContent = File.ReadAllText(filePath);
                            LoadConfigFromJson(fileContent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ImportBasketSettingsCommand));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        [Command]
        public (BasketTraderView View, BasketTraderViewModel ViewModel) Clone(object parameter)
        {
            try
            {
                if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                {
                    if (viewModel.IsReady)
                    {
                        Task.Run(() => OnReady(viewModel));
                    }
                    else
                    {
                        viewModel.Ready += OnReady;
                    }

                    void OnReady(IModuleViewModel module)
                    {
                        viewModel.Ready -= OnReady;
                        if (parameter != null)
                        {
                            switch (parameter)
                            {
                                case Window cloneSourceWindow:
                                    {
                                        Point location = cloneSourceWindow.PointToScreen(new Point(0, 0));
                                        view.WindowStartupLocation = WindowStartupLocation.Manual;
                                        view.Width = cloneSourceWindow.Width;
                                        view.Height = cloneSourceWindow.Height;
                                        view.Left = location.X + 300;
                                        view.Top = location.Y;
                                        break;
                                    }
                                case string config:
                                    view.LoadConfigFromJsonAsync(config, false);
                                    break;
                            }
                        }
                        viewModel.LoadFromTemplateAsync(this);
                    }
                    return (view, viewModel);
                }
                return (null, null);

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
                return (null, null);
            }
        }

        [Command]
        public void Clear()
        {
            _submitIndex = 0;
            RemoveSelected(BasketItems);
        }

        [Command]
        public void EnableStockTieCommand()
        {
            foreach (var basketItem in BasketItems)
            {
                _ = basketItem.SetupStockTieAsync();
            }
        }

        [Command]
        public void DisableStockTieCommand()
        {
            foreach (var basketItem in BasketItems)
            {
                basketItem.RemoveStockTieAsync();
            }
        }

        [Command]
        public async void EnableCheapoCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            await CleanInvalidRows();
            var newItems = new List<BasketTraderItemModel>();
            foreach (var basketItem in BasketItems.ToList())
            {
                if (!basketItem.ContainsCheapo())
                {
                    if (BasketSettings.CheaposGeneratedPerOrder > 1)
                    {
                        for (int i = 1; i < BasketSettings.CheaposGeneratedPerOrder; i++)
                        {
                            var clone = await GetBasketItemCloneAsync(basketItem, false, false, true);
                            await clone.AddCheapo(index: i);
                            newItems.Add(clone);
                        }
                    }
                    basketItem.AddCheapo(index: 0);
                }
            }
            if (newItems.Count > 0)
            {
                await AddMultipleToBasketAsync(newItems);
            }
            await CleanInvalidRows();
            SetEdgeOnAll();
        }

        [Command]
        public async void DisableCheapoCommand()
        {
            foreach (var basketItem in BasketItems.ToList())
            {
                if (basketItem.ContainsCheapo())
                {
                    basketItem.RemoveCheapo();
                }
            }
            await CleanInvalidRows();
            SetEdgeOnAll();
        }

        private void SetEdgeOnAll()
        {
            for (var index = BasketItems.Count - 1; index >= 0; index--)
            {
                var item = BasketItems[index];
                SetEdgeAsync(item);
            }
        }

        [Command]
        public void AddColumn()
        {
            AddColumnView addColumnView = new();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent += OnAddColumnEvent;
            addColumnView.ShowDialog();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent -= OnAddColumnEvent;
        }

        private void OnAddColumnEvent(CustomColumnTemplateModel colTemplate)
        {
            LoadCustomColumnService.AddCustomColumn(colTemplate);
        }

        [Command]
        public void ModuleChooser()
        {
            BasketModuleChooserView view = new()
            {
                DataContext = this
            };
            view.ShowDialog();
        }

        [Command]
        public void ShowNagbotIntervalConfigPanelCommand()
        {
            try
            {
                NagbotIntervalManagementView view = new();
                if (view.DataContext is NagbotIntervalManagementViewModel viewModel)
                {
                    viewModel.ParentBasket = this;
                    view.Show();
                }
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowNagbotIntervalConfigPanelCommand));
            }
        }

        [Command]
        public void ShowSizeupConfigPanelCommand()
        {
            try
            {
                LoopSizeupManagementView view = new();
                if (view.DataContext is LoopSizeupManagementViewModel viewModel)
                {
                    viewModel.AutomationTrader = this;
                    view.Show();
                }
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowSizeupConfigPanelCommand));
            }
        }

        [Command]
        public void ShowAutoPermConfigPanelCommand()
        {
            try
            {
                var automationConfig = GetAutomationConfig();
                if (automationConfig.AutoPermConfigModel != null &&
                    automationConfig.AutoPermConfigModelId == 0 &&
                    string.IsNullOrWhiteSpace(automationConfig.AutoPermConfigModel.Title))
                {
                    EditAutoPermConfig(automationConfig.AutoPermConfigModel);
                }
                else
                {
                    DynamicConfigManagementView view = new();
                    if (view.DataContext is DynamicConfigManagementViewModel viewModel)
                    {
                        viewModel.Parent = this;
                        viewModel.ConfigModule = Module.AutoPermConfig;
                        view.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowAutoPermConfigPanelCommand));
            }
        }

        [Command]
        public void ShowLoopIncrementConfigPanelCommand()
        {
            try
            {
                var automationConfig = GetAutomationConfig();
                if (automationConfig.LoopIncrementConfigModel != null &&
                    automationConfig.LoopIncrementConfigModelId == 0 &&
                    string.IsNullOrWhiteSpace(automationConfig.LoopIncrementConfigModel.Title))
                {
                    EditDynamicIncrementConfig(automationConfig.LoopIncrementConfigModel);
                }
                else
                {
                    DynamicConfigManagementView view = new();
                    if (view.DataContext is DynamicConfigManagementViewModel viewModel)
                    {
                        viewModel.Parent = this;
                        viewModel.ConfigModule = Module.DynamicIncrementConfigs;
                        view.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopIncrementConfigPanelCommand));
            }
        }

        [Command]
        public void ShowLoopCloseEdgeConfigPanelCommand()
        {
            try
            {
                DynamicEdgeManagementView view = new();
                if (view.DataContext is DynamicEdgeManagementViewModel viewModel)
                {
                    viewModel.ParentBasket = this;
                    void reloader(object o, EventArgs e)
                    {
                        view.Closed -= reloader;
                        ReloadDynamicEdgeConfig();
                    }
                    view.Closed += reloader;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopCloseEdgeConfigPanelCommand));
            }
        }

        [Command]
        public void ShowDynamicIntervalConfigPanelCommand()
        {
            try
            {
                DynamicIntervalManagementView view = new();
                if (view.DataContext is DynamicIntervalManagementViewModel viewModel)
                {
                    viewModel.AutomationTrader = this;
                    void reloader(object o, EventArgs e)
                    {
                        view.Closed -= reloader;
                        ReloadDynamicIntervalConfig();
                    }
                    view.Closed += reloader;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowDynamicIntervalConfigPanelCommand));
            }
        }

        [Command]
        public void ClearEdgeOverrides()
        {
            if (BasketType == BasketType.LockTrader)
            {
                return;
            }
            bool ok = false;
            Dispatcher?.Invoke(new Action(() =>
            {
                ok = MessageBoxService?.Show("Are you sure you want to clear all edge overrides?",
                                             "Verification",
                                             MessageButton.YesNo,
                                             MessageIcon.Exclamation,
                                             MessageResult.Yes) == MessageResult.Yes;
            }));
            if (ok)
            {
                foreach (BasketTraderItemModel order in BasketItems)
                {
                    order.ClearEdgeOverride();
                    _ = SetEdgeAsync(order);
                }
            }
        }

        [Command]
        public async void LoadEdgeOverrides()
        {
            if (BasketType == BasketType.LockTrader)
            {
                return;
            }
            bool ok = false;
            await Dispatcher?.BeginInvoke(new Action(() =>
            {
                ok = MessageBoxService?.Show("Are you sure you want to load edge overrides from template?",
                                             "Verification",
                                             MessageButton.YesNo,
                                             MessageIcon.Exclamation,
                                             MessageResult.Yes) == MessageResult.Yes;
            }));
            if (ok)
            {
                foreach (BasketTraderItemModel order in BasketItems)
                {
                    order.LoadEdgeOverride();
                    _ = SetEdgeAsync(order);
                }
            }
        }

        [Command]
        public void SaveAutomationConfigCommand()
        {
            SaveView view = new();
            if (view.DataContext is SaveViewModel viewModel)
            {
                viewModel.ShowDefault = true;
                viewModel.ShowGroup = false;
                viewModel.ShowLocation = false;
                AutomationConfigModel automationConfig = GetAutomationConfig();
                viewModel.Title = automationConfig.Title;
                viewModel.SetDispatcher(view.Dispatcher);

                view.ShowDialog();

                if (viewModel.Success)
                {
                    if (!string.IsNullOrWhiteSpace(viewModel.Title))
                    {
                        automationConfig.Title = viewModel.Title;
                        List<AutomationConfigModel> automationConfigModels = AutomationConfigModels.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
                        if (!automationConfigModels.Contains(automationConfig))
                        {
                            automationConfigModels.Add(automationConfig);
                        }
                        SaveConfig(automationConfigModels);
                    }
                    else
                    {
                        MessageBoxService?.ShowMessage("Title can not be empty", "Basket Automation Config");
                    }
                }
            }
        }

        [Command]
        public void ClearAutomationConfigCommand()
        {
            UnderlyingToAutomationConfigModelLookup = null;
        }

        [Command]
        public void ConfigAutomationConfigCommand()
        {
            try
            {
                List<AutomationConfigModel> automationConfigModels = AutomationConfigModels.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
                List<AutomationConfigMap> list = GetCurrentMappings();

                AutomationConfigMappingView view = new();
                if (view.DataContext is AutomationConfigMappingViewModel viewModel)
                {
                    viewModel.Configs.AddRange(list);
                    viewModel.AutomationConfigs.AddRange(automationConfigModels);
                    viewModel.ApplyConfigHandler = ApplyAutomationConfigMap;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ConfigAutomationConfigCommand));
            }
        }

        [Command]
        public void ShowMatrixSyntheticSpreadConfigPanelCommand()
        {
            try
            {
                DynamicConfigManagementView view = new();
                if (view.DataContext is DynamicConfigManagementViewModel viewModel)
                {
                    viewModel.Parent = this;
                    viewModel.ConfigModule = Module.MatrixSmartConfig;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowMatrixSyntheticSpreadConfigPanelCommand));
            }
        }

        private List<AutomationConfigMap> GetCurrentMappings()
        {
            List<AutomationConfigMap> list = new();
            if (UnderlyingToAutomationConfigModelLookup != null)
            {
                foreach (KeyValuePair<Tuple<string, double>, AutomationConfigModel> group in UnderlyingToAutomationConfigModelLookup)
                {
                    string underlying = group.Key.Item1;
                    AutomationConfigModel automationConfig = group.Value;
                    if (!string.IsNullOrWhiteSpace(underlying) && automationConfig != null)
                    {
                        AutomationConfigMap automationConfigMap = new()
                        {
                            Underlyings = underlying,
                            Increment = group.Key.Item2,
                            AutomationConfig = automationConfig,
                        };
                        list.Add(automationConfigMap);
                    }
                }
            }
            return list;
        }

        private void ApplyAutomationConfigMap(ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> config)
        {
            if (config != null)
            {
                UnderlyingToAutomationConfigModelLookup = config;
            }
        }

        [Command]
        public void DeleteConfigCommand(AutomationConfigModel automationConfigModel)
        {
            List<AutomationConfigModel> automationConfigModels = AutomationConfigModels.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
            if (automationConfigModels.Contains(automationConfigModel))
            {
                automationConfigModels.Remove(automationConfigModel);
            }
            SaveConfig(automationConfigModels);
        }

        [Command]
        public void AddLoopBlockListCommand()
        {
            BasketBlockListConfigView view = new();
            if (view.DataContext is BasketBlockListConfigViewModel viewModel)
            {
                BasketLoopBlockListModel model = new();
                BasketLoopBlockListModels.Add(model);
                viewModel.Model = model;
                viewModel.List = BasketLoopBlockListModels;
                view.ShowDialog();

                if (model.Items.Count <= 0 || string.IsNullOrWhiteSpace(model.Title))
                {
                    return;
                }
            }
        }

        [Command]
        public void ClearLoopBlockListCommand()
        {
            if (!IsDisposed && BasketSettings.BasketLoopBlockList != null)
            {
                BasketSettings.BasketLoopBlockList = null;
            }
        }

        [Command]
        public void EditLoopBlockListCommand()
        {
            BasketLoopBlockListModel basketLoopBlockList = BasketSettings.BasketLoopBlockList;
            if (basketLoopBlockList != null)
            {
                BasketBlockListConfigView view = new();
                if (view.DataContext is BasketBlockListConfigViewModel viewModel)
                {
                    viewModel.Model = basketLoopBlockList;
                    viewModel.List = BasketLoopBlockListModels;
                    view.Show();
                }
            }
        }

        [Command]
        public async void DeleteLoopBlockListCommand()
        {
            BasketLoopBlockListModel basketLoopBlockList = BasketSettings.BasketLoopBlockList;
            if (basketLoopBlockList != null)
            {
                if (await GetVerificationAsync("Are you sure you want to delete the block list " + basketLoopBlockList.Title + "?", "ZeroPlus OMS"))
                {
                    BasketLoopBlockListModels.Remove(basketLoopBlockList);
                }
            }
        }

        private void SaveConfig(List<AutomationConfigModel> automationConfigModels)
        {
            string configDirectory = OmsConfig.GetConfigDirectory();

            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }
            string automationConfigFile = Path.Combine(configDirectory, $"AutomationConfigs.json");
            string content = JsonConvert.SerializeObject(automationConfigModels);
            File.WriteAllText(automationConfigFile, content);

            ObservableCollection<AutomationConfigModel> models = automationConfigModels.ToObservableCollection();
            AutomationConfigModels = models;

            OmsCore.Config.OnChange(requiresRestart: false);
        }

        [Command]
        public void ToggleAutoCancelCommand(AutoCancel cancelMode)
        {
            switch (cancelMode)
            {
                case AutoCancel.EdgeToTheo:
                    BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled = !BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled;
                    break;
                case AutoCancel.EdgeToModelTheo:
                    BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled = !BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled;
                    break;
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithEdgeToTheoEnabled = !BasketSettings.CancelWithEdgeToTheoEnabled;
            if (BasketSettings.CancelWithEdgeToTheoEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithTheoChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithMaxSizeCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithMaxSizeEnabled = !BasketSettings.CancelWithMaxSizeEnabled;
            if (BasketSettings.CancelWithMaxSizeEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMaxSize();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToAdjTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithEdgeToAdjTheoEnabled = !BasketSettings.CancelWithEdgeToAdjTheoEnabled;
            if (BasketSettings.CancelWithEdgeToAdjTheoEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithAdjTheoChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToMid()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithEdgeToMidEnabled = !BasketSettings.CancelWithEdgeToMidEnabled;
            if (BasketSettings.CancelWithEdgeToMidEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMidChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMaxWidthCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MaxWidthCheckEnabled = !BasketSettings.MaxWidthCheckEnabled;
            if (BasketSettings.MaxWidthCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMaxWidthChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinTheoEdgeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinTheoEdgeCheckEnabled = !BasketSettings.MinTheoEdgeCheckEnabled;
            if (BasketSettings.MinTheoEdgeCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinTheoEdgeChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinHwTheoEdgeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinHwTheoEdgeCheckEnabled = !BasketSettings.MinHwTheoEdgeCheckEnabled;
            if (BasketSettings.MinHwTheoEdgeCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinHwTheoEdgeChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinV0TheoEdgeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinV0TheoEdgeCheckEnabled = !BasketSettings.MinV0TheoEdgeCheckEnabled;
            if (BasketSettings.MinV0TheoEdgeCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinV0TheoEdgeChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinMidEdgeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinMidEdgeCheckEnabled = !BasketSettings.MinMidEdgeCheckEnabled;
            if (BasketSettings.MinMidEdgeCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinMidEdgeChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinEmaEdgeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinEmaEdgeCheckEnabled = !BasketSettings.MinEmaEdgeCheckEnabled;
            if (BasketSettings.MinEmaEdgeCheckEnabled)
            {
                if (BasketSettings.SubscribeToEma)
                {
                    Task.Run(() =>
                    {
                        for (var index = BasketItems.Count - 1; index >= 0; index--)
                        {
                            var order = BasketItems[index];
                            order.SetAutoCancelWithMinEmaEdgeChange();
                        }
                    });
                }
                else
                {
                    ShowMessageFromItem("Toggling EMA Fish Loss/Auto Cancel with no subscription", "Missing Data", true);
                }
            }
        }

        [Command]
        public void ToggleMinEdgeToMarketCheckCommand()
        {
            BasketSettings.MinEdgeToMarketCheckEnabled = !BasketSettings.MinEdgeToMarketCheckEnabled;
        }

        [Command]
        public void ToggleMinEdgeToSkewMarketCheckCommand()
        {
            BasketSettings.MinEdgeToSkewMarketCheckEnabled = !BasketSettings.MinEdgeToSkewMarketCheckEnabled;
        }

        [Command]
        public void ToggleMinEdgeToSkewMarketCrossCheckCommand()
        {
            BasketSettings.MinEdgeToSkewMarketCrossCheckEnabled = !BasketSettings.MinEdgeToSkewMarketCrossCheckEnabled;
        }

        [Command]
        public void ToggleMinPercentBidCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinPercentBidCheckEnabled = !BasketSettings.MinPercentBidCheckEnabled;
            if (BasketSettings.MinPercentBidCheckEnabled)
            {
                if (BasketSettings.SubscribeToMarketData)
                {
                    Task.Run(() =>
                    {
                        for (var index = BasketItems.Count - 1; index >= 0; index--)
                        {
                            var order = BasketItems[index];
                            order.SetAutoCancelWithMinPercentBid();
                        }
                    });
                }
                else
                {
                    ShowMessageFromItem("Toggling Market Fish Loss/Auto Cancel with no subscription", "Missing Data", true);
                }
            }
        }

        [Command]
        public void ToggleMaxPercentBidCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MaxPercentBidCheckEnabled = !BasketSettings.MaxPercentBidCheckEnabled;
            if (BasketSettings.MaxPercentBidCheckEnabled)
            {
                if (BasketSettings.SubscribeToMarketData)
                {
                    Task.Run(() =>
                    {
                        for (var index = BasketItems.Count - 1; index >= 0; index--)
                        {
                            var order = BasketItems[index];
                            order.SetAutoCancelWithMaxPercentBid();
                        }
                    });
                }
                else
                {
                    ShowMessageFromItem("Toggling Market Fish Loss/Auto Cancel with no subscription", "Missing Data", true);
                }
            }
        }

        [Command]
        public void ToggleMaxDigPercentBidCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MaxDigPercentBidCheckEnabled = !BasketSettings.MaxDigPercentBidCheckEnabled;
            if (BasketSettings.MaxDigPercentBidCheckEnabled)
            {
                if (BasketSettings.SubscribeToMarketData)
                {
                    Task.Run(() =>
                    {
                        for (var index = BasketItems.Count - 1; index >= 0; index--)
                        {
                            var order = BasketItems[index];
                            order.SetAutoCancelWithMaxDigPercentBid();
                        }
                    });
                }
                else
                {
                    ShowMessageFromItem("Toggling Market Fish Loss/Auto Cancel with no subscription", "Missing Data", true);
                }
            }
        }

        [Command]
        public void ToggleMinTheoCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinTheoCheckEnabled = !BasketSettings.MinTheoCheckEnabled;
        }

        [Command]
        public void ToggleMinBidCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinBidCheckEnabled = !BasketSettings.MinBidCheckEnabled;
            if (BasketSettings.MinBidCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinBid();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinBidAskSizeCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinBidAskSizeCheckEnabled = !BasketSettings.MinBidAskSizeCheckEnabled;
            if (BasketSettings.MinBidAskSizeCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinBidAskSize();
                    }
                });
            }
        }

        [Command]
        public void ToggleMinEmaWidthPercentEdgeToTheoCheckCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled = !BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled;
            if (BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithMinEmaWidthPercentEdgeToTheo();
                    }
                });
            }
        }

        [Command]
        public void ToggleCheckForRecentAttemptCommand()
        {
            BasketSettings.CheckForRecentAttempt = !BasketSettings.CheckForRecentAttempt;
        }

        [Command]
        public void ToggleCheckForRecentFillCommand()
        {
            BasketSettings.CheckForRecentFill = !BasketSettings.CheckForRecentFill;
        }

        [Command]
        public void ToggleMinEdgeToPreviousAttemptCheckEnabledCommand()
        {
            BasketSettings.MinEdgeToPreviousAttemptCheckEnabled = !BasketSettings.MinEdgeToPreviousAttemptCheckEnabled;
        }

        [Command]
        public void TogglePreviousAttemptCrossCheckEnabledCommand()
        {
            BasketSettings.PreviousAttemptCrossCheckEnabled = !BasketSettings.PreviousAttemptCrossCheckEnabled;
        }

        [Command]
        public void ToggleMinTimeToPreviousAttemptCheckEnabledCommand()
        {
            BasketSettings.MinTimeToPreviousAttemptCheckEnabled = !BasketSettings.MinTimeToPreviousAttemptCheckEnabled;
        }

        [Command]
        public void ToggleMinTimeToPermLoserCheckEnabledCommand()
        {
            BasketSettings.MinTimeToPermLoserCheckEnabled = !BasketSettings.MinTimeToPermLoserCheckEnabled;
        }

        [Command]
        public void ToggleCancelWithWidthCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithWidthEnabled = !BasketSettings.CancelWithWidthEnabled;
            if (BasketSettings.CancelWithWidthEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithWidthChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithUnderlyingPx()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithUnderlyingPxEnabled = !BasketSettings.CancelWithUnderlyingPxEnabled;
            if (BasketSettings.CancelWithUnderlyingPxEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithUnderPxChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithUnderlyingDeltaPx()
        {
            if (IsDisposed)
            {
                return;
            }
            BasketSettings.CancelWithUnderlyingDeltaPxEnabled = !BasketSettings.CancelWithUnderlyingDeltaPxEnabled;
            if (BasketSettings.CancelWithUnderlyingDeltaPxEnabled)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        order.SetAutoCancelWithUnderDeltaPxChange();
                    }
                });
            }
        }

        [Command]
        public void ToggleCancelWithTimer()
        {
            BasketSettings.CancelWithTimerEnabled = !BasketSettings.CancelWithTimerEnabled;
        }

        [Command]
        public void CheckForAutoCancel()
        {
            if (BasketItems != null)
            {
                Task.Run(() =>
                {
                    for (var index = BasketItems.Count - 1; index >= 0; index--)
                    {
                        var order = BasketItems[index];
                        _ = order.CheckForAutoCancel();
                    }
                });
            }
        }

        [Command]
        public void EnableEdgeToTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToTheo || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToTheo = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToTheoToMarketSpreadCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseTheoToMarketSpreadPx || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseTheoToMarketSpreadPx = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToHistoricBest()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToHistoricBest || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToHistoricBest = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToAdjTheoWithOverrideCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToAdjTheoWithOverride || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToAdjTheoWithOverride = true;
            CheckEdge();
        }

        [Command]
        public void EnableCustomFunctionEdgeCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseCustomFunctionEdge || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseCustomFunctionEdge = true;
            CheckEdge();
        }

        [Command]
        public void EditCustomFunctionEdgeCommand()
        {
            CustomEdgeFunctionEditorView view = _customEdgeFunctionEditorViewFactory.Create();
            if (view.DataContext is CustomEdgeFunctionEditorViewModel viewModel)
            {
                viewModel.Formula = BasketSettings.CustomFunctionEdgeFormula;
                viewModel.FormulaUpdated += CustomEdgeFormulaUpdated;
                view.Show();
            }
        }

        [Command]
        public void EnableDomStyleEdgeCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseDomStyleEdge || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseDomStyleEdge = true;
            CheckEdge();
        }

        [Command]
        public void EditDomStyleEdgeCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            DominatorConfigurationViewModel viewModel = new();
            if (BasketSettings.DominatorConfiguration is DominatorConfigurationViewModel config)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(config);
                    var copy = JsonConvert.DeserializeObject<DominatorConfigurationViewModel>(json);
                    viewModel = copy;
                }
                catch (Exception e)
                {
                    _log.Error(e, "failed to copy");
                }
            }

            var result = DominatorConfiguration.ShowDialog(
                dialogButtons: MessageButton.OKCancel,
                viewModel: viewModel,
                title: viewModel.Title
                );

            if (result == MessageResult.OK)
            {
                BasketSettings.DominatorConfiguration = viewModel;
            }
        }
        protected IDialogService DominatorConfiguration => GetService<IDialogService>("DominatorConfigurationViewService");


        private void CustomEdgeFormulaUpdated(string formula)
        {
            BasketSettings.CustomFunctionEdgeFormula = formula;
        }

        [Command]
        public void EnableEdgeToAdjTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToAdjTheo || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToAdjTheo = true;
            CheckEdge();
        }

        [Command]
        public void EnableBestOfEdge()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseBestOfEdge || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseBestOfEdge = true;
            CheckEdge();
        }

        private async void FlashEdgeLock()
        {
            BestOfEdgeLockFlash = false;
            BestOfEdgeLockFlash = true;
            await Task.Delay(900);
            BestOfEdgeLockFlash = false;
        }

        [Command]
        public void ToggleBestOfAdjTheo()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfAdjTheoEnabled = !BasketSettings.BestOfAdjTheoEnabled;
        }

        [Command]
        public void ToggleBestOfHwTheo()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfHwTheoEnabled = !BasketSettings.BestOfHwTheoEnabled;
        }

        [Command]
        public void ToggleBestOfV0Theo()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfV0TheoEnabled = !BasketSettings.BestOfV0TheoEnabled;
        }

        [Command]
        public void ToggleBestOfMid()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfMidEnabled = !BasketSettings.BestOfMidEnabled;
        }

        [Command]
        public void ToggleBestOfEma()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfEmaEnabled = !BasketSettings.BestOfEmaEnabled;
        }

        [Command]
        public void ToggleBestOfBidPercent()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfBidPercentEnabled = !BasketSettings.BestOfBidPercentEnabled;
        }

        [Command]
        public void ToggleBestOfDigBidPercent()
        {
            if (BestOfEdgeLocked) { FlashEdgeLock(); return; }
            BasketSettings.BestOfDigBidPercentEnabled = !BasketSettings.BestOfDigBidPercentEnabled;
        }

        [Command]
        public void ToggleBestOfEdgeExpanded()
        {
            ShowBestOfEdgeExpanded = !ShowBestOfEdgeExpanded;
        }

        [Command]
        public void ToggleBestOfEdgeLocked()
        {
            BestOfEdgeLocked = !BestOfEdgeLocked;
        }

        [Command]
        public void ToggleBestOfAdjTheoPinned()
        {
            BasketSettings.BestOfAdjTheoPinned = !BasketSettings.BestOfAdjTheoPinned;
        }

        [Command]
        public void ToggleBestOfHwTheoPinned()
        {
            BasketSettings.BestOfHwTheoPinned = !BasketSettings.BestOfHwTheoPinned;
        }

        [Command]
        public void ToggleBestOfV0TheoPinned()
        {
            BasketSettings.BestOfV0TheoPinned = !BasketSettings.BestOfV0TheoPinned;
        }

        [Command]
        public void ToggleBestOfMidPinned()
        {
            BasketSettings.BestOfMidPinned = !BasketSettings.BestOfMidPinned;
        }

        [Command]
        public void ToggleBestOfEmaPinned()
        {
            BasketSettings.BestOfEmaPinned = !BasketSettings.BestOfEmaPinned;
        }

        [Command]
        public void ToggleBestOfBidPercentPinned()
        {
            BasketSettings.BestOfBidPercentPinned = !BasketSettings.BestOfBidPercentPinned;
        }

        [Command]
        public void ToggleBestOfDigBidPercentPinned()
        {
            BasketSettings.BestOfDigBidPercentPinned = !BasketSettings.BestOfDigBidPercentPinned;
        }

        [Command]
        public void EnableLastFillAdjEdgeCommand()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseLastFillAdjPx || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseLastFillAdjPx = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToMid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToMid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToMid = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToEma()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToEma || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToEma = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToTheoAndMid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToTheoAndMid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToTheoAndMid = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToTheoStopMid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToTheoStopMid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToTheoStopMid = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToEmaStopMid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToEmaStopMid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToEmaStopMid = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToMidStopEma()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToMidStopEma || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToMidStopEma = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToBidPercentStopEma()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToBidPercentStopEma || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToBidPercentStopEma = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToBidPercentStopEmaStopTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToEmaBidPercentStopEmaStopTheo()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToDerivedBidPercentStopEmaStopMid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = true;
            CheckEdge();
        }

        [Command]
        public void EnableBidPercent()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseBidPercent || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseBidPercent = true;
            CheckEdge();

            if (BasketItems.Any(x => !double.IsNaN(x.EdgeOverride) || !double.IsNaN(x.AdjustedEdgeOverride)))
            {
                MessageBoxService?.ShowMessage("You have an order with an edge override.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning,
                                               MessageResult.OK);
            }
        }

        [Command]
        public void EnableTheoBidPercent()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseTheoBidPercent || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseTheoBidPercent = true;
            CheckEdge();
        }

        [Command]
        public void ResetEmaCommand()
        {
            BasketSettings.ResetEma();
        }

        [Command]
        public void EmaEnabledChangedCommand()
        {
            try
            {
                SubscriptionManagerChangedCommand("EMA");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmaEnabledChangedCommand));
            }
        }

        [Command]
        public void SubscriptionManagerChangedCommand(string source)
        {
            try
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    item.Resubscribe(source);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscriptionManagerChangedCommand));
            }
        }

        [Command]
        public void ToggleTheoLockCommand()
        {
            try
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    item.ToggleTheoLock();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleTheoLockCommand));
            }
        }

        [Command]
        public void EnableEdgeToEmaBid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToEmaBid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToEmaBid = true;
            CheckEdge();
        }

        [Command]
        public void EnableEdgeToBid()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UseEdgeToBid || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UseEdgeToBid = true;
            CheckEdge();
        }

        [Command]
        public void EnablePermAdjPx()
        {
            if (IsDisposed)
            {
                return;
            }
            if (BasketSettings.UsePermAdjPx || !GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }

            ResetEdgeTypes();
            BasketSettings.UsePermAdjPx = true;
            CheckEdge();
        }

        [Command]
        public void EnableAskPercent()
        {
            if (!GetEdgeTypeChangeVerification())
            {
                CheckEdge();
                return;
            }
            ResetEdgeTypes();
            CheckEdge();
        }

        internal void ResetEdgeTypes()
        {
            BasketSettings.ResubmitAfterCancel = false;
            BasketSettings.UseEdgeToTheo = false;
            BasketSettings.UseEdgeToHistoricBest = false;
            BasketSettings.UseEdgeToAdjTheo = false;
            BasketSettings.UseEdgeToMid = false;
            BasketSettings.UseEdgeToEma = false;
            BasketSettings.UseEdgeToTheoAndMid = false;
            BasketSettings.UseEdgeToTheoStopMid = false;
            BasketSettings.UseEdgeToEmaStopMid = false;
            BasketSettings.UseEdgeToMidStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = false;
            BasketSettings.UseBidPercent = false;
            BasketSettings.UseEdgeToEmaBid = false;
            BasketSettings.UseEdgeToBid = false;
            BasketSettings.UsePermAdjPx = false;
            BasketSettings.UseLastFillAdjPx = false;
            BasketSettings.UseCustomFunctionEdge = false;
            BasketSettings.UseDomStyleEdge = false;
            BasketSettings.UseEdgeToAdjTheoWithOverride = false;
            BasketSettings.UseTheoToMarketSpreadPx = false;
            BasketSettings.UseBestOfEdge = false;
        }

        private void CheckEdge()
        {
            AutoConfigUpdated = false;
            if (GetEdge() < 0)
            {
                ToggleEdgeWarning("Warn: Negative Edge Set.", "Basket Reverse");
            }
            else if (ShowEdgeWarning)
            {
                ShowEdgeWarning = false;
            }
        }

        private bool GetEdgeTypeChangeVerification()
        {
            return MessageBoxService?.ShowMessage("Are you sure you want to change edge type?",
                                                  "ZeroPlus OMS",
                                                  MessageButton.YesNo,
                                                  MessageIcon.Question,
                                                  MessageResult.Yes) == MessageResult.Yes;
        }

        [Command]
        public void EdgeChangedCommand(EditValueChangedEventArgs eventArgs)
        {
            ImmediateAutoTraderUpdateCommand();
            if (BasketSettings != null && BasketSettings.DynamicUpdateEdgeOverrides && eventArgs.NewValue != null && eventArgs.OldValue != null)
            {
                double delta = (double)eventArgs.NewValue - (double)eventArgs.OldValue;
                for (int i = 0; i < BasketItems.Count; i++)
                {
                    BasketTraderItemModel basketItem = BasketItems[i];
                    if (!double.IsNaN(basketItem.EdgeOverride))
                    {
                        basketItem.EdgeOverride += delta;
                    }
                }
            }
        }

        [Command]
        public async Task EdgeToTheoChanged()
        {
            try
            {
                EnableEdgeToTheo();
                if (!IsDisposed && BasketSettings.UseEdgeToTheo)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToTheoAsync(BasketSettings.EdgeToTheo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToTheoChanged));
            }
        }

        [Command]
        public async Task EdgeToTheoToMarketChangedCommand()
        {
            try
            {
                EnableEdgeToTheoToMarketSpreadCommand();
                if (!IsDisposed && BasketSettings.UseEdgeToTheo)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToTheoAsync(BasketSettings.EdgeToTheo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToTheoToMarketChangedCommand));
            }
        }

        [Command]
        public async Task EdgeToAdjTheoWithOverrideChangedCommand()
        {
            try
            {
                EnableEdgeToAdjTheoWithOverrideCommand();
                if (!IsDisposed && BasketSettings.UseEdgeToAdjTheoWithOverride)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToAdjTheoWithOverrideAsync(BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage, BasketSettings.EdgeToAdjTheoWithOverrideStatic, BasketSettings.EdgeToAdjTheoWithOverridePercent);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToAdjTheoWithOverrideChangedCommand));
            }
        }

        [Command]
        public async Task EdgeToHistoricBestChanged()
        {
            try
            {
                EnableEdgeToHistoricBest();
                if (!IsDisposed && BasketSettings.UseEdgeToHistoricBest)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToHistoricBestAsync(BasketSettings.EdgeToHistoricBest);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToHistoricBestChanged));
            }
        }

        [Command]
        public async Task EdgeToAdjTheoChanged()
        {
            try
            {
                EnableEdgeToAdjTheo();
                if (!IsDisposed && BasketSettings.UseEdgeToAdjTheo)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToAdjTheoAsync(BasketSettings.EdgeToAdjTheo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToAdjTheoChanged));
            }
        }

        [Command]
        public async Task EdgeToMidChanged()
        {
            try
            {

                EnableEdgeToMid();
                if (!IsDisposed && BasketSettings.UseEdgeToMid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToMid(BasketSettings.EdgeToMid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToMidChanged));
            }
        }

        [Command]
        public async Task EdgeToEmaChanged()
        {
            try
            {

                EnableEdgeToEma();
                if (!IsDisposed && BasketSettings.UseEdgeToEma)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToEma(BasketSettings.EdgeToEma);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToEmaChanged));
            }
        }

        [Command]
        public async Task LastFillAdjEdgeChangedCommand()
        {
            try
            {
                EnableLastFillAdjEdgeCommand();
                if (!IsDisposed && BasketSettings.UseLastFillAdjPx)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToLastFillAdjPx(BasketSettings.LastFillAdjEdge);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LastFillAdjEdgeChangedCommand));
            }
        }

        [Command]
        public async Task EdgeToTheoAndMidChanged()
        {
            try
            {
                EnableEdgeToTheoAndMid();
                if (!IsDisposed && BasketSettings.UseEdgeToTheoAndMid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToTheoAndMid(BasketSettings.EdgeToTheoAndMid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToTheoAndMidChanged));
            }
        }

        [Command]
        public async Task EdgeToTheoStopMidChanged()
        {
            try
            {
                EnableEdgeToTheoStopMid();
                if (!IsDisposed && BasketSettings.UseEdgeToTheoStopMid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToTheoStopMid(BasketSettings.EdgeToTheoStopMid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToTheoStopMidChanged));
            }
        }

        [Command]
        public async Task EdgeToEmaStopMidChanged()
        {
            try
            {
                EnableEdgeToEmaStopMid();
                if (!IsDisposed && BasketSettings.UseEdgeToEmaStopMid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToEmaStopMid(BasketSettings.EdgeToEmaStopMid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToEmaStopMidChanged));
            }
        }

        [Command]
        public async Task EdgeToMidStopEmaChanged()
        {
            try
            {
                EnableEdgeToMidStopEma();
                if (!IsDisposed && BasketSettings.UseEdgeToMidStopEma)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToMidStopEma(BasketSettings.EdgeToMidStopEma);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToMidStopEmaChanged));
            }
        }

        [Command]
        public async Task EdgeToBidPercentStopEmaChanged()
        {
            try
            {
                EnableEdgeToBidPercentStopEma();
                if (!IsDisposed && BasketSettings.UseEdgeToBidPercentStopEma)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToBidPercentStopEma(BasketSettings.EdgeToBidPercentStopEma);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToBidPercentStopEmaChanged));
            }
        }

        [Command]
        public async Task EdgeToBidPercentStopEmaStopTheoChanged()
        {
            try
            {
                EnableEdgeToBidPercentStopEmaStopTheo();
                if (!IsDisposed && BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToBidPercentStopEmaStopTheo(BasketSettings.EdgeToBidPercentStopEmaStopTheo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToBidPercentStopEmaStopTheoChanged));
            }
        }

        [Command]
        public async Task EdgeToEmaBidPercentStopEmaStopTheoChanged()
        {
            try
            {
                EnableEdgeToEmaBidPercentStopEmaStopTheo();
                if (!IsDisposed && BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToEmaBidPercentStopEmaStopTheo(BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToEmaBidPercentStopEmaStopTheoChanged));
            }
        }

        [Command]
        public async Task EdgeToDerivedBidPercentStopEmaStopMidChanged()
        {
            try
            {
                EnableEdgeToDerivedBidPercentStopEmaStopMid();
                if (!IsDisposed && BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToDerivedBidPercentStopEmaStopMid(BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToDerivedBidPercentStopEmaStopMidChanged));
            }
        }

        [Command]
        public async Task EdgeToEmaBidChanged()
        {
            try
            {
                EnableEdgeToEmaBid();
                if (!IsDisposed && BasketSettings.UseEdgeToEmaBid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToEmaBid(BasketSettings.EdgeToEmaBid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToEmaBidChanged));
            }
        }

        [Command]
        public async Task EdgeToBidChanged()
        {
            try
            {
                EnableEdgeToBid();
                if (!IsDisposed && BasketSettings.UseEdgeToBid)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseEdgeToBid(BasketSettings.EdgeToBid);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeToEmaBidChanged));
            }
        }

        [Command]
        public async Task PermAdjEdgeChangedCommand()
        {
            try
            {
                EnablePermAdjPx();
                if (!IsDisposed && BasketSettings.UsePermAdjPx)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UsePermAdjPx(BasketSettings.PermAdjEdge);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PermAdjEdgeChangedCommand));
            }
        }

        [Command]
        public async Task BidPercentChanged()
        {
            try
            {
                EnableBidPercent();
                if (!IsDisposed && BasketSettings.UseBidPercent)
                {
                    if (BasketSettings.BidPercent < OmsCore.Config.MinimumBidPercentLimit || BasketSettings.BidPercent > OmsCore.Config.BidPercentLimit)
                    {
                        return;
                    }
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseBidPercent(BasketSettings.BidPercent);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BidPercentChanged));
            }
        }

        [Command]
        public async Task BestOfEdgeChangedCommand()
        {
            try
            {
                ImmediateAutoTraderUpdateCommand();
                EnableBestOfEdge();
                if (!IsDisposed && BasketSettings.UseBestOfEdge)
                {
                    await Task.Run(async () =>
                    {
                        for (int i = 0; i < BasketItems.Count; i++)
                        {
                            BasketTraderItemModel item = BasketItems[i];
                            await item.UseBestOfEdge();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BidPercentChanged));
            }
        }

        [Command]
        public async Task SetEdgeToTheoBidPercent(double theoBidPercent)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }

                if (theoBidPercent < OmsCore.Config.MinimumBidPercentLimit || theoBidPercent > OmsCore.Config.BidPercentLimit)
                {
                    return;
                }

                await Task.Run(async () =>
                {
                    for (int i = 0; i < BasketItems.Count; i++)
                    {
                        BasketTraderItemModel item = BasketItems[i];
                        await item.SetEdgeToTheoBidPercent(theoBidPercent);
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetEdgeToTheoBidPercent));
            }
        }

        [Command]
        public async Task TheoBidPercentChanged()
        {
            try
            {
                EnableTheoBidPercent();
                if (!IsDisposed && BasketSettings.UseTheoBidPercent)
                {
                    await SetEdgeToTheoBidPercent(BasketSettings.TheoBidPercent);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TheoBidPercentChanged));
            }
        }

        [Command]
        public void ReverseLooperRoutesCommand()
        {
            try
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                (automationConfig.LooperCloseRoute, automationConfig.LooperOpenRoute) = (automationConfig.LooperOpenRoute, automationConfig.LooperCloseRoute);
                (automationConfig.LooperCloseRouteSize, automationConfig.LooperOpenRouteSize) = (automationConfig.LooperOpenRouteSize, automationConfig.LooperCloseRouteSize);
                (automationConfig.LooperCloseRouteSingleLeg, automationConfig.LooperOpenRouteSingleLeg) = (automationConfig.LooperOpenRouteSingleLeg, automationConfig.LooperCloseRouteSingleLeg);
                (automationConfig.LooperCloseRouteSingleLegSize, automationConfig.LooperOpenRouteSingleLegSize) = (automationConfig.LooperOpenRouteSingleLegSize, automationConfig.LooperCloseRouteSingleLegSize);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseLooperRoutesCommand));
            }
        }

        [Command]
        public void CopyFishSettings(string parameter)
        {
            AutomationConfigModel automationConfig = GetAutomationConfig();
            switch (parameter)
            {
                case "FromMain":
                    automationConfig.ContraFishEdge = automationConfig.FishEdge;
                    automationConfig.ContraFishInterval = automationConfig.FishInterval;
                    automationConfig.ContraFishPriceIncrement = automationConfig.FishPriceIncrement;
                    break;
                case "FromClose":
                    automationConfig.FishEdge = automationConfig.ContraFishEdge;
                    automationConfig.FishInterval = automationConfig.ContraFishInterval;
                    automationConfig.FishPriceIncrement = automationConfig.ContraFishPriceIncrement;
                    break;
            }
        }

        [Command]
        public async void ReverseSides()
        {
            bool ok = !OmsCore.Config.GetVerificationForBasketReverse || await GetVerificationAsync("Do you want to reverse sides for all staged orders?");
            if (ok)
            {
                BasketStateChange();
                ReverseSidesNoCheck();
            }
        }

        [Command]
        public async void FlipCP()
        {
            bool ok = await GetVerificationAsync("Do you want to flip C/P for all staged orders?");
            if (ok)
            {
                BasketStateChange();
                FlipCpNoCheck();
            }
        }

        [Command]
        public async void OppCP()
        {
            bool ok = await GetVerificationAsync("Do you want to load opposite C/P for all staged orders?");
            if (ok)
            {
                BasketStateChange();
                OppCpNoCheck();
            }
        }

        [Command]
        public async virtual Task SetEdgeAsync(BasketTraderItemModel basketItem)
        {
            if (basketItem != null)
            {
                await basketItem.SetEdgeAsync();
            }
        }

        [Command]
        public void ModifyStagedOrders()
        {

            ModifyStagedOrdersViewModel modifyStagedOrdersViewModel = new();
            modifyStagedOrdersViewModel.SetDispatcher(Dispatcher);
            modifyStagedOrdersViewModel.InitializeForATR();

            modifyStagedOrdersViewModel.ModifyBasketEvent += ModifyAllOrders;

            ModifyStagedOrdersView view = new()
            {
                DataContext = modifyStagedOrdersViewModel,
            };

            void cleanup(object sender, EventArgs args)
            {
                view.Closed -= cleanup;
                modifyStagedOrdersViewModel.ModifyBasketEvent -= ModifyAllOrders;
            }
            view.Closed += cleanup;

            view.Show();
        }

        [Command]
        public void ModifyStagedPxQty()
        {
            ModifyStagedOrdersViewModel modifyStagedOrdersViewModel = new();

            modifyStagedOrdersViewModel.ModifyBasketQtyPxEvent += ModifyAllPxQty;

            ModifyStagedPxQtyView view = new()
            {
                DataContext = modifyStagedOrdersViewModel
            };

            void cleanup(object sender, EventArgs args)
            {
                view.Closed -= cleanup;
                modifyStagedOrdersViewModel.ModifyBasketQtyPxEvent -= ModifyAllPxQty;
            }
            view.Closed += cleanup;

            view.Show();
        }

        [Command]
        public void AutoCleanChanged()
        {
            if (AutoClean)
            {
                CleanInvalidRows();
            }
        }

        [Command]
        public void Clean()
        {
            BasketStateChange();
            CleanInvalidRows(withUndoPrompt: true);
        }

        [Command]
        public async Task ClearAllBuysCommand()
        {
            BasketStateChange();
            HashSet<BasketTraderItemModel> itemsToRemove = BasketItems.Where(x => x.IsSingleLeg ? x.Side == Side.Buy : x.Price >= 0).ToHashSet();
            await RemoveItems(withUndoPrompt: true, itemsToRemove);
        }

        [Command]
        public async Task ClearAllSellsCommand()
        {
            BasketStateChange();
            HashSet<BasketTraderItemModel> itemsToRemove = BasketItems.Where(x => x.IsSingleLeg ? x.Side == Side.Sell : x.Price < 0).ToHashSet();
            await RemoveItems(withUndoPrompt: true, itemsToRemove);
        }

        [Command]
        public void ClearAllOutsideWidthWindowCommand()
        {
            if (double.IsNaN(MinWidthForClear))
            {
                MinWidthForClear = BasketSettings.CancelWithWidthThreshold;
            }
            if (double.IsNaN(MaxWidthForClear))
            {
                MaxWidthForClear = BasketSettings.MaxWidthCheckPx;
            }
            RemoveByWidthPromptView view = new()
            {
                DataContext = this
            };
            view.Show();
        }

        [Command]
        public async Task ClearAllOutsideWidthCommand()
        {
            HashSet<BasketTraderItemModel> itemsToRemove = new();
            foreach (BasketTraderItemModel item in BasketItems)
            {
                if (!await item.WaitForMarkLoad())
                {
                    itemsToRemove.Add(item);
                }
                else
                {
                    if (item.Width < MinWidthForClear ||
                        item.Width > MaxWidthForClear)
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }
            await RemoveItems(withUndoPrompt: true, itemsToRemove);
        }

        [Command]
        public async Task CleanDuplicateStrikesCommand()
        {
            HashSet<BasketTraderItemModel> itemsToRemove = new();
            List<BasketTraderItemModel> flys = BasketItems.Where(x => x.SpreadType is "PUT BUTTERFLY" or
                                              "CALL BUTTERFLY" or
                                              "CALL SKEWED BUTTERFLY" or
                                              "PUT SKEWED BUTTERFLY").ToList();
            if (flys.Count > 0)
            {
                IEnumerable<IGrouping<DateTime, BasketTraderItemModel>> groups = flys.GroupBy(x => x.Legs[0].ExpirationInfo.Expiration);
                foreach (IGrouping<DateTime, BasketTraderItemModel> group in groups)
                {
                    Dictionary<Tuple<double, double>, List<BasketTraderItemModel>> strikeKeyToOrdersListMap = new();
                    foreach (BasketTraderItemModel fly in group)
                    {
                        List<double> strikes = fly.Legs.Select(x => x.Strike.Strike).OrderBy(x => x).ToList();
                        Tuple<double, double> key = Tuple.Create(strikes[0], strikes[1]);
                        if (!strikeKeyToOrdersListMap.TryGetValue(key, out List<BasketTraderItemModel> listOfOrders))
                        {
                            listOfOrders = new List<BasketTraderItemModel>();
                            strikeKeyToOrdersListMap[key] = listOfOrders;
                        }
                        listOfOrders.Add(fly);
                        key = Tuple.Create(strikes[1], strikes[2]);
                        if (!strikeKeyToOrdersListMap.TryGetValue(key, out listOfOrders))
                        {
                            listOfOrders = new List<BasketTraderItemModel>();
                            strikeKeyToOrdersListMap[key] = listOfOrders;
                        }
                        listOfOrders.Add(fly);
                        key = Tuple.Create(strikes[0], strikes[2]);
                        if (!strikeKeyToOrdersListMap.TryGetValue(key, out listOfOrders))
                        {
                            listOfOrders = new List<BasketTraderItemModel>();
                            strikeKeyToOrdersListMap[key] = listOfOrders;
                        }
                        listOfOrders.Add(fly);
                    }
                    foreach (KeyValuePair<Tuple<double, double>, List<BasketTraderItemModel>> kvp in strikeKeyToOrdersListMap)
                    {
                        if (kvp.Value.Count > 1)
                        {
                            for (int i = 1; i < kvp.Value.Count; i++)
                            {
                                itemsToRemove.Add(kvp.Value[i]);
                            }
                        }
                    }
                }
            }
            List<BasketTraderItemModel> diagonals = BasketItems.Where(x => x.SpreadType is "PUT DIAGONAL" or
                                                   "CALL DIAGONAL").ToList();
            if (diagonals.Count > 0)
            {
                for (int i = 0; i < diagonals.Count; i++)
                {
                    BasketTraderItemModel diagonal = diagonals[i];
                    for (int j = i + 1; j < diagonals.Count; j++)
                    {
                        BasketTraderItemModel diagonal2 = diagonals[j];
                        if (diagonal.Legs[0].Symbol == diagonal2.Legs[0].Symbol ||
                            diagonal.Legs[0].Symbol == diagonal2.Legs[1].Symbol ||
                            diagonal.Legs[1].Symbol == diagonal2.Legs[0].Symbol ||
                            diagonal.Legs[1].Symbol == diagonal2.Legs[1].Symbol)
                        {
                            itemsToRemove.Add(diagonal2);
                        }
                    }
                }
            }

            await RemoveItems(true, itemsToRemove);
        }

        [Command]
        public async Task CleanAllItmCommand()
        {
            HashSet<BasketTraderItemModel> itemsToRemove = new();

            IEnumerable<Task<bool>> task = BasketItems.Select(x => x.WaitForTheoLoadAsync());
            await Task.WhenAll(task);
            foreach (BasketTraderItemModel item in BasketItems)
            {
                if (item.NetTheoLoaded)
                {
                    foreach (TicketLegModel leg in item.Legs)
                    {
                        double delta = Math.Abs(leg.Delta);
                        if (delta > .5)
                        {
                            itemsToRemove.Add(item);
                            break;
                        }
                    }
                }
                else
                {
                    itemsToRemove.Add(item);
                }
            }

            await RemoveItems(true, itemsToRemove);
        }

        [Command]
        public async Task CleanAllOtmCommand()
        {
            HashSet<BasketTraderItemModel> itemsToRemove = new();

            IEnumerable<Task<bool>> task = BasketItems.Select(x => x.WaitForTheoLoadAsync());
            await Task.WhenAll(task);
            foreach (BasketTraderItemModel item in BasketItems)
            {
                if (item.NetTheoLoaded)
                {
                    foreach (TicketLegModel leg in item.Legs)
                    {
                        double delta = Math.Abs(leg.Delta);
                        if (delta < .5)
                        {
                            itemsToRemove.Add(item);
                            break;
                        }
                    }
                }
                else
                {
                    itemsToRemove.Add(item);
                }
            }

            await RemoveItems(true, itemsToRemove);
        }

        public async Task CleanInvalidRows(bool withUndoPrompt = false)
        {
            try
            {
                HashSet<string> loadedSpreads = new();
                HashSet<BasketTraderItemModel> itemsToRemove = new();
                foreach (BasketTraderItemModel item in BasketItems.ToList())
                {
                    if (string.IsNullOrWhiteSpace(item.Description))
                    {
                        item.UpdateDescription();
                    }

                    if (loadedSpreads.Contains(item.Description) ||
                        (BasketSettings.SubscribeToHanweck && !await item.WaitForTheoLoadAsync()) ||
                        (BasketSettings.SubscribeToMarketData && !await item.WaitForMarkLoad()))
                    {
                        itemsToRemove.Add(item);
                    }
                    else if (!item.IsWithinStrikeCapAsync())
                    {
                        itemsToRemove.Add(item);
                    }
                    else if (!IsDisposed && BasketSettings.SubscribeToHanweck && !await item.IsWithinDeltaCapAsync())
                    {
                        itemsToRemove.Add(item);
                    }
                    else if (!IsDisposed && BasketSettings.SubscribeToMarketData && !await item.IsWithinWidthCapAsync())
                    {
                        itemsToRemove.Add(item);
                    }
                    else if (BasketType == BasketType.LockTrader &&
                            (string.IsNullOrWhiteSpace(item.SpreadType) || !OmsCore.Config.LockTraderAllowedStrategies.Contains(item.BaseStrategy.ToString())))
                    {
                        itemsToRemove.Add(item);
                    }
                    else
                    {
                        loadedSpreads.Add(item.Description);
                    }
                }
                await RemoveItems(withUndoPrompt, itemsToRemove);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CleanInvalidRows));
            }
        }

        public async Task RemoveItems(bool withUndoPrompt, HashSet<BasketTraderItemModel> itemsToRemove, bool checkForResting = false)
        {
            if (itemsToRemove.Count > 0)
            {
                bool undo = false;
                HashSet<BasketTraderItemModel> temp = itemsToRemove;
                itemsToRemove = new HashSet<BasketTraderItemModel>();
                await Dispatcher?.BeginInvoke(new Action(() =>
                {
                    foreach (BasketTraderItemModel item in temp)
                    {
                        if (checkForResting)
                        {
                            if (item.IsActive || item.MainResting)
                            {
                                continue;
                            }
                        }
                        BasketItems.Remove(item);
                        itemsToRemove.Add(item);
                    }
                    if (withUndoPrompt)
                    {
                        if (itemsToRemove.Count > 0)
                        {
                            undo = MessageBoxService?.Show($"{itemsToRemove.Count} items removed.\nTo Undo this change click on Cancel",
                                                             "Confirm",
                                                             MessageButton.OKCancel,
                                                             MessageIcon.Exclamation,
                                                             MessageResult.OK) == MessageResult.Cancel;
                        }
                        else
                        {
                            MessageBoxService?.ShowMessage("No item removed.",
                                                           "ZeroPlus OMS",
                                                           MessageButton.OK,
                                                           MessageIcon.Information,
                                                           MessageResult.OK);
                        }
                    }
                    if (undo)
                    {
                        _ = AddMultipleToBasketAsync(itemsToRemove);
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            foreach (BasketTraderItemModel item in itemsToRemove)
                            {
                                UnregisterEvents(item);
                                _log.Info(nameof(RemoveItems) + " Disposing order model for " + item.SpreadId);
                                item.Dispose();
                            }
                        });
                    }
                }));
            }
        }

        public async Task RemoveItem(bool withUndoPrompt, BasketTraderItemModel item)
        {
            if (item == null)
            {
                return;
            }

            Dispatcher?.BeginInvoke(new Action(() =>
            {
                BasketItems.Remove(item);
            }));

            bool undo = false;
            if (withUndoPrompt)
            {
                await Dispatcher?.BeginInvoke(new Action(() =>
                {

                    undo = MessageBoxService?.Show($"Items removed.\nTo Undo this change click on Cancel",
                                                     "Confirm",
                                                     MessageButton.OKCancel,
                                                     MessageIcon.Exclamation,
                                                     MessageResult.OK) == MessageResult.Cancel;
                }));
            }

            if (undo)
            {
                _ = AddToBasketAsync(item);
            }
            else
            {
                UnregisterEvents(item);
                _log.Info(nameof(RemoveItems) + " Disposing order model for " + item.SpreadId);
                item.Dispose();
            }
        }

        [AsyncCommand(UseCommandManager = false)]
        public async Task FlattenHedgeDeltaCommand()
        {
            try
            {
                List<(string Underlying, string SpreadId, int RequiredStocks)> basketItems = new();

                foreach (BasketTraderItemModel item in BasketItems.DistinctBy(x => x.SpreadId))
                {
                    item.UpdateStockPositions();
                    if (item.RequiredStocks != 0)
                    {
                        basketItems.Add((item.Underlying, item.SpreadId, item.RequiredStocks));
                    }
                }
                basketItems = basketItems.DistinctBy(x => x.SpreadId).ToList();

                IEnumerable<IGrouping<string, (string Underlying, string SpreadId, int RequiredStocks)>> groupedByUnderlying = basketItems.GroupBy(x => x.Underlying);
                foreach (IGrouping<string, (string Underlying, string SpreadId, int RequiredStocks)> group in groupedByUnderlying)
                {
                    string comment = "MULTIHEDGE - " + OmsCore.User.Username.ToUpper();
                    int netQty = 0;

                    foreach ((string Underlying, string SpreadId, int RequiredStocks) in group.DistinctBy(x => x.SpreadId))
                    {
                        comment += $" - {SpreadId.ToUpper()}({RequiredStocks})";
                        netQty += RequiredStocks;
                    }

                    comment += " - " + BasketSettings.Uid;
                    if (netQty != 0)
                    {
                        if (BasketItems.FirstOrDefault(x => x.Underlying == group.First().Underlying) is BasketTraderItemModel ticket)
                        {
                            var orderInfo = ticket.BuildStockHedgeOrderAsync(netQty, comment);

                            if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
                            {
                                if (double.IsNaN(orderInfo.Price))
                                {
                                    throw new SlimException("[Risk] Hedge price could not be determined.");
                                }
                                if (orderInfo.Price > OmsCore.Config.MaxAutoHedgeNetCash)
                                {
                                    throw new SlimException("[Risk] Hedge price above risk limit.");
                                }
                            }

                            if (OmsCore.Config.MaxAutoHedgePositionEnabled)
                            {
                                if (orderInfo.Qty > OmsCore.Config.MaxAutoHedgePosition)
                                {
                                    throw new SlimException("[Risk] Hedge qty above risk limit.");
                                }
                            }

                            bool submit = await GetVerificationAsync($"Hedge with: {ticket.Underlying}{System.Environment.NewLine}Required : {orderInfo.OMSSide} {netQty} {ticket.HedgeUnderlying}{System.Environment.NewLine}Would you like to proceed?"
                                                         , "Flatten Hedge Delta");
                            if (submit)
                            {
                                orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                                await OmsCore.OrderClient.SendOrderAsync(orderInfo, ticket.GetInstanceMode(), ticket, false, ticket.Multiplier, false);
                            }
                        }
                    }
                }
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(FlattenHedgeDeltaCommand));
                MessageBoxService?.ShowMessage(ex.Message, "Flatten Hedge Failed");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FlattenHedgeDeltaCommand));
            }
        }

        internal IEnumerable<(string Underlying, string SpreadId, int RequiredStocks)> GetHedgeDeltaItems()
        {
            return BasketItems.DistinctBy(x => x.SpreadId)
            .Where(item =>
            {
                Dispatcher.Invoke(item.UpdateStockPositions);
                return item.RequiredStocks != 0;
            }).Select(item => (item.Underlying, item.SpreadId, item.RequiredStocks));
        }

        [Command]
        public void ResetLoopCounterCommand()
        {
            foreach (BasketTraderItemModel item in BasketItems.ToList())
            {
                item.LoopIterationCounter = 0;
                item.LoopIterationCounterAfterSizeup = 0;
            }
        }

        [Command]
        public void ClearQty()
        {
            BasketSettings.InitQty = 1;
            foreach (BasketTraderItemModel item in BasketItems.ToList())
            {
                item.UpdateQty(1);
            }
        }

        [Command]
        public void ClearStatus()
        {
            foreach (BasketTraderItemModel item in BasketItems.ToList())
            {
                item.ClearStatus();
            }
        }

        [Command]
        public void ClearPnlCommand()
        {
            foreach (BasketTraderItemModel item in BasketItems)
            {
                item.ClearPnl();
            }
            BasketItemPnlUpdatedEvent(ResetVolumeChange);
        }

        [Command]
        public void ClearHedgePnlCommand()
        {
            foreach (BasketTraderItemModel item in BasketItems)
            {
                item.ClearHedgePnl();
            }
        }

        [Command]
        public async Task SubmitAll()
        {
            if (SubmitAllRunning)
            {
                MessageBoxService?.ShowMessage("Submit all is already in progress.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }

            AutomationConfigModel automationConfig = GetAutomationConfig();
            if (BasketSettings != null && automationConfig != null)
            {
                var openingRoute = automationConfig.LooperOpenRoute;
                if (!string.IsNullOrWhiteSpace(openingRoute))
                {
                    if (OrderTicket.SingleLegOnlyRoutes.Contains(openingRoute.ToUpper()) &&
                        BasketItems.Any(x => !x.IsSingleLeg))
                    {
                        MessageResult? result = MessageBoxService?.ShowMessage("You are about to submit spread orders to a route that does not support spreads.\n" +
                                                                    "Are you sure you want to proceed?",
                                                                    "ZeroPlus OMS",
                                                                    MessageButton.YesNo,
                                                                    MessageIcon.Warning,
                                                                    MessageResult.No);
                        if (result == MessageResult.No)
                        {
                            return;
                        }
                    }
                }

                bool isValidRoute = IsValidRoute();
                if (!isValidRoute)
                {
                    MessageBoxService?.ShowMessage("Invalid Open/Close Route Set!",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Warning);
                    return;
                }
            }

            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                MessageBoxService?.ShowMessage("Basket Delta Limit Reached.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }

            if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit)
            {
                MessageBoxService?.ShowMessage("Basket Long Position Limit Reached.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }

            if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit)
            {
                MessageBoxService?.ShowMessage("Basket Short Position Limit Reached.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }

            if (automationConfig != null &&
                automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Dynamic &&
                automationConfig.DynamicEdgeModel == null)
            {
                MessageBoxService?.ShowMessage("Dynamic edge lookup model not loaded.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }

            _lastActivityTime = DateTime.Now;
            CancelQueuedSubmitWithDelay();
            bool proceed = !OmsCore.Config.GetVerificationForBasketSubmitAllV2;
            if (!proceed)
            {
                await Dispatcher?.BeginInvoke(new Action(() =>
                {
                    proceed = MessageBoxService?.Show(string.Format("Are you sure you want to submit all orders?"),
                                                 "Confirm",
                                                 MessageButton.YesNo,
                                                 MessageIcon.Exclamation,
                                                 MessageResult.Yes) == MessageResult.Yes;
                }));
            }
            if (proceed)
            {
                await SubmitAllNoCheck();
            }
        }

        [Command]
        public async void ModifyAll()
        {
            bool ok = false;
            await Dispatcher?.BeginInvoke(new Action(() =>
            {
                ok = MessageBoxService?.Show(string.Format("Are you sure you want to modify all working orders?"),
                                              "Confirm",
                                              MessageButton.YesNo,
                                              MessageIcon.Exclamation,
                                              MessageResult.Yes) == MessageResult.Yes;
            }));
            if (ok)
            {
                await ModifyAllNoCheck();
                _lastActivityTime = DateTime.Now;
            }
        }

        [Command]
        public void CancelAll()
        {
            _submitIndex = 0;
            _lastActivityTime = DateTime.Now;
            CancelAllNoCheck();
        }

        [Command]
        public void CheckAll()
        {
            try
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    item.Active = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckAll));
            }
        }

        [Command]
        public void UncheckAll()
        {
            try
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    item.Active = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UncheckAll));
            }
        }

        [Command]
        public void Remove(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is BasketTraderItemModel item)
                {
                    Dispatcher?.BeginInvoke(new Action(() => BasketItems.Remove(item)));
                    UnregisterEvents(item);
                    item.Dispose();
                    _log.Info(nameof(Remove) + " Disposing order model for " + item.SpreadId);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Remove));
            }
        }

        [Command]
        public void RemoveSelected(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                if (parameter is IEnumerable<object> basketItemsSelected)
                {
                    foreach (object basketItem in basketItemsSelected.ToList())
                    {
                        Remove((BasketTraderItemModel)basketItem);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveSelected));
            }
        }

        [Command]
        public void CopySymbolsTosCsvCommand(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                if (parameter is IEnumerable<object> basketItemsSelected)
                {
                    var symbols = new HashSet<string>();
                    foreach (object basketItem in basketItemsSelected.ToList())
                    {
                        BasketTraderItemModel item = (BasketTraderItemModel)basketItem;
                        foreach (var leg in item.Legs)
                        {
                            symbols.Add(leg.Symbol);
                        }
                    }

                    var csv = string.Join(", ", symbols.OrderBy(x => x));
                    Clipboard.SetText(csv);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CopySymbolsTosCsvCommand));
            }
        }

        [Command]
        public void DuplicateOrder(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }
                if (parameter is BasketTraderItemModel item)
                {
                    BasketTraderItemModel newItem = MakeBasketItemModel();
                    newItem.LoadFromTicketAsync(item).ContinueWith(t => Dispatcher?.BeginInvoke(new Action(() => BasketItems.Add(newItem))));
                    newItem.ToggleTheoLock();
                    RegisterEvents(newItem);
                    SetTitle();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveSelected));
            }
        }

        [Command]
        public void OpenPermComboEditorCommand()
        {
            PermComboEditorView view = new();
            view.Show();
        }

        [Command]
        public async Task UndoCommand()
        {
            try
            {
                if (_undoStack.Count() > 0)
                {
                    var basketState = _undoStack.Pop();
                    var currentState = GetBasketState();
                    await LoadFromBasketStateAsync(basketState);
                    _redoStack.Push(currentState);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UndoCommand));
            }
        }

        [Command]
        public async Task RedoCommand()
        {
            try
            {
                if (_redoStack.Count() > 0)
                {
                    var basketState = _redoStack.Pop();
                    var currentState = GetBasketState();
                    await LoadFromBasketStateAsync(basketState);
                    _undoStack.Push(currentState);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RedoCommand));
            }
        }

        [Command]
        public async Task ExpirationUp(object parameter)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BasketStateChange();
                if (PermSelf)
                {
                    switch (PermType)
                    {
                        case PermType.Greek when BasketItems.All(x => x.IsSingleLeg):
                            {
                                await ExpirationGreekPerm(PermutationDirection.Up);
                                break;
                            }
                        default:
                            await ExpirationUpSelf();
                            break;
                    }
                    stopwatch.Stop();
                    _log.Info(nameof(ExpirationDown) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms.");
                }
                else
                {
                    if (SelectedItems.Count != 0)
                    {
                        parameter = SelectedItems;
                    }
                    parameter ??= SelectedItem;
                    int count = await Task.Run(async () => await PermAndAddMultiple(parameter, PermMode.ExpirationUp, PermSide, Count, MaintainBaseStrategyOnPerm));
                    stopwatch.Stop();
                    _log.Info(nameof(ExpirationUp) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms, Generated: " + count);
                }
            }
            catch (ArgumentNullException)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage("No item selected for permutation.", "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationUp));
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        [Command]
        public async Task ExpirationDown(object parameter)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BasketStateChange();
                if (PermSelf)
                {
                    switch (PermType)
                    {
                        case PermType.Greek when BasketItems.All(x => x.IsSingleLeg):
                            {
                                await ExpirationGreekPerm(PermutationDirection.Down);
                                break;
                            }
                        default:
                            await ExpirationDownSelf();
                            break;
                    }
                    stopwatch.Stop();
                    _log.Info(nameof(ExpirationDown) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms.");
                }
                else
                {
                    if (SelectedItems.Count != 0)
                    {
                        parameter = SelectedItems;
                    }
                    parameter ??= SelectedItem;
                    int count = await Task.Run(async () => await PermAndAddMultiple(parameter, PermMode.ExpirationDown, PermSide, Count, MaintainBaseStrategyOnPerm));
                    stopwatch.Stop();
                    _log.Info(nameof(ExpirationDown) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms, Generated: " + count);
                }
            }
            catch (ArgumentNullException)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage("No item selected for permutation.", "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationDown));
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        [Command]
        public async Task StrikeUp(object parameter)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BasketStateChange();
                if (PermSelf)
                {
                    await StrikeUpSelf();
                    stopwatch.Stop();
                    _log.Info(nameof(StrikeUp) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms.");
                }
                else
                {
                    if (SelectedItems.Count != 0)
                    {
                        parameter = SelectedItems;
                    }
                    parameter ??= SelectedItem;
                    int count = await Task.Run(async () => await PermAndAddMultiple(parameter, PermMode.StrikeUp, PermSide, Count, MaintainBaseStrategyOnPerm));
                    stopwatch.Stop();
                    _log.Info(nameof(StrikeUp) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms, Generated: " + count);
                }
            }
            catch (ArgumentNullException)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage("No item selected for permutation.", "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikeUp));
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        [Command]
        public async Task StrikeDown(object parameter)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BasketStateChange();
                if (PermSelf)
                {
                    await StrikeDownSelf();
                    stopwatch.Stop();
                    _log.Info(nameof(StrikeDown) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms.");
                }
                else
                {
                    if (SelectedItems.Count != 0)
                    {
                        parameter = SelectedItems;
                    }
                    parameter ??= SelectedItem;
                    int count = await Task.Run(async () => await PermAndAddMultiple(parameter, PermMode.StrikeDown, PermSide, Count, MaintainBaseStrategyOnPerm));
                    stopwatch.Stop();
                    _log.Info(nameof(StrikeDown) + " Complete. Ellapsed: " + stopwatch.ElapsedMilliseconds + " ms, Generated: " + count);
                }
            }
            catch (ArgumentNullException)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage("No item selected for permutation.", "Permutation Failed - ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikeDown));
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        [Command]
        public void RowDoubleClick(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is BasketTraderItemModel orderModel)
            {
                OpenInComplexOrderTicket(orderModel);
            }
        }

        [Command]
        public void OpenInComplexOrderTicket(OrderTicket orderModel)
        {
            try
            {
                CreateComplexOrderTicket(orderModel: orderModel, resetEvent: null, isClosing: false, isContraTicket: false, local: true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        [Command]
        public void ResetAutomationCommand(OrderTicket orderModel)
        {
            try
            {
                orderModel?.ResetAutomation();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetAutomationCommand));
            }
        }

        [Command]
        public void SearchTimeAndSalesCommand(OrderTicket orderModel)
        {
            try
            {
                orderModel?.SearchTimeAndSalesCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchTimeAndSalesCommand));
            }
        }

        [Command]
        public void ImmediateAutoTraderUpdateCommand()
        {
            if (!IsDisposed)
            {
                ApplyAutoTraderChanges();
            }
        }

        [Command]
        public void UpdateBasketManager()
        {
            try
            {
                if (!IsDisposed)
                {
                    if (OmsCore.Config.EnableBasketManagerClientV2 && !string.IsNullOrEmpty(Uid) && BasketType != BasketType.LockTrader && OmsCore.BasketManagerClient.IsConnected)
                    {
                        SetTitle();
                        Username = OmsCore.User.Username;
                        Task.Run(() => OmsCore.BasketManagerClient.Update(this));
                    }

                    if (!AutoConfigUpdated)
                    {
                        ApplyAutoTraderChanges();
                    }
                    else if (!OmsCore.AutoTraderClient.IsConnected)
                    {
                        AutoConfigUpdated = false;
                        BasketAutomationStatusReady = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateBasketManager));
            }
        }

        [Command]
        public void StopAllLoops()
        {
            GetAutomationConfig().CloseOrderMode = null;
        }

        [Command]
        public void PasteMorphSymbolsFromClipboard()
        {
            MorphSymbolsQuery = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        [Command]
        public async Task SearchMorphSymbols()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(MorphSymbolsQuery))
                {
                    IEnumerable<string> symbols = MorphSymbolsQuery.Replace(",", ";")
                                                   .Split(';')
                                                   .Where(x => !string.IsNullOrWhiteSpace(x))
                                                   .Select(x => x.Trim().ToUpper())
                                                   .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                                                   .Distinct();

                    List<Task> getOptionsTasks = new();
                    ConcurrentBag<string> validSymbols = new();
                    foreach (string symbol in symbols)
                    {
                        Task task = OmsCore.QuoteClient.GetSymbolsAsync(symbol)
                                                      .ContinueWith(t =>
                                                      {
                                                          if (t.Result.Count > 0)
                                                          {
                                                              validSymbols.Add(symbol);
                                                          }
                                                      });
                        getOptionsTasks.Add(task);
                    }
                    await Task.WhenAll(getOptionsTasks);
                    await Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        foreach (string symbol in validSymbols)
                        {
                            if (!MorphSymbols.Any(x => x == symbol))
                            {
                                MorphSymbols.Add(symbol);
                            }
                        }
                        MorphSummary = "Loaded: " + MorphSymbols.Count();
                    }));

                    _log.Info("Morph symbols loaded! Query: {}, Loaded: {}, Valid: {}", MorphSymbolsQuery, symbols.Count(), validSymbols.Count());
                }
                else
                {
                    _log.Warn("Morph load failed! Invalid input symbol.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchMorphSymbols));
            }
        }

        [Command]
        public void RemoveMorphSymbols(string symbol)
        {
            if (MorphSymbols.Any(x => x == symbol))
            {
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    MorphSymbols.Remove(symbol);
                }));
                MorphSummary = "Loaded: " + MorphSymbols.Count();
            }
        }

        [Command]
        public void MorphSpreads()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    SearchMorphSymbols().ContinueWith(t =>
                    {
                        if (MorphSymbols.Count > 0)
                        {
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
                                    _log.Info("Morph basket ready!");
                                    viewModel.Ready -= OnReady;
                                    viewModel.LoadMorph(BasketItems.ToList(), MorphSymbols.ToList());
                                }
                            }
                            else
                            {
                                _log.Warn("Morph failed! Invalid basket.");
                            }
                        }
                        else
                        {
                            _log.Warn("Morph failed! No symbol loaded.");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(MorphSpreads));
            }
        }

        [Command]
        public void OpenInVolTraderCommand(VolTraderViewModel volTrader)
        {
            if (volTrader == null)
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    VolTraderView window = new();
                    volTrader = (VolTraderViewModel)window.DataContext;
                    volTrader.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (s, e) => _volTradersManager.AddBasketToVolTrader(this, volTrader);

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
            else
            {
                _volTradersManager.AddBasketToVolTrader(this, volTrader);
            }
        }

        internal bool ActiveEdgeScanFeedOrderExistsForDescription(OrderTicket orderBase, out OrderTicket order)
        {
            if (IsEdgeScanFeedAutoTrader)
            {
                lock (_activeOrdersLock)
                {
                    if (_activeOrderDescriptions.TryGetValue(orderBase.SpreadId, out order))
                    {
                        if (order.TraderSpreadPosition != 0 || order.IsActive || !order.OrderClosedEventIsSet)
                        {
                            return true;
                        }
                    }
                    _activeOrderDescriptions[orderBase.SpreadId] = orderBase;
                    return false;
                }
            }
            else
            {
                order = null;
                return false;
            }
        }

        internal void RemoveActiveEdgeScanFeedOrderForDescription(OrderTicket orderBase, OrderStatus orderStatus)
        {
            try
            {
                if (IsEdgeScanFeedAutoTrader && !orderBase.IsActive && (orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.Rejected || orderStatus == OrderStatus.Filled))
                {
                    if (_activeOrderDescriptions.TryRemove(orderBase.SpreadId, out OrderTicket order))
                    {
                        _log.Info(nameof(RemoveActiveEdgeScanFeedOrderForDescription) + " active order removed for " + orderBase.SpreadId);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveActiveEdgeScanFeedOrderForDescription));
            }
        }

        public bool IsValidRoute(string underlying = null, bool checkSingle = true, bool checkSpread = true)
        {
            bool isValidRoute = true;
            return isValidRoute;
        }

        private void RegisterEvents(BasketTraderItemModel item)
        {
            item.TradeEvent += OnTrade;
            item.OrderFilledUpdatedEvent += OnOrderFilledEvent;
            item.OrderClosedUpdateEvent += OnOrderClosedEvent;
            item.EdgeAcquiredEvent += OnEdgeAcquired;
            item.ActivateWindow += OnActivateWindowRequest;
        }

        private void UnregisterEvents(BasketTraderItemModel item)
        {
            try
            {
                item.TradeEvent -= OnTrade;
                item.OrderFilledUpdatedEvent -= OnOrderFilledEvent;
                item.OrderClosedUpdateEvent -= OnOrderClosedEvent;
                item.EdgeAcquiredEvent -= OnEdgeAcquired;
                item.ActivateWindow -= OnActivateWindowRequest;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnregisterEvents));
            }
        }

        private void OnActivateWindowRequest()
        {
            try
            {
                Activate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnActivateWindowRequest));
            }
        }

        private BasketTraderItemModel MakeBasketItemModel() => new(this, Dispatcher, OmsCore);

        public async void LoadMorph(List<BasketTraderItemModel> basketTraderItemModels, List<string> morphList)
        {
            try
            {
                _log.Info("Loading morph! Items: {}, Symbols: {}\nSpreads: {}\nSymbols: {}", basketTraderItemModels.Count, morphList.Count, string.Join(",\n", basketTraderItemModels.Select(x => x.SpreadId)), string.Join(",\n", morphList));
                List<Task<OrderTicket>> morphTasks = new();
                List<DataStore> deltaStores = new();

                await Dispatcher.BeginInvoke(async () =>
                {
                    foreach (string symbol in morphList)
                    {
                        List<Data.Securities.Option> options = await OmsCore.QuoteClient.GetSymbols(symbol);
                        _log.Info("Basket morph requesting symbols for {}. Got: {}!", symbol, options.Count);
                        DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        deltaStores.Add(deltaStore);
                        deltaStore.GetHanweckDataFor(options, SubscriptionFieldType.Delta);

                        foreach (BasketTraderItemModel basketItemTemplate in basketTraderItemModels)
                        {
                            BasketTraderItemModel item = MakeBasketItemModel();
                            Task<OrderTicket> loadTask = Task.Run(async () => await item.LoadMorphFromOrderAsync(basketItemTemplate, options, deltaStore));
                            morphTasks.Add(loadTask);
                        }
                    }
                });

                await Task.WhenAll(morphTasks).ContinueWith(morphTaskResults =>
                {
                    List<BasketTraderItemModel> morphResult = new();
                    foreach (Task<OrderTicket> task in morphTasks)
                    {
                        try
                        {
                            if (task.Result != null)
                            {
                                BasketTraderItemModel basketItem = (BasketTraderItemModel)task.Result;
                                morphResult.Add(basketItem);
                            }
                        }
                        catch (AggregateException ae)
                        {
                            foreach (Exception ex in ae.InnerExceptions)
                            {
                                _log.Error(ex, nameof(LoadMorph));
                            }
                        }
                    }

                    _log.Warn("Morph complete! Results:{}\n{}.", morphResult.Count, string.Join(",\n", morphResult.Select(x => x.SpreadId)));
                    _ = AddMultipleToBasketAsync(morphResult);

                    foreach (DataStore deltaStore in deltaStores)
                    {
                        deltaStore.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadMorph));
            }
        }

        public void ReverseSidesNoCheck()
        {
            _submitIndex = 0;
            Parallel.ForEach(BasketItems.Where(x => x.Active), order =>
            {
                order.Reverse();
                _ = SetEdgeAsync(order);
            });
        }

        private void ToggleEdgeWarning(string message, string title)
        {
            if (OmsCore.Config.ShowNegativeEdgeWarning)
            {
                ShowEdgeWarning = true;
                ShowMessageFromItem(message, title, canBeSilenced: true);
            }
        }

        public void FlipCpNoCheck()
        {
            _submitIndex = 0;
            Parallel.ForEach(BasketItems.Where(x => x.Active), order =>
            {
                order.FlipCP();
            });
        }

        public void OppCpNoCheck()
        {
            _submitIndex = 0;
            Parallel.ForEach(BasketItems.Where(x => x.Active), order =>
            {
                _ = order.OppCP();
            });
        }

        public void ResetTimerNoCheck()
        {
            if (ResubmitOnTimer && _resubmitCountdownTimer.IsEnabled)
            {
                StartResubmitTimer();
            }
            else if (ModifyOnTimer && _modifyCountdownTimer.IsEnabled)
            {
                StartModifyTimer();
            }
        }

        public void CancelAllNoCheck()
        {
            try
            {
                _submitIndex = 0;
                _log.Info("Cancel All Active Basket Orders. Basket ID: " + InstanceId);
                if (GetInstanceMode().IsAutoTraderInstance())
                {
                    OmsCore.AutoTraderClient.CancelGroup(Uid);
                }
                Parallel.ForEach(BasketItems, order => CancelOrder(order));
                if (!IsDisposed && BasketSettings.SubmitWithDelayEnabled && BasketSettings.SubmitWithDelayInterval > 0)
                {
                    CancelQueuedSubmitWithDelay();
                }
                _timerCts.Cancel();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllNoCheck));
            }
        }

        private static void CancelOrder(BasketTraderItemModel order)
        {
            try
            {
                order.ResetAutomation();
                if (order.MainResting)
                {
                    order.CancelAsync();
                }
                if (order.ContraResting)
                {
                    order.CancelContraAsync();
                }

                order.InvokeLoopCommand(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelOrder));
            }
        }

        public async Task ModifyAllNoCheck()
        {
            await ModifyAllNoCheck(false);
        }

        public async Task ModifyAllNoCheck(bool fromTimer)
        {
            if (IsDisposed || _modifyAllRunning)
            {
                return;
            }
            _modifyAllRunning = true;
            CancellationToken token = _timerCts.Token;
            try
            {
                StartModifyTimer();
                if (!IsDisposed && BasketSettings.SubmitWithDelayEnabled && BasketSettings.SubmitWithDelayInterval > 0)
                {
                    List<Tuple<int, object>> basketItems = GetBasketItemsByVisualOrder();
                    foreach (Tuple<int, object> indexOrderPair in basketItems)
                    {
                        int index = indexOrderPair.Item1;
                        BasketTraderItemModel order = (BasketTraderItemModel)indexOrderPair.Item2;

                        if (token.IsCancellationRequested || IsDisposed)
                        {
                            break;
                        }
                        if (order.Active)
                        {
                            Status = "Modifying Row: " + index;
                            var success = await CheckForRiskAndModifyOrder(order, fromTimer);
                            if (success)
                            {
                                var delay = GetSubmitDelay();
                                if (delay > 0)
                                {
                                    await Task.Delay(delay);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Parallel.ForEach(BasketItems.Where(x => x.Active), order =>
                    {
                        _ = CheckForRiskAndModifyOrder(order, fromTimer);
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifyAllNoCheck));
            }
            _modifyAllRunning = false;
            Status = "";
            if (ModifyOnTimer && ModifyCountDown.Ticks == 0 && !token.IsCancellationRequested)
            {
                _ = ModifyAllNoCheck(fromTimer: true);
            }
        }

        public async Task SubmitAllNoCheckSafe()
        {
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                await Dispatcher.BeginInvoke(() => SubmitAllNoCheck());
            }
        }

        public async Task SubmitAllNoCheck()
        {
            if (IsDisposed || (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2))
            {
                return;
            }

            if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit)
            {
                return;
            }

            if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit)
            {
                return;
            }
            _lastActivityTime = DateTime.Now;
            SubmittedCount = 0;
            FailedCount = 0;
            FillsCount = 0;
            CancelQueuedSubmitWithDelay();
            _submitWithDelayCancellationTokenSource = new CancellationTokenSource();
            _timerCts.Cancel();
            _timerCts = new CancellationTokenSource();

            CancellationToken token = _submitWithDelayCancellationTokenSource.Token;
            CancellationToken timerToken = _timerCts.Token;
            try
            {
                StartResubmitTimer();
                StartModifyTimer();
                if (!IsDisposed && BasketSettings.SubmitWithDelayEnabled && BasketSettings.SubmitWithDelayInterval > 0 && !SubmitAllRunning)
                {
                    SubmitAllRunning = true;

                    List<Tuple<int, object>> basketItems = GetBasketItemsByVisualOrder();
                    if (!BasketSettings.Resume)
                    {
                        _submitIndex = 0;
                    }

                    string id = null;
                    for (; _submitIndex < basketItems.Count; _submitIndex++)
                    {
                        try
                        {
                            id = null;
                            await WaitForRestingOrders(token);
                            if (token.IsCancellationRequested || IsDisposed)
                            {
                                break;
                            }
                            Tuple<int, object> indexOrderPair = basketItems[_submitIndex];
                            int index = indexOrderPair.Item1;
                            BasketTraderItemModel order = (BasketTraderItemModel)indexOrderPair.Item2;
                            id = order?.SpreadId;
                            if (order.Active)
                            {
                                if ((SkipRestingOrders && (order.MainResting || order.ContraResting)) || order.IsActive)
                                {
                                    continue;
                                }

                                Status = "Active Row: " + index;

                                var sucess = await order.SubmitAsync();

                                if (sucess)
                                {
                                    SubmittedCount++;
                                    var delay = GetSubmitDelay();
                                    if (delay > 0)
                                    {
                                        await Task.Delay(delay);
                                    }

                                    if (BasketSettings.AlertWhenGettingNoFill && SubmittedCount >= BasketSettings.AlertWhenGettingNoFillCount && FillsCount == 0)
                                    {
                                        var cont = await GetVerificationAsync($"You have submitted {SubmittedCount} orders with no fills!\nDo you want to continue?", "No Fill Submission Alert");
                                        SubmittedCount = 0;
                                        FillsCount = 0;
                                        if (!cont)
                                        {
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    FailedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, nameof(SubmitAllNoCheck) + " Index: " + _submitIndex + " Id: " + id);
                            FailedCount++;
                        }
                    }

                    if (!BasketSettings.Resume || _submitIndex >= basketItems.Count)
                    {
                        _submitIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitAllNoCheck));
            }
            SubmitAllRunning = false;
            Status = "";
            if (ResubmitOnTimer && ResubmitCountDown.Ticks == 0 && !token.IsCancellationRequested && !timerToken.IsCancellationRequested)
            {
                _ = SubmitAllNoCheck();
            }
            else if (ModifyOnTimer && ModifyCountDown.Ticks == 0 && !token.IsCancellationRequested && !timerToken.IsCancellationRequested)
            {
                _ = ModifyAllNoCheck(fromTimer: true);
            }
        }

        private List<Tuple<int, object>> GetBasketItemsByVisualOrder()
        {
            List<Tuple<int, object>> basketItems = GetItemsByVisualOrderService.GetItemsByVisualOrder(BasketSettings.StartProcessingFromSelectedRow);
            if (!IsDisposed && BasketSettings.Randomize)
            {
                ListHelper.Shuffle(basketItems);
            }

            return basketItems;
        }

        private async Task WaitForRestingOrders(CancellationToken token)
        {
            if (!IsDisposed && BasketSettings.MaxRestingOrdersEnabled)
            {
                await WaitForResting(token, minSize: 0, maxCount: BasketSettings.MaxRestingOrdersCount);
            }

            if (!IsDisposed && BasketSettings.DisableMultipleRestingSizeOrders)
            {
                await WaitForResting(token, minSize: 1, maxCount: 0);
            }
        }

        private async Task WaitForResting(CancellationToken token, int minSize, int maxCount)
        {
            var count = 0;
            int interval = Math.Max(500, BasketSettings.CancelWithTimerEnabled ? (int)BasketSettings.CancelWithTimer : 500);
            var logCountMultiplier = Math.Max(50, 5000 / interval);
            while (GetRestingOrdersCount(minSize) >= maxCount)
            {
                if (token.IsCancellationRequested || IsDisposed)
                {
                    break;
                }
                _orderSubmitResetEvent.Reset();
                bool success = await Task.Run(() => _orderSubmitResetEvent.WaitOneAsync(token, interval));
                if (!success)
                {
                    count++;
                    if (count % logCountMultiplier == 0)
                    {
                        _log.Warn($"Delay on wait for resting orders. Id: {BasketSettings?.Uid}, Title: {ModuleTitle}, Count: {count}");
                    }
                    continue;
                }
                if (token.IsCancellationRequested || IsDisposed)
                {
                    break;
                }
            }
        }

        public int GetRestingOrdersCount(int minSize = 0)
        {
            return GetBasketRestingOrdersCount(minSize);
        }

        private async Task<bool> CheckForRiskAndModifyOrder(BasketTraderItemModel order, bool isFromTimer)
        {
            if (!order.MainResting || !order.CanReplace || order.IsActive)
            {
                return false;
            }
            if (!IsDisposed && BasketSettings.AdjustPriceBeforeSubmit)
            {
                await SetEdgeAsync(order);
                if (isFromTimer)
                {
                    order.CorrectIfDuplicateLastPx();
                }
            }
            return await order.ModifyAsync();
        }

        private void StartResubmitTimer()
        {
            _resubmitCountdownTimer.Stop();
            Dispatcher?.Invoke(() => _resubmitCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) });
            _resubmitCountdownTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
            _elapsedResubmitTime = TimeSpan.Zero;
            _resubmitCountdownTimer.Tick += ResubmitTimer_Tick;

            if (ResubmitOnTimer)
            {
                _resubmitCountdownTimer.Start();
            }
        }

        private void StartModifyTimer()
        {
            _modifyCountdownTimer.Stop();
            Dispatcher?.Invoke(() =>
            {
                if (ModifyIntervalSec >= 1)
                {
                    _modifyCountdownTimer.Interval = TimeSpan.FromSeconds(1);
                }
                else
                {
                    _modifyCountdownTimer.Interval = TimeSpan.FromMilliseconds(50);
                }
            });
            _modifyCountdownTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _elapsedModifyTime = TimeSpan.Zero;
            ModifyCountDown = TimeSpan.FromSeconds(ModifyIntervalSec);
            _modifyCountdownTimer.Tick += ModifyCountdownTimer_Tick;

            if (ModifyOnTimer)
            {
                _modifyCountdownTimer.Start();
            }
        }

        private void ModifyCountdownTimer_Tick(object sender, EventArgs args)
        {
            if (!ModifyOnTimer ||
                IsDisposed ||
                _timerCts.Token.IsCancellationRequested ||
                !BasketItems.Any(x => x.MainResting))
            {
                _modifyCountdownTimer.Stop();
                _modifyCountdownTimer.Tick -= ModifyCountdownTimer_Tick;
                _elapsedModifyTime = TimeSpan.Zero;
                ModifyCountDown = TimeSpan.FromSeconds(ModifyIntervalSec);
                return;
            }

            _elapsedModifyTime += _modifyCountdownTimer.Interval;

            if (_elapsedModifyTime >= TimeSpan.FromSeconds(ModifyIntervalSec))
            {
                _modifyCountdownTimer.Stop();
                _modifyCountdownTimer.Tick -= ModifyCountdownTimer_Tick;

                if (!_modifyAllRunning)
                {
                    _ = ModifyAllNoCheck(fromTimer: true);
                    _elapsedModifyTime = TimeSpan.Zero;
                }
                else
                {
                    ModifyCountDown = TimeSpan.Zero;
                }
            }
            else
            {
                ModifyCountDown = TimeSpan.FromSeconds(ModifyIntervalSec) - _elapsedModifyTime;
            }
        }

        private void ResubmitTimer_Tick(object sender, EventArgs e)
        {
            if (!ResubmitOnTimer || IsDisposed || _timerCts.Token.IsCancellationRequested)
            {
                ResetResubmitCountdown();
                return;
            }
            _elapsedResubmitTime += _resubmitCountdownTimer.Interval;

            if (_elapsedResubmitTime >= TimeSpan.FromSeconds(ResubmitIntervalSec))
            {
                _resubmitCountdownTimer.Stop();
                if (!SubmitAllRunning)
                {
                    if (_resubmitCount++ < ResubmitIntervalCount)
                    {
                        _ = SubmitAllNoCheck();
                        _elapsedResubmitTime = TimeSpan.Zero;
                    }
                    else
                    {
                        ResetResubmitCountdown();
                    }
                }
            }
            else
            {
                ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec) - _elapsedResubmitTime;
            }
        }

        private void ResetResubmitCountdown()
        {
            _resubmitCountdownTimer.Stop();
            _resubmitCountdownTimer.Tick -= ResubmitTimer_Tick;
            _elapsedResubmitTime = TimeSpan.Zero;
            _resubmitCount = 0;
            ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
        }

        private void OnOrderClosedEvent(IOmsOrder order, OrderStatus orderStatus, OrderTicket ticket)
        {
            RemoveActiveEdgeScanFeedOrderForDescription(ticket, orderStatus);
            NotifyOrderCloseListeners();
        }

        internal void NotifyOrderCloseListenersSafe()
        {
            Dispatcher.Invoke(() =>
            {
                NotifyOrderCloseListeners();
            });
        }

        private void NotifyOrderCloseListeners()
        {
            _orderSubmitResetEvent.Set();
        }

        private void OnOrderFilledEvent(OrderTicket changedOrder, OrderStatus orderStatus)
        {
            if (!IsDisposed && BasketSettings.SubmitWithDelayEnabled && BasketSettings.SubmitWithDelayInterval > 0 && !IsEdgeScanFeedAutoTrader)
            {
                FillsCount++;
                if (++_fillCount >= BasketSettings.CancelOnAmountOfFillsCount)
                {
                    CancelQueuedOrders();
                }
            }

            if (!IsDisposed && BasketSettings.OpenTicketForFills)
            {
                Task.Run(() => OpenOrActivateTicket(changedOrder));
            }

            if (ResubmitOnTimer && ActivateWindowOnResubmitFill)
            {
                OnActivateWindowRequest();
            }
        }

        private void OpenOrActivateTicket(OrderTicket changedOrder)
        {
            lock (_openTicketLock)
            {
                if (_spreadIdToOpenedTicketsMap.TryGetValue(changedOrder.SpreadId, out Window ticketView))
                {
                    ticketView.Dispatcher?.BeginInvoke(() =>
                    {
                        if (ticketView.IsLoaded)
                        {
                            ticketView.Activate();
                        }
                    });
                }
                else
                {
                    CreateComplexOrderTicketSync(changedOrder);
                }
            }
        }

        internal void CancelQueuedOrdersSafe()
        {
            try
            {
                Dispatcher?.BeginInvoke(() => CancelQueuedOrders());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelQueuedOrdersSafe));
            }
        }

        private void CancelQueuedOrders()
        {
            try
            {
                if (CanCancelQueuedOrders())
                {
                    _log.Info("Cancel All Basket Orders. Basket ID: " + InstanceId);
                    CancelQueuedSubmitWithDelay();
                    Parallel.ForEach(BasketItems.Where(x => x.Active), order =>
                    {
                        order.CancelAsync();
                    });
                }
                else
                {
                    _log.Info("Cancel All Basket Orders Not Allowed. Basket ID: " + InstanceId);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelQueuedOrders));
            }
        }

        protected virtual bool CanCancelQueuedOrders()
        {
            return true;
        }

        public void RemoveClosedOrders()
        {
            try
            {
                if (Monitor.TryEnter(_basketCleanLock, 10))
                {
                    try
                    {
                        if (IsDisposed || BasketItems == null)
                        {
                            return;
                        }

                        foreach (BasketTraderItemModel basketItem in BasketItems.ToList())
                        {
                            if (basketItem.Legs.Any(x => x.NetQty == 0))
                            {
                                Dispatcher?.BeginInvoke(new Action(() =>
                                {
                                    BasketItems.Remove(basketItem);
                                }));
                            }
                        }
                    }
                    finally
                    {
                        Monitor.Exit(_basketCleanLock);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveClosedOrders));
            }
        }

        public async void OnEdgeAcquired(OrderTicket order, double lastEdge, double lastEdgeAfterFees)
        {
            try
            {
                lastEdge = lastEdge.Round(2);
                lastEdgeAfterFees = lastEdgeAfterFees.Round(2);

                if (lastEdgeAfterFees < 0)
                {
                    _timerCts.Cancel();
                }

                if (AutoPermOnFill)
                {
                    OmsCore.AutoPermTelemetryService.Begin(order.PermID, order.SpreadId, InstanceId, lastEdge, order.PermGen + 1);
                    AutoPermConfigModel selectedConfig = SelectAutoPermConfig(order, lastEdge, out var preExistingOnly);

                    if (selectedConfig != null)
                    {
                        int gen = order.PermGen + 1;
                        if (gen <= selectedConfig.MaxGenForPerms)
                        {
                            OmsCore.AutoPermTelemetryService.RecordConfigSelection(order.PermID, "ConfigFound", "", selectedConfig.Edge);
                            await LoadAutoPerms(order, lastEdge, selectedConfig, sendOrders: true, preExistingOnly: preExistingOnly);
                        }
                        else
                        {
                            OmsCore.AutoPermTelemetryService.RecordConfigSelection(order.PermID, "MaxGenReached", "", selectedConfig.Edge);
                            OmsCore.AutoPermTelemetryService.Complete(order.PermID);
                            _log.Info($"{nameof(OnEdgeAcquired)} Max Perm Generation reached. {order.SpreadId}, Gen: {gen}");
                        }
                    }
                    else
                    {
                        OmsCore.AutoPermTelemetryService.RecordConfigSelection(order.PermID, "NotFound", "", double.NaN);
                        OmsCore.AutoPermTelemetryService.Complete(order.PermID);
                    }
                }
                else
                {
                    _log.Info($"{nameof(OnEdgeAcquired)} Auto Perm Disabled. " +
                        $"Basket Id: {InstanceId}, " +
                        $"Order Id: {order.SpreadId}, " +
                        $"Last Edge : {lastEdge}.");
                }

                if (BasketSettings.AlertWhenGettingCloseEdge && lastEdge >= order.GetClosingEdge(false))
                {
                    order.ShowAlert();
                }

                if (!IsDisposed && BasketSettings.OpenTicketOnEdgeAcquired)
                {
                    _ = Task.Run(() => OpenOrActivateTicket(order));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnEdgeAcquired));
            }
        }

        public AutoPermConfigModel SelectAutoPermConfig(OrderTicket order, double lastEdge, out bool preExistingOnly)
        {
            preExistingOnly = false;
            var automationConfig = GetAutomationConfig();
            if (automationConfig != null && automationConfig.AutoPermConfigModel != null && automationConfig.AutoPermConfigModel.AutoPermConfigs != null)
            {
                preExistingOnly = automationConfig.AutoPermConfigModel.SubmitExistingItemsOnly;
                return SelectAutoPermConfig(order, lastEdge, automationConfig.AutoPermConfigModel.AutoPermConfigs, automationConfig.AutoPermConfigModel.AutoPermSelectionMode);
            }
            else
            {
                return null;
            }
        }

        public static AutoPermConfigModel SelectAutoPermConfig(OrderTicket order, double lastEdge, IEnumerable<AutoPermConfigModel> autoPermConfigs, AutoPermSelectionMode autoPermSelectionMode)
        {
            string message = "Order Id: " + order.SpreadId + ", " + "Last Edge Before Fees: " + lastEdge + ".";
            List<AutoPermConfigModel> matchingConfigs = new();
            IEnumerable<AutoPermConfigModel> validConfigs = autoPermConfigs.Where(x => x.Enabled && x.AutoPermTemplate != null && x.AutoPermTemplate.Perms.Count > 0);
            foreach (AutoPermConfigModel config in validConfigs)
            {
                if (config.IsMatch(lastEdge, order.DaysToExpiration))
                {
                    matchingConfigs.Add(config);
                }
            }

            AutoPermConfigModel selectedConfig = null;
            if (matchingConfigs.Count == 1)
            {
                selectedConfig = matchingConfigs.FirstOrDefault();
                _log.Info(nameof(SelectAutoPermConfig) + " Single Config Found." + message);
            }
            else if (matchingConfigs.Count > 1)
            {
                switch (autoPermSelectionMode)
                {
                    case AutoPermSelectionMode.Closest:
                        selectedConfig = matchingConfigs.MinBy(x => lastEdge - x.Edge);
                        break;
                    case AutoPermSelectionMode.Highest:
                        selectedConfig = matchingConfigs.OrderByDescending(x => x.Edge).FirstOrDefault();
                        break;
                }
                _log.Info(nameof(SelectAutoPermConfig) + " " + autoPermSelectionMode + " Config Selected." + message);
            }
            else
            {
                _log.Info(nameof(SelectAutoPermConfig) + " Config Not Found." + message);
            }

            return selectedConfig;
        }

        public async Task LoadAutoPerms(OrderTicket orderBase, double lastEdge, AutoPermConfigModel selectedConfig, bool sendOrders = true, bool preExistingOnly = false, bool queuePerms = true, bool activate = false)
        {
            BasketTraderItemModel order = orderBase as BasketTraderItemModel;

            if (order == null)
            {
                _log.Info(nameof(LoadAutoPerms) + " Invalid reference order. " + orderBase?.SpreadId);
                OmsCore.AutoPermTelemetryService.Complete(orderBase?.PermID);
                return;
            }

            List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> permPairs = await LoadPerms(order, lastEdge, selectedConfig, activate);
            int permsLoadedCount = permPairs.Count;

            if (preExistingOnly)
            {
                HashSet<string> spreads = null;
                await Dispatcher.BeginInvoke(() => spreads = BasketItems.Select(x => x.SpreadId).ToHashSet());
                if (spreads != null)
                {
                    permPairs = permPairs.Where(x => spreads.Contains(x.Item1.SpreadId)).ToList();
                }
            }

            if (permPairs.Count > selectedConfig.MaxNumberOfPerms)
            {
                permPairs = permPairs.Take(selectedConfig.MaxNumberOfPerms).ToList();
            }

            object syncLock = new();
            bool CheckForPermWithSize()
            {
                lock (syncLock)
                {
                    int openCount = 0;
                    int initialSizeForPerms = Math.Max(selectedConfig.InitialSizeForAutoPerms, selectedConfig.HardSideInitialSizeForAutoPerms);
                    foreach (Tuple<BasketTraderItemModel, BasketTraderItemModel> pair in permPairs)
                    {
                        BasketTraderItemModel item = pair.Item1;
                        if (item.Lcd > initialSizeForPerms)
                        {
                            if (++openCount > selectedConfig.MaxOpenPerms)
                            {
                                return false;
                            }
                        }
                        item = pair.Item2;
                        if (item != null)
                        {
                            if (item.Lcd > initialSizeForPerms)
                            {
                                if (++openCount > selectedConfig.MaxOpenPerms)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
                return true;
            }
            ;

            await CalculatePermAdjPxAsync(order as BasketTraderItemModel, permPairs);

            List<BasketTraderItemModel> allPerms = permPairs.SelectMany(x => (new[] { x.Item1, x.Item2 })).Where(x => x != null).Distinct().ToList();
            List<Task<bool>> greekLoadTasks = BasketSettings.SubscribeToHanweck ? allPerms.Select(x => x.WaitForTheoLoadAsync()).ToList() : null;

            if (greekLoadTasks != null)
            {
                await Task.WhenAll(greekLoadTasks);
            }

            List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> pairs;

            if (greekLoadTasks != null && greekLoadTasks.All(x => x.IsCompletedSuccessfully && x.Result))
            {
                pairs = permPairs.OrderBy(x => Math.Abs(x.Item1.TotalVega - order.TotalVega)).ToList();
                _log.Info("{} perms sorted by vega diff. Count: {}", order.SpreadId, pairs.Count);
                OmsCore.AutoPermTelemetryService.RecordSortMethod(order.PermID, "Vega");
            }
            else
            {
                pairs = permPairs.ToList();
                _log.Info("{} perms loaded unordered. Count: {}", order.SpreadId, pairs.Count);
                OmsCore.AutoPermTelemetryService.RecordSortMethod(order.PermID, "Unordered");
            }

            string spreadHash = order.SpreadId.Md5Hash();
            foreach (Tuple<BasketTraderItemModel, BasketTraderItemModel> pair in pairs)
            {
                BasketTraderItemModel item = pair.Item1;
                BasketTraderItemModel coItem = pair.Item2;
                if (TransactionConsumer.TryGetLastTradeTime(item.SpreadId, out DateTime lastTradeTime))
                {
                    if ((DateTime.Now - lastTradeTime).TotalSeconds < OmsCore.Config.AutoPermTradeLookback)
                    {
                        _log.Info($"{nameof(LoadAutoPerms)} recent trade check failed, " +
                                  $"Original: {order.SpreadId}, " +
                                  $"Perm : {item.SpreadId}.");
                        OmsCore.AutoPermTelemetryService.RecordFilterRejection(order.PermID, "RecentTrade");
                        permPairs.Remove(pair);
                        item.Dispose();
                        coItem?.Dispose();
                        continue;
                    }
                }

                if (selectedConfig.MinDelta > 0 && selectedConfig.MaxDelta < 1)
                {
                    await item.WaitForTheoLoadAsync();
                    double delta = Math.Abs(item.TotalDelta);
                    if (delta < selectedConfig.MinDelta ||
                        delta > selectedConfig.MaxDelta)
                    {
                        _log.Info($"{nameof(LoadAutoPerms)} delta check failed, " +
                                  $"Original: {order.SpreadId}, " +
                                  $"Perm : {item.SpreadId}, " +
                                  $"Delta: {delta}, " +
                                  $"Delta: {selectedConfig.MinDelta}-{selectedConfig.MaxDelta}, " +
                                  $"Dte: {selectedConfig.MinDte}-{selectedConfig.MaxDte}");
                        OmsCore.AutoPermTelemetryService.RecordFilterRejection(order.PermID, "Delta");
                        permPairs.Remove(pair);
                        item.Dispose();
                        coItem?.Dispose();
                        continue;
                    }
                }

                if (selectedConfig.MaxDeltaAddition > 0 && selectedConfig.MaxDeltaAddition < 1)
                {
                    Task[] tasks = new Task[] { item.WaitForTheoLoadAsync(), order.WaitForTheoLoadAsync() };
                    await Task.WhenAll(tasks);
                    double delta = Math.Abs(item.TotalDelta);
                    double currentDelta = Math.Abs(order.TotalDelta);
                    if (delta > currentDelta + selectedConfig.MaxDeltaAddition)
                    {
                        _log.Info($"{nameof(LoadAutoPerms)} add delta check failed, " +
                                  $"Original: {order.SpreadId}, " +
                                  $"Perm : {item.SpreadId}, " +
                                  $"Delta: {delta}, " +
                                  $"Current Delta: {currentDelta}, " +
                                  $"Max Delta: {selectedConfig.MaxDeltaAddition}.");
                        OmsCore.AutoPermTelemetryService.RecordFilterRejection(order.PermID, "AddDelta");
                        permPairs.Remove(pair);
                        item.Dispose();
                        coItem?.Dispose();
                        continue;
                    }
                }

                if (selectedConfig.MaxLegDeltaDiff > 0 && selectedConfig.MaxLegDeltaDiff < 1)
                {
                    bool proceed = true;
                    Task[] tasks = new Task[] { item.WaitForTheoLoadAsync(), order.WaitForTheoLoadAsync() };
                    await Task.WhenAll(tasks);
                    var baseLegs = order.Legs.OrderBy(x => x.Strike).ToArray();
                    var permLegs = item.Legs.OrderBy(x => x.Strike).ToArray();
                    if (baseLegs.Length != permLegs.Length)
                    {
                        continue;
                    }
                    else
                    {
                        for (var index = 0; index < permLegs.Length; index++)
                        {
                            TicketLegModel baseLeg = baseLegs[index];
                            TicketLegModel permLeg = permLegs[index];

                            double delta = Math.Abs(permLeg.Delta);

                            if (baseLeg == null)
                            {
                                _log.Warn($"{nameof(LoadAutoPerms)} leg delta check match failed, " +
                                          $"Original: {order.SpreadId}, " +
                                          $"Perm : {item.SpreadId}, " +
                                          $"Delta: {delta}, " +
                                          $"Max Delta: {selectedConfig.MaxDeltaAddition}, " +
                                          $"Msg: {$" Basket Id: {InstanceId}. Order Id: {order.SpreadId}, Last Edge Before Fees: {lastEdge}."}");
                            }
                            else
                            {
                                double currentDelta = Math.Abs(baseLeg.Delta);
                                if (Math.Abs(delta - currentDelta) > selectedConfig.MaxLegDeltaDiff)
                                {
                                    _log.Info($"{nameof(LoadAutoPerms)} leg delta check failed, " +
                                              $"Original: {order.SpreadId}, " +
                                              $"Perm : {item.SpreadId}, " +
                                              $"Delta: {delta}, " +
                                              $"Current Delta: {currentDelta}, " +
                                              $"Max Delta: {selectedConfig.MaxDeltaAddition}, " +
                                              $"Msg: {$" Basket Id: {InstanceId}. Order Id: {order.SpreadId}, Last Edge Before Fees: {lastEdge}."}");
                                    OmsCore.AutoPermTelemetryService.RecordFilterRejection(order.PermID, "LegDelta");
                                    permPairs.Remove(pair);
                                    item.Dispose();
                                    coItem?.Dispose();
                                    proceed = false;
                                    break;
                                }
                                else
                                {
                                    _log.Info($"{nameof(LoadAutoPerms)} leg delta check passed, " +
                                              $"Original: {order.SpreadId}, " +
                                              $"Perm : {item.SpreadId}, " +
                                              $"Delta: {delta}, " +
                                              $"Current Delta: {currentDelta}, " +
                                              $"Max Delta: {selectedConfig.MaxDeltaAddition}, " +
                                              $"Msg: {$" Basket Id: {InstanceId}. Order Id: {order.SpreadId}, Last Edge Before Fees: {lastEdge}."}");
                                }
                            }
                        }
                    }

                    if (!proceed)
                    {
                        continue;
                    }
                }

                if (selectedConfig.MaxWeightedVegaDiff < 1)
                {
                    bool proceed = true;
                    Task[] tasks = new Task[] { item.WaitForWeightedVegaLoadAsync(), order.WaitForWeightedVegaLoadAsync() };
                    await Task.WhenAll(tasks);

                    double itemWeightedVega = item.GetWeightedVega();
                    double orderWeightedVega = order.GetWeightedVega();
                    if (Math.Abs(itemWeightedVega - orderWeightedVega) > selectedConfig.MaxWeightedVegaDiff)
                    {
                        _log.Info($"{nameof(LoadAutoPerms)} w.vega check failed, " +
                                  $"Orig : {order.SpreadId}, " +
                                  $"Perm : {item.SpreadId}, " +
                                  $"Orig W.Vega: {orderWeightedVega}, " +
                                  $"Perm W.Vega: {itemWeightedVega}, " +
                                  $"Max Diff: {selectedConfig.MaxWeightedVegaDiff}, " +
                                  $"Msg: {$" Basket Id: {InstanceId}. Order Id: {order.SpreadId}, Last Edge Before Fees: {lastEdge}."}");
                        OmsCore.AutoPermTelemetryService.RecordFilterRejection(order.PermID, "WeightedVega");
                        permPairs.Remove(pair);
                        item.Dispose();
                        coItem?.Dispose();
                        proceed = false;
                        break;
                    }
                    if (!proceed)
                    {
                        continue;
                    }
                }

                if (item != null)
                {
                    item.PermGen = order.PermGen + 1;
                }
                if (coItem != null)
                {
                    coItem.PermGen = order.PermGen + 1;
                }

                int newSize;
                int newCoSize;
                if (order.HardSide != null)
                {
                    if (order.HardSide.Value == ((IOrder)item).Side)
                    {
                        newSize = Math.Max(selectedConfig.HardSideInitialSizeForAutoPerms, selectedConfig.InitialSizeForAutoPerms);
                        newCoSize = selectedConfig.InitialSizeForAutoPerms;
                    }
                    else if (coItem != null && order.HardSide.Value == ((IOrder)coItem).Side)
                    {
                        newSize = selectedConfig.InitialSizeForAutoPerms;
                        newCoSize = Math.Max(selectedConfig.HardSideInitialSizeForAutoPerms, selectedConfig.InitialSizeForAutoPerms);
                    }
                    else
                    {
                        newSize = selectedConfig.InitialSizeForAutoPerms;
                        newCoSize = selectedConfig.InitialSizeForAutoPerms;
                    }
                }
                else
                {
                    newSize = selectedConfig.InitialSizeForAutoPerms;
                    newCoSize = selectedConfig.InitialSizeForAutoPerms;
                }
                if (newSize != item.Lcd)
                {
                    item.UpdateQty(newSize);
                    _log.Info($"{nameof(LoadAutoPerms)} perms qty update, " +
                              $"Perm: {item.SpreadId}, " +
                              $"Size: {newSize}, " +
                              $"Hash: {spreadHash}, " +
                              $"Original: {order.SpreadId}");
                }
                if (coItem != null && newCoSize != coItem.Lcd)
                {
                    coItem.UpdateQty(newCoSize);
                    _log.Info($"{nameof(LoadAutoPerms)} co perms qty update, " +
                             $"Perm: {item.SpreadId}, " +
                             $"Size: {newCoSize}, " +
                             $"Hash: {spreadHash}, " +
                             $"Original: {order.SpreadId}");
                }

                item.ResetPriceAndContraPrice();
                item.SubType = OrderSubType.BasketAutoPerm;
                item.ModuleTypeSuffix = spreadHash;
                item.PreSizeCheck = CheckForPermWithSize;
                if (coItem != null)
                {
                    coItem.ResetPriceAndContraPrice();
                    item.SubType = OrderSubType.BasketAutoPerm;
                    item.ModuleTypeSuffix = spreadHash;
                    coItem.PreSizeCheck = CheckForPermWithSize;
                }
            }

            _log.Info($"{nameof(LoadAutoPerms)} perm adj px loaded, " +
                      $"Original: {order.SpreadId}, " +
                      $"Count: {permPairs.Count}, " +
                      $"Perms: {string.Join(", ", permPairs.Select(x => x.Item1.SpreadId))}, " +
                      $"Msg: {$" Basket Id: {InstanceId}. Order Id: {order.SpreadId}, Last Edge Before Fees: {lastEdge}."}");

            OmsCore.AutoPermTelemetryService.RecordPermsLoaded(order.PermID, permsLoadedCount, permPairs.Count,
                string.Join(",", permPairs.Select(x => x.Item1.SpreadId)));

            if (queuePerms)
            {
                PermInput = Tuple.Create(order, lastEdge, selectedConfig, GetAutomationConfig().AutoPermConfigModel.WaitForPrevious, permPairs);
                if (permPairs.Count > 0 && sendOrders)
                {
                    await SendQueuedPerms().ContinueWith(t =>
                    {
                        if (!RemoveAllOnInterval)
                        {
                            _ = Task.Delay(60000).ContinueWith(t =>
                            {
                                RemoveMultipleBasketItems(allPerms.Where(x => !x.IsActive).ToList());
                            });
                        }
                    });
                }
                else
                {
                    OmsCore.AutoPermTelemetryService.Complete(order.PermID);
                }
            }
            else
            {
                OmsCore.AutoPermTelemetryService.Complete(order.PermID);
            }
        }

        private async Task<List<Tuple<BasketTraderItemModel, BasketTraderItemModel>>> LoadPerms(BasketTraderItemModel basketTraderItemModel, double lastEdge, AutoPermConfigModel selectedConfig, bool activate = false)
        {
            List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> permPairs = new();
            List<object> itemsToPerm = new() { basketTraderItemModel };
            List<BasketTraderItemModel> tempNewPerms = await PermUsingTemplate(itemsToPerm, selectedConfig.AutoPermTemplate.Perms, activate: false);
            if (tempNewPerms == null || tempNewPerms.Count == 0)
            {
                _log.Info("Perm load failed, Original: {}, Edge: {}", basketTraderItemModel.SpreadId, lastEdge);
                return permPairs;
            }
            _log.Info("Perms loaded, Orig: {}, Count: {}, Perms: {}, Edge: {}.", basketTraderItemModel.SpreadId, tempNewPerms.Count, string.Join(", ", tempNewPerms.Select(x => x.SpreadId)), lastEdge);

            foreach (var perm in tempNewPerms)
            {
                BasketTraderItemModel inverted = selectedConfig.AttemptBothSides ? await GetBasketItemCloneAsync(perm, reverse: true, add: true, activate: activate) : null;
                Tuple<BasketTraderItemModel, BasketTraderItemModel> pair = Tuple.Create(perm, inverted);
                permPairs.Add(pair);
            }

            string route = basketTraderItemModel.Route;
            if (!string.IsNullOrWhiteSpace(route))
            {
                foreach (var perm in permPairs)
                {
                    if (perm.Item1 != null)
                    {
                        perm.Item1.Route = route;
                    }

                    if (perm.Item2 != null)
                    {
                        perm.Item2.Route = route;
                    }
                }
            }
            else
            {
                foreach (var perm in permPairs)
                {
                    perm.Item1?.SetBestRoute(true);

                    perm.Item2?.SetBestRoute(true);
                }
            }
            return permPairs;
        }

        [Command]
        public async Task SendAutoPermsCommand()
        {
            var proceed = await GetVerificationAsync("Are you sure you want to submit queued perms?");
            if (proceed)
            {
                await SendQueuedPerms();
            }
        }

        public async Task SendQueuedPerms()
        {
            if (PermInput == null)
            {
                return;
            }

            OrderTicket order = PermInput.Item1;
            double lastEdge = PermInput.Item2;
            AutoPermConfigModel selectedConfig = PermInput.Item3;
            bool waitForPrevious = PermInput.Item4;
            List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> permPairs = PermInput.Item5;

            if (!order.TryGetDynamicEdge(out _, out _, out _, out double permMinEdge) || double.IsNaN(permMinEdge))
            {
                permMinEdge = GetBasketMinEdge();
            }

            double minimumEdge = permMinEdge - lastEdge;
            if (selectedConfig.MatchTargetEdge && lastEdge > permMinEdge)
            {
                minimumEdge = 0;
            }

            double targetEdge = selectedConfig.UseBasketEdge ? order.GetClosingEdge(false) : selectedConfig.TargetEdge;
            if (order.Legs.Count > 2)
            {
                int addedContracts = order.Legs.Count - 2;
                targetEdge += addedContracts * selectedConfig.EdgePerAddedLeg;
            }
            double startingEdge = Math.Max(minimumEdge, targetEdge - lastEdge);
            startingEdge += Math.Abs(selectedConfig.BackupEdge);

            _log.Info($"{nameof(SendQueuedPerms)}. " +
                      $"Original: {order.SpreadId}, " +
                      $"New Edge: {startingEdge}, " +
                      $"Min Edge: {minimumEdge}, " +
                      $"Perm Min Edge: {permMinEdge}, " +
                      $"Last Edge After Fees: {lastEdge}, " +
                      $"MatchTargetEdge: {selectedConfig.MatchTargetEdge}, " +
                      $"Selected Target Edge: {targetEdge}, " +
                      $"Target Edge: {selectedConfig.TargetEdge}, " +
                      $"Use Basket Edge: {selectedConfig.UseBasketEdge}, " +
                      $"Edge Per Contract: {selectedConfig.EdgePerAddedLeg}, " +
                      $"Backup Edge: {selectedConfig.BackupEdge}");

            OmsCore.AutoPermTelemetryService.RecordQueueStart(order.PermID, startingEdge, minimumEdge, permMinEdge,
                targetEdge, selectedConfig.UseBasketEdge, selectedConfig.MatchTargetEdge,
                selectedConfig.BackupEdge, selectedConfig.EdgePerAddedLeg, permPairs.Count);

            CancellationToken token = GetCancellationToken();

            double increment = (double)order.GetPriceIncrement();
            double nextIncrement = Math.Round(startingEdge, 2);
            int count = 0;
            bool permGotFill = false;
            Side? lastFilledSide = null;
            bool stop = false;
            int resubmit = 0;
            var dict = new Dictionary<BasketTraderItemModel, Tuple<BasketTraderItemModel, BasketTraderItemModel>>();
            foreach (var pair in permPairs)
            {
                dict[pair.Item1] = pair;
                if (pair.Item2 != null)
                {
                    dict[pair.Item2] = pair;
                }
            }
            while (nextIncrement >= minimumEdge && !stop)
            {
                _log.Info($"{nameof(SendQueuedPerms)}. " +
                          $"New Edge: {startingEdge}, " +
                          $"MinEdge: {minimumEdge}, " +
                          $"Perm Min Edge: {permMinEdge}, " +
                          $"MatchTargetEdge: {selectedConfig.MatchTargetEdge}, " +
                          $"Selected Target Edge: {targetEdge}, " +
                          $"TargetEdge: {selectedConfig.TargetEdge}, " +
                          $"Use Basket Edge: {selectedConfig.UseBasketEdge}, " +
                          $"Edge Per Leg: {selectedConfig.EdgePerAddedLeg}, " +
                          $"Count: : {count}, " +
                          $"Next: : {nextIncrement}, " +
                          $"Inc: : {increment}");

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (resubmit == 0 && selectedConfig.MaxResub > 0 && count++ > selectedConfig.MaxResub)
                {
                    _log.Info($"{nameof(SendQueuedPerms)}. " +
                              $"Max Resub Reached. " +
                              $"New Edge: {startingEdge}, " +
                              $"MinEdge: {minimumEdge}, " +
                              $"Perm Min Edge: {permMinEdge}, " +
                              $"MatchTargetEdge: {selectedConfig.MatchTargetEdge}, " +
                              $"Selected Target Edge: {targetEdge}, " +
                              $"TargetEdge: {selectedConfig.TargetEdge}, " +
                              $"Use Basket Edge: {selectedConfig.UseBasketEdge}, " +
                              $"Edge Per Leg: {selectedConfig.EdgePerAddedLeg}, " +
                              $"Count: {count}, " +
                              $"Next: {nextIncrement}, " +
                              $"Inc: {increment}");

                    break;
                }

                if (waitForPrevious)
                {
                    foreach (Tuple<BasketTraderItemModel, BasketTraderItemModel> pair in permPairs.ToList())
                    {
                        BasketTraderItemModel item = pair.Item1;
                        double prevPrice = item.Price;
                        _log.Info($"{nameof(LoadAutoPerms)}. Next Attempt. " +
                                  $"Next Inc: " + nextIncrement + ", " +
                                  $"Prev Px: {prevPrice}, " +
                                  $"Status: {item.Status}, " +
                                  $"Is Looping: {item.IsActive}, " +
                                  $"Order: {item.SpreadId}, " +
                                  $"New Px: {item.Price}, " +
                                  $"Bid: {item.Low}, " +
                                  $"Ask: {item.High}, " +
                                  $"Theo: {item.NetTheo}, " +
                                  $"Test Value: {item.NetTestValue}, " +
                                  $"Price: {item.Price}, " +
                                  $"C.Price: {item.ContraPrice}, " +
                                  $"Perm Price: {item.PermAdjPxBase}, " +
                                  $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                                  $"A.Theo: {item.NetDeltaAdjTheo}");
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (item.IsActive || item.SpreadPosition != 0)
                        {
                            continue;
                        }

                        item.ResetEdgeOverride(silent: true);
                        if (permGotFill)
                        {
                            if (selectedConfig.StopOnFill)
                            {
                                stop = true;
                                break;
                            }
                            if (selectedConfig.SizeDownOnFill)
                            {
                                item.UpdateQty(1);
                            }
                        }

                        var emaLoaded = await item.WaitForEmaLoad();
                        var dataLoaded = await item.WaitForMarkLoad();
                        if (!emaLoaded || !dataLoaded)
                        {
                            item.Status = $"Perm {(emaLoaded ? "" : "EMA")} {(dataLoaded ? "" : "DATA")} load failed.";
                            item.StatusMode = StatusMode.CancelledSell;
                            _log.Info($"{nameof(LoadAutoPerms)} Ema load failed!, " +
                                      $"Original: {order.SpreadId}, " +
                                      $"Perm: {item.SpreadId}");
                            OmsCore.AutoPermTelemetryService.RecordEmaLoadFailure(order.PermID);
                        }
                        else
                        {
                            item.CreationTime = DateTime.Now;
                            item.ClearOrderDetails();
                            item.SetOrderDetailTag("Perm Details", item.PermDetailsLog);
                            item.SetOrderDetailTag($"Converted Ref {order.Side} Px", order.AveragePrice.ToString("F2"));
                            item.SetOrderDetailTag("Conversion Underlying", item.UnderMidAtPermLoad.ToString("F2"));
                            item.PermAdjPxAsync(nextIncrement);

                            bool validEma = !selectedConfig.EdgeToEmaEnabled || (double.IsNaN(selectedConfig.EdgeToEma) || item.IsSingleLegSell
                                ? item.Price >= item.GetEma(OmsCore.Config.PerformanceModeEnabled) + selectedConfig.EdgeToEma
                                : item.Price <= item.GetEma(OmsCore.Config.PerformanceModeEnabled) - selectedConfig.EdgeToEma);

                            bool validTheo = !selectedConfig.EdgeToTheoEnabled || (double.IsNaN(selectedConfig.EdgeToTheo) || item.IsSingleLegSell
                                ? item.Price >= item.DeltaAdjustedTheo + selectedConfig.EdgeToTheo
                                : item.Price <= item.DeltaAdjustedTheo - selectedConfig.EdgeToTheo);

                            double widthEdge = Math.Round((item.High - item.Low) * selectedConfig.WidthPercentEdgeToTheo, 2);
                            bool validWidthPercentEdgeToTheo = !selectedConfig.WidthPercentEdgeToTheoEnabled || (double.IsNaN(widthEdge) || item.IsSingleLegSell
                                ? item.Price >= item.DeltaAdjustedTheo + widthEdge
                                : item.Price <= item.DeltaAdjustedTheo - widthEdge);

                            if (validEma && validTheo && validWidthPercentEdgeToTheo)
                            {
                                if (!double.IsNaN(item.Price))
                                {
                                    bool failed = await CheckForMarketCross(item);
                                    if (failed)
                                    {
                                        item.Status = "Perm Mkt check failed.";
                                        item.StatusMode = StatusMode.CancelledSell;
                                        OmsCore.AutoPermTelemetryService.RecordMarketCrossFailure(order.PermID);
                                    }
                                    else
                                    {
                                        DateTime time = DateTime.Now;
                                        await item.SubmitOrder(resting: false, skipAdjPxBeforeSubmit: true, totalResubmitCount: 0, markForRemoval: false, doNotTradeThroughFillPrice: false, subType: null, restOverride: double.NaN, referenceTradeModel: null, clearDetailsContainer: false);
                                        bool success = await item.OrderClosedEvent.WaitOneAsync(3000);
                                        double ellapsed = (DateTime.Now - time).TotalMilliseconds;
                                        _log.Info($"{nameof(LoadAutoPerms)}. Next Attempt. " +
                                                  $"Next Inc: " + nextIncrement + ", " +
                                                  $"Time: " + ellapsed + ", " +
                                                  $"Status: {item.Status}, " +
                                                  $"Order: {item.SpreadId}, " +
                                                  $"New Px: {item.Price}, " +
                                                  $"Bid: {item.Low}, " +
                                                  $"Ask: {item.High}, " +
                                                  $"Theo: {item.NetTheo}, " +
                                                  $"Test Value: {item.NetTestValue}, " +
                                                  $"Price: {item.Price}, " +
                                                  $"C.Price: {item.ContraPrice}, " +
                                                  $"Perm Price: {item.PermAdjPxBase}, " +
                                                  $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                                                  $"A.Theo: {item.NetDeltaAdjTheo}");
                                        OmsCore.AutoPermTelemetryService.RecordAttempt(order.PermID, item.SpreadId,
                                            nextIncrement, item.Price, item.Low, item.High, item.NetTheo,
                                            item.NetDeltaAdjTheo, item.Status, item.TotalFills != 0, ellapsed, "");
                                        if (item.TotalFills != 0)
                                        {
                                            permPairs.Remove(pair);
                                            permGotFill = true;
                                            lastFilledSide = ((IOrder)item).Side;
                                        }
                                        if (!success)
                                        {
                                            stop = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                string message = $"{(!validEma ? "EMA," : "")} {(!validTheo ? "THEO," : "")} {(!validWidthPercentEdgeToTheo ? "Width%THEO," : "")} risk check failed.";
                                item.Status = message;
                                item.StatusMode = StatusMode.CancelledSell;
                                _log.Info(message + ", " +
                                          $"Original: {order.SpreadId}, " +
                                          $"Perm: {item.SpreadId}, " +
                                          $"Price: {item.Price}, " +
                                          $"Ema: {item.GetEma()}, " +
                                          $"Adj Theo: {item.DeltaAdjustedTheo}, " +
                                          $"Mkt: [{item.Low}]X[{item.High}], " +
                                          $"Theo Edge: {selectedConfig.EdgeToTheo}, " +
                                          $"Width % Theo Edge: {selectedConfig.WidthPercentEdgeToTheo}, " +
                                          $"Ema Edge: {selectedConfig.EdgeToEma}");
                                OmsCore.AutoPermTelemetryService.RecordRiskCheckFailure(order.PermID);
                            }
                        }

                        if (selectedConfig.AttemptBothSides && !permGotFill)
                        {
                            BasketTraderItemModel coItem = pair.Item2;
                            if (coItem != null)
                            {
                                var coEmaLoaded = await coItem.WaitForEmaLoad();
                                var coDataLoaded = await coItem.WaitForMarkLoad();
                                if (!coEmaLoaded || !coDataLoaded)
                                {
                                    coItem.Status = $"Perm {(coEmaLoaded ? "" : "EMA")} {(coDataLoaded ? "" : "DATA")} load failed.";
                                    coItem.StatusMode = StatusMode.CancelledSell;
                                    _log.Info($"{nameof(LoadAutoPerms)} Ema load failed!, " +
                                              $"Original: {order.SpreadId}, " +
                                              $"Perm: {coItem.SpreadId}");
                                    OmsCore.AutoPermTelemetryService.RecordEmaLoadFailure(order.PermID);
                                }
                                else
                                {
                                    coItem.ClearOrderDetails();
                                    coItem.SetOrderDetailTag("Perm Details", coItem.PermDetailsLog);
                                    coItem.SetOrderDetailTag($"Converted Ref {order.Side} Px", order.AveragePrice.ToString("F2"));
                                    coItem.SetOrderDetailTag("Conversion Underlying", coItem.UnderMidAtPermLoad.ToString("F2"));
                                    coItem.CreationTime = DateTime.Now;
                                    double reverseEdge = nextIncrement + targetEdge;
                                    coItem.PermAdjPxAsync(reverseEdge);

                                    bool validEma = !selectedConfig.EdgeToEmaEnabled || (double.IsNaN(selectedConfig.EdgeToEma) || coItem.IsSingleLegSell
                                        ? coItem.Price >= coItem.GetEma(OmsCore.Config.PerformanceModeEnabled) + selectedConfig.EdgeToEma
                                        : coItem.Price <= coItem.GetEma(OmsCore.Config.PerformanceModeEnabled) - selectedConfig.EdgeToEma);

                                    bool validTheo = !selectedConfig.EdgeToTheoEnabled || (double.IsNaN(selectedConfig.EdgeToTheo) || coItem.IsSingleLegSell
                                        ? coItem.Price >= coItem.DeltaAdjustedTheo + selectedConfig.EdgeToTheo
                                        : coItem.Price <= coItem.DeltaAdjustedTheo - selectedConfig.EdgeToTheo);

                                    double widthEdge = Math.Round((coItem.High - coItem.Low) * selectedConfig.WidthPercentEdgeToTheo, 2);
                                    bool validWidthPercentEdgeToTheo = !selectedConfig.WidthPercentEdgeToTheoEnabled || (double.IsNaN(widthEdge) || coItem.IsSingleLegSell
                                        ? coItem.Price >= coItem.DeltaAdjustedTheo + widthEdge
                                        : coItem.Price <= coItem.DeltaAdjustedTheo - widthEdge);

                                    if (validEma && validTheo && validWidthPercentEdgeToTheo)
                                    {
                                        if (!double.IsNaN(coItem.Price))
                                        {
                                            bool failed = await CheckForMarketCross(coItem);
                                            if (failed)
                                            {
                                                coItem.Status = "Perm Mkt check failed.";
                                                coItem.StatusMode = StatusMode.CancelledSell;
                                                OmsCore.AutoPermTelemetryService.RecordMarketCrossFailure(order.PermID);
                                            }
                                            else
                                            {
                                                DateTime time = DateTime.Now;
                                                await coItem.SubmitOrder(resting: false, skipAdjPxBeforeSubmit: true, totalResubmitCount: 0, markForRemoval: false, doNotTradeThroughFillPrice: false, subType: null, restOverride: double.NaN, referenceTradeModel: null, clearDetailsContainer: false);
                                                bool success = await coItem.OrderClosedEvent.WaitOneAsync(3000);
                                                double ellapsed = (DateTime.Now - time).TotalMilliseconds;
                                                _log.Info($"{nameof(LoadAutoPerms)}. Next Attempt. " +
                                                          $"Next Inc: " + reverseEdge + ", " +
                                                          $"Time: " + ellapsed + ", " +
                                                          $"Status: {coItem.Status}, " +
                                                          $"Order: {coItem.SpreadId}, " +
                                                          $"New Px: {coItem.Price}, " +
                                                          $"Bid: {coItem.Low}, " +
                                                          $"Ask: {coItem.High}, " +
                                                          $"Theo: {coItem.NetTheo}, " +
                                                          $"Test Value: {coItem.NetTestValue}, " +
                                                          $"Price: {coItem.Price}, " +
                                                          $"C.Price: {coItem.ContraPrice}, " +
                                                          $"Perm Price: {coItem.PermAdjPxBase}, " +
                                                          $"Perm C.Price: {coItem.PermAdjContraPxBase}, " +
                                                          $"A.Theo: {coItem.NetDeltaAdjTheo}");
                                                OmsCore.AutoPermTelemetryService.RecordAttempt(order.PermID, coItem.SpreadId,
                                                    reverseEdge, coItem.Price, coItem.Low, coItem.High, coItem.NetTheo,
                                                    coItem.NetDeltaAdjTheo, coItem.Status, coItem.TotalFills != 0, ellapsed, "");
                                                if (coItem.TotalFills != 0)
                                                {
                                                    permPairs.Remove(pair);
                                                    permGotFill = true;
                                                    lastFilledSide = ((IOrder)coItem).Side;
                                                }
                                                if (!success)
                                                {
                                                    stop = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        string message = $"{(!validEma ? "EMA," : "")} {(!validTheo ? "THEO," : "")} {(!validWidthPercentEdgeToTheo ? "Width%THEO," : "")} risk check failed.";
                                        coItem.Status = message;
                                        coItem.StatusMode = StatusMode.CancelledSell;
                                        _log.Info(message + ", " +
                                                  $"Original: {order.SpreadId}, " +
                                                  $"Perm: {coItem.SpreadId}, " +
                                                  $"Price: {coItem.Price}, " +
                                                  $"Ema: {coItem.GetEma()}, " +
                                                  $"Ema: {coItem.DeltaAdjustedTheo}, " +
                                                  $"Mkt: [{coItem.Low}]X[{coItem.High}], " +
                                                  $"Theo Edge: {selectedConfig.EdgeToTheo}, " +
                                                  $"Width % Theo Edge: {selectedConfig.WidthPercentEdgeToTheo}, " +
                                                  $"Ema Edge: {selectedConfig.EdgeToEma}");
                                        OmsCore.AutoPermTelemetryService.RecordRiskCheckFailure(order.PermID);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    List<List<Tuple<BasketTraderItemModel, double>>> groups = new()
                        {
                            new(),
                            new(),
                        };

                    List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> list = permPairs.ToList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        Tuple<BasketTraderItemModel, BasketTraderItemModel> pair = list[i];
                        var main = pair.Item1;
                        var contra = pair.Item2;

                        if (i % 2 == 0)
                        {
                            groups[0].Add(Tuple.Create(main, 0.0));
                            groups[1].Add(Tuple.Create(contra, targetEdge));
                        }
                        else
                        {
                            groups[0].Add(Tuple.Create(contra, targetEdge));
                            groups[1].Add(Tuple.Create(main, 0.0));
                        }
                    }

                    for (int i = 0; i < groups.Count; i++)
                    {
                        List<Tuple<BasketTraderItemModel, double>> group = groups[i];
                        List<Task<bool>> waitTasks = new List<Task<bool>>();
                        foreach (var tuple in group)
                        {
                            var item = tuple.Item1;
                            var edge = tuple.Item2;
                            double prevPrice = item.Price;
                            _log.Info($"{nameof(SendQueuedPerms)}. " +
                                      $"Next Attempt. " +
                                      $"Next Inc: " + nextIncrement + ", " +
                                      $"Prev Px: {prevPrice}, " +
                                      $"Status: {item.Status}, " +
                                      $"Is Looping: {item.IsActive}, " +
                                      $"Order: {item.SpreadId}, " +
                                      $"New Px: {item.Price}, " +
                                      $"Bid: {item.Low}, " +
                                      $"Ask: {item.High}, " +
                                      $"Theo: {item.NetTheo}, " +
                                      $"Test Value: {item.NetTestValue}, " +
                                      $"Price: {item.Price}, " +
                                      $"C.Price: {item.ContraPrice}, " +
                                      $"Perm Price: {item.PermAdjPxBase}, " +
                                      $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                                      $"A.Theo: {item.NetDeltaAdjTheo}");
                            if (token.IsCancellationRequested || stop)
                            {
                                break;
                            }

                            if (item.IsActive || item.SpreadPosition != 0)
                            {
                                continue;
                            }

                            item.ResetEdgeOverride(silent: true);
                            if (permGotFill)
                            {
                                if (selectedConfig.StopOnFill)
                                {
                                    stop = true;
                                    break;
                                }
                                if (selectedConfig.SizeDownOnFill)
                                {
                                    item.UpdateQty(1);
                                }
                            }

                            var emaLoaded = !selectedConfig.EdgeToEmaEnabled || await item.WaitForEmaLoad();
                            var dataLoaded = await item.WaitForMarkLoad();
                            if (!emaLoaded || !dataLoaded)
                            {
                                item.Status = $"Perm {(emaLoaded ? "" : "EMA")} {(dataLoaded ? "" : "DATA")} load failed.";
                                item.StatusMode = StatusMode.CancelledSell;
                                _log.Info($"{nameof(SendQueuedPerms)} Ema load failed!, " +
                                          $"Original: {order.SpreadId}, " +
                                          $"Perm: {item.SpreadId}");
                                OmsCore.AutoPermTelemetryService.RecordEmaLoadFailure(order.PermID);
                            }
                            else
                            {
                                item.CreationTime = DateTime.Now;
                                item.ClearOrderDetails();
                                item.SetOrderDetailTag("Perm Details", item.PermDetailsLog);
                                item.SetOrderDetailTag($"Converted Ref {order.Side} Px", order.AveragePrice.ToString("F2"));
                                item.SetOrderDetailTag("Conversion Underlying", item.UnderMidAtPermLoad.ToString("F2"));
                                item.PermAdjPxAsync(edge + nextIncrement);

                                bool validEma = !selectedConfig.EdgeToEmaEnabled || (double.IsNaN(selectedConfig.EdgeToEma) || item.IsSingleLegSell
                                    ? item.Price >= item.GetEma(OmsCore.Config.PerformanceModeEnabled) + selectedConfig.EdgeToEma
                                    : item.Price <= item.GetEma(OmsCore.Config.PerformanceModeEnabled) - selectedConfig.EdgeToEma);

                                bool validTheo = !selectedConfig.EdgeToTheoEnabled || (double.IsNaN(selectedConfig.EdgeToTheo) || item.IsSingleLegSell
                                    ? item.Price >= item.DeltaAdjustedTheo + selectedConfig.EdgeToTheo
                                    : item.Price <= item.DeltaAdjustedTheo - selectedConfig.EdgeToTheo);

                                double widthEdge = Math.Round((item.High - item.Low) * selectedConfig.WidthPercentEdgeToTheo, 2);
                                bool validWidthPercentEdgeToTheo = !selectedConfig.WidthPercentEdgeToTheoEnabled || (double.IsNaN(widthEdge) || item.IsSingleLegSell
                                    ? item.Price >= item.DeltaAdjustedTheo + widthEdge
                                    : item.Price <= item.DeltaAdjustedTheo - widthEdge);

                                if (validEma && validTheo && validWidthPercentEdgeToTheo)
                                {
                                    if (!double.IsNaN(item.Price))
                                    {
                                        bool failed = await CheckForMarketCross(item);
                                        if (failed)
                                        {
                                            item.Status = "Perm Mkt check failed.";
                                            item.StatusMode = StatusMode.CancelledSell;
                                            OmsCore.AutoPermTelemetryService.RecordMarketCrossFailure(order.PermID);
                                        }
                                        else
                                        {
                                            DateTime time = DateTime.Now;
                                            await item.SubmitOrder(resting: false, skipAdjPxBeforeSubmit: true, totalResubmitCount: 0, markForRemoval: false, doNotTradeThroughFillPrice: false, subType: null, restOverride: double.NaN, referenceTradeModel: null, clearDetailsContainer: false);
                                            Task<bool> task = item.OrderClosedEvent.WaitOneAsync(3000);
                                            waitTasks.Add(task);
                                            _log.Info($"{nameof(SendQueuedPerms)}. " +
                                                      $"Next Attempt. " +
                                                      $"Next Inc: " + nextIncrement + ", " +
                                                      $"Status: {item.Status}, " +
                                                      $"Order: {item.SpreadId}, " +
                                                      $"New Px: {item.Price}, " +
                                                      $"Bid: {item.Low}, " +
                                                      $"Ask: {item.High}, " +
                                                      $"Theo: {item.NetTheo}, " +
                                                      $"Test Value: {item.NetTestValue}, " +
                                                      $"Price: {item.Price}, " +
                                                      $"C.Price: {item.ContraPrice}, " +
                                                      $"Perm Price: {item.PermAdjPxBase}, " +
                                                      $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                                                      $"A.Theo: {item.NetDeltaAdjTheo}");
                                            OmsCore.AutoPermTelemetryService.RecordAttempt(order.PermID, item.SpreadId,
                                                edge + nextIncrement, item.Price, item.Low, item.High, item.NetTheo,
                                                item.NetDeltaAdjTheo, item.Status, false, 0, "");
                                        }
                                    }
                                }
                                else
                                {
                                    string message = $"{(!validEma ? "EMA," : "")} {(!validTheo ? "THEO," : "")} {(!validWidthPercentEdgeToTheo ? "Width%THEO," : "")} risk check failed.";
                                    item.Status = message;
                                    item.StatusMode = StatusMode.CancelledSell;
                                    _log.Info(message + ", " +
                                              $"Original: {order.SpreadId}, " +
                                              $"Perm: {item.SpreadId}, " +
                                              $"Price: {item.Price}, " +
                                              $"Ema: {item.GetEma()}, " +
                                              $"Ema: {item.DeltaAdjustedTheo}, " +
                                              $"Mkt: [{item.Low}]X[{item.High}], " +
                                              $"Theo Edge: {selectedConfig.EdgeToTheo}, " +
                                              $"Width % Theo Edge: {selectedConfig.WidthPercentEdgeToTheo}, " +
                                              $"Ema Edge: {selectedConfig.EdgeToEma}");
                                    OmsCore.AutoPermTelemetryService.RecordRiskCheckFailure(order.PermID);
                                }
                            }
                        }

                        await Task.WhenAll(waitTasks);
                        stop = waitTasks.Any(x => !x.Result);

                        foreach (var item in group.Select(x => x.Item1).Where(x => x.TotalFills != 0 || x.FilledQty != 0))
                        {
                            permGotFill = true;
                            lastFilledSide = ((IOrder)item).Side;

                            if (dict.TryGetValue(item, out Tuple<BasketTraderItemModel, BasketTraderItemModel> pair))
                            {
                                permPairs.Remove(pair);
                                for (int j = i + 1; j < groups.Count; j++)
                                {
                                    var nextGroup = groups[j];
                                    foreach (var nextGroupItem in nextGroup.ToList())
                                    {
                                        if (nextGroupItem.Item1 == pair.Item1 || nextGroupItem.Item1 == pair.Item2)
                                        {
                                            nextGroup.Remove(nextGroupItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (resubmit++ >= selectedConfig.LevelResub)
                {
                    resubmit = 0;
                    nextIncrement = Math.Round(nextIncrement - increment, 2);
                }
            }

            OmsCore.AutoPermTelemetryService.RecordCompletion(order.PermID, permGotFill,
                lastFilledSide?.ToString() ?? "", selectedConfig.StopOnFill && permGotFill,
                resubmit == 0 && selectedConfig.MaxResub > 0 && count > selectedConfig.MaxResub);
            OmsCore.AutoPermTelemetryService.Complete(order.PermID);
        }

        private static async Task<bool> CheckForMarketCross(BasketTraderItemModel item)
        {
            bool failed = false;
            bool dataLoaded = await item.WaitForMarkLoad();
            if (!dataLoaded)
            {
                _log.Info($"{nameof(LoadAutoPerms)}. Data Load Failed. " +
                      $"Order: {item.SpreadId}, " +
                      $"Bid: {item.Low}, " +
                      $"Ask: {item.High}, " +
                      $"Theo: {item.NetTheo}, " +
                      $"Price: {item.Price}, " +
                      $"C.Price: {item.ContraPrice}, " +
                      $"Perm Price: {item.PermAdjPxBase}, " +
                      $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                      $"A.Theo: {item.NetDeltaAdjTheo}");
                failed = true;
            }

            if ((item.Price > item.High && item.IsSingleLegSell) ||
                (item.Price < item.Low && !item.IsSingleLegSell))
            {
                _log.Info($"{nameof(LoadAutoPerms)}. Px Cross Detected. " +
                      $"Order: {item.SpreadId}, " +
                      $"Bid: {item.Low}, " +
                      $"Ask: {item.High}, " +
                      $"Theo: {item.NetTheo}, " +
                      $"Price: {item.Price}, " +
                      $"C.Price: {item.ContraPrice}, " +
                      $"Perm Price: {item.PermAdjPxBase}, " +
                      $"Perm C.Price: {item.PermAdjContraPxBase}, " +
                      $"A.Theo: {item.NetDeltaAdjTheo}");
                failed = true;
            }

            return failed;
        }

        private CancellationToken GetCancellationToken()
        {
            if (_submitWithDelayCancellationTokenSource.IsCancellationRequested)
            {
                _submitWithDelayCancellationTokenSource = new CancellationTokenSource();
            }
            CancellationToken token = _submitWithDelayCancellationTokenSource.Token;
            return token;
        }

        private double GetBasketMinEdge()
        {
            var automationConfigModel = GetAutomationConfig();
            return automationConfigModel.LoopMinEdgeUsePercentage ?
                Math.Max(automationConfigModel.ContraFishEdge * automationConfigModel.LoopMinEdgePercentage, 0) :
                automationConfigModel.LoopMinEdge;
        }

        private void OnTrade(OrderTicket ticket, IOmsOrder trade)
        {
            SendTradeUpdateToBasketManagerAsync(trade);
            if (!BasketSettings.EvaluateAdjustedEdgeOverrides)
            {
                return;
            }
            Data.Trading.PositionModel spreadPosition = GetSpreadPosition(ticket.SpreadId);
            spreadPosition.AddTradeWithPosition(trade);

            if (spreadPosition.RealizedPnl < 0)
            {
                ticket.AdjustedEdgeOverride = ticket.GetCurrentEdge() + Math.Abs(spreadPosition.AdjustedPnl) + BasketSettings.AdjustedEdgeOverrideCushionValue;
            }
        }

        internal Data.Trading.PositionModel GetSpreadPosition(string spreadId)
        {
            if (!_spreadIdToPositionMap.TryGetValue(spreadId, out Data.Trading.PositionModel position))
            {
                position = new Data.Trading.PositionModel(spreadId);
                _spreadIdToPositionMap[spreadId] = position;
            }

            return position;
        }

        internal void CancelQueuedSubmitWithDelay()
        {
            try
            {
                _fillCount = 0;
                _submitWithDelayCancellationTokenSource.Cancel();
                _orderSubmitResetEvent.Set();
                if (BasketSettings?.AutomationConfig?.CloseOrderMode != null)
                {
                    _timerCts.Cancel();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelQueuedSubmitWithDelay));
            }
        }

        internal void CreateComplexOrderTicketSync(OrderTicket orderModel)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                ManualResetEventSlim resetEvent = new(false);
                CreateComplexOrderTicket(orderModel, resetEvent, false, false, false);
                resetEvent.Wait();
            }
        }

        internal void CreateComplexOrderTicket(string tos, Side? side = null, bool openDepth = false)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    var model = new SymbolCodec(tos);

                    (Window view, ComplexOrderTicketViewModel viewModel) = CreateWindow();
                    view.Loaded += async (s, e) =>
                    {
                        await viewModel.LoadLegsFromTosAsync(tos, side, true);
                        viewModel.ShowDepthBook = openDepth;
                    };

                    view.Show();
                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        internal void CreateComplexOrderTicket(OrderTicket orderModel, ManualResetEventSlim resetEvent = null, bool isClosing = false, bool isContraTicket = false, bool local = false)
        {
            if (OmsCore.BasketManagerClient.IsConnected && !local)
            {
                var sent = OmsCore.BasketManagerClient.OpenTicket(this, orderModel, isClosing);
            }
            _log.Info($"[Trace-1] Open Ticket Requested. Id: {Uid}, Spread: {orderModel.SpreadId}, LastId: {orderModel.OrderId}/{orderModel.ContraOrderId}, State: {orderModel.OrderStatus}/{orderModel.ContraOrderStatus}");
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    bool isSingleLeg = orderModel.Legs.Count <= 1;
                    string spreadId = orderModel.SpreadId;

                    (Window view, ComplexOrderTicketViewModel viewModel) = CreateWindow(isSingleLeg);

                    _spreadIdToOpenedTicketsMap[spreadId] = view;
                    view.Closed += (s, e) => _spreadIdToOpenedTicketsMap.TryRemove(spreadId, out _);

                    if (!orderModel.TrySelectRoute(true, true, out string route, out _))
                    {
                        route = orderModel.Route;
                    }

                    resetEvent?.Set();
                    view.Loaded += Loaded;

                    async void Loaded(object s, RoutedEventArgs e)
                    {
                        view.Loaded -= Loaded;
                        viewModel.InstanceMode = GetInstanceMode();
                        viewModel.BrokerOverride = BrokerOverride;
                        await LoadTicketFromOrdermodel(view, viewModel, orderModel, orderModel.Account, route, isClosing, isContraTicket);
                    }

                    view.Show();
                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        private async Task LoadTicketFromOrdermodel(Window view, ComplexOrderTicketViewModel viewModel, OrderTicket orderModel, string account, string route, bool isClosing, bool isContraTicket)
        {
            _log.Info($"[Trace-1] Loading Ticket. Id: {Uid}, Spread: {orderModel.SpreadId}, LastId: {orderModel.OrderId}/{orderModel.ContraOrderId}, State: {orderModel.OrderStatus}/{orderModel.ContraOrderStatus}");
            Task loadTask = !isContraTicket ?
                                        viewModel.LoadFromTicketAsync(orderModel) :
                                        viewModel.LoadContraFromTemplateAsync(orderModel);
            await loadTask.ContinueWith(async t =>
            {
                _log.Info($"[Trace-1] Ticket Loaded. Id: {Uid}, Spread: {orderModel.SpreadId}, LastId: {orderModel.OrderId}/{orderModel.ContraOrderId}, State: {orderModel.OrderStatus}/{orderModel.ContraOrderStatus}");
                var instanceMode = GetInstanceMode();
                if (instanceMode.IsAutoTraderInstance())
                {
                    viewModel.Account = account;
                    if (isClosing)
                    {
                        viewModel.Reverse();
                        foreach (TicketLegModel leg in viewModel.Legs)
                        {
                            leg.Position = Positions.CLOSE.ToString();
                        }
                    }
                }

                if (isContraTicket || (!isClosing && AlsoOpenContraTicketEnabled) || OmsCore.Config.OpenSeparateTicketForUnderlying)
                {
                    await view.Dispatcher.BeginInvoke(() =>
                    {
                        _log.Info($"[Trace-1] Setting Ticket Location. Id: {Uid}, Spread: {orderModel.SpreadId}, LastId: {orderModel.OrderId}/{orderModel.ContraOrderId}, State: {orderModel.OrderStatus}/{orderModel.ContraOrderStatus}");
                        if (isContraTicket)
                        {
                            view.Left += view.Width;
                        }

                        if (!isClosing && AlsoOpenContraTicketEnabled)
                        {
                            object[] cloneParameter = new object[]
                            {
                                        view.Width,
                                        view.Height,
                                        view.Left,
                                        view.Top,
                                        view
                            };
                            viewModel.Contra(cloneParameter.Clone());
                        }

                        if (OmsCore.Config.OpenSeparateTicketForUnderlying)
                        {
                            double left = view.Left;
                            double top = view.Top;
                            double width = view.Width;
                            double height = view.Height;
                            _ = viewModel.OpenUnderlyingTicket(left, top, width, height);
                        }
                    });
                }
            });
        }

        private (Window window, ComplexOrderTicketViewModel) CreateWindow(bool isSingleLeg = false)
        {
            Window window = null;
            if (isSingleLeg && OmsCore.Config.UseOrderTicketForSingleLegOrders)
            {
                window = new OrderTicketView();
            }
            else
            {
                switch (OmsCore.Config.DefaultOrderTicketStyle)
                {
                    case OrderTicketStyle.Complex:
                        window = new ComplexOrderTicketView()
                        {
                            Manual = false,
                        };
                        break;
                    case OrderTicketStyle.Combined:
                        window = new CombinedOrderTicketView()
                        {
                            Manual = false,
                        };
                        break;
                }
            }

            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
            viewModel.InstanceMode = GetInstanceMode();
            viewModel.BrokerOverride = BrokerOverride;
            viewModel.SetDispatcher(window.Dispatcher);
            window.Dispatcher.UnhandledException += (s, e) =>
            {
                _log.Error(e.Exception, "DispatcherUnhandledException");
                e.Handled = true;
            };
            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
            return (window, viewModel);
        }

        internal void LoadFromTemplateAsync(BasketTraderViewModel basketTraderViewModel)
        {
            BasketTraderConfig config = basketTraderViewModel.GetConfig();
            LoadConfig(config);

            List<BasketTraderItemModel> items = new();
            List<Task> loadTasks = new();
            foreach (BasketTraderItemModel basketItem in basketTraderViewModel.BasketItems)
            {
                BasketTraderItemModel item = MakeBasketItemModel();
                items.Add(item);
                Task loadTask = item.LoadFromTicketAsync(basketItem);
                loadTasks.Add(loadTask);
            }
            Task.WhenAll(loadTasks).ContinueWith(t => AddMultipleToBasketAsync(items));
        }

        internal void LoadFromTemplateFlippedAsync(BasketTraderViewModel basketTraderViewModel)
        {
            BasketTraderConfig config = basketTraderViewModel.GetConfig();
            LoadConfig(config);

            List<BasketTraderItemModel> items = new();
            List<Task> loadTasks = new();
            foreach (BasketTraderItemModel basketItem in basketTraderViewModel.BasketItems)
            {
                BasketTraderItemModel item = MakeBasketItemModel();
                items.Add(item);
                Task loadTask = item.LoadFromTicketAsync(basketItem, flipCP: true);
                loadTasks.Add(loadTask);
            }
            Task.WhenAll(loadTasks).ContinueWith(t => AddMultipleToBasketAsync(items));
        }

        internal async Task<BasketTraderViewModel> LoadFromTemplatePermAsync(BasketTraderViewModel basketTraderViewModel, DateTime expiration, bool deltaRangeEnabled, double minDelta, double maxDelta)
        {
            BasketTraderConfig config = basketTraderViewModel.GetConfig();
            LoadConfig(config);

            List<BasketTraderItemModel> items = new();
            List<Task> loadTasks = new();
            foreach (BasketTraderItemModel basketItem in basketTraderViewModel.BasketItems)
            {
                BasketTraderItemModel item = MakeBasketItemModel();
                items.Add(item);
                Task loadTask = item.LoadPermFromTicketAsync(basketItem, expiration);
                loadTasks.Add(loadTask);
            }
            await Task.WhenAll(loadTasks);
            if (deltaRangeEnabled)
            {
                foreach (BasketTraderItemModel item in items.ToList())
                {
                    await item.WaitForTheoLoadAsync();
                    double totalDelta = Math.Abs(item.TotalDelta);
                    bool valid = totalDelta >= minDelta && totalDelta <= maxDelta;
                    if (!valid)
                    {
                        items.Remove(item);
                    }
                }
            }
            _ = AddMultipleToBasketAsync(items);

            return this;
        }

        internal async Task<BasketTraderViewModel> LoadRangeFromTemplatePermAsync(BasketTraderViewModel basketTraderViewModel, DateTime expiration, double minStrike, double maxStrike, bool includeDecimalStrikes, bool deltaRangeEnabled, double minDelta, double maxDelta)
        {
            BasketTraderConfig config = basketTraderViewModel.GetConfig();
            LoadConfig(config);

            List<BasketTraderItemModel> items = new();
            List<Task> loadT = new();
            IEnumerable<string> underlyings = basketTraderViewModel.BasketItems.Select(x => x.Underlying).Distinct();

            foreach (string underlying in underlyings)
            {
                List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbolsAsync(underlying);
                if (symbols != null)
                {
                    BasketTraderItemModel basketItem = basketTraderViewModel.BasketItems.FirstOrDefault(x => x.Underlying == underlying);
                    List<double> strikes = symbols.Where(x => x.Expiration.Date == expiration.Date && x.Strike >= minStrike && x.Strike <= maxStrike).Select(x => x.Strike).Distinct().ToList();
                    if (!includeDecimalStrikes)
                    {
                        strikes = strikes.Where(x => x % 1 == 0).ToList();
                    }
                    foreach (double strike in strikes)
                    {
                        BasketTraderItemModel item = MakeBasketItemModel();
                        items.Add(item);
                        Task loadTask = item.LoadPermFromTicketAsync(basketItem, expiration, strike);
                        loadT.Add(loadTask);
                    }
                }
            }

            await Task.WhenAll(loadT);
            if (deltaRangeEnabled)
            {
                foreach (BasketTraderItemModel item in items.ToList())
                {
                    await item.WaitForTheoLoadAsync();
                    double totalDelta = Math.Abs(item.TotalDelta);
                    bool valid = totalDelta >= minDelta && totalDelta <= maxDelta;
                    if (!valid)
                    {
                        items.Remove(item);
                    }
                }
            }
            _ = AddMultipleToBasketAsync(items);

            return this;
        }

        internal async Task<BasketTraderViewModel> LoadRangeAsync(bool enableStrikeRange, double minStrike, double maxStrike, bool includeDecimalStrikes, bool deltaRangeEnabled, double minDelta, double maxDelta)
        {
            List<BasketTraderItemModel> basketItems = BasketItems.ToList();
            List<BasketTraderItemModel> itemsToRemove = !enableStrikeRange ? new List<BasketTraderItemModel>() : basketItems.Where(x => x.Legs.Any(x => x.Strike < minStrike || x.Strike > maxStrike)).ToList();

            if (!includeDecimalStrikes)
            {
                foreach (BasketTraderItemModel item in basketItems)
                {
                    if (item.Legs.Any(x => x.Strike.Strike % 1 != 0))
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }

            if (deltaRangeEnabled)
            {
                foreach (BasketTraderItemModel item in basketItems)
                {
                    await item.WaitForTheoLoadAsync();
                    double totalDelta = Math.Abs(item.TotalDelta);
                    bool valid = totalDelta >= minDelta && totalDelta <= maxDelta;
                    if (!valid)
                    {
                        itemsToRemove.Add(item);
                    }
                }
            }

            RemoveMultipleBasketItems(itemsToRemove);

            List<BasketTraderItemModel> items = new();
            List<Task> loadTasks = new();
            IEnumerable<string> underlyings = basketItems.Select(x => x.Underlying).Distinct();
            foreach (string underlying in underlyings)
            {
                List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbolsAsync(underlying);
                if (symbols != null)
                {
                    BasketTraderItemModel basketItem = basketItems.FirstOrDefault(x => x.Underlying == underlying);
                    DateTime? expiration = basketItem?.Expirations.FirstOrDefault();
                    if (expiration != null)
                    {
                        HashSet<double> currentStrikes = basketItems.SelectMany(x => x.Legs.Select(y => y.Strike.Strike)).ToHashSet();
                        List<double> strikes = symbols.Where(x => x.Expiration.Date == expiration.Value.Date).Select(x => x.Strike).Distinct().ToList();
                        if (enableStrikeRange)
                        {
                            strikes = strikes.Where(x => x >= minStrike && x <= maxStrike && !currentStrikes.Contains(x)).ToList();
                        }
                        if (!includeDecimalStrikes)
                        {
                            strikes = strikes.Where(x => x % 1 == 0).ToList();
                        }
                        foreach (double strike in strikes)
                        {
                            BasketTraderItemModel item = MakeBasketItemModel();
                            items.Add(item);
                            Task loadTask = item.LoadPermFromTicketAsync(basketItem, expiration.Value, strike);
                            loadTasks.Add(loadTask);
                        }
                    }
                }
            }

            await Task.WhenAll(loadTasks);
            if (deltaRangeEnabled)
            {
                foreach (BasketTraderItemModel item in items.ToList())
                {
                    await item.WaitForTheoLoadAsync();
                    double totalDelta = Math.Abs(item.TotalDelta);
                    bool valid = totalDelta >= minDelta && totalDelta <= maxDelta;
                    if (!valid)
                    {
                        _log.Info(nameof(LoadRangeAsync) + " Disposing order model for " + item.SpreadId);
                        items.Remove(item);
                        item.Dispose();
                    }
                }
            }

            _ = AddMultipleToBasketAsync(items);
            return this;
        }

        private void RemoveMultipleBasketItems(List<BasketTraderItemModel> itemsToRemove)
        {
            try
            {
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    foreach (BasketTraderItemModel item in itemsToRemove)
                    {
                        BasketItems.Remove(item);
                    }
                }));
                foreach (BasketTraderItemModel item in itemsToRemove)
                {
                    UnregisterEvents(item);
                    _log.Info(nameof(RemoveMultipleBasketItems) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveMultipleBasketItems));
            }
        }

        internal async Task LoadFromTicketAsync(ComplexOrderTicketViewModel orderTicket)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            await item.LoadFromTicketAsync(orderTicket).ContinueWith(async t =>
            {
                await AddToBasketAsync(item);
                double averagePrice = orderTicket.AveragePrice;
                UpdateEdgeSettings(item, averagePrice);
            });
        }

        internal async Task LoadFromLegsAsync(List<TicketLegModel> legs)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            await item.LoadFromLegsAsync(legs);
            _ = AddToBasketAsync(item);
        }

        internal async Task LoadFromTradeAsync(OpraDatabaseTradeModel trade)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            await item.LoadFromTradeAsync(trade)
                      .ContinueWith(x => AddToBasketAsync(item))
                      .ContinueWith(x => UpdateEdgeSettings(item, trade.Price));
        }

        internal async Task<BasketTraderItemModel> LoadFromTradeAsync(EdgeScanFeedModel trade)
        {
            return await LoadFromSymbol(trade.BuySymbol);
        }

        internal async Task<BasketTraderItemModel> LoadFromSymbol(string symbol, Side? side = null)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            return await await item.LoadLegsFromTosAsync(symbol, side).ContinueWith(x => AddToBasketAsync(item)) ? item : null;
        }

        internal void BasketStateChange()
        {
            try
            {
                BasketSettings.InitQty = 1;
                var basketState = GetBasketState();
                _undoStack.Push(basketState);
                _redoStack.Clear();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BasketStateChange));
            }
        }

        private BasketState GetBasketState()
        {
            HashSet<string> symbols = BasketItems.Select(x => x.Symbol).Distinct().ToHashSet();
            var state = new BasketState()
            {
                Symbols = symbols,
            };
            return state;
        }

        internal async Task LoadFromBasketStateAsync(BasketState basketState)
        {
            await Task.Run(() => LoadFromBasketState(basketState));
        }

        private async Task LoadFromBasketState(BasketState basketState)
        {
            ProgressValue = 0;
            ShowProgressBar = true;
            List<BasketTraderItemModel> models = new();

            var existing = new HashSet<string>();
            var removeList = new List<BasketTraderItemModel>();
            foreach (var item in BasketItems)
            {
                if (basketState.Symbols.Contains(item.Symbol))
                {
                    existing.Add(item.Symbol);
                }
                else
                {
                    removeList.Add(item);
                }
            }

            if (removeList.Any())
            {
                RemoveMultipleBasketItems(removeList);
            }

            List<Task> tasks = new();
            var symbols = basketState.Symbols.Where(x => !existing.Contains(x)).ToList();
            for (var i = 0; i < symbols.Count; i++)
            {
                var spreadId = symbols[i];
                try
                {
                    if (IsDisposed)
                    {
                        break;
                    }

                    BasketTraderItemModel model = MakeBasketItemModel();
                    models.Add(model);
                    Task task = model.LoadLegsFromTosAsync(spreadId);
                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(LoadFromSpreadIds));
                }

                ProgressValue = (i + 1) * 100 / symbols.Count;
            }

            await Task.WhenAll(tasks).ContinueWith(t => AddMultipleToBasketAsync(models));

            if (IsDisposed)
            {
                foreach (BasketTraderItemModel item in models)
                {
                    _log.Info(nameof(LoadFromSpreadResults) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                }
                return;
            }

            ProgressValue = 0;
            ShowProgressBar = false;
        }

        internal async Task LoadFromTradesAsync(List<OpraDatabaseTradeModel> trades, string customPermTitle)
        {
            try
            {
                List<object> items = new();
                foreach (OpraDatabaseTradeModel trade in trades)
                {
                    BasketTraderItemModel item = MakeBasketItemModel();
                    await item.LoadFromTradeAsync(trade);
                    items.Add(item);
                }
                await LoadCustomPermsForList(customPermTitle, items);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFromTradesAsync));
            }
        }

        private async void LoadCustomPerms(string customPermTitle)
        {
            try
            {
                await LoadCustomPermsForList(customPermTitle, SelectedItems);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCustomPerms));
            }
        }

        private async void LoadCustomPermsNew(string customPermTitle)
        {
            try
            {
                BasketTraderViewModel clone = Clone(null).ViewModel;
                await clone.LoadCustomPermsForList(customPermTitle, SelectedItems);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCustomPermsNew));
            }
        }

        private async Task LoadCustomPermsForList(string customPermTitle, List<object> items)
        {
            if (items.Count > 0 && OmsCore.Config.CustomPermCombinations.TryGetValue(customPermTitle, out List<Data.PermOperationMode> permOperationModes))
            {
                await PermUsingTemplate(items, permOperationModes);
            }
        }

        private async Task<List<BasketTraderItemModel>> PermUsingTemplate(List<object> itemsToPerm, List<PermOperationMode> permOperationModes, bool activate = true)
        {
            List<List<BasketTraderItemModel>> allItems = new();
            foreach (PermOperationMode operation in permOperationModes)
            {
                switch (operation.PermMode)
                {
                    case PermMode.Highlight:
                        itemsToPerm = allItems.SelectMany(x => x).Union(itemsToPerm).ToList();
                        break;
                    default:
                        if (operation.PermSide == PermSide.Alternate)
                        {
                            IEnumerable<PermSide> permSides = Enum.GetValues(typeof(PermSide)).Cast<PermSide>().Where(x => x != operation.PermSide);
                            foreach (PermSide permSide in permSides)
                            {
                                List<BasketTraderItemModel> basketItemsToAdd = await GetPermsAsync(itemsToPerm, operation.PermMode, permSide, operation.Count, operation.MaintainBaseStrategy);
                                allItems.Add(basketItemsToAdd);
                            }
                        }
                        else
                        {
                            List<BasketTraderItemModel> basketItemsToAdd = await GetPermsAsync(itemsToPerm, operation.PermMode, operation.PermSide, operation.Count, operation.MaintainBaseStrategy);
                            allItems.Add(basketItemsToAdd);
                        }
                        break;
                }
            }
            List<BasketTraderItemModel> finalItems = allItems.SelectMany(x => x).DistinctBy(x => x.SpreadId).Take(100).ToList();
            if (!activate)
            {
                foreach (BasketTraderItemModel item in finalItems)
                {
                    item.Active = false;
                }
            }
            return await AddMultipleToBasketAsync(finalItems);
        }

        internal void LoadFromOpenBasketRequest(OpenBasketRequest openBasketRequest)
        {
            if (!string.IsNullOrWhiteSpace(openBasketRequest.Settings))
            {
                LoadConfigFromJson(openBasketRequest.Settings);
            }

            foreach (OpenTicketRequest symbol in openBasketRequest.Tickets)
            {
                BasketTraderItemModel item = MakeBasketItemModel();
                _ = item.LoadLegsFromTosAsync(symbol.Symbol)
                        .ContinueWith(x => AddToBasketAsync(item));
            }
        }

        internal void LoadFromOpenPermBasketRequest(OpenBasketRequest openBasketRequest)
        {
            AutomationConfigModel automationConfig = GetAutomationConfig();
            if (!string.IsNullOrWhiteSpace(openBasketRequest.BuyRoute))
            {
                automationConfig.LooperOpenRoute = openBasketRequest.BuyRoute;
                automationConfig.LooperOpenRouteSingleLeg = openBasketRequest.BuyRoute;
                automationConfig.LooperDynamicRouting = true;
            }
            if (!string.IsNullOrWhiteSpace(openBasketRequest.SellRoute))
            {
                automationConfig.LooperCloseRoute = openBasketRequest.SellRoute;
                automationConfig.LooperCloseRouteSingleLeg = openBasketRequest.SellRoute;
                automationConfig.LooperDynamicRouting = true;
            }
            if (!double.IsNaN(openBasketRequest.CloseEdge) && openBasketRequest.CloseEdge > 0)
            {
                automationConfig.ContraFishEdge = openBasketRequest.CloseEdge;
                automationConfig.LoopCloseEdgeType = LoopCloseEdgeType.Static;
                automationConfig.LoopIntervalType = LoopIntervalType.Static;
            }
            if (!double.IsNaN(openBasketRequest.CloseInterval) && openBasketRequest.CloseInterval > 0)
            {
                automationConfig.ContraFishInterval = Convert.ToInt32(openBasketRequest.CloseInterval);
                automationConfig.ContraFishIntervalMax = Convert.ToInt32(openBasketRequest.CloseInterval);
            }
            if (!double.IsNaN(openBasketRequest.CloseIncrement) && openBasketRequest.CloseIncrement > 0)
            {
                automationConfig.LoopIncrementType = LoopIncrementType.Static;
                automationConfig.ContraFishPriceIncrement = openBasketRequest.CloseIncrement;
            }
            if (!double.IsNaN(openBasketRequest.MaxLoss) && openBasketRequest.MaxLoss > 0)
            {
                automationConfig.LoopMaxLoss = openBasketRequest.MaxLoss;
            }
            if (!double.IsNaN(openBasketRequest.LoopInterval) && openBasketRequest.LoopInterval > 0)
            {
                automationConfig.LoopInterval = Convert.ToInt32(openBasketRequest.LoopInterval);
                automationConfig.LoopIntervalMax = Convert.ToInt32(openBasketRequest.LoopInterval);
            }
            if (!double.IsNaN(openBasketRequest.MinEdge) && openBasketRequest.MinEdge > 0)
            {
                automationConfig.LoopMinEdge = openBasketRequest.MinEdge;
            }
            if (openBasketRequest.SizeUpQty > 0)
            {
                automationConfig.LoopSizeupQty = openBasketRequest.SizeUpQty;
            }
            if (openBasketRequest.LoopBeforeSizeup > 0)
            {
                automationConfig.LoopCountBeforeSizeup = openBasketRequest.LoopBeforeSizeup;
            }
        }

        internal async Task LoadBlockFromOpenBasketRequest(OpenBasketRequest openBasketRequest)
        {
            BasketSettings.BidPercent = OmsCore.Config.BlockTraderPercentBid;
            ResetEdgeTypes();
            BasketSettings.UseBidPercent = true;
            if (!IsDisposed && BasketSettings.BidPercent >= OmsCore.Config.MinimumBidPercentLimit && BasketSettings.BidPercent <= OmsCore.Config.BidPercentLimit)
            {
                foreach (BasketTraderItemModel item in BasketItems)
                {
                    await item.UseBidPercent(BasketSettings.BidPercent);
                }
            }

            BasketSettings.RiskCheckEnabled = false;
            BasketSettings.CancelWithTimerEnabled = false;
            BasketSettings.FishModeEnabled = false;
            GetAutomationConfig().CloseOrderMode = null;

            BasketSettings.AdjustPriceBeforeSubmit = true;
            CheckEdge();
            ModifyOnTimer = true;
            ModifyIntervalSec = OmsCore.Config.BlockTraderModifyTimer;

            foreach (OpenTicketRequest symbol in openBasketRequest.Tickets)
            {
                foreach (Tuple<string, string> route in OmsCore.Config.BlockTraderRoutes)
                {
                    BasketTraderItemModel item = MakeBasketItemModel();
                    await item.LoadLegsFromTosAsync(symbol.Symbol)
                              .ContinueWith(x => AddToBasketAsync(item, ignoreDuplicateCheck: true))
                              .ContinueWith(x => item.Route = route.Item2);
                }
            }
        }

        internal async Task<BasketTraderItemModel> LoadFromOrderModelAsync(OmsOrderModel orderModel)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            await item.LoadFromOrderBookAsync(orderModel)
                .ContinueWith(x => AddToBasketAsync(item));
            return item;
        }

        internal void LoadNagbotFromOrderModelAsync(OmsOrderModel orderModel)
        {
            NagbotEnabled = true;
            BasketTraderItemModel item = MakeBasketItemModel();
            NagbotEnabled = true;
            item.LoadFromOrderBookAsync(orderModel)
                .ContinueWith(async x =>
                {
                    if (await AddToBasketAsync(item, ignoreDuplicateCheck: true, setPriceAfterCheck: false))
                    {
                        item.NextNagTime = default;
                        item.CurrentNagInterval = 0;
                        item.NagEnabled = true;
                    }
                });
        }

        internal void LoadNagbotFromTicketAsync(ComplexOrderTicketViewModel orderTicket)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            NagbotEnabled = true;
            item.LoadFromTicketAsync(orderTicket)
                .ContinueWith(async x =>
                {
                    if (await AddToBasketAsync(item, ignoreDuplicateCheck: true, setPriceAfterCheck: false))
                    {

                        item.BestAveragePrice = orderTicket.Price;
                        item.LastMainUnderMidAtBestFill = orderTicket.UnderMid;

                        item.NextNagTime = default;
                        item.CurrentNagInterval = 0;
                        item.NagEnabled = true;
                    }
                });
        }

        private async Task<int> PermAndAddMultiple(object parameter, PermMode permMode, PermSide permSide, int count, bool maintainBaseStrategy)
        {
            List<BasketTraderItemModel> basketItemsToAdd = await GetPermsAsync(parameter, permMode, permSide, count, maintainBaseStrategy);
            if (basketItemsToAdd.Count > 0)
            {
                await AddMultipleToBasketAsync(basketItemsToAdd);
            }
            if (parameter is BasketTraderItemModel model)
            {
                SelectedItem = model;
            }
            else if (parameter is IEnumerable orders)
            {
                List<object> selectedItems = new();
                foreach (object order in orders)
                {
                    selectedItems.Add(order);
                }
                SelectedItems = selectedItems;
            }
            return basketItemsToAdd.Count;
        }

        private async Task<List<BasketTraderItemModel>> GetPermsAsync(object parameter, PermMode permMode, PermSide permSide, int count, bool maintainBaseStrategy)
        {
            List<BasketTraderItemModel> basketItemsToAdd = new();
            if (parameter is null)
            {
                if (BasketItems.Count != 1)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                else if (BasketItems.Count == 1)
                {
                    parameter = BasketItems[0];
                }
            }
            if (parameter is BasketTraderItemModel model)
            {
                await Perm(permMode, permSide, Math.Min(MAX_PERM_PER_OP, count), basketItemsToAdd, model, maintainBaseStrategy);
            }
            else if (parameter is IEnumerable orders)
            {
                int summary = 0;
                foreach (object order in orders)
                {
                    int countToUse = count;
                    if (summary + count > MAX_PERM_PER_OP)
                    {
                        countToUse = MAX_PERM_PER_OP - summary;
                    }
                    if (countToUse > 0 && order is BasketTraderItemModel orderModel)
                    {
                        await Perm(permMode, permSide, countToUse, basketItemsToAdd, orderModel, maintainBaseStrategy);
                    }
                    summary += basketItemsToAdd.Count;

                    if (summary >= MAX_PERM_PER_OP)
                    {
                        break;
                    }
                }
            }

            return basketItemsToAdd;
        }

        private async Task Perm(PermMode permMode, PermSide permSide, int count, List<BasketTraderItemModel> basketItemsToAdd, BasketTraderItemModel orderModel, bool maintainBaseStrategy)
        {
            _submitIndex = 0;
            List<BasketTraderItemModel> perms = new();
            BasketTraderItemModel previous = orderModel;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    BasketTraderItemModel item = await GetBasketItemCloneAsync(previous);

                    switch (permMode)
                    {
                        case PermMode.ExpirationUp:
                            if (await item.IsNextPermValidAsync(permMode, permSide, maintainBaseStrategy))
                            {
                                await item.ExpirationUpAsync(permSide, skipCheck: true, maintainBaseStrategy);
                            }
                            else if (maintainBaseStrategy && item.BaseStrategy.IsCalendar())
                            {
                                await item.ExpirationUpAsync(permSide, skipCheck: false, maintainBaseStrategy);
                            }
                            break;
                        case PermMode.ExpirationDown:
                            if (await item.IsNextPermValidAsync(permMode, permSide, maintainBaseStrategy))
                            {
                                await item.ExpirationDownAsync(permSide, skipCheck: true, maintainBaseStrategy);
                            }
                            else if (maintainBaseStrategy && item.BaseStrategy.IsCalendar())
                            {
                                await item.ExpirationDownAsync(permSide, skipCheck: false, maintainBaseStrategy);
                            }
                            break;
                        case PermMode.StrikeUp:
                            if (await item.IsNextPermValidAsync(permMode, permSide, maintainBaseStrategy))
                            {
                                await item.StrikeUpAsync(permSide, skipCheck: true, maintainBaseStrategy);
                            }
                            break;
                        case PermMode.StrikeDown:
                            if (await item.IsNextPermValidAsync(permMode, permSide, maintainBaseStrategy))
                            {
                                await item.StrikeDownAsync(permSide, skipCheck: true, maintainBaseStrategy);
                            }
                            break;
                    }

                    if (previous.Description == item.Description)
                    {
                        break;
                    }

                    if (maintainBaseStrategy)
                    {
                        bool bothFly = OmsCore.Config.MaintainBaseStrategyExceptionForFlyEnabled && orderModel.BaseStrategy.IsAnyFly() && item.BaseStrategy.IsAnyFly();
                        if (orderModel.BaseStrategy != item.BaseStrategy && !bothFly)
                        {
                            previous = item;
                            continue;
                        }
                    }

                    item.SetBestRoute(true);
                    perms.Add(item);
                    basketItemsToAdd.Add(item);
                    previous = item;
                }
                catch (Exception)
                {
                    break;
                }
            }
            _ = CalculatePermAdjPxAsync(orderModel, perms);
        }

        private async Task<BasketTraderItemModel> GetBasketItemCloneAsync(BasketTraderItemModel previous, bool reverse = false, bool add = false, bool activate = true)
        {
            BasketTraderItemModel item = MakeBasketItemModel();
            item.LoadMinimalFromTicket(previous);
            item.SetBestRoute();
            if (reverse)
            {
                item.Reverse();
            }
            item.ResetPriceAndContraPrice();
            item.PreUpdate();
            foreach (TicketLegModel leg in item.Legs)
            {
                await leg.ValidateLegAsync(true);
            }
            item.PostUpdate();
            item.Active = activate;
            if (add)
            {
                await AddToBasketAsync(item, ignoreDuplicateCheck: true, setPriceAfterCheck: false);
            }
            return item;
        }

        private async Task ExpirationGreekPerm(PermutationDirection direction)
        {
            foreach (var underGroup in BasketItems.GroupBy(x => x.Underlying).ToList())
            {
                var symbols = await OmsCore.QuoteClient.GetOptionsAsync(underGroup.Key);

                var grouped = BasketItems.GroupBy(x => Tuple.Create(x.Underlying, x.BaseStrategy, x.Leg1?.ExpirationInfo, x.Leg2?.ExpirationInfo, x.Leg3?.ExpirationInfo, x.Leg4?.ExpirationInfo)).ToList();
                foreach (var group in grouped)
                {
                    var basketItems = group.ToList();
                    var minDelta = basketItems.MinBy(x => Math.Abs(x.TotalDelta));
                    var maxDelta = basketItems.MaxBy(x => Math.Abs(x.TotalDelta));
                    var count = basketItems.Count;
                    var nextExpiration =
                        await OmsCore.QuoteClient.GetNextExpirationOption(minDelta.Leg1.Security,
                            direction);
                    var targetList = symbols.Where(x =>
                        x.PutCall.ToString().ToUpper() == nextExpiration.Type.ToString().ToUpper() && x.Expiration == nextExpiration.Expiration).ToList();
                    SingleLegSpreadsGenerator generator = new(_logger, new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore());
                    SingleLegSpreadsGeneratorSettingsModel settings = new()
                    {
                        Leg1DeltaRangeEnabled = true,
                        Leg1DeltaRangeFloor = Math.Abs(minDelta.TotalDelta),
                        Leg1DeltaRangeCeil = Math.Abs(maxDelta.TotalDelta),
                    };
                    SpreadGeneratorResults result = await generator.GenerateAsync(targetList, null, false, settings,
                        count, CancellationToken.None);
                    await LoadFromSpreads(result.Spreads.ToList());
                    RemoveMultipleBasketItems(basketItems);
                }
            }
        }

        private async Task ExpirationUpSelf()
        {
            await Task.Run(async () =>
            {
                List<Task> tasks = new();
                foreach (BasketTraderItemModel item in await ValidForPermAsync(PermMode.ExpirationUp, PermSide))
                {
                    tasks.Add(item.ExpirationUpAsync(PermSide, skipCheck: true));
                }
                await Task.WhenAll(tasks);
            }).ContinueWith(t => CleanInvalidRows());
        }

        private async Task ExpirationDownSelf()
        {
            await Task.Run(async () =>
            {
                List<Task> tasks = new();
                foreach (BasketTraderItemModel item in await ValidForPermAsync(PermMode.ExpirationDown, PermSide))
                {
                    tasks.Add(item.ExpirationDownAsync(PermSide, skipCheck: true));
                }
                await Task.WhenAll(tasks);
            }).ContinueWith(t => CleanInvalidRows());
        }

        private async Task StrikeUpSelf()
        {
            await Task.Run(async () =>
            {
                List<Task> tasks = new();
                foreach (BasketTraderItemModel item in await ValidForPermAsync(PermMode.StrikeUp, PermSide))
                {
                    tasks.Add(item.StrikeUpAsync(PermSide, skipCheck: true));
                }
                await Task.WhenAll(tasks);
            }).ContinueWith(t => CleanInvalidRows());
        }

        private async Task StrikeDownSelf()
        {
            await Task.Run(async () =>
            {
                List<Task> tasks = new();
                foreach (BasketTraderItemModel item in await ValidForPermAsync(PermMode.StrikeDown, PermSide))
                {
                    tasks.Add(item.StrikeDownAsync(PermSide, skipCheck: true));
                }
                await Task.WhenAll(tasks);
            }).ContinueWith(t => CleanInvalidRows());
        }

        private async Task<List<BasketTraderItemModel>> ValidForPermAsync(PermMode permMode, PermSide permSide)
        {
            List<BasketTraderItemModel> result = new();
            foreach (BasketTraderItemModel item in BasketItems.Where(x => !x.IsActive).ToList())
            {
                if (await item.IsNextPermValidAsync(permMode, permSide, maintainBaseStrategy: false))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        private async Task CalculatePermAdjPxAsync(BasketTraderItemModel orderModel, List<Tuple<BasketTraderItemModel, BasketTraderItemModel>> basketItems)
        {
            await Task.Run(async () =>
            {
                var automationConfig = GetAutomationConfig();
                if (automationConfig != null && automationConfig.AutoPermConfigModel != null)
                {
                    foreach (Tuple<BasketTraderItemModel, BasketTraderItemModel> pair in basketItems.ToList())
                    {
                        BasketTraderItemModel item = pair.Item1;
                        BasketTraderItemModel coItem = pair.Item2;

                        Task loadMain = Task.CompletedTask;
                        Task loadConta = Task.CompletedTask;

                        switch (automationConfig.AutoPermConfigModel.PermMatchingMode)
                        {
                            case PermMatchingMode.MatchingHanweck:
                                loadMain = item?.CalculatePermAdjPxUsingMatchingHwAsync(orderModel) ?? Task.CompletedTask;
                                loadConta = coItem?.CalculatePermAdjPxUsingMatchingHwAsync(orderModel) ?? Task.CompletedTask;
                                break;
                            case PermMatchingMode.EdgeToTheo:
                                var edgeToTheo = orderModel.TagEdgeToTheo;
                                loadMain = item?.CalculatePermAdjPxUsingEdgeToTheoAsync(edgeToTheo) ?? Task.CompletedTask;
                                loadConta = coItem?.CalculatePermAdjPxUsingEdgeToTheoAsync(edgeToTheo) ?? Task.CompletedTask;
                                break;
                            case PermMatchingMode.AdjEdgeToTheo:
                                if (await orderModel.WaitForAdjTheoLoadAsync())
                                {
                                    orderModel.DeltaAdjPrice();
                                    edgeToTheo = orderModel.IsSingleLegSell
                                        ? orderModel.DeltaAdjPx - orderModel.NetDeltaAdjTheo
                                        : orderModel.NetDeltaAdjTheo - orderModel.DeltaAdjPx;

                                    loadMain = item?.CalculatePermAdjPxUsingEdgeToTheoAsync(edgeToTheo) ?? Task.CompletedTask;
                                    loadConta = coItem?.CalculatePermAdjPxUsingEdgeToTheoAsync(edgeToTheo) ?? Task.CompletedTask;
                                }
                                break;
                        }

                        await Task.WhenAll(loadMain, loadConta);

                        if (!item.PermAdjPxLoaded || (coItem != null && !coItem.PermAdjPxLoaded))
                        {
                            basketItems.Remove(pair);
                        }
                    }
                }
            });
        }

        private async Task<List<BasketTraderItemModel>> CalculatePermAdjPxAsync(BasketTraderItemModel orderModel, List<BasketTraderItemModel> basketItems)
        {
            List<BasketTraderItemModel> loaded = new();
            if (OmsCore.Config.EnablePermAdjPx)
            {
                await Task.Run(async () =>
                {
                    foreach (BasketTraderItemModel item in basketItems)
                    {
                        await item.CalculatePermAdjPxUsingMatchingHwAsync(orderModel);
                        if (item.PermAdjPxLoaded)
                        {
                            loaded.Add(item);
                        }
                    }
                });
            }
            return loaded;
        }

        private void UpdateEdgeSettings(BasketTraderItemModel item, double averagePrice)
        {
            if (averagePrice != 0)
            {
                item.WaitForTheoLoadAsync()
                    .ContinueWith(x =>
                    {
                        if (item.NetTheo > averagePrice)
                        {
                            BasketSettings.EdgeToTheo = BasketSettings.EdgeToAdjTheo = Math.Round(Math.Abs(item.NetTheo - averagePrice), 2);
                        }
                    })
                    .ContinueWith(async x =>
                    {
                        if (BasketItems != null && BasketSettings.UseEdgeToTheo)
                        {
                            foreach (BasketTraderItemModel item in BasketItems)
                            {
                                await item.UseEdgeToTheoAsync(BasketSettings.EdgeToTheo);
                            }
                        }
                        else if (BasketItems != null && BasketSettings.UseEdgeToAdjTheo)
                        {
                            foreach (BasketTraderItemModel item in BasketItems)
                            {
                                await item.UseEdgeToAdjTheoAsync(BasketSettings.EdgeToAdjTheo);
                            }
                        }
                    });
            }
        }

        private void ModifyAllOrders(string account, string route, TimeInForce? tif)
        {
            bool updateAccount = !string.IsNullOrWhiteSpace(account);
            bool updateRoute = !string.IsNullOrWhiteSpace(route);
            bool updateTif = tif.HasValue;
            foreach (BasketTraderItemModel item in BasketItems)
            {
                if (updateAccount && OmsCore.User.Accounts.Contains(account))
                {
                    item.Account = account;
                    item.AccountLocked = false;
                }
                if (updateRoute)
                {
                    item.Route = route;
                }
                if (updateTif)
                {
                    item.TimeInForce = tif.Value;
                }
            }
        }

        protected void ModifyAllPxQty(bool updateQty, int qty, bool updatePx, double px)
        {
            foreach (BasketTraderItemModel item in BasketItems)
            {
                if (item.IsActive)
                {
                    continue;
                }
                if (updateQty)
                {
                    int prevQty = item.Lcd;
                    item.UpdateQty(qty);
                    AutomationConfigModel automationConfig = GetAutomationConfig();
                    if (automationConfig != null && automationConfig.LoopSizeupType == LoopSizeupType.Down)
                    {
                        if (item.PrevQty != 0)
                        {
                            item.PrevQty = Math.Min(item.PrevQty, item.Lcd);
                        }
                        else
                        {
                            item.PrevQty = Math.Min(prevQty, item.Lcd);
                        }
                    }
                    else
                    {
                        item.PrevQty = Math.Min(item.PrevQty, item.Lcd);
                    }
                }
                if (updatePx)
                {
                    item.Price = px;
                }
            }
        }

        private void LoadFromFileAsync(string filePath)
        {
            Task.Run(() => LoadFromFile(filePath));
        }

        private async void LoadFromFile(string filePath)
        {
            try
            {
                if (filePath != null)
                {
                    Loaded = true;
                    string extention = Path.GetExtension(filePath);
                    if (!string.IsNullOrWhiteSpace(extention))
                    {
                        if (string.Equals(extention, ".json", StringComparison.InvariantCultureIgnoreCase))
                        {
                            string fileContent = File.ReadAllText(filePath);
                            await LoadOrdersFromJson(fileContent);
                        }
                        else if (extention.Contains("xls", StringComparison.OrdinalIgnoreCase) || extention.Contains("csv", StringComparison.OrdinalIgnoreCase))
                        {
                            LoadFromExcel(filePath);
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService?.ShowMessage($"Basket file not found,\n{ex.FileName}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFromFile));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error, MessageResult.OK)
                ));
            }
        }

        public async Task LoadOrdersFromJson(string fileContent, bool isApiRequest = false)
        {
            try
            {
                List<OmsOrder> orders = JsonConvert.DeserializeObject<List<OmsOrder>>(fileContent);
                await LoadFromOrders(orders, isApiRequest);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadOrdersFromJson));
            }
        }

        private void LoadFromExcel(string filePath)
        {
            try
            {
                object[,] values = ExcelHelper.ReadExcelFile(filePath);

                int start = values.GetLowerBound(0);
                int count = values.GetUpperBound(0);
                int end = values.GetUpperBound(1);
                List<Tuple<string, double>> uniqueSpreads = new();
                for (int index = start; index <= count; index++)
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    var symbolIndex = 0;
                    var edgeOverrideIndex = 1;
                    if (symbolIndex <= end)
                    {
                        if (values[index, symbolIndex] is string spreadId)
                        {
                            double edgeOverride = double.NaN;
                            if (edgeOverrideIndex <= end)
                            {
                                var value = values[index, edgeOverrideIndex];
                                if (value is string edgeOverrideStr && double.TryParse(edgeOverrideStr, out var edgeOverrideVal))
                                {
                                    edgeOverride = edgeOverrideVal;
                                }
                                else if (value is double doubleVal)
                                {
                                    edgeOverride = doubleVal;
                                }
                            }
                            uniqueSpreads.Add(Tuple.Create(spreadId, edgeOverride));
                        }
                    }
                }

                LoadFromSpreadIds(uniqueSpreads.DistinctBy(x => x.Item1).ToList());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFromExcel));
            }
        }

        private async Task LoadFromOrders(List<OmsOrder> orders, bool isApiRequest = false)
        {
            ProgressValue = 0;
            ShowProgressBar = true;
            List<BasketTraderItemModel> models = new();
            var loadTasks = new List<Task>(orders.Count);
            for (int index = 0; index < orders.Count; index++)
            {
                if (IsDisposed)
                {
                    break;
                }
                OmsOrder order = orders[index];
                order.Price = 0.0;

                BasketTraderItemModel model = MakeBasketItemModel();
                var task = model.LoadFromOrder(order);
                loadTasks.Add(task);
                if (model.EdgeOverride <= 0.0)
                {
                    model.EdgeOverride = double.NaN;
                }

                if (isApiRequest)
                {
                    model.UpdateQty(1);
                    model.SubType = order.Subtype?.TryGetSubType();
                }
                models.Add(model);
                ProgressValue = (index + 1) * 100 / orders.Count;
            }

            await Task.WhenAll(loadTasks);
            await AddMultipleToBasketAsync(models, updateIfExists: isApiRequest);

            if (IsDisposed)
            {
                foreach (BasketTraderItemModel item in models)
                {
                    _log.Info(nameof(LoadFromOrders) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                }
                return;
            }

            ProgressValue = 0;
            ShowProgressBar = false;
        }

        public async Task DeleteOrders(string json)
        {
            try
            {
                List<string> orders = await Task.Run(() => JsonConvert.DeserializeObject<List<string>>(json));
                DeleteOrders(orders.ToHashSet());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeleteOrders));
            }
        }

        private void DeleteOrders(HashSet<string> orders)
        {
            foreach (BasketTraderItemModel order in BasketItems)
            {
                CheckForMatchAndRemove(orders, order);
            }
        }

        private void CheckForMatchAndRemove(HashSet<string> orders, BasketTraderItemModel order)
        {
            try
            {
                if (orders.Contains(order.SpreadSymbol))
                {
                    Remove(order);
                }
                else
                {
                    SymbolCodec codec = new(order.SpreadSymbol);
                    codec.Invert();
                    string symbol = codec.ToTOS();
                    if (orders.Contains(symbol))
                    {
                        Remove(order);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForMatchAndRemove));
            }
        }

        internal async Task AddOrders(string json)
        {
            try
            {
                List<string> orders = await Task.Run(() => JsonConvert.DeserializeObject<List<string>>(json));

                if (orders != null)
                {
                    await LoadFromSpreadIdsAsync(orders.Select(x => Tuple.Create(x, double.NaN)).ToList());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddOrders));
            }
        }

        internal async Task LoadFromSpreadResultsAsync(List<SpreadGeneratorResults> resuls)
        {
            await Task.Run(() => LoadFromSpreadResults(resuls));
        }

        private async Task LoadFromSpreadResults(List<SpreadGeneratorResults> resuls)
        {
            List<Spread> spreads = resuls.SelectMany(x => x.Spreads).ToList();
            await LoadFromSpreads(spreads);
        }

        private async Task LoadFromSpreads(List<Spread> spreads)
        {
            ProgressValue = 0;
            ShowProgressBar = true;
            List<BasketTraderItemModel> models = new();
            List<Task> tasks = new();
            for (int i = 0; i < spreads.Count; i++)
            {
                try
                {
                    if (IsDisposed)
                    {
                        break;
                    }
                    Spread spread = spreads[i];
                    string spreadId = spread.Symbol;
                    BasketTraderItemModel model = MakeBasketItemModel();
                    models.Add(model);
                    model.EdgeOverride = spread.EdgeOverride;
                    Task task = model.LoadLegsFromTosAsync(spreadId, string.IsNullOrWhiteSpace(spread.Side) ? null : spread.Side.Equals(Side.Buy.ToString(), StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell);
                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(LoadFromSpreadIds));
                }
                ProgressValue = (i + 1) * 100 / spreads.Count;
            }

            await Task.WhenAll(tasks).ContinueWith(t => AddMultipleToBasketAsync(models));

            if (IsDisposed)
            {
                foreach (BasketTraderItemModel item in models)
                {
                    _log.Info(nameof(LoadFromSpreadResults) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                }

                return;
            }

            ProgressValue = 0;
            ShowProgressBar = false;
        }

        internal async Task LoadFromSpreadIdsAsync(List<Tuple<string, double>> uniqueSpreads)
        {
            await Task.Run(() => LoadFromSpreadIds(uniqueSpreads));
        }

        internal async void LoadFromSpreadIds(List<Tuple<string, double>> spreads)
        {
            ProgressValue = 0;
            ShowProgressBar = true;
            List<BasketTraderItemModel> models = new();
            var tasks = new List<Task>();
            for (int index = 0; index < spreads.Count; index++)
            {
                try
                {
                    if (IsDisposed)
                    {
                        break;
                    }

                    string spreadId = spreads[index].Item1;
                    double edgeOverride = spreads[index].Item2;

                    BasketTraderItemModel model = MakeBasketItemModel();
                    models.Add(model);
                    var loadTask = model.LoadLegsFromTosAsync(spreadId);
                    tasks.Add(loadTask);
                    model.EdgeOverride = edgeOverride;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(LoadFromSpreadIds));
                }
                ProgressValue = (index + 1) * 100 / spreads.Count;
            }

            await Task.WhenAll(tasks);

            Dispatcher?.BeginInvoke(new Action(async () =>
            {
                await AddMultipleToBasketAsync(models);
            }));

            if (IsDisposed)
            {
                foreach (BasketTraderItemModel item in models)
                {
                    _log.Info(nameof(LoadFromSpreadIds) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                }
                return;
            }

            ProgressValue = 0;
            ShowProgressBar = false;
        }

        internal async Task LoadBlockFromTradeAsync(OpraDatabaseTradeModel trade)
        {
            if (OmsCore.Config.BlockTraderPercentBid >= OmsCore.Config.MinimumBidPercentLimit &&
                OmsCore.Config.BlockTraderPercentBid <= OmsCore.Config.BidPercentLimit)
            {
                List<Task> tasks = new();
                foreach (var route in OmsCore.Config.BlockTraderRoutes.Select(x => x.Item2))
                {
                    BasketTraderItemModel item = MakeBasketItemModel();
                    Task<string> loadTask = item.LoadFromTradeAsync(trade)
                        .ContinueWith(x => AddToBasketAsync(item, ignoreDuplicateCheck: true))
                        .ContinueWith(x => item.Route = route);
                    tasks.Add(loadTask);
                }

                await Task.WhenAll(tasks);

                BasketSettings.RiskCheckEnabled = false;
                BasketSettings.CancelWithTimerEnabled = false;
                BasketSettings.FishModeEnabled = false;
                GetAutomationConfig().CloseOrderMode = null;

                BasketSettings.BidPercent = OmsCore.Config.BlockTraderPercentBid;
                ResetEdgeTypes();
                BasketSettings.UseBidPercent = true;
                CheckEdge();

                foreach (BasketTraderItemModel item in BasketItems)
                {
                    await item.UseBidPercent(BasketSettings.BidPercent);
                }

                BasketSettings.AdjustPriceBeforeSubmit = true;

                ModifyOnTimer = true;
                ModifyIntervalSec = OmsCore.Config.BlockTraderModifyTimer;
            }
        }

        private void WriteToFile()
        {
            Loaded = true;
            string jsonString = GetOrdersJson();
            File.WriteAllText(FilePath, jsonString);
        }

        public string GetOrdersJson()
        {
            if (BasketItems != null)
            {
                List<OmsOrder> orders = BasketItems.Select(x => x.ToOrder()).ToList();
                string jsonString = JsonConvert.SerializeObject(orders, Formatting.Indented);
                return jsonString;
            }
            else
            {
                return "";
            }
        }

        internal async Task<List<BasketTraderItemModel>> AddMultipleToBasketAsync(IEnumerable<BasketTraderItemModel> items, bool updateIfExists = false)
        {
            try
            {
                _submitIndex = 0;
                if (AvoidDuplicates)
                {
                    HashSet<BasketTraderItemModel> uniqueItems = items.GroupBy(x => Tuple.Create(x.Description, x.Legs?.FirstOrDefault()?.Security?.RootSymbol))
                                                                   .Select(g => g.First())
                                                                   .ToHashSet();
                    if (uniqueItems.Count != items.Count())
                    {
                        DisposeBasketItems(items.Where(x => !uniqueItems.Contains(x)));
                    }
                    items = uniqueItems;
                }

                return await Task.Run(async () =>
                {
                    ConcurrentBag<BasketTraderItemModel> toBeAdded = new();
                    List<Task> tasks = new();
                    BasketTraderItemModel[] array = items.ToArray();
                    for (int index = 0; index < array.Length; index++)
                    {
                        if (IsDisposed)
                        {
                            break;
                        }

                        BasketTraderItemModel item = array[index];

                        if (!IsDisposed && BasketSettings.StockTiedEnabled)
                        {
                            await item.SetupStockTieAsync();
                        }

                        if (updateIfExists)
                        {
                            bool found = await UpdateExistingOrders(item);
                            if (found)
                            {
                                continue;
                            }
                        }

                        Task task = IsValidBasketItemAsync(item, ignoreDuplicateCheck: false, setPriceAfterCheck: false).ContinueWith(t =>
                        {
                            if (t.Result)
                            {
                                item.BasketTraderViewModel = this;
                                item.BasketSettings = BasketSettings;
                                item.Dispatcher = Dispatcher;
                                item.ToggleTheoLock();
                                RegisterEvents(item);
                                toBeAdded.Add(item);
                            }
                        });
                        tasks.Add(task);
                    }

                    await Task.WhenAll(tasks);

                    if (toBeAdded.Count > 0)
                    {
                        await Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            foreach (BasketTraderItemModel item in toBeAdded)
                            {
                                BasketItems.Add(item);
                            }
                        }));
                        _ = UpdateRoutesListAsync();
                    }
                    SetTitle();
                    return toBeAdded.ToList();
                });
            }
            finally
            {
                if (IsDisposed)
                {
                    DisposeBasketItems(items);
                }
            }
        }

        public async Task<bool> AddToBasketAsync(BasketTraderItemModel item, bool ignoreDuplicateCheck = false, bool setPriceAfterCheck = true)
        {
            _submitIndex = 0;

            if (!IsDisposed && BasketSettings.StockTiedEnabled)
            {
                await item.SetupStockTieAsync();
            }

            if (await IsValidBasketItemAsync(item, ignoreDuplicateCheck, setPriceAfterCheck))
            {
                await Dispatcher?.BeginInvoke(new Action(() => BasketItems.Add(item)));
                item.BasketTraderViewModel = this;
                item.BasketSettings = BasketSettings;
                item.Dispatcher = Dispatcher;
                item.ToggleTheoLock();
                RegisterEvents(item);
                _ = UpdateRoutesListAsync();
                SetTitle();
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task<bool> UpdateExistingOrders(BasketTraderItemModel item)
        {
            try
            {
                IEnumerable<BasketTraderItemModel> matchingOrders = BasketItems.Where(x => x.Description == item.Description);
                if (matchingOrders.Count() > 0)
                {
                    await Dispatcher?.BeginInvoke(() =>
                    {
                        foreach (BasketTraderItemModel order in matchingOrders)
                        {
                            order.EdgeOverride = item.EdgeOverride;
                            order.SubType = item.SubType;
                        }
                    });
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateExistingOrders));
                return false;
            }
        }

        private void DisposeBasketItems(IEnumerable<BasketTraderItemModel> items)
        {
            try
            {
                foreach (BasketTraderItemModel item in items)
                {
                    _log.Info(nameof(DisposeBasketItems) + " Disposing order model for " + item.SpreadId);
                    item.Dispose();
                    UnregisterEvents(item);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisposeBasketItems));
            }
        }

        private void MigrateLegacyAutomationRoutes()
        {
            try
            {
                var configs = GetAutomationConfigs();
                if (configs == null || configs.Count == 0)
                {
                    return;
                }

                foreach (var config in configs)
                {
                    if (config == null)
                    {
                        continue;
                    }

                    config.LooperOpenRoute = MigrateRoute(config.LooperOpenRoute);
                    config.LooperCloseRoute = MigrateRoute(config.LooperCloseRoute);
                    config.LooperOpenRouteSize = MigrateRoute(config.LooperOpenRouteSize);
                    config.LooperCloseRouteSize = MigrateRoute(config.LooperCloseRouteSize);
                    config.LooperOpenRouteSingleLeg = MigrateRoute(config.LooperOpenRouteSingleLeg);
                    config.LooperCloseRouteSingleLeg = MigrateRoute(config.LooperCloseRouteSingleLeg);
                    config.LooperOpenRouteSingleLegSize = MigrateRoute(config.LooperOpenRouteSingleLegSize);
                    config.LooperCloseRouteSingleLegSize = MigrateRoute(config.LooperCloseRouteSingleLegSize);
                    config.StockTiedOrderRoute = MigrateRoute(config.StockTiedOrderRoute);
                    config.LoopFreeLookOnNickelNamesRoute = MigrateRoute(config.LoopFreeLookOnNickelNamesRoute);
                    config.LoopFreeLookOnDimeNamesRoute = MigrateRoute(config.LoopFreeLookOnDimeNamesRoute);
                    config.AutoLegCloseRoute = MigrateRoute(config.AutoLegCloseRoute);

                    if (config.ExchToRouteMap != null && config.ExchToRouteMap.Count > 0)
                    {
                        foreach (var key in config.ExchToRouteMap.Keys.ToList())
                        {
                            config.ExchToRouteMap[key] = MigrateRoute(config.ExchToRouteMap[key]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(MigrateLegacyAutomationRoutes));
            }
        }

        private string MigrateRoute(string saved)
        {
            if (!OrderTicket.TryMigrateLegacyRoute(saved, out var broker, out var routeName))
            {
                return saved;
            }

            if (!BrokerLocked && !string.Equals(broker, EffectiveBroker, StringComparison.OrdinalIgnoreCase))
            {
                BrokerOverride = broker;
            }

            return routeName;
        }

        private async Task UpdateRoutesListAsync()
        {
            try
            {
                MigrateLegacyAutomationRoutes();

                HashSet<string> uniqueRoutes = new(StringComparer.OrdinalIgnoreCase);
                var currentBroker = EffectiveBroker;
                var account = BasketItems.FirstOrDefault()?.Account ?? OmsCore.Config.DefaultAccount;
                var venue = GetVenue();
                var orderTypes = BasketItems.Select(item => item.GetOrderType()).Distinct().ToArray();

                var routeLookup = OmsCore.OrderClient?.RouteLookup;
                var ogRoutes = !string.IsNullOrWhiteSpace(currentBroker)
                    ? (routeLookup?.GetRoutesForBroker(currentBroker, orderTypes, account, venue, activeOnly: true) ?? Array.Empty<string>())
                    : (routeLookup?.GetRoutes(orderTypes, account, venue, activeOnly: true) ?? Array.Empty<string>());
                foreach (var route in ogRoutes)
                {
                    uniqueRoutes.Add(route);
                }

                uniqueRoutes.Add(RouteSelectionViewModel.StripBrokerPrefix(OmsCore.Config.DefaultRouteSpxRutXsp(InstanceMode)));
                uniqueRoutes.Add(RouteSelectionViewModel.StripBrokerPrefix(OmsCore.Config.DefaultRouteNdx(InstanceMode)));
                uniqueRoutes.Add(RouteSelectionViewModel.StripBrokerPrefix(OmsCore.Config.DefaultRoute(InstanceMode)));
                uniqueRoutes.Add(RouteSelectionViewModel.StripBrokerPrefix(OmsCore.Config.DefaultSingleLegRoute(InstanceMode)));
                uniqueRoutes.Add(RouteSelectionViewModel.StripBrokerPrefix(OmsCore.Config.DefaultCurbSessionRoute(InstanceMode)));

                if (Dispatcher != null)
                {
                    await Dispatcher.BeginInvoke(new Action(() => PopulateRoutes(uniqueRoutes)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateRoutesListAsync));
            }
        }

        private void PopulateRoutes(HashSet<string> uniqueRoutes)
        {
            try
            {
                RoutesList.Clear();
                DmaRoutesList.Clear();
                SorRoutesList.Clear();
                RoutesList.Add("");
                foreach (string route in uniqueRoutes.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x))
                {
                    RoutesList.Add(route);
                    if (IsSorRoute(route))
                    {
                        SorRoutesList.Add(route);
                    }
                    else
                    {
                        DmaRoutesList.Add(route);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PopulateRoutes));
            }
        }

        private bool IsSorRoute(string route)
        {
            return OmsCore.OrderClient?.RouteLookup?.IsSmartRoute(route) ?? false;
        }

        public async Task<bool> IsValidBasketItemAsync(BasketTraderItemModel item, bool ignoreDuplicateCheck = false, bool setPriceAfterCheck = false)
        {
            bool isDuplicate = !ignoreDuplicateCheck && BasketItems.ToList().Where(x => x.Description == item.Description).Any();
            bool isInvalid = string.IsNullOrEmpty(item.Description) || ((item.Description.StartsWith("CUSTOM") || item.Description.StartsWith("INVALID")) && !item.ContainsCheapo());

            if (!isInvalid && BasketType == BasketType.LockTrader)
            {
                isInvalid = string.IsNullOrEmpty(item.SpreadType) || !OmsCore.Config.LockTraderAllowedStrategies.Contains(item.BaseStrategy.ToString());
            }

            if ((!isDuplicate || (isDuplicate && !AvoidDuplicates)) && (!isInvalid || (isInvalid && !AvoidInvalid)))
            {
                if (await item.IsValidOrder())
                {
                    if (setPriceAfterCheck)
                    {
                        _ = item.SetEdgeAsync();
                    }
                    return true;
                }
            }

            _log.Info("{} Disposing order model for {}, Dup: {}, IgnDup: {}, Inv: {}, SetP: {}, Avoid: {}", nameof(IsValidBasketItemAsync), item.SpreadId, isDuplicate, ignoreDuplicateCheck, isInvalid, setPriceAfterCheck, AvoidInvalid);
            item.Dispose();
            return false;
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            BasketTraderConfig config = LoadConfigFromJson(configJson);

            if (withContent)
            {
                if (!string.IsNullOrEmpty(config.BasketItems))
                {
                    List<OmsOrder> orders = JsonConvert.DeserializeObject<List<OmsOrder>>(config.BasketItems);
                    await LoadFromOrders(orders);
                }
                else
                {
                    LoadFromFileAsync(FilePath);
                }
            }
        }

        public BasketTraderConfig LoadConfigFromJson(string configJson, bool isApiRequest = false)
        {
            try
            {
                BasketTraderConfig config = JsonConvert.DeserializeObject<BasketTraderConfig>(configJson);
                return LoadConfig(config, isApiRequest);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJson));
                throw;
            }
        }

        public BasketTraderConfig LoadConfig(BasketTraderConfig config, bool isApiRequest = false)
        {
            FileName = config.FileName;
            FilePath = config.FilePath;

            AutomationConfigModel automationConfig = GetAutomationConfig();
            if (automationConfig == null)
            {
                automationConfig = new AutomationConfigModel()
                {
                    FishEdge = 0.10,
                    ContraFishEdge = 0.10,
                    FishInterval = 1100,
                    FishPriceIncrement = 0.01,
                    LoopIncrementType = LoopIncrementType.Static,
                    LoopIntervalType = LoopIntervalType.Static,
                    LoopCloseEdgeType = LoopCloseEdgeType.Static,
                    ContraFishPriceIncrement = 0.01,
                };
                BasketSettings.AutomationConfig = automationConfig;
            }

            BasketSettings.FishModeEnabled = config.FishModeEnabled;
            automationConfig.FishEdge = config.FishEdge;
            automationConfig.FishInterval = config.FishInterval;
            automationConfig.FishPriceIncrement = config.FishPriceIncrement;

            AutoClean = config.AutoClean;

            automationConfig.UseResubmit = config.UseResubmit;
            automationConfig.LeaveAutoCloseResting = config.LeaveAutoCloseResting;

            automationConfig.SizeUpOnClosingLoop = config.SizeUpOnClosingLoop;
            automationConfig.SizeUpOnHardSideOnly = config.SizeUpOnHardSideOnly;
            automationConfig.RequireAdjEdgeForSizeUp = false;
            automationConfig.ContraFishEdge = config.ContraFishEdge;
            automationConfig.LoopIncrementType = config.LoopIncrementType;
            automationConfig.LoopIntervalType = config.LoopIntervalType;
            automationConfig.LoopCloseEdgeType = config.LoopCloseEdgeType;
            automationConfig.ContraFishInterval = config.ContraFishIntervalV2;
            automationConfig.ContraFishIntervalMax = Math.Max(config.ContraFishIntervalV2, config.ContraFishIntervalMaxV2);
            automationConfig.ContraFishPriceIncrement = config.ContraFishPriceIncrement;

            BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled = config.CancelWithOrderPriceEdgeToTheoEnabled;
            BasketSettings.CancelWithOrderPriceEdgeToTheo = config.CancelWithOrderPriceEdgeToTheo;

            BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled = config.CancelWithOrderPriceEdgeToModelTheoEnabled;
            BasketSettings.CancelWithOrderPriceEdgeToModelTheo = config.CancelWithOrderPriceEdgeToModelTheo;

            BasketSettings.CancelWithEdgeToTheoEnabled = config.CancelWithEdgeToTheoEnabled;
            BasketSettings.CancelWithTheoEdge = config.CancelWithTheoEdge;

            BasketSettings.CancelWithEdgeToAdjTheoEnabled = config.CancelWithEdgeToAdjTheoEnabled;
            BasketSettings.CancelWithAdjTheoEdge = config.CancelWithAdjTheoEdge;

            BasketSettings.SubmitWithDelayEnabled = config.SubmitWithDelayEnabled;
            BasketSettings.OpenTicketForFills = config.OpenTicketForFills;
            BasketSettings.OpenTicketForFailedClose = config.OpenTicketForFailedClose;
            BasketSettings.OpenTicketOnEdgeAcquired = config.OpenTicketOnEdgeAcquired;
            BasketSettings.StartProcessingFromSelectedRow = config.StartProcessingFromSelectedRow;
            BasketSettings.Randomize = config.Randomize;
            BasketSettings.Resume = config.Resume;
            BasketSettings.DisablePriceRounding = config.DisablePriceRounding;

            BasketSettings.CancelWithUnderlyingPxEnabled = config.CancelWithUnderlyingPxEnabled;
            BasketSettings.CancelWithUnderlyingPx = config.CancelWithUnderlyingPx;

            BasketSettings.CancelWithUnderlyingDeltaPxEnabled = config.CancelWithUnderlyingDeltaPxEnabled;
            BasketSettings.CancelWithUnderlyingDeltaPx = config.CancelWithUnderlyingDeltaPx;

            BasketSettings.CancelWithMaxSizeEnabled = config.CancelWithMaxSizeEnabled;
            BasketSettings.CancelWithMaxSizeLimit = config.CancelWithMaxSizeLimit;

            BasketSettings.CancelWithTimerEnabled = config.CancelWithTimerEnabled;
            BasketSettings.CancelWithTimer = config.CancelWithTimer;

            BasketSettings.CancelWithEdgeToMidEnabled = config.CancelWithEdgeToMidEnabled;
            BasketSettings.CancelWithMidEdge = config.CancelWithMidEdge;

            BasketSettings.CancelWithWidthEnabled = config.CancelWithWidthEnabled;
            BasketSettings.CancelWithWidthThreshold = config.CancelWithWidthThreshold;

            BasketSettings.MaxWidthCheckEnabled = config.MaxWidthCheckEnabled;
            BasketSettings.MaxWidthCheckPx = config.MaxWidthCheckPx;

            BasketSettings.MinTheoEdgeCheckEnabled = config.MinTheoEdgeCheckEnabled;
            BasketSettings.MinTheoEdgeCheckEdge = config.MinTheoEdgeCheckEdge;

            BasketSettings.MinHwTheoEdgeCheckEnabled = config.MinHwTheoEdgeCheckEnabled;
            BasketSettings.MinHwTheoEdgeCheckEdge = config.MinHwTheoEdgeCheckEdge;

            BasketSettings.MinV0TheoEdgeCheckEnabled = config.MinV0TheoEdgeCheckEnabled;
            BasketSettings.MinV0TheoEdgeCheckEdge = config.MinV0TheoEdgeCheckEdge;

            BasketSettings.MinBidCheckEnabled = config.MinBidCheckEnabled;
            BasketSettings.MinBidCheckBidValue = config.MinBidCheckBidValue;

            BasketSettings.MinTheoCheckEnabled = config.MinTheoCheckEnabled;
            BasketSettings.MinTheoCheckTheoValue = config.MinTheoCheckTheoValue;

            BasketSettings.MinEdgeToMarketCheckEnabled = config.MinEdgeToMarketCheckEnabled;
            BasketSettings.MinEdgeToMarketCheckEdge = config.MinEdgeToMarketCheckEdge;

            BasketSettings.IgnoreSkewMktCheckIfBothSidesFail = config.IgnoreSkewMktCheckIfBothSidesFail;

            BasketSettings.AdjustAfterMinEdgeToSkewMarketCheck = config.AdjustAfterMinEdgeToSkewMarketCheck;
            BasketSettings.MinEdgeToSkewMarketCheckEnabled = config.MinEdgeToSkewMarketCheckEnabled;
            BasketSettings.MinEdgeToSkewMarketCheckEdge = config.MinEdgeToSkewMarketCheckEdge;

            BasketSettings.AdjustAfterMinEdgeToSkewMarketCrossCheck = config.AdjustAfterMinEdgeToSkewMarketCrossCheck;
            BasketSettings.MinEdgeToSkewMarketCrossCheckEnabled = config.MinEdgeToSkewMarketCrossCheckEnabled;
            BasketSettings.MinEdgeToSkewMarketCrossCheckEdge = config.MinEdgeToSkewMarketCrossCheckEdge;

            BasketSettings.BlockZeroPrice = config.BlockZeroPrice;
            BasketSettings.BlockSubmissionOnTheoJump = config.BlockSubmissionOnTheoJump;

            BasketSettings.MaxPercentBidCheckUseBestQuote = config.MaxPercentBidCheckUseBestQuote;
            BasketSettings.MinPercentBidCheckEnabled = config.MinPercentBidCheckEnabled;
            BasketSettings.MinPercentBidCheckEdge = config.MinPercentBidCheckEdge;
            BasketSettings.MaxPercentBidCheckEnabled = config.MaxPercentBidCheckEnabled;
            BasketSettings.MaxPercentBidCheckEdge = config.MaxPercentBidCheckEdge;
            BasketSettings.MaxDigPercentBidCheckEnabled = config.MaxDigPercentBidCheckEnabled;
            BasketSettings.MaxDigPercentBidCheckEdge = config.MaxDigPercentBidCheckEdge;

            BasketSettings.MinBidAskSizeCheckEnabled = config.MinBidAskSizeCheckEnabled;
            BasketSettings.MinBidAskSize = config.MinBidAskSize;

            BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled = config.MinEmaWidthPercentEdgeToTheoCheckEnabled;
            BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge = config.MinEmaWidthPercentEdgeToTheoCheckEdge;

            BasketSettings.PreviousAttemptCrossCheckEnabled = config.PreviousAttemptCrossCheckEnabled;
            BasketSettings.MinEdgeToPreviousAttemptCheckEnabled = config.MinEdgeToPreviousAttemptCheckEnabled;
            BasketSettings.MinTimeToPreviousAttemptCheckEnabled = config.MinTimeToPreviousAttemptCheckEnabled;
            BasketSettings.MinTimeToPreviousAttemptIntervalSeconds = config.MinTimeToPreviousAttemptIntervalSeconds;
            BasketSettings.MinTimeToPermLoserCheckEnabled = config.MinTimeToPermLoserCheckEnabled;
            BasketSettings.MinTimeToPermLoserIntervalSeconds = config.MinTimeToPermLoserIntervalSeconds;

            BasketSettings.MinMidEdgeCheckEnabled = config.MinMidEdgeCheckEnabled;
            BasketSettings.MinMidEdgeCheckEdge = config.MinMidEdgeCheckEdge;

            BasketSettings.MinEmaEdgeCheckEnabled = config.MinEmaEdgeCheckEnabled;
            BasketSettings.MinEmaEdgeCheckEdge = config.MinEmaEdgeCheckEdge;

            BasketSettings.DerivedValuesEnabled = config.DerivedValuesEnabled;

            BasketSettings.DynamicUpdateEdgeOverrides = config.DynamicUpdateEdgeOverrides;
            BasketSettings.EvaluateAdjustedEdgeOverrides = config.EvaluateAdjustedEdgeOverrides;
            BasketSettings.AdjustedEdgeOverrideCushionValue = config.AdjustedEdgeOverrideCushionValue;
            BasketSettings.DeltaCapEnabled = config.DeltaCapEnabled;
            BasketSettings.DeltaCapUpperBound = config.DeltaCapUpperBound;
            BasketSettings.DeltaCapLowerBound = config.DeltaCapLowerBound;
            BasketSettings.ModifyPxWithMktChange = config.ModifyPxWithMktChange;

            BasketSettings.StrikeCapEnabled = config.StrikeCapEnabled;
            BasketSettings.StrikeCapUpperBound = config.StrikeCapUpperBound;
            BasketSettings.StrikeCapLowerBound = config.StrikeCapLowerBound;

            BasketSettings.WidthCapEnabled = config.WidthCapEnabled;
            BasketSettings.WidthCapUpperBound = config.WidthCapUpperBound;
            BasketSettings.WidthCapLowerBound = config.WidthCapLowerBound;

            BasketSettings.CancelOnAmountOfFillsCount = config.CancelOnAmountOfFillsCount == 0 ? 1 : Math.Min(config.CancelOnAmountOfFillsCount, OmsCore.Config.MaxCancelOnLimitV2);
            BasketSettings.MaxRestingOrdersEnabled = config.MaxRestingOrdersEnabledV3;
            BasketSettings.MaxRestingOrdersCount = Math.Max(1, config.MaxRestingOrdersCount);

            BasketSettings.DisableMultipleRestingSizeOrders = config.DisableMultipleRestingSizeOrders;

            PermSelf = config.PermSelf;
            AvoidInvalid = config.AvoidInvalidItems;

            BasketSettings.UseEdgeToTheo = false;
            BasketSettings.UseEdgeToHistoricBest = false;
            BasketSettings.UseEdgeToAdjTheo = false;
            BasketSettings.UseLastFillAdjPx = false;
            BasketSettings.UseCustomFunctionEdge = false;
            BasketSettings.UseDomStyleEdge = false;
            BasketSettings.UseEdgeToMid = false;
            BasketSettings.UseEdgeToEma = false;
            BasketSettings.UseEdgeToTheoAndMid = false;
            BasketSettings.UseEdgeToTheoStopMid = false;
            BasketSettings.UseEdgeToEmaStopMid = false;
            BasketSettings.UseEdgeToMidStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = false;
            BasketSettings.UseTheoBidPercent = false;
            BasketSettings.UseBidPercent = false;
            BasketSettings.UseEdgeToEmaBid = false;
            BasketSettings.UseEdgeToBid = false;
            BasketSettings.UsePermAdjPx = false;
            BasketSettings.UseBestOfEdge = false;

            if (config.UseEdgeToTheo)
            {
                BasketSettings.UseEdgeToTheo = true;
            }
            else if (config.UseEdgeToHistoricBest)
            {
                BasketSettings.UseEdgeToHistoricBest = true;
            }
            else if (config.UseEdgeToAdjTheo)
            {
                BasketSettings.UseEdgeToAdjTheo = true;
            }
            else if (config.UseLastFillAdjPx)
            {
                BasketSettings.UseLastFillAdjPx = true;
            }
            else if (config.UseEdgeToMid)
            {
                BasketSettings.UseEdgeToMid = true;
            }
            else if (config.UseEdgeToEma)
            {
                BasketSettings.UseEdgeToEma = true;
            }
            else if (config.UseEdgeToTheoAndMid)
            {
                BasketSettings.UseEdgeToTheoAndMid = true;
            }
            else if (config.UseEdgeToTheoStopMid)
            {
                BasketSettings.UseEdgeToTheoStopMid = true;
            }
            else if (config.UseEdgeToEmaStopMid)
            {
                BasketSettings.UseEdgeToEmaStopMid = true;
            }
            else if (config.UseEdgeToMidStopEma)
            {
                BasketSettings.UseEdgeToMidStopEma = true;
            }
            else if (config.UseEdgeToBidPercentStopEma)
            {
                BasketSettings.UseEdgeToBidPercentStopEma = true;
            }
            else if (config.UseEdgeToBidPercentStopEmaStopTheo)
            {
                BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = true;
            }
            else if (config.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = true;
            }
            else if (config.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = true;
            }
            else if (config.UseTheoBidPercent)
            {
                BasketSettings.UseTheoBidPercent = true;
            }
            else if (config.UseBidPercent)
            {
                BasketSettings.UseBidPercent = true;
            }
            else if (config.UseEdgeToEmaBid)
            {
                BasketSettings.UseEdgeToEmaBid = true;
            }
            else if (config.UseEdgeToBid)
            {
                BasketSettings.UseEdgeToBid = true;
            }
            else if (config.UseCustomFunctionEdge)
            {
                BasketSettings.UseCustomFunctionEdge = true;
            }
            else if (config.UseDomStyleEdge)
            {
                BasketSettings.UseDomStyleEdge = true;
            }
            else if (config.UseEdgeToAdjTheoWithOverride)
            {
                BasketSettings.UseEdgeToAdjTheoWithOverride = true;
            }
            else if (config.UseBestOfEdge)
            {
                BasketSettings.UseBestOfEdge = true;
            }

            BasketSettings.BestOfAdjTheoEnabled = config.BestOfAdjTheoEnabled;
            BasketSettings.BestOfAdjTheoEdge = config.BestOfAdjTheoEdge;
            BasketSettings.BestOfAdjTheoModel = config.BestOfAdjTheoModel;
            BasketSettings.BestOfHwTheoEnabled = config.BestOfHwTheoEnabled;
            BasketSettings.BestOfHwTheoEdge = config.BestOfHwTheoEdge;
            BasketSettings.BestOfV0TheoEnabled = config.BestOfV0TheoEnabled;
            BasketSettings.BestOfV0TheoEdge = config.BestOfV0TheoEdge;
            BasketSettings.BestOfMidEnabled = config.BestOfMidEnabled;
            BasketSettings.BestOfMidEdge = config.BestOfMidEdge;
            BasketSettings.BestOfEmaEnabled = config.BestOfEmaEnabled;
            BasketSettings.BestOfEmaEdge = config.BestOfEmaEdge;
            BasketSettings.BestOfBidPercentEnabled = config.BestOfBidPercentEnabled;
            BasketSettings.BestOfBidPercentEdge = config.BestOfBidPercentEdge;
            BasketSettings.BestOfDigBidPercentEnabled = config.BestOfDigBidPercentEnabled;
            BasketSettings.BestOfDigBidPercentEdge = config.BestOfDigBidPercentEdge;

            ShowBestOfEdgeExpanded = config.ShowBestOfEdgeExpanded;
            BestOfEdgeLocked = config.BestOfEdgeLocked;

            BasketSettings.BestOfAdjTheoPinned = config.BestOfAdjTheoPinned;
            BasketSettings.BestOfHwTheoPinned = config.BestOfHwTheoPinned;
            BasketSettings.BestOfV0TheoPinned = config.BestOfV0TheoPinned;
            BasketSettings.BestOfMidPinned = config.BestOfMidPinned;
            BasketSettings.BestOfEmaPinned = config.BestOfEmaPinned;
            BasketSettings.BestOfBidPercentPinned = config.BestOfBidPercentPinned;
            BasketSettings.BestOfDigBidPercentPinned = config.BestOfDigBidPercentPinned;

            BasketSettings.EdgeToTheo = config.EdgeToTheo;
            BasketSettings.EdgeToHistoricBest = config.EdgeToHistoricBest;
            BasketSettings.EdgeToAdjTheo = config.EdgeToAdjTheo;
            BasketSettings.EmaModel = config.EmaModel;
            BasketSettings.TheoModel = config.TheoModel;
            BasketSettings.FishLossTheoModel = config.FishLossTheoModel;
            BasketSettings.AutoCancelTheoModel = config.AutoCancelTheoModel;
            BasketSettings.CustomFunctionEdgeFormula = config.CustomFunctionEdgeFormula;
            BasketSettings.DominatorConfiguration = config.DominatorConfiguration;
            BasketSettings.LastFillAdjEdge = config.LastFillAdjEdge;
            BasketSettings.EdgeToMid = config.EdgeToMid;
            BasketSettings.EdgeToEma = config.EdgeToEma;
            BasketSettings.EdgeToTheoAndMid = config.EdgeToTheoAndMid;
            BasketSettings.EdgeToTheoStopMid = config.EdgeToTheoStopMid;
            BasketSettings.EdgeToEmaStopMid = config.EdgeToEmaStopMid;
            BasketSettings.EdgeToMidStopEma = config.EdgeToMidStopEma;
            BasketSettings.EdgeToBidPercentStopEma = config.EdgeToBidPercentStopEma;
            BasketSettings.EdgeToBidPercentStopEmaStopTheo = config.EdgeToBidPercentStopEmaStopTheo;
            BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo = config.EdgeToEmaBidPercentStopEmaStopTheo;
            BasketSettings.TheoBidPercent = config.TheoBidPercent;
            BasketSettings.BidPercent = config.BidPercent;
            BasketSettings.EdgeToEmaBid = config.EdgeToEmaBid;
            BasketSettings.EdgeToBid = config.EdgeToBid;
            BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage = config.EdgeToAdjTheoWithOverrideUsePercentage;
            BasketSettings.EdgeToAdjTheoWithOverrideStatic = config.EdgeToAdjTheoWithOverrideStatic;
            BasketSettings.EdgeToAdjTheoWithOverridePercent = config.EdgeToAdjTheoWithOverridePercent;
            Count = config.Count;
            PermSide = config.PermSide;
            PermType = config.PermType;
            MaintainBaseStrategyOnPerm = config.MaintainBaseStrategyOnPerm;
            ContraEnabled = config.ContraEnabled;
            AlsoOpenContraTicketEnabled = config.AlsoOpenContraTicketEnabled;
            AutoSave = config.AutoSave;

            LayoutLocked = config.LayoutLocked;
            ShowBasketSettings = config.ShowBasketSettings;
            ShowEdgeSettings = config.ShowEdgeSettings;
            ShowMarketMakerSettings = config.ShowMarketMakerSettings;
            ShowHedgeSettings = config.ShowHedgeSettings;
            ShowPermSettings = config.ShowPermSettings;
            ShowAdvancedPermSettings = config.ShowAdvancedPermSettings;
            ShowMorphSettings = config.ShowMorphSettings;
            ShowContraSettings = config.ShowContraSettings;
            RecalculatePriceOnInterval = config.RecalculatePriceOnInterval;
            AutoPermOnFill = config.AutoPermOnFill;

            ShowSubmitWithDelaySettings = config.ShowSubmitWithDelaySettings;
            ShowRouteSettings = config.ShowRouteSettings;
            ShowAdvancedRouteSettings = config.ShowAdvancedRouteSettings;
            ShowEdgeToTheoModelSettings = config.ShowEdgeToTheoModelSettings;
            ShowNagbotSettings = config.ShowNagbotSettings;
            ShowNotificationSettings = config.ShowNotificationSettings;
            ShowLoggingSettings = config.ShowLoggingSettings;
            ShowAlerts = config.ShowAlerts;
            ShowMatrixAlgos = config.ShowMatrixAlgos;
            ShowAutoLegSettings = config.ShowAutoLegSettings;
            ShowFishSettings = config.ShowFishSettings;
            ShowHedgeHouseSettings = config.ShowHedgeHouseSettings;
            ShowFishLossSettings = config.ShowFishLossSettings;
            ShowLegOutSettings = config.ShowLegOutSettings;
            ShowLegInSettings = config.ShowLegInSettings;
            ShowSweepTradeSettings = config.ShowSweepTradeSettings;
            ShowAutoCloseSettings = config.ShowAutoCloseSettings;
            ShowAutoCancelSettings = config.ShowAutoCancelSettings;
            ShowAutoPermSettings = config.ShowAutoPermSettings;
            BasketSettings.AutoPermEnabled = config.AutoPermEnabled;
            BasketSettings.AutoPermMinEdge = config.AutoPermMinEdge;
            BasketSettings.AutoPermOrderCount = Math.Min(config.AutoPermOrderCount, MaxAutoPermOrderCount);
            BasketSettings.AutoPermMaxGeneration = Math.Min(config.AutoPermMaxGeneration, MaxAutoPermMaxGeneration);
            BasketSettings.AutoPermSubmissionStyle = config.AutoPermSubmissionStyle;
            BasketSettings.AutoPermOrderInitialSize = Math.Min(config.AutoPermOrderInitialSize, MaxAutoPermOrderInitialSize);
            ShowSubscriptionManager = config.ShowSubscriptionManager;
            ShowBasketStats = config.ShowBasketStats;
            ShowStockTiedSettings = config.ShowStockTiedSettings;
            ShowCheapoSettings = config.ShowCheapoSettings;
            ShowBlockListSettings = config.ShowBlockListSettings;

            MinWidthFishLossVisible = config.MinWidthFishLossVisible;
            MaxWidthFishLossVisible = config.MaxWidthFishLossVisible;
            TheoEdgeFishLossVisible = config.TheoEdgeFishLossVisible;
            HwTheoEdgeFishLossVisible = config.HwTheoEdgeFishLossVisible;
            V0TheoEdgeFishLossVisible = config.V0TheoEdgeFishLossVisible;
            MinTheoFishLossVisible = config.MinTheoFishLossVisible;
            MinEdgeFishLossVisible = config.MinEdgeFishLossVisible;
            EmaEdgeFishLossVisible = config.EmaEdgeFishLossVisible;
            MktEdgeFishLossVisible = config.MktEdgeFishLossVisible;
            SkewMktEdgeFishLossVisible = config.SkewMktEdgeFishLossVisible;
            SkewCrossEdgeFishLossVisible = config.SkewCrossEdgeFishLossVisible;
            MinPercentBidFishLossVisible = config.MinPercentBidFishLossVisible;
            MaxPercentBidFishLossVisible = config.MaxPercentBidFishLossVisible;
            MaxDigPercentBidFishLossVisible = config.MaxDigPercentBidFishLossVisible;
            MinBidFishLossVisible = config.MinBidFishLossVisible;
            MinBidAskSizeFishLossVisible = config.MinBidAskSizeFishLossVisible;
            WidthPercentE2TFishLossVisible = config.WidthPercentE2TFishLossVisible;
            FirmAttemptFishLossVisible = config.FirmAttemptFishLossVisible;
            FirmTradeFishLossVisible = config.FirmTradeFishLossVisible;
            PermTimeFishLossVisible = config.PermTimeFishLossVisible;
            PermLoserFishLossVisible = config.PermLoserFishLossVisible;
            RecentAttemptFishLossVisible = config.RecentAttemptFishLossVisible;
            PxCrossMktFishLossVisible = config.PxCrossMktFishLossVisible;

            ResetVolumeChange = config.ResetVolumeChange;

            ResubmitOnTimer = config.ResubmitOnTimer;
            ActivateWindowOnResubmitFill = config.ActivateWindowOnResubmitFill;
            ResubmitIntervalSec = config.ResubmitIntervalSec;
            ResubmitIntervalCount = config.ResubmitIntervalCount;


            ModifyOnTimer = config.ModifyOnTimer;
            ModifyIntervalSec = config.ModifyIntervalSec;

            NagbotEnabled = config.NagbotEnabled;

            BasketSettings.NagbotMaintainEdge = config.NagbotMaintainEdge;
            BasketSettings.NagBotEdge = config.NagBotEdge;
            BasketSettings.NagbotMaxChangeInUnderlying = config.NagbotMaxChangeInUnderlying;
            BasketSettings.NagbotMaxChangeInVolume = config.NagbotMaxChangeInVolume;
            BasketSettings.NagbotMinEdgeForSize = config.NagbotMinEdgeForSize;
            BasketSettings.NagbotMinEdge = config.NagbotMinEdge;
            BasketSettings.NagbotMinEdgeForSizeEnabled = config.NagbotMinEdgeForSizeEnabled;
            BasketSettings.NagbotMinEdgeEnabled = config.NagbotMinEdgeEnabled;
            BasketSettings.WidthNotificationEnabled = config.WidthNotificationEnabled;
            BasketSettings.MinChangeToEmaNotificationEnabled = config.MinChangeToEmaNotificationEnabled;
            BasketSettings.PercentChangeInEmaNotificationEnabled = config.PercentChangeInEmaNotificationEnabled;
            BasketSettings.MaxPercentChangeInUnderlyingEmaEnabled = config.MaxPercentChangeInUnderlyingEmaEnabled;
            BasketSettings.NotificationEnabled = config.NotificationEnabled;
            BasketSettings.LoggingEnabled = config.LoggingEnabled;
            BasketSettings.MinEdgeForLogging = config.MinEdgeForLogging;
            BasketSettings.LoggingTimespan = config.LoggingTimespan;
            BasketSettings.WidthNotificationTrigger = config.WidthNotificationTrigger;
            BasketSettings.MaxPercentChangeInUnderlyingEma = config.MaxPercentChangeInUnderlyingEma;
            BasketSettings.PercentChangeInEmaNotificationTrigger = config.PercentChangeInEmaNotificationTrigger;
            BasketSettings.MinChangeToEmaNotificationEnabledTrigger = config.MinChangeToEmaNotificationEnabledTrigger;
            BasketSettings.ActivateWindowOnNotificationEnabled = config.ActivateWindowOnNotificationEnabled;
            BasketSettings.SubmitOnTriggerEnabled = config.SubmitOnTriggerEnabled;
            BasketSettings.CancelOnLoss = config.CancelOnLoss;
            BasketSettings.DisableSubmitOnTriggerOnLoss = config.DisableSubmitOnWidthTriggerOnLoss;
            BasketSettings.ShowTheoToMidIndicator = config.ShowTheoToMidIndicator;
            BasketSettings.NotifyOnTheoToMarketSpreadWideningFromEmaEnabled = config.NotifyOnTheoToMarketSpreadWideningFromEmaEnabled;
            BasketSettings.MinPercentChangeOnTheoToMarketSpreadWideningFromEma = config.MinPercentChangeOnTheoToMarketSpreadWideningFromEma;
            BasketSettings.AskPriceNotificationEnabled = config.AskPriceNotificationEnabled;
            BasketSettings.AskPriceNotificationTrigger = config.AskPriceNotificationTrigger;

            BasketSettings.SubmitOnTriggerMaxOpenEnabled = config.SubmitOnWidthTriggerMaxOpenEnabled;
            BasketSettings.SubmitOnTriggerMaxOpenPos = config.SubmitOnWidthTriggerMaxOpenPos;

            BasketSettings.StockTiedEnabled = config.StockTiedEnabled;
            BasketSettings.StockTiedDeltaNeutral = config.StockTiedDeltaNeutral;

            BasketSettings.CheapoEnabled = config.CheapoEnabled;
            BasketSettings.CheapoLegMaxWidth = config.CheapoLegMaxWidth;
            BasketSettings.CheaposGeneratedPerOrder = config.CheaposGeneratedPerOrder;
            BasketSettings.CheapoDteRangeMin = config.CheapoDteRangeMin;
            BasketSettings.CheapoDteRangeMax = config.CheapoDteRangeMax;
            BasketSettings.CheapoDeltaRangeMin = config.CheapoDeltaRangeMin;
            BasketSettings.CheapoDeltaRangeMax = config.CheapoDeltaRangeMax;
            BasketSettings.CheapoMarketRangeMin = config.CheapoMarketRangeMin;
            BasketSettings.CheapoMarketRangeMax = config.CheapoMarketRangeMax;

            BasketSettings.AlertWhenGettingNoFill = config.AlertWhenGettingNoFill;
            BasketSettings.AlertWhenGettingNoFillCount = config.AlertWhenGettingNoFillCount;

            BasketSettings.SubmitWithDelayInterval = config.SubmitWithDelayIntervalMin;
            BasketSettings.SubmitWithDelayIntervalEnd = config.SubmitWithDelayIntervalEnd >= config.SubmitWithDelayIntervalMin ? config.SubmitWithDelayIntervalEnd : config.SubmitWithDelayIntervalMin;
            if (isApiRequest)
            {
                automationConfig.CloseOrderMode = null;
                automationConfig.LoopingEnabled = false;
                automationConfig.GoFishAutoCloseEnabled = false;
            }
            else
            {
                automationConfig.LoopMinEdge = config.LoopMinEdge;
                automationConfig.LoopInterval = config.LoopInterval;
                automationConfig.LoopIntervalMax = Math.Max(config.LoopInterval, config.LoopIntervalMax);
                automationConfig.MaintainLastEdge = config.MaintainLastEdge;
                automationConfig.LoopResubmit = config.LoopResubmit;
                automationConfig.AttemptResubmit = config.AttemptResubmit;
                automationConfig.LoopMaxLoss = config.LoopMaxLoss;
                automationConfig.LoopMinEdgePercentage = config.LoopMinEdgePercentage;
                automationConfig.LoopMinEdgeUsePercentage = config.LoopMinEdgeUsePercentage;
                automationConfig.LoopFreeLook = config.LoopFreeLook;
                automationConfig.FreeLookWhenGettingCloseEdge = config.FreeLookWhenGettingCloseEdge;
                automationConfig.AutoHedgeOnClose = config.AutoHedgeOnClose;
                automationConfig.AutoHedgeOnCloseSizeOnly = config.AutoHedgeOnCloseSizeOnly;
                automationConfig.MinHedgeHouseEdge = config.MinHedgeHouseEdge;
                automationConfig.AutoHedgeOnFailure = config.AutoHedgeOnFailure;
                automationConfig.AutoHedgePartial = config.AutoHedgePartial;
                automationConfig.LoopFreeLookOnAll = config.LoopFreeLookOnAll;

                automationConfig.FreeLookRequireMinFillTime = config.FreeLookRequireMinFillTime;
                automationConfig.FreeLookMinFillTime = config.FreeLookMinFillTime;

                automationConfig.FreeLookOnLosers = config.FreeLookOnLosers;
                automationConfig.FreeLookOnLosersMax = config.FreeLookOnLosersMax;

                automationConfig.LoopFreeLookOnAllUsingTicks = config.LoopFreeLookOnAllUsingTicks;
                automationConfig.FreeLookOnAllIncrementTicks = config.FreeLookOnAllIncrementTicks;
                automationConfig.FreeLookOnAllWalkBackIncrementTicks = config.FreeLookOnAllWalkBackIncrementTicks;

                automationConfig.LoopFreeLookOnNickelNames = config.LoopFreeLookOnNickelNames;
                automationConfig.LoopFreeLookOnNickelNamesIncrement = config.LoopFreeLookOnNickelNamesIncrement;
                automationConfig.LoopFreeLookOnNickelNamesRoute = config.LoopFreeLookOnNickelNamesRoute;
                automationConfig.LoopFreeLookOnDimeNames = config.LoopFreeLookOnDimeNames;
                automationConfig.LoopFreeLookOnDimeNamesIncrement = config.LoopFreeLookOnDimeNamesIncrement;
                automationConfig.LoopFreeLookOnDimeNamesRoute = config.LoopFreeLookOnDimeNamesRoute;

                automationConfig.FreeLookOnAllIncrement = config.FreeLookOnAllIncrement;
                automationConfig.FreeLookOnAllWalkBackIncrement = config.FreeLookOnAllWalkBackIncrement;
                automationConfig.MaxLoopCount = config.MaxLoopCount;
                automationConfig.AutomationPartialResubmitCount = config.AutomationPartialResubmitCountV2;
                automationConfig.AutomationRequiredPartialFillPercentage = config.AutomationRequiredPartialFillPercentageV2;
                automationConfig.LoopSizeupType = config.LoopSizeupType;
                automationConfig.LooperDynamicRouting = config.LooperDynamicRouting;
                automationConfig.AttemptIncrementUsingDynamicRoute = config.AttemptIncrementUsingDynamicRoute;
                automationConfig.EnableDynamicRouteForOpeningOrders = config.EnableDynamicRouteForOpeningOrders;
                automationConfig.EnableDynamicRouteForClosingOrders = config.EnableDynamicRouteForClosingOrders;

                if (config.ExchToRouteMapV5 != null)
                {
                    automationConfig.ExchToRouteMap = new();
                    foreach (var pair in config.ExchToRouteMapV5)
                    {
                        if (!string.IsNullOrWhiteSpace(pair.Item1) && !string.IsNullOrWhiteSpace(pair.Item2))
                        {
                            automationConfig.ExchToRouteMap[pair.Item1.ToUpper()] = pair.Item2.ToUpper();
                        }
                    }
                }

                automationConfig.LooperOpenRoute = config.LooperOpenRoute;
                automationConfig.LooperCloseRoute = config.LooperCloseRoute;
                automationConfig.LooperOpenRouteSize = config.LooperOpenRouteSize;
                automationConfig.LooperCloseRouteSize = config.LooperCloseRouteSize;
                automationConfig.StockTiedOrderRoute = config.StockTiedOrderRoute ?? OmsCore.Config.DefaultHedgeRoute(GetInstanceMode());

                automationConfig.LooperOpenRouteSingleLeg = config.LooperOpenRouteSingleLeg;
                automationConfig.LooperCloseRouteSingleLeg = config.LooperCloseRouteSingleLeg;
                automationConfig.LooperOpenRouteSingleLegSize = config.LooperOpenRouteSingleLegSize;
                automationConfig.LooperCloseRouteSingleLegSize = config.LooperCloseRouteSingleLegSize;
                automationConfig.UseSingleLegSeparateLooperRoutes = config.UseSingleLegSeparateLooperRoutes;

                automationConfig.AdjustClosingPriceToMarket = config.AdjustClosingPriceToMarketV2;
                automationConfig.AdjustClosingPriceToMarketWinnersOnly = false;

                automationConfig.LockTraderAutoCloseEnabled = config.LockTraderAutoCloseEnabled;
                automationConfig.LockTraderResubmitOnFillEnabled = config.LockTraderResubmitOnFillEnabled;
                automationConfig.LockTraderResetQtyOnResubmit = config.LockTraderResetQtyOnResubmit;
                automationConfig.LockTraderResubmitOnFillMaxCount = config.LockTraderResubmitOnFillMaxCount;

                automationConfig.LoopSizeupQty = config.LoopSizeupQty;
                automationConfig.LoopCountBeforeSizeup = config.LoopCountBeforeSizeup;
                automationConfig.LoopPricingMode = config.LoopPricingMode;
                automationConfig.CloseOrderMode = config.CloseOrderMode;

                automationConfig.AutoAggressorEnabled = config.AutoAggressorEnabled;
                automationConfig.AutoAggressorMode = config.AutoAggressorMode;
                automationConfig.AutoAggressorEdgeTightenMode = config.AutoAggressorEdgeTightenMode;
                automationConfig.AutoAggressorEdgeTightenPercentage = config.AutoAggressorEdgeTightenPercentage;

                automationConfig.ScratchOnLowDeltaSize = config.ScratchOnLowDeltaSize;
                automationConfig.ScratchOnLowDeltaMax = config.ScratchOnLowDeltaMax;
                automationConfig.ScratchOnLowDeltaMaxLoss = config.ScratchOnLowDeltaMaxLoss;
                automationConfig.ScratchOnLowDeltaMinSize = config.ScratchOnLowDeltaMinSize;

                automationConfig.IcebergCloserEnabled = config.IcebergCloserEnabled;
                automationConfig.IcebergDisplaySize = config.IcebergDisplaySize;
                automationConfig.IcebergTotalSize = config.IcebergTotalSize;
                automationConfig.IcebergMaxResubmit = config.IcebergMaxResubmit;

                automationConfig.MaxBelowEdgeResubmit = config.MaxBelowEdgeResubmitV2;
                automationConfig.LastEdgeTightenPercent = config.LastEdgeTightenPercentV2;

                automationConfig.DynamicEdgeExpansion = config.DynamicEdgeExpansionV2;
                automationConfig.DynamicSizeExpansion = config.DynamicSizeExpansionV2;

                automationConfig.DynamicEdgeModelId = config.DynamicEdgeConfigId;
                automationConfig.DynamicIntervalModelId = config.DynamicIntervalModelId;
                automationConfig.AutoPermConfigModelId = config.AutoPermConfigModelId;
                automationConfig.LoopIncrementConfigModelId = config.LoopIncrementConfigModelId;
                automationConfig.SizeupConfigId = config.SizeupConfigId;

                automationConfig.EdgeMultiplier = config.EdgeMultiplier;
                automationConfig.MaxLossMultiplier = config.MaxLossMultiplier;

                Task.Run(() => LoadDynamicConfigs(config, automationConfig));
            }

            BasketSettings.BuyEdge = config.BuyEdge;
            BasketSettings.SellEdge = config.SellEdge;
            BasketSettings.PxCrossOption = config.CrossOption;
            BasketSettings.CancelOnClose = config.CancelOnClose;
            BasketSettings.QueueCancel = config.QueueCancel;
            BasketSettings.UseHedgeUnderlyingForAutoCancel = config.UseHedgeUnderlyingForAutoCancel;
            BasketSettings.HedgeAutoEnabled = !isApiRequest && config.HedgeAutoEnabled;
            BasketSettings.HedgeOrderType = config.HedgeOrderType;
            BasketSettings.HedgeLimitEdge = config.HedgeLimitEdge;
            BasketSettings.HedgeLimitIncrement = config.HedgeLimitIncrement;
            BasketSettings.HedgeAttempt = config.HedgeAttempt;
            BasketSettings.HedgeInterval = config.HedgeInterval;
            BasketSettings.HedgeOnFailedClose = config.HedgeOnFailedClose;
            BasketSettings.HedgeWithEdge = config.HedgeWithEdge;
            BasketSettings.HedgeMinEdge = config.HedgeMinEdge;

            BasketSettings.SelectedEmaType = config.SavedEmaType;
            BasketSettings.PercentVegaThreshold = config.EmaPercentVegaThreshold;
            BasketSettings.EmaSmoothing = config.EmaSmoothing;
            BasketSettings.EmaInterval = config.EmaPeriods;
            BasketSettings.EmaPeriods = config.EmaInterval;
            BasketSettings.MaxBidDeviation = config.MaxBidDeviation;
            BasketSettings.MaxAskDeviation = config.MaxAskDeviation;

            BasketSettings.SubscribeToMarketData = config.SubscribeToMarketData;
            BasketSettings.SubscribeToHanweck = config.SubscribeToHanweck;
            BasketSettings.SubscribeToEma = config.SubscribeToEma;
            BasketSettings.SubscribeToDerivatives = config.SubscribeToDerivatives;
            BasketSettings.SubscribeToUnderlying = config.SubscribeToUnderlying;
            BasketSettings.SubscribeToHedgeUnderlying = config.SubscribeToHedgeUnderlying;
            BasketSettings.SubscribeToGlobalEdgeToTheo = config.SubscribeToGlobalEdgeToTheo;
            BasketSettings.SubscribeToFirmSummary = config.SubscribeToFirmSummary;
            BasketSettings.RequestBestEdge = config.RequestBestEdge;
            BasketSettings.RequestBestEdgeDays = config.RequestBestEdgeDays;

            BasketSettings.SubscribeToImplied = config.SubscribeToImplied;
            BasketSettings.SubscribeToInterpolated = config.SubscribeToInterpolatedValues;
            BasketSettings.SubscribeToDerivedValues = config.SubscribeToDerivedValues;

            BasketSettings.NagbotIntervalModelConfigId = config.NagbotIntervalModelConfigId;

            BasketSettings.CheckForRecentAttempt = config.CheckForRecentAttempt;
            BasketSettings.CheckForRecentAttemptTimespan = config.CheckForRecentAttemptTimespan;
            BasketSettings.CheckForRecentFill = config.CheckForRecentFill;
            BasketSettings.CheckForRecentFillTimespan = config.CheckForRecentFillTimespan;

            BasketSettings.InitQtyEnabled = config.InitQtyEnabled;
            BasketSettings.InitQty = config.InitQty;

            BasketSettings.UseMatrixAlgo = config.UseMatrixAlgo;
            BasketSettings.MatrixStrategy = config.MatrixStrategy;
            BasketSettings.MinStrikeSortingEnabled = config.MinStrikeSortingEnabled;

            MorphSymbolsQuery = config.MorphSymbolsQuery;

            if (config.UnderlyingMappingConfigs != null)
            {
                foreach (var kvp in config.UnderlyingMappingConfigs)
                {
                    var model = AutomationConfigModels.FirstOrDefault(x => x.Title == kvp.Value);
                    if (model != null)
                    {
                        if (UnderlyingToAutomationConfigModelLookup == null)
                        {
                            UnderlyingToAutomationConfigModelLookup = new();
                        }

                        UnderlyingToAutomationConfigModelLookup[Tuple.Create(kvp.Key.Symbol?.Replace(".", "")?.Trim()?.ToUpper(), kvp.Key.Increment)] = model;
                    }
                }
            }

            OnConfigLoaded();

            return config;
        }

        protected virtual void OnConfigLoaded()
        {
            return;
        }

        public void LoadDynamicConfigs(BasketTraderConfig config, AutomationConfigModel automationConfig)
        {
            ReloadDynamicEdgeConfig(automationConfig);
            ReloadDynamicIntervalConfig(automationConfig);
            ReloadDynamicAutoPermConfig(automationConfig, config);
            ReloadDynamicLoopIncrementConfig(automationConfig, config);
            ReloadDynamicSizeUpConfig(automationConfig, config);
            ReloadConfigs();
            ReloadBasketBlockList(config);
        }

        private void ReloadBasketBlockList(BasketTraderConfig config)
        {
            if (config.BasketLoopBlockModels != null)
            {
                List<BasketLoopBlockListModel> models = config.BasketLoopBlockModels.GroupBy(x => x.Title).Select(x => x.FirstOrDefault()).Where(x => x != null).ToList();
                foreach (BasketLoopBlockListModel model in models)
                {
                    if (model.Items.Count > 0)
                    {
                        BasketLoopBlockListModel newModel = new()
                        {
                            Title = model.Title,
                        };
                        foreach (string item in model.Items)
                        {
                            newModel.BlockedSymbols.Add(new SymbolModel()
                            {
                                Symbol = item
                            });
                        }
                        newModel.UpdateList();
                        Dispatcher?.BeginInvoke(() => BasketLoopBlockListModels.Add(newModel));
                    }
                }
            }

            if (config.BasketLoopBlockList != null)
            {
                BasketLoopBlockListModel model = config.BasketLoopBlockList;
                BasketLoopBlockListModel newModel = BasketLoopBlockListModels.FirstOrDefault(x => x.Title == model.Title);
                if (newModel == null)
                {
                    newModel = new BasketLoopBlockListModel()
                    {
                        Title = model.Title,
                    };
                    foreach (string item in model.Items)
                    {
                        newModel.BlockedSymbols.Add(new SymbolModel()
                        {
                            Symbol = item
                        });
                    }
                    newModel.UpdateList();
                }
                Dispatcher?.BeginInvoke(() => BasketLoopBlockListModels.Add(newModel));
            }
        }

        private async void ReloadConfigs()
        {
            try
            {

                if (!IsDisposed && BasketSettings.NagbotIntervalModelConfigId > 0)
                {
                    Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(BasketSettings.NagbotIntervalModelConfigId);
                    if (details != null)
                    {
                        NagbotIntervalModel model = JsonConvert.DeserializeObject<NagbotIntervalModel>(details.ConfigJson);
                        if (model != null)
                        {
                            model.Id = BasketSettings.NagbotIntervalModelConfigId;
                            model.Details = details;
                            model.Configs = model.Configs.OrderBy(x => x.Interval).ToObservableCollection();
                        }
                        BasketSettings.NagbotIntervalModel = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadConfigs));
            }
        }

        private async void ReloadDynamicEdgeConfig(AutomationConfigModel automationConfig = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null && automationConfig.DynamicEdgeModelId > 0)
                {
                    Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(automationConfig.DynamicEdgeModelId);
                    if (details != null)
                    {
                        DynamicEdgeModel model = JsonConvert.DeserializeObject<DynamicEdgeModel>(details.ConfigJson);
                        model.Id = automationConfig.DynamicEdgeModelId;
                        model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(details));
                        model.Load();
                        automationConfig.DynamicEdgeModel = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicEdgeConfig));
            }
        }

        private async void ReloadDynamicIntervalConfig(AutomationConfigModel automationConfig = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null && automationConfig.DynamicIntervalModelId > 0)
                {
                    Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(automationConfig.DynamicIntervalModelId);
                    if (details != null)
                    {
                        DynamicIntervalModel model = JsonConvert.DeserializeObject<DynamicIntervalModel>(details.ConfigJson);
                        model.Id = automationConfig.DynamicIntervalModelId;
                        model.Details = details;
                        automationConfig.DynamicIntervalModel = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicIntervalConfig));
            }
        }

        private async void ReloadDynamicAutoPermConfig(AutomationConfigModel automationConfig = null, BasketTraderConfig config = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null)
                {
                    if (automationConfig.AutoPermConfigModelId > 0)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(automationConfig.AutoPermConfigModelId);
                        if (details != null)
                        {
                            BasketAutoPermModel model = JsonConvert.DeserializeObject<BasketAutoPermModel>(details.ConfigJson);
                            model.Id = automationConfig.AutoPermConfigModelId;
                            model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(details));
                            automationConfig.AutoPermConfigModel = model;
                        }
                    }
                    else if (config != null && config.AutoPermConfigs != null && config.AutoPermConfigs.Any())
                    {
                        string title = OmsCore.User.Username + " Default";
                        BasketAutoPermModel model = new()
                        {
                            AutoPermOthers = config.AutoPermOthers,
                            AutoPermOtherInstances = config.AutoPermOthersList.Select(x => (object)x).ToList(),
                            SubmitAutoPerms = config.SubmitAutoPerms,
                            WaitForPrevious = config.WaitForPrevious,
                            AutoPermConfigs = config.AutoPermConfigs,
                            Creator = OmsCore.User.Username,
                            LastUpdateTime = DateTime.Now,
                        };

                        var other = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.AutoPermConfig);
                        var sameConfig = other?.FirstOrDefault(x => x.Title == title);

                        if (sameConfig == null)
                        {
                            model.Title = title;
                            Comms.Models.Data.Oms.Config.ConfigSave configSave = new()
                            {
                                Id = model.Id,
                                Title = model.Title,
                                ConfigJson = model.GetAsJson(),
                                Module = (int)Module.AutoPermConfig,
                                OwnerId = OmsCore.User.ID,
                                Username = OmsCore.User.Username,
                                Group = OmsCore.User.Username,
                                SaveTime = DateTime.Now,
                            };

                            OmsCore.GatewayClient.SaveConfig(configSave);
                        }

                        automationConfig.AutoPermConfigModel = model;
                    }

                    if (automationConfig.AutoPermConfigModel != null && automationConfig.AutoPermConfigModel.AutoPermConfigs != null && automationConfig.AutoPermConfigModel.AutoPermConfigs.Any())
                    {
                        IEnumerable<PermOperationModel> permModels = automationConfig.AutoPermConfigModel.AutoPermConfigs.Select(x => x.AutoPermTemplate).GroupBy(x => x.Title).Select(x => x.FirstOrDefault());
                        var existing = PermOperationModels.Select(x => x.Title).ToHashSet();
                        foreach (PermOperationModel model in permModels)
                        {
                            if (!existing.Contains(model.Title))
                            {
                                PermOperationModels.Add(model);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicAutoPermConfig));
            }
        }

        private async void ReloadDynamicLoopIncrementConfig(AutomationConfigModel automationConfig = null, BasketTraderConfig config = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null)
                {
                    if (automationConfig.LoopIncrementConfigModelId > 0)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(automationConfig.LoopIncrementConfigModelId);
                        if (details != null)
                        {
                            LoopIncrementConfigModel model = JsonConvert.DeserializeObject<LoopIncrementConfigModel>(details.ConfigJson);
                            if (model != null)
                            {
                                model.Id = automationConfig.LoopIncrementConfigModelId;
                                model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(details));
                                automationConfig.LoopIncrementConfigModel = model;
                            }
                        }
                    }
                    else if (config != null && config.DynamicIncrementConfigs != null && config.DynamicIncrementConfigs.Any())
                    {
                        string title = OmsCore.User.Username + " Default";
                        LoopIncrementConfigModel model = new()
                        {
                            DynamicIncrementConfigs = config.DynamicIncrementConfigs,
                            Creator = OmsCore.User.Username,
                            LastUpdateTime = DateTime.Now,
                        };

                        var other = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.DynamicIncrementConfigs);
                        var sameConfig = other?.FirstOrDefault(x => x.Title == title);

                        if (sameConfig == null)
                        {
                            model.Title = title;
                            Comms.Models.Data.Oms.Config.ConfigSave configSave = new()
                            {
                                Id = model.Id,
                                Title = model.Title,
                                ConfigJson = model.GetAsJson(),
                                Module = (int)Module.DynamicIncrementConfigs,
                                OwnerId = OmsCore.User.ID,
                                Username = OmsCore.User.Username,
                                Group = OmsCore.User.Username,
                                SaveTime = DateTime.Now,
                            };

                            automationConfig.LoopIncrementConfigModel = model;

                            OmsCore.GatewayClient.SaveConfig(configSave);
                        }
                        else
                        {
                            Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(sameConfig.Id);
                            if (details != null)
                            {
                                var copy = JsonConvert.DeserializeObject<LoopIncrementConfigModel>(details.ConfigJson);
                                if (copy != null)
                                {
                                    copy.Id = automationConfig.LoopIncrementConfigModelId;
                                    copy.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(sameConfig));
                                    automationConfig.LoopIncrementConfigModel = copy;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicLoopIncrementConfig));
            }
        }

        private async void ReloadDynamicSizeUpConfig(AutomationConfigModel automationConfig = null, BasketTraderConfig config = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null)
                {
                    if (config.SizeupConfigId > 0)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(config.SizeupConfigId);
                        if (details != null)
                        {
                            DynamicSizeupModel model = JsonConvert.DeserializeObject<DynamicSizeupModel>(details.ConfigJson);
                            if (model != null)
                            {
                                model.Id = config.SizeupConfigId;
                                model.Details = details;
                                model.SizeUpConfigs = model.SizeUpConfigs.OrderByDescending(x => x.Edge).ThenByDescending(x => x.Size).ToObservableCollection();
                            }
                            automationConfig.SizeupConfig = model;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicSizeUpConfig));
            }
        }

        public override string GetConfigSerialized(bool withItems = false, bool onlyLayout = false)
        {
            BasketTraderConfig config = GetConfig();

            if (onlyLayout)
            {
                config.FileName = String.Empty;
                config.FilePath = String.Empty;
            }

            if (withItems)
            {
                config.BasketItems = GetOrdersJson();
            }

            string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            return configJson;
        }

        internal int GetTotalOpenPos()
        {
            int total = 0;
            try
            {
                for (int i = 0; i < BasketItems.Count; i++)
                {
                    BasketTraderItemModel item = BasketItems[i];
                    total += Math.Abs(item.TraderSpreadPosition);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetTotalOpenPos));
            }
            return total;
        }

        public BasketTraderConfig GetConfig()
        {
            AutomationConfigModel automationConfig = GetAutomationConfig();
            return new()
            {
                FilePath = FilePath,
                FileName = FileName,

                FishModeEnabled = BasketSettings.FishModeEnabled,
                FishEdge = automationConfig.FishEdge,
                FishInterval = automationConfig.FishInterval,
                FishPriceIncrement = automationConfig.FishPriceIncrement,

                AutoClean = AutoClean,

                UseResubmit = automationConfig.UseResubmit,
                LeaveAutoCloseResting = automationConfig.LeaveAutoCloseResting,

                SubmitWithDelayEnabled = BasketSettings.SubmitWithDelayEnabled,
                OpenTicketForFills = BasketSettings.OpenTicketForFills,
                OpenTicketForFailedClose = BasketSettings.OpenTicketForFailedClose,
                OpenTicketOnEdgeAcquired = BasketSettings.OpenTicketOnEdgeAcquired,
                StartProcessingFromSelectedRow = BasketSettings.StartProcessingFromSelectedRow,
                Randomize = BasketSettings.Randomize,
                Resume = BasketSettings.Resume,
                DisablePriceRounding = BasketSettings.DisablePriceRounding,

                ContraFishEdge = automationConfig.ContraFishEdge,
                LoopIncrementType = automationConfig.LoopIncrementType,
                LoopIntervalType = automationConfig.LoopIntervalType,
                LoopCloseEdgeType = automationConfig.LoopCloseEdgeType,
                ContraFishIntervalV2 = automationConfig.ContraFishInterval,
                ContraFishIntervalMaxV2 = Math.Max(automationConfig.ContraFishInterval, automationConfig.ContraFishIntervalMax),
                SizeUpOnClosingLoop = automationConfig.SizeUpOnClosingLoop,
                SizeUpOnHardSideOnly = automationConfig.SizeUpOnHardSideOnly,
                RequireAdjEdgeForSizeUp = automationConfig.RequireAdjEdgeForSizeUp,
                ContraFishPriceIncrement = automationConfig.ContraFishPriceIncrement,
                CancelWithEdgeToTheoEnabled = BasketSettings.CancelWithEdgeToTheoEnabled,
                CancelWithTheoEdge = BasketSettings.CancelWithTheoEdge,

                CancelWithOrderPriceEdgeToTheoEnabled = BasketSettings.CancelWithOrderPriceEdgeToTheoEnabled,
                CancelWithOrderPriceEdgeToTheo = BasketSettings.CancelWithOrderPriceEdgeToTheo,

                CancelWithOrderPriceEdgeToModelTheoEnabled = BasketSettings.CancelWithOrderPriceEdgeToModelTheoEnabled,
                CancelWithOrderPriceEdgeToModelTheo = BasketSettings.CancelWithOrderPriceEdgeToModelTheo,

                CancelWithEdgeToAdjTheoEnabled = BasketSettings.CancelWithEdgeToAdjTheoEnabled,
                CancelWithAdjTheoEdge = BasketSettings.CancelWithAdjTheoEdge,

                CancelWithEdgeToMidEnabled = BasketSettings.CancelWithEdgeToMidEnabled,
                CancelWithMidEdge = BasketSettings.CancelWithMidEdge,

                CancelWithWidthEnabled = BasketSettings.CancelWithWidthEnabled,
                CancelWithWidthThreshold = BasketSettings.CancelWithWidthThreshold,

                CancelWithUnderlyingPxEnabled = BasketSettings.CancelWithUnderlyingPxEnabled,
                CancelWithUnderlyingPx = BasketSettings.CancelWithUnderlyingPx,

                CancelWithUnderlyingDeltaPxEnabled = BasketSettings.CancelWithUnderlyingDeltaPxEnabled,
                CancelWithUnderlyingDeltaPx = BasketSettings.CancelWithUnderlyingDeltaPx,

                CancelWithMaxSizeEnabled = BasketSettings.CancelWithMaxSizeEnabled,
                CancelWithMaxSizeLimit = BasketSettings.CancelWithMaxSizeLimit,

                CancelWithTimerEnabled = BasketSettings.CancelWithTimerEnabled,
                CancelWithTimer = BasketSettings.CancelWithTimer,

                DerivedValuesEnabled = BasketSettings.DerivedValuesEnabled,

                DynamicUpdateEdgeOverrides = BasketSettings.DynamicUpdateEdgeOverrides,
                EvaluateAdjustedEdgeOverrides = BasketSettings.EvaluateAdjustedEdgeOverrides,
                AdjustedEdgeOverrideCushionValue = BasketSettings.AdjustedEdgeOverrideCushionValue,

                DeltaCapEnabled = BasketSettings.DeltaCapEnabled,
                DeltaCapUpperBound = BasketSettings.DeltaCapUpperBound,
                DeltaCapLowerBound = BasketSettings.DeltaCapLowerBound,
                ModifyPxWithMktChange = BasketSettings.ModifyPxWithMktChange,

                StrikeCapEnabled = BasketSettings.StrikeCapEnabled,
                StrikeCapUpperBound = BasketSettings.StrikeCapUpperBound,
                StrikeCapLowerBound = BasketSettings.StrikeCapLowerBound,

                WidthCapEnabled = BasketSettings.WidthCapEnabled,
                WidthCapUpperBound = BasketSettings.WidthCapUpperBound,
                WidthCapLowerBound = BasketSettings.WidthCapLowerBound,

                CancelOnAmountOfFillsCount = BasketSettings.CancelOnAmountOfFillsCount,
                MaxRestingOrdersEnabledV3 = BasketSettings.MaxRestingOrdersEnabled,
                MaxRestingOrdersCount = BasketSettings.MaxRestingOrdersCount,

                DisableMultipleRestingSizeOrders = BasketSettings.DisableMultipleRestingSizeOrders,

                PermSelf = PermSelf,
                AvoidInvalidItems = AvoidInvalid,
                UseEdgeToTheo = BasketSettings.UseEdgeToTheo,
                UseEdgeToHistoricBest = BasketSettings.UseEdgeToHistoricBest,
                UseEdgeToAdjTheo = BasketSettings.UseEdgeToAdjTheo,
                UseLastFillAdjPx = BasketSettings.UseLastFillAdjPx,
                UseEdgeToMid = BasketSettings.UseEdgeToMid,
                UseEdgeToEma = BasketSettings.UseEdgeToEma,
                UseEdgeToTheoAndMid = BasketSettings.UseEdgeToTheoAndMid,
                UseEdgeToTheoStopMid = BasketSettings.UseEdgeToTheoStopMid,
                UseEdgeToEmaStopMid = BasketSettings.UseEdgeToEmaStopMid,
                UseEdgeToMidStopEma = BasketSettings.UseEdgeToMidStopEma,
                UseEdgeToBidPercentStopEma = BasketSettings.UseEdgeToBidPercentStopEma,
                UseEdgeToBidPercentStopEmaStopTheo = BasketSettings.UseEdgeToBidPercentStopEmaStopTheo,
                UseEdgeToEmaBidPercentStopEmaStopTheo = BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo,
                UseEdgeToDerivedBidPercentStopEmaStopMid = BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid,
                UseTheoBidPercent = BasketSettings.UseTheoBidPercent,
                UseBidPercent = BasketSettings.UseBidPercent,
                UseEdgeToEmaBid = BasketSettings.UseEdgeToEmaBid,
                UseDomStyleEdge = BasketSettings.UseDomStyleEdge,
                DominatorConfiguration = BasketSettings.DominatorConfiguration,
                UseEdgeToBid = BasketSettings.UseEdgeToBid,
                UseCustomFunctionEdge = BasketSettings.UseCustomFunctionEdge,
                UseBestOfEdge = BasketSettings.UseBestOfEdge,
                BestOfAdjTheoEnabled = BasketSettings.BestOfAdjTheoEnabled,
                BestOfAdjTheoEdge = BasketSettings.BestOfAdjTheoEdge,
                BestOfAdjTheoModel = BasketSettings.BestOfAdjTheoModel,
                BestOfHwTheoEnabled = BasketSettings.BestOfHwTheoEnabled,
                BestOfHwTheoEdge = BasketSettings.BestOfHwTheoEdge,
                BestOfV0TheoEnabled = BasketSettings.BestOfV0TheoEnabled,
                BestOfV0TheoEdge = BasketSettings.BestOfV0TheoEdge,
                BestOfMidEnabled = BasketSettings.BestOfMidEnabled,
                BestOfMidEdge = BasketSettings.BestOfMidEdge,
                BestOfEmaEnabled = BasketSettings.BestOfEmaEnabled,
                BestOfEmaEdge = BasketSettings.BestOfEmaEdge,
                BestOfBidPercentEnabled = BasketSettings.BestOfBidPercentEnabled,
                BestOfBidPercentEdge = BasketSettings.BestOfBidPercentEdge,
                BestOfDigBidPercentEnabled = BasketSettings.BestOfDigBidPercentEnabled,
                BestOfDigBidPercentEdge = BasketSettings.BestOfDigBidPercentEdge,
                ShowBestOfEdgeExpanded = ShowBestOfEdgeExpanded,
                BestOfEdgeLocked = BestOfEdgeLocked,
                BestOfAdjTheoPinned = BasketSettings.BestOfAdjTheoPinned,
                BestOfHwTheoPinned = BasketSettings.BestOfHwTheoPinned,
                BestOfV0TheoPinned = BasketSettings.BestOfV0TheoPinned,
                BestOfMidPinned = BasketSettings.BestOfMidPinned,
                BestOfEmaPinned = BasketSettings.BestOfEmaPinned,
                BestOfBidPercentPinned = BasketSettings.BestOfBidPercentPinned,
                BestOfDigBidPercentPinned = BasketSettings.BestOfDigBidPercentPinned,
                EdgeToTheo = BasketSettings.EdgeToTheo,
                CustomFunctionEdgeFormula = BasketSettings.CustomFunctionEdgeFormula,
                EdgeToHistoricBest = BasketSettings.EdgeToHistoricBest,
                EdgeToAdjTheo = BasketSettings.EdgeToAdjTheo,
                EmaModel = BasketSettings.EmaModel,
                TheoModel = BasketSettings.TheoModel,
                FishLossTheoModel = BasketSettings.FishLossTheoModel,
                AutoCancelTheoModel = BasketSettings.AutoCancelTheoModel,
                LastFillAdjEdge = BasketSettings.LastFillAdjEdge,
                EdgeToMid = BasketSettings.EdgeToMid,
                EdgeToEma = BasketSettings.EdgeToEma,
                EdgeToTheoAndMid = BasketSettings.EdgeToTheoAndMid,
                EdgeToTheoStopMid = BasketSettings.EdgeToTheoStopMid,
                EdgeToEmaStopMid = BasketSettings.EdgeToEmaStopMid,
                EdgeToMidStopEma = BasketSettings.EdgeToMidStopEma,
                EdgeToBidPercentStopEma = BasketSettings.EdgeToBidPercentStopEma,
                EdgeToBidPercentStopEmaStopTheo = BasketSettings.EdgeToBidPercentStopEmaStopTheo,
                EdgeToEmaBidPercentStopEmaStopTheo = BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo,
                TheoBidPercent = BasketSettings.TheoBidPercent,
                BidPercent = BasketSettings.BidPercent,
                EdgeToEmaBid = BasketSettings.EdgeToEmaBid,
                EdgeToBid = BasketSettings.EdgeToBid,
                UseEdgeToAdjTheoWithOverride = BasketSettings.UseEdgeToAdjTheoWithOverride,
                EdgeToAdjTheoWithOverrideUsePercentage = BasketSettings.EdgeToAdjTheoWithOverrideUsePercentage,
                EdgeToAdjTheoWithOverrideStatic = BasketSettings.EdgeToAdjTheoWithOverrideStatic,
                EdgeToAdjTheoWithOverridePercent = BasketSettings.EdgeToAdjTheoWithOverridePercent,
                Count = Count,
                PermType = PermType,
                PermSide = PermSide,
                MaintainBaseStrategyOnPerm = MaintainBaseStrategyOnPerm,
                ContraEnabled = ContraEnabled,
                AlsoOpenContraTicketEnabled = AlsoOpenContraTicketEnabled,
                AutoSave = AutoSave,
                LayoutLocked = LayoutLocked,
                ShowBasketSettings = ShowBasketSettings,
                ShowEdgeSettings = ShowEdgeSettings,
                ShowMarketMakerSettings = ShowMarketMakerSettings,
                ShowHedgeSettings = ShowHedgeSettings,
                ShowPermSettings = ShowPermSettings,
                ShowAdvancedPermSettings = ShowAdvancedPermSettings,
                ShowMorphSettings = ShowMorphSettings,
                ShowContraSettings = ShowContraSettings,
                ShowSubmitWithDelaySettings = ShowSubmitWithDelaySettings,
                ShowRouteSettings = ShowRouteSettings,
                ShowAdvancedRouteSettings = ShowAdvancedRouteSettings,
                ShowFishSettings = ShowFishSettings,
                ShowHedgeHouseSettings = ShowHedgeHouseSettings,
                ShowNagbotSettings = ShowNagbotSettings,
                ShowNotificationSettings = ShowNotificationSettings,
                ShowLoggingSettings = ShowLoggingSettings,
                ShowAlerts = ShowAlerts,
                ShowMatrixAlgos = ShowMatrixAlgos,
                ShowAutoLegSettings = ShowAutoLegSettings,
                ShowFishLossSettings = ShowFishLossSettings,
                ShowEdgeToTheoModelSettings = ShowEdgeToTheoModelSettings,
                ShowLegOutSettings = ShowLegOutSettings,
                ShowLegInSettings = ShowLegInSettings,
                ShowSweepTradeSettings = ShowSweepTradeSettings,
                ShowAutoCloseSettings = ShowAutoCloseSettings,
                ShowAutoCancelSettings = ShowAutoCancelSettings,
                ShowAutoPermSettings = ShowAutoPermSettings,
                AutoPermEnabled = BasketSettings.AutoPermEnabled,
                AutoPermMinEdge = BasketSettings.AutoPermMinEdge,
                AutoPermOrderCount = BasketSettings.AutoPermOrderCount,
                AutoPermMaxGeneration = BasketSettings.AutoPermMaxGeneration,
                AutoPermSubmissionStyle = BasketSettings.AutoPermSubmissionStyle,
                AutoPermOrderInitialSize = BasketSettings.AutoPermOrderInitialSize,
                ShowSubscriptionManager = ShowSubscriptionManager,
                ShowBasketStats = ShowBasketStats,
                ShowStockTiedSettings = ShowStockTiedSettings,
                ShowCheapoSettings = ShowCheapoSettings,
                ShowBlockListSettings = ShowBlockListSettings,

                MinWidthFishLossVisible = MinWidthFishLossVisible,
                MaxWidthFishLossVisible = MaxWidthFishLossVisible,
                TheoEdgeFishLossVisible = TheoEdgeFishLossVisible,
                HwTheoEdgeFishLossVisible = HwTheoEdgeFishLossVisible,
                V0TheoEdgeFishLossVisible = V0TheoEdgeFishLossVisible,
                MinTheoFishLossVisible = MinTheoFishLossVisible,
                MinEdgeFishLossVisible = MinEdgeFishLossVisible,
                EmaEdgeFishLossVisible = EmaEdgeFishLossVisible,
                MktEdgeFishLossVisible = MktEdgeFishLossVisible,
                SkewMktEdgeFishLossVisible = SkewMktEdgeFishLossVisible,
                SkewCrossEdgeFishLossVisible = SkewCrossEdgeFishLossVisible,
                MinPercentBidFishLossVisible = MinPercentBidFishLossVisible,
                MaxPercentBidFishLossVisible = MaxPercentBidFishLossVisible,
                MaxDigPercentBidFishLossVisible = MaxDigPercentBidFishLossVisible,
                MinBidFishLossVisible = MinBidFishLossVisible,
                MinBidAskSizeFishLossVisible = MinBidAskSizeFishLossVisible,
                WidthPercentE2TFishLossVisible = WidthPercentE2TFishLossVisible,
                FirmAttemptFishLossVisible = FirmAttemptFishLossVisible,
                FirmTradeFishLossVisible = FirmTradeFishLossVisible,
                PermTimeFishLossVisible = PermTimeFishLossVisible,
                PermLoserFishLossVisible = PermLoserFishLossVisible,
                RecentAttemptFishLossVisible = RecentAttemptFishLossVisible,
                PxCrossMktFishLossVisible = PxCrossMktFishLossVisible,

                ResetVolumeChange = ResetVolumeChange,

                RecalculatePriceOnInterval = RecalculatePriceOnInterval,
                AutoPermOnFill = AutoPermOnFill,

                ResubmitOnTimer = ResubmitOnTimer,
                ResubmitIntervalSec = ResubmitIntervalSec,
                ResubmitIntervalCount = ResubmitIntervalCount,
                ActivateWindowOnResubmitFill = ActivateWindowOnResubmitFill,

                ModifyOnTimer = ModifyOnTimer,
                ModifyIntervalSec = ModifyIntervalSec,

                SubmitWithDelayIntervalMin = BasketSettings.SubmitWithDelayInterval,
                SubmitWithDelayIntervalEnd = BasketSettings.SubmitWithDelayIntervalEnd,
                BuyEdge = BasketSettings.BuyEdge,
                SellEdge = BasketSettings.SellEdge,

                MaxWidthCheckEnabled = BasketSettings.MaxWidthCheckEnabled,
                MaxWidthCheckPx = BasketSettings.MaxWidthCheckPx,

                MinTheoEdgeCheckEnabled = BasketSettings.MinTheoEdgeCheckEnabled,
                MinTheoEdgeCheckEdge = BasketSettings.MinTheoEdgeCheckEdge,

                MinHwTheoEdgeCheckEnabled = BasketSettings.MinHwTheoEdgeCheckEnabled,
                MinHwTheoEdgeCheckEdge = BasketSettings.MinHwTheoEdgeCheckEdge,

                MinV0TheoEdgeCheckEnabled = BasketSettings.MinV0TheoEdgeCheckEnabled,
                MinV0TheoEdgeCheckEdge = BasketSettings.MinV0TheoEdgeCheckEdge,

                MinBidCheckEnabled = BasketSettings.MinBidCheckEnabled,
                MinBidCheckBidValue = BasketSettings.MinBidCheckBidValue,

                MinTheoCheckEnabled = BasketSettings.MinTheoCheckEnabled,
                MinTheoCheckTheoValue = BasketSettings.MinTheoCheckTheoValue,

                MinBidAskSizeCheckEnabled = BasketSettings.MinBidAskSizeCheckEnabled,
                MinBidAskSize = BasketSettings.MinBidAskSize,

                MinEmaWidthPercentEdgeToTheoCheckEnabled = BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEnabled,
                MinEmaWidthPercentEdgeToTheoCheckEdge = BasketSettings.MinEmaWidthPercentEdgeToTheoCheckEdge,

                MinEdgeToMarketCheckEnabled = BasketSettings.MinEdgeToMarketCheckEnabled,
                MinEdgeToMarketCheckEdge = BasketSettings.MinEdgeToMarketCheckEdge,

                IgnoreSkewMktCheckIfBothSidesFail = BasketSettings.IgnoreSkewMktCheckIfBothSidesFail,

                MinEdgeToSkewMarketCheckEnabled = BasketSettings.MinEdgeToSkewMarketCheckEnabled,
                MinEdgeToSkewMarketCheckEdge = BasketSettings.MinEdgeToSkewMarketCheckEdge,
                AdjustAfterMinEdgeToSkewMarketCheck = BasketSettings.AdjustAfterMinEdgeToSkewMarketCheck,

                MinEdgeToSkewMarketCrossCheckEnabled = BasketSettings.MinEdgeToSkewMarketCrossCheckEnabled,
                MinEdgeToSkewMarketCrossCheckEdge = BasketSettings.MinEdgeToSkewMarketCrossCheckEdge,
                AdjustAfterMinEdgeToSkewMarketCrossCheck = BasketSettings.AdjustAfterMinEdgeToSkewMarketCrossCheck,

                BlockZeroPrice = BasketSettings.BlockZeroPrice,
                BlockSubmissionOnTheoJump = BasketSettings.BlockSubmissionOnTheoJump,

                MaxPercentBidCheckUseBestQuote = BasketSettings.MaxPercentBidCheckUseBestQuote,
                MinPercentBidCheckEnabled = BasketSettings.MinPercentBidCheckEnabled,
                MinPercentBidCheckEdge = BasketSettings.MinPercentBidCheckEdge,
                MaxPercentBidCheckEnabled = BasketSettings.MaxPercentBidCheckEnabled,
                MaxPercentBidCheckEdge = BasketSettings.MaxPercentBidCheckEdge,
                MaxDigPercentBidCheckEnabled = BasketSettings.MaxDigPercentBidCheckEnabled,
                MaxDigPercentBidCheckEdge = BasketSettings.MaxDigPercentBidCheckEdge,

                PreviousAttemptCrossCheckEnabled = BasketSettings.PreviousAttemptCrossCheckEnabled,
                MinEdgeToPreviousAttemptCheckEnabled = BasketSettings.MinEdgeToPreviousAttemptCheckEnabled,
                MinTimeToPreviousAttemptCheckEnabled = BasketSettings.MinTimeToPreviousAttemptCheckEnabled,
                MinTimeToPreviousAttemptIntervalSeconds = BasketSettings.MinTimeToPreviousAttemptIntervalSeconds,
                MinTimeToPermLoserCheckEnabled = BasketSettings.MinTimeToPermLoserCheckEnabled,
                MinTimeToPermLoserIntervalSeconds = BasketSettings.MinTimeToPermLoserIntervalSeconds,

                MinMidEdgeCheckEnabled = BasketSettings.MinMidEdgeCheckEnabled,
                MinMidEdgeCheckEdge = BasketSettings.MinMidEdgeCheckEdge,

                MinEmaEdgeCheckEnabled = BasketSettings.MinEmaEdgeCheckEnabled,
                MinEmaEdgeCheckEdge = BasketSettings.MinEmaEdgeCheckEdge,

                LoopMinEdge = automationConfig.LoopMinEdge,
                LoopInterval = automationConfig.LoopInterval,
                LoopIntervalMax = Math.Max(automationConfig.LoopInterval, automationConfig.LoopIntervalMax),
                MaintainLastEdge = automationConfig.MaintainLastEdge,
                LoopResubmit = automationConfig.LoopResubmit,
                AttemptResubmit = automationConfig.AttemptResubmit,

                AutoHedgeOnClose = automationConfig.AutoHedgeOnClose,
                AutoHedgeOnCloseSizeOnly = automationConfig.AutoHedgeOnCloseSizeOnly,
                MinHedgeHouseEdge = automationConfig.MinHedgeHouseEdge,
                AutoHedgeOnFailure = automationConfig.AutoHedgeOnFailure,
                AutoHedgePartial = automationConfig.AutoHedgePartial,

                NagbotEnabled = NagbotEnabled,
                NagBotEdge = BasketSettings.NagBotEdge,
                NagbotMaintainEdge = BasketSettings.NagbotMaintainEdge,
                NagbotMaxChangeInUnderlying = BasketSettings.NagbotMaxChangeInUnderlying,
                NagbotMaxChangeInVolume = BasketSettings.NagbotMaxChangeInVolume,
                NagbotMinEdgeForSize = BasketSettings.NagbotMinEdgeForSize,
                NagbotMinEdge = BasketSettings.NagbotMinEdge,
                NagbotMinEdgeForSizeEnabled = BasketSettings.NagbotMinEdgeForSizeEnabled,
                NagbotMinEdgeEnabled = BasketSettings.NagbotMinEdgeEnabled,
                NagbotIntervalModelConfigId = BasketSettings.NagbotIntervalModelConfigId,
                WidthNotificationEnabled = BasketSettings.WidthNotificationEnabled,
                MinChangeToEmaNotificationEnabled = BasketSettings.MinChangeToEmaNotificationEnabled,
                PercentChangeInEmaNotificationEnabled = BasketSettings.PercentChangeInEmaNotificationEnabled,
                MaxPercentChangeInUnderlyingEmaEnabled = BasketSettings.MaxPercentChangeInUnderlyingEmaEnabled,
                NotificationEnabled = BasketSettings.NotificationEnabled,
                LoggingEnabled = BasketSettings.LoggingEnabled,
                MinEdgeForLogging = BasketSettings.MinEdgeForLogging,
                LoggingTimespan = BasketSettings.LoggingTimespan,
                WidthNotificationTrigger = BasketSettings.WidthNotificationTrigger,
                MaxPercentChangeInUnderlyingEma = BasketSettings.MaxPercentChangeInUnderlyingEma,
                PercentChangeInEmaNotificationTrigger = BasketSettings.PercentChangeInEmaNotificationTrigger,
                MinChangeToEmaNotificationEnabledTrigger = BasketSettings.MinChangeToEmaNotificationEnabledTrigger,
                ActivateWindowOnNotificationEnabled = BasketSettings.ActivateWindowOnNotificationEnabled,
                SubmitOnTriggerEnabled = BasketSettings.SubmitOnTriggerEnabled,
                CancelOnLoss = BasketSettings.CancelOnLoss,
                DisableSubmitOnWidthTriggerOnLoss = BasketSettings.DisableSubmitOnTriggerOnLoss,
                ShowTheoToMidIndicator = BasketSettings.ShowTheoToMidIndicator,
                SubmitOnWidthTriggerMaxOpenEnabled = BasketSettings.SubmitOnTriggerMaxOpenEnabled,
                SubmitOnWidthTriggerMaxOpenPos = BasketSettings.SubmitOnTriggerMaxOpenPos,
                NotifyOnTheoToMarketSpreadWideningFromEmaEnabled = BasketSettings.NotifyOnTheoToMarketSpreadWideningFromEmaEnabled,
                MinPercentChangeOnTheoToMarketSpreadWideningFromEma = BasketSettings.MinPercentChangeOnTheoToMarketSpreadWideningFromEma,
                AskPriceNotificationEnabled = BasketSettings.AskPriceNotificationEnabled,
                AskPriceNotificationTrigger = BasketSettings.AskPriceNotificationTrigger,

                StockTiedEnabled = BasketSettings.StockTiedEnabled,
                StockTiedDeltaNeutral = BasketSettings.StockTiedDeltaNeutral,
                CheapoEnabled = BasketSettings.CheapoEnabled,
                CheapoLegMaxWidth = BasketSettings.CheapoLegMaxWidth,
                CheaposGeneratedPerOrder = BasketSettings.CheaposGeneratedPerOrder,
                CheapoDteRangeMin = BasketSettings.CheapoDteRangeMin,
                CheapoDteRangeMax = BasketSettings.CheapoDteRangeMax,
                CheapoDeltaRangeMin = BasketSettings.CheapoDeltaRangeMin,
                CheapoDeltaRangeMax = BasketSettings.CheapoDeltaRangeMax,
                CheapoMarketRangeMin = BasketSettings.CheapoMarketRangeMin,
                CheapoMarketRangeMax = BasketSettings.CheapoMarketRangeMax,

                AlertWhenGettingNoFill = BasketSettings.AlertWhenGettingNoFill,
                AlertWhenGettingNoFillCount = BasketSettings.AlertWhenGettingNoFillCount,

                EdgeMultiplier = automationConfig.EdgeMultiplier,
                MaxLossMultiplier = automationConfig.MaxLossMultiplier,

                LoopMaxLoss = automationConfig.LoopMaxLoss,
                LoopMinEdgePercentage = automationConfig.LoopMinEdgePercentage,
                LoopMinEdgeUsePercentage = automationConfig.LoopMinEdgeUsePercentage,
                LoopFreeLook = automationConfig.LoopFreeLook,

                FreeLookRequireMinFillTime = automationConfig.FreeLookRequireMinFillTime,
                FreeLookMinFillTime = automationConfig.FreeLookMinFillTime,

                FreeLookOnLosers = automationConfig.FreeLookOnLosers,
                FreeLookOnLosersMax = automationConfig.FreeLookOnLosersMax,

                FreeLookWhenGettingCloseEdge = automationConfig.FreeLookWhenGettingCloseEdge,
                LoopFreeLookOnAll = automationConfig.LoopFreeLookOnAll,
                FreeLookOnAllIncrement = automationConfig.FreeLookOnAllIncrement,
                FreeLookOnAllWalkBackIncrement = automationConfig.FreeLookOnAllWalkBackIncrement,

                LoopFreeLookOnAllUsingTicks = automationConfig.LoopFreeLookOnAllUsingTicks,
                FreeLookOnAllIncrementTicks = automationConfig.FreeLookOnAllIncrementTicks,
                FreeLookOnAllWalkBackIncrementTicks = automationConfig.FreeLookOnAllWalkBackIncrementTicks,

                LoopFreeLookOnNickelNames = automationConfig.LoopFreeLookOnNickelNames,
                LoopFreeLookOnNickelNamesIncrement = automationConfig.LoopFreeLookOnNickelNamesIncrement,
                LoopFreeLookOnNickelNamesRoute = automationConfig.LoopFreeLookOnNickelNamesRoute,
                LoopFreeLookOnDimeNames = automationConfig.LoopFreeLookOnDimeNames,
                LoopFreeLookOnDimeNamesIncrement = automationConfig.LoopFreeLookOnDimeNamesIncrement,
                LoopFreeLookOnDimeNamesRoute = automationConfig.LoopFreeLookOnDimeNamesRoute,

                MaxLoopCount = automationConfig.MaxLoopCount,
                AutomationPartialResubmitCountV2 = automationConfig.AutomationPartialResubmitCount,
                AutomationRequiredPartialFillPercentageV2 = automationConfig.AutomationRequiredPartialFillPercentage,
                LoopSizeupType = automationConfig.LoopSizeupType,
                LooperDynamicRouting = automationConfig.LooperDynamicRouting,
                AttemptIncrementUsingDynamicRoute = automationConfig.AttemptIncrementUsingDynamicRoute,
                EnableDynamicRouteForOpeningOrders = automationConfig.EnableDynamicRouteForOpeningOrders,
                EnableDynamicRouteForClosingOrders = automationConfig.EnableDynamicRouteForClosingOrders,
                ExchToRouteMapV5 = automationConfig.ExchToRouteMap?.Select(x => Tuple.Create(x.Key?.ToUpper(), x.Value?.ToUpper())).ToList(),
                LooperOpenRoute = automationConfig.LooperOpenRoute,
                LooperCloseRoute = automationConfig.LooperCloseRoute,
                LooperOpenRouteSize = automationConfig.LooperOpenRouteSize,
                LooperCloseRouteSize = automationConfig.LooperCloseRouteSize,
                StockTiedOrderRoute = automationConfig.StockTiedOrderRoute,

                AdjustClosingPriceToMarketV2 = automationConfig.AdjustClosingPriceToMarket,
                AdjustClosingPriceToMarketWinnersOnly = automationConfig.AdjustClosingPriceToMarketWinnersOnly,

                LooperOpenRouteSingleLeg = automationConfig.LooperOpenRouteSingleLeg,
                LooperCloseRouteSingleLeg = automationConfig.LooperCloseRouteSingleLeg,
                LooperOpenRouteSingleLegSize = automationConfig.LooperOpenRouteSingleLegSize,
                LooperCloseRouteSingleLegSize = automationConfig.LooperCloseRouteSingleLegSize,
                UseSingleLegSeparateLooperRoutes = automationConfig.UseSingleLegSeparateLooperRoutes,

                AutoAggressorEnabled = automationConfig.AutoAggressorEnabled,
                AutoAggressorMode = automationConfig.AutoAggressorMode,
                AutoAggressorEdgeTightenMode = automationConfig.AutoAggressorEdgeTightenMode,
                AutoAggressorEdgeTightenPercentage = automationConfig.AutoAggressorEdgeTightenPercentage,

                ScratchOnLowDeltaSize = automationConfig.ScratchOnLowDeltaSize,
                ScratchOnLowDeltaMax = automationConfig.ScratchOnLowDeltaMax,
                ScratchOnLowDeltaMaxLoss = automationConfig.ScratchOnLowDeltaMaxLoss,
                ScratchOnLowDeltaMinSize = automationConfig.ScratchOnLowDeltaMinSize,

                IcebergCloserEnabled = automationConfig.IcebergCloserEnabled,
                IcebergDisplaySize = automationConfig.IcebergDisplaySize,
                IcebergTotalSize = automationConfig.IcebergTotalSize,
                IcebergMaxResubmit = automationConfig.IcebergMaxResubmit,

                LoopSizeupQty = automationConfig.LoopSizeupQty,
                LoopCountBeforeSizeup = automationConfig.LoopCountBeforeSizeup,
                LoopPricingMode = automationConfig.LoopPricingMode,
                CloseOrderMode = automationConfig.CloseOrderMode,

                LockTraderAutoCloseEnabled = automationConfig.LockTraderAutoCloseEnabled,
                LockTraderResubmitOnFillEnabled = automationConfig.LockTraderResubmitOnFillEnabled,
                LockTraderResetQtyOnResubmit = automationConfig.LockTraderResetQtyOnResubmit,
                LockTraderResubmitOnFillMaxCount = automationConfig.LockTraderResubmitOnFillMaxCount,

                DynamicEdgeExpansionV2 = automationConfig.DynamicEdgeExpansion,
                DynamicSizeExpansionV2 = automationConfig.DynamicSizeExpansion,
                LastEdgeTightenPercentV2 = automationConfig.LastEdgeTightenPercent,
                MaxBelowEdgeResubmitV2 = automationConfig.MaxBelowEdgeResubmit,
                DynamicEdgeConfigId = automationConfig.DynamicEdgeModelId,
                DynamicIntervalModelId = automationConfig.DynamicIntervalModelId,
                AutoPermConfigModelId = automationConfig.AutoPermConfigModelId,
                LoopIncrementConfigModelId = automationConfig.LoopIncrementConfigModelId,
                SizeupConfigId = automationConfig.SizeupConfigId,
                AutoPermSelectionMode = automationConfig.AutoPermConfigModel != null ? automationConfig.AutoPermConfigModel.AutoPermSelectionMode : default,
                AutoPermConfigs = automationConfig.AutoPermConfigModel != null ? automationConfig.AutoPermConfigModel?.AutoPermConfigs : default,
                AutoPermOthers = automationConfig.AutoPermConfigModel != null && automationConfig.AutoPermConfigModel.AutoPermOthers,
                AutoPermOthersList = automationConfig.AutoPermConfigModel != null ? automationConfig.AutoPermConfigModel.AutoPermOtherInstances.Select(x => (string)x).ToList() : default,
                SubmitAutoPerms = automationConfig.AutoPermConfigModel != null && automationConfig.AutoPermConfigModel.SubmitAutoPerms,
                WaitForPrevious = automationConfig.AutoPermConfigModel != null && automationConfig.AutoPermConfigModel.WaitForPrevious,
                DynamicIncrementConfigs = automationConfig.LoopIncrementConfigModel?.DynamicIncrementConfigs,

                CrossOption = BasketSettings.PxCrossOption,
                CancelOnClose = BasketSettings.CancelOnClose,
                QueueCancel = BasketSettings.QueueCancel,
                UseHedgeUnderlyingForAutoCancel = BasketSettings.UseHedgeUnderlyingForAutoCancel,

                MorphSymbolsQuery = MorphSymbolsQuery,

                HedgeAutoEnabled = BasketSettings.HedgeAutoEnabled,
                HedgeOrderType = BasketSettings.HedgeOrderType,
                HedgeLimitEdge = BasketSettings.HedgeLimitEdge,
                HedgeLimitIncrement = BasketSettings.HedgeLimitIncrement,
                HedgeAttempt = BasketSettings.HedgeAttempt,
                HedgeInterval = BasketSettings.HedgeInterval,
                HedgeOnFailedClose = BasketSettings.HedgeOnFailedClose,
                HedgeWithEdge = BasketSettings.HedgeWithEdge,
                HedgeMinEdge = BasketSettings.HedgeMinEdge,

                SavedEmaType = BasketSettings.SelectedEmaType,
                EmaPercentVegaThreshold = BasketSettings.PercentVegaThreshold,
                EmaSmoothing = BasketSettings.EmaSmoothing,
                EmaPeriods = BasketSettings.EmaInterval,
                EmaInterval = BasketSettings.EmaPeriods,
                MaxBidDeviation = BasketSettings.MaxBidDeviation,
                MaxAskDeviation = BasketSettings.MaxAskDeviation,

                SubscribeToMarketData = BasketSettings.SubscribeToMarketData,
                SubscribeToHanweck = BasketSettings.SubscribeToHanweck,
                SubscribeToEma = BasketSettings.SubscribeToEma,
                SubscribeToDerivatives = BasketSettings.SubscribeToDerivatives,
                SubscribeToUnderlying = BasketSettings.SubscribeToUnderlying,
                SubscribeToHedgeUnderlying = BasketSettings.SubscribeToHedgeUnderlying,
                SubscribeToGlobalEdgeToTheo = BasketSettings.SubscribeToGlobalEdgeToTheo,
                SubscribeToFirmSummary = BasketSettings.SubscribeToFirmSummary,
                RequestBestEdge = BasketSettings.RequestBestEdge,
                RequestBestEdgeDays = BasketSettings.RequestBestEdgeDays,

                SubscribeToImplied = BasketSettings.SubscribeToImplied,
                SubscribeToInterpolatedValues = BasketSettings.SubscribeToInterpolated,
                SubscribeToDerivedValues = BasketSettings.SubscribeToDerivedValues,

                BasketLoopBlockList = BasketSettings.BasketLoopBlockList,
                BasketLoopBlockModels = BasketLoopBlockListModels.GroupBy(x => x.Title).Select(g => g.FirstOrDefault()).Where(x => x != null).ToList(),

                CheckForRecentAttempt = BasketSettings.CheckForRecentAttempt,
                CheckForRecentAttemptTimespan = BasketSettings.CheckForRecentAttemptTimespan,
                CheckForRecentFill = BasketSettings.CheckForRecentFill,
                CheckForRecentFillTimespan = BasketSettings.CheckForRecentFillTimespan,

                InitQtyEnabled = BasketSettings.InitQtyEnabled,
                InitQty = BasketSettings.InitQty,

                UseMatrixAlgo = BasketSettings.UseMatrixAlgo,
                MatrixStrategy = BasketSettings.MatrixStrategy,
                MinStrikeSortingEnabled = BasketSettings.MinStrikeSortingEnabled,

                UnderlyingMappingConfigs = UnderlyingToAutomationConfigModelLookup?.Select(x => new KeyValuePair<UnderlyingLookupKey, string>(new UnderlyingLookupKey(x.Key.Item1?.Replace(".", "").Trim().ToUpper(), x.Key.Item2), x.Value.Title)).ToList(),
            };
        }

        private void ValidatePath()
        {
            string path = Path.GetDirectoryName(FilePath);

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception)
                {
                    FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FileName + ".json");
                }
            }
        }

        public string GetEdgeType()
        {
            if (IsDisposed)
            {
                return default;
            }
            if (BasketSettings.UseEdgeToTheo)
            {
                return "Theo";
            }
            else if (BasketSettings.UseEdgeToHistoricBest)
            {
                return "Historic Best";
            }
            else if (BasketSettings.UseEdgeToAdjTheo)
            {
                return "Adj Theo";
            }
            else if (BasketSettings.UseLastFillAdjPx)
            {
                return "Last Fill Adj";
            }
            else if (BasketSettings.UseEdgeToMid)
            {
                return "Mid";
            }
            else if (BasketSettings.UseEdgeToEma)
            {
                return "Ema";
            }
            else if (BasketSettings.UseEdgeToTheoAndMid)
            {
                return "Theo & Mid";
            }
            else if (BasketSettings.UseEdgeToTheoStopMid)
            {
                return "Theo stop Mid";
            }
            else if (BasketSettings.UseEdgeToEmaStopMid)
            {
                return "Ema stop Mid";
            }
            else if (BasketSettings.UseEdgeToMidStopEma)
            {
                return "Mid stop Ema";
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEma)
            {
                return "% Bid stop Ema";
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
            {
                return "% B 🛑 E 🛑 T";
            }
            else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                return "% EB 🛑 E 🛑 T";
            }
            else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                return "% DB 🛑 E 🛑 M";
            }
            else if (BasketSettings.UseTheoBidPercent)
            {
                return "Theo Bid %";
            }
            else if (BasketSettings.UseBidPercent)
            {
                return "Bid %";
            }
            else if (BasketSettings.UseEdgeToEmaBid)
            {
                return "Bid stop Ema";
            }
            else if (BasketSettings.UseEdgeToBid)
            {
                return "Bid";
            }
            else if (BasketSettings.UseCustomFunctionEdge)
            {
                return "Func";
            }
            else if (BasketSettings.UseDomStyleEdge)
            {
                return "DomStyle";
            }
            else
            {
                return "None";
            }
        }

        public EdgeType GetEdgeTypeEnum()
        {
            if (IsDisposed)
            {
                return EdgeType.None;
            }
            if (!BasketSettings.AdjustPriceBeforeSubmit)
            {
                return EdgeType.None;
            }
            if (BasketSettings.UseEdgeToTheo)
            {
                return EdgeType.EdgeToTheo;
            }
            else if (BasketSettings.UseEdgeToAdjTheo)
            {
                return EdgeType.EdgeToAdjustedTheo;
            }
            else if (BasketSettings.UseLastFillAdjPx)
            {
                return EdgeType.LastFillAdjEdge;
            }
            else if (BasketSettings.UseEdgeToMid)
            {
                return EdgeType.EdgeToMid;
            }
            else if (BasketSettings.UseEdgeToEma)
            {
                return EdgeType.EdgeToEma;
            }
            else if (BasketSettings.UseEdgeToTheoAndMid)
            {
                return EdgeType.EdgeToTheoAndMid;
            }
            else if (BasketSettings.UseEdgeToTheoStopMid)
            {
                return EdgeType.EdgeToTheoStopMid;
            }
            else if (BasketSettings.UseEdgeToEmaStopMid)
            {
                return EdgeType.EdgeToEmaStopMid;
            }
            else if (BasketSettings.UseEdgeToMidStopEma)
            {
                return EdgeType.EdgeToMidStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEma)
            {
                return EdgeType.EdgeToBidPercentStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
            {
                return EdgeType.EdgeToBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                return EdgeType.EdgeToEmaBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                return EdgeType.EdgeToDerivedBidPercentStopEmaStopMid;
            }
            else if (BasketSettings.UseTheoBidPercent)
            {
                return EdgeType.TheoBidPercent;
            }
            else if (BasketSettings.UseBidPercent)
            {
                return EdgeType.BidPercent;
            }
            else if (BasketSettings.UseEdgeToEmaBid)
            {
                return EdgeType.EdgeToEmaBid;
            }
            else if (BasketSettings.UseEdgeToBid)
            {
                return EdgeType.EdgeToBid;
            }
            else if (BasketSettings.UsePermAdjPx)
            {
                return EdgeType.PermAdjEdge;
            }
            else if (BasketSettings.UseDomStyleEdge)
            {
                return EdgeType.DomStyleEdge;
            }
            else if (BasketSettings.UseEdgeToAdjTheoWithOverride)
            {
                return EdgeType.EdgeToAdjTheoWithOverride;
            }
            else if (BasketSettings.UseBestOfEdge)
            {
                return EdgeType.UseBestOfEdge;
            }
            else
            {
                return EdgeType.None;
            }
        }

        public double GetEdge()
        {
            if (IsDisposed)
            {
                return double.NaN;
            }
            if (BasketSettings.UseEdgeToTheo)
            {
                return BasketSettings.EdgeToTheo;
            }
            else if (BasketSettings.UseEdgeToHistoricBest)
            {
                return BasketSettings.EdgeToHistoricBest;
            }
            else if (BasketSettings.UseEdgeToAdjTheo)
            {
                return BasketSettings.EdgeToAdjTheo;
            }
            else if (BasketSettings.UseLastFillAdjPx)
            {
                return BasketSettings.LastFillAdjEdge;
            }
            else if (BasketSettings.UseEdgeToMid)
            {
                return BasketSettings.EdgeToMid;
            }
            else if (BasketSettings.UseEdgeToEma)
            {
                return BasketSettings.EdgeToEma;
            }
            else if (BasketSettings.UseEdgeToTheoAndMid)
            {
                return BasketSettings.EdgeToTheoAndMid;
            }
            else if (BasketSettings.UseEdgeToTheoStopMid)
            {
                return BasketSettings.EdgeToTheoStopMid;
            }
            else if (BasketSettings.UseEdgeToEmaStopMid)
            {
                return BasketSettings.EdgeToEmaStopMid;
            }
            else if (BasketSettings.UseEdgeToMidStopEma)
            {
                return BasketSettings.EdgeToMidStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEma)
            {
                return BasketSettings.EdgeToBidPercentStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
            {
                return BasketSettings.EdgeToBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                return BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                return BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid;
            }
            else if (BasketSettings.UseTheoBidPercent)
            {
                return BasketSettings.TheoBidPercent;
            }
            else if (BasketSettings.UseBidPercent)
            {
                return BasketSettings.BidPercent;
            }
            else if (BasketSettings.UseEdgeToEmaBid)
            {
                return BasketSettings.EdgeToEmaBid;
            }
            else if (BasketSettings.UseEdgeToBid)
            {
                return BasketSettings.EdgeToBid;
            }
            else if (BasketSettings.UsePermAdjPx)
            {
                return BasketSettings.PermAdjEdge;
            }
            else if (BasketSettings.UseEdgeToAdjTheoWithOverride)
            {
                return double.NaN;
            }
            else
            {
                return double.NaN;
            }
        }

        internal void SetTitle()
        {
            if (BasketItems != null)
            {
                RowCount = BasketItems.Count;
                IOrderedEnumerable<DateTime> expirations = GetExpirations();
                ExpirationDescription = new BasketDescriptionModel()
                {
                    ExpirationSample = expirations.FirstOrDefault(),
                    ExpirationDescription = expirations.Count() > 0 ? string.Join(", ", expirations.Select(x => x.ToString("MMM dd yy"))) : "",
                };
                IOrderedEnumerable<string> underlyings = GetUnderlyings();
                Underlyings = string.Join(", ", underlyings);
                BasketSettings.EdgeType = GetEdgeType();

                if (OmsCore.Config.UseSmartModuleTitleForBaskets)
                {
                    var strategy = GetStrategies();
                    if (expirations.Count() == 1)
                    {
                        ModuleTitle = Underlyings + " " + strategy + " [" + expirations.FirstOrDefault().ToString("MMM-dd-yy") + "]";
                    }
                    else if (expirations.Count() > 1)
                    {
                        var min = expirations.FirstOrDefault();
                        var max = expirations.LastOrDefault();
                        ModuleTitle = Underlyings + " " + strategy + " [" + min.ToString("MMM-dd-yy") + " - " + max.ToString("MMM-dd-yy") + "]";
                    }
                }
            }
        }

        public IOrderedEnumerable<DateTime> GetExpirations()
        {
            try
            {
                return BasketItems.SelectMany(x => x.Expirations).Distinct().OrderBy(x => x);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetExpirations));
                return default;
            }
        }

        public string GetStrategies()
        {
            try
            {
                var strategies = BasketItems.Select(x => x.BaseStrategy).Distinct();
                var strategy = string.Join(", ", strategies);
                return strategy;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetExpirations));
                return "";
            }
        }

        public IOrderedEnumerable<string> GetUnderlyings()
        {
            try
            {
                return BasketItems.Select(x => x.Underlying).Distinct().OrderBy(x => x);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetExpirations));
                return default;
            }
        }

        private void SendTradeUpdateToBasketManagerAsync(IOmsOrder trade)
        {
            try
            {
                if (IsDisposed || !OmsCore.Config.EnableBasketManagerClientV2 || string.IsNullOrEmpty(Uid) || BasketType == BasketType.LockTrader)
                {
                    return;
                }

                Task.Run(() => OmsCore.BasketManagerClient.TradeUpdate(this, trade));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendTradeUpdateToBasketManagerAsync));
            }
        }

        [Command]
        public void BuyEdgeChangedCommand()
        {

        }

        [Command]
        public void SellEdgeChangedCommand()
        {

        }

        internal void SubmitFromTrigger(OrderTicket orderTicketViewModelBase, string type)
        {
            if (!BasketSettings.SubmitOnTriggerMaxOpenEnabled ||
                BasketSettings.SubmitOnTriggerMaxOpenPos > GetRestingOrdersCount())
            {
                _ = orderTicketViewModelBase.SubmitOrder(resting: false, skipAdjPxBeforeSubmit: false, totalResubmitCount: 0, markForRemoval: true, doNotTradeThroughFillPrice: false, OrderSubType.Basket);
            }
        }

        private void ResubmitBuy_Tick(object sender, EventArgs e)
        {
            if (ResubmitSide == ResubmitSide.Off || ResubmitSide == ResubmitSide.Sell || IsDisposed || _timerCts.Token.IsCancellationRequested)
            {
                _resubmitCountdownTimer.Stop();
                _resubmitCountdownTimer.Tick -= ResubmitBuy_Tick;
                _elapsedResubmitTime = TimeSpan.Zero;
                ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
                return;
            }

            _elapsedResubmitTime += _resubmitCountdownTimer.Interval;

            if (_elapsedResubmitTime >= TimeSpan.FromSeconds(ResubmitIntervalSec))
            {
                _resubmitCountdownTimer.Stop();
                _resubmitCountdownTimer.Tick -= ResubmitBuy_Tick;

                if (!SubmitAllRunning)
                {
                    CancelQueuedSubmitWithDelay();
                    _timerCts.Cancel();
                    _timerCts = new CancellationTokenSource();
                    CancellationToken token = _timerCts.Token;
                    Task.Run(() => SubmitSide(Side.Buy, token));
                    switch (ResubmitSide)
                    {
                        case ResubmitSide.Off:
                        case ResubmitSide.Sell:
                            return;
                        case ResubmitSide.Buy:
                            _ = SubmitBuysWithBuyEdgeCommand();
                            return;
                        case ResubmitSide.Alternate:
                            _ = SubmitSellssWithSellEdgeCommand();
                            return;
                    }
                    _elapsedResubmitTime = TimeSpan.Zero;
                }
            }
            else
            {
                ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec) - _elapsedResubmitTime;
            }
        }

        [Command]
        public async Task SubmitBuysWithBuyEdgeCommand()
        {
            if (SubmitAllRunning)
            {
                MessageBoxService?.ShowMessage("Submit all is in progress.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }
            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                MessageBoxService?.ShowMessage("Basket Delta Limit Reached.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }
            _lastActivityTime = DateTime.Now;
            CancelQueuedSubmitWithDelay();
            bool proceed = !OmsCore.Config.GetVerificationForBasketSubmitAllV2;
            if (!proceed)
            {
                await Dispatcher?.BeginInvoke(new Action(() =>
                {
                    proceed = MessageBoxService?.Show("Are you sure you want to submit all buys?",
                                                 "Confirm",
                                                 MessageButton.YesNo,
                                                 MessageIcon.Exclamation,
                                                 MessageResult.Yes) == MessageResult.Yes;
                }));
            }
            if (proceed)
            {
                if (ResubmitSide is ResubmitSide.Buy or ResubmitSide.Alternate)
                {
                    _resubmitCountdownTimer.Stop();
                    Dispatcher?.Invoke(new Action(() => _resubmitCountdownTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1),
                    }));
                    _resubmitCountdownTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
                    ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
                    if (_timerCts.IsCancellationRequested)
                    {
                        _timerCts = new CancellationTokenSource();
                    }
                    _elapsedResubmitTime = TimeSpan.Zero;
                    _resubmitCountdownTimer.Tick += ResubmitBuy_Tick;

                    if (ResubmitSide is ResubmitSide.Buy or ResubmitSide.Alternate)
                    {
                        _resubmitCountdownTimer.Start();
                    }
                }
                else
                {
                    CancelQueuedSubmitWithDelay();
                    _timerCts.Cancel();
                    _timerCts = new CancellationTokenSource();
                    CancellationToken token = _timerCts.Token;
                    await SubmitSide(Side.Buy, token);
                }
            }
        }

        private void ResubmitSell_Tick(object sender, EventArgs e)
        {
            if (ResubmitSide == ResubmitSide.Off || ResubmitSide == ResubmitSide.Buy || IsDisposed || _timerCts.Token.IsCancellationRequested)
            {
                _resubmitCountdownTimer.Stop();
                _resubmitCountdownTimer.Tick -= ResubmitSell_Tick;
                _elapsedResubmitTime = TimeSpan.Zero;
                ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
                return;
            }

            _elapsedResubmitTime += _resubmitCountdownTimer.Interval;

            if (_elapsedResubmitTime >= TimeSpan.FromSeconds(ResubmitIntervalSec))
            {
                _resubmitCountdownTimer.Stop();
                _resubmitCountdownTimer.Tick -= ResubmitSell_Tick;

                if (!SubmitAllRunning)
                {
                    CancelQueuedSubmitWithDelay();
                    _timerCts.Cancel();
                    _timerCts = new CancellationTokenSource();
                    CancellationToken token = _timerCts.Token;
                    Task.Run(() => SubmitSide(Side.Sell, token));
                    switch (ResubmitSide)
                    {
                        case ResubmitSide.Off:
                        case ResubmitSide.Buy:
                            return;
                        case ResubmitSide.Sell:
                            _ = SubmitSellssWithSellEdgeCommand();
                            return;
                        case ResubmitSide.Alternate:
                            _ = SubmitBuysWithBuyEdgeCommand();
                            return;
                    }
                    _elapsedResubmitTime = TimeSpan.Zero;
                }
            }
            else
            {
                ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec) - _elapsedResubmitTime;
            }
        }

        [Command]
        public async Task SubmitSellssWithSellEdgeCommand()
        {
            if (SubmitAllRunning)
            {
                MessageBoxService?.ShowMessage("Submit all is in progress.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }
            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                MessageBoxService?.ShowMessage("Basket Delta Limit Reached.",
                                               "ZeroPlus OMS",
                                               MessageButton.OK,
                                               MessageIcon.Warning);
                return;
            }
            _lastActivityTime = DateTime.Now;
            CancelQueuedSubmitWithDelay();
            bool proceed = !OmsCore.Config.GetVerificationForBasketSubmitAllV2;
            if (!proceed)
            {
                await Dispatcher?.BeginInvoke(new Action(() =>
                {
                    proceed = MessageBoxService?.Show(string.Format("Are you sure you want to submit all sells?"),
                                                 "Confirm",
                                                 MessageButton.YesNo,
                                                 MessageIcon.Exclamation,
                                                 MessageResult.Yes) == MessageResult.Yes;
                }));
            }
            if (proceed)
            {

                if (ResubmitSide is ResubmitSide.Sell or ResubmitSide.Alternate)
                {
                    _resubmitCountdownTimer.Stop();
                    Dispatcher?.Invoke(new Action(() => _resubmitCountdownTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1),
                    }));
                    _resubmitCountdownTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
                    _elapsedResubmitTime = TimeSpan.Zero;
                    ResubmitCountDown = TimeSpan.FromSeconds(ResubmitIntervalSec);
                    if (_timerCts.IsCancellationRequested)
                    {
                        _timerCts = new CancellationTokenSource();
                    }
                    _resubmitCountdownTimer.Tick += ResubmitSell_Tick;

                    if (ResubmitSide is ResubmitSide.Sell or ResubmitSide.Alternate)
                    {
                        _resubmitCountdownTimer.Start();
                    }
                }
                else
                {
                    CancelQueuedSubmitWithDelay();
                    _timerCts.Cancel();
                    _timerCts = new CancellationTokenSource();
                    CancellationToken token = _timerCts.Token;
                    await SubmitSide(Side.Sell, token);
                }
            }
        }

        private async Task SubmitSide(Side side, CancellationToken token)
        {
            if (!IsDisposed && !SubmitAllRunning)
            {
                _lastActivityTime = DateTime.Now;
                SubmitAllRunning = true;
                try
                {

                    if (!IsDisposed && BasketSettings.SubmitWithDelayInterval > 0)
                    {
                        List<Tuple<int, object>> basketItems = null;
                        Dispatcher?.Invoke(new Action(() => basketItems = GetBasketItemsByVisualOrder()));

                        foreach (Tuple<int, object> indexOrderPair in basketItems)
                        {
                            await WaitForRestingOrders(token);
                            int index = indexOrderPair.Item1;
                            BasketTraderItemModel order = (BasketTraderItemModel)indexOrderPair.Item2;
                            if (token.IsCancellationRequested || IsDisposed)
                            {
                                break;
                            }
                            if (order.Active)
                            {
                                if (order.IsActive)
                                {
                                    continue;
                                }
                                Status = "Active Row: " + index;

                                var success = await order.SubmitCustomEdgeAsync(side);

                                if (success)
                                {
                                    var delay = GetSubmitDelay();
                                    if (delay > 0)
                                    {
                                        await Task.Delay(delay);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(SubmitSide));
                }
                SubmitAllRunning = false;
                Status = "";
            }
        }

        private int GetSubmitDelay()
        {
            var minInterval = BasketSettings.SubmitWithDelayInterval;
            var maxInterval = BasketSettings.SubmitWithDelayIntervalEnd;
            int delay = minInterval >= maxInterval ? minInterval : _randomGenerator.Next(minInterval, maxInterval);
            return delay;
        }

        [Command]
        public void EdgeTypeChangedCommand()
        {
            string type = BasketSettings.EdgeType;
            if (type == "Theo")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToTheo = true;
                return;
            }
            else if (type == "Historic Best")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToHistoricBest = true;
                return;
            }
            else if (type == "Adj Theo")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToAdjTheo = true;
                return;
            }
            else if (type == "Last Fill Adj")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseLastFillAdjPx = true;
                return;
            }
            else if (type == "Mid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToMid = true;
                return;
            }
            else if (type == "Ema")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToEma = true;
                return;
            }
            else if (type == "Theo & Mid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToTheoAndMid = true;
                return;
            }
            else if (type == "Theo stop Mid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToTheoStopMid = true;
                return;
            }
            else if (type == "Ema stop Mid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToEmaStopMid = true;
                return;
            }
            else if (type == "Mid stop Ema")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToMidStopEma = true;
                return;
            }
            else if (type == "% Bid stop Ema")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToBidPercentStopEma = true;
                return;
            }
            else if (type == "% B 🛑 E 🛑 T")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = true;
                return;
            }
            else if (type == "% EB 🛑 E 🛑 T")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = true;
                return;
            }
            else if (type == "% DB 🛑 E 🛑 M")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = true;
                return;
            }
            else if (type == "Theo Bid %")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseTheoBidPercent = true;
                return;
            }
            else if (type == "Bid %")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseBidPercent = true;
                return;
            }
            else if (type == "Ema stop Bid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToEmaBid = true;
                return;
            }
            else if (type == "Bid")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseEdgeToBid = true;
                return;
            }
            else if (type == "Func")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseCustomFunctionEdge = true;
                return;
            }
            else if (type == "DomStyle")
            {
                ResetEdgeAllEdgeTypes();
                BasketSettings.UseDomStyleEdge = true;
                return;
            }
        }

        public void SetEdge(string edgeType, double edge)
        {
            ResetEdgeAllEdgeTypes();

            if (edgeType == "Theo")
            {
                BasketSettings.EdgeToTheo = edge;
                BasketSettings.UseEdgeToTheo = true;
                return;
            }
            else if (edgeType == "Historic Best")
            {
                BasketSettings.EdgeToHistoricBest = edge;
                BasketSettings.UseEdgeToHistoricBest = true;
                return;
            }
            else if (edgeType == "Adj Theo")
            {
                BasketSettings.EdgeToAdjTheo = edge;
                BasketSettings.UseEdgeToAdjTheo = true;
                return;
            }
            else if (edgeType == "Last Fill Adj")
            {
                BasketSettings.LastFillAdjEdge = edge;
                BasketSettings.UseLastFillAdjPx = true;
                return;
            }
            else if (edgeType == "Mid")
            {
                BasketSettings.EdgeToMid = edge;
                BasketSettings.UseEdgeToMid = true;
                return;
            }
            else if (edgeType == "Ema")
            {
                BasketSettings.EdgeToEma = edge;
                BasketSettings.UseEdgeToEma = true;
                return;
            }
            else if (edgeType == "Theo & Mid")
            {
                BasketSettings.EdgeToTheoAndMid = edge;
                BasketSettings.UseEdgeToTheoAndMid = true;
                return;
            }
            else if (edgeType == "Theo stop Mid")
            {
                BasketSettings.EdgeToTheoStopMid = edge;
                BasketSettings.UseEdgeToTheoStopMid = true;
                return;
            }
            else if (edgeType == "Ema stop Mid")
            {
                BasketSettings.EdgeToEmaStopMid = edge;
                BasketSettings.UseEdgeToEmaStopMid = true;
                return;
            }
            else if (edgeType == "Mid stop Ema")
            {
                BasketSettings.EdgeToMidStopEma = edge;
                BasketSettings.UseEdgeToMidStopEma = true;
                return;
            }
            else if (edgeType == "% Bid stop Ema")
            {
                BasketSettings.EdgeToBidPercentStopEma = edge;
                BasketSettings.UseEdgeToBidPercentStopEma = true;
                return;
            }
            else if (edgeType == "% B 🛑 E 🛑 T")
            {
                BasketSettings.EdgeToBidPercentStopEmaStopTheo = edge;
                BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = true;
                return;
            }
            else if (edgeType == "% EB 🛑 E 🛑 T")
            {
                BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo = edge;
                BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = true;
                return;
            }
            else if (edgeType == "% DB 🛑 E 🛑 M")
            {
                BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid = edge;
                BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = true;
                return;
            }
            else if (edgeType == "Theo Bid %")
            {
                BasketSettings.TheoBidPercent = edge;
                BasketSettings.UseTheoBidPercent = true;
                return;
            }
            else if (edgeType == "Bid %")
            {
                BasketSettings.BidPercent = edge;
                BasketSettings.UseBidPercent = true;
                return;
            }
            else if (edgeType == "Ema stop Bid")
            {
                BasketSettings.EdgeToEmaBid = edge;
                BasketSettings.UseEdgeToEmaBid = true;
                return;
            }
            else if (edgeType == "Bid")
            {
                BasketSettings.EdgeToBid = edge;
                BasketSettings.UseEdgeToBid = true;
                return;
            }
            else if (edgeType == "Func")
            {
                BasketSettings.UseCustomFunctionEdge = true;
                return;
            }
            else if (edgeType == "DomStyle")
            {
                BasketSettings.UseDomStyleEdge = true;
                return;
            }
        }

        private void ResetEdgeAllEdgeTypes()
        {
            BasketSettings.UseEdgeToTheo = false;
            BasketSettings.UseEdgeToHistoricBest = false;
            BasketSettings.UseEdgeToAdjTheo = false;
            BasketSettings.UseLastFillAdjPx = false;
            BasketSettings.UseEdgeToMid = false;
            BasketSettings.UseEdgeToEma = false;
            BasketSettings.UseEdgeToTheoAndMid = false;
            BasketSettings.UseEdgeToTheoStopMid = false;
            BasketSettings.UseEdgeToEmaStopMid = false;
            BasketSettings.UseEdgeToMidStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEma = false;
            BasketSettings.UseEdgeToBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo = false;
            BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid = false;
            BasketSettings.UseTheoBidPercent = false;
            BasketSettings.UseBidPercent = false;
            BasketSettings.UseEdgeToEmaBid = false;
            BasketSettings.UseEdgeToBid = false;
            BasketSettings.UseCustomFunctionEdge = false;
            BasketSettings.UseDomStyleEdge = false;
        }

        private void SubscribePnl()
        {
            try
            {
                if (!IsDisposed && !string.IsNullOrEmpty(InstanceId) && BasketType != BasketType.LockTrader)
                {
                    PortfolioManagerModel.Subscribe(InstanceId, SubscriptionFieldType.FirmInstancePosition, this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribePnl));
            }
        }

        private void UnsubscribePnlAsync()
        {
            try
            {
                _ = PortfolioManagerModel?.UnsubscribeAllAsync(this);
                AdjustedPnl = 0;
                RealizedPnl = 0;
                if (BasketSettings != null)
                {
                    BasketSettings.NetPos = 0;
                    BasketSettings.NetDelta = 0;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribePnlAsync));
            }
        }

        private void BasketItemPnlUpdatedEvent(bool resetVolume)
        {
            try
            {
                List<BasketTraderItemModel> basketItems = BasketItems.ToList();
                UnrealizedPnl = basketItems.Where(x => !double.IsNaN(x.UnrealizedPnl)).Sum(x => x.UnrealizedPnl);
                UnrealizedPnl += basketItems.Where(x => !double.IsNaN(x.StockHedgeUnrealizedPnl)).Sum(x => x.StockHedgeUnrealizedPnl);
                NetDelta = basketItems.Where(x => !double.IsNaN(x.PositionNetDelta)).Sum(x => x.PositionNetDelta);
                TotalPositionsInitialized = basketItems.Any(x => x.SpreadPositionInitialized);
                TotalPositions = basketItems.Where(x => x.SpreadPositionInitialized).Sum(x => Math.Abs(x.SpreadPosition));
                HedgeNetDelta = basketItems.Where(x => !double.IsNaN(x.HedgeNetDelta)).Sum(x => x.HedgeNetDelta);
                TotalNetDelta = NetDelta + HedgeNetDelta;

                if (resetVolume)
                {
                    foreach (BasketTraderItemModel basketItem in basketItems)
                    {
                        basketItem.ResetVolumeCounter();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BasketItemPnlUpdatedEvent));
            }
        }

        private void OnConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            if (InstanceModeLocked)
            {
                InstanceMode = OmsCore.Config.InstanceModeV3;
            }
            SetMixedBasketBorder(AreSingles);
            if (config.BasketBorderKeyGesture is string gesture)
            {
                SetBorderKeyBinding(gesture);
            }
            ShowBasketDeltaAdjLastFillPx |= config.BasketDeltaAdjLastFillPx;
            BasketSettings.RiskCheckEnabled = config.GlobalBasketRiskControlEnabledV2;
            if (config.CustomPermCombinations != null)
            {
                PermOperationModels = config.CustomPermCombinations.Where(x => x.Key != null && x.Value != null && x.Value.Count > 0).Select(x => new PermOperationModel(x.Key, x.Value)).ToObservableCollection();
            }
            string configDirectory = OmsConfig.GetConfigDirectory();
            LoadBasketSavedConfigs(configDirectory);
        }

        public async void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                switch (key.Type)
                {
                    case SubscriptionFieldType.FirmInstancePosition:
                        HandlePositionUpdate(value as IPosition);
                        break;
                    case SubscriptionFieldType.OrderUpdate:
                        if (value is OmsOrderModel model)
                        {
                            double totalSeconds = (DateTime.Now - model.LastUpdateTime).TotalSeconds;
                            _log.Info($"Instance trade update. Spread: {model.SpreadId}, Qty: {model.Quantity}, Time: {totalSeconds}, Edge: {model.LastEdge}, Adj Edge: {model.DeltaAdjLastEdge}");
                            if (totalSeconds < 10 && Math.Abs(model.Position) <= 1)
                            {
                                if (GetAutomationConfig().AutoPermConfigModel.AutoPermOthers)
                                {
                                    BasketTraderItemModel order = MakeBasketItemModel();
                                    await order.LoadFromOrderBookAsync(model);
                                    double lastEdgeBeforeFees = Math.Min(model.LastEdge, model.DeltaAdjLastEdge);
                                    OnEdgeAcquired(order, lastEdgeBeforeFees, lastEdgeBeforeFees);
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        internal Dictionary<string, double> GetWeightedVegaMap()
        {
            Dictionary<string, double> dict = new();
            foreach (BasketTraderItemModel basketItem in BasketItems.ToList())
            {
                if (!double.IsNaN(basketItem.PositionNetWeightedVega))
                {
                    dict[basketItem.SpreadId] = basketItem.PositionNetWeightedVega;
                }
            }
            return dict;
        }

        public void HandlePositionUpdate(IPosition instancePosition)
        {
            try
            {
                AdjustedPnl = instancePosition.AdjustedPnl;
                RealizedPnl = instancePosition.RealizedPnl;
                BasketSettings.NetPos = instancePosition.NetQty;
                BasketSettings.NetDelta = instancePosition.NetDelta;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePositionUpdate));
            }
        }

        [Command]
        public void Activate()
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                    {
                        CurrentWindowService?.Show();
                        CurrentWindowService?.Activate();
                    });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Activate));
            }
        }

        [Command]
        public void Hide()
        {
            try
            {
                Dispatcher.BeginInvoke(() => CurrentWindowService?.Hide());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Hide));
            }
        }

        [Command]
        public void Close()
        {
            try
            {
                Dispatcher.BeginInvoke(() => CurrentWindowService?.Close());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Close));
            }
        }

        [Command]
        public void LoadEdgeToTheoModelCommand()
        {
            try
            {
                string scriptPath = Path.Combine(@"\\192.168.60.12", "zeroplusshared", "EdgeToTheoModels", "dom_interface.py");
                if (File.Exists(scriptPath))
                {
                    LoadedEdgeToTheoModelName = "dom_interface.py";
                }
                else
                {
                    LoadedEdgeToTheoModelName = "";
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadEdgeToTheoModelCommand));
            }
        }

        [Command]
        public void RefreshEdgeToTheoModelCommand()
        {
            try
            {
                string errors = "";

                using (Python.Runtime.Py.GIL())
                {
                    using Python.Runtime.PyModule scope = Python.Runtime.Py.CreateScope();
                    string scriptPath = Path.Combine(@"\\192.168.60.12", "zeroplusshared", "EdgeToTheoModels", "dom_interface.py");
                    string code = File.ReadAllText(scriptPath);
                    dynamic compiled = Python.Runtime.PythonEngine.Compile(code);
                    scope.Execute(compiled);
                    dynamic func = scope.Get("edge_to_theo");

                    foreach (BasketTraderItemModel item in BasketItems)
                    {
                        if (item.Legs.Count == 2)
                        {
                            TicketLegModel leg1 = item.Legs[0];
                            TicketLegModel leg2 = item.Legs[1];
                            if (leg1 != null && leg2 != null)
                            {
                                double leg1Delta = leg1.Delta;
                                double leg2Delta = leg2.Delta;
                                int side = item.Side == Side.Buy ? 1 : -1;
                                int callPut = leg1.Type == "CALL" ? 1 : 0;
                                int width = (int)Math.Abs(leg1.Strike.Strike - leg2.Strike.Strike);
                                int dte1 = (int)(leg1.ExpirationInfo.Expiration - DateTime.Now).TotalDays;
                                int dte2 = (int)(leg2.ExpirationInfo.Expiration - DateTime.Now).TotalDays;
                                string underlying = leg1.Underlying;
                                dynamic edgeToTheo = func(leg1Delta, leg2Delta, side, callPut, width, dte1, dte2, underlying);
                                if (double.TryParse(edgeToTheo.ToString(), out double result))
                                {
                                    if (!double.IsNaN(result))
                                    {
                                        item.EdgeOverride = result;
                                    }
                                    else
                                    {
                                        errors += item.SpreadId + " " + edgeToTheo.ToString() + "\n";
                                        item.EdgeOverride = double.NaN;
                                    }
                                }
                                else
                                {
                                    errors += edgeToTheo.ToString();
                                    item.EdgeOverride = double.NaN;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(errors))
                {
                    MessageBoxService.ShowMessage(errors, "Error Loading Edge To Theo", MessageButton.OK, MessageIcon.Warning);
                }
            }
            catch (Python.Runtime.PythonException ex)
            {
                MessageBoxService.ShowMessage(ex.Message, "Error Loading Edge To Theo", MessageButton.OK, MessageIcon.Warning);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RefreshEdgeToTheoModelCommand));
            }
        }

        public bool GetOpenTicketState()
        {
            return BasketSettings.OpenTicketForFills;
        }

        public void EnableResubmitTimer(int interval)
        {
            ResubmitIntervalSec = interval;
            ResubmitOnTimer = true;
        }

        public void DisableResubmitTimer(int interval)
        {
            ResubmitIntervalSec = interval;
            ResubmitOnTimer = false;
        }

        public void EnableOpenTicket()
        {
            BasketSettings.OpenTicketForFills = !IsEdgeScanFeedAutoTrader;
            BasketSettings.OpenTicketForFailedClose = true;
        }

        public void DisableOpenTicket()
        {
            BasketSettings.OpenTicketForFills = false;
            BasketSettings.OpenTicketForFailedClose = false;
        }

        public void EnableTicketProxy()
        {
        }

        public void DisableTicketProxy()
        {
        }

        internal async Task<int> GetRestingOrdersCountSafe()
        {
            try
            {
                int count = 0;
                await Dispatcher.BeginInvoke(() => count = BasketItems.Count(order => order.MainResting || order.ContraResting || order.IsActive));
                return count;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetRestingOrdersCountSafe));
                return int.MaxValue;
            }
        }

        internal int GetBasketRestingOrdersCount(int minSize = 0)
        {
            try
            {
                int count = 0;
                count = 0;
                var basketItemsCount = BasketItems.Count;
                for (var index = basketItemsCount - 1; index >= 0; index--)
                {
                    var order = BasketItems[index];
                    if (order.Lcd > minSize && (order.MainResting || order.IsActive))
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetBasketRestingOrdersCount));
                return int.MaxValue;
            }
        }

        internal int GetBasketRestingOrdersCount(string spreadId)
        {
            try
            {
                int count = 0;
                count = 0;
                var basketItemsCount = BasketItems.Count;
                for (var index = basketItemsCount - 1; index >= 0; index--)
                {
                    var order = BasketItems[index];
                    if (order.SpreadId == spreadId && (order.MainResting || order.IsActive))
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetBasketRestingOrdersCount));
                return int.MaxValue;
            }
        }

        internal void VolTraderAutoCancelUpdate(VolTraderViewModel volTraderViewModel)
        {
            try
            {
                BasketSettings.CancelWithEdgeToTheoEnabled = volTraderViewModel.CancelWithEdgeToTheoEnabled;
                BasketSettings.CancelWithTheoEdge = volTraderViewModel.CancelWithTheoEdge;
                BasketSettings.CancelWithEdgeToAdjTheoEnabled = volTraderViewModel.CancelWithEdgeToAdjTheoEnabled;
                BasketSettings.CancelWithAdjTheoEdge = volTraderViewModel.CancelWithAdjTheoEdge;
                BasketSettings.CancelWithUnderlyingPxEnabled = volTraderViewModel.CancelWithUnderlyingPxEnabled;
                BasketSettings.CancelWithUnderlyingPx = volTraderViewModel.CancelWithUnderlyingPx;
                BasketSettings.CancelWithUnderlyingDeltaPxEnabled = volTraderViewModel.CancelWithUnderlyingDeltaPxEnabled;
                BasketSettings.CancelWithUnderlyingDeltaPx = volTraderViewModel.CancelWithUnderlyingDeltaPx;
                BasketSettings.CancelWithEdgeToMidEnabled = volTraderViewModel.CancelWithEdgeToMidEnabled;
                BasketSettings.CancelWithMidEdge = volTraderViewModel.CancelWithMidEdge;
                BasketSettings.CancelWithWidthEnabled = volTraderViewModel.CancelWithWidthEnabled;
                BasketSettings.CancelWithWidthThreshold = volTraderViewModel.CancelWithWidthThreshold;
                BasketSettings.CancelWithTimerEnabled = volTraderViewModel.CancelWithTimerEnabled;
                BasketSettings.CancelWithTimer = volTraderViewModel.CancelWithTimer;
                BasketSettings.ResubmitAfterCancel = volTraderViewModel.ResubmitAfterCancel;
                BasketSettings.UseHedgeUnderlyingForAutoCancel = volTraderViewModel.UseHedgeUnderlyingForAutoCancel;
                BasketSettings.CancelOnClose = volTraderViewModel.CancelOnClose;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderAutoCancelUpdate));
            }
        }

        internal void VolTraderSubmitWithDelayUpdated(VolTraderViewModel volTraderViewModel)
        {
            try
            {
                BasketSettings.SubmitWithDelayEnabled = volTraderViewModel.SubmitWithDelayEnabled;
                BasketSettings.SubmitWithDelayInterval = volTraderViewModel.SubmitWithDelayInterval;
                BasketSettings.SubmitWithDelayIntervalEnd = volTraderViewModel.SubmitWithDelayIntervalEnd;
                BasketSettings.OpenTicketForFills = volTraderViewModel.OpenTicketForFills;
                BasketSettings.OpenTicketForFailedClose = volTraderViewModel.OpenTicketForFailedClose;
                BasketSettings.CancelOnAmountOfFillsCount = volTraderViewModel.CancelOnAmountOfFillsCount;
                BasketSettings.Randomize = volTraderViewModel.Randomize;
                BasketSettings.Resume = volTraderViewModel.Resume;
                BasketSettings.DisablePriceRounding = volTraderViewModel.DisablePriceRounding;
                BasketSettings.MaxRestingOrdersEnabled = volTraderViewModel.MaxRestingOrdersEnabled;
                BasketSettings.MaxRestingOrdersCount = volTraderViewModel.MaxRestingOrdersCount;
                BasketSettings.StartProcessingFromSelectedRow = volTraderViewModel.StartProcessingFromSelectedRow;
                ContraEnabled = volTraderViewModel.ContraEnabled;
                AlsoOpenContraTicketEnabled = volTraderViewModel.AlsoOpenContraTicketEnabled;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderSubmitWithDelayUpdated));
            }
        }

        internal void VolTraderRouteUpdated(VolTraderViewModel volTraderViewModel)
        {
            try
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                automationConfig.LooperOpenRoute = volTraderViewModel.LooperOpenRoute;
                automationConfig.LooperCloseRoute = volTraderViewModel.LooperCloseRoute;
                automationConfig.LooperOpenRouteSingleLeg = volTraderViewModel.LooperOpenRouteSingleLeg;
                automationConfig.LooperCloseRouteSingleLeg = volTraderViewModel.LooperCloseRouteSingleLeg;
                automationConfig.UseSingleLegSeparateLooperRoutes = volTraderViewModel.UseSingleLegSeparateLooperRoutes;
                automationConfig.StockTiedOrderRoute = volTraderViewModel.StockTiedOrderRoute;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderRouteUpdated));
            }
        }

        internal void VolTraderAutomationUpdated(VolTraderViewModel volTraderViewModel)
        {
            try
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                if (volTraderViewModel.UseGlobalCloseEdge)
                {
                    automationConfig.ContraFishEdge = volTraderViewModel.ContraFishEdge;
                    automationConfig.CloseEdgeMinValue = volTraderViewModel.CloseEdgeMinValue;
                }
                automationConfig.CloseOrderMode = volTraderViewModel.CloseOrderMode;
                automationConfig.LoopMaxLoss = volTraderViewModel.LoopMaxLoss;
                automationConfig.LoopMinEdgePercentage = volTraderViewModel.LoopMinEdgePercentage;
                automationConfig.LoopMinEdgeUsePercentage = volTraderViewModel.LoopMinEdgeUsePercentage;
                automationConfig.LoopMinEdge = volTraderViewModel.LoopMinEdge;
                automationConfig.MaxLoopCount = volTraderViewModel.MaxLoopCount;
                automationConfig.AutomationRequiredPartialFillPercentage = volTraderViewModel.AutomationRequiredPartialFillPercentage;
                automationConfig.LoopPricingMode = volTraderViewModel.LoopPricingMode;
                automationConfig.ContraFishInterval = volTraderViewModel.ContraFishInterval;
                automationConfig.ContraFishIntervalMax = Math.Max(volTraderViewModel.ContraFishInterval, volTraderViewModel.ContraFishIntervalMax);
                automationConfig.ClosingMode = volTraderViewModel.ClosingMode;
                automationConfig.LoopInterval = volTraderViewModel.LoopInterval;
                automationConfig.LoopIntervalMax = Math.Max(volTraderViewModel.LoopInterval, volTraderViewModel.LoopIntervalMax);
                automationConfig.AttemptResubmit = volTraderViewModel.AttemptResubmit;
                automationConfig.LoopSizeupType = volTraderViewModel.LoopSizeupType;
                automationConfig.LoopSizeupQty = volTraderViewModel.LoopSizeupQty;
                automationConfig.AutomationPartialResubmitCount = volTraderViewModel.AutomationPartialResubmitCount;
                automationConfig.ContraFishPriceIncrement = volTraderViewModel.ContraFishPriceIncrement;
                automationConfig.LeaveAutoCloseResting = volTraderViewModel.LeaveAutoCloseResting;
                automationConfig.LoopResubmit = volTraderViewModel.LoopResubmit;
                automationConfig.LoopCountBeforeSizeup = volTraderViewModel.LoopCountBeforeSizeup;
                automationConfig.LooperDynamicRouting = volTraderViewModel.LooperDynamicRouting;
                automationConfig.AttemptIncrementUsingDynamicRoute = volTraderViewModel.AttemptIncrementUsingDynamicRoute;
                automationConfig.EnableDynamicRouteForOpeningOrders = volTraderViewModel.EnableDynamicRouteForOpeningOrders;
                automationConfig.EnableDynamicRouteForClosingOrders = volTraderViewModel.EnableDynamicRouteForClosingOrders;
                automationConfig.LoopFreeLook = volTraderViewModel.LoopFreeLook;
                automationConfig.FreeLookWhenGettingCloseEdge = volTraderViewModel.FreeLookWhenGettingCloseEdge;
                automationConfig.LoopFreeLookOnAll = volTraderViewModel.LoopFreeLookOnAll;
                automationConfig.LoopFreeLookOnAllUsingTicks = volTraderViewModel.LoopFreeLookOnAllUsingTicks;
                automationConfig.FreeLookOnAllIncrementTicks = volTraderViewModel.FreeLookOnAllIncrementTicks;
                automationConfig.FreeLookOnAllWalkBackIncrementTicks = volTraderViewModel.FreeLookOnAllWalkBackIncrementTicks;
                automationConfig.FreeLookOnAllIncrement = volTraderViewModel.FreeLookOnAllIncrement;
                automationConfig.FreeLookOnAllWalkBackIncrement = volTraderViewModel.FreeLookOnAllWalkBackIncrement;
                automationConfig.LoopFreeLookOnNickelNames = volTraderViewModel.LoopFreeLookOnNickelNames;
                automationConfig.LoopFreeLookOnNickelNamesIncrement = volTraderViewModel.LoopFreeLookOnNickelNamesIncrement;
                automationConfig.LoopFreeLookOnNickelNamesRoute = volTraderViewModel.LoopFreeLookOnNickelNamesRoute;
                automationConfig.LoopFreeLookOnDimeNames = volTraderViewModel.LoopFreeLookOnDimeNames;
                automationConfig.LoopFreeLookOnDimeNamesIncrement = volTraderViewModel.LoopFreeLookOnDimeNamesIncrement;
                automationConfig.LoopFreeLookOnDimeNamesRoute = volTraderViewModel.LoopFreeLookOnDimeNamesRoute;


                ApplyAutoTraderChanges();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderAutomationUpdated));
            }
        }

        public void LoadDynamicConfig(Module configModule, IDynamicConfigModel currentModel)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    AutomationConfigModel automationConfigModel = GetAutomationConfig();
                    automationConfigModel.DynamicEdgeModelId = currentModel != null ? currentModel.Id : 0;
                    automationConfigModel.DynamicEdgeModel = (IDynamicEdgeModel)currentModel;
                    break;
                case Module.AutoPermConfig:
                    BasketAutoPermModel autoPermConfigViewModel = (BasketAutoPermModel)currentModel;
                    automationConfigModel = GetAutomationConfig();
                    automationConfigModel.AutoPermConfigModelId = currentModel != null ? currentModel.Id : 0;
                    automationConfigModel.AutoPermConfigModel = autoPermConfigViewModel;
                    break;
                case Module.DynamicIncrementConfigs:
                    automationConfigModel = GetAutomationConfig();
                    automationConfigModel.LoopIncrementConfigModelId = currentModel != null ? currentModel.Id : 0;
                    automationConfigModel.LoopIncrementConfigModel = (LoopIncrementConfigModel)currentModel;
                    break;
                case Models.Module.MatrixSmartConfig:
                    BasketSettings.MatrixStrategyConfigId = currentModel != null ? currentModel.Id : 0;
                    BasketSettings.MatrixStrategyConfigModel = (MatrixStrategyConfigModel)currentModel;
                    break;
            }
        }

        public void EditDynamicConfig(Module configModule, IDynamicConfigModel selectedModel)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    DynamicEdgeConfigView view = new();
                    if (view.DataContext is DynamicEdgeConfigViewModel viewModel)
                    {
                        viewModel.Model = (DynamicEdgeModel)selectedModel;
                        viewModel.Loader = (config) => LoadDynamicConfig(configModule, selectedModel);
                    }
                    view.ShowDialog();
                    break;
                case Module.AutoPermConfig:
                    EditAutoPermConfig(selectedModel);
                    break;
                case Module.DynamicIncrementConfigs:
                    EditDynamicIncrementConfig(selectedModel);
                    break;
                case Models.Module.MatrixSmartConfig:
                    EditSyntheticSpreadConfig(selectedModel);
                    break;
            }
        }

        public IDynamicConfigModel GetDynamicConfig(Module configModule, string configJson = null)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    DynamicEdgeConfigModel config = null;
                    if (!string.IsNullOrWhiteSpace(configJson))
                    {
                        config = JsonConvert.DeserializeObject<DynamicEdgeConfigModel>(configJson);
                    }

                    config ??= new DynamicEdgeConfigModel()
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };


                    return config;
                case Module.AutoPermConfig:
                    BasketAutoPermModel autoPermConfig = null;
                    if (!string.IsNullOrWhiteSpace(configJson))
                    {
                        autoPermConfig = JsonConvert.DeserializeObject<BasketAutoPermModel>(configJson);
                    }
                    if (autoPermConfig == null)
                    {
                        AutomationConfigModel automationConfigModel = GetAutomationConfig();
                        BasketAutoPermModel autoPermConfigModel = automationConfigModel.AutoPermConfigModel;
                        autoPermConfig = new()
                        {
                            Creator = OmsCore.User.Username,
                            LastUpdateTime = DateTime.Now,
                            ShowAutoPermOthers = true,
                            AutoPermOtherInstances = autoPermConfigModel?.AutoPermOtherInstances ?? new(),
                            SubmitAutoPerms = autoPermConfigModel?.SubmitAutoPerms ?? true,
                            WaitForPrevious = autoPermConfigModel?.WaitForPrevious ?? true,
                            AutoPermOthers = autoPermConfigModel?.AutoPermOthers ?? false,
                        };
                    }
                    return autoPermConfig;
                case Module.DynamicIncrementConfigs:
                    LoopIncrementConfigModel model = null;
                    if (!string.IsNullOrWhiteSpace(configJson))
                    {
                        model = JsonConvert.DeserializeObject<LoopIncrementConfigModel>(configJson);
                    }

                    if (model == null)
                    {
                        model ??= new LoopIncrementConfigModel()
                        {
                            Creator = OmsCore.User.Username,
                            LastUpdateTime = DateTime.Now,
                            DynamicIncrementConfigs = new List<DynamicIncrementConfigModel>(),
                        };
                    }

                    return model;
                case Module.MatrixSmartConfig:
                    MatrixStrategyConfigModel syntheticSpreadConfig = null;
                    if (!string.IsNullOrWhiteSpace(configJson))
                    {
                        syntheticSpreadConfig = JsonConvert.DeserializeObject<MatrixStrategyConfigModel>(configJson);
                    }

                    if (syntheticSpreadConfig == null)
                    {
                        syntheticSpreadConfig ??= new MatrixStrategyConfigModel()
                        {
                            Creator = OmsCore.User.Username,
                            LastUpdateTime = DateTime.Now,

                        };
                    }

                    syntheticSpreadConfig.SyntheticSpreadStrategyData ??= new();
                    syntheticSpreadConfig.ScrapeStrategyData ??= new();
                    syntheticSpreadConfig.SeekerStrategyData ??= new();
                    syntheticSpreadConfig.SeekerSpreadStrategyData ??= new();

                    return syntheticSpreadConfig;
            }
            return null;
        }

        public int GetCurrentConfigId(Module configModule)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    AutomationConfigModel automationConfigModel = GetAutomationConfig();
                    return automationConfigModel.DynamicEdgeModelId;
                case Module.AutoPermConfig:
                    automationConfigModel = GetAutomationConfig();
                    return automationConfigModel.AutoPermConfigModelId;
                case Module.DynamicIncrementConfigs:
                    automationConfigModel = GetAutomationConfig();
                    return automationConfigModel.LoopIncrementConfigModelId;
                case Module.MatrixSmartConfig:
                    return BasketSettings.MatrixStrategyConfigId;
            }
            return 0;
        }

        public void EditAutoPermConfig(IDynamicConfigModel selectedModel)
        {
            try
            {
                AutoPermConfigView view = new();
                AutoPermConfigViewModel viewModel = view.DataContext as AutoPermConfigViewModel;

                BasketAutoPermModel basketAutoPermModel = (BasketAutoPermModel)selectedModel;
                viewModel.Model = basketAutoPermModel;
                viewModel.AutoPermConfigs = basketAutoPermModel.AutoPermConfigs.ToObservableCollection();
                viewModel.PermOperationModels = PermOperationModels.GroupBy(x => x.Title).Select(x => x.FirstOrDefault()).ToObservableCollection();
                viewModel.Loader = LoadDynamicConfig;
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditAutoPermConfig));
            }
        }

        public void EditDynamicIncrementConfig(IDynamicConfigModel selectedModel)
        {
            try
            {
                LoopDynamicIncrementConfigView view = new();
                LoopDynamicIncrementConfigViewModel viewModel = view.DataContext as LoopDynamicIncrementConfigViewModel;

                viewModel.SetModel((LoopIncrementConfigModel)selectedModel);

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditDynamicIncrementConfig));
            }
        }

        public void EditSyntheticSpreadConfig(IDynamicConfigModel selectedModel)
        {
            try
            {
                SyntheticSpreadConfigView view = new();
                SyntheticSpreadConfigViewModel viewModel = view.DataContext as SyntheticSpreadConfigViewModel;

                viewModel.SetModel((MatrixStrategyConfigModel)selectedModel);

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditSyntheticSpreadConfig));
            }
        }

        internal new bool Dispose()
        {
            _log.Info("Start. Id: {0}", InstanceId);
            if (GetIsPartOfVol())
            {
                _log.Info("Basket Part of Vol-Trader. Id: {0}", InstanceId);
                MessageResult response = Dispatcher.Invoke(() => MessageBoxService.ShowMessage("Basket is part of vol trader, Would you like to hide the basket instead?", Name, MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.Yes));
                switch (response)
                {
                    case MessageResult.Cancel:
                        return true;
                    case MessageResult.Yes:
                        Hide();
                        _log.Info("Hide Basket Selected. Id: {0}", InstanceId);
                        return true;
                    case MessageResult.No:
                        break;
                }
            }
            else if (IsEdgeScanFeedAutoTrader)
            {
                _log.Info("Basket Part of Edge Scan Feed. Id: {0}", InstanceId);
                return true;
            }

            _log.Info("Disposing Basket. Id: {0}", InstanceId);

            base.Dispose();

            return false;
        }

        public override void OnDispose()
        {
            try
            {
                OmsCore.Config.ConfigChangedEvent -= OnConfigChangedEvent;
                ConfigBrowserViewModel.LoadConfig = null;

                _log.Info("Unsubscribing Basket. Id: {0}", InstanceId);
                OmsCore.QuoteClient.UnsubscribeAll(this);
                TransactionConsumer?.UnsubscribeAll(this);
                PortfolioManagerModel?.UnsubscribeAll(this);
                UnsubscribePnlAsync();

                _log.Info("Removing Basket From Vol Trader. Id: {0}", InstanceId);
                _volTradersManager.RemoveBasketFromVolTraders(this);
                _volTradersManager.VolTraderUpdatedEvent -= VolTraderUpdatetd;

                _log.Info("Stopping UI update timers. Id: {0}", InstanceId);
                StopTimers();
                _undoStack?.Clear();
                _redoStack?.Clear();

                _log.Info("Removing From Basket Manager. Id: {0}", InstanceId);
                RemoveFromBasketGroupManager();
                BasketItems.ForEach(i => UnregisterEvents(i));
                if (BasketSettings != null && (BasketType == BasketType.LockTrader || !BasketSettings.CancelOnClose))
                {
                    _log.Info("Disposing Basket Items (No Cancel). Type: {0}, Id: {1}", BasketType, InstanceId);
                    BasketItems.ForEach(i => i.DisposeNoCancel());
                }
                else
                {
                    _log.Info("Stopping Submit with Delay. Id: {0}", InstanceId);
                    CancelQueuedSubmitWithDelay();

                    _log.Info("Disposing Basket Items. Type: {0}, Id: {1}", BasketType, InstanceId);
                    BasketItems.ForEach(i => i.Dispose());

                    _log.Info("Disconnecting Basket Manager. Id: {0}", InstanceId);
                    OmsCore.BasketManagerClient.Disconnect(this);

                    if (GetInstanceMode().IsAutoTraderInstance())
                    {
                        OmsCore.AutoTraderClient.CancelGroup(Uid);
                    }
                }

                SelectedItems?.Clear();

                _submitWithDelayCancellationTokenSource?.Cancel();
                _submitWithDelayCancellationTokenSource?.Dispose();

                _timerCts?.Cancel();
                _timerCts?.Dispose();

                EmaCalculatorGenerator.Dispose();

                Dispatcher?.Invoke(() =>
                {
                    VolTraders.Clear();
                    VolTraders = null;

                    BasketItems?.Clear();
                    BasketItems = null;
                });

                ServiceContainer.Clear();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to dispose basket. Id: {0}", InstanceId);
            }
            finally
            {
                BorderBrush = null;
                PortfolioManagerModel = null;
                NotificationManager = null;
                TransactionConsumer = null;
                DominatorsManagerModel = null;
                TicketFactory = null;
                ThreeWayCloserFactory = null;
                ExpirationDescription = null;
                BasketSettings = null;
                base.OnDispose();
            }
        }


        private void StopTimers()
        {
            if (_uiUpdateTimer != null)
            {
                _uiUpdateTimer.Stop();
                _uiUpdateTimer.Tick -= UiUpdateTimer_Tick;
                _uiUpdateTimer = null;
            }

            if (_basketTitleBarUpdateTimer is not null)
            {
                _basketTitleBarUpdateTimer.Stop();
                _basketTitleBarUpdateTimer.Tick -= BasketGeneralUpdateTimer_Tick;
                _basketTitleBarUpdateTimer = null;
            }

            if (_messageClearTimer is not null)
            {
                _messageClearTimer.Elapsed -= OnMessageClearTimerElapsed;
                _messageClearTimer.Stop();
                _messageClearTimer.Dispose();
            }

            if (_nagbotTimer is not null)
            {
                _nagbotTimer.Elapsed -= OnNagbotTimerElapsed;
                _nagbotTimer.Stop();
                _nagbotTimer.Dispose();
            }

            if (_resubmitCountdownTimer is not null)
            {
                _resubmitCountdownTimer.Stop();
                _resubmitCountdownTimer.Tick -= ResubmitTimer_Tick;
                _resubmitCountdownTimer.Tick -= ResubmitBuy_Tick;
                _resubmitCountdownTimer.Tick -= ResubmitSell_Tick;
                _resubmitCountdownTimer = null;
            }

            if (_modifyCountdownTimer is not null)
            {
                _modifyCountdownTimer.Stop();
                _modifyCountdownTimer.Tick -= ModifyCountdownTimer_Tick;
                _modifyCountdownTimer = null;
            }
        }

        public InstanceMode GetInstanceMode()
        {
            return InstanceModeLocked ? OmsCore.Config.InstanceModeV3 : InstanceMode;
        }

        public Venue GetVenue()
        {
            switch (GetInstanceMode())
            {
                case InstanceMode.OPS_SILEXX:
                    return Venue.Silexx;
                case InstanceMode.OPS_TB:
                    return Venue.TB;
                case InstanceMode.OPS_ZPFIX:
                    return Venue.ZpFix;
                case InstanceMode.AT_SILEXX:
                    return Venue.Silexx;
                case InstanceMode.AT_ZPFIX:
                    return Venue.ZpFix;
                default:
                    return Venue.TB;
            }
        }

        [Command]
        public void BrokerChangedCommand()
        {
            ApplyInstanceModeChangesCommand();
        }

        [Command]
        public void ApplyInstanceModeChangesCommand()
        {
            if (IsDisposed)
            {
                return;
            }

            CancelAll();
            ApplyAutoTraderChanges();
            UpdateRoutesListAsync();
        }
    }
}
