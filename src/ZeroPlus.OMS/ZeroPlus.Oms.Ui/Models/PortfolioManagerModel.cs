using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.OrderRouting;
using ZeroPlus.Models.Data.Portfolio;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels;
using OrderType = ZeroPlus.Models.Data.Enums.OrderType;
using PositionType = ZeroPlus.Models.Data.Enums.PositionType;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class PortfolioManagerModel : SubscriptionProvider, IPortfolioManager, INotifyPropertyChanged, IDeltaHedgeManagerModel
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public new event PropertyChangedEventHandler PropertyChanged;
        private readonly object _processedPortfoliosLock;
        private readonly object _userPositionLock;
        private readonly object _portfolioUpdateLock;
        private readonly object _symbolToHedgeManagerMapLock;
        private readonly HashSet<IPortfolio> _processedPortfolios;
        private readonly ConcurrentDictionary<int, SymbolStatModel> _idToSymbolStatModel;
        private readonly ConcurrentDictionary<int, HedgePositionModel> _userPositionIdToModelMap;
        private readonly ConcurrentDictionary<string, UnderlyingPositionModel> _underlyingSymbolToUnderlyingPositionModelMap;
        private readonly ConcurrentDictionary<string, SymbolHedgeManagerModel> _symbolToHedgeManagerMap;
        private readonly ConcurrentDictionary<Tuple<int, PortfolioType>, PortfolioModel> _idToPortfolioMap;
        private readonly ConcurrentDictionary<PortfolioType, IPortfolio> _portfolioKeyToPortfolioMap;
        private readonly ConcurrentDictionary<string, ISpreadRiskModel> _spreadIdToRiskModelMap = new();
        private readonly ConcurrentDictionary<int, IOrderArchiveReceiver> _requestIdToRequesterMap = new();
        private readonly object _hedgeManagerLock = new();
        private readonly NotificationManager _notificationManager;
        private IHerculesClient _herculesClient;
        private int _totalOrdersCount;
        private int _uniqueOrdersCount;
        private int _filledOrdersCount;
        private double _closedContractsCount;
        private double _uniqueFillsContractsCount;
        private double _uniqueOrdersContractsCount;
        private double _filledContractsCount;
        private double _highestOpenNotional;
        private double _totalOpenNotional;
        private double _fillRate;

        private DispatcherTimer _hedgeManagerTimer;
        private DispatcherTimer _globalTradesUpdateTimer;
        private bool _roundDeltaForHedge;
        private string _selectedUser;
        private bool _IsHedging;
        private ObservableCollection<string> _routesList;
        private ObservableCollection<string> _dmaRoutesList;
        private ObservableCollection<string> _sorRoutesList;
        private string _account;
        private ObservableCollection<string> _accounts;
        private OrderType _orderType;
        private LimitHandling _limitHandling;
        private PortfolioModel _userPortfolio;

        private readonly IAbstractFactory<SymbolHedgeManagerModel> _symbolHedgeManagerFactory;
        private double _autoHedgeLimitDiff;
        private double _initialHedgeLimitDiff;

        private readonly ConcurrentDictionary<Tuple<Broker, Exchange, string, string>, TraderSubmissionsSummaryModel> _keyToTraderSummaryModel = [];
        private readonly ConcurrentDictionary<Broker, BrokerSubmissionsSummaryModel> _keyToBrokerSummaryModel = [];
        private readonly ConcurrentDictionary<string, UserSpreadPositionModel> _spreadIdToPositionModelMap = new();
        private readonly ConcurrentDictionary<string, UserSymbolPositionModel> _symbolToPositionModelMap = new();

        private int _hedgeHouseInterval = 15000;
        private TimeSpan _hedgeHouseCountDown;
        private DateTime _hedgeHouseManagerNextRun;
        private bool _hedgeHouseEnabled;
        private bool _hedgeManagerRunning;
        private IPortfolio _firmPortfolio;
        private bool _positionUpdated;

        private HashSet<ISymbolStatModel> _updatedSymbolStatModels = new();
        private HashSet<ISymbolStatModel> _updatedSymbolStatModelsSwap = new();
        private readonly HashSet<string> _removedUserPositions = new();
        private readonly HashSet<UserSpreadPositionModel> _removedUserPositionModels = new();

        public OmsCore OmsCore { get; set; }
        public FastObservableCollection<SymbolHedgeManagerModel> HedgedPositionManagers { get; set; }
        public FastObservableCollection<UserSpreadPositionModel> UserSpreadPositions { get; set; }
        public FastObservableCollection<UserSymbolPositionModel> UserSymbolPositions { get; set; }
        public FastObservableCollection<UnderlyingPositionModel> UserPositions { get; set; }
        public FastObservableCollection<string> Users { get; set; }
        public FastObservableCollection<PortfolioModel> FirmPortfolios { get; set; }
        public FastObservableCollection<PortfolioModel> TraderPortfolios { get; set; }
        public FastObservableCollection<PortfolioModel> ApiPortfolios { get; set; }
        public FastObservableCollection<SymbolStatModel> SymbolStatModels { get; set; }
        public FastObservableCollection<OpenPositionModel> OpenNotionalModels { get; set; }
        public FastObservableCollection<OpenPositionModel> OpenPositions { get; set; }
        public FastObservableCollection<SpreadRiskModel> SpreadRiskModels { get; } = new();
        public FastObservableCollection<SelfTradeModel> IdentifiedSelfTrades { get; } = new();
        public FastObservableCollection<SubmissionsSummaryModel> UniqueSubmissionsSummary { get; } = new();
        public Dispatcher Dispatcher { get; private set; }

        public AlertConfigurationModel TotalSubmissionsAlertConfig { get; }
        public AlertConfigurationModel UniqueSubmissionsAlertConfig { get; }
        public AlertConfigurationModel TotalContractsAlertConfig { get; }
        public AlertConfigurationModel UniqueContractsAlertConfig { get; }

        #region Properties
        public bool HedgeHouseEnabled
        {
            get => _hedgeHouseEnabled;
            set
            {
                _hedgeHouseEnabled = value;
                NotifyPropertyChanged();
            }
        }

        public int HedgeHouseInterval
        {
            get => _hedgeHouseInterval;
            set
            {
                _hedgeHouseInterval = value;
                NotifyPropertyChanged();
            }
        }

        public TimeSpan HedgeHouseCountDown
        {
            get => _hedgeHouseCountDown;
            set
            {
                _hedgeHouseCountDown = value;
                NotifyPropertyChanged();
            }
        }

        public PortfolioModel UserPortfolio
        {
            get => _userPortfolio;
            set
            {
                _userPortfolio = value;
                NotifyPropertyChanged();
            }
        }
        public string SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int TotalOrdersCount
        {
            get => _totalOrdersCount;
            set
            {
                if (_totalOrdersCount != value)
                {
                    _totalOrdersCount = value;
                    TotalSubmissionsAlertConfig.CheckAlert(_totalOrdersCount);
                    NotifyPropertyChanged();
                }
            }
        }
        public int UniqueOrdersCount
        {
            get => _uniqueOrdersCount;
            set
            {
                if (_uniqueOrdersCount != value)
                {
                    _uniqueOrdersCount = value;
                    UniqueSubmissionsAlertConfig.CheckAlert(_uniqueOrdersCount);
                    NotifyPropertyChanged();
                }
            }
        }
        public double ClosedContractsCount
        {
            get => _closedContractsCount;
            set
            {
                if (_closedContractsCount != value)
                {
                    _closedContractsCount = value;
                    TotalContractsAlertConfig.CheckAlert(_closedContractsCount);
                    NotifyPropertyChanged();
                }
            }
        }
        public double UniqueFillsContractsCount
        {
            get => _uniqueFillsContractsCount;
            set
            {
                if (_uniqueFillsContractsCount != value)
                {
                    _uniqueFillsContractsCount = value;
                    UniqueContractsAlertConfig.CheckAlert(_uniqueFillsContractsCount);
                    NotifyPropertyChanged();
                }
            }
        }
        public double UniqueOrdersContractsCount
        {
            get => _uniqueOrdersContractsCount;
            set
            {
                if (_uniqueOrdersContractsCount != value)
                {
                    _uniqueOrdersContractsCount = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public double FilledContractsCount
        {
            get => _filledContractsCount;
            set
            {
                if (_filledContractsCount != value)
                {
                    _filledContractsCount = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public double HighestOpenNotional
        {
            get => _highestOpenNotional;
            set
            {
                if (_highestOpenNotional != value)
                {
                    _highestOpenNotional = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public double TotalOpenNotional
        {
            get => _totalOpenNotional;
            set
            {
                if (_totalOpenNotional != value)
                {
                    _totalOpenNotional = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int FilledOrdersCount
        {
            get => _filledOrdersCount;
            set
            {
                if (_filledOrdersCount != value)
                {
                    _filledOrdersCount = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public double FillRate
        {
            get => _fillRate;
            set
            {
                if (_fillRate != value)
                {
                    _fillRate = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public bool IsHedging
        {
            get => _IsHedging;
            set
            {
                if (_IsHedging != value)
                {
                    _IsHedging = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public bool RoundDeltaForHedge
        {
            get => _roundDeltaForHedge;
            set
            {
                if (_roundDeltaForHedge != value)
                {
                    _roundDeltaForHedge = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public ObservableCollection<string> RoutesList
        {
            get => _routesList;
            set
            {
                if (_routesList != value)
                {
                    _routesList = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public ObservableCollection<string> DmaRoutesList
        {
            get => _dmaRoutesList;
            set
            {
                if (_dmaRoutesList != value)
                {
                    _dmaRoutesList = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public ObservableCollection<string> SorRoutesList
        {
            get => _sorRoutesList;
            set
            {
                if (_sorRoutesList != value)
                {
                    _sorRoutesList = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public ObservableCollection<string> Accounts
        {
            get => _accounts;
            set
            {
                _accounts = value;
                NotifyPropertyChanged();
            }
        }
        public string Account
        {
            get => _account;
            set
            {
                _account = value;
                NotifyPropertyChanged();
            }
        }
        public OrderType OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                NotifyPropertyChanged();
            }
        }
        public LimitHandling LimitHandling
        {
            get => _limitHandling;
            set
            {
                _limitHandling = value;
                NotifyPropertyChanged();
            }
        }
        public double AutoHedgeLimitDiff
        {
            get => _autoHedgeLimitDiff;
            set
            {
                _autoHedgeLimitDiff = value;
                NotifyPropertyChanged();
            }
        }
        public double InitialHedgeLimitDiff
        {
            get => _initialHedgeLimitDiff;
            set
            {
                _initialHedgeLimitDiff = value;
                NotifyPropertyChanged();
            }
        }
        public bool GammaScalper { get; } = false;
        #endregion

        public PortfolioManagerModel(NotificationManager notificationManager)
        {
            _notificationManager = notificationManager;
            _idToSymbolStatModel = new ConcurrentDictionary<int, SymbolStatModel>();
            _idToPortfolioMap = new ConcurrentDictionary<Tuple<int, PortfolioType>, PortfolioModel>();
            _portfolioKeyToPortfolioMap = new ConcurrentDictionary<PortfolioType, IPortfolio>();
            _portfolioUpdateLock = new object();
            _processedPortfoliosLock = new object();
            _processedPortfolios = new HashSet<IPortfolio>();
            _underlyingSymbolToUnderlyingPositionModelMap = new();
            _userPositionLock = new();
            _symbolToHedgeManagerMap = new();
            _userPositionIdToModelMap = new();
            _symbolToHedgeManagerMapLock = new();
            OrderType = OrderType.Limit;
            Accounts = new ObservableCollection<string>();
            RoutesList = new ObservableCollection<string>();
            DmaRoutesList = new ObservableCollection<string>();
            SorRoutesList = new ObservableCollection<string>();
            Users = new FastObservableCollection<string>();
            UserPositions = new FastObservableCollection<UnderlyingPositionModel>();
            UserPortfolio = new PortfolioModel(Dispatcher);
            SymbolStatModels = new FastObservableCollection<SymbolStatModel>();
            FirmPortfolios = new FastObservableCollection<PortfolioModel>();
            ApiPortfolios = new FastObservableCollection<PortfolioModel>();
            TraderPortfolios = new FastObservableCollection<PortfolioModel>();
            UserSpreadPositions = new FastObservableCollection<UserSpreadPositionModel>();
            UserSymbolPositions = new FastObservableCollection<UserSymbolPositionModel>();
            HedgedPositionManagers = new FastObservableCollection<SymbolHedgeManagerModel>();
            OpenNotionalModels = new FastObservableCollection<OpenPositionModel>();
            OpenPositions = new FastObservableCollection<OpenPositionModel>();
            TotalSubmissionsAlertConfig = new AlertConfigurationModel("Total Subs", _notificationManager);
            UniqueSubmissionsAlertConfig = new AlertConfigurationModel("Unique Subs", _notificationManager);
            TotalContractsAlertConfig = new AlertConfigurationModel("Total Contracts", _notificationManager);
            UniqueContractsAlertConfig = new AlertConfigurationModel("Unique Contracts", _notificationManager);
        }

        public PortfolioManagerModel(NotificationManager notificationManager, IAbstractFactory<SymbolHedgeManagerModel> symbolHedgeManagerFactory) : this(notificationManager)
        {
            _symbolHedgeManagerFactory = symbolHedgeManagerFactory;
        }

        internal async void LoadHedgeRoutes()
        {
            try
            {
                if (RoutesList.Count == 0)
                {
                    await Task.Run(() =>
                    {
                        List<Comms.Models.Data.ZPAccount> accounts = OmsCore.OrderClient.GetAccountAndRoutes("AAPL");
                        Accounts = accounts.Select(x => x.Acronym).ToObservableCollection();
                        Account = OmsCore.Config.DefaultAccount;
                        if (Accounts.Contains(Account))
                        {
                            Accounts.Add(Account);
                        }
                        var routeLookup = OmsCore.OrderClient?.RouteLookup;
                        var classified = routeLookup?.GetClassifiedRoutes() ?? AutoTraderClient.ClassifiedRoutes.Empty;
                        RoutesList = classified.Combined.ToObservableCollection();
                        DmaRoutesList = classified.Dma.ToObservableCollection();
                        SorRoutesList = classified.Sor.ToObservableCollection();
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadHedgeRoutes));
            }
        }

        private bool IsSorRoute(string route)
        {
            return OmsCore?.OrderClient?.RouteLookup?.IsSmartRoute(route) ?? false;
        }

        private void StartUpdateTimer()
        {
            _hedgeManagerTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000),
            };
            _hedgeManagerTimer.Dispatcher.Thread.Priority = ThreadPriority.Normal;
            _hedgeManagerTimer.Tick += (_, _) => HedgeManagerTimerTick();
            _hedgeManagerTimer.Start();

            _globalTradesUpdateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1500),
            };
            _globalTradesUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.Normal;
            _globalTradesUpdateTimer.Tick += (_, _) => UpdateGlobalStats();
            _globalTradesUpdateTimer.Start();
        }

        internal void ResetHedgeHouseState()
        {
            HedgeHouseCountDown = TimeSpan.Zero;
            if (HedgeHouseEnabled)
            {
                _hedgeHouseManagerNextRun = DateTime.Now + TimeSpan.FromMilliseconds(HedgeHouseInterval);
            }
        }

        private void HedgeManagerTimerTick()
        {
            try
            {
                _hedgeManagerTimer.Stop();
                if (!HedgeHouseEnabled)
                {
                    HedgeHouseCountDown = TimeSpan.Zero;
                }
                else
                {
                    if (_hedgeHouseManagerNextRun < DateTime.Now)
                    {
                        HedgeHouseCountDown = TimeSpan.Zero;
                        _hedgeHouseManagerNextRun = DateTime.Now + TimeSpan.FromMilliseconds(HedgeHouseInterval);
                        RunHedgeManager();
                    }
                    else
                    {
                        HedgeHouseCountDown = _hedgeHouseManagerNextRun - DateTime.Now;
                    }
                    CheckForTimerTasks();
                }
                UpdateHedgeManagers();
            }
            finally
            {
                _hedgeManagerTimer.Start();
            }
        }

        private void UpdateGlobalStats()
        {
            try
            {
                _globalTradesUpdateTimer.Stop();
                if (_updatedSymbolStatModels.Count > 0)
                {
                    (_updatedSymbolStatModels, _updatedSymbolStatModelsSwap) = (_updatedSymbolStatModelsSwap, _updatedSymbolStatModels);
                    foreach (SymbolStatModel model in _updatedSymbolStatModelsSwap.Cast<SymbolStatModel>())
                    {
                        model.Notify();
                    }
                    _updatedSymbolStatModelsSwap.Clear();
                }
            }
            finally
            {
                _globalTradesUpdateTimer.Start();
            }
        }

        private void UpdateHedgeManagers()
        {
            try
            {
                for (int i = 0; i < HedgedPositionManagers.Count; i++)
                {
                    SymbolHedgeManagerModel item = HedgedPositionManagers[i];
                    item.UpdateNetValues();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateHedgeManagers));
            }
        }

        private void RunHedgeManager()
        {
            try
            {
                lock (_hedgeManagerLock)
                {
                    if (_hedgeManagerRunning)
                    {
                        return;
                    }
                    _hedgeManagerRunning = true;
                }

                int count = HedgedPositionManagers.Count;
                for (int i = 0; i < count; i++)
                {
                    SymbolHedgeManagerModel item = HedgedPositionManagers[i];
                    if (item.IsRunning)
                    {
                        item.AttemptClose();
                    }
                }
            }
            finally
            {
                _hedgeManagerRunning = false;
            }
        }

        private void CheckForTimerTasks()
        {
            try
            {
                int count = HedgedPositionManagers.Count;
                for (int i = 0; i < count; i++)
                {
                    SymbolHedgeManagerModel item = HedgedPositionManagers[i];
                    if (item.IsRunning)
                    {
                        item.CheckForEdgeDecrement();
                        item.CheckForDeltaNeutralTrade();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForTimerTasks));
            }
        }

        private void CheckForPositionUpdate()
        {
            try
            {
                for (int i = UserSpreadPositions.Count - 1; i >= 0; i--)
                {
                    UserSpreadPositionModel position = UserSpreadPositions[i];
                    position.Update();
                }
                for (int i = UserSymbolPositions.Count - 1; i >= 0; i--)
                {
                    UserSymbolPositionModel position = UserSymbolPositions[i];
                    position.Update();
                }
                if (_firmPortfolio != null && _positionUpdated)
                {
                    IEnumerable<IPosition> positions = _firmPortfolio.Positions.Where(x => x.PositionType == PositionType.Spread && !double.IsNaN(x.OpenNotional) && x.OpenNotional != 0).ToList();
                    if (positions.Any())
                    {
                        OpenNotionalModels.Clear();
                        OpenPositions.Clear();
                        List<OpenPositionModel> openPositions = positions.Select(x => new OpenPositionModel(x)).ToList();
                        OpenNotionalModels.AddRange(openPositions);
                        OpenPositions.AddRange(openPositions);
                    }
                    else if (OpenNotionalModels.Any())
                    {
                        OpenNotionalModels.Clear();
                        OpenPositions.Clear();
                    }
                    _positionUpdated = false;
                }
                CheckForAlert();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForPositionUpdate));
            }
        }

        private void CheckForAlert()
        {
            if (OmsCore.Config.AlertEnabled)
            {
                for (int i = UserSpreadPositions.Count - 1; i >= 0; i--)
                {
                    UserSpreadPositionModel position = UserSpreadPositions[i];
                    if (position.ActiveAlert)
                    {
                        bool passed = Math.Abs(position.NetQty) >= OmsCore.Config.AlertMinQty && Math.Abs(position.FirmNetQty) >= OmsCore.Config.AlertMinQty && (DateTime.Now - position.LastTradeTime).TotalSeconds >= OmsCore.Config.AlertThreshold;
                        DateTime lastNotified = position.LastNotified;
                        bool found = lastNotified != default;
                        if ((!found && passed) || (found && passed && (DateTime.Now - lastNotified).TotalSeconds >= OmsCore.Config.SnoozeThreshold))
                        {
                            position.LastNotified = DateTime.Now;
                            if (OmsCore.Config.AudioAlertEnabled)
                            {
                                SoundManager.Play(OmsCore.Config.AudioAlertSound);
                            }
                            if (OmsCore.Config.TtsAlertEnabled)
                            {
                                _notificationManager.PlayTts($"Position alert, {OmsCore.User.Username} you are {(position.NetQty > 0 ? "long " : "short ")}{Math.Abs(position.NetQty)} on {position.Description.Replace("-", " ").Replace("/", " ")}");
                            }
                            if (OmsCore.Config.VisualAlertEnabled)
                            {
                                _notificationManager.AddAlert($"Position Alert {position.Description}", DateTime.Now, "Portfolio", position);
                            }
                        }
                        else if (found && position.NetQty == 0)
                        {
                            position.LastNotified = default;
                        }
                    }
                }
            }
        }

        private void RunHedgeDeltaUpdate()
        {
            try
            {
                if (IsHedging)
                {
                    foreach (UnderlyingPositionModel position in UserPositions)
                    {
                        position.Update(RoundDeltaForHedge);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RunHedgeDeltaUpdate));
            }
        }

        internal void SetDispatcherAndStart(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            UserPortfolio.Dispatcher = dispatcher;
            _herculesClient = OmsCore.HerculesClient;
            _herculesClient.ClientDisconnected += OnHerculesClientDisconnected;
            foreach (PortfolioModel item in _idToPortfolioMap.Values)
            {
                item.Dispatcher = dispatcher;
            }

            if (OmsCore.Config.ConnectClientsOnStartupV2)
            {
                _ = OmsCore.HerculesClient.ConnectAndStart();
            }

            StartUpdateTimer();
        }

        public IPortfolio GetPortfolio(int id, PortfolioType portfolioType, int requestId = 0)
        {
            Tuple<int, PortfolioType> key = Tuple.Create(id, portfolioType);
            if (!_idToPortfolioMap.TryGetValue(key, out PortfolioModel portfolio))
            {
                portfolio = new PortfolioModel(Dispatcher)
                {
                    PortfolioType = portfolioType
                };
                _idToPortfolioMap[key] = portfolio;
            }
            return portfolio;
        }

        public void MultiplePortfoliosAdded(int requestId, HashSet<IPortfolio> portfolios)
        {
            if (requestId == 0)
            {
                foreach (IPortfolio portfolio in portfolios)
                {
                    PortfolioAdded(portfolio);
                    List<IPosition> positions = portfolio.Positions.ToList();
                    foreach (IPosition position in positions)
                    {
                        PositionUpdated(portfolio, position, true);
                    }
                }
            }
            else
            {
                if (_requestIdToRequesterMap.TryGetValue(requestId, out IOrderArchiveReceiver requester))
                {
                    requester.AddMultiplePortfolios(portfolios);
                }
            }
        }

        public void PositionAdded(IPortfolio portfolio, IPosition position)
        {
            PortfolioAdded(portfolio);

            if (portfolio.PortfolioType == PortfolioType.Trader &&
                position.PositionType == PositionType.Instance &&
                position.Name.StartsWith("HEDGE - ") &&
                OmsCore.User.Username.Equals(portfolio.Name, StringComparison.OrdinalIgnoreCase))
            {
                GetSymbolHedgeManager(position);
            }

            PositionUpdated(portfolio, position, false);
        }

        public void PortfolioAdded(IPortfolio portfolio)
        {
            if ((portfolio.PortfolioType == PortfolioType.Firm && PortfolioType.Firm.ToString().Equals(portfolio.Name, StringComparison.OrdinalIgnoreCase)) ||
                (portfolio.PortfolioType == PortfolioType.Trader && OmsCore.User.Username.Equals(portfolio.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (portfolio.PortfolioType == PortfolioType.Trader &&
                    OmsCore.OrderClient.TraderPortfolio != portfolio)
                {
                    OmsCore.OrderClient.TraderPortfolio = portfolio;
                    UserPortfolio = (PortfolioModel)portfolio;
                }
                lock (_portfolioUpdateLock)
                {
                    _portfolioKeyToPortfolioMap.TryAdd(portfolio.PortfolioType, portfolio);
                }
            }
            AddPortfolioToCollection(portfolio);
        }

        public void PositionUpdated(IPortfolio portfolio, List<IPosition> positionsList, bool isReplay)
        {
            switch (portfolio.PortfolioType)
            {
                case PortfolioType.Firm when "Firm" == portfolio.Name:
                    foreach (IPosition position in positionsList)
                    {
                        if (position.LastTraderId > 0)
                        {
                            if (OmsCore.GatewayClient.TryGetUser(position.LastTraderId, out var lastTrader))
                            {
                                position.LastTrader = lastTrader.Username;
                            }
                        }
                        HandleFirmPositionUpdate(position);
                    }
                    UpdateGlobalCounter(portfolio);
                    break;
                case PortfolioType.Trader when OmsCore.User.Username.Equals(portfolio.Name, StringComparison.OrdinalIgnoreCase):
                    if (string.IsNullOrEmpty(SelectedUser))
                    {
                        Dispatcher?.BeginInvoke(() =>
                        {
                            SelectedUser = OmsCore.User.Username;
                            Users.Add(SelectedUser);
                        });
                    }

                    foreach (IPosition position in positionsList)
                    {
                        HandleUserPositionUpdate(position, isReplay);
                    }

                    if (OmsCore.Config.AllowDeltaHedging)
                    {
                        List<UnderlyingPositionModel> positionModels = new();
                        List<UnderlyingPositionModel> userSpreadPositions = new();
                        lock (_userPositionLock)
                        {
                            foreach (IPosition position in positionsList.Where(x => x.PositionType == PositionType.Symbol))
                            {
                                bool isNew = false;
                                bool isNewPosition = false;
                                Option option = OptionsHelper.GetOptionFromSymbol(position.Name);
                                string underlyingSymbol = option.UnderlyingSymbol;
                                isNew = !_underlyingSymbolToUnderlyingPositionModelMap.TryGetValue(underlyingSymbol, out UnderlyingPositionModel positionModel);
                                if (isNew)
                                {
                                    positionModel = new UnderlyingPositionModel(this, underlyingSymbol);
                                    _underlyingSymbolToUnderlyingPositionModelMap[underlyingSymbol] = positionModel;
                                    positionModels.Add(positionModel);
                                }

                                isNewPosition = !_userPositionIdToModelMap.TryGetValue(position.Id, out HedgePositionModel model);
                                if (isNewPosition)
                                {
                                    model = new HedgePositionModel(position, option, this);
                                    _userPositionIdToModelMap[position.Id] = model;
                                    positionModel.AddPosition(model);
                                }

                                positionModel.CanHedge = position.Name != positionModel.Symbol;
                            }
                        }

                        if (positionModels.Count > 0)
                        {
                            Dispatcher?.BeginInvoke(() =>
                            {
                                UserPositions.AddRange(positionModels);
                            });
                        }
                    }
                    break;
            }
        }

        public void SubmissionSummaryUpdate(SubmissionsSummary submissionsSummary)
        {
            var key = Tuple.Create(submissionsSummary.Broker, submissionsSummary.Exchange,
                submissionsSummary.Underlying, submissionsSummary.Trader);
            if (!_keyToTraderSummaryModel.TryGetValue(key, out var model))
            {
                if (!_keyToBrokerSummaryModel.TryGetValue(submissionsSummary.Broker, out var brokerModel))
                {
                    brokerModel = new BrokerSubmissionsSummaryModel(Dispatcher)
                    {
                        Name = submissionsSummary.Broker.ToString(),
                    };
                    _keyToBrokerSummaryModel[submissionsSummary.Broker] = brokerModel;
                    Dispatcher.BeginInvoke(() => UniqueSubmissionsSummary.AddItem(brokerModel));
                }
                model = brokerModel.GetModel(submissionsSummary);
                _keyToTraderSummaryModel[key] = model;
            }

            model?.Update(submissionsSummary);
        }

        public void PositionUpdated(IPortfolio portfolio, IPosition position, bool isFromCache)
        {
            switch (portfolio.PortfolioType)
            {
                case PortfolioType.Firm when "Firm" == portfolio.Name:
                    HandleFirmPositionUpdate(position);
                    UpdateGlobalCounter(portfolio);
                    break;
                case PortfolioType.Trader when OmsCore.User.Username.Equals(portfolio.Name, StringComparison.OrdinalIgnoreCase):
                    HandleUserPositionUpdate(position, isFromCache);
                    if (OmsCore.Config.AllowDeltaHedging && position.PositionType == PositionType.Symbol)
                    {
                        HandleUserSymbolPositionUpdate(position);
                    }
                    break;
            }
        }

        private void OnHerculesClientDisconnected()
        {
            List<PortfolioModel> allPortfolios = FirmPortfolios.Union(TraderPortfolios).Union(ApiPortfolios).ToList();
            foreach (PortfolioModel portfolio in allPortfolios)
            {
                portfolio.ClearData();
            }

            foreach (UserSpreadPositionModel position in _spreadIdToPositionModelMap.Values)
            {
                position.Dispose();
            }

            foreach (UserSymbolPositionModel position in _symbolToPositionModelMap.Values)
            {
                position.Dispose();
            }

            if (Dispatcher != null)
            {
                Dispatcher.BeginInvoke(() => ClearContainers(allPortfolios));
            }
            else
            {
                ClearContainers(allPortfolios);
            }

            lock (_processedPortfoliosLock)
            {
                _processedPortfolios.Clear();
            }

            lock (_userPositionLock)
            {
                _userPositionIdToModelMap.Clear();
                _underlyingSymbolToUnderlyingPositionModelMap.Clear();
            }

            _spreadIdToPositionModelMap.Clear();
            _symbolToPositionModelMap.Clear();
            _spreadIdToRiskModelMap.Clear();
            _keyToBrokerSummaryModel.Clear();

            TotalOrdersCount = 0;
            UniqueOrdersCount = 0;
            ClosedContractsCount = 0;
            UniqueFillsContractsCount = 0;
            UniqueOrdersContractsCount = 0;
            FilledContractsCount = 0;
            HighestOpenNotional = 0;
            TotalOpenNotional = 0;
            FilledOrdersCount = 0;
            FillRate = 0;
        }

        private void ClearContainers(List<PortfolioModel> allPortfolios)
        {
            foreach (PortfolioModel portfolio in allPortfolios)
            {
                portfolio.ClearCollections();
            }
            _removedUserPositionModels.Clear();
            UserSpreadPositions.Clear();
            UserSymbolPositions.Clear();
            FirmPortfolios.Clear();
            TraderPortfolios.Clear();
            ApiPortfolios.Clear();
            UserPositions.Clear();
            SpreadRiskModels.Clear();
            IdentifiedSelfTrades.Clear();
            UniqueSubmissionsSummary.Clear();
        }

        private void AddPortfolioToCollection(IPortfolio portfolio)
        {
            try
            {
                bool isNew = false;
                lock (_processedPortfoliosLock)
                {
                    isNew = _processedPortfolios.Add(portfolio);
                }
                if (isNew)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher?.BeginInvoke(() =>
                        {
                            switch (portfolio.PortfolioType)
                            {
                                case PortfolioType.Api:
                                    ApiPortfolios.Add((PortfolioModel)portfolio);
                                    break;
                                case PortfolioType.Trader:
                                    TraderPortfolios.Add((PortfolioModel)portfolio);
                                    break;
                                case PortfolioType.Firm:
                                    FirmPortfolios.Add((PortfolioModel)portfolio);
                                    break;
                            }
                        });
                    }
                    else
                    {
                        switch (portfolio.PortfolioType)
                        {
                            case PortfolioType.Api:
                                ApiPortfolios.Add((PortfolioModel)portfolio);
                                break;
                            case PortfolioType.Trader:
                                TraderPortfolios.Add((PortfolioModel)portfolio);
                                break;
                            case PortfolioType.Firm:
                                FirmPortfolios.Add((PortfolioModel)portfolio);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddPortfolioToCollection));
            }
        }

        public bool TryGetTraderPortfolio(out IPortfolio portfolio)
        {
            lock (_portfolioUpdateLock)
            {
                return _portfolioKeyToPortfolioMap.TryGetValue(PortfolioType.Trader, out portfolio);
            }
        }

        public bool TryGetFirmPortfolio(out IPortfolio portfolio)
        {
            lock (_portfolioUpdateLock)
            {
                return _portfolioKeyToPortfolioMap.TryGetValue(PortfolioType.Firm, out portfolio);
            }
        }

        public bool TryGetTraderPosition(string spreadId, out IPosition position)
        {
            try
            {
                if (TryGetTraderPortfolio(out IPortfolio portfolio) && portfolio != null)
                {
                    return portfolio.TryGetPosition(spreadId, PositionType.Spread, out position) && position != null;
                }
                else
                {
                    position = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetTraderPosition));
                position = null;
                return false;
            }
        }

        public bool TryGetFirmPosition(string spreadId, out IPosition position)
        {
            try
            {
                if (TryGetFirmPortfolio(out IPortfolio portfolio) && portfolio != null)
                {
                    return portfolio.TryGetPosition(spreadId, PositionType.Spread, out position) && position != null;
                }
                else
                {
                    position = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetFirmPosition));
                position = null;
                return false;
            }
        }

        private SymbolHedgeManagerModel GetSymbolHedgeManager(IPosition position)
        {
            SymbolHedgeManagerModel manager = null;
            try
            {
                if (string.IsNullOrWhiteSpace(position.Name))
                {
                    return default;
                }
                if (_symbolHedgeManagerFactory == null)
                {
                    return default;
                }
                string spreadId = position.Name.Replace("HEDGE - " + OmsCore.User.Username.ToUpper() + " - ", "").Trim().ToUpper();
                bool isNew = false;
                lock (_symbolToHedgeManagerMapLock)
                {
                    if (!_symbolToHedgeManagerMap.TryGetValue(spreadId, out manager))
                    {
                        manager = _symbolHedgeManagerFactory.Create();
                        manager.Initialize(position);
                        isNew = true;
                        _symbolToHedgeManagerMap[spreadId] = manager;
                    }
                }
                if (isNew)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher.BeginInvoke(() => HedgedPositionManagers.Add(manager));
                    }
                    else
                    {
                        HedgedPositionManagers.Add(manager);
                    }
                }
                return manager;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetSymbolHedgeManager));
                return manager;
            }
        }

        private void UpdateGlobalCounter(IPortfolio portfolio)
        {
            _firmPortfolio = portfolio;
            _positionUpdated = FilledOrdersCount != portfolio.TotalFills;
            TotalOrdersCount = portfolio.TotalSubmissions;
            UniqueOrdersCount = portfolio.UniqueSubmissions;
            ClosedContractsCount = portfolio.TotalContracts;
            FilledContractsCount = portfolio.TotalContracts;
            UniqueOrdersContractsCount = portfolio.UniqueContracts;
            UniqueFillsContractsCount = portfolio.UniqueContracts;
            HighestOpenNotional = portfolio.HighestOpenNotional;
            TotalOpenNotional = portfolio.TotalOpenNotional;
            FilledOrdersCount = portfolio.TotalFills;
            FillRate = portfolio.FillRate;
        }

        private void HandleFirmPositionUpdate(IPosition position)
        {
            try
            {
                switch (position.PositionType)
                {
                    case PositionType.Spread:
                        if (string.IsNullOrWhiteSpace(position.Name))
                        {
                            _log.Warn(nameof(HandleFirmPositionUpdate) + ". Empty position name. Id: " + position.Id + ", Type: " + position.PositionType);
                            return;
                        }
                        Update(position.Name, SubscriptionFieldType.FirmSpreadPosition, position);
                        CheckForFirmFirstEdge(position);
                        break;
                    case PositionType.Underlying:
                        if (string.IsNullOrWhiteSpace(position.Name))
                        {
                            _log.Warn(nameof(HandleFirmPositionUpdate) + ". Empty underlying position name. Id: " + position.Id + ", Type: " + position.PositionType);
                            return;
                        }
                        Update(position.Name, SubscriptionFieldType.FirmUnderlyingPosition, position);
                        break;
                    case PositionType.Symbol:
                        if (string.IsNullOrWhiteSpace(position.Name))
                        {
                            break;
                        }
                        Update(position.Name, SubscriptionFieldType.FirmSymbolPosition, position);
                        break;
                    case PositionType.Instance:
                        Update(position.Name, SubscriptionFieldType.FirmInstancePosition, position);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleFirmPositionUpdate));
            }
        }

        private void HandleUserPositionUpdate(IPosition position, bool isReplay)
        {
            try
            {
                switch (position.PositionType)
                {
                    case PositionType.Spread:
                        Update(position.Name.ToUpper(), SubscriptionFieldType.UserSpreadPosition, position, saveCache: false, isReplay);
                        if (OmsCore.Config.AllowUserPositionTracking)
                        {
                            UserPortfolio.UpdatePosition(position);
                        }
                        CheckForUserFirstEdge(position);
                        UserSpreadPositionUpdate(position);
                        break;
                    case PositionType.Underlying:
                        Update(position.Name, SubscriptionFieldType.UserUnderlyingPosition, position);
                        break;
                    case PositionType.Instance:
                        Update(position.Name, SubscriptionFieldType.UserInstancePosition, position);
                        break;
                    case PositionType.Symbol:
                        UserSymbolPositionUpdate(position);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUserPositionUpdate));
            }
        }

        private void UserSpreadPositionUpdate(IPosition position)
        {
            string symbol = position.Name;

            if (!_spreadIdToPositionModelMap.TryGetValue(symbol, out UserSpreadPositionModel model))
            {
                model = new UserSpreadPositionModel(OmsCore, this)
                {
                    Position = position
                };
                _spreadIdToPositionModelMap[symbol] = model;
                if (_removedUserPositions.Contains(symbol))
                {
                    _removedUserPositionModels.Add(model);
                }
            }
            model.HandleUpdate(position);
        }

        private void UserSymbolPositionUpdate(IPosition position)
        {
            string symbol = position.Name;
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Contains(' '))
            {
                return;
            }
            UserSymbolPositionModel model = GetUserSymbolPositionModel(symbol);
            model?.HandleUpdate(position);
        }

        public void Refresh()
        {
            if (_spreadIdToPositionModelMap.Any() || _symbolToPositionModelMap.Any())
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UserSpreadPositions.Clear();
                    UserSpreadPositions.AddRange(_spreadIdToPositionModelMap.Values.ToList());

                    UserSymbolPositions.Clear();
                    UserSymbolPositions.AddRange(_symbolToPositionModelMap.Values.ToList());

                    CheckForPositionUpdate();
                    RunHedgeDeltaUpdate();
                });
            }
        }

        private UserSymbolPositionModel GetUserSymbolPositionModel(string symbol)
        {
            if (!_symbolToPositionModelMap.TryGetValue(symbol, out UserSymbolPositionModel model))
            {
                model = new UserSymbolPositionModel
                {
                    Symbol = symbol
                };
                _symbolToPositionModelMap[symbol] = model;
            }

            return model;
        }

        internal void AddSymbol(string underlyingSymbol)
        {
            bool isNew = !_underlyingSymbolToUnderlyingPositionModelMap.TryGetValue(underlyingSymbol, out UnderlyingPositionModel positionModel);
            if (isNew)
            {
                positionModel = new UnderlyingPositionModel(this, underlyingSymbol);
                _underlyingSymbolToUnderlyingPositionModelMap[underlyingSymbol] = positionModel;
                UserPositions.Add(positionModel);
            }
        }

        private void HandleUserSymbolPositionUpdate(IPosition position)
        {
            try
            {
                bool isNew = false;
                bool isNewPosition = false;
                UnderlyingPositionModel positionModel = null;
                HedgePositionModel model = null;

                Option option = OptionsHelper.GetOptionFromSymbol(position.Name);
                string underlyingSymbol = option.UnderlyingSymbol;

                lock (_userPositionLock)
                {
                    isNew = !_underlyingSymbolToUnderlyingPositionModelMap.TryGetValue(underlyingSymbol, out positionModel);
                    if (isNew)
                    {
                        positionModel = new UnderlyingPositionModel(this, underlyingSymbol);
                        _underlyingSymbolToUnderlyingPositionModelMap[underlyingSymbol] = positionModel;
                    }

                    isNewPosition = !_userPositionIdToModelMap.TryGetValue(position.Id, out model);
                    if (isNewPosition)
                    {
                        model = new HedgePositionModel(position, option, this);
                        _userPositionIdToModelMap[position.Id] = model;
                    }
                }

                if (isNew)
                {
                    UserPositions.Add(positionModel);
                    if (isNewPosition)
                    {
                        positionModel.AddPosition(model);
                    }
                }
                else if (isNewPosition)
                {
                    if (isNewPosition)
                    {
                        positionModel.AddPosition(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleUserSymbolPositionUpdate));
            }
        }

        private void CheckForFirmFirstEdge(IPosition position)
        {
            if (position.FirstEdgeAcquired &&
                OmsCore.Config.ShowFirstEdgeNotificationsV2 &&
                !OmsCore.Config.ShowFirstEdgeForThisSessionOnly)
            {
                _notificationManager.FirstEdgeAcquired(position);
            }
        }

        private void CheckForUserFirstEdge(IPosition position)
        {
            if (position.FirstEdgeAcquired &&
                OmsCore.Config.ShowFirstEdgeNotificationsV2 &&
                OmsCore.Config.ShowFirstEdgeForThisSessionOnly)
            {
                _notificationManager.FirstEdgeAcquired(position);
            }
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            PortfolioType? portfolioType = null;
            PositionType? positionType = null;
            switch (subscription.Type)
            {
                case SubscriptionFieldType.FirmSpreadPosition:
                    portfolioType = PortfolioType.Firm;
                    positionType = PositionType.Spread;
                    break;
                case SubscriptionFieldType.FirmUnderlyingPosition:
                    portfolioType = PortfolioType.Firm;
                    positionType = PositionType.Underlying;
                    break;
                case SubscriptionFieldType.FirmInstancePosition:
                    portfolioType = PortfolioType.Firm;
                    positionType = PositionType.Instance;
                    break;
                case SubscriptionFieldType.FirmSymbolPosition:
                    portfolioType = PortfolioType.Firm;
                    positionType = PositionType.Symbol;
                    break;
                case SubscriptionFieldType.UserSpreadPosition:
                    portfolioType = PortfolioType.Trader;
                    positionType = PositionType.Spread;
                    break;
                case SubscriptionFieldType.UserUnderlyingPosition:
                    portfolioType = PortfolioType.Trader;
                    positionType = PositionType.Underlying;
                    break;
                case SubscriptionFieldType.UserInstancePosition:
                    portfolioType = PortfolioType.Trader;
                    positionType = PositionType.Instance;
                    break;
            }
            if (portfolioType != null && positionType != null)
            {
                bool found = false;
                bool portfolioFound = false;
                IPortfolio portfolio;

                lock (_portfolioUpdateLock)
                {
                    portfolioFound = _portfolioKeyToPortfolioMap.TryGetValue((PortfolioType)portfolioType, out portfolio);
                }

                if (portfolioFound)
                {
                    if (portfolio.TryGetPosition(subscription.Symbol, (PositionType)positionType, out IPosition position) && position != null)
                    {
                        PositionUpdated(portfolio, position, true);
                        found = true;
                    }
                }
                if (!found && portfolioType == PortfolioType.Trader && positionType == PositionType.Instance)
                {
                    Position position = new()
                    {
                        Name = subscription.Symbol,
                        PositionType = (PositionType)positionType,
                    };
                    Update(subscription.Symbol, SubscriptionFieldType.UserInstancePosition, position);
                }
            }
        }

        internal void HardSideUpdated(HardSideResult model)
        {
            Update(model.HardSideKey.ToString(), SubscriptionFieldType.HardSide, model, saveCache: true);
        }

        internal List<ZeroPlus.Models.Data.Securities.Option> GetNonTradedByFirm(List<ZeroPlus.Models.Data.Securities.Option> options)
        {
            List<ZeroPlus.Models.Data.Securities.Option> output = new();

            if (_portfolioKeyToPortfolioMap.TryGetValue(PortfolioType.Firm, out IPortfolio firmPortfolio))
            {
                foreach (ZeroPlus.Models.Data.Securities.Option option in options)
                {
                    string symbol = option.PutCall.ToString().ToUpper() + " " + option.Underlying?.Symbol + " " + option.Expiration.ToString("MMM-dd-yy") + " " + option.Strike.ToString("G29");
                    if (!firmPortfolio.TryGetPosition(symbol, PositionType.Spread, out IPosition position))
                    {
                        output.Add(option);
                    }
                }
            }

            return output;
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
        }

        internal void AddRequester(int requestId, IOrderArchiveReceiver receiver)
        {
            _requestIdToRequesterMap[requestId] = receiver;
        }

        internal string SerializeAlertsToJson()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(TotalSubmissionsAlertConfig)] = TotalSubmissionsAlertConfig.SerializeToJson(),
                [nameof(UniqueSubmissionsAlertConfig)] = UniqueSubmissionsAlertConfig.SerializeToJson(),
                [nameof(TotalContractsAlertConfig)] = TotalContractsAlertConfig.SerializeToJson(),
                [nameof(UniqueContractsAlertConfig)] = UniqueContractsAlertConfig.SerializeToJson(),
            };
            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async Task LoadAlertsFromJsonAsync(string alertsJson)
        {
            try
            {
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(alertsJson));
                await TotalSubmissionsAlertConfig.LoadFromJsonAsync(configDictionary[nameof(TotalSubmissionsAlertConfig)]);
                await UniqueSubmissionsAlertConfig.LoadFromJsonAsync(configDictionary[nameof(UniqueSubmissionsAlertConfig)]);
                await TotalContractsAlertConfig.LoadFromJsonAsync(configDictionary[nameof(TotalContractsAlertConfig)]);
                await UniqueContractsAlertConfig.LoadFromJsonAsync(configDictionary[nameof(UniqueContractsAlertConfig)]);
            }
            catch
            {
                // ignored
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ISymbolStatModel GetSymbolStatModel(int id)
        {
            _idToSymbolStatModel.TryGetValue(id, out SymbolStatModel model);
            return model;
        }

        public ISymbolStatModel GetSymbolStatModel(int id, string symbol)
        {
            SymbolStatModel model = new()
            {
                Id = id,
                Symbol = symbol,
            };
            _idToSymbolStatModel[id] = model;
            AddModel(model);
            return model;
        }

        public void SymbolStatModelUpdated(ISymbolStatModel model)
        {
            _updatedSymbolStatModels.Add(model);
        }

        private void AddModel(SymbolStatModel model)
        {
            try
            {
                Dispatcher?.BeginInvoke(() => SymbolStatModels.Add(model));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddModel));
            }
        }

        public ISpreadRiskModel GetSpreadRiskModel(string spreadId)
        {
            ISpreadRiskModel model;

            if (!OmsCore.Config.ShowEodRiskV2)
            {
                model = null;
            }
            else
            {
                if (!_spreadIdToRiskModelMap.TryGetValue(spreadId, out model))
                {
                    SpreadRiskModelContainer spreadRiskModel = new(spreadId);
                    _spreadIdToRiskModelMap[spreadId] = model = spreadRiskModel;
                    Dispatcher?.BeginInvoke(() => SpreadRiskModels.Add(spreadRiskModel.SpreadRiskModel));
                }
            }

            return model;
        }

        public void SpreadRiskUpdate(ISpreadRiskModel model)
        {
            if (OmsCore.Config.ShowEodRiskV2 && model is SpreadRiskModelContainer riskModel)
            {
                Dispatcher?.BeginInvoke(() => riskModel.CopyToModel());
            }
        }

        public ISelfTradeModel GetSelfTradeWarningModel()
        {
            return new SelfTradeModel();
        }

        public void SelfTradeWarning(ISelfTradeModel model)
        {
            Dispatcher?.BeginInvoke(() => IdentifiedSelfTrades.Add(model as SelfTradeModel));
        }

        public void MultiplePositionsAdded(int requestId, IPortfolio portfolio, List<IPosition> positionsList)
        {
        }

        public void ResetHiddenModels()
        {
            var modelsToAdd = _removedUserPositionModels.ToList();

            if (Dispatcher != null)
            {
                Dispatcher.BeginInvoke(() => UserSpreadPositions.AddRange(modelsToAdd));
            }
            else
            {
                UserSpreadPositions.AddRange(modelsToAdd);
            }

            _removedUserPositions.Clear();
            _removedUserPositionModels.Clear();
        }

        public void RemoveUserSpreadPosition(UserSpreadPositionModel model)
        {
            try
            {
                _removedUserPositions.Add(model.Description);
                _removedUserPositionModels.Add(model);
                if (Dispatcher != null)
                {
                    Dispatcher.BeginInvoke(() => UserSpreadPositions.Remove(model));
                }
                else
                {
                    UserSpreadPositions.Remove(model);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveUserSpreadPosition));
            }
        }
    }
}
