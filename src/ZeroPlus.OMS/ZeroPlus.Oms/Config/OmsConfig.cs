using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.Cob.Client.Config.Interfaces;
using ZeroPlus.Comms.Models.Data.Oms;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Databento.Client.Config.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;
using ZeroPlus.Ema.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.HubTron.Client.Config.Interfaces;
using ZeroPlus.IbGateway.Client.Config.Interfaces;
using ZeroPlus.Interpolator.Client.Config.Interfaces;
using ZeroPlus.LiveVol.Client.Config.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Helpers;
using ZeroPlus.Pricing.Client.Config.Interfaces;
using ZeroPlus.Raptor.Client.Config;
using ZeroPlus.Raptor.Client.Config.Interfaces;
using ZeroPlus.SymbolMap.Client.Config.Interfaces;
using ZeroPlus.Telemetry.Client.Config.Interfaces;
using ZeroPlus.Theos.Client.Config.Interfaces;
using ZeroPlus.Trades.Client.Config.Interfaces;


namespace ZeroPlus.Oms.Config
{
    [Serializable]
    public class OmsConfig : INotifyPropertyChanged
    {
        public delegate void ConfigChangedEventHandler(OmsConfig config, bool requiresRestart);
        public delegate void ConfigMessageEventHandler(string message);

        public event PropertyChangedEventHandler PropertyChanged;
        public event ConfigChangedEventHandler ConfigChangedEvent;
        public event ConfigMessageEventHandler ConfigMessageEvent;

        public const string SOLUTION_NAME = "ZeroPlus.Oms";
        public static string ConfigName = "ZeroPlus.Oms";
        public const string DEFAULT_WORKSPACE = "Oms Default";
        private const string DEFAULT_EMASERVER_PORT = "8095";
        private readonly HashSet<string> _algoRoutes = new()
        {
            "EXCH_ROLL",
            "EXCH_ROLL_S",
            "EXCH_ROLL_SR",
            "ZPROLL",
            "ZPROLLS",
            "TEXCH_ROLL",
            "TEXCH_ROLL_S",
            "TEXCH_ROLL_SR",
            "TZPROLL",
            "TZPROLLS",
            "MSEEKER",
        };

        #region Defaults
        private string _logLevel = "Info";
        private string _mdsClientVersion = "1.43.0.0";
        private string _orderAddress = "127.0.0.1";
        private int _orderPort = 8111;
        private string _positionAddress = "127.0.0.1";
        private int _positionPort = 9091;
        private string _quoteAddress = "127.0.0.1";
        private int _quotePort = 8090;
        private int _performanceModeMarketDataThrottleMs = 500;
        private EmaModel _emaMode = EmaModel.Ema;
        private bool _useAdjEma;
        private LiveEmaPeriod _liveEmaPeriod = LiveEmaPeriod.Mid;
        private GreekSource _greeksSource = GreekSource.Hanweck;
        private string _hanweckAddress = "127.0.0.1";
        private int _hanweckPort = 8096;
        private string _databentoAddress = "xnas-itch.lsg.databento.com";
        private string _databentoTsAddress = "https://hist.databento.com/v0/timeseries.get_range";
        private int _databentoPort = 13000;
        private bool _connectClientsOnStartupV2 = true;
        private int _clientHbInterval = 500;
        private int _clientReconInterval = 5000;
        private bool _checkForUpdateOnInterval = true;
        private string _appUpdateUrl = @"http://downloads.corp.zeroplusderivatives.com/oms/";
        private bool _useLogAggregator = true;
        private string _logAggregatorUrl = @"http://alloy.telemetry.zp:4317";
        private int _defaultUiUpdateInterval = 500;
        private int _maxUpdateSize = 200000;
        private bool _isDevMode;
        private bool _notificationsForMyOrdersOnly = true;
        private bool _showOrderAckNotificationsV2;
        private bool _showOrderPartialFillNotificationsV2;
        private bool _showOrderFillNotificationsV2;
        private bool _showOrderCancelNotificationsV2;
        private bool _showOrderReplaceNotificationsV2;
        private bool _showOrderRejectNotificationsV2;
        private bool _showFirstEdgeNotificationsV2;
        private double _showFirstEdgeNotificationsThreshold;
        private bool _showDuplicateRestingOrdersNotificationV2;
        private bool _showTaggedOrdersNotificationV2;
        private int _showDuplicateRestingOrdersNotificationCount = 3;
        private int _showDuplicateRestingOrdersNotificationPeriod = 180000;
        private bool _showPopupOnRejectedOrder;
        private bool _playNotificationSoundsInSequence;
        private bool _playOrderAckNotificationSoundV2;
        private bool _playOrderPartialFillNotificationSoundV2 = true;
        private bool _playOrderFillNotificationSoundV2 = true;
        private bool _playOrderCancelNotificationSoundV2;
        private bool _playOrderReplaceNotificationSoundV2;
        private bool _playOrderRejectNotificationSoundV2;
        private bool _playFirstEdgeNotificationSoundV2;
        private double _playFirstEdgeNotificationSoundThreshold;
        private string _firstEdgeNotificationSound = "";
        private bool _playDuplicateRestingOrdersNotificationV2;
        private string _duplicateRestingOrdersNotificationSound = "DING";
        private double _globalNotificationSettingsX;
        private double _globalNotificationSettingsY;
        private bool _newestNotificationOnTop;
        private int _maxDisplayedNotifications = 10;
        private int _removeNotificationTimerInterval = 20000;
        private bool _enablePriceTrackBar = true;
        private bool _showQuickRoutes = true;
        private double _maxHedgeWidthV2 = 0.15;
        private double _maxAutoHedgePercentV2 = 1;
        private bool _maxAutoHedgePositionEnabled;
        private bool _maxAutoHedgeNetCashEnabled;
        private int _maxAutoHedgePosition;
        private double _maxAutoHedgeNetCash;
        private bool _autoScrollOrderGrids;
        private bool _addNewRowsToTop;
        private bool _alwaysShowBuysOnTheLeft;
        private string _orderBookFilter = "ALL";
        private bool _showWorkingOrdersGrid = true;
        private int _decimalPlacesForGreeks = 3;
        private bool _ticketLockEdgeToTheoWhenLoadingFromOrderbook;
        private double _defaultContraEdge = 0.2;
        private double _defaultSpxContraEdge = 0.2;
        private double _defaultTheoEdge;
        private double _defaultMidEdge;
        private bool _allowCombinedSimultaneousOrders;
        private bool _autoReverseOrdersToBuySide = true;
        private bool _haveContraBuysOnTheLeft;
        private int _orderBookQuoteDecimalPlaces = 4;
        private int _orderBookGreekDecimalPlaces = 4;
        private double _orderBookFontSize = 12;
        private double _portfolioFontSize = 12;
        private double _percentBidFontSize = 8;
        private bool _progressiveZoomEnabled = true;

        private string _authServer = "auth.oms.corp.zeroplusderivatives.com";
        private int _authServerPort = 7677;

        private bool _dominatorsManagerListenerEnabled;
        private bool _dominatorsManagerAutoTraderEnabled;
        private string _dominatorsManagerListenerAddress = "127.0.0.1";
        private int _dominatorsManagerListenerPort = 7676;

        private bool _basketManagerListenerEnabledV2;
        private string _basketManagerListenerAddress = "127.0.0.1";
        private int _basketManagerListenerPort = 7677;

        private bool _dominatorClientEnable;
        private string _dominatorsManagerOmsAddress = "127.0.0.1";
        private int _dominatorsManagerOmsPort = 7676;

        private bool _basketManagerClientEnableV2;
        private string _basketManagerOmsAddress = "127.0.0.1";
        private int _basketManagerOmsPort = 7677;

        private string _externalDbAddress = "127.0.0.1";
        private string _externalDbUser = "dbUser";
        private string _externalDbPass = "dbPass";

        private int _submitWithDelayIntervalMin = 250;
        private bool _getVerificationForBasketSubmitAllV2;
        private bool _getVerificationForBasketReverse = true;

        private int _maxSimultaneousLoopsV2 = 30;
        private int _maxCancelOnLimitV2 = 3;
        private int _loopDelayMin;
        private int _loopDelayMax;
        private double _blockTraderPercentBid = 0.05;
        private double _blockTraderModifyTimer = 0.2;
        private string _blockTraderLayout = "";
        private bool _globalBasketRiskControlEnabledV2 = true;
        private bool _globalTicketRiskControlEnabledV2 = true;
        private string _workspaceTitle = "Default";
        private bool _useDeltaAdjTheoForThreeWay = true;
        private ThreeWayPreference _threeWayPreference = ThreeWayPreference.ITM;
        private OrderType _hedgeOrderType = OrderType.Market;
        private int _hedgeInterval = 1000;
        private bool _autoFlattenHedgeV2 = true;
        private bool _autoHedgeWhenAddingPositionToHedgedSpread;
        private double _autoHedgeLimitDiff;
        private double _autoHedgeLimitIncrement = 0.05;
        private bool _interpolationEnabled;
        private bool _usePercentageForDeltaNotion;
        private bool _deltaAdjPxEnabledOnTickets;
        private OrderTicketStyle _defaultOrderTicketStyle = OrderTicketStyle.Complex;
        private PriceEvaluationStyle _priceEvaluationStyle = PriceEvaluationStyle.Reversed;
        private string _lockTraderTif = "GTC";
        private int _lockTraderQtyMin = 1;
        private int _lockTraderQtyMax = 1;
        private double _lockTraderPriceMin = -0.05;
        private double _lockTraderPriceMax = 0.02;
        private List<object> _lockTraderAllowedStrategies = new();
        private bool _ticketCancelTimerEnabledV2 = true;
        private bool _stockTicketCancelTimerEnabledV2 = true;
        private bool _basketCancelTimerEnabledV2 = true;
        private double _singleLegCancelTimerDefaultIntervalV2 = 20;
        private double _spreadCancelTimerDefaultIntervalV2 = 125;
        private bool _forThreeWayTicketsUseThreeWayFlat;
        private bool _lockDeltaAdjPriceWhenLoadingTickets = true;
        private double _smartRouteOverwatchTimer = 1200;
        private bool _ticketAlwaysOnTop;
        private bool _doNotStackUpTickets = true;
        private bool _showFirstEdgeForThisSessionOnly;
        private bool _activeUncheckEnabled = true;
        private bool _basketDeltaAdjLastFillPx;
        private int _activeUncheckQty = 5;
        private double _activeUncheckEdge = .05;
        private double _recentTradeLookback = 15;
        private double _autoPermTradeLookback = 20;
        private double _closeButtonEdge = .1;
        private double _closeButtonPxIncrement = .05;
        private int _closeButtonInterval = 1100;
        private CloseFastEdgeType _closeFastEdgeType = CloseFastEdgeType.Edge;
        private double _closeFastButtonEdgePercentageMin = .2;
        private double _closeFastButtonEdgePercentageMax = .3;
        private double _closeFastButtonEdge = .1;
        private double _closeFastButtonPxIncrement = .05;
        private int _closeFastButtonInterval = 1100;
        private double _marketMakerMaxPxOffsetPercent = .2;
        private double _marketMakerMaxAwayFromMarketPercent = .15;
        private double _bidPercentLimit = .5;
        private double _minimumBidPercentLimit;
        private double _minimumEmaAndBidEdge;
        private double _minimumEdgeToTheoLimitV2 = -3;
        private double _minimumVolTraderEdgeLimit = -0.2;
        private bool _basketDeltaLimitEnabledV2 = true;
        private double _basketDeltaLimitV2 = 10000;
        private bool _basketLongPositionLimitEnabled;
        private int _basketLongPositionLimit = 15;
        private bool _basketShortPositionLimitEnabled;
        private int _basketShortPositionLimit = 15;
        private bool _pushUpdatesAsync;
        private bool _logBasketValues;
        private double _ticketUiUpdateInterval = 65;
        private double _basketUiUpdateInterval = 125;
        private bool _restApiEnabled;
        private string _restApiAddress = "127.0.0.1";
        private int _restApiPort = 7678;
        private bool _audioAlertEnabled;
        private bool _visualAlertEnabled;
        private bool _ttsEnabled = true;
        private string _audioAlertSound;
        private bool _alertEnabled;
        private int _alertMinQty = 1;
        private int _alertThreshold = 5;
        private int _snoozeThreshold = 60;
        private bool _updateOnlySyncAdjTheo = true;
        private bool _updateOnlySyncEma = true;
        private double _adjustedEdgeSummaryLookback = 30;
        private double _adjustedEdgeSummaryUnderPercentage = 0.05;
        private int _twsClientId = 7;
        private bool _enableTwsApi;
        private bool _orderbookIdentifyEdgeScanTrades;
        private bool _promptToLoadEdgeOverrideWhenDraggingToBasket;
        private bool _allowDeltaHedging;
        private DepthColorType _depthColorType = DepthColorType.UNIFORM;
        private bool _silentTicketNotifications;
        private bool _allowUserPositionTracking;
        private bool _enablePermAdjPx;
        private TwsMarketDataType _twsMarketDataType = TwsMarketDataType.Delayed;
        private bool _enableDeltaAdjustedEma;
        private double _emaSmoothing = 2;
        private int _emaInterval = 5000;
        private int _emaPeriods = 30;
        private bool _warnAgainstDoubleFillOnCloseEnabled;
        private bool _useOrderTicketForSingleLegOrders;
        private double _orderDelayForBlocking = 10000;
        private double _orderDelayBlockingDelay;
        private bool _enablePythonEngine;
        private int _stopLossMaxAttempt = 5;
        private bool _forCrossPriceUseRouteEnabled = true;
        private bool _useSignedValueForSpreadVegaFilters;
        private bool _blockDuplicateByTimeV2 = true;
        private int _blockDuplicateThresholdV2 = 250;
        private double _recalculateBasketPriceInterval = 1.5;
        private double _basketItemsTimeToLiveForAutoClear = 45;
        private bool _showBasketConfigsInOrderbook = true;
        private bool _showNagBotButtonInOrderbookV2;
        private bool _showBasketQuickLayoutPanel;
        private bool _allowLockingMarketPrices;
        private int _loopMaxResubmitWithIncrement = 100;
        private bool _loopMaxResubmitWithIncrementCheckEnabled;
        private readonly ConcurrentDictionary<ExecutingBroker, ExecutingBrokerFeeModel> _executingBrokerToFeeModelMap = new();
        private readonly List<ExecutingBrokerFeeModel> _executingFeeModels = new();
        private ExecutingBrokerFeeModel _defaultBrokerFeeModel;
        private bool _allowSavingEdgeScanFeedBasketsWithWorkspace;
        private string _defaultHedgeHouseRouteRegular = "ISMART";
        private string _defaultHedgeHouseRouteAutoTraderV2 = "ISMART";
        private string _defaultHedgeHouseSpreadRoute = "EXCH_ROLL";
        private string _defaultHedgeHouseSingleLegRoute = "EXCH_ROLL_S";
        private int _hedgeHouseResubmitCount = 2;
        private double _priceCacheClearIntervalMs = 120_000;
        private bool _priceCacheClearIntervalEnabled;
        private int _basketHedgeHouseMaxQtyV2 = 5000;
        private double _basketHedgeHouseMaxNotionalV2 = 2_000_000;
        private bool _openSeparateTicketForUnderlying;
        private bool _testValueSubscriptionEnabled;
        private SubscriptionFieldType _testValueSubscriptionFieldType = SubscriptionFieldType.DeltaAdjTheo;
        private bool _showNegativeEdgeWarning;
        private bool _enableEmbeddedLogging;
        private int _maxPosForDualTicket = 2;
        private bool _wrapColumnHeaderV2 = true;
        private string _layoutDefaultDateTimeColumnFormat = "MM/dd hh:mm:ss.fff";
        private bool _configReset;
        private InstanceMode _instanceModeV3 = InstanceMode.OPS_SILEXX;
        private bool _perforamnceModeEnabled;
        private double _theoMisMatchResetIntervalSec = 5;
        private string _databentoApiKey = "";
        private bool _removeAdjustedOptions = true;
        private double _maxStatusDelaySeconds = 10;
        private bool _warnAgainstLargeSizeUpConfigV2 = true;
        private int _warnAgainstLargeSizeUpQty = 99;
        private bool _showOrderRateAlertV2 = true;
        private bool _removeDuplicateColumnsFromLayout;
        private bool _useMoneynessForOppCpInBasketsV2;
        private bool _useMoneynessForOppCpInTicketsV2;
        private int _undoCapacity = 50;
        private string _lowLatencyAddressV1 = "10.11.128.85;10.11.128.85";
        private string _lowLatencyTestAddressV1 = "10.11.128.81";
        private int _lowLatencyPort = 8555;
        private int _lowLatencyTestPort = 8555;
        private string _loLaAccount = "AOSZERO";
        private string _loLaFdid = "";
        private bool _subscribeToHardSideIdentification;
        private bool _showEodRiskV2;
        private bool _basketAutomationHighlightEdge;
        private bool _basketAutomationHighlightMinEdge;
        private bool _basketAutomationHighlightSizeup;
        private bool _basketAutomationHighlightMaxLoss;
        private bool _basketAutomationHighlightPxInc;
        private bool _basketAutomationHighlightMaxLoop;
        private bool _maintainBaseStrategyExceptionForFlyEnabled;
        private bool _useServerSidePerming;
        private bool _trackOrdersBySpreadV2;
        private bool _cancelRestingOrdersOnCombinedTickets = true;
        private bool _showEdgeRiskWarningOnDualTickets = true;
        private int _spreadGeneratorTimeout = 1000;
        private bool _spreadGeneratorUseGlobalTimeout;
        private bool _playEdgeGiveUpNotificationV2;
        private string _edgeGiveUpNotificationSound = "";
        private double _playEdgeGiveUpNotificationThreshold;
        private bool _showEdgeGiveUpNotificationV2;
        private int _maxOrderSubmissionPerSecondLimit = 30;
        private string _savedAccount;
        private string _savedBroker;
        private AccountConfigModel _accountConfig;
        private bool _subscribeToHardSideIdentificationOnTickets;
        private bool _useCommonDispatcherForBaskets;
        private bool _useSkewAdjustedHiBidLoAskForSpreadCalc = true;
        private bool _showCobOnTicketsV2;
        private int _autoCancelMaxResubmit = 5;
        private string _basketBorderKeyGesture = "Ctrl+Alt+K";
        private string _mixedBorderColor = "#FFA500";
        private string _multiLegBorderColor = "#0000FF";
        private string _singlesLegBorderColor = "#008000";
        private FishLossSaveModel _basketFishLossConfig;
        private FishLossSaveModel _edgeScanFishLossConfig;
        private ColorTheme _colorTheme;
        private bool _logAutoCancelAndFishLossPass;
        private bool _ticketCancelOnPartialFill = true;
        private bool _lockAutoTraderVenue = true;
        private PositionSubscriptionMode _positionSubscriptionMode = PositionSubscriptionMode.User;
        private int _BasketAlertTimeout = 5000;
        private bool _allowUsingOfStaticPriceInBaskets;
        private bool _useFirmNetQtyForFlat;
        private bool _interpolatorClientEnabled;
        private bool _theosClientEnabled;
        private bool _hubTronClientEnabled;
        private bool _transactionClientEnabled = true;
        private bool _orderClientEnabled = true;
        private bool _autoTraderClientEnabled = true;
        private bool _autoTraderDirectClientEnabled = true;
        private bool _positionClientEnabled = true;
        private bool _quoteClientEnabled = true;
        private bool _requestClientEnabled = true;
        private bool _hanweckClientEnabled = true;
        private bool _edgeScannerClientEnabled = true;
        private bool _edgeScanFeedRunnerClientEnabled = true;
        private bool _symbolMapClientEnabled = true;
        private bool _telemetryClientEnabled;
        private byte _telemetryBoxId = TelemetryHelper.GetBoxId();
        private byte _telemetryInstanceId = TelemetryHelper.GetInstanceId();
        private bool _emaClientEnabled = true;
        private bool _daEmaClientEnabled = true;
        private bool _ibGatewayClientEnabled;
        private bool _databentoClientEnabled;
        private bool _cobClientEnabled;
        private bool _PricingClientEnabledV2 = true;
        private bool _derivativesClientEnabled = true;
        private bool _liveVolDataClientEnabled = true;
        private bool _showInitQtyWarning = true;
        private QuoteSource _quoteSource = QuoteSource.Tron;
        private bool _autoSwitchQuoteSource = true;
        private long _marketOpenTimeEasternTicks = new TimeSpan(9, 30, 0).Ticks;
        private long _marketCloseTimeEasternTicks = new TimeSpan(16, 0, 0).Ticks;
        private bool _useSmartModuleTitleForBaskets;
        private bool _routeOpsOrdersToAutoTraderDirect;
        private bool _showLockTheosShortcutInBasketHeader = true;
        private bool _showProminentBasketPermButtons = true;
        private int _spreadGeneratorPromptLimit = 2000;

        #endregion
        [XmlIgnore]
        public IEnumerable<string> DeltaAdjTheoBases { get; } = Enum.GetNames<DeltaAdjTheoBase>().ToList();
        [XmlIgnore]
        public IEnumerable<EmaModel> EmaSources { get; } = Enum.GetValues<EmaModel>().ToList();
        [XmlIgnore]
        public IEnumerable<LiveEmaPeriod> LiveEmaPeriods { get; } = Enum.GetValues<LiveEmaPeriod>().ToList();
        [XmlIgnore]
        public IEnumerable<TwsMarketDataType> TwsMarketDataTypes { get; } = Enum.GetValues<TwsMarketDataType>().ToList();
        [XmlIgnore]
        public IEnumerable<CloseFastEdgeType> CloseFastEdgeTypes { get; } = Enum.GetValues<CloseFastEdgeType>().ToList();
        [XmlIgnore]
        public IEnumerable<ThreeWayPreference> ThreeWayPreferences { get; } = Enum.GetValues<ThreeWayPreference>().ToList();
        [XmlIgnore]
        public IEnumerable<OrderType> AutoHedgePreferences { get; } = Enum.GetValues<OrderType>().ToList();
        [XmlIgnore]
        public IEnumerable<DepthColorType> DepthColorTypes { get; } = Enum.GetValues<DepthColorType>().ToList();
        [XmlIgnore]
        public IEnumerable<OrderTicketStyle> OrderTicketStyles { get; } = Enum.GetValues<OrderTicketStyle>().ToList();
        [XmlIgnore]
        public IEnumerable<PriceEvaluationStyle> PriceEvaluationStyles { get; } = Enum.GetValues<PriceEvaluationStyle>().ToList();
        [XmlIgnore]
        public IEnumerable<PositionSubscriptionMode> PositionSubscriptionModes { get; } = Enum.GetValues<PositionSubscriptionMode>().ToList();
        [XmlIgnore]
        public Dictionary<string, Tuple<string, string, double, bool>> SymbolsLookup { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, double> ContraEdgeLookup { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, List<Tuple<string, string, double, bool>>> ReverseSymbolsLookup { get; set; } = new();
        [XmlIgnore]
        public List<FishRoute> FishRoutes { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, Dictionary<int, Tuple<string, double>>> SmartRoutes { get; set; } = new();
        [XmlIgnore]
        public List<ColorTheme> ColorThemes { get; set; }
        [XmlIgnore]
        public FishLossSaveModel BasketFishLossConfig
        {
            get => _basketFishLossConfig;
            set
            {
                if (value != null)
                {
                    _basketFishLossConfig = value;
                    SaveFishLossConfig();
                }
            }
        }
        [XmlIgnore]
        public FishLossSaveModel EdgeScanFishLossConfig
        {
            get => _edgeScanFishLossConfig;
            set
            {
                if (value != null)
                {
                    _edgeScanFishLossConfig = value;
                    SaveEdgeScanFishLossConfig();
                }
            }
        }
        [XmlIgnore]
        public Dictionary<BaseStrategy, LockTraderPriceLimitModel> LockTraderPriceLimits { get; set; } = [];
        [XmlIgnore]
        public List<Tuple<string, string>> QuickRoutes { get; set; } = new List<Tuple<string, string>>();
        [XmlIgnore]
        public List<Tuple<string, string>> BlockTraderRoutes { get; set; } = new List<Tuple<string, string>>();
        [XmlIgnore]
        public List<Tuple<string, string, double, double>> CancelTimerLookup { get; set; } = new();
        [XmlIgnore]
        public ConcurrentDictionary<Tuple<string, string>, Tuple<string, string, double, double>> UnderAndRouteToCancelIntervalMap { get; } = new();
        [XmlIgnore]
        public ConcurrentDictionary<string, Tuple<string, string, double, double>> UnderToCancelIntervalMap { get; } = new();
        [XmlIgnore]
        public ConcurrentDictionary<string, Tuple<string, string, double, double>> RouteToCancelIntervalMap { get; } = new();
        [XmlIgnore]
        public List<Tuple<string, double, double, int>> TicketStopLossLookup { get; set; } = new List<Tuple<string, double, double, int>>();
        [XmlIgnore]
        public List<Tuple<string, string, double>> BasketHedgeLookup { get; set; } = new List<Tuple<string, string, double>>();
        [XmlIgnore]
        public Dictionary<string, Tuple<string, double>> BasketHedgeLookupMap { get; set; } = new Dictionary<string, Tuple<string, double>>();
        [XmlIgnore]
        public Dictionary<string, BasketMarketMakerOffsetLookupModel> BasketMarketMakerOffsetLookup { get; set; } = new Dictionary<string, BasketMarketMakerOffsetLookupModel>();
        [XmlIgnore]
        public Dictionary<string, DerivedValueConfigModel> DerivedValueConfigModelLookup { get; set; } = new Dictionary<string, DerivedValueConfigModel>();
        [XmlIgnore]
        public bool SaveOnChange { get; set; }
        [XmlIgnore]
        public string AppId { get; set; } = "ZeroPlus OMS";
        [XmlIgnore]
        public static Guid OpsGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid PositionGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid MdGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid ReqGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid HwGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid GatewayGuid => Guid.NewGuid();
        [XmlIgnore]
        public static Guid ManagerGuid => Guid.NewGuid();
        #region Fees
        [XmlIgnore]
        public double BrokerageFee { get; set; }
        [XmlIgnore]
        public double OrfFee { get; set; }
        [XmlIgnore]
        public double SecFee { get; set; }
        [XmlIgnore]
        public double VolantFee { get; set; }
        [XmlIgnore]
        public double VolantZprollFee { get; set; }
        [XmlIgnore]
        public double DashFee { get; set; }
        [XmlIgnore]
        public double DashSPXFee { get; set; }
        [XmlIgnore]
        public Dictionary<string, Commission> UnderlyingToCommissionsMap { get; set; } = new Dictionary<string, Commission>();
        [XmlIgnore]
        public List<ExecutingBrokerFeeModel> ExecutingBrokerFeeModels { get; set; } = new();
        [XmlIgnore]
        public double ExecutingBrokerFeeModelsMax { get; set; }
        [XmlIgnore]
        public double ExecutingBrokerFeeModelsAverage { get; set; }
        [XmlIgnore]
        public double ExecutingBrokerFeeModelsMaxNonPenny { get; set; }
        [XmlIgnore]
        public double ExecutingBrokerFeeModelsAverageNonPenny { get; set; }
        #endregion Fees

        public string LogLevel
        {
            get => _logLevel;
            set
            {
                _logLevel = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string OrderAddress
        {
            get => _orderAddress;
            set
            {
                _orderAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int OrderPort
        {
            get => _orderPort;
            set
            {
                _orderPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string PositionAddress
        {
            get => _positionAddress;
            set
            {
                _positionAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int PositionPort
        {
            get => _positionPort;
            set
            {
                _positionPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string QuoteAddress
        {
            get => _quoteAddress;
            set
            {
                _quoteAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int QuotePort
        {
            get => _quotePort;
            set
            {
                _quotePort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int PerformanceModeMarketDataThrottleMs
        {
            get => _performanceModeMarketDataThrottleMs;
            set
            {
                _performanceModeMarketDataThrottleMs = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public EmaModel EmaMode
        {
            get => _emaMode;
            set
            {
                _emaMode = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool UseAdjEma
        {
            get => _useAdjEma;
            set
            {
                _useAdjEma = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public LiveEmaPeriod LiveEmaPeriod
        {
            get => _liveEmaPeriod;
            set
            {
                _liveEmaPeriod = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public QuoteSource QuoteSource
        {
            get => _quoteSource;
            set
            {
                _quoteSource = value;
                UpdateQuoteSource(value);
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AutoSwitchQuoteSource
        {
            get => _autoSwitchQuoteSource;
            set
            {
                _autoSwitchQuoteSource = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public long MarketOpenTimeEasternTicks
        {
            get => _marketOpenTimeEasternTicks;
            set
            {
                _marketOpenTimeEasternTicks = value;
                OnChange(false);
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(MarketOpenTimeEasternDisplay));
            }
        }

        public long MarketCloseTimeEasternTicks
        {
            get => _marketCloseTimeEasternTicks;
            set
            {
                _marketCloseTimeEasternTicks = value;
                OnChange(false);
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(MarketCloseTimeEasternDisplay));
            }
        }

        [XmlIgnore]
        public string MarketOpenTimeEasternDisplay
        {
            get => TimeSpan.FromTicks(_marketOpenTimeEasternTicks).ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value, @"hh\:mm", null, out var ts))
                {
                    MarketOpenTimeEasternTicks = ts.Ticks;
                    NotifyPropertyChanged();
                }
            }
        }

        [XmlIgnore]
        public string MarketCloseTimeEasternDisplay
        {
            get => TimeSpan.FromTicks(_marketCloseTimeEasternTicks).ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value, @"hh\:mm", null, out var ts))
                {
                    MarketCloseTimeEasternTicks = ts.Ticks;
                    NotifyPropertyChanged();
                }
            }
        }

        public GreekSource GreeksSource
        {
            get => _greeksSource;
            set
            {
                _greeksSource = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string HanweckAddress
        {
            get => _hanweckAddress;
            set
            {
                _hanweckAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int HanweckPort
        {
            get => _hanweckPort;
            set
            {
                _hanweckPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string LowLatencyAddressV1
        {
            get => _lowLatencyAddressV1;
            set
            {
                _lowLatencyAddressV1 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string LowLatencyTestAddressV1
        {
            get => _lowLatencyTestAddressV1;
            set
            {
                _lowLatencyTestAddressV1 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LowLatencyPort
        {
            get => _lowLatencyPort;
            set
            {
                _lowLatencyPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LowLatencyTestPort
        {
            get => _lowLatencyTestPort;
            set
            {
                _lowLatencyTestPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string DatabentoEq2Address
        {
            get => _databentoAddress;
            set
            {
                _databentoAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string DatabentoTsAddress
        {
            get => _databentoTsAddress;
            set
            {
                _databentoTsAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DatabentoPort
        {
            get => _databentoPort;
            set
            {
                _databentoPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string DatabentoApiKey
        {
            get => _databentoApiKey;
            set
            {
                _databentoApiKey = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string MdsClientVersion
        {
            get => _mdsClientVersion;
            set
            {
                _mdsClientVersion = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public bool ConnectClientsOnStartupV2
        {
            get => _connectClientsOnStartupV2;
            set
            {
                _connectClientsOnStartupV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int ClientHbInterval
        {
            get => _clientHbInterval;
            set
            {
                _clientHbInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int ClientReconInterval
        {
            get => _clientReconInterval;
            set
            {
                _clientReconInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool UseLogAggregator
        {
            get => _useLogAggregator;
            set
            {
                _useLogAggregator = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string OtlpEndpoint
        {
            get => _logAggregatorUrl;
            set
            {
                _logAggregatorUrl = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool CheckForUpdateOnInterval
        {
            get => _checkForUpdateOnInterval;
            set
            {
                _checkForUpdateOnInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string AppUpdateUrl
        {
            get => _appUpdateUrl;
            set
            {
                _appUpdateUrl = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DefaultUiUpdateInterval
        {
            get => _defaultUiUpdateInterval;
            set
            {
                _defaultUiUpdateInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int MaxUpdateSize
        {
            get => _maxUpdateSize;
            set
            {
                _maxUpdateSize = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool IsDevMode
        {
            get => _isDevMode;
            set
            {
                _isDevMode = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public bool EnablePythonEngine
        {
            get => _enablePythonEngine;
            set
            {
                _enablePythonEngine = value;
                OnChange(value);
                NotifyPropertyChanged();
            }
        }

        public bool NotificationsForMyOrdersOnly
        {
            get => _notificationsForMyOrdersOnly;
            set
            {
                _notificationsForMyOrdersOnly = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderAckNotificationsV2
        {
            get => _showOrderAckNotificationsV2;
            set
            {
                _showOrderAckNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderPartialFillNotificationsV2
        {
            get => _showOrderPartialFillNotificationsV2;
            set
            {
                _showOrderPartialFillNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderFillNotificationsV2
        {
            get => _showOrderFillNotificationsV2;
            set
            {
                _showOrderFillNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderCancelNotificationsV2
        {
            get => _showOrderCancelNotificationsV2;
            set
            {
                _showOrderCancelNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderReplaceNotificationsV2
        {
            get => _showOrderReplaceNotificationsV2;
            set
            {
                _showOrderReplaceNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowOrderRejectNotificationsV2
        {
            get => _showOrderRejectNotificationsV2;
            set
            {
                _showOrderRejectNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowFirstEdgeNotificationsV2
        {
            get => _showFirstEdgeNotificationsV2;
            set
            {
                _showFirstEdgeNotificationsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowEdgeGiveUpNotificationV2
        {
            get => _showEdgeGiveUpNotificationV2;
            set
            {
                _showEdgeGiveUpNotificationV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double ShowFirstEdgeNotificationsThreshold
        {
            get => _showFirstEdgeNotificationsThreshold;
            set
            {
                _showFirstEdgeNotificationsThreshold = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowDuplicateRestingOrdersNotificationV2
        {
            get => _showDuplicateRestingOrdersNotificationV2;
            set
            {
                _showDuplicateRestingOrdersNotificationV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowTaggedOrdersNotificationV2
        {
            get => _showTaggedOrdersNotificationV2;
            set
            {
                _showTaggedOrdersNotificationV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DuplicateRestingOrdersNotificationCount
        {
            get => _showDuplicateRestingOrdersNotificationCount;
            set
            {
                _showDuplicateRestingOrdersNotificationCount = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DuplicateRestingOrdersNotificationPeriod
        {
            get => _showDuplicateRestingOrdersNotificationPeriod;
            set
            {
                _showDuplicateRestingOrdersNotificationPeriod = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowPopupOnRejectedOrder
        {
            get => _showPopupOnRejectedOrder;
            set
            {
                _showPopupOnRejectedOrder = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayNotificationSoundsInSequence
        {
            get => _playNotificationSoundsInSequence;
            set
            {
                _playNotificationSoundsInSequence = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderAckNotificationSoundV2
        {
            get => _playOrderAckNotificationSoundV2;
            set
            {
                _playOrderAckNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderPartialFillNotificationSoundV2
        {
            get => _playOrderPartialFillNotificationSoundV2;
            set
            {
                _playOrderPartialFillNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderFillNotificationSoundV2
        {
            get => _playOrderFillNotificationSoundV2;
            set
            {
                _playOrderFillNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderCancelNotificationSoundV2
        {
            get => _playOrderCancelNotificationSoundV2;
            set
            {
                _playOrderCancelNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderReplaceNotificationSoundV2
        {
            get => _playOrderReplaceNotificationSoundV2;
            set
            {
                _playOrderReplaceNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayOrderRejectNotificationSoundV2
        {
            get => _playOrderRejectNotificationSoundV2;
            set
            {
                _playOrderRejectNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayFirstEdgeNotificationSoundV2
        {
            get => _playFirstEdgeNotificationSoundV2;
            set
            {
                _playFirstEdgeNotificationSoundV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayEdgeGiveUpNotificationV2
        {
            get => _playEdgeGiveUpNotificationV2;
            set
            {
                _playEdgeGiveUpNotificationV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double PlayEdgeGiveUpNotificationThreshold
        {
            get => _playEdgeGiveUpNotificationThreshold;
            set
            {
                _playEdgeGiveUpNotificationThreshold = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string EdgeGiveUpNotificationSound
        {
            get => _edgeGiveUpNotificationSound;
            set
            {
                _edgeGiveUpNotificationSound = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PlayDuplicateRestingOrdersNotificationV2
        {
            get => _playDuplicateRestingOrdersNotificationV2;
            set
            {
                _playDuplicateRestingOrdersNotificationV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string DuplicateRestingOrdersNotificationSound
        {
            get => _duplicateRestingOrdersNotificationSound;
            set
            {
                _duplicateRestingOrdersNotificationSound = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double PlayFirstEdgeNotificationsThreshold
        {
            get => _playFirstEdgeNotificationSoundThreshold;
            set
            {
                _playFirstEdgeNotificationSoundThreshold = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string FirstEdgeNotificationSound
        {
            get => _firstEdgeNotificationSound;
            set
            {
                _firstEdgeNotificationSound = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double GlobalNotificationSettingsX
        {
            get => _globalNotificationSettingsX;
            set
            {
                _globalNotificationSettingsX = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double GlobalNotificationSettingsY
        {
            get => _globalNotificationSettingsY;
            set
            {
                _globalNotificationSettingsY = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool NewestNotificationOnTop
        {
            get => _newestNotificationOnTop;
            set
            {
                _newestNotificationOnTop = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int MaxDisplayedNotifications
        {
            get => _maxDisplayedNotifications;
            set
            {
                _maxDisplayedNotifications = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int RemoveNotificationTimerInterval
        {
            get => _removeNotificationTimerInterval;
            set
            {
                _removeNotificationTimerInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool EnablePriceTrackBar
        {
            get => _enablePriceTrackBar;
            set
            {
                _enablePriceTrackBar = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowQuickRoutes
        {
            get => _showQuickRoutes;
            set
            {
                _showQuickRoutes = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double AutoHedgeLimitDiff
        {
            get => _autoHedgeLimitDiff;
            set
            {
                _autoHedgeLimitDiff = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double AutoHedgeLimitIncrement
        {
            get => _autoHedgeLimitIncrement;
            set
            {
                _autoHedgeLimitIncrement = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MaxAutoHedgePercentV2
        {
            get => _maxAutoHedgePercentV2;
            set
            {
                _maxAutoHedgePercentV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MaxHedgeWidthV2
        {
            get => _maxHedgeWidthV2;
            set
            {
                _maxHedgeWidthV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool MaxAutoHedgePositionEnabled
        {
            get => _maxAutoHedgePositionEnabled;
            set
            {
                _maxAutoHedgePositionEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool MaxAutoHedgeNetCashEnabled
        {
            get => _maxAutoHedgeNetCashEnabled;
            set
            {
                _maxAutoHedgeNetCashEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool WarnAgainstDoubleFillOnCloseEnabled
        {
            get => _warnAgainstDoubleFillOnCloseEnabled;
            set
            {
                _warnAgainstDoubleFillOnCloseEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int MaxAutoHedgePosition
        {
            get => _maxAutoHedgePosition;
            set
            {
                _maxAutoHedgePosition = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MaxAutoHedgeNetCash
        {
            get => _maxAutoHedgeNetCash;
            set
            {
                _maxAutoHedgeNetCash = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AutoScrollOrderGrids
        {
            get => _autoScrollOrderGrids;
            set
            {
                _autoScrollOrderGrids = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AddNewRowsToTop
        {
            get => _addNewRowsToTop;
            set
            {
                _addNewRowsToTop = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AlwaysShowBuysOnTheLeft
        {
            get => _alwaysShowBuysOnTheLeft;
            set
            {
                _alwaysShowBuysOnTheLeft = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string OrderBookFilter
        {
            get => _orderBookFilter;
            set
            {
                _orderBookFilter = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowWorkingOrdersGrid
        {
            get => _showWorkingOrdersGrid;
            set
            {
                _showWorkingOrdersGrid = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DecimalPlacesForGreeks
        {
            get => _decimalPlacesForGreeks;
            set
            {
                _decimalPlacesForGreeks = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool TicketLockEdgeToTheoWhenLoadingFromOrderbook
        {
            get => _ticketLockEdgeToTheoWhenLoadingFromOrderbook;
            set
            {
                _ticketLockEdgeToTheoWhenLoadingFromOrderbook = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool TicketCancelOnPartialFill
        {
            get => _ticketCancelOnPartialFill;
            set
            {
                _ticketCancelOnPartialFill = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double DefaultContraEdge
        {
            get => _defaultContraEdge;
            set
            {
                _defaultContraEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double DefaultSpxContraEdge
        {
            get => _defaultSpxContraEdge;
            set
            {
                _defaultSpxContraEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double DefaultTheoEdge
        {
            get => _defaultTheoEdge;
            set
            {
                _defaultTheoEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double DefaultMidEdge
        {
            get => _defaultMidEdge;
            set
            {
                _defaultMidEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AllowCombinedSimultaneousOrders
        {
            get => _allowCombinedSimultaneousOrders;
            set
            {
                _allowCombinedSimultaneousOrders = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AutoReverseOrdersToBuySide
        {
            get => _autoReverseOrdersToBuySide;
            set
            {
                _autoReverseOrdersToBuySide = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool HaveContraBuysOnTheLeft
        {
            get => _haveContraBuysOnTheLeft;
            set
            {
                _haveContraBuysOnTheLeft = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int OrderBookGreekDecimalPlaces
        {
            get => _orderBookGreekDecimalPlaces;
            set
            {
                _orderBookGreekDecimalPlaces = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int OrderBookQuoteDecimalPlaces
        {
            get => _orderBookQuoteDecimalPlaces;
            set
            {
                _orderBookQuoteDecimalPlaces = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double OrderBookFontSize
        {
            get => _orderBookFontSize;
            set
            {
                _orderBookFontSize = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double PortfolioFontSize
        {
            get => _portfolioFontSize;
            set
            {
                _portfolioFontSize = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double PercentBidFontSize
        {
            get => _percentBidFontSize;
            set
            {
                _percentBidFontSize = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ProgressiveZoomEnabled
        {
            get => _progressiveZoomEnabled;
            set
            {
                _progressiveZoomEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string AuthServer
        {
            get => _authServer;
            set
            {
                _authServer = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int AuthServerPort
        {
            get => _authServerPort;
            set
            {
                _authServerPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string DominatorsManagerListenerAddress
        {
            get => _dominatorsManagerListenerAddress;
            set
            {
                _dominatorsManagerListenerAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DominatorsManagerListenerPort
        {
            get => _dominatorsManagerListenerPort;
            set
            {
                _dominatorsManagerListenerPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool DominatorsManagerListenerEnabled
        {
            get => _dominatorsManagerListenerEnabled;
            set
            {
                _dominatorsManagerListenerEnabled = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }
        public bool DominatorsManagerAutoTraderEnabled
        {
            get => _dominatorsManagerAutoTraderEnabled;
            set
            {
                _dominatorsManagerAutoTraderEnabled = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public string BasketManagerListenerAddress
        {
            get => _basketManagerListenerAddress;
            set
            {
                _basketManagerListenerAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int BasketManagerListenerPort
        {
            get => _basketManagerListenerPort;
            set
            {
                _basketManagerListenerPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketManagerListenerEnabledV2
        {
            get => _basketManagerListenerEnabledV2;
            set
            {
                _basketManagerListenerEnabledV2 = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public string DominatorsManagerOmsAddress
        {
            get => _dominatorsManagerOmsAddress;
            set
            {
                _dominatorsManagerOmsAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int DominatorsManagerOmsPort
        {
            get => _dominatorsManagerOmsPort;
            set
            {
                _dominatorsManagerOmsPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool DominatorClientEnabled
        {
            get => _dominatorClientEnable;
            set
            {
                _dominatorClientEnable = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public string BasketManagerOmsAddress
        {
            get => _basketManagerOmsAddress;
            set
            {
                _basketManagerOmsAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int BasketManagerOmsPort
        {
            get => _basketManagerOmsPort;
            set
            {
                _basketManagerOmsPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool EnableBasketManagerClientV2
        {
            get => _basketManagerClientEnableV2;
            set
            {
                _basketManagerClientEnableV2 = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }

        public string ExternalTradeDbAddress
        {
            get => _externalDbAddress;
            set
            {
                _externalDbAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string ExternalTradeDbUser
        {
            get => _externalDbUser;
            set
            {
                _externalDbUser = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string ExternalTradeDbPass
        {
            get => _externalDbPass;
            set
            {
                _externalDbPass = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int SubmitWithDelayIntervalMin
        {
            get => _submitWithDelayIntervalMin;
            set
            {
                _submitWithDelayIntervalMin = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool GetVerificationForBasketSubmitAllV2
        {
            get => _getVerificationForBasketSubmitAllV2;
            set
            {
                _getVerificationForBasketSubmitAllV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool GetVerificationForBasketReverse
        {
            get => _getVerificationForBasketReverse;
            set
            {
                _getVerificationForBasketReverse = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int MaxCancelOnLimitV2
        {
            get => _maxCancelOnLimitV2;
            set
            {
                _maxCancelOnLimitV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int MaxSimultaneousLoopsV2
        {
            get => _maxSimultaneousLoopsV2;
            set
            {
                _maxSimultaneousLoopsV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LoopDelayMin
        {
            get => _loopDelayMin;
            set
            {
                _loopDelayMin = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LoopDelayMax
        {
            get => _loopDelayMax;
            set
            {
                _loopDelayMax = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double BlockTraderPercentBid
        {
            get => _blockTraderPercentBid;
            set
            {
                _blockTraderPercentBid = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double BlockTraderModifyTimer
        {
            get => _blockTraderModifyTimer;
            set
            {
                _blockTraderModifyTimer = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string BlockTraderLayout
        {
            get => _blockTraderLayout;
            set
            {
                _blockTraderLayout = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool GlobalBasketRiskControlEnabledV2
        {
            get => _globalBasketRiskControlEnabledV2;
            set
            {
                _globalBasketRiskControlEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool GlobalTicketRiskControlEnabledV2
        {
            get => _globalTicketRiskControlEnabledV2;
            set
            {
                _globalTicketRiskControlEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string WorkspaceTitle
        {
            get => _workspaceTitle;
            set
            {
                _workspaceTitle = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool DeltaAdjPxEnabledOnTickets
        {
            get => _deltaAdjPxEnabledOnTickets;
            set
            {
                _deltaAdjPxEnabledOnTickets = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool UseDeltaAdjTheoForThreeWay
        {
            get => _useDeltaAdjTheoForThreeWay;
            set
            {
                _useDeltaAdjTheoForThreeWay = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public ThreeWayPreference ThreeWayPreference
        {
            get => _threeWayPreference;
            set
            {
                _threeWayPreference = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public OrderType HedgeOrderType
        {
            get => _hedgeOrderType;
            set
            {
                _hedgeOrderType = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public DepthColorType DepthColorType
        {
            get => _depthColorType;
            set
            {
                _depthColorType = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int HedgeInterval
        {
            get => _hedgeInterval;
            set
            {
                _hedgeInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AutoFlattenHedgeV2
        {
            get => _autoFlattenHedgeV2;
            set
            {
                _autoFlattenHedgeV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AutoHedgeWhenAddingPositionToHedgedSpread
        {
            get => _autoHedgeWhenAddingPositionToHedgedSpread;
            set
            {
                _autoHedgeWhenAddingPositionToHedgedSpread = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool InterpolationEnabled
        {
            get => _interpolationEnabled;
            set
            {
                _interpolationEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool UsePercentageForDeltaNotion
        {
            get => _usePercentageForDeltaNotion;
            set
            {
                _usePercentageForDeltaNotion = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public OrderTicketStyle DefaultOrderTicketStyle
        {
            get => _defaultOrderTicketStyle;
            set
            {
                _defaultOrderTicketStyle = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public PriceEvaluationStyle PriceEvaluationStyle
        {
            get => _priceEvaluationStyle;
            set
            {
                _priceEvaluationStyle = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public string LockTraderTif
        {
            get => _lockTraderTif;
            set
            {
                _lockTraderTif = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LockTraderQtyMin
        {
            get => _lockTraderQtyMin;
            set
            {
                _lockTraderQtyMin = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int LockTraderQtyMax
        {
            get => _lockTraderQtyMax;
            set
            {
                _lockTraderQtyMax = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double LockTraderPriceMin
        {
            get => _lockTraderPriceMin;
            set
            {
                _lockTraderPriceMin = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double LockTraderPriceMax
        {
            get => _lockTraderPriceMax;
            set
            {
                _lockTraderPriceMax = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public List<object> LockTraderAllowedStrategies
        {
            get => _lockTraderAllowedStrategies;
            set
            {
                _lockTraderAllowedStrategies = value;
                OnChange(false, notify: false);
                NotifyPropertyChanged();
            }
        }

        public bool TicketCancelTimerEnabledV2
        {
            get => _ticketCancelTimerEnabledV2;
            set
            {
                _ticketCancelTimerEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool StockTicketCancelTimerEnabledV2
        {
            get => _stockTicketCancelTimerEnabledV2;
            set
            {
                _stockTicketCancelTimerEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketCancelTimerEnabledV2
        {
            get => _basketCancelTimerEnabledV2;
            set
            {
                _basketCancelTimerEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double SingleLegCancelTimerDefaultIntervalV2
        {
            get => _singleLegCancelTimerDefaultIntervalV2;
            set
            {
                _singleLegCancelTimerDefaultIntervalV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double SpreadCancelTimerDefaultIntervalV2
        {
            get => _spreadCancelTimerDefaultIntervalV2;
            set
            {
                _spreadCancelTimerDefaultIntervalV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ForThreeWayTicketsUseThreeWayFlat
        {
            get => _forThreeWayTicketsUseThreeWayFlat;
            set
            {
                _forThreeWayTicketsUseThreeWayFlat = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool LockDeltaAdjPriceWhenLoadingTickets
        {
            get => _lockDeltaAdjPriceWhenLoadingTickets;
            set
            {
                _lockDeltaAdjPriceWhenLoadingTickets = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double SmartRouteOverwatchTimer
        {
            get => _smartRouteOverwatchTimer;
            set
            {
                _smartRouteOverwatchTimer = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool TicketAlwaysOnTop
        {
            get => _ticketAlwaysOnTop;
            set
            {
                _ticketAlwaysOnTop = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool DoNotStackUpTickets
        {
            get => _doNotStackUpTickets;
            set
            {
                _doNotStackUpTickets = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowFirstEdgeForThisSessionOnly
        {
            get => _showFirstEdgeForThisSessionOnly;
            set
            {
                _showFirstEdgeForThisSessionOnly = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ActiveUncheckEnabled
        {
            get => _activeUncheckEnabled;
            set
            {
                _activeUncheckEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketDeltaAdjLastFillPx
        {
            get => _basketDeltaAdjLastFillPx;
            set
            {
                _basketDeltaAdjLastFillPx = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int ActiveUncheckQty
        {
            get => _activeUncheckQty;
            set
            {
                _activeUncheckQty = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double ActiveUncheckEdge
        {
            get => _activeUncheckEdge;
            set
            {
                _activeUncheckEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double AutoPermTradeLookback
        {
            get => _autoPermTradeLookback;
            set
            {
                _autoPermTradeLookback = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double RecentTradeLookback
        {
            get => _recentTradeLookback;
            set
            {
                _recentTradeLookback = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseButtonEdge
        {
            get => _closeButtonEdge;
            set
            {
                _closeButtonEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseButtonPxIncrement
        {
            get => _closeButtonPxIncrement;
            set
            {
                _closeButtonPxIncrement = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int CloseButtonInterval
        {
            get => _closeButtonInterval;
            set
            {
                _closeButtonInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public CloseFastEdgeType CloseFastEdgeType
        {
            get => _closeFastEdgeType;
            set
            {
                _closeFastEdgeType = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseFastButtonEdgePercentageMin
        {
            get => _closeFastButtonEdgePercentageMin;
            set
            {
                _closeFastButtonEdgePercentageMin = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseFastButtonEdgePercentageMax
        {
            get => _closeFastButtonEdgePercentageMax;
            set
            {
                _closeFastButtonEdgePercentageMax = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseFastButtonEdge
        {
            get => _closeFastButtonEdge;
            set
            {
                _closeFastButtonEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double CloseFastButtonPxIncrement
        {
            get => _closeFastButtonPxIncrement;
            set
            {
                _closeFastButtonPxIncrement = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int CloseFastButtonInterval
        {
            get => _closeFastButtonInterval;
            set
            {
                _closeFastButtonInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MarketMakerMaxPxOffsetPercent
        {
            get => _marketMakerMaxPxOffsetPercent;
            set
            {
                _marketMakerMaxPxOffsetPercent = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MarketMakerMaxAwayFromMarketPercent
        {
            get => _marketMakerMaxAwayFromMarketPercent;
            set
            {
                _marketMakerMaxAwayFromMarketPercent = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double BidPercentLimit
        {
            get => _bidPercentLimit;
            set
            {
                _bidPercentLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MinimumBidPercentLimit
        {
            get => _minimumBidPercentLimit;
            set
            {
                _minimumBidPercentLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MinimumEmaAndBidEdge
        {
            get => _minimumEmaAndBidEdge;
            set
            {
                _minimumEmaAndBidEdge = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MinimumEdgeToTheoLimitV2
        {
            get => _minimumEdgeToTheoLimitV2;
            set
            {
                _minimumEdgeToTheoLimitV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double MinimumVolTraderEdgeLimit
        {
            get => _minimumVolTraderEdgeLimit;
            set
            {
                _minimumVolTraderEdgeLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketDeltaLimitEnabledV2
        {
            get => _basketDeltaLimitEnabledV2;
            set
            {
                _basketDeltaLimitEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double BasketDeltaLimitV2
        {
            get => _basketDeltaLimitV2;
            set
            {
                _basketDeltaLimitV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketLongPositionLimitEnabled
        {
            get => _basketLongPositionLimitEnabled;
            set
            {
                _basketLongPositionLimitEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int BasketLongPositionLimit
        {
            get => _basketLongPositionLimit;
            set
            {
                _basketLongPositionLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool BasketShortPositionLimitEnabled
        {
            get => _basketShortPositionLimitEnabled;
            set
            {
                _basketShortPositionLimitEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int BasketShortPositionLimit
        {
            get => _basketShortPositionLimit;
            set
            {
                _basketShortPositionLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool PushUpdatesAsync
        {
            get => _pushUpdatesAsync;
            set
            {
                _pushUpdatesAsync = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool LogBasketValues
        {
            get => _logBasketValues;
            set
            {
                _logBasketValues = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double TicketUiUpdateInterval
        {
            get => _ticketUiUpdateInterval;
            set
            {
                _ticketUiUpdateInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double BasketUiUpdateInterval
        {
            get => _basketUiUpdateInterval;
            set
            {
                _basketUiUpdateInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool RestApiEnabled
        {
            get => _restApiEnabled;
            set
            {
                _restApiEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string RestApiAddress
        {
            get => _restApiAddress;
            set
            {
                _restApiAddress = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int RestApiPort
        {
            get => _restApiPort;
            set
            {
                _restApiPort = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string AudioAlertSound
        {
            get => _audioAlertSound;
            set
            {
                _audioAlertSound = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AudioAlertEnabled
        {
            get => _audioAlertEnabled;
            set
            {
                _audioAlertEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool VisualAlertEnabled
        {
            get => _visualAlertEnabled;
            set
            {
                _visualAlertEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool TtsAlertEnabled
        {
            get => _ttsEnabled;
            set
            {
                _ttsEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool AlertEnabled
        {
            get => _alertEnabled;
            set
            {
                _alertEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int AlertMinQty
        {
            get => _alertMinQty;
            set
            {
                _alertMinQty = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int AlertThreshold
        {
            get => _alertThreshold;
            set
            {
                _alertThreshold = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int SnoozeThreshold
        {
            get => _snoozeThreshold;
            set
            {
                _snoozeThreshold = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UpdateOnlySyncAdjTheo
        {
            get => _updateOnlySyncAdjTheo;
            set
            {
                _updateOnlySyncAdjTheo = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UpdateOnlySyncEma
        {
            get => _updateOnlySyncEma;
            set
            {
                _updateOnlySyncEma = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double AdjustedEdgeSummaryLookback
        {
            get => _adjustedEdgeSummaryLookback;
            set
            {
                _adjustedEdgeSummaryLookback = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double AdjustedEdgeSummaryUnderPercentage
        {
            get => _adjustedEdgeSummaryUnderPercentage;
            set
            {
                _adjustedEdgeSummaryUnderPercentage = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int TwsClientId
        {
            get => _twsClientId;
            set
            {
                _twsClientId = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EnableTwsApi
        {
            get => _enableTwsApi;
            set
            {
                _enableTwsApi = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public TwsMarketDataType TwsMarketDataType
        {
            get => _twsMarketDataType;
            set
            {
                _twsMarketDataType = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool OrderbookIdentifyEdgeScanTrades
        {
            get => _orderbookIdentifyEdgeScanTrades;
            set
            {
                _orderbookIdentifyEdgeScanTrades = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }
        public bool PromptToLoadEdgeOverrideWhenDraggingToBasket
        {
            get => _promptToLoadEdgeOverrideWhenDraggingToBasket;
            set
            {
                _promptToLoadEdgeOverrideWhenDraggingToBasket = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }
        public bool AllowDeltaHedging
        {
            get => _allowDeltaHedging;
            set
            {
                _allowDeltaHedging = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }
        public bool SilentTicketNotifications
        {
            get => _silentTicketNotifications;
            set
            {
                _silentTicketNotifications = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AllowUserPositionTracking
        {
            get => _allowUserPositionTracking;
            set
            {
                _allowUserPositionTracking = value;
                OnChange(true);
                NotifyPropertyChanged();
            }
        }
        public bool EnablePermAdjPx
        {
            get => _enablePermAdjPx;
            set
            {
                _enablePermAdjPx = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UseOrderTicketForSingleLegOrders
        {
            get => _useOrderTicketForSingleLegOrders;
            set
            {
                _useOrderTicketForSingleLegOrders = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UseSignedValueForSpreadVegaFilters
        {
            get => _useSignedValueForSpreadVegaFilters;
            set
            {
                _useSignedValueForSpreadVegaFilters = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool BlockDuplicateByTimeV2
        {
            get => _blockDuplicateByTimeV2;
            set
            {
                _blockDuplicateByTimeV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int BlockDuplicateThresholdV2
        {
            get => _blockDuplicateThresholdV2;
            set
            {
                _blockDuplicateThresholdV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AllowLockingMarketPrices
        {
            get => _allowLockingMarketPrices;
            set
            {
                _allowLockingMarketPrices = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EnableDeltaAdjustedEma
        {
            get => _enableDeltaAdjustedEma;
            set
            {
                _enableDeltaAdjustedEma = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double EmaSmoothing
        {
            get => _emaSmoothing;
            set
            {
                _emaSmoothing = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int EmaInterval
        {
            get => _emaInterval;
            set
            {
                _emaInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int EmaPeriods
        {
            get => _emaPeriods;
            set
            {
                _emaPeriods = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double OrderDelayForBlocking
        {
            get => _orderDelayForBlocking;
            set
            {
                _orderDelayForBlocking = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public double OrderDelayBlockingDelay
        {
            get => _orderDelayBlockingDelay;
            set
            {
                _orderDelayBlockingDelay = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public int StopLossMaxAttempt
        {
            get => _stopLossMaxAttempt;
            set
            {
                _stopLossMaxAttempt = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ForCrossPriceUseRouteEnabled
        {
            get => _forCrossPriceUseRouteEnabled;
            set
            {
                _forCrossPriceUseRouteEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double RecalculateBasketPriceInterval
        {
            get => _recalculateBasketPriceInterval;
            set
            {
                _recalculateBasketPriceInterval = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double BasketItemsTimeToLiveForAutoClear
        {
            get => _basketItemsTimeToLiveForAutoClear;
            set
            {
                _basketItemsTimeToLiveForAutoClear = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowBasketConfigsInOrderbook
        {
            get => _showBasketConfigsInOrderbook;
            set
            {
                _showBasketConfigsInOrderbook = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowNagBotButtonInOrderbookV2
        {
            get => _showNagBotButtonInOrderbookV2;
            set
            {
                _showNagBotButtonInOrderbookV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowBasketQuickLayoutPanel
        {
            get => _showBasketQuickLayoutPanel;
            set
            {
                _showBasketQuickLayoutPanel = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int LoopMaxResubmitWithIncrement
        {
            get => _loopMaxResubmitWithIncrement;
            set
            {
                _loopMaxResubmitWithIncrement = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool LoopMaxResubmitWithIncrementCheckEnabled
        {
            get => _loopMaxResubmitWithIncrementCheckEnabled;
            set
            {
                _loopMaxResubmitWithIncrementCheckEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AllowSavingEdgeScanFeedBasketsWithWorkspace
        {
            get => _allowSavingEdgeScanFeedBasketsWithWorkspace;
            set
            {
                _allowSavingEdgeScanFeedBasketsWithWorkspace = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string DefaultHedgeHouseRouteRegular
        {
            get => _defaultHedgeHouseRouteRegular;
            set
            {
                _defaultHedgeHouseRouteRegular = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string DefaultHedgeHouseRouteAutoTraderV2
        {
            get => _defaultHedgeHouseRouteAutoTraderV2;
            set
            {
                _defaultHedgeHouseRouteAutoTraderV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string DefaultHedgeHouseSpreadRoute
        {
            get => _defaultHedgeHouseSpreadRoute;
            set
            {
                _defaultHedgeHouseSpreadRoute = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string DefaultHedgeHouseSingleLegRoute
        {
            get => _defaultHedgeHouseSingleLegRoute;
            set
            {
                _defaultHedgeHouseSingleLegRoute = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int HedgeHouseResubmitCount
        {
            get => _hedgeHouseResubmitCount;
            set
            {
                _hedgeHouseResubmitCount = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double PriceCacheClearIntervalMs
        {
            get => _priceCacheClearIntervalMs;
            set
            {
                _priceCacheClearIntervalMs = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool PriceCacheClearIntervalEnabled
        {
            get => _priceCacheClearIntervalEnabled;
            set
            {
                _priceCacheClearIntervalEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int BasketHedgeHouseMaxQtyV2
        {
            get => _basketHedgeHouseMaxQtyV2;
            set
            {
                _basketHedgeHouseMaxQtyV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double BasketHedgeHouseMaxNotionalV2
        {
            get => _basketHedgeHouseMaxNotionalV2;
            set
            {
                _basketHedgeHouseMaxNotionalV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool OpenSeparateTicketForUnderlying
        {
            get => _openSeparateTicketForUnderlying;
            set
            {
                _openSeparateTicketForUnderlying = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool TestValueSubscriptionEnabled
        {
            get => _testValueSubscriptionEnabled;
            set
            {
                _testValueSubscriptionEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public SubscriptionFieldType TestValueSubscriptionFieldType
        {
            get => _testValueSubscriptionFieldType;
            set
            {
                _testValueSubscriptionFieldType = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowNegativeEdgeWarning
        {
            get => _showNegativeEdgeWarning;
            set
            {
                _showNegativeEdgeWarning = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EnableEmbeddedLogging
        {
            get => _enableEmbeddedLogging;
            set
            {
                _enableEmbeddedLogging = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int MaxPosForDualTicket
        {
            get => _maxPosForDualTicket;
            set
            {
                _maxPosForDualTicket = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool WrapColumnHeaderV2
        {
            get => _wrapColumnHeaderV2;
            set
            {
                _wrapColumnHeaderV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool RemoveAdjustedOptions
        {
            get => _removeAdjustedOptions;
            set
            {
                _removeAdjustedOptions = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public string LayoutDefaultDateTimeColumnFormat
        {
            get => _layoutDefaultDateTimeColumnFormat;
            set
            {
                _layoutDefaultDateTimeColumnFormat = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public double TheoMisMatchResetIntervalSec
        {
            get => _theoMisMatchResetIntervalSec;
            set
            {
                _theoMisMatchResetIntervalSec = value;
                OnChange(false);
            }
        }
        public double MaxStatusDelaySeconds
        {
            get => _maxStatusDelaySeconds;
            set
            {
                _maxStatusDelaySeconds = value;
                OnChange(false);
            }
        }
        public bool WarnAgainstLargeSizeUpConfigV2
        {
            get => _warnAgainstLargeSizeUpConfigV2;
            set
            {
                _warnAgainstLargeSizeUpConfigV2 = value;
                OnChange(false);
            }
        }
        public int WarnAgainstLargeSizeUpQty
        {
            get => _warnAgainstLargeSizeUpQty;
            set
            {
                _warnAgainstLargeSizeUpQty = value;
                OnChange(false);
            }
        }
        public bool ShowOrderRateAlertV2
        {
            get => _showOrderRateAlertV2;
            set
            {
                _showOrderRateAlertV2 = value;
                OnChange(false);
            }
        }
        public bool RemoveDuplicateColumnsFromLayout
        {
            get => _removeDuplicateColumnsFromLayout;
            set
            {
                _removeDuplicateColumnsFromLayout = value;
                OnChange(false);
            }
        }
        public bool SubscribeToHardSideIdentification
        {
            get => _subscribeToHardSideIdentification;
            set
            {
                _subscribeToHardSideIdentification = value;
                OnChange(false);
            }
        }
        public bool SubscribeToHardSideIdentificationOnTickets
        {
            get => _subscribeToHardSideIdentificationOnTickets;
            set
            {
                _subscribeToHardSideIdentificationOnTickets = value;
                OnChange(false);
            }
        }
        public bool UseMoneynessForOppCpInTicketsV2
        {
            get => _useMoneynessForOppCpInTicketsV2;
            set
            {
                _useMoneynessForOppCpInTicketsV2 = value;
                OnChange(false);
            }
        }
        public bool UseMoneynessForOppCpInBasketsV2
        {
            get => _useMoneynessForOppCpInBasketsV2;
            set
            {
                _useMoneynessForOppCpInBasketsV2 = value;
                OnChange(false);
            }
        }

        public string BasketBorderKeyGesture
        {
            get => _basketBorderKeyGesture;
            set
            {
                _basketBorderKeyGesture = value;
                OnChange(false);
            }
        }

        public string SinglesLegBorderColor
        {
            get => _singlesLegBorderColor;
            set
            {
                _singlesLegBorderColor = value;
                OnChange(false);
            }
        }


        public string MultiLegBorderColor
        {
            get => _multiLegBorderColor;
            set
            {
                _multiLegBorderColor = value;
                OnChange(false);
            }
        }


        public string MixedBorderColor
        {
            get => _mixedBorderColor;
            set
            {
                _mixedBorderColor = value;
                OnChange(false);
            }
        }

        public int UndoCapacity
        {
            get => _undoCapacity;
            set
            {
                _undoCapacity = value;
                OnChange(false);
            }
        }
        public string LoLaAccount
        {
            get => _loLaAccount;
            set
            {
                _loLaAccount = value;
                OnChange(false);
            }
        }
        public string LoLaFdid
        {
            get => _loLaFdid;
            set
            {
                _loLaFdid = value;
                OnChange(false);
            }
        }
        public bool ShowEodRiskV2
        {
            get => _showEodRiskV2;
            set
            {
                _showEodRiskV2 = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightEdge
        {
            get => _basketAutomationHighlightEdge;
            set
            {
                _basketAutomationHighlightEdge = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightMinEdge
        {
            get => _basketAutomationHighlightMinEdge;
            set
            {
                _basketAutomationHighlightMinEdge = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightSizeup
        {
            get => _basketAutomationHighlightSizeup;
            set
            {
                _basketAutomationHighlightSizeup = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightMaxLoss
        {
            get => _basketAutomationHighlightMaxLoss;
            set
            {
                _basketAutomationHighlightMaxLoss = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightPxInc
        {
            get => _basketAutomationHighlightPxInc;
            set
            {
                _basketAutomationHighlightPxInc = value;
                OnChange(false);
            }
        }
        public bool BasketAutomationHighlightMaxLoop
        {
            get => _basketAutomationHighlightMaxLoop;
            set
            {
                _basketAutomationHighlightMaxLoop = value;
                OnChange(false);
            }
        }
        public bool MaintainBaseStrategyExceptionForFlyEnabled
        {
            get => _maintainBaseStrategyExceptionForFlyEnabled;
            set
            {
                _maintainBaseStrategyExceptionForFlyEnabled = value;
                OnChange(false);
            }
        }
        public bool UseServerSidePerming
        {
            get => _useServerSidePerming;
            set
            {
                _useServerSidePerming = value;
                OnChange(false);
            }
        }
        public bool TrackOrdersBySpreadV2
        {
            get => _trackOrdersBySpreadV2;
            set
            {
                _trackOrdersBySpreadV2 = value;
                OnChange(false);
            }
        }
        public bool CancelRestingOrdersOnCombinedTickets
        {
            get => _cancelRestingOrdersOnCombinedTickets;
            set
            {
                _cancelRestingOrdersOnCombinedTickets = value;
                OnChange(false);
            }
        }
        public bool ShowEdgeRiskWarningOnDualTickets
        {
            get => _showEdgeRiskWarningOnDualTickets;
            set
            {
                _showEdgeRiskWarningOnDualTickets = value;
                OnChange(false);
            }
        }
        public int SpreadGeneratorTimeout
        {
            get => _spreadGeneratorTimeout;
            set
            {
                _spreadGeneratorTimeout = value;
                OnChange(false);
            }
        }
        public bool SpreadGeneratorUseGlobalTimeout
        {
            get => _spreadGeneratorUseGlobalTimeout;
            set
            {
                _spreadGeneratorUseGlobalTimeout = value;
                OnChange(false);
            }
        }
        public int MaxOrderSubmissionPerSecondLimit
        {
            get => _maxOrderSubmissionPerSecondLimit;
            set
            {
                _maxOrderSubmissionPerSecondLimit = value;
                OnChange(false);
            }
        }
        public string SavedAccount
        {
            get => _savedAccount; set
            {
                _savedAccount = value;
                OnChange(false);
            }
        }
        public string SavedBroker
        {
            get => _savedBroker;
            set
            {
                _savedBroker = value;
                OnChange(false);
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(DefaultBroker));
            }
        }
        public bool ConfigReset_260518
        {
            get => _configReset;
            set
            {
                _configReset = value;
                OnChange(false);
            }
        }
        public InstanceMode InstanceModeV3
        {
            get => _instanceModeV3;
            set
            {
                _instanceModeV3 = value;
                OnChange(false);
            }
        }

        public bool PerformanceModeEnabled
        {
            get => _perforamnceModeEnabled;
            set
            {
                _perforamnceModeEnabled = value;
                // Notifying the PerformanceModeManager via NotifyPropertyChanged, so we don't need to also trigger the ConfigChangedEvent
                OnChange(false, false);
                NotifyPropertyChanged();
            }
        }

        public bool UseCommonDispatcherForBaskets
        {
            get => _useCommonDispatcherForBaskets;
            set
            {
                _useCommonDispatcherForBaskets = value;
                OnChange(true);
            }
        }
        public bool UseSkewAdjustedHiBidLoAskForSpreadCalc
        {
            get => _useSkewAdjustedHiBidLoAskForSpreadCalc;
            set
            {
                _useSkewAdjustedHiBidLoAskForSpreadCalc = value;
                OnChange(false);
            }
        }
        public bool ShowCobOnTicketsV2
        {
            get => _showCobOnTicketsV2;
            set
            {
                _showCobOnTicketsV2 = value;
                OnChange(false);
            }
        }
        public int AutoCancelMaxResubmit
        {
            get => _autoCancelMaxResubmit;
            set
            {
                _autoCancelMaxResubmit = value;
                OnChange(false);
            }
        }
        public ColorTheme ColorTheme
        {
            get => _colorTheme;
            set
            {
                _colorTheme = value;
                OnChange(true);
            }
        }
        public bool LogAutoCancelAndFishLossPass
        {
            get => _logAutoCancelAndFishLossPass;
            set
            {
                _logAutoCancelAndFishLossPass = value;
                OnChange(true);
            }
        }
        public bool LockAutoTraderVenue
        {
            get => _lockAutoTraderVenue;
            set
            {
                _lockAutoTraderVenue = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public PositionSubscriptionMode PositionSubscriptionMode
        {
            get => _positionSubscriptionMode;
            set
            {
                _positionSubscriptionMode = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int BasketAlertTimeout
        {
            get => _BasketAlertTimeout;
            set
            {
                _BasketAlertTimeout = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AllowUsingOfStaticPriceInBaskets
        {
            get => _allowUsingOfStaticPriceInBaskets;
            set
            {
                _allowUsingOfStaticPriceInBaskets = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UseFirmNetQtyForFlat
        {
            get => _useFirmNetQtyForFlat;
            set
            {
                _useFirmNetQtyForFlat = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool InterpolatorClientEnabled
        {
            get => _interpolatorClientEnabled;
            set
            {
                _interpolatorClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool TheosClientEnabled
        {
            get => _theosClientEnabled;
            set
            {
                _theosClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool HubTronClientEnabled
        {
            get => _hubTronClientEnabled;
            set
            {
                _hubTronClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool TransactionClientEnabled
        {
            get => _transactionClientEnabled; set
            {
                _transactionClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool OrderClientEnabled
        {
            get => _orderClientEnabled; set
            {
                _orderClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AutoTraderClientEnabled
        {
            get => _autoTraderClientEnabled; set
            {
                _autoTraderClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool AutoTraderDirectClientEnabled
        {
            get => _autoTraderDirectClientEnabled; set
            {
                _autoTraderDirectClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool PositionClientEnabled
        {
            get => _positionClientEnabled; set
            {
                _positionClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool QuoteClientEnabled
        {
            get => _quoteClientEnabled; set
            {
                _quoteClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool RequestClientEnabled
        {
            get => _requestClientEnabled; set
            {
                _requestClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool HanweckClientEnabled
        {
            get => _hanweckClientEnabled; set
            {
                _hanweckClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EdgeScannerClientEnabled
        {
            get => _edgeScannerClientEnabled; set
            {
                _edgeScannerClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EdgeScanFeedRunnerClientEnabled
        {
            get => _edgeScanFeedRunnerClientEnabled; set
            {
                _edgeScanFeedRunnerClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool SymbolMapClientEnabled
        {
            get => _symbolMapClientEnabled; set
            {
                _symbolMapClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool TelemetryClientEnabled
        {
            get => _telemetryClientEnabled; set
            {
                _telemetryClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public byte TelemetryBoxId
        {
            get => _telemetryBoxId; set
            {
                _telemetryBoxId = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        [XmlIgnore]
        public byte TelemetryProgId => 11;
        public byte TelemetryInstanceId
        {
            get => _telemetryInstanceId; set
            {
                _telemetryInstanceId = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool EmaClientEnabled
        {
            get => _emaClientEnabled; set
            {
                _emaClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool DaEmaClientEnabled
        {
            get => _daEmaClientEnabled; set
            {
                _daEmaClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool IbGatewayClientEnabled
        {
            get => _ibGatewayClientEnabled; set
            {
                _ibGatewayClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool DatabentoClientEnabled
        {
            get => _databentoClientEnabled; set
            {
                _databentoClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool CobClientEnabled
        {
            get => _cobClientEnabled; set
            {
                _cobClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool PricingClientEnabledV2
        {
            get => _PricingClientEnabledV2; set
            {
                _PricingClientEnabledV2 = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool DerivativesClientEnabled
        {
            get => _derivativesClientEnabled; set
            {
                _derivativesClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool LiveVolDataClientEnabled
        {
            get => _liveVolDataClientEnabled; set
            {
                _liveVolDataClientEnabled = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        public bool ShowInitQtyWarning
        {
            get => _showInitQtyWarning;
            set
            {
                _showInitQtyWarning = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool UseSmartModuleTitleForBaskets
        {
            get => _useSmartModuleTitleForBaskets;
            set
            {
                _useSmartModuleTitleForBaskets = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool RouteOpsOrdersToAutoTraderDirect
        {
            get => _routeOpsOrdersToAutoTraderDirect;
            set
            {
                _routeOpsOrdersToAutoTraderDirect = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowLockTheosShortcutInBasketHeader
        {
            get => _showLockTheosShortcutInBasketHeader;
            set
            {
                _showLockTheosShortcutInBasketHeader = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public bool ShowProminentBasketPermButtons
        {
            get => _showProminentBasketPermButtons;
            set
            {
                _showProminentBasketPermButtons = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }
        public int SpreadGeneratorPromptLimit
        {
            get => _spreadGeneratorPromptLimit; set
            {
                _spreadGeneratorPromptLimit = value;
                OnChange(false);
                NotifyPropertyChanged();
            }
        }

        [XmlIgnore]
        public List<FilterModel> TradeFilters { get; set; } = new();
        [XmlIgnore]
        public HashSet<string> NonAutoCancelRoutes { get; } = new()
        {
            "EXCH_ROLL",
            "EXCH_ROLL_S",
            "EXCH_ROLL_SR",
        };
        [XmlIgnore]
        public AccountConfigModel AccountConfig
        {
            get => _accountConfig;
            set
            {
                if (value == null && _accountConfig != null && AccountConfigs != null && AccountConfigs.Contains(_accountConfig))
                {
                    NotifyPropertyChanged();
                    return;
                }
                _accountConfig = value;
                SavedAccount = value?.Account;
                SavedBroker = value?.DefaultBroker;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(DefaultBroker));
            }
        }
        [XmlIgnore]
        public List<AccountConfigModel> AccountConfigs { get; set; } = new List<AccountConfigModel>();

        [XmlIgnore]
        public List<FavoriteModuleGroupModel> FavoriteModules { get; set; } = new List<FavoriteModuleGroupModel>();

        [XmlIgnore]
        public ObservableCollection<UnderlyingRiskModel> UnderlyingRiskSettings { get; set; } = new ObservableCollection<UnderlyingRiskModel>();

        [XmlIgnore]
        public List<string> AvailableBrokers { get; private set; } = new List<string>();

        [XmlIgnore]
        public string DefaultAccount => AccountConfig?.Account;

        [XmlIgnore]
        public string DefaultBroker
        {
            get => _savedBroker ?? AccountConfig?.DefaultBroker;
            set
            {
                if (_accountConfig != null)
                {
                    _accountConfig.DefaultBroker = value;
                }
                SavedBroker = value;
            }
        }

        public void SetAvailableBrokers(IEnumerable<string> brokers)
        {
            AvailableBrokers = brokers?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? new List<string>();
            NotifyPropertyChanged(nameof(AvailableBrokers));
        }

        [XmlIgnore]
        public List<Tuple<int, string, ConfigSave>> SavedBasketQuickAccessLayouts { get; set; } = new();
        [XmlIgnore]
        public List<Tuple<int, string, LegTypes, Strategy, ConfigSave>> SavedBasketDefaultLayouts { get; set; } = new();
        [XmlIgnore]
        public Dictionary<string, List<PermOperationMode>> CustomPermCombinations { get; private set; } = new Dictionary<string, List<PermOperationMode>>();
        [XmlIgnore]
        public Dictionary<string, Tuple<string, double, double, int>> TicketStopLossLookupMap { get; private set; } = new Dictionary<string, Tuple<string, double, double, int>>();
        [XmlIgnore]
        public static ConcurrentDictionary<string, UnderlyingRiskModel> UnderlyingRiskModelLookup { get; private set; }
        [XmlIgnore]
        public HashSet<string> LowLatencyAccounts { get; } = new() { "AOSZERO" };
        [XmlIgnore]
        public string LowLatencyHungRoute { get; set; } = "TEXCH_ROLL_S";
        [XmlIgnore]
        public IHerculesClientConfig HerculesClientConfig { get; private set; }
        [XmlIgnore]
        public IHerculesClientConfigParser HerculesClientConfigParser { get; private set; }
        [XmlIgnore]
        public IRaptorClientConfig RaptorClientConfig { get; private set; }
        [XmlIgnore]
        public IRaptorClientConfigParser RaptorClientConfigParser { get; private set; }
        [XmlIgnore]
        public IEdgeScannerClientConfig EdgeScannerClientConfig { get; private set; }
        [XmlIgnore]
        public IEdgeScannerClientConfigParser EdgeScannerClientConfigParser { get; private set; }
        [XmlIgnore]
        public IEdgeScanFeedRunnerClientConfig EdgeScanFeedRunnerClientConfig { get; private set; }
        [XmlIgnore]
        public IEdgeScanFeedRunnerClientConfigParser EdgeScanFeedRunnerClientConfigParser { get; private set; }
        [XmlIgnore]
        public ISymbolMapClientConfig SymbolMapClientConfig { get; private set; }
        [XmlIgnore]
        public ISymbolMapClientConfigParser SymbolMapClientConfigParser { get; private set; }
        [XmlIgnore]
        public ITelemetryClientConfig TelemetryClientConfig { get; private set; }
        [XmlIgnore]
        public ITelemetryClientConfigParser TelemetryClientConfigParser { get; private set; }
        [XmlIgnore]
        public ITradesClientConfig TradesClientConfig { get; private set; }
        [XmlIgnore]
        public ITradesClientConfigParser TradesClientConfigParser { get; private set; }
        [XmlIgnore]
        public IEmaClientConfig EmaClientConfig { get; private set; }
        [XmlIgnore]
        public IEmaClientConfigParser EmaClientConfigParser { get; private set; }
        [XmlIgnore]
        public EmaServerConfig.EmaServerConfigParser EmaServerConfigParser { get; private set; }
        [XmlIgnore]
        public EMAServer.Client.IConfig EmaServerClientConfig { get; private set; }
        [XmlIgnore]
        public IInterpolatorClientConfig InterpolatorClientConfig { get; private set; }
        [XmlIgnore]
        public IInterpolatorClientConfigParser InterpolatorClientConfigParser { get; private set; }
        [XmlIgnore]
        public ITheosClientConfig TheosClientConfig { get; private set; }
        [XmlIgnore]
        public ITheosClientConfigParser TheosClientConfigParser { get; private set; }
        [XmlIgnore]
        public IHubTronClientConfig HubTronClientConfig { get; private set; }
        [XmlIgnore]
        public IHubTronClientConfigParser HubTronClientConfigParser { get; private set; }
        [XmlIgnore]
        public IIbGatewayClientConfig IbGatewayClientConfig { get; private set; }
        [XmlIgnore]
        public IIbGatewayClientConfigParser IbGatewayClientConfigParser { get; private set; }
        [XmlIgnore]
        public IDatabentoClientConfig DatabentoClientConfig { get; private set; }
        [XmlIgnore]
        public IDatabentoClientConfigParser DatabentoClientConfigParser { get; private set; }
        [XmlIgnore]
        public ICobClientConfig CobClientConfig { get; private set; }
        [XmlIgnore]
        public ICobClientConfigParser CobClientConfigParser { get; private set; }
        [XmlIgnore]
        public IPricingClientConfig PricingClientConfig { get; private set; }
        [XmlIgnore]
        public IPricingClientConfigParser PricingClientConfigParser { get; private set; }
        [XmlIgnore]
        public IAutoTraderClientConfig OrderGatewayClientConfig { get; private set; }
        [XmlIgnore]
        public IAutoTraderClientConfigParser OrderGatewayClientConfigParser { get; private set; }
        [XmlIgnore]
        public IAutoTraderClientConfig AutoTraderDirectClientConfig { get; private set; }
        [XmlIgnore]
        public IAutoTraderClientConfigParser AutoTraderDirectClientConfigParser { get; private set; }

        [XmlIgnore]
        public ILiveVolClientConfig LiveVolDataClientConfig { get; private set; }
        [XmlIgnore]
        public ILiveVolClientConfigParser LiveVolDataClientConfigParser { get; private set; }
        [XmlIgnore]
        public bool ConnectAll { get; set; }
        [XmlIgnore]
        public HashSet<string> HalfDays { get; } =
        [
            "2024-12-24",
            "2025-06-03",
            "2025-11-28",
            "2025-12-24",
            "2026-11-27",
            "2026-12-24"
        ];
        [XmlIgnore]
        public List<RaptorClientConfig> RaptorClientConfigs { get; set; } = [];

        public UnderlyingRiskModel GetRiskModel(string underlying)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(underlying))
                {
                    return UnderlyingRiskModel.Default;
                }
                else if (UnderlyingRiskModelLookup.TryGetValue(underlying.ToUpper(), out UnderlyingRiskModel model))
                {
                    return model;
                }
                else if (UnderlyingRiskModelLookup.TryGetValue("ALL", out model))
                {
                    return model;
                }
                else
                {
                    return UnderlyingRiskModel.Default;
                }
            }
            catch (Exception)
            {
                return UnderlyingRiskModel.Default;
            }
        }

        public OmsConfig()
        {
            ColorThemes = new()
            {
                new ColorTheme()
                {
                    Name="Original",
                    Transparent = "Transparent",
                    WhiteColor = "#FFFFFF",
                    GreenColorFg = "#FFFFFF",
                    GreenColor = "#1D673F",
                    GreenColorLight = "#48cb8e",
                    RedColorFg = "#FFFFFF",
                    RedColor = "#832121",
                    RedColorLight = "#ff633a",
                    BlueColorFg = "#80d8f8",
                    BlueColor = "#FFFFFF",
                    LightYellowColor = "#fbbc05",
                    CanceledColor = "#DCDCDC",
                    PendingNewColorFg = "#FFFFFF",
                    PendingNewColor = "#953BBC",
                    PendingNewColorLight = "#9575a5",
                    GreenFocusedColor = "#2D7F59",
                    GreenFocusedColorLight = "#35976a",
                    RedFocusedColor = "#A45E5E",
                    RedFocusedColorLight = "#cc4f2e",
                    CanceledFocusedColor = "#BBBBBB",
                    PendingNewFocusedColor = "#64337F",
                    PendingNewFocusedColorLight = "#665072",
                    UncertainColorFg = "#1D673F",
                    UncertainColor = "#ffcf48",
                    UncertainFocusedColor = "#cca63e",
                    OrangeColor = "#f1a46b",
                    OrangeFocusedColor = "#be8154",
                },
                new ColorTheme()
                {
                    Name="New",
                    Transparent = "Transparent",
                    WhiteColor = "#FFFFFF",
                    GreenColorFg = "#5ac09b",
                    GreenColor = "#013125",
                    GreenColorLight = "#1a4c35",
                    RedColorFg = "#e98ea3",
                    RedColor = "#550911",
                    RedColorLight = "#ff633a",
                    BlueColorFg = "#51bbdc",
                    BlueColor = "#002d49",
                    LightYellowColor = "#fbbc05",
                    CanceledColor = "#DCDCDC",
                    PendingNewColorFg = "#d1a3dc",
                    PendingNewColor = "#451648",
                    PendingNewColorLight = "#9575a5",
                    GreenFocusedColor = "#2D7F59",
                    GreenFocusedColorLight = "#35976a",
                    RedFocusedColor = "#A45E5E",
                    RedFocusedColorLight = "#cc4f2e",
                    CanceledFocusedColor = "#BBBBBB",
                    PendingNewFocusedColor = "#64337F",
                    PendingNewFocusedColorLight = "#665072",
                    UncertainColorFg = "#f2b962",
                    UncertainColor = "#4e2d07",
                    UncertainFocusedColor = "#cca63e",
                    OrangeColor = "#f1a46b",
                    OrangeFocusedColor = "#be8154",
                },
            };
            _colorTheme = ColorThemes.FirstOrDefault();
        }

        public static OmsConfig LoadConfig(IHerculesClientConfig herculesClientConfig,
                                           IHerculesClientConfigParser herculesClientConfigParser,
                                           IRaptorClientConfig raptorClientConfig,
                                           IRaptorClientConfigParser raptorClientConfigParser,
                                           IEdgeScannerClientConfig edgeScannerClientConfig,
                                           IEdgeScannerClientConfigParser edgeScannerClientConfigParser,
                                           IEdgeScanFeedRunnerClientConfig edgeScanFeedRunnerClientConfig,
                                           IEdgeScanFeedRunnerClientConfigParser edgeScanFeedRunnerClientConfigParser,
                                           ISymbolMapClientConfig symbolMapClientConfig,
                                           ISymbolMapClientConfigParser symbolMapClientConfigParser,
                                           IEmaClientConfig emaClientConfig,
                                           IEmaClientConfigParser emaClientConfigParser,
                                           EMAServer.Client.IConfig emaServerConfig,
                                           EmaServerConfig.EmaServerConfigParser emaServerConfigParser,
                                           IInterpolatorClientConfig interpolatorClientConfig,
                                           IInterpolatorClientConfigParser interpolatorClientConfigParser,
                                           ITheosClientConfig theosClientConfig,
                                           ITheosClientConfigParser theosClientConfigParser,
                                           IHubTronClientConfig hubTronClientConfig,
                                           IHubTronClientConfigParser hubTronClientConfigParser,
                                           IIbGatewayClientConfig ibGatewayClientConfig,
                                           IIbGatewayClientConfigParser ibGatewayClientConfigParser,
                                           IDatabentoClientConfig databentoClientConfig,
                                           IDatabentoClientConfigParser databentoClientConfigParser,
                                           ICobClientConfig cobClientConfig,
                                           ICobClientConfigParser cobClientConfigParser,
                                           IPricingClientConfig pricingClientConfig,
                                           IPricingClientConfigParser pricingClientConfigParser,
                                           IAutoTraderClientConfig orderGatewayClientConfig,
                                           IAutoTraderClientConfigParser orderGatewayClientConfigParser,
                                           AutoTraderDirectClientConfig autoTraderDirectClientConfig,
                                           AutoTraderDirectClientConfigParser autoTraderDirectClientConfigParser,
                                           ILiveVolClientConfig liveVolDataClientConfig,
                                           ILiveVolClientConfigParser liveVolDataClientConfigParser,
                                           ITelemetryClientConfig telemetryClientConfig,
                                           ITelemetryClientConfigParser telemetryClientConfigParser,
                                           ITradesClientConfig tradesClientConfig,
                                           ITradesClientConfigParser tradesClientConfigParser,
                                           string configName = "ZeroPlus.Oms")
        {
            ConfigName = configName;
            string configDirectory = GetConfigDirectory();
            string configFilePath = Path.Combine(configDirectory, $"{configName}.xml");
            try
            {
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                var config = LoadConfigFromFile(configFilePath, out var isNew);

                if (config != null)
                {
                    config.HerculesClientConfig = herculesClientConfig;
                    config.HerculesClientConfigParser = herculesClientConfigParser;

                    config.RaptorClientConfig = raptorClientConfig;
                    config.RaptorClientConfigParser = raptorClientConfigParser;

                    config.EdgeScannerClientConfig = edgeScannerClientConfig;
                    config.EdgeScannerClientConfigParser = edgeScannerClientConfigParser;

                    config.EdgeScanFeedRunnerClientConfig = edgeScanFeedRunnerClientConfig;
                    config.EdgeScanFeedRunnerClientConfigParser = edgeScanFeedRunnerClientConfigParser;

                    config.SymbolMapClientConfig = symbolMapClientConfig;
                    config.SymbolMapClientConfigParser = symbolMapClientConfigParser;

                    config.EmaClientConfig = emaClientConfig;
                    config.EmaClientConfigParser = emaClientConfigParser;

                    config.EmaServerConfigParser = emaServerConfigParser;
                    config.EmaServerClientConfig = emaServerConfig;

                    config.InterpolatorClientConfig = interpolatorClientConfig;
                    config.InterpolatorClientConfigParser = interpolatorClientConfigParser;

                    config.TheosClientConfig = theosClientConfig;
                    config.TheosClientConfigParser = theosClientConfigParser;

                    config.HubTronClientConfig = hubTronClientConfig;
                    config.HubTronClientConfigParser = hubTronClientConfigParser;

                    config.IbGatewayClientConfig = ibGatewayClientConfig;
                    config.IbGatewayClientConfigParser = ibGatewayClientConfigParser;

                    config.DatabentoClientConfig = databentoClientConfig;
                    config.DatabentoClientConfigParser = databentoClientConfigParser;

                    config.CobClientConfig = cobClientConfig;
                    config.CobClientConfigParser = cobClientConfigParser;

                    config.PricingClientConfig = pricingClientConfig;
                    config.PricingClientConfigParser = pricingClientConfigParser;

                    config.OrderGatewayClientConfig = orderGatewayClientConfig;
                    config.OrderGatewayClientConfigParser = orderGatewayClientConfigParser;

                    config.AutoTraderDirectClientConfigParser = autoTraderDirectClientConfigParser;
                    config.AutoTraderDirectClientConfig = autoTraderDirectClientConfig;

                    config.LiveVolDataClientConfig = liveVolDataClientConfig;
                    config.LiveVolDataClientConfigParser = liveVolDataClientConfigParser;

                    config.TelemetryClientConfig = telemetryClientConfig;
                    config.TelemetryClientConfigParser = telemetryClientConfigParser;

                    config.TradesClientConfig = tradesClientConfig;
                    config.TradesClientConfigParser = tradesClientConfigParser;

                    System.Threading.Tasks.Parallel.Invoke(
                        config.LoadTbRouteLookup,
                        config.LoadContraEdgeLookup,
                        config.LoadSymbolLookup,
                        config.LoadFishRoutes,
                        config.LoadSmartRoutes,
                        config.LoadQuickRoutes,
                        config.LoadCancelTimerLookup,
                        config.LoadTicketStopLossLookup,
                        config.LoadBasketHedgeLookup,
                        config.LoadBasketMarketMakerOffsetLookup,
                        config.LoadDerivedValueConfigModelLookup,
                        config.LoadExecutingBrokerFeeModels,
                        config.LoadBlockTraderRoutes,
                        config.LoadFavoriteModules,
                        config.LoadUnderlyingRiskSettings,
                        config.LoadCustomPermCombinations,
                        config.LoadBasketQuickAccessPanel,
                        config.LoadBasketDefaultLayouts,
                        config.LoadFishLossConfig,
                        config.LoadEdgeScanFishLossConfig,
                        config.LoadRaptorConfigs,
                        config.LoadLockTraderPriceLimits
                    );
                    config.DoVersionSpecificConfigUpdates();
                    config.SaveOnChange = true;

                    if (isNew)
                    {
                        config.ResetAddresses(true);
                        SaveConfig(config);
                    }

                }
                return config;
            }
            catch (InvalidOperationException)
            {
                throw new SlimException("Config change detected!");
            }
        }

        private static OmsConfig LoadConfigFromFile(string configFilePath, out bool isNew)
        {
            OmsConfig config;
            if (File.Exists(configFilePath))
            {
                FileStream myFileStream = new(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlSerializer xmlSerializer = new(typeof(OmsConfig));
                config = (OmsConfig)xmlSerializer.Deserialize(myFileStream);
                isNew = false;
            }
            else
            {
                config = new OmsConfig();
                isNew = true;
            }

            return config;
        }

        public string DefaultRoute(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultRoute
                : AccountConfig?.DefaultRouteAutoTrader;
        }

        public string DefaultRouteSpxRutXsp(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultRouteSpxRutXsp
                : AccountConfig?.DefaultRouteSpxRutXspAutoTrader;
        }

        public string DefaultRouteNdx(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultRouteNdx
                : AccountConfig?.DefaultRouteNdxAutoTrader;
        }

        public string DefaultSingleLegRoute(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultSingleLegRoute
                : AccountConfig?.DefaultSingleLegRouteAutoTrader;
        }

        public string DefaultHedgeRoute(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultHedgeRouteRegular
                : AccountConfig?.DefaultHedgeRouteAutoTrader;
        }

        public string DefaultCurbSessionRoute(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultCurbSessionRouteRegular
                : AccountConfig?.DefaultCurbSessionRouteAutoTrader;
        }

        public string DefaultSweepRoute(InstanceMode instanceMode)
        {
            return !instanceMode.IsAutoTraderInstance()
                ? AccountConfig?.DefaultSweepRouteRegular
                : AccountConfig?.DefaultSweepRouteAutoTrader;
        }

        public bool IsAlgoRoute(string route, out ExecutingBrokerFeeModel feeModel)
        {
            foreach (var model in _executingFeeModels)
            {
                if (model.AlgoRoutes.Contains(route))
                {
                    feeModel = model;
                    return true;
                }
            }
            feeModel = null;
            return false;
        }

        public bool IsAlgoRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return false;
            }
            return _algoRoutes.Contains(route);
        }

        public ExecutingBrokerFeeModel GetExecutingBrokerFeeModel(ExecutingBroker volant)
        {
            if (_executingBrokerToFeeModelMap.TryGetValue(volant, out ExecutingBrokerFeeModel model))
            {
                return model;
            }
            else
            {
                return _defaultBrokerFeeModel;
            }
        }

        public void LoadFishRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("FishRoute");
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<FishRoute> export = JsonConvert.DeserializeObject<List<FishRoute>>(json);

                    FishRoutes.Clear();
                    foreach (FishRoute item in export)
                    {
                        FishRoutes.Add(item);
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadSmartRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("Smart");
                string fileNew = GetRoutesExportPath("SmartNew");
                if (File.Exists(fileNew))
                {
                    string json = File.ReadAllText(fileNew);
                    List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export = JsonConvert.DeserializeObject<List<Tuple<string, Dictionary<int, Tuple<string, double>>>>>(json);

                    SmartRoutes.Clear();
                    foreach (Tuple<string, Dictionary<int, Tuple<string, double>>> item in export)
                    {
                        SmartRoutes[item.Item1] = item.Item2;
                    }
                }
                else if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<Tuple<string, Dictionary<int, string>>> export = JsonConvert.DeserializeObject<List<Tuple<string, Dictionary<int, string>>>>(json);

                    SmartRoutes.Clear();
                    foreach (Tuple<string, Dictionary<int, string>> item in export)
                    {
                        Dictionary<int, Tuple<string, double>> dictionary = new();
                        foreach (KeyValuePair<int, string> kvp in item.Item2)
                        {
                            dictionary[kvp.Key] = Tuple.Create(kvp.Value, SmartRouteOverwatchTimer);
                        }

                        SmartRoutes[item.Item1] = dictionary;
                    }
                    SaveSmartRoutes();
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadQuickRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("Quick");
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<Tuple<string, string>> export = JsonConvert.DeserializeObject<List<Tuple<string, string>>>(json);

                    QuickRoutes.Clear();
                    foreach (Tuple<string, string> item in export)
                    {
                        QuickRoutes.Add(item);
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadCancelTimerLookup()
        {
            try
            {
                string file = GetCancelTimerLookupExportPath();
                ClearCancelTimers();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);

                    List<Tuple<string, string, double, double>> export = JsonConvert.DeserializeObject<List<Tuple<string, string, double, double>>>(json);

                    foreach (Tuple<string, string, double, double> item in export)
                    {
                        var tuple = Tuple.Create(item.Item1, item.Item2, item.Item3, item.Item4 == 0 ? item.Item3 : item.Item4);
                        AddCancelTimer(tuple);
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    AddCancelTimer(Tuple.Create("$SPX", "", 550.0, 100.0));
                    SaveCancelTimerLookup();
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void ClearCancelTimers()
        {
            CancelTimerLookup.Clear();
            UnderAndRouteToCancelIntervalMap.Clear();
            UnderToCancelIntervalMap.Clear();
            RouteToCancelIntervalMap.Clear();
        }

        public void AddCancelTimer(Tuple<string, string, double, double> tuple)
        {
            CancelTimerLookup.Add(tuple);
            if (!string.IsNullOrWhiteSpace(tuple.Item1) && !string.IsNullOrWhiteSpace(tuple.Item2))
            {
                UnderAndRouteToCancelIntervalMap[Tuple.Create(tuple.Item1, tuple.Item2)] = tuple;
            }
            if (!string.IsNullOrWhiteSpace(tuple.Item1))
            {
                UnderToCancelIntervalMap[tuple.Item1] = tuple;
            }
            if (!string.IsNullOrWhiteSpace(tuple.Item2))
            {
                RouteToCancelIntervalMap[tuple.Item2] = tuple;
            }
        }

        public void LoadTicketStopLossLookup()
        {
            try
            {
                string file = GetTicketStopLossLookupExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<Tuple<string, double, double, int>> export = JsonConvert.DeserializeObject<List<Tuple<string, double, double, int>>>(json);

                    TicketStopLossLookup.Clear();
                    foreach (Tuple<string, double, double, int> item in export)
                    {
                        TicketStopLossLookup.Add(item);
                    }

                    TicketStopLossLookupMap = TicketStopLossLookup.DistinctBy(x => x.Item1).ToDictionary(x => x.Item1, x => x);
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    TicketStopLossLookup.Clear();
                    TicketStopLossLookup.Add(Tuple.Create("SPY", 250.0, .01, 5));
                    TicketStopLossLookup.Add(Tuple.Create("NVDA", 250.0, .01, 5));
                    SaveTicketStopLossLookup();
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadBasketHedgeLookup()
        {
            try
            {
                string file = GetBasketHedgeLookupExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<Tuple<string, string, double>> export = JsonConvert.DeserializeObject<List<Tuple<string, string, double>>>(json);

                    BasketHedgeLookup.Clear();
                    foreach (Tuple<string, string, double> item in export)
                    {
                        BasketHedgeLookup.Add(item);
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    BasketHedgeLookup.Clear();
                    BasketHedgeLookup.Add(Tuple.Create("$SPX", "SPY", 10.0));
                    BasketHedgeLookup.Add(Tuple.Create("$NDX", "QQQ", 40.0));
                    BasketHedgeLookup.Add(Tuple.Create("$RUT", "IWM", 10.0));
                    SaveBasketHedgeLookupLookup();
                }
                BasketHedgeLookupMap = BasketHedgeLookup.ToDictionary(x => x.Item1, x => Tuple.Create(x.Item2, x.Item3));
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadBasketMarketMakerOffsetLookup()
        {
            try
            {
                string file = GetBasketMarketMakerOffsetLookupExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    BasketMarketMakerOffsetLookup = JsonConvert.DeserializeObject<Dictionary<string, BasketMarketMakerOffsetLookupModel>>(json);
                    BasketMarketMakerOffsetLookup ??= new Dictionary<string, BasketMarketMakerOffsetLookupModel>();
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    BasketMarketMakerOffsetLookup = new Dictionary<string, BasketMarketMakerOffsetLookupModel>
                    {
                        ["$SPX"] = new BasketMarketMakerOffsetLookupModel()
                        {
                            Symbol = "$SPX",
                            MinPriceDiff = .15,
                            StrikeOffset = .02,
                            MaxStrikeOffset = .07,
                        }
                    };
                    SaveBasketMarketMakerOffsetLookup();
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
                BasketMarketMakerOffsetLookup = new Dictionary<string, BasketMarketMakerOffsetLookupModel>();
            }
        }

        public void LoadDerivedValueConfigModelLookup()
        {
            try
            {
                string file = GetDerivedValueConfigModelLookupExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    DerivedValueConfigModelLookup = JsonConvert.DeserializeObject<Dictionary<string, DerivedValueConfigModel>>(json);
                    DerivedValueConfigModelLookup ??= new Dictionary<string, DerivedValueConfigModel>();
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    DerivedValueConfigModelLookup = new Dictionary<string, DerivedValueConfigModel>
                    {
                        ["$SPX"] = new DerivedValueConfigModel()
                        {
                            Symbol = "$SPX",
                            DerivedSymbol = "SPY",
                            Multiplier = 10,
                        },
                        ["$NDX"] = new DerivedValueConfigModel()
                        {
                            Symbol = "$NDX",
                            DerivedSymbol = "QQQ",
                            Multiplier = 41.0692,
                        }
                    };
                    SaveDerivedValueConfigModelLookup();
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
                DerivedValueConfigModelLookup = new Dictionary<string, DerivedValueConfigModel>();
            }
        }

        public void LoadExecutingBrokerFeeModels()
        {
            try
            {
                string file = GetExecutingBrokerFeeModelsExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    ExecutingBrokerFeeModels = JsonConvert.DeserializeObject<List<ExecutingBrokerFeeModel>>(json);
                    if (ExecutingBrokerFeeModels == null || ExecutingBrokerFeeModels.Count <= 0)
                    {
                        CreateExecutingBrokerFeeModels();
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    CreateExecutingBrokerFeeModels();
                }

                if (ExecutingBrokerFeeModels != null)
                {
                    foreach (ExecutingBrokerFeeModel model in ExecutingBrokerFeeModels)
                    {
                        _executingBrokerToFeeModelMap[model.ExecutingBroker] = model;
                        if (model.ExecutingBroker == ExecutingBroker.InteractiveBrokers)
                        {
                            _defaultBrokerFeeModel = model;
                        }
                    }
                }

                _executingFeeModels.AddRange(_executingBrokerToFeeModelMap.Values);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
                CreateExecutingBrokerFeeModels();
            }
        }

        private void CreateExecutingBrokerFeeModels()
        {
            try
            {
                GetExecutingBrokerFeeModelsExportPath();
                ExecutingBrokerFeeModels = new List<ExecutingBrokerFeeModel>();
                ExecutingBrokerFeeModel volant = new()
                {
                    ExecutingBroker = ExecutingBroker.Volant,
                    BrokerName = "Volant",
                    ExecutionFee = .04,
                    AlgoExecutionFee = .04,
                    DefaultExchangeFee = .4,
                    AlgoRoutes = new List<string>()
                    {
                        "EXCH_ROLL",
                        "EXCH_ROLL_S",
                        "EXCH_ROLL_SR",
                        "BMMFREE",
                        "BMMSWEEP",
                        "ZPROLL",
                        "ZPSROLL",
                        "TZPROLL",
                        "TZPSROLL",
                        "TEXCH_ROLL",
                    }
                };
                ExecutingBrokerFeeModel dash = new()
                {
                    ExecutingBroker = ExecutingBroker.Dash,
                    BrokerName = "Dash",
                    ExecutionFee = .01,
                    AlgoExecutionFee = .03,
                    DefaultExchangeFee = .4,
                    AlgoRoutes = new List<string>()
                    {
                        "DSTRIKE",
                        "DSMOKE",
                        "DSENSOR",
                    }
                };
                ExecutingBrokerFeeModel ib = new()
                {
                    ExecutingBroker = ExecutingBroker.InteractiveBrokers,
                    BrokerName = "Interactive Brokers",
                    ExecutionFee = .5,
                    AlgoExecutionFee = .5,
                    DefaultExchangeFee = .83,
                    AlgoRoutes = new List<string>()
                    {
                        "SMART",
                        "IB",
                    }
                };
                ExecutingBrokerFeeModel inst = new()
                {
                    ExecutingBroker = ExecutingBroker.Instinet,
                    BrokerName = "Instinet",
                    ExecutionFee = .025,
                    AlgoExecutionFee = .025,
                    DefaultExchangeFee = .4,
                    AlgoRoutes = new List<string>(),
                };

                ExecutingBrokerFeeModels.Add(volant);
                ExecutingBrokerFeeModels.Add(dash);
                ExecutingBrokerFeeModels.Add(ib);
                ExecutingBrokerFeeModels.Add(inst);

                SaveExecutingBrokerFeeModels();
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config creation failed {ex.Message}.");
            }
        }

        public void LoadLockTraderPriceLimits()
        {
            try
            {
                string file = GetLockTraderPriceLimitsExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    LockTraderPriceLimits = JsonConvert.DeserializeObject<Dictionary<BaseStrategy, LockTraderPriceLimitModel>>(json);
                    if (LockTraderPriceLimits == null)
                    {
                        CreateLockTraderPriceLimits();
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                    CreateLockTraderPriceLimits();
                }

                if (LockTraderPriceLimits != null)
                {
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
                CreateLockTraderPriceLimits();
            }
        }

        private void CreateLockTraderPriceLimits()
        {
            try
            {
                GetLockTraderPriceLimitsExportPath();
                LockTraderPriceLimits = new Dictionary<BaseStrategy, LockTraderPriceLimitModel>();
                SaveLockTraderPriceLimits();
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config creation failed {ex.Message}.");
            }
        }

        public void LoadTbRouteLookup()
        {
            try
            {
                TbRouteMapping TbRouteMapping;
                string file = GetRoutesExportPath("TB_I1");
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    TbRouteMapping tbRouteMapping = JsonConvert.DeserializeObject<TbRouteMapping>(json);
                    if (tbRouteMapping != null)
                    {
                        TbRouteMapping = tbRouteMapping;
                        return;
                    }
                }
                TbRouteMapping = TbRouteMapping.Default;
                string content = JsonConvert.SerializeObject(TbRouteMapping, Formatting.Indented);
                if (file != null)
                {
                    File.WriteAllText(file, content);
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadFishLossConfig()
        {
            try
            {
                string file = GetConfigExportPath(nameof(BasketFishLossConfig));
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    FishLossSaveModel fishLossConfig = JsonConvert.DeserializeObject<FishLossSaveModel>(json);
                    if (fishLossConfig != null)
                    {
                        BasketFishLossConfig = fishLossConfig;
                        return;
                    }
                }

                BasketFishLossConfig = new FishLossSaveModel();
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        private void SaveFishLossConfig()
        {
            try
            {
                string file = GetConfigExportPath(nameof(BasketFishLossConfig));
                string content = JsonConvert.SerializeObject(BasketFishLossConfig, Formatting.Indented);
                File.WriteAllText(file, content);
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void LoadEdgeScanFishLossConfig()
        {
            try
            {
                string file = GetConfigExportPath(nameof(EdgeScanFishLossConfig));
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    FishLossSaveModel fishLossConfig = JsonConvert.DeserializeObject<FishLossSaveModel>(json);
                    if (fishLossConfig != null)
                    {
                        EdgeScanFishLossConfig = fishLossConfig;
                        return;
                    }
                }

                EdgeScanFishLossConfig = new FishLossSaveModel();
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        private void SaveEdgeScanFishLossConfig()
        {
            try
            {
                string file = GetConfigExportPath(nameof(EdgeScanFishLossConfig));
                string content = JsonConvert.SerializeObject(EdgeScanFishLossConfig, Formatting.Indented);
                File.WriteAllText(file, content);
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void LoadTradeFilters()
        {
            try
            {
                string file = GetConfigExportPath(nameof(TradeFilters));
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<FilterModel> fishLossConfig = JsonConvert.DeserializeObject<List<FilterModel>>(json);
                    if (fishLossConfig != null)
                    {
                        TradeFilters = fishLossConfig;
                        return;
                    }
                }

                TradeFilters = new();
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        private void SaveTradeFilters()
        {
            try
            {
                string file = GetConfigExportPath(nameof(TradeFilters));
                string content = JsonConvert.SerializeObject(TradeFilters, Formatting.Indented);
                File.WriteAllText(file, content);
                NotifyPropertyChanged();
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void AddTradesFilter(FilterModel filter)
        {
            try
            {
                TradeFilters.Add(filter);
                SaveTradeFilters();
            }
            catch
            {
                // ignored
            }
        }

        public void RemoveTradesFilter(FilterModel filter)
        {
            try
            {
                TradeFilters.Remove(filter);
                SaveTradeFilters();
            }
            catch
            {
                // ignored
            }
        }

        public void LoadSymbolLookup()
        {
            try
            {
                string file = GetSymbolsLookupExportPath();
                List<Tuple<string, string, double, bool>> export = new();
                bool save = false;
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    export = JsonConvert.DeserializeObject<List<Tuple<string, string, double, bool>>>(json);
                }
                else
                {
                    export.Add(Tuple.Create("$SPX", "SPY", 10D, false));
                    export.Add(Tuple.Create("$NDX", "QQQ", 40d, false));
                    export.Add(Tuple.Create("$RUT", "IWM", 10D, false));
                    export.Add(Tuple.Create("$XSP", "SPY", 1D, false));
                    save = true;
                }

                SymbolsLookup.Clear();
                ReverseSymbolsLookup.Clear();
                if (export != null)
                {
                    foreach (Tuple<string, string, double, bool> item in export)
                    {
                        SymbolsLookup[item.Item1] = item;

                        if (!ReverseSymbolsLookup.TryGetValue(item.Item2, out var list))
                        {
                            list = new List<Tuple<string, string, double, bool>>();
                            ReverseSymbolsLookup[item.Item2] = list;
                        }

                        list.Add(item);
                    }
                }

                if (save)
                {
                    SaveSymbolsLookup();
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadContraEdgeLookup()
        {
            try
            {
                string file = GetContraEdgeLookupExportPath();
                Dictionary<string, double> import = new();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    import = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
                }

                ContraEdgeLookup.Clear();
                if (import != null)
                {
                    foreach (var kvp in import)
                    {
                        ContraEdgeLookup[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadBlockTraderRoutes()
        {
            try
            {
                string file = GetBlockTraderRoutesExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<Tuple<string, string>> export = JsonConvert.DeserializeObject<List<Tuple<string, string>>>(json);

                    BlockTraderRoutes.Clear();
                    foreach (Tuple<string, string> item in export)
                    {
                        BlockTraderRoutes.Add(item);
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadFavoriteModules()
        {
            try
            {
                string file = GetFavoriteModulesExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    FavoriteModules = JsonConvert.DeserializeObject<List<FavoriteModuleGroupModel>>(json);
                    ConfigChangedEvent?.Invoke(this, false);
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadUnderlyingRiskSettings()
        {
            try
            {
                string file = GetUnderlyingRiskModelExportPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<UnderlyingRiskModel> export = JsonConvert.DeserializeObject<List<UnderlyingRiskModel>>(json);
                    bool spxFound = false;
                    foreach (UnderlyingRiskModel item in export)
                    {
                        if (!string.IsNullOrWhiteSpace(item.UnderlyingSymbols) && item.UnderlyingSymbols.ToUpper().Contains("$SPX"))
                        {
                            item.OverrideEdgeCheck = true;
                            spxFound = true;
                        }
                        UnderlyingRiskSettings.Add(item);
                    }
                    if (!spxFound)
                    {
                        UnderlyingRiskModel spx = new()
                        {
                            UnderlyingSymbols = "$SPX",
                            OverrideEdgeCheck = true,
                            DontTradeThroughMarketCap = false,
                            DontTradeThroughBidPercent = false,
                            DontTradeThroughEdge = false,
                            DontTradeThroughMid = false,
                            AutoCancelWhenThroughEdge = false,
                            AutoCancelWhenThroughMid = false,
                        };
                        UnderlyingRiskSettings.Add(spx);
                    }
                    UpdateLookup(export);
                    ConfigChangedEvent?.Invoke(this, false);
                }
                else
                {
                    UnderlyingRiskModel all = new()
                    {
                        UnderlyingSymbols = "ALL",
                        QtyViolationMinLimit = 250,
                        RiskCheckMarketPercentage = 0,
                        MaxTheoToAdjTheoOffset = 50,
                        StaleTheoRiskThreshold = 500000,
                        DontTradeThroughBidPercent = false,
                        DontTradeThroughBidPercentValue = .5,
                        DontTradeThroughMid = false,
                        DontTradeThroughEdge = false,
                        DontTradeThroughMarketCap = true,
                        AutoCancelWhenThroughMid = false,
                        AutoCancelWhenThroughEdge = true,
                    };
                    UnderlyingRiskModel spx = new()
                    {
                        UnderlyingSymbols = "$SPX",
                        OverrideEdgeCheck = true,
                        QtyViolationMinLimit = 99,
                        RiskCheckMarketPercentage = .15,
                        MaxTheoToAdjTheoOffset = 30,
                        StaleTheoRiskThreshold = 500000,
                        DontTradeThroughBidPercent = true,
                        DontTradeThroughBidPercentValue = .5,
                        DontTradeThroughMid = true,
                        DontTradeThroughEdge = true,
                        DontTradeThroughMarketCap = true,
                        AutoCancelWhenThroughMid = false,
                        AutoCancelWhenThroughEdge = false,
                    };
                    UnderlyingRiskModel ndx = new()
                    {
                        UnderlyingSymbols = "$NDX",
                        OverrideEdgeCheck = false,
                        QtyViolationMinLimit = 20,
                        RiskCheckMarketPercentage = .15,
                        MaxTheoToAdjTheoOffset = 30,
                        StaleTheoRiskThreshold = 500000,
                        DontTradeThroughBidPercent = true,
                        DontTradeThroughBidPercentValue = .5,
                        DontTradeThroughMid = true,
                        DontTradeThroughEdge = true,
                        DontTradeThroughMarketCap = true,
                        AutoCancelWhenThroughMid = true,
                        AutoCancelWhenThroughEdge = true,
                    };
                    UnderlyingRiskSettings.Add(all);
                    UnderlyingRiskSettings.Add(spx);
                    UnderlyingRiskSettings.Add(ndx);
                    SaveUnderlyingRiskModel();
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public string SaveUnderlyingRiskModel()
        {
            List<UnderlyingRiskModel> export = UnderlyingRiskSettings.ToList();
            UpdateLookup(export);

            string file = GetUnderlyingRiskModelExportPath();
            string json = JsonConvert.SerializeObject(export, Formatting.Indented);
            File.WriteAllText(file, json);
            return file;
        }

        private static void UpdateLookup(List<UnderlyingRiskModel> export)
        {
            ConcurrentDictionary<string, UnderlyingRiskModel> dictionary = new();

            foreach (UnderlyingRiskModel item in export)
            {
                string key = item.UnderlyingSymbols;
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = "ALL";
                }
                key = key.ToUpper();

                foreach (string symbols in key.Split(","))
                {
                    dictionary[symbols] = item;
                }
            }

            UnderlyingRiskModelLookup = dictionary;
        }

        public void LoadCustomPermCombinations()
        {
            try
            {
                string file = GetCustomPermCombinationsPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    CustomPermCombinations = JsonConvert.DeserializeObject<Dictionary<string, List<PermOperationMode>>>(json);
                    ConfigChangedEvent?.Invoke(this, false);
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{file} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadRaptorConfigs()
        {
            try
            {
                string exportPath = GetRaptorConfigExportPath();
                if (File.Exists(exportPath))
                {
                    string json = File.ReadAllText(exportPath);
                    var list = JsonConvert.DeserializeObject<List<RaptorClientConfig>>(json);
                    if (list != null)
                    {
                        RaptorClientConfigs = list.DistinctBy(x => Tuple.Create(x.ServerAddress, x.ServerPort)).ToList();
                        return;
                    }
                }

                RaptorClientConfigs = new List<RaptorClientConfig>();
                for (int i = 0; i < 2; i++)
                {
                    var config = Raptor.Client.Config.RaptorClientConfig.GetDefaultConfig();
                    config.ServerAddress = "derivatives.chi.corp.zeroplusderivatives.com";
                    config.ServerPort += i + 1;
                    RaptorClientConfigs.Add(config);
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadBasketQuickAccessPanel()
        {
            try
            {
                string exportPath = GetBasketQuickAccessPanelExportPath();
                if (File.Exists(exportPath))
                {
                    string json = File.ReadAllText(exportPath);
                    List<Tuple<int, string, ConfigSave>> list = JsonConvert.DeserializeObject<List<Tuple<int, string, ConfigSave>>>(json);
                    if (list != null)
                    {
                        SavedBasketQuickAccessLayouts = list;
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{exportPath} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void LoadBasketDefaultLayouts()
        {
            try
            {
                string exportPath = GetBasketDefaultLayoutExportPath();
                if (File.Exists(exportPath))
                {
                    string json = File.ReadAllText(exportPath);
                    List<Tuple<int, string, LegTypes, Strategy, ConfigSave>> list = JsonConvert.DeserializeObject<List<Tuple<int, string, LegTypes, Strategy, ConfigSave>>>(json);
                    if (list != null)
                    {
                        SavedBasketDefaultLayouts = list;
                    }
                }
                else
                {
                    ConfigMessageEvent?.Invoke($"{exportPath} not found.");
                }
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config load failed {ex.Message}.");
            }
        }

        public void SaveFishRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("FishRoute");

                List<FishRoute> export = FishRoutes.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveSmartRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("SmartNew");

                List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export = SmartRoutes.Select(x => Tuple.Create(x.Key, x.Value)).ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveQuickRoutes()
        {
            try
            {
                string file = GetRoutesExportPath("Quick");

                List<Tuple<string, string>> export = QuickRoutes.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveCancelTimerLookup()
        {
            try
            {
                string file = GetCancelTimerLookupExportPath();

                List<Tuple<string, string, double, double>> export = CancelTimerLookup.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveTicketStopLossLookup()
        {
            try
            {
                string file = GetTicketStopLossLookupExportPath();

                List<Tuple<string, double, double, int>> export = TicketStopLossLookup.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                TicketStopLossLookupMap = TicketStopLossLookup.DistinctBy(x => x.Item1).ToDictionary(x => x.Item1, x => x);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveBasketHedgeLookupLookup()
        {
            try
            {
                string file = GetBasketHedgeLookupExportPath();

                List<Tuple<string, string, double>> export = BasketHedgeLookup.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                BasketHedgeLookupMap = BasketHedgeLookup.ToDictionary(x => x.Item1, x => Tuple.Create(x.Item2, x.Item3));

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveBasketMarketMakerOffsetLookup()
        {
            try
            {
                string file = GetBasketMarketMakerOffsetLookupExportPath();

                string json = JsonConvert.SerializeObject(BasketMarketMakerOffsetLookup, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveDerivedValueConfigModelLookup()
        {
            try
            {
                string file = GetDerivedValueConfigModelLookupExportPath();

                string json = JsonConvert.SerializeObject(DerivedValueConfigModelLookup, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveLockTraderPriceLimits()
        {
            try
            {
                string file = GetLockTraderPriceLimitsExportPath();

                string json = JsonConvert.SerializeObject(LockTraderPriceLimits, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveExecutingBrokerFeeModels()
        {
            try
            {
                string file = GetExecutingBrokerFeeModelsExportPath();

                string json = JsonConvert.SerializeObject(ExecutingBrokerFeeModels, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveSymbolsLookup()
        {
            try
            {
                string file = GetSymbolsLookupExportPath();

                List<Tuple<string, string, double, bool>> export = SymbolsLookup.Values.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveContraEdgeLookup()
        {
            try
            {
                string file = GetContraEdgeLookupExportPath();

                string json = JsonConvert.SerializeObject(ContraEdgeLookup, Formatting.Indented);
                File.WriteAllText(file, json);
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveBlockTraderRoutes()
        {
            try
            {
                string file = GetBlockTraderRoutesExportPath();

                List<Tuple<string, string>> export = BlockTraderRoutes.ToList();
                string json = JsonConvert.SerializeObject(export, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveRaptorClientConfig()
        {
            try
            {
                string file = GetRaptorConfigExportPath();

                string json = JsonConvert.SerializeObject(RaptorClientConfigs, Formatting.Indented);

                File.WriteAllText(file, json);

                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void AddFavoriteModule(string module, ConfigSave configSave)
        {
            FavoriteModuleGroupModel group = FavoriteModules.FirstOrDefault(x => x.ModuleId == configSave.Module);
            if (group == null)
            {
                group = new FavoriteModuleGroupModel
                {
                    Module = module,
                    ModuleId = configSave.Module,
                    GroupCaption = module,
                };
                FavoriteModules.Add(group);
            }
            group.AddFavorite(module, configSave);
            SaveFavoriteModules();
        }

        public void RemoveFavoriteModule(FavoriteModuleModel favoriteModuleModel)
        {
            FavoriteModuleGroupModel group = FavoriteModules.FirstOrDefault(x => x.ModuleId == favoriteModuleModel.ConfigSave.Module);
            if (group != null)
            {
                group.RemoveFavorite(favoriteModuleModel);
                if (group.IsEmpty())
                {
                    FavoriteModules.Remove(group);
                }
                SaveFavoriteModules();
            }
        }

        public void SaveFavoriteModules()
        {
            try
            {
                string file = GetFavoriteModulesExportPath();
                string json = JsonConvert.SerializeObject(FavoriteModules, Formatting.Indented);
                File.WriteAllText(file, json);
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public void SaveCustomPermCombinations()
        {
            try
            {
                string file = GetCustomPermCombinationsPath();
                string json = JsonConvert.SerializeObject(CustomPermCombinations, Formatting.Indented);
                File.WriteAllText(file, json);
                ConfigMessageEvent?.Invoke($"Config saved to {file}.");
                ConfigChangedEvent?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                ConfigMessageEvent?.Invoke($"Config save failed {ex.Message}.");
            }
        }

        public static string SaveConfig(OmsConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            XmlSerializer xmlSerializer = new(typeof(OmsConfig));

            string finalFilePath = Path.Combine(GetConfigDirectory(), $"{ConfigName}.xml");
            string tempFilePath = Path.Combine(GetConfigDirectory(), $"{ConfigName}.temp.xml");

            using (FileStream fs = new(tempFilePath, FileMode.Create, FileAccess.Write))
            using (StreamWriter myWriter = new(fs))
            {
                xmlSerializer.Serialize(myWriter, config);
            }

            File.Move(tempFilePath, finalFilePath, true);

            return finalFilePath;
        }

        public string GetWorkspaceDirectory()
        {

            string layoutDirectory = Path.Combine(GetConfigDirectory(), "Workspaces", WorkspaceTitle);

            if (!Directory.Exists(layoutDirectory))
            {
                Directory.CreateDirectory(layoutDirectory);
            }

            return layoutDirectory;
        }

        public HashSet<string> GetWorkspaces()
        {
            HashSet<string> workspaces = new();

            string layoutDirectory = Path.Combine(GetConfigDirectory(), "Workspaces");
            try
            {
                if (Directory.Exists(layoutDirectory))
                {
                    foreach (string directory in Directory.GetDirectories(layoutDirectory))
                    {
                        DirectoryInfo dirInfo = new(directory);
                        workspaces.Add(dirInfo.Name);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return workspaces;
        }

        public string DeleteConfig(string title)
        {
            try
            {
                if (title == DEFAULT_WORKSPACE)
                {
                    return "Can not delete default workspace";
                }

                string layoutDirectory = Path.Combine(GetConfigDirectory(), "Workspaces", title);

                if (Directory.Exists(layoutDirectory))
                {
                    Directory.Delete(layoutDirectory);
                }
                return title + " deleted successfully.";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string GetConfigExportPath(string type)
        {
            return Path.Combine(GetConfigDirectory(), $"{type}_Config.json");
        }

        public static string GetRoutesExportPath(string type)
        {
            return Path.Combine(GetConfigDirectory(), $"{type}_Routes.json");
        }

        public static string GetCancelTimerLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "CancelTimerIntervalsV2.json");
        }

        public static string GetTicketStopLossLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "TicketStopLossIntervals.json");
        }

        public static string GetBasketHedgeLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "BasketHedgeLookup.json");
        }

        public static string GetBasketMarketMakerOffsetLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "BasketMarketMakerOffsetLookup.json");
        }

        public static string GetDerivedValueConfigModelLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "DerivedValueConfigModelLookupV1.json");
        }

        public static string GetExecutingBrokerFeeModelsExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "ExecutingBrokerFeesModel25.json");
        }

        public static string GetLockTraderPriceLimitsExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "LockTraderPriceLimits.json");
        }

        public static string GetSymbolsLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "SymbolsLookupEqtV1.json");
        }

        public static string GetContraEdgeLookupExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "ContraEdgeLookup.json");
        }

        public static string GetBlockTraderRoutesExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "BlockTrader_Routes.json");
        }

        public static string GetFavoriteModulesExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "FavoriteModules.json");
        }

        public static string GetCustomPermCombinationsPath()
        {
            return Path.Combine(GetConfigDirectory(), "CustomPermCombinations.json");
        }

        public static string GetCustomNotificationsExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "Custom_Notifications.json");
        }

        public static string GetUnderlyingRiskModelExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "UnderlyingRiskModelV2.json");
        }

        public static string GetUserExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "Users.json");
        }

        public static string GetAccountExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "AccountConfigs.json");
        }

        public static string GetEdgeScanExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "EdgeScanFeedFiltersLocal.json");
        }

        public static string GetBasketQuickAccessPanelExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "BasketQuickAccessPanel.json");
        }

        public static string GetBasketDefaultLayoutExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "BasketDefaultLayouts.json");
        }

        public static string GetRaptorConfigExportPath()
        {
            return Path.Combine(GetConfigDirectory(), "RaptorConfigs.json");
        }

        public List<AccountConfigModel> GetSavedAccountConfigs()
        {
            try
            {
                string accountExportPath = GetAccountExportPath();
                if (File.Exists(accountExportPath))
                {
                    string json = File.ReadAllText(accountExportPath);
                    return JsonConvert.DeserializeObject<List<AccountConfigModel>>(json);
                }
                else
                {
                    return new List<AccountConfigModel>();
                }
            }
            catch (Exception)
            {
                return new List<AccountConfigModel>();
            }
        }

        public string GetEdgeScanFeedFilterConfigs()
        {
            try
            {
                string exportPath = GetEdgeScanExportPath();
                if (File.Exists(exportPath))
                {
                    string json = File.ReadAllText(exportPath);
                    return json;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public void SaveAccountConfigs()
        {
            try
            {
                string accountExportPath = GetAccountExportPath();
                string json = JsonConvert.SerializeObject(AccountConfigs);
                File.WriteAllText(accountExportPath, json);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void SaveEdgeScanFeedFiltersConfigs(string json)
        {
            try
            {
                string exportPath = GetEdgeScanExportPath();
                File.WriteAllText(exportPath, json);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void SaveBasketQuickAccessLayouts(List<Tuple<int, string, ConfigSave>> list)
        {
            try
            {
                if (list != null)
                {
                    SavedBasketQuickAccessLayouts = list;
                    string json = JsonConvert.SerializeObject(list);
                    string exportPath = GetBasketQuickAccessPanelExportPath();
                    File.WriteAllText(exportPath, json);
                    ConfigMessageEvent?.Invoke($"Config saved to {exportPath}.");
                    ConfigChangedEvent?.Invoke(this, false);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void SaveBasketDefaultLayouts(List<Tuple<int, string, LegTypes, Strategy, ConfigSave>> list)
        {
            try
            {
                if (list != null)
                {
                    SavedBasketDefaultLayouts = list;
                    string json = JsonConvert.SerializeObject(list);
                    string exportPath = GetBasketDefaultLayoutExportPath();
                    File.WriteAllText(exportPath, json);
                    ConfigMessageEvent?.Invoke($"Config saved to {exportPath}.");
                    ConfigChangedEvent?.Invoke(this, false);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static string GetConfigDirectory()
        {
            string newConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SOLUTION_NAME, "config");
            if (!Directory.Exists(newConfigDirectory))
            {
                Directory.CreateDirectory(newConfigDirectory);

                MigrateFromOldConfigDir(newConfigDirectory);
            }
            return newConfigDirectory;
        }

        private static void MigrateFromOldConfigDir(string newConfigDirectory)
        {
            try
            {
                string oldConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), SOLUTION_NAME, "config");
                if (Directory.Exists(oldConfigDirectory))
                {
                    string[] dirs = Directory.GetDirectories(oldConfigDirectory, "*", SearchOption.AllDirectories);
                    foreach (string dir in dirs)
                    {
                        string dirToCreate = dir.Replace(oldConfigDirectory, newConfigDirectory);
                        Directory.CreateDirectory(dirToCreate);
                    }
                    string[] files = Directory.GetFiles(oldConfigDirectory, "*.*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        File.Copy(file, file.Replace(oldConfigDirectory, newConfigDirectory), true);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void DoVersionSpecificConfigUpdates()
        {
            if (!ConfigReset_260518)
            {
                string configPath = GetConfigDirectory();

                AuthServer = "zpsvr12.corp.zeroplusderivatives.com";
                QuoteAddress = "zpsvr12.corp.zeroplusderivatives.com";
                OrderAddress = "zpsvr12.corp.zeroplusderivatives.com";
                PositionAddress = "zpsvr12.corp.zeroplusderivatives.com";
                HanweckAddress = "zpsvr12.corp.zeroplusderivatives.com";
                TradesClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                TradesClientConfigParser.SaveConfig(configPath, TradesClientConfig);
                HerculesClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                HerculesClientConfigParser.SaveConfig(configPath, HerculesClientConfig);
                EmaClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                EmaClientConfigParser.SaveConfig(configPath, EmaClientConfig);
                AutoTraderDirectClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                AutoTraderDirectClientConfigParser.SaveConfig(configPath, AutoTraderDirectClientConfig);
                EdgeScannerClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                EdgeScannerClientConfigParser.SaveConfig(configPath, EdgeScannerClientConfig);
                PricingClientConfig.ServerAddress = "zpsvr12.corp.zeroplusderivatives.com";
                PricingClientConfigParser.SaveConfig(configPath, PricingClientConfig);
                SymbolMapClientConfig.ServerAddress = "zpsvr05.corp.zeroplusderivatives.com";
                SymbolMapClientConfigParser.SaveConfig(configPath, SymbolMapClientConfig);

                ConfigReset_260518 = true;
            }
        }

        public void ResetAddresses(bool forced, string user = null)
        {
            var location = GetHostLocation(user);
            string configPath = GetConfigDirectory();
            ResetPositionAddress(forced, location);
            ResetOrderAddress(forced, location);
            ResetQuoteAddress(forced, location);
            ResetAtAddress(forced, location, configPath);
            ResetAtLocalAddress(forced, location, configPath);
            ResetHanweckAddress(forced);
            ResetAuthAddress(forced);
            ResetHerculesAddress(forced, configPath);
            ResetRaptorAddress(forced, configPath);
            ResetEdgeScanFeedAddress(forced, configPath);
            ResetEmaAddress(forced, configPath);
            ResetEmaServerAddress(forced, configPath);
            ResetInterpolatorAddress(forced, configPath);
            ResetTheosAddress(forced, configPath);
            ResetSymbolMapAddress(forced, configPath);
            ResetIbAddress(forced, configPath);
            ResetCobAddress(forced, configPath);
            ResetPricingAddress(forced, configPath);
            ResetTradeReqAddress(forced, configPath);
            ResetLiveVolDataAddress(forced, configPath);
        }

        public void SwitchToBackup()
        {
            string configPath = GetConfigDirectory();
            string backupAddress = "zpsvr06.corp.zeroplusderivatives.com";
            var location = "";
            ResetPositionAddress(true, location, backupAddress);
            ResetOrderAddress(true, location, backupAddress);
            ResetQuoteAddress(true, location, backupAddress);
            ResetHanweckAddress(true, backupAddress);
            ResetAuthAddress(true, backupAddress);
            ResetHerculesAddress(true, configPath, backupAddress);
            ResetAtAddress(true, location, configPath, "autotrader.colo.corp.zeroplusderivatives.com");
            ResetAtLocalAddress(true, location, configPath, "orders.chi.corp.zeroplusderivatives.com");
            ResetRaptorAddress(true, configPath, backupAddress);
            ResetEdgeScanFeedAddress(true, configPath, backupAddress);
            ResetEmaAddress(true, configPath, backupAddress);
            ResetEmaServerAddress(true, backupAddress);
            ResetInterpolatorAddress(true, configPath, backupAddress);
            ResetTheosAddress(true, configPath, backupAddress);
            ResetSymbolMapAddress(true, configPath, backupAddress);
            ResetIbAddress(true, configPath, backupAddress);
            ResetCobAddress(true, configPath, backupAddress);
            ResetPricingAddress(true, configPath, backupAddress);
            ResetTradeReqAddress(true, configPath, backupAddress);
            ResetLiveVolDataAddress(true, configPath, backupAddress);

            OnChange(true);
        }

        private static string GetHostLocation(string user)
        {
#if DEBUG
            string location = "test";
#else
            string location = user == "Demo" ? "test" : "chi";
#endif
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    byte[] bytes = ip.GetAddressBytes();
                    if (bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 50)
                    {
                        location = "ge";
                    }
                }
            }

            return location;
        }

        private void ResetPositionAddress(bool forced, string location, string address = null)
        {
            if (forced || IPAddress.TryParse(PositionAddress, out _))
            {
                address ??= "positions." + location + ".corp.zeroplusderivatives.com";
                PositionAddress = address;
                PositionPort = 9091;
            }
        }

        private void ResetOrderAddress(bool forced, string location, string address = null)
        {
            if (forced || IPAddress.TryParse(OrderAddress, out _))
            {
                address ??= "orders." + location + ".corp.zeroplusderivatives.com";
                OrderAddress = address;
                OrderPort = 8111;
            }
        }

        private void ResetQuoteAddress(bool forced, string location, string address = null)
        {
            if (forced || IPAddress.TryParse(QuoteAddress, out _))
            {
                address ??= "marketdata." + (location == "test" ? "chi" : location) + ".corp.zeroplusderivatives.com";
                QuoteAddress = address;
                QuotePort = 8090;
            }
        }

        private void ResetHanweckAddress(bool forced, string address = null)
        {
            if (forced || IPAddress.TryParse(HanweckAddress, out _))
            {
                address ??= "hanweck.chi.corp.zeroplusderivatives.com";
                HanweckAddress = address;
                HanweckPort = 8096;
            }
        }

        private void ResetAuthAddress(bool forced, string address = null)
        {
            if (forced || IPAddress.TryParse(AuthServer, out _))
            {
                address ??= "auth.oms.corp.zeroplusderivatives.com";
                AuthServer = address;
                AuthServerPort = 7677;
            }
        }

        private void ResetHerculesAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(HerculesClientConfig.ServerAddress, out _))
            {
                address ??= "transactions.chi.corp.zeroplusderivatives.com";
                HerculesClientConfig.ServerAddress = address;
                HerculesClientConfig.ServerPort = 7688;
                HerculesClientConfigParser.SaveConfig(configPath, HerculesClientConfig);
            }
        }

        private void ResetAtAddress(bool forced, string location, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(OrderGatewayClientConfig.ServerAddress, out _))
            {
                address ??= "autotrader." + (location != "test" ? "colo" : location) + ".corp.zeroplusderivatives.com";
                OrderGatewayClientConfig.ServerAddress = address;
                OrderGatewayClientConfig.ServerPort = 7697;
                OrderGatewayClientConfigParser.SaveConfig(configPath, OrderGatewayClientConfig);
            }
        }

        private void ResetAtLocalAddress(bool forced, string location, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(AutoTraderDirectClientConfig.ServerAddress, out _))
            {
                address ??= "orders." + (location != "test" ? "chi" : location) + ".corp.zeroplusderivatives.com";
                AutoTraderDirectClientConfig.ServerAddress = address;
                AutoTraderDirectClientConfig.ServerPort = 7697;
                AutoTraderDirectClientConfigParser.SaveConfig(configPath, AutoTraderDirectClientConfig);
            }
        }

        private void ResetRaptorAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(RaptorClientConfig.ServerAddress, out _))
            {
                address ??= "derivatives.chi.corp.zeroplusderivatives.com";
                RaptorClientConfig.ServerAddress = address;
                RaptorClientConfig.ServerPort = 7690;
                RaptorClientConfigParser.SaveConfig(configPath, RaptorClientConfig);

                if (RaptorClientConfigs != null)
                {
                    for (var index = 0; index < RaptorClientConfigs.Count; index++)
                    {
                        var config = RaptorClientConfigs[index];
                        config.ServerAddress = address;
                        config.ServerPort = 7691 + index;
                    }
                }

                SaveRaptorClientConfig();
            }
        }

        private void ResetEdgeScanFeedAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(EdgeScannerClientConfig.ServerAddress, out _))
            {
                address ??= "edgescanner.chi.corp.zeroplusderivatives.com";
                EdgeScannerClientConfig.ServerAddress = address;
                EdgeScannerClientConfig.ServerPort = 7691;
                EdgeScannerClientConfigParser.SaveConfig(configPath, EdgeScannerClientConfig);
            }
        }

        private void ResetEmaAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(EmaClientConfig.ServerAddress, out _))
            {
                address ??= "ema.chi.corp.zeroplusderivatives.com";
                EmaClientConfig.ServerAddress = address;
                EmaClientConfig.ServerPort = 7692;
                EmaClientConfigParser.SaveConfig(configPath, EmaClientConfig);
            }
        }

        private void ResetEmaServerAddress(bool forced, string configPath, string address = null)
        {
            try
            {
                IPAddress ip = EmaServerClientConfig.myIP;
            }
            catch
            {
                forced = true;
                Console.Write("Failed to Parse IP");
            }
            if (forced)
            {
                address ??= "zpsvr05.corp.zeroplusderivatives.com";
                EmaServerClientConfig.ServerEMAAddress = address;
                EmaServerClientConfig.ServerEMAPort = DEFAULT_EMASERVER_PORT;
                EmaServerConfigParser.SaveConfig(EmaServerClientConfig, configPath);
            }
        }

        private void ResetInterpolatorAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(InterpolatorClientConfig.ServerAddress, out _))
            {
                address ??= "ema.chi.corp.zeroplusderivatives.com";
                InterpolatorClientConfig.ServerAddress = address;
                InterpolatorClientConfig.ServerPort = 7670;
                InterpolatorClientConfigParser.SaveConfig(configPath, InterpolatorClientConfig);
            }
        }

        private void ResetTheosAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(TheosClientConfig.ServerAddress, out _))
            {
                address ??= "derivatives.chi.corp.zeroplusderivatives.com";
                TheosClientConfig.ServerAddress = address;
                TheosClientConfig.ServerPort = 7850;
                TheosClientConfigParser.SaveConfig(configPath, TheosClientConfig);
            }
        }

        private void ResetSymbolMapAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(SymbolMapClientConfig.ServerAddress, out _))
            {
                address ??= "derivatives.chi.corp.zeroplusderivatives.com";
                SymbolMapClientConfig.ServerAddress = address;
                SymbolMapClientConfig.ServerPort = 7791;
                SymbolMapClientConfigParser.SaveConfig(configPath, SymbolMapClientConfig);
            }
        }

        private void ResetIbAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(IbGatewayClientConfig.ServerAddress, out _))
            {
                address ??= "192.168.60.28";
                IbGatewayClientConfig.ServerAddress = address;
                IbGatewayClientConfig.ServerPort = 7799;
                IbGatewayClientConfigParser.SaveConfig(configPath, IbGatewayClientConfig);
            }
        }

        private void ResetCobAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(CobClientConfig.ServerAddress, out _))
            {
                address ??= "192.168.60.28";
                CobClientConfig.ServerAddress = address;
                CobClientConfig.ServerPort = 7800;
                CobClientConfigParser.SaveConfig(configPath, CobClientConfig);
            }
        }

        private void ResetPricingAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(PricingClientConfig.ServerAddress, out _))
            {
                address ??= "192.168.60.28";
                PricingClientConfig.ServerAddress = address;
                PricingClientConfig.ServerPort = 7777;
                PricingClientConfigParser.SaveConfig(configPath, PricingClientConfig);
            }
        }

        private void ResetTradeReqAddress(bool forced, string configPath, string address = null)
        {
            if (forced || IPAddress.TryParse(TradesClientConfig.ServerAddress, out _))
            {
                address ??= "192.168.60.28";
                TradesClientConfig.ServerAddress = address;
                TradesClientConfig.ServerPort = 10000;
                TradesClientConfigParser.SaveConfig(configPath, TradesClientConfig);
            }
        }

        private void ResetLiveVolDataAddress(bool forced, string configPath, string address = null)
        {
            if (forced || System.Net.IPAddress.TryParse(LiveVolDataClientConfig.ServerAddress, out _))
            {
                address ??= "127.0.0.1";
                LiveVolDataClientConfig.ServerAddress = address;
                LiveVolDataClientConfig.ServerPort = 7840;
                LiveVolDataClientConfigParser.SaveConfig(configPath, LiveVolDataClientConfig);
            }
        }

        private void UpdateQuoteSource(QuoteSource value)
        {
            switch (value)
            {
                case QuoteSource.Databento:
                    {
                        if (!DatabentoClientEnabled)
                        {
                            DatabentoClientEnabled = true;
                        }

                        break;
                    }
                case QuoteSource.Tron:
                    {
                        if (!QuoteClientEnabled)
                        {
                            QuoteClientEnabled = true;
                        }

                        break;
                    }
            }
        }

        internal void EnsureClientEnabledForQuoteSource(QuoteSource value)
        {
            switch (value)
            {
                case QuoteSource.Databento:
                    {
                        if (!_databentoClientEnabled)
                        {
                            _databentoClientEnabled = true;
                            NotifyPropertyChanged(nameof(DatabentoClientEnabled));
                        }

                        break;
                    }
                case QuoteSource.Tron:
                    {
                        if (!_quoteClientEnabled)
                        {
                            _quoteClientEnabled = true;
                            NotifyPropertyChanged(nameof(QuoteClientEnabled));
                        }

                        break;
                    }
            }
        }

        public void OnChange(bool requiresRestart, bool notify = true)
        {
            if (SaveOnChange)
            {
                try
                {
                    string loc = SaveConfig(this);
                    ConfigMessageEvent?.Invoke($"Config saved to {loc}.");
                }
                catch (Exception ex)
                {
                    ConfigMessageEvent?.Invoke($"Saving config failed, {ex.Message}.");
                }

                if (notify)
                {
                    ConfigChangedEvent?.Invoke(this, requiresRestart);
                }
            }
        }

        public override string ToString()
        {
            return $"\n\n{nameof(OmsConfig)}\n\n"
                   + $"{nameof(LogLevel)}: {_logLevel}\n"
                   + $"{nameof(MdsClientVersion)}: {_mdsClientVersion}\n"
                   + $"{nameof(OrderAddress)}: {_orderAddress}\n"
                   + $"{nameof(OrderPort)}: {_orderPort}\n"
                   + $"{nameof(PositionAddress)}: {_positionAddress}\n"
                   + $"{nameof(PositionPort)}: {_positionPort}\n"
                   + $"{nameof(QuoteAddress)}: {_quoteAddress}\n"
                   + $"{nameof(QuotePort)}: {_quotePort}\n"
                   + $"{nameof(HanweckAddress)}: {_hanweckAddress}\n"
                   + $"{nameof(HanweckPort)}: {_hanweckPort}\n"
                   + $"{nameof(ConnectClientsOnStartupV2)}: {_connectClientsOnStartupV2}\n"
                   + $"{nameof(ClientHbInterval)}: {_clientHbInterval}\n"
                   + $"{nameof(ClientReconInterval)}: {_clientReconInterval}\n"
                   + $"{nameof(CheckForUpdateOnInterval)}: {_checkForUpdateOnInterval}\n"
                   + $"{nameof(AppUpdateUrl)}: {_appUpdateUrl}\n"
                   + $"{nameof(DefaultUiUpdateInterval)}: {_defaultUiUpdateInterval}\n"
                   + $"{nameof(IsDevMode)}: {_isDevMode}";
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
