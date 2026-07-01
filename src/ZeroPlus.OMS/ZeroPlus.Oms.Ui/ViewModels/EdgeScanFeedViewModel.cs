using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helpers;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using DaysToExpirationEdgeModel = ZeroPlus.Oms.Ui.Models.DaysToExpirationEdgeModel;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EdgeScanFeedViewModel : ModuleViewModelBase, IOmsDataSubscriber
    {
        private readonly BulletinBroker _bulletinBroker;
        private readonly IModuleFactory _moduleFactory;
        public const string EDGE_SCAN_FEED_SUBTYPE = "ESF";

        private DelegateCommand<object> _filterInNewOrderBookCommand;
        private DelegateCommand<object> _searchInNewTradesModuleCommand;
        private DelegateCommand<object> _chartSymbolBidAskIvCommand;

        private readonly FastObservableCollection<EdgeScanFeedModel> _alltrades;
        private readonly FastObservableCollection<EdgeScanFeedModel> _uniqueTrades;
        private readonly HashSet<string> _uniqueTradesList;


        private readonly object _lastTradeTimeMapLock = new();
        private readonly ConcurrentDictionary<string, ReferenceTradeModel> _lastTradeTimeMap = new();
        private readonly ConcurrentDictionary<Tuple<string, DateTime>, Dictionary<double, DateTime>> _lastTradeAreaTimeMap = new();
        private readonly ConcurrentDictionary<SignalKey, bool> _processed = new();

        private EdgeScanFeedFilterConfig _moduleConfig;
        private DateTime _startTime;
        private DispatcherTimer _upTimeUpdateTimer;

        public string RunnerId { get; }
        public override Module Module { get; protected set; } = Module.EdgeScanFeed;

        public static TimeSpan EdgeScanFeedStartTimeEastern { get; } = TimeSpan.FromHours(9) + TimeSpan.FromMinutes(40);
        public DominatorsManagerModel DominatorsManagerModel { get; }
        public NotificationManager NotificationManager { get; }

        private static Color GetRandomBrush() => Color.FromRgb((byte)Random.Shared.Next(0, 256), (byte)Random.Shared.Next(0, 256), (byte)Random.Shared.Next(0, 256));

        protected IVerificationService VerificationService => GetService<IVerificationService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public TextWrapping HeaderTextWrapping => OmsCore.Config.WrapColumnHeaderV2 ? TextWrapping.WrapWithOverflow : TextWrapping.NoWrap;
        public IEnumerable<AutoTraderEdgeOverride> AutoTraderEdgeOverrides { get; } = ((AutoTraderEdgeOverride[])Enum.GetValues(typeof(AutoTraderEdgeOverride))).ToList();
        public IEnumerable<AutoTraderRouteOption> AutoTraderRouteOptions { get; } = ((AutoTraderRouteOption[])Enum.GetValues(typeof(AutoTraderRouteOption))).ToList();
        public IEnumerable<EdgeScanFeedRunnerOption> EdgeScanFeedRunnerOptions { get; } = ((EdgeScanFeedRunnerOption[])Enum.GetValues(typeof(EdgeScanFeedRunnerOption))).ToList();
        public IEnumerable<AutoTraderSideSelection> AutoTraderSideSelectionOptions { get; } = ((AutoTraderSideSelection[])Enum.GetValues(typeof(AutoTraderSideSelection))).ToList();
        public TransactionConsumerModel TransactionConsumerModel { get; }
        public PortfolioManagerModel PortfolioManagerModel { get; }
        public Color BorderBrushColor { get; }

        private int _totalReceived;
        private int _lastReceived;
        private int _lastSubmission;
        private int _totalSubmission;
        private int _totalSubmissionRate;
        private int _totalSubmissionRateSec;
        private DateTime _orderRateLastUpdateTime;
        private DateTime _orderRateLastUpdateTimeSec;
        private DateTime _cutoffTime;
        private DateTime _saveTime;
        private string _audioAlertSound;
        private ConcurrentDictionary<string, System.Timers.Timer> _spreadIdToUnbanTimerMap = new();
        private bool _saved;

        private readonly IEdgeScanFeedStatisticsSummary _summary;
        private readonly OmsCore _omsCore;

        private static readonly TimeSpan ServerStopAckGracePeriod = TimeSpan.FromSeconds(5);
        private DateTime _lastStopRequestUtc = DateTime.MinValue;

        [Bindable]
        public partial bool OverrideMarketOpenCheck { get; set; }
        [Bindable]
        public partial bool DebugModeEnabled { get; set; }
        [Bindable]
        public partial string UpTime { get; set; }
        [Bindable]
        public partial BasketTraderView AutoTraderBasketView { get; set; }
        [Bindable]
        public partial BasketTraderViewModel AutoTraderBasketViewModel { get; set; }
        [Bindable]
        public partial Brush BorderBrush { get; set; }
        [Bindable]
        public partial ObservableCollection<object> SelectedItems { get; set; }

        [Bindable]
        public partial BlockedSymbolModel BlockedSymbolModel { get; set; }

        [Bindable]
        public partial int BlockedSymbolModelId { get; set; }

        [Bindable]
        public partial ObservableCollection<EdgeScanFeedModel> Trades { get; set; }

        [Bindable]
        public partial bool ShowUniqueSelected { get; set; }

        [Bindable]
        public partial EdgeScanFeedModel LatestEdgeScanFeed { get; set; }

        [Bindable]
        public partial bool AutoTraderAutoPermTrades { get; set; }

        [Bindable(Default = true)]
        public partial bool AutoTraderSkipActiveOrders { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double SecondaryOrderRestPeriod { get; set; }

        [Bindable]
        public partial bool MarkPrices { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double MarkPricesMinEdge { get; set; }

        [Bindable]
        public partial AutoPermConfigModel AutoTraderAutoPermConfig { get; set; }

        [Bindable(Default = true)]
        public partial bool AutoStop { get; set; }

        [Bindable]
        public partial bool AutoSave { get; set; }

        [Bindable]
        public partial DateTime CutoffTime { get; set; }

        [Bindable]
        public partial DateTime SaveTime { get; set; }

        [Bindable]
        public partial bool AutoScrollEdgeFeed { get; set; }

        [Bindable]
        public partial bool EnableLogMode { get; set; }

        [Bindable]
        public partial string FilterString { get; set; }

        [Bindable]
        public partial string UnderlyingFilter { get; set; }

        [Bindable]
        public partial bool MinExpirationFilterEnabled { get; set; }

        [Bindable]
        public partial bool MaxExpirationFilterEnabled { get; set; }

        [Bindable]
        public partial bool AutoTraderRunning { get; set; }
        [Bindable(Default = EdgeScanFeedRunnerState.Stopped)]
        public partial EdgeScanFeedRunnerState ServerRunnerState { get; set; }
        [Bindable]
        public partial bool IsServerRunnerActive { get; set; }
        [Bindable(Default = true)]
        public partial bool AutoTraderUseTradePrice { get; set; }

        [Bindable]
        public partial bool ConfirmWithIbCob { get; set; }

        [Bindable(Default = true)]
        public partial bool AutoTraderAttemptBothSides { get; set; }

        [Bindable]
        public partial bool AutoTraderDoNotTradeThroughFillPrice { get; set; }

        [Bindable]
        public partial bool AutoTraderEnablePayUpTicks { get; set; }

        [Bindable]
        public partial bool AutoClearFeed { get; set; }

        [Bindable]
        public partial int AutoClearFeedCount { get; set; }

        [Bindable]
        public partial int AutoTraderPayUpTicks { get; set; }

        [Bindable(Default = AutoTraderEdgeOverride.Edge)]
        public partial AutoTraderEdgeOverride AutoTraderEdgeOverride { get; set; }

        [Bindable]
        public partial AutoTraderSideSelection AutoTraderSideSelector { get; set; }

        [Bindable]
        public partial AutoTraderRouteOption AutoTraderRouteOption { get; set; }

        [Bindable]
        public partial EdgeScanFeedRunnerOption EdgeScanFeedRunnerOption { get; set; }

        [Bindable]
        public partial bool IsServerRunnerMode { get; set; }

        partial void OnEdgeScanFeedRunnerOptionChanged(EdgeScanFeedRunnerOption value)
        {
            IsServerRunnerMode = value == EdgeScanFeedRunnerOption.Agent;
            if (value == EdgeScanFeedRunnerOption.Agent)
            {
                TransactionConsumerModel?.UnsubscribeAll(this);
            }
            else if (value == EdgeScanFeedRunnerOption.Local && SelectedModel != null)
            {
                LoadFilterModel(SelectedModel);
            }
        }

        [Bindable(Default = 5)]
        public partial int AutoTraderMinQty { get; set; }

        [Bindable(Default = 500)]
        public partial int AutoTraderMaxLatency { get; set; }

        [Bindable(Default = 2)]
        public partial int AutoTraderMaxOpenPos { get; set; }

        [Bindable]
        public partial bool LoadWithStockTiedLeg { get; set; }

        [Bindable]
        public partial bool MinPnlForAutoTraderEnabled { get; set; }

        [Bindable]
        public partial bool MinPnlMaxQtyCheckEnabled { get; set; }

        [Bindable]
        public partial int MinPnlMaxQty { get; set; }

        [Bindable(Default = -.05)]
        public partial double MinPnlForAutoTrader { get; set; }

        [Bindable]
        public partial bool BlockAlreadyTradedSymbols { get; set; }

        [Bindable]
        public partial bool BlockFirmTradesForTime { get; set; }

        [Bindable(Default = 2500)]
        public partial int BlockFirmTradesForTimeInterval { get; set; }

        [Bindable(Default = 1500)]
        public partial double BlockAlreadyTradedSymbolsTimeout { get; set; }

        [Bindable]
        public partial bool BlockArea { get; set; }

        [Bindable(Default = 1000)]
        public partial double BlockAreaStrikeRange { get; set; }

        [Bindable(Default = 30)]
        public partial double BlockAreaAttemptBlockSeconds { get; set; }

        [Bindable(Default = 300.0)]
        public partial double BlockAreaWinnerBlockSeconds { get; set; }

        [Bindable(Default = 900.0)]
        public partial double BlockAreaLoserBlockSeconds { get; set; }

        [Bindable]
        public partial int AutoTraderResubmitCount { get; set; }

        [Bindable(Default = 10_000)]
        public partial int AutoTraderMaxAllowedOrders { get; set; }

        [Bindable(Default = 1_000)]
        public partial int AutoTraderMaxOrderRate { get; set; }

        [Bindable(Default = 30)]
        public partial int AutoTraderMaxOrderRateSec { get; set; }

        [Bindable(Default = true)]
        public partial bool AutoTraderCheckForVisualFilters { get; set; }

        [Bindable]
        public partial bool AudioAlertEnabled { get; set; }

        [Bindable]
        public partial Dictionary<string, string> ExchToRouteMap { get; set; }

        public string AudioAlertSound
        {
            get => _audioAlertSound;
            set
            {
                SetValue(ref _audioAlertSound, value);
                SoundManager.Play(_audioAlertSound);
            }
        }

        [Bindable]
        public partial EdgeScanFeedTradeFilterModel SelectedModel { get; set; }

        public ICommand FilterInNewOrderBookCommand
        {
            get
            {
                _filterInNewOrderBookCommand ??= new DelegateCommand<object>(FilterInNewOrderBook);
                return _filterInNewOrderBookCommand;
            }
        }

        public ICommand FilterInNewTradesModuleCommand
        {
            get
            {
                _searchInNewTradesModuleCommand ??= new DelegateCommand<object>(SearchInNewTradesModule);
                return _searchInNewTradesModuleCommand;
            }
        }

        public ICommand ChartSymbolBidAskIvCommand
        {
            get
            {
                _chartSymbolBidAskIvCommand ??= new DelegateCommand<object>(ChartSymbolBidAskIv);
                return _chartSymbolBidAskIvCommand;
            }
        }

        public EdgeScanFeedViewModel(OmsCore omsCore,
            ConfigBrowserViewModel configBrowserViewModel,
            TransactionConsumerModel transactionConsumerModel,
            BulletinBroker bulletinBroker,
            PortfolioManagerModel portfolioManagerModel,
            DominatorsManagerModel dominatorsManagerModel,
            NotificationManager notificationManager,
            IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
        {
            _bulletinBroker = bulletinBroker;
            _moduleFactory = moduleFactory;
            _alltrades = new FastObservableCollection<EdgeScanFeedModel>();
            _uniqueTrades = new FastObservableCollection<EdgeScanFeedModel>();
            _uniqueTradesList = new HashSet<string>();
            _summary = new EdgeScanFeedStatsModel()
            {
                User = omsCore.User.Username
            };
            _omsCore = omsCore;
            RunnerId = EdgeScanFeedRunnerIdFactory.Create();
            Trades = _alltrades;
            DominatorsManagerModel = dominatorsManagerModel;
            NotificationManager = notificationManager;
            CutoffTime = DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(12);
            SaveTime = DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(12);
            ExchToRouteMap = new Dictionary<string, string>
            {
                ["ISE"] = "MISE",
                ["CBOE"] = "MCBOE",
                ["PHLX"] = "MPHLX",
                ["ARCA"] = "MARCA",
                ["BOX"] = "MBOX",
                ["MIAX"] = "MMIAX",
                ["C2"] = "MC2",
                ["EDGX"] = "MEDGX",
                ["AMEX"] = "MAMEX",
                ["EMLD"] = "MEMLD",
                ["MCRY"] = "MMCRY",
                ["NOM"] = "MNASDAQ",
                ["BATS"] = "MBATS",
                ["U"] = "MMEMX",
                ["NQBX"] = "MNQBX",
                ["GEMX"] = "MGMNI",
                ["MPRL"] = "MPEARL",
                ["S"] = "MSPHR",
            };
            PortfolioManagerModel = portfolioManagerModel;
            TransactionConsumerModel = transactionConsumerModel;
            SelectedItems = new ObservableCollection<object>();
            OmsCore.GatewayClient.ConfigChangeEvent += GatewayClient_ConfigChangeEvent;

            BorderBrushColor = GetRandomBrush();
            BorderBrush = new SolidColorBrush(BorderBrushColor);

            StartUpTimeTimer();
        }

        private void GatewayClient_ConfigChangeEvent(ConfigSave configSave)
        {
            if (configSave != null)
            {
                if ((Module)configSave.Module == Module.EdgeScanFeedBanList && configSave.Id == BlockedSymbolModelId)
                {
                    LoadBlockedListFromConfig(configSave);
                }
            }
        }

        [Command]
        public void TradeExchToRouteMappingCommand()
        {
            try
            {
                ExchToRouteMapConfigView view = new();
                if (view.DataContext is ExchToRouteMapConfigViewModel viewModel)
                {
                    if (ExchToRouteMap != null)
                    {
                        foreach (var kvp in ExchToRouteMap)
                        {
                            viewModel.Mapping.Add(new ExchToRouteMapModel { Exchange = kvp.Key, Route = kvp.Value });
                        }
                    }

                    viewModel.MappingUpdated += map => ExchToRouteMap = map;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TradeExchToRouteMappingCommand));
            }
        }

        [Command]
        public void ShowBannedListManagerCommand()
        {
            try
            {
                EdgeScanFeedBannedSymbolsListManagerView view = new();
                if (view.DataContext is EdgeScanFeedBannedSymbolsListManagerViewModel viewModel)
                {
                    viewModel.Parent = this;
                    view.Closed += (_, _) => ReloadBannedSymbolsList();
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowBannedListManagerCommand));
            }
        }

        partial void OnServerRunnerStateChanged(EdgeScanFeedRunnerState value)
        {
            IsServerRunnerActive = value == EdgeScanFeedRunnerState.Running;
        }

        private async void ReloadBannedSymbolsList()
        {
            try
            {
                if (BlockedSymbolModelId > 0)
                {
                    ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(BlockedSymbolModelId);
                    LoadBlockedListFromConfig(details);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadBannedSymbolsList));
            }
        }

        private void LoadBlockedListFromConfig(ConfigSave config)
        {
            if (config != null)
            {
                BlockedSymbolModel model = JsonConvert.DeserializeObject<BlockedSymbolModel>(config.ConfigJson);
                model.Id = config.Id;
                model.Details = new()
                {
                    Id = config.Id,
                    OwnerId = config.OwnerId,
                    Username = config.Username,
                    SaveTime = config.SaveTime,
                    Module = config.Module,
                    ConfigJson = config.ConfigJson,
                    Title = config.Title,
                    Group = config.Group
                };
                model.UpdateSet();
                BlockedSymbolModel = model;
            }
        }

        private void StartUpTimeTimer()
        {
            _startTime = DateTime.Now;
            _upTimeUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1_000),
            };
            _upTimeUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _upTimeUpdateTimer.Tick += UpTimeTimerTick;
            _upTimeUpdateTimer.Start();
        }

        private void UpTimeTimerTick(object sender, EventArgs e)
        {
            TimeSpan delta = DateTime.Now - _startTime;
            UpTime = Math.Truncate(delta.TotalHours).ToString("00") + ":" + delta.Minutes.ToString("00") + ":" + delta.Seconds.ToString("00");

            CheckForAutoStop();
            SaveTrades();
            ClearOldItems();
            UpdateStats(true);
        }

        private void UpdateStats(bool mini)
        {
            if (!AutoTraderRunning && mini)
            {
                return;
            }

            if (AutoTraderRunning && !mini)
            {
                _summary.StartTime = DateTime.Now;
            }
            _summary.Timestamp = DateTime.Now;
            _summary.TotalSubs = _totalSubmission;
            _summary.TotalAttempts = _totalReceived;
            _summary.Received = _lastReceived;
            _summary.Submissions = _lastSubmission;
            _summary.ScannerConfig = SelectedModel?.Title ?? "";
            _summary.BasketConfig = AutoTraderBasketViewModel?.ModuleTitle ?? "";
            _summary.InstanceId = AutoTraderBasketViewModel?.BasketSettings?.Uid ?? "";
            _summary.State = (AutoTraderRunning ? "Running " : "Stopped ") + EdgeScanFeedRunnerOption.ToString();
            _omsCore.EdgeScannerClient.ScannerClient.SendEdgeScanFeedStatisticsSummary(_summary, mini);

            _lastReceived = 0;
            _lastSubmission = 0;
        }

        private void CheckForAutoStop()
        {
            if (AutoTraderRunning)
            {
                if (AutoStop)
                {
                    DateTime now = DateTime.Now;
                    if (now.Date.Day != _startTime.Date.Day ||
                        now.TimeOfDay >= CutoffTime.TimeOfDay)
                    {
                        AutoTraderRunning = false;
                    }
                }
            }
        }

        private void ClearOldItems()
        {
            if (AutoClearFeed)
            {
                var count = Trades.Count - AutoClearFeedCount;
                if (count > 0)
                {
                    var items = Trades.OrderBy(x => x.BuyTime).Take(count);
                    Dispatcher.BeginInvoke(() => items.ForEach(x => Trades.Remove(x)));
                }
            }
        }

        private void SaveTrades()
        {
            if (!_saved && DateTime.Now.TimeOfDay >= SaveTime.TimeOfDay && AutoSave)
            {
                _saved = true;
                if (Trades.Count > 0)
                {
                    Task.Run(() =>
                    {
                        List<EdgeScanFeedModel> trades = Trades.ToList();
                        string export = EdgeScanFeedModel.GetCsvHeader();
                        foreach (EdgeScanFeedModel item in trades)
                        {
                            export += item.GetCsv();
                        }

                        string title = "Edge Scan Feed Export " + DateTime.Now.ToString("MM-dd hh.mm") + ".csv";
                        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), title);

                        File.WriteAllText(path, export);
                    });
                }
            }
        }

        private void OnEdgeScanFeedReceivedEvent(EdgeScanFeedModel signalModel, EdgeScanFeedModel duplicateRowForSale)
        {
            signalModel.ReceiveTime = DateTime.Now;
            GetTradeSide(signalModel, out bool checkBuySide, out bool checkSellSide);

            bool matchFound = CheckForFilter(signalModel, checkBuySide, checkSellSide, out var error, out var filter);
            if (!matchFound)
            {
                if (!OmsCore.Config.IsDevMode && !DebugModeEnabled)
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(error))
                {
                    signalModel.Message = error;
                }
            }

            if (!_processed.TryAdd(signalModel.GetKey(), true))
            {
                return;
            }

            signalModel = new EdgeScanFeedModel(signalModel);
            bool addDuplicate = false;
            if (duplicateRowForSale != null)
            {
                duplicateRowForSale = new EdgeScanFeedModel(duplicateRowForSale)
                {
                    Message = "Duplicate Feed"
                };
                addDuplicate = true;
            }
            bool addUnique = _uniqueTradesList.Add(signalModel.SpreadId);

            Dispatcher.BeginInvoke(async () =>
            {
                _alltrades.Add(signalModel);
                if (addDuplicate)
                {
                    _alltrades.Add(duplicateRowForSale);
                }
                if (addUnique)
                {
                    _uniqueTrades.Add(signalModel);
                }

                if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Local && matchFound)
                {
                    await CheckForTrading(signalModel, checkBuySide, checkSellSide, filter);
                }

                if (AutoScrollEdgeFeed)
                {
                    LatestEdgeScanFeed = duplicateRowForSale ?? signalModel;
                }
            });

            CheckForSweepLogger(signalModel);
            CheckForAudioNotification(signalModel, duplicateRowForSale);
        }

        private void GetTradeSide(EdgeScanFeedModel signalModel, out bool checkBuySide, out bool checkSellSide)
        {
            checkBuySide = false;
            checkSellSide = false;
            switch (AutoTraderSideSelector)
            {
                case AutoTraderSideSelection.EdgeToTheo:
                    if (signalModel.EdgeScannerType == EdgeScannerType.FullAuto)
                    {
                        var buyEdgeToTheo = signalModel.BuyTradeTheo - signalModel.BuyPrice;
                        var sellEdgeToTheo = signalModel.BuyPrice - signalModel.BuyTradeTheo;
                        checkBuySide = buyEdgeToTheo >= sellEdgeToTheo;
                        checkSellSide = !checkBuySide;
                    }
                    else
                    {
                        checkBuySide = !double.IsNaN(signalModel.BuyEdgeToTheo) &&
                                       (signalModel.BuyEdgeToTheo >= signalModel.SellEdgeToTheo ||
                                        double.IsNaN(signalModel.SellEdgeToTheo));
                        checkSellSide = !checkBuySide && !double.IsNaN(signalModel.SellEdgeToTheo) &&
                                        (signalModel.BuyEdgeToTheo <= signalModel.SellEdgeToTheo ||
                                         double.IsNaN(signalModel.BuyEdgeToTheo));
                    }

                    break;
                case AutoTraderSideSelection.OlderTrade:
                    checkBuySide = signalModel.BuyTime.Date == DateTime.Today &&
                                   (signalModel.SellTime.Date != DateTime.Today ||
                                    signalModel.BuyTime <= signalModel.SellTime);
                    checkSellSide = !checkBuySide && signalModel.SellTime.Date == DateTime.Today &&
                                    (signalModel.BuyTime.Date != DateTime.Today ||
                                     signalModel.SellTime < signalModel.BuyTime);
                    break;
                case AutoTraderSideSelection.NewestTrade:
                    checkBuySide = signalModel.BuyTime.Date == DateTime.Today &&
                                   (signalModel.SellTime.Date != DateTime.Today ||
                                    signalModel.BuyTime >= signalModel.SellTime);
                    checkSellSide = !checkBuySide && signalModel.SellTime.Date == DateTime.Today &&
                                    (signalModel.BuyTime.Date != DateTime.Today ||
                                     signalModel.SellTime > signalModel.BuyTime);
                    break;
                case AutoTraderSideSelection.BuySide:
                    checkBuySide = true;
                    checkSellSide = false;
                    break;
                case AutoTraderSideSelection.SellSide:
                    checkBuySide = false;
                    checkSellSide = true;
                    break;
                case AutoTraderSideSelection.CloseToMarket:
                    switch (signalModel.EdgeScannerType)
                    {
                        case EdgeScannerType.FullAuto:
                            checkBuySide = signalModel.BuyBidPercent <= 0.5;
                            checkSellSide = !checkBuySide;
                            break;
                        case EdgeScannerType.SideScan:
                        case EdgeScannerType.EqSideScan:
                            checkBuySide = signalModel.AdjSide == Side.Buy;
                            checkSellSide = signalModel.AdjSide == Side.Sell;
                            break;
                        default:
                            checkBuySide = !double.IsNaN(signalModel.BuyBidPercent) && signalModel.BuyBidPercent <= .50 &&
                                           (signalModel.BuyBidPercent <= signalModel.SellBidPercent ||
                                            double.IsNaN(signalModel.SellBidPercent));
                            checkSellSide = !checkBuySide && !double.IsNaN(signalModel.SellBidPercent) &&
                                            signalModel.SellBidPercent < .50 &&
                                            (signalModel.BuyBidPercent > signalModel.SellBidPercent ||
                                             double.IsNaN(signalModel.BuyBidPercent));
                            break;
                    }

                    break;
            }

            switch (signalModel.EdgeScannerType)
            {
                case EdgeScannerType.FullAuto when AutoTraderAttemptBothSides:
                case EdgeScannerType.CrossedMarketMaker when AutoTraderAttemptBothSides:
                    checkBuySide = true;
                    checkSellSide = false;
                    break;
                case EdgeScannerType.OutOfMarketTrade:
                    checkBuySide = !double.IsNaN(signalModel.BuyPrice) &&
                                    double.IsNaN(signalModel.SellPrice);
                    checkSellSide = double.IsNaN(signalModel.BuyPrice) &&
                                   !double.IsNaN(signalModel.SellPrice);
                    break;
                case EdgeScannerType.SweepFinder:
                    if (signalModel.BuyBidPercent > .75 && signalModel.BuyEdgeToTheo < 0)
                    {
                        checkBuySide = true;
                        checkSellSide = false;
                    }
                    else if (signalModel.BuyBidPercent < .25 && signalModel.BuyEdgeToTheo > 0)
                    {
                        checkBuySide = false;
                        checkSellSide = true;
                    }
                    else
                    {
                        checkBuySide = false;
                        checkSellSide = false;
                    }
                    break;
                case EdgeScannerType.SideScan:
                case EdgeScannerType.EqSideScan:
                    checkBuySide &= signalModel.AdjSide == Side.Buy;
                    checkSellSide &= signalModel.AdjSide == Side.Sell;
                    break;
            }
        }

        private async void CheckForSweepLogger(EdgeScanFeedModel signalModel)
        {
            if (signalModel.EdgeScannerType == EdgeScannerType.SweepFinder)
            {
                BasketTraderItemModel basketItem = new(AutoTraderBasketViewModel, AutoTraderBasketViewModel.Dispatcher, OmsCore);
                await basketItem.LoadLegsFromTosAsync(signalModel.BuySymbol);
                signalModel.SetupLogger(basketItem, fullLog: EnableLogMode);
            }
        }

        private void CheckForAudioNotification(EdgeScanFeedModel signalModel, EdgeScanFeedModel duplicateRowForSale)
        {
            if (AudioAlertEnabled && !signalModel.IsFirm && !signalModel.PossibleFirm)
            {
                SoundManager.Play(AudioAlertSound);
            }
            if (MarkPrices && signalModel.DeltaAdjEdge >= MarkPricesMinEdge)
            {
                signalModel.ShowPriceNotification = true;
                if (duplicateRowForSale != null)
                {
                    duplicateRowForSale.ShowPriceNotification = true;
                }
            }
        }

        private async Task CheckForTrading(EdgeScanFeedModel signalModel, bool checkBuySide, bool checkSellSide, EdgeScanFeedTradeFilterRowModel filter)
        {
            if (!IsValidSignal(signalModel, checkBuySide, checkSellSide))
            {
                return;
            }

            if (!IsValidPosition(signalModel))
            {
                return;
            }

            if (!CheckForRecentTrades(signalModel))
            {
                return;
            }

            if (!CheckForBlockList(signalModel))
            {
                return;
            }

            if (!CheckForVisualFilters(signalModel))
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            BasketTraderItemModel basketItem = await LoadBasketItem(signalModel, checkBuySide, checkSellSide);

            try
            {
                _log.Info($"Source: Edge scan feed auto trader, Message: Basket Item Prep Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                stopwatch.Restart();
                if (!IsValidBasketItemAsync(signalModel, basketItem))
                {
                    return;
                }

                if (!ValidateEdgeAndSetSizeAndEdgeOverrides(signalModel, basketItem))
                {
                    return;
                }

                if (basketItem == null || basketItem.IsDisposed)
                {
                    throw new SlimException("Basket Item Disposed");
                }

                if (signalModel.EdgeScannerType == EdgeScannerType.CopyCatWithEdge || ConfirmWithIbCob)
                {
                    await CheckForIbCob(signalModel, checkBuySide, checkSellSide, basketItem);
                }

                stopwatch.Restart();
                var exchanges = signalModel.Exchange.Split(", ");
                if (AutoTraderRouteOption is AutoTraderRouteOption.FillExch or AutoTraderRouteOption.Both)
                {
                    string exchange = checkSellSide && exchanges.Length > 1 ? exchanges[1] : exchanges[0];

                    if (ExchToRouteMap != null && ExchToRouteMap.TryGetValue(exchange, out var route))
                    {
                        basketItem.RouteOverride = route;
                        basketItem.ResubmitWithRegularRoute = AutoTraderRouteOption == AutoTraderRouteOption.Both;
                    }
                }
                _log.Info($"Source: Edge scan feed auto trader, Message: Basket override setting Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                double maxPercentBid = double.NaN;
                bool usingEdgeLookup = false;
                double restOverride = double.NaN;
                double maxTheo = double.NaN;
                double maxVola = double.NaN;
                TheoModel volaModel = TheoModel.VolaV0;
                double maxEma = double.NaN;
                int qty = 1;
                stopwatch.Restart();
                _ = basketItem.WaitForMarkLoad().ContinueWith(async waitForMarkTask =>
                {
                    try
                    {
                        var basketTraderItemModel = basketItem;
                        if (!waitForMarkTask.Result)
                        {
                            throw new SlimException("Wait for data timeout");
                        }
                        _log.Info($"Source: Edge scan feed auto trader, Message: Data Load Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" +
                                  signalModel);

                        stopwatch.Restart();
                        if (!AutoTraderUseTradePrice && !await basketTraderItemModel.WaitForUnderMidLoadAsync())
                        {
                            throw new SlimException("Wait for Under data timeout");
                        }
                        _log.Info($"Source: Edge scan feed auto trader, Message: Under Data Load Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" +
                                  signalModel);

                        if (filter.LowLegMinDelta > 0 || filter.LowLegMaxDelta < 1 ||
                            filter.HighLegMinDelta > 0 || filter.HighLegMaxDelta < 1)
                        {
                            var deltaLoaded = await basketTraderItemModel.WaitForTheoLoadAsync();
                            if (!deltaLoaded)
                            {
                                throw new SlimException("Wait for Delta timeout");
                            }
                            var minLegDelta = basketTraderItemModel.Legs.Min(x => Math.Abs(x.Delta));
                            var maxLegDelta = basketTraderItemModel.Legs.Max(x => Math.Abs(x.Delta));

                            if ((filter.LowLegMinDelta > 0 && minLegDelta < filter.LowLegMinDelta) ||
                                (filter.LowLegMaxDelta < 1 && minLegDelta > filter.LowLegMaxDelta) ||
                                (filter.HighLegMinDelta > 0 && maxLegDelta < filter.HighLegMinDelta) ||
                                (filter.HighLegMaxDelta < 1 && maxLegDelta > filter.HighLegMaxDelta))
                            {
                                throw new SlimException("Leg Delta check failed");
                            }
                        }

                        double tradeDelta = checkBuySide ? signalModel.BuyTradeDelta : signalModel.SellTradeDelta;
                        double price = signalModel.BuyPrice;
                        double contraPrice = signalModel.SellPrice;
                        string reason = string.Empty;
                        bool sendMain = true;
                        bool sendContra = true;
                        AutomationConfigModel automationConfig = AutoTraderBasketViewModel.GetAutomationConfig(basketTraderItemModel.Underlying, (double)basketTraderItemModel.PriceIncrement);
                        switch (signalModel.EdgeScannerType)
                        {
                            case EdgeScannerType.PermAdjustedLoopFinder:
                                if (automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Dynamic &&
                                    automationConfig.DynamicEdgeModel != null &&
                                    automationConfig.DynamicEdgeModel.GetEdge(fish: true,
                                                                              basketTraderItemModel.BaseStrategy,
                                                                              signalModel.UnderSymbol,
                                                                              signalModel.BuyTradeUnderlyingMid,
                                                                              basketTraderItemModel.StrikeSpacing,
                                                                              basketTraderItemModel.DaysToExpiration,
                                                                              basketTraderItemModel.Contracts,
                                                                              await basketTraderItemModel.GetMinOfBidAndAskSize(),
                                                                              tradeDelta,
                                                                              basketTraderItemModel.Width,
                                                                              (double)basketTraderItemModel.PriceIncrement,
                                                                              basketTraderItemModel.GetWeightedVega,
                                                                              out double edge,
                                                                              out double loopMinEdge,
                                                                              out _,
                                                                              out maxTheo,
                                                                              out maxVola,
                                                                              out volaModel,
                                                                              out maxPercentBid,
                                                                              out maxEma,
                                                                              out double maxThroughTradePx,
                                                                              out double minMarketWidth,
                                                                              out double minMarketCross,
                                                                              out qty,
                                                                              out _,
                                                                              out reason))
                                {
                                    usingEdgeLookup = true;
                                    _log.Info($"Source: Edge scan feed auto trader, Message: Edge Selection Complete, Edge: {edge:F3}, Min Edge: {loopMinEdge:F3}, MaxTheo: {maxTheo:F3}, MaxVola: {maxVola:F3}, VolaModel: {volaModel}, Max % Bid: {maxPercentBid:F3}, MaxEma: {maxEma:F3}, MaxThroughTrade: {maxThroughTradePx:F3}, MinMarketWidth: {minMarketWidth:F3}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                    bool reversed;
                                    double baseTradePrice;
                                    if (checkBuySide)
                                    {
                                        baseTradePrice = price;
                                        reversed = ConfirmBuySide(basketTraderItemModel);
                                    }
                                    else
                                    {
                                        baseTradePrice = signalModel.Mleg ? -contraPrice : contraPrice;
                                        reversed = ConfirmSellSide(basketTraderItemModel);
                                    }

                                    if (reversed && signalModel.Mleg)
                                    {
                                        baseTradePrice = -baseTradePrice;
                                        tradeDelta = -tradeDelta;
                                    }

                                    edge = edge - signalModel.DeltaAdjEdge;

                                    price = baseTradePrice - edge;
                                    contraPrice = !signalModel.Mleg ? baseTradePrice + edge : baseTradePrice - edge;

                                    if (AutoTraderAttemptBothSides)
                                    {
                                        sendMain = true;
                                        sendContra = true;
                                    }
                                    else
                                    {
                                        sendMain = checkBuySide;
                                        sendContra = checkSellSide;
                                    }

                                    if (AutoTraderBasketViewModel.BasketSettings.MinBidCheckEnabled)
                                    {
                                        if (basketTraderItemModel.Low < AutoTraderBasketViewModel.BasketSettings.MinBidCheckBidValue)
                                        {
                                            throw new SlimException("Min Bid Check Failed");
                                        }
                                    }

                                    stopwatch.Restart();
                                    if (baseTradePrice < 0 && !signalModel.Mleg)
                                    {
                                        throw new SlimException("Invalid Px for single-leg");
                                    }
                                    basketTraderItemModel.CloseEdgeOveride = edge;
                                    _log.Info($"Source: Edge scan feed auto trader, Message: Full Auto edge set, Id: {signalModel.Description}, Buy Price: {price}, Sell Price: {contraPrice}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}");

                                    if (!double.IsNaN(minMarketWidth))
                                    {
                                        stopwatch.Restart();
                                        double width = basketTraderItemModel.High - basketTraderItemModel.Low;
                                        if (width < minMarketWidth)
                                        {
                                            throw new SlimException("Min Market Width Check Failed");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Width Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                    }

                                    if (!double.IsNaN(maxThroughTradePx))
                                    {
                                        stopwatch.Restart();
                                        if (Math.Abs(price - baseTradePrice) > maxThroughTradePx)
                                        {
                                            throw new SlimException("Buy px crosses original trade px.");
                                        }
                                        if (Math.Abs(baseTradePrice - contraPrice) > maxThroughTradePx)
                                        {
                                            throw new SlimException("Sell px crosses original trade px.");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Trade Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                    }

                                    stopwatch.Restart();
                                    if (checkBuySide)
                                    {
                                        basketTraderItemModel.SetPrice(price);
                                        basketTraderItemModel.AveragePrice = price;
                                        basketTraderItemModel.BestAveragePrice = price;
                                        basketTraderItemModel.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                                        basketTraderItemModel.EdgeScanFeedSellPrice = signalModel.SellPrice;
                                        basketTraderItemModel.EdgeScanFeedUnderlying = signalModel.BuyTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;
                                    }
                                    else if (checkSellSide)
                                    {
                                        basketTraderItemModel.SetPrice(contraPrice);
                                        basketTraderItemModel.AveragePrice = contraPrice;
                                        basketTraderItemModel.BestAveragePrice = contraPrice;
                                        basketTraderItemModel.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                                        basketTraderItemModel.EdgeScanFeedSellPrice = signalModel.SellPrice;
                                        basketTraderItemModel.EdgeScanFeedUnderlying = signalModel.SellTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtFill = signalModel.SellTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.SellTradeUnderlyingMid;
                                    }
                                    else
                                    {
                                        throw new SlimException("Edge Setter Failed");
                                    }
                                }
                                else
                                {
                                    throw new SlimException("Edge Setter Failed");
                                }
                                break;
                            case EdgeScannerType.FullAuto:
                                if (automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Dynamic &&
                                    automationConfig.DynamicEdgeModel != null &&
                                    automationConfig.DynamicEdgeModel.GetEdge(fish: true,
                                                                              basketTraderItemModel.BaseStrategy,
                                                                              signalModel.UnderSymbol,
                                                                              signalModel.BuyTradeUnderlyingMid,
                                                                              basketTraderItemModel.StrikeSpacing,
                                                                              basketTraderItemModel.DaysToExpiration,
                                                                              basketTraderItemModel.Contracts,
                                                                              await basketTraderItemModel.GetMinOfBidAndAskSize(),
                                                                              tradeDelta,
                                                                              basketTraderItemModel.Width,
                                                                              (double)basketTraderItemModel.PriceIncrement,
                                                                              basketTraderItemModel.GetWeightedVega,
                                                                              out edge,
                                                                              out loopMinEdge,
                                                                              out _,
                                                                              out maxTheo,
                                                                              out maxVola,
                                                                              out volaModel,
                                                                              out maxPercentBid,
                                                                              out maxEma,
                                                                              out maxThroughTradePx,
                                                                              out minMarketWidth,
                                                                              out minMarketCross,
                                                                              out qty,
                                                                              out _,
                                                                              out reason))
                                {
                                    usingEdgeLookup = true;
                                    _log.Info($"Source: Edge scan feed auto trader, Message: Edge Selection Complete, Edge: {edge:F3}, Min Edge: {loopMinEdge:F3}, MaxTheo: {maxTheo:F3}, MaxVola: {maxVola:F3}, VolaModel: {volaModel}, Max % Bid: {maxPercentBid:F3}, MaxEma: {maxEma:F3}, MaxThroughTrade: {maxThroughTradePx:F3}, MinMarketWidth: {minMarketWidth:F3}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                    tradeDelta = signalModel.BuyTradeDelta;
                                    double baseTradePrice = price;

                                    bool reversed = ConfirmBuySide(basketTraderItemModel);

                                    if (reversed && signalModel.Mleg)
                                    {
                                        baseTradePrice = -baseTradePrice;
                                        tradeDelta = -tradeDelta;
                                    }

                                    if (AutoTraderAttemptBothSides)
                                    {
                                        sendMain = true;
                                        sendContra = true;
                                    }
                                    else
                                    {
                                        sendMain = checkBuySide;
                                        sendContra = checkSellSide;
                                    }

                                    if (baseTradePrice < 0 && !signalModel.Mleg)
                                    {
                                        throw new SlimException("Invalid Px for single-leg");
                                    }

                                    price = baseTradePrice - edge;
                                    contraPrice = !signalModel.Mleg ? baseTradePrice + edge : -baseTradePrice - edge;
                                    basketTraderItemModel.CloseEdgeOveride = edge;
                                    _log.Info($"Source: Edge scan feed auto trader, Message: Full Auto edge set. Id: {signalModel.Description}, Buy Price: {price}, Sell Price: {contraPrice}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                    if (!double.IsNaN(minMarketWidth))
                                    {
                                        stopwatch.Restart();
                                        double width = basketTraderItemModel.High - basketTraderItemModel.Low;
                                        if (width < minMarketWidth)
                                        {
                                            throw new SlimException("Min Market Width Check Failed");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Width Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                    }

                                    if (!double.IsNaN(maxThroughTradePx))
                                    {
                                        stopwatch.Restart();
                                        if (Math.Abs(price - baseTradePrice) > maxThroughTradePx)
                                        {
                                            throw new SlimException("Buy px crosses original trade px.");
                                        }
                                        if (Math.Abs(baseTradePrice - contraPrice) > maxThroughTradePx)
                                        {
                                            throw new SlimException("Sell px crosses original trade px.");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Trade Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                    }

                                    basketTraderItemModel.SetPrice(price);
                                    basketTraderItemModel.AveragePrice = price;
                                    basketTraderItemModel.BestAveragePrice = price;
                                    basketTraderItemModel.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                                    basketTraderItemModel.EdgeScanFeedSellPrice = signalModel.SellPrice;
                                    basketTraderItemModel.EdgeScanFeedUnderlying = signalModel.BuyTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;
                                }
                                else
                                {
                                    signalModel.Reason = reason;
                                    throw new SlimException("Edge Setter Failed");
                                }
                                break;
                            case EdgeScannerType.CrossedMarketMaker:
                                if (automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Dynamic)
                                {
                                    tradeDelta = checkBuySide ? Math.Abs(signalModel.BuyTradeDelta) : Math.Abs(signalModel.SellTradeDelta);
                                    if (automationConfig.DynamicEdgeModel.GetEdge(fish: true,
                                                                                  basketTraderItemModel.BaseStrategy,
                                                                                  signalModel.UnderSymbol,
                                                                                  signalModel.BuyTradeUnderlyingMid,
                                                                                  basketTraderItemModel.StrikeSpacing,
                                                                                  basketTraderItemModel.DaysToExpiration,
                                                                                  basketTraderItemModel.Contracts,
                                                                                  await basketTraderItemModel.GetMinOfBidAndAskSize(),
                                                                                  tradeDelta,
                                                                                  basketTraderItemModel.Width,
                                                                                  (double)basketTraderItemModel.PriceIncrement,
                                                                                  basketTraderItemModel.GetWeightedVega,
                                                                                  out edge,
                                                                                  out loopMinEdge,
                                                                                  out _,
                                                                                  out maxTheo,
                                                                                  out maxVola,
                                                                                  out volaModel,
                                                                                  out maxPercentBid,
                                                                                  out maxEma,
                                                                                  out maxThroughTradePx,
                                                                                  out minMarketWidth,
                                                                                  out minMarketCross,
                                                                                  out qty,
                                                                                  out _,
                                                                                  out reason))
                                    {
                                        if (!double.IsNaN(minMarketCross))
                                        {
                                            stopwatch.Restart();
                                            double width = signalModel.SellPrice - signalModel.BuyPrice;
                                            if (width < minMarketCross)
                                            {
                                                throw new SlimException("Min Cross Width Check Failed");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Width Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        if (!double.IsNaN(minMarketWidth))
                                        {
                                            stopwatch.Restart();
                                            double width = basketTraderItemModel.High - basketTraderItemModel.Low;
                                            if (width < minMarketWidth)
                                            {
                                                throw new SlimException("Min Market Width Check Failed");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Width Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }
                                    }
                                }

                                if (checkBuySide)
                                {
                                    ConfirmBuySide(basketTraderItemModel);

                                    basketTraderItemModel.SetPrice(price);
                                    basketTraderItemModel.AveragePrice = price;
                                    basketTraderItemModel.BestAveragePrice = price;
                                    basketTraderItemModel.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                                    basketTraderItemModel.EdgeScanFeedSellPrice = signalModel.SellPrice;
                                    basketTraderItemModel.EdgeScanFeedUnderlying = signalModel.BuyTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;
                                }
                                else if (checkSellSide)
                                {
                                    ConfirmSellSide(basketTraderItemModel);

                                    basketTraderItemModel.SetPrice(contraPrice);
                                    basketTraderItemModel.AveragePrice = contraPrice;
                                    basketTraderItemModel.BestAveragePrice = contraPrice;
                                    basketTraderItemModel.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                                    basketTraderItemModel.EdgeScanFeedSellPrice = signalModel.SellPrice;
                                    basketTraderItemModel.EdgeScanFeedUnderlying = signalModel.SellTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtFill = signalModel.SellTradeUnderlyingMid;
                                    basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.SellTradeUnderlyingMid;
                                }
                                break;
                            case EdgeScannerType.CopyCatWithEdge:
                                var ibCobBid = basketTraderItemModel.TwsPrice;
                                var ibCobAsk = basketTraderItemModel.IsSingleLeg ? basketTraderItemModel.TwsContraPrice : -basketTraderItemModel.TwsContraPrice;
                                var liveUnderMid = (basketTraderItemModel.UnderBid + basketTraderItemModel.UnderAsk) / 2;
                                var threshold = (double)(basketTraderItemModel.PriceIncrement * 2);
                                if (checkBuySide)
                                {
                                    stopwatch.Restart();
                                    if (ibCobBid - basketTraderItemModel.Low >= threshold)
                                    {
                                        ConfirmBuySide(basketTraderItemModel);

                                        basketTraderItemModel.SetPrice(ibCobBid);
                                        basketTraderItemModel.AveragePrice = ibCobBid;
                                        basketTraderItemModel.BestAveragePrice = ibCobBid;
                                        basketTraderItemModel.EdgeScanFeedBuyPrice = ibCobBid;
                                        basketTraderItemModel.EdgeScanFeedSellPrice = ibCobAsk;
                                        basketTraderItemModel.EdgeScanFeedUnderlying = liveUnderMid;
                                        basketTraderItemModel.LastMainUnderMidAtFill = liveUnderMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = liveUnderMid;
                                    }
                                    else
                                    {
                                        throw new SlimException($"IB Px Check Failed on Buy. IB: [{ibCobBid}X{ibCobAsk}], Mkt: [{basketTraderItemModel.Low}X{basketTraderItemModel.High}]");
                                    }
                                }
                                else if (checkSellSide)
                                {
                                    stopwatch.Restart();
                                    if (basketTraderItemModel.IsSingleLeg ?
                                            (basketTraderItemModel.High - ibCobAsk >= threshold) :
                                            (ibCobAsk - basketTraderItemModel.Low >= threshold))
                                    {
                                        ConfirmSellSide(basketTraderItemModel);

                                        basketTraderItemModel.SetPrice(ibCobAsk);
                                        basketTraderItemModel.AveragePrice = ibCobAsk;
                                        basketTraderItemModel.BestAveragePrice = ibCobAsk;
                                        basketTraderItemModel.EdgeScanFeedBuyPrice = ibCobBid;
                                        basketTraderItemModel.EdgeScanFeedSellPrice = ibCobAsk;
                                        basketTraderItemModel.EdgeScanFeedUnderlying = liveUnderMid;
                                        basketTraderItemModel.LastMainUnderMidAtFill = liveUnderMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = liveUnderMid;
                                    }
                                    else
                                    {
                                        throw new SlimException($"IB Px Check Failed on Sell. IB: [{ibCobBid}X{ibCobAsk}], Mkt: [{basketTraderItemModel.Low}X{basketTraderItemModel.High}]");
                                    }
                                }
                                else
                                {
                                    throw new SlimException("Side not found!");
                                }
                                break;
                            default:
                                bool success = SetPrice(signalModel, checkBuySide, checkSellSide, basketTraderItemModel);
                                if (!success)
                                {
                                    throw new SlimException("Set edge failed!");
                                }
                                break;
                        }

                        _log.Info($"Source: Edge scan feed auto trader, Message: Side Evaluation and Edge Setting Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                        double timespan = 0;
                        if (signalModel.BuyTime.Year == DateTime.Today.Year && signalModel.SellTime.Year == DateTime.Today.Year)
                        {
                            timespan = Math.Abs((signalModel.BuyTime - signalModel.SellTime).TotalMilliseconds);
                        }

                        if (AutoTraderSkipActiveOrders || SecondaryOrderRestPeriod > 0)
                        {
                            stopwatch.Restart();
                            if (AutoTraderBasketViewModel.ActiveEdgeScanFeedOrderExistsForDescription(basketTraderItemModel, out OrderTicket other))
                            {
                                stopwatch.Restart();
                                if (AutoTraderSkipActiveOrders)
                                {
                                    throw new SlimException("Active Order Found For Symbol");
                                }
                                else
                                {
                                    if (other.Side == basketTraderItemModel.Side)
                                    {
                                        double adjPrice = ((basketTraderItemModel.UnderMid - signalModel.BuyTradeUnderlyingMid) * tradeDelta) + price;
                                        if ((basketTraderItemModel.IsSingleLegSell && adjPrice > other.Price) ||
                                            (!basketTraderItemModel.IsSingleLegSell && adjPrice < other.Price))
                                        {
                                            bool closed = await other.OrderClosedEvent.WaitOneAsync(2000);
                                            if (!closed || other.IsActive)
                                            {
                                                throw new SlimException("Active Order Wait Failed");
                                            }
                                            else if (SecondaryOrderRestPeriod > 0)
                                            {
                                                restOverride = SecondaryOrderRestPeriod;
                                            }
                                        }
                                        else
                                        {
                                            throw new SlimException("Active Order With Better Px Found");
                                        }
                                    }
                                    else
                                    {
                                        throw new SlimException("Active Opp Order Found");
                                    }
                                }
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Active Order Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        if (BlockArea)
                        {
                            stopwatch.Restart();
                            foreach (TicketLegModel leg in basketTraderItemModel.Legs)
                            {
                                DateTime expiration = leg.ExpirationInfo.Expiration;
                                double strike = leg.Strike.Strike;
                                Tuple<string, DateTime> key = Tuple.Create(basketTraderItemModel.Underlying, expiration);

                                if (!_lastTradeAreaTimeMap.TryGetValue(key, out Dictionary<double, DateTime> dict))
                                {
                                    dict = new Dictionary<double, DateTime>();
                                    _lastTradeAreaTimeMap[key] = dict;
                                }

                                foreach (KeyValuePair<double, DateTime> kvp in dict)
                                {
                                    if (Math.Abs(kvp.Key - strike) < BlockAreaStrikeRange)
                                    {
                                        if (kvp.Value > DateTime.Now)
                                        {
                                            throw new SlimException("Area Already Attempted.");
                                        }
                                    }
                                }

                                dict[strike] = DateTime.Now + TimeSpan.FromSeconds(BlockAreaAttemptBlockSeconds);
                            }

                            basketTraderItemModel.EdgeAcquiredEvent += OnItemEdgeAcquiredEvent;
                            _log.Info($"Source: Edge scan feed auto trader, Message: Block Area Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        if ((AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled || AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled) && OmsCore.Config.PriceCacheClearIntervalEnabled)
                        {
                            if (AutoTraderBasketViewModel.BasketSettings.MinTimeToPreviousAttemptCheckEnabled)
                            {
                                stopwatch.Restart();
                                bool failed = CheckForRecentAttempt(basketTraderItemModel);
                                if (failed)
                                {
                                    throw new SlimException("Recent attempt found for perm");
                                }
                            }

                            if (AutoTraderBasketViewModel.BasketSettings.MinTimeToPermLoserCheckEnabled)
                            {
                                stopwatch.Restart();
                                bool failed = CheckForPermLoserAttempt(basketTraderItemModel);
                                if (failed)
                                {
                                    throw new SlimException("Recent perm loser found");
                                }
                            }

                            if (basketTraderItemModel.TryGetPriceCache(out PriceCache chain))
                            {
                                stopwatch.Restart();
                                double edge = basketTraderItemModel.CloseEdgeOveride;
                                if (basketTraderItemModel.IsSingleLeg)
                                {
                                    double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                    double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                    if (basketTraderItemModel.Side == Side.Buy)
                                    {
                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjAsk))
                                            {
                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}, Edge: {edge}";
                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses min edge from last attempt passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for px cross min edge from last attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                        }

                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjBid))
                                            {
                                                if (basketTraderItemModel.Price < adjBid)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}";
                                                    throw new SlimException("Px crosses prev attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses prev attempt check passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for prev attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjBid))
                                            {
                                                if (basketTraderItemModel.Price < adjBid + edge)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses min edge from last attempt passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for px cross min edge from last attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                            }
                                        }

                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                        {
                                            if (!double.IsNaN(adjAsk))
                                            {
                                                stopwatch.Restart();
                                                if (basketTraderItemModel.Price > adjAsk)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                    throw new SlimException("Px crosses prev attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses prev attempt check passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for prev attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (basketTraderItemModel.Side == Side.Buy)
                                    {
                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);

                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjAsk))
                                            {
                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}, Edge: {edge}";
                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses min edge from last attempt passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for px cross min edge from last attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }

                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjBid))
                                            {
                                                if (basketTraderItemModel.Price < adjBid)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}";
                                                    throw new SlimException("Px crosses prev attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses prev attempt check passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for prev attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);
                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);

                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjBid))
                                            {
                                                if (basketTraderItemModel.Price > -(adjBid + edge))
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses min edge from last attempt passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for px cross min edge from last attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }

                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            if (!double.IsNaN(adjAsk))
                                            {
                                                if (basketTraderItemModel.Price < adjAsk)
                                                {
                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                    throw new SlimException("Px crosses prev attempt");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Px crosses prev attempt check passed. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                            else
                                            {
                                                _log.Info($"Source: Edge scan feed auto trader, Message: value not found for prev attempt check. Px: {basketTraderItemModel.Price}, Adj Px: {adjBid}X{adjAsk}, Edge: {edge}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }
                                    }
                                }
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Prev Attempt Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        if (AutoTraderMaxOpenPos > 0)
                        {
                            stopwatch.Restart();
                            if (AutoTraderBasketViewModel.GetTotalOpenPos() > AutoTraderMaxOpenPos)
                            {
                                throw new SlimException("Max Open Pos Reached");
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Max Open Pos Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        string areaKey = signalModel.SpreadId;
                        if (BlockAlreadyTradedSymbols)
                        {
                            bool found;
                            if (signalModel.EdgeScannerType == EdgeScannerType.PermAdjustedLoopFinder)
                            {
                                areaKey = signalModel.UnderSymbol + signalModel.NearExpiration + signalModel.FarExpiration;
                            }

                            ReferenceTradeModel reference;
                            lock (_lastTradeTimeMapLock)
                            {
                                found = _lastTradeTimeMap.TryGetValue(areaKey, out reference);
                            }

                            stopwatch.Restart();
                            if (found)
                            {
                                double lastTraded = Math.Round((DateTime.Now - reference.TradeTime).TotalMilliseconds, 2);
                                if (lastTraded < BlockAlreadyTradedSymbolsTimeout)
                                {
                                    if (signalModel.EdgeScannerType == EdgeScannerType.PermAdjustedLoopFinder)
                                    {
                                        throw new SlimException("Area Already Traded ({lastTraded})");
                                    }

                                    if (reference.TryGetAdjustedTradePrice(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta, out double adjPrice))
                                    {
                                        if (basketTraderItemModel.IsSingleLegSell)
                                        {
                                            if (basketTraderItemModel.Price <= adjPrice)
                                            {
                                                throw new SlimException("Already Traded ({lastTraded})");
                                            }
                                        }
                                        else
                                        {
                                            if (basketTraderItemModel.Price >= adjPrice)
                                            {
                                                throw new SlimException("Already Traded ({lastTraded})");
                                            }
                                        }
                                    }
                                }
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Block Traded Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        stopwatch.Restart();
                        if (!await AutoTraderBasketViewModel.AddToBasketAsync(basketTraderItemModel, ignoreDuplicateCheck: true, setPriceAfterCheck: false))
                        {
                            throw new SlimException("Invalid Basket Item");
                        }
                        _log.Info($"Source: Edge scan feed auto trader, Message: Basket Item Load Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                        stopwatch.Restart();
                        if (_totalSubmissionRateSec >= AutoTraderMaxOrderRateSec)
                        {
                            AutoTraderRunning = false;
                            _ = ShowMessage("Max order submission rate breached!");
                            throw new SlimException("Max Order Rate Breached");
                        }

                        if (_totalSubmissionRate >= AutoTraderMaxOrderRate)
                        {
                            RiskWarningMessageResponse res = await ShowMessage("Max order rate limit reached.\nWould you like to clear the limit and continue?");
                            if (res == RiskWarningMessageResponse.Proceed)
                            {
                                _orderRateLastUpdateTime = DateTime.Now;
                                _totalSubmissionRate = 0;
                            }
                        }

                        if (AutoTraderBasketViewModel.BasketSettings.MaxRestingOrdersEnabled &&
                            AutoTraderBasketViewModel.GetRestingOrdersCount() >= AutoTraderBasketViewModel.BasketSettings.MaxRestingOrdersCount)
                        {
                            throw new SlimException("Max Resting Check Failed!");
                        }

                        if (_totalSubmission >= AutoTraderMaxAllowedOrders)
                        {
                            _ = ShowMessage("Max order submission limit reached.");
                        }

                        if (qty <= 0)
                        {
                            qty = 1;
                        }

                        stopwatch.Restart();
                        if (basketTraderItemModel.Lcd != qty)
                        {
                            basketTraderItemModel.UpdateQty(qty);
                            _log.Info($"Source: Edge scan feed auto trader, Message: Qty Update Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        if (signalModel.EdgeScannerType == EdgeScannerType.FullAuto)
                        {
                            double deltaAdjBuyPrice = ((basketTraderItemModel.UnderMid - signalModel.BuyTradeUnderlyingMid) * tradeDelta) + price;
                            var updated = false;
                            if (!double.IsNaN(maxTheo))
                            {
                                stopwatch.Restart();
                                if (await basketTraderItemModel.WaitForAdjTheoLoadAsync())
                                {
                                    var edgeToTheo = basketTraderItemModel.IsSingleLegSell ? basketTraderItemModel.NetDeltaAdjTheo - deltaAdjBuyPrice : deltaAdjBuyPrice - basketTraderItemModel.NetDeltaAdjTheo;
                                    if (edgeToTheo > maxTheo)
                                    {
                                        _log.Info($"Theo Cross Adj B. Id: {signalModel.Description}, Buy Price: {price}, Theo: {basketTraderItemModel.NetDeltaAdjTheo}, Limit: {maxTheo}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        price = basketTraderItemModel.IsSingleLegSell ? basketTraderItemModel.NetDeltaAdjTheo - maxTheo : basketTraderItemModel.NetDeltaAdjTheo + maxTheo;
                                        signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                        updated = true;
                                    }
                                }
                                else
                                {
                                    throw new SlimException("Wait for theo timeout");
                                }
                                _log.Info($"Source: Edge scan feed auto trader, Message: Theo Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                            }

                            if (!double.IsNaN(maxVola))
                            {
                                stopwatch.Restart();
                                if (await basketTraderItemModel.WaitForAdjTheoLoadAsync())
                                {
                                    var result = await basketTraderItemModel.GetTheoAsync(volaModel, false, OmsCore.Config.PerformanceModeEnabled);
                                    var vola = result.NetDeltaAdjTheo;
                                    var edgeToVola = basketTraderItemModel.IsSingleLegSell ? vola - deltaAdjBuyPrice : deltaAdjBuyPrice - vola;
                                    if (edgeToVola > maxVola)
                                    {
                                        _log.Info($"Vola Cross Adj B. Id: {signalModel.Description}, Buy Price: {price}, Vola: {vola}, Limit: {maxVola}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        price = basketTraderItemModel.IsSingleLegSell ? vola - maxVola : vola + maxVola;
                                        signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                        updated = true;
                                    }
                                }
                                else
                                {
                                    throw new SlimException("Wait for Vola timeout");
                                }
                                _log.Info($"Source: Edge scan feed auto trader, Message: Vola Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                            }

                            if (!double.IsNaN(maxEma))
                            {
                                if (!basketTraderItemModel.SubscribedToEma)
                                {
                                    _log.Info(
                                        $"Source: Edge scan feed auto trader, Message: Not subscribed to EMA, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" +
                                        signalModel);
                                }
                                else
                                {
                                    stopwatch.Restart();
                                    if (await basketTraderItemModel.WaitForEmaLoad())
                                    {
                                        double ema = basketTraderItemModel.GetEma(OmsCore.Config.PerformanceModeEnabled);
                                        var edgeToEma = basketTraderItemModel.IsSingleLegSell ? ema - deltaAdjBuyPrice : deltaAdjBuyPrice - ema;
                                        if (edgeToEma > maxEma)
                                        {
                                            _log.Info($"EMA Cross Adj. Id: {signalModel.Description}, Buy Price: {price}, Theo: {ema}, Limit: {maxEma}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            price = basketTraderItemModel.IsSingleLegSell ? ema - maxEma : ema + maxEma;
                                            signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                            updated = true;
                                        }
                                    }
                                    else
                                    {
                                        throw new SlimException("Wait for ema timeout");
                                    }
                                    _log.Info($"Source: Edge scan feed auto trader, Message: Ema Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                }
                            }

                            if (!double.IsNaN(maxPercentBid))
                            {
                                stopwatch.Restart();
                                if (await basketTraderItemModel.WaitForMarkLoad())
                                {
                                    var calculateBidPercent = basketTraderItemModel.CalculateBidPercent(maxPercentBid, overrideEdge: false);
                                    double bidPercentPrice = calculateBidPercent.Price;
                                    bool valid = basketTraderItemModel.IsSingleLegSell ? deltaAdjBuyPrice >= bidPercentPrice : deltaAdjBuyPrice <= bidPercentPrice;
                                    if (!valid)
                                    {
                                        price = bidPercentPrice;
                                        signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                        updated = true;
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Percent Bid Check Override, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                    }
                                }
                                else
                                {
                                    throw new SlimException("Wait for data timeout");
                                }
                                _log.Info($"Source: Edge scan feed auto trader, Message: Percent Bid Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                            }

                            if (updated)
                            {
                                basketTraderItemModel.SetPrice(price);
                                basketTraderItemModel.AveragePrice = price;
                                basketTraderItemModel.BestAveragePrice = price;
                                basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;
                            }
                        }

                        double basePrice = checkBuySide ? price : contraPrice;
                        double baseUnderlying = checkBuySide ? signalModel.BuyTradeUnderlyingMid : signalModel.SellTradeUnderlyingMid;
                        double underMid = basketTraderItemModel.UnderMid;
                        double deltaAdjPrice = ((underMid - baseUnderlying) * tradeDelta) + basePrice;

                        string log = "Under Mid: " + underMid + ", Fill Under Mid: " + baseUnderlying + ", Delta: " + tradeDelta + ", Fill Px" + basePrice;
                        _log.Info("Delta Adj Edge Scan Feed. " + log + $", Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                        double totalMilliseconds = (DateTime.Now - signalModel.CreationTime).TotalMilliseconds;
                        basketTraderItemModel.ModuleTypeSuffix = $"`{basketTraderItemModel.CloseEdgeOveride:F2}`{timespan:F0}`{signalModel.BuyConditionCode}`{deltaAdjPrice:F2}`{signalModel.BuyQty}`{signalModel.BuyPrice:F2}`{signalModel.BuyTime.ToUnixEpoch()}`{signalModel.SellQty}`{signalModel.SellPrice:F2}`{signalModel.SellTime.ToUnixEpoch()}`{totalMilliseconds:F2}`{(int)signalModel.EdgeScannerType}`{EDGE_SCAN_FEED_SUBTYPE}";
                        basketTraderItemModel.EdgeScanFeedRespondLatency = totalMilliseconds;

                        ReferenceTradeModel referenceTradeModel = new()
                        {
                            TradeBid = checkBuySide ? signalModel.BuyTradeBid : signalModel.SellTradeBid,
                            TradeAsk = checkBuySide ? signalModel.BuyTradeAsk : signalModel.SellTradeAsk,
                            TradePrice = basePrice,
                            TradeUnderMid = baseUnderlying,
                            TradeTime = DateTime.Now,
                        };

                        basketTraderItemModel.ClearOrderDetails();
                        if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker &&
                            !string.IsNullOrWhiteSpace(signalModel.ExtraTag))
                        {
                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade", signalModel.ExtraTag);
                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade Time", signalModel.BuyTime.ToString("hh:mm:ss.ffffff") + "," + signalModel.SellTime.ToString("hh:mm:ss.ffffff"));
                        }

                        bool checkForSkewAdjEdge = true;
                        bool checkForSkewAdjCrossEdge = true;

                        if (sendMain)
                        {
                            if (BlockAlreadyTradedSymbols)
                            {
                                stopwatch.Restart();
                                if (AutoTraderBasketViewModel.GetBasketRestingOrdersCount(basketTraderItemModel.SpreadId) > 0)
                                {
                                    throw new SlimException("Open Order Found");
                                }


                                bool found;
                                ReferenceTradeModel reference;
                                lock (_lastTradeTimeMapLock)
                                {
                                    found = _lastTradeTimeMap.TryGetValue(areaKey, out reference);
                                    if (!found)
                                    {
                                        _lastTradeTimeMap[areaKey] = referenceTradeModel;
                                    }
                                }


                                if (found)
                                {
                                    double lastTraded = Math.Round((DateTime.Now - reference.TradeTime).TotalMilliseconds, 2);
                                    if (lastTraded < AutoTraderBasketViewModel.BasketSettings.CancelWithTimer)
                                    {
                                        throw new SlimException("Recently Traded ({lastTraded})");
                                    }
                                }
                            }

                            if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEnabled)
                            {
                                stopwatch.Restart();
                                double edge = basketTraderItemModel.CloseEdgeOveride + AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEdge;
                                if (basketTraderItemModel.IsSingleLeg)
                                {
                                    await basketTraderItemModel.SetPriceIncrementAsync();
                                }
                                double minTick = (double)basketTraderItemModel.PriceIncrement;
                                if (edge < minTick)
                                {
                                    edge = minTick;
                                }

                                if (basketTraderItemModel.IsSingleLegSell)
                                {
                                    if (deltaAdjPrice <= basketTraderItemModel.Low + edge)
                                    {
                                        throw new SlimException("Px crosses min edge from mkt");
                                    }
                                }
                                else
                                {
                                    if (deltaAdjPrice >= basketTraderItemModel.High - edge)
                                    {
                                        throw new SlimException("Px crosses min edge from mkt");
                                    }
                                }
                                _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Market Check Complete. Edge: {edge}, Inc: {minTick}, Px: {deltaAdjPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                            }

                            if (AutoTraderBasketViewModel.BasketSettings.IgnoreSkewMktCheckIfBothSidesFail)
                            {
                                checkForSkewAdjEdge =
                                    (await basketTraderItemModel.IsValidEdgeToSkewAdjMarketAsync(deltaAdjPrice)).IsValid! ^
                                    (await basketTraderItemModel.IsValidEdgeToSkewAdjMarketAsync(contraPrice, true)).IsValid;
                                checkForSkewAdjCrossEdge =
                                    (await basketTraderItemModel.IsValidEdgeToSkewAdjMarketCrossAsync(deltaAdjPrice, basketTraderItemModel.CloseEdgeOveride)).IsValid! ^
                                    (await basketTraderItemModel.IsValidEdgeToSkewAdjMarketCrossAsync(contraPrice, basketTraderItemModel.CloseEdgeOveride, true)).IsValid;
                            }

                            stopwatch.Restart();
                            if (checkForSkewAdjEdge && !(await basketTraderItemModel.IsValidEdgeToSkewAdjMarketAsync(deltaAdjPrice)).IsValid)
                            {
                                throw new SlimException("Px crosses min edge from skew mkt");
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Skew Market Check Complete. Px: {deltaAdjPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Skew Mkt: {basketTraderItemModel.HighestBid}X{basketTraderItemModel.LowestAsk}, Time: {stopwatch.ElapsedMilliseconds}, TotalTime: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);

                            stopwatch.Restart();
                            if (checkForSkewAdjCrossEdge && !(await basketTraderItemModel.IsValidEdgeToSkewAdjMarketCrossAsync(deltaAdjPrice, basketTraderItemModel.CloseEdgeOveride)).IsValid)
                            {
                                throw new SlimException("Px crosses min edge from skew mkt cross");
                            }
                            _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Skew Market Cross Check Complete. Px: {deltaAdjPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Skew Mkt: {basketTraderItemModel.HighestBid}X{basketTraderItemModel.LowestAsk}, Time: {stopwatch.ElapsedMilliseconds}, TotalTime: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);

                            stopwatch.Restart();
                            string extraMessage = "";
                            switch (signalModel.EdgeScannerType)
                            {
                                case EdgeScannerType.OutOfMarketTrade:
                                    string exchange = exchanges[0];
                                    extraMessage = await basketTraderItemModel.FishOffMarket(exchange);
                                    break;
                                case EdgeScannerType.SweepFinder:
                                    extraMessage = await basketTraderItemModel.JoinSweep(AutoTraderResubmitCount);
                                    break;
                                default:
                                    int autoTraderResubmitCount;
                                    if (signalModel.EdgeScannerType == EdgeScannerType.FullAuto)
                                    {
                                        autoTraderResubmitCount = 0;
                                    }
                                    else
                                    {
                                        autoTraderResubmitCount = AutoTraderResubmitCount;
                                        if (basketTraderItemModel.ResubmitWithRegularRoute)
                                        {
                                            autoTraderResubmitCount++;
                                        }
                                    }
                                    _ = basketTraderItemModel.SubmitOrder(resting: false,
                                                               skipAdjPxBeforeSubmit: AutoTraderUseTradePrice,
                                                               totalResubmitCount: autoTraderResubmitCount,
                                                               markForRemoval: !usingEdgeLookup,
                                                               doNotTradeThroughFillPrice: AutoTraderDoNotTradeThroughFillPrice,
                                                               subType: null,
                                                               restOverride: restOverride,
                                                               referenceTradeModel: referenceTradeModel,
                                                               clearDetailsContainer: false,
                                                               referenceTradeOriginalPrice: signalModel.BuyPrice,
                                                               payUpTicks: AutoTraderEnablePayUpTicks ? AutoTraderPayUpTicks : 0);
                                    break;
                            }

                            signalModel.Message = string.IsNullOrWhiteSpace(extraMessage) ? "Order Sent" : extraMessage;
                            signalModel.Reason = "(" + totalMilliseconds + ")";
                            signalModel.OrderSent = true;
                            _log.Info($"Source: Edge scan feed auto trader, Message: {signalModel.Message}, Reason: {signalModel.Reason}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                            _totalSubmission++;
                            _lastSubmission++;
                            if ((DateTime.Now - _orderRateLastUpdateTime).TotalSeconds > 60)
                            {
                                _orderRateLastUpdateTime = DateTime.Now;
                                _totalSubmissionRate = 0;
                            }
                            if ((DateTime.Now - _orderRateLastUpdateTimeSec).TotalSeconds > 1)
                            {
                                _orderRateLastUpdateTimeSec = DateTime.Now;
                                _totalSubmissionRateSec = 0;
                            }
                            _totalSubmissionRate++;
                            _totalSubmissionRateSec++;
                            if (AutoTraderAutoPermTrades && AutoTraderAutoPermConfig != null)
                            {
                                if (checkBuySide || checkSellSide)
                                {
                                    _ = AutoTraderBasketViewModel.LoadAutoPerms(basketTraderItemModel, signalModel.DeltaAdjEdge, AutoTraderAutoPermConfig);
                                }
                            }
                        }
                        else
                        {
                            basketTraderItemModel.NotifyOrderCloseWaitHandlers(true, null);
                            _log.Info($"Source: Edge scan feed auto trader, Message: Max Bid % Check Failed Main. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                        }

                        int wait = (int)Math.Max(2000, AutoTraderBasketViewModel.BasketSettings.CancelWithTimer);
                        await basketTraderItemModel.OrderClosedEvent.WaitOneAsync(wait).ContinueWith(t =>
                        {
                            if (t.Result && (basketTraderItemModel.OrderStatus.IsFilled() || basketTraderItemModel.TotalFills > 0))
                            {
                                signalModel.OrderFilled = true;
                            }
                        });

                        if (sendContra)
                        {
                            if (signalModel.EdgeScannerType == EdgeScannerType.FullAuto)
                            {
                                stopwatch.Restart();
                                await basketTraderItemModel.OrderClosedEvent.WaitOneAsync(wait).ContinueWith(async t =>
                                {
                                    if (!sendMain || (t.Result && basketTraderItemModel.OrderStatus == OrderStatus.Canceled && basketTraderItemModel.TotalFills == 0 && !basketTraderItemModel.IsActive))
                                    {
                                        _log.Info("Source: Edge scan feed auto trader, Message: Send Opposing Side Signal." + $"Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                        var reversed = basketTraderItemModel.Reverse();
                                        if (!reversed)
                                        {
                                            throw new SlimException("Order failed to reverse!");
                                        }

                                        if (!double.IsNaN(maxPercentBid))
                                        {
                                            stopwatch.Restart();
                                            if (await basketTraderItemModel.WaitForMarkLoad())
                                            {
                                                double bidPercentPrice = basketTraderItemModel.CalculateBidPercent(maxPercentBid, overrideEdge: false).Price;
                                                bool valid = basketTraderItemModel.IsSingleLegSell ? contraPrice >= bidPercentPrice : contraPrice <= bidPercentPrice;
                                                if (!valid)
                                                {
                                                    contraPrice = bidPercentPrice;
                                                    _log.Info($"Source: Edge scan feed auto trader, Message: Contra Percent Bid Check Override. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                }
                                            }
                                            else
                                            {
                                                throw new SlimException("Wait for data timeout");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Contra Percent Bid Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        basketTraderItemModel.SetPrice(contraPrice);
                                        basketTraderItemModel.AveragePrice = contraPrice;
                                        basketTraderItemModel.BestAveragePrice = contraPrice;
                                        basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;

                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            double edge = basketTraderItemModel.CloseEdgeOveride + AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEdge;
                                            if (basketTraderItemModel.IsSingleLeg)
                                            {
                                                await basketTraderItemModel.SetPriceIncrementAsync();
                                            }
                                            double minTick = (double)basketTraderItemModel.PriceIncrement;
                                            if (edge < minTick)
                                            {
                                                edge = minTick;
                                            }
                                            if (basketTraderItemModel.IsSingleLegSell)
                                            {
                                                if (contraPrice <= basketTraderItemModel.Low + edge)
                                                {
                                                    throw new SlimException("Px crosses min edge from mkt");
                                                }
                                            }
                                            else
                                            {
                                                if (contraPrice >= basketTraderItemModel.High - edge)
                                                {
                                                    throw new SlimException("Px crosses min edge from mkt");
                                                }
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Market Check Complete. Edge: {edge}, Inc: {minTick}, Px: {contraPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        stopwatch.Restart();
                                        if (checkForSkewAdjEdge && !(await basketTraderItemModel.IsValidEdgeToSkewAdjMarketAsync(contraPrice)).IsValid)
                                        {
                                            throw new SlimException("Px crosses min edge from skew mkt");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Skew Market Check Complete. Px: {contraPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Skew Mkt: {basketTraderItemModel.HighestBid}X{basketTraderItemModel.LowestAsk}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                        stopwatch.Restart();
                                        if (checkForSkewAdjCrossEdge && !(await basketTraderItemModel.IsValidEdgeToSkewAdjMarketCrossAsync(contraPrice, basketTraderItemModel.CloseEdgeOveride)).IsValid)
                                        {
                                            throw new SlimException("Px crosses min edge from skew mkt cross");
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Min Edge To Skew Market Cross Check Complete. Px: {contraPrice}, Mkt: {basketTraderItemModel.Low}X{basketTraderItemModel.High}, Skew Mkt: {basketTraderItemModel.HighestBid}X{basketTraderItemModel.LowestAsk}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                        if ((AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled || AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled) && OmsCore.Config.PriceCacheClearIntervalEnabled)
                                        {
                                            if (basketTraderItemModel.TryGetPriceCache(out PriceCache chain))
                                            {
                                                double edge = basketTraderItemModel.CloseEdgeOveride;
                                                if (basketTraderItemModel.IsSingleLeg)
                                                {
                                                    double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                    double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                    if (basketTraderItemModel.Side == Side.Buy)
                                                    {
                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid + edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (basketTraderItemModel.Side == Side.Buy)
                                                    {
                                                        stopwatch.Restart();
                                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);

                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        stopwatch.Restart();
                                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);
                                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);

                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price > -(adjBid + edge))
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price < adjAsk)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        stopwatch.Restart();
                                        if (basketTraderItemModel.Lcd != qty)
                                        {
                                            basketTraderItemModel.UpdateQty(qty);
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Contra Qty Update Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        stopwatch.Restart();
                                        basketTraderItemModel.ClearOrderDetails();
                                        if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker &&
                                            !string.IsNullOrWhiteSpace(signalModel.ExtraTag))
                                        {
                                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade", signalModel.ExtraTag);
                                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade Time", signalModel.BuyTime.ToString("hh:mm:ss.ffffff") + "," + signalModel.SellTime.ToString("hh:mm:ss.ffffff"));
                                        }

                                        double deltaAdjContraPrice = ((basketTraderItemModel.UnderMid - signalModel.BuyTradeUnderlyingMid) * (basketTraderItemModel.IsSingleLeg ? tradeDelta : -tradeDelta)) + contraPrice;

                                        if (!double.IsNaN(maxTheo))
                                        {
                                            stopwatch.Restart();
                                            if (await basketTraderItemModel.WaitForAdjTheoLoadAsync())
                                            {
                                                var edgeToTheo = basketTraderItemModel.IsSingleLegSell ? basketTraderItemModel.NetDeltaAdjTheo - deltaAdjContraPrice : deltaAdjContraPrice - basketTraderItemModel.NetDeltaAdjTheo;
                                                if (edgeToTheo > maxTheo)
                                                {
                                                    _log.Info($"Theo Cross Adj S. Id: {signalModel.Description}, Contra Price: {contraPrice}, Theo: {basketTraderItemModel.NetDeltaAdjTheo}, Limit: {maxTheo}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                    contraPrice = basketTraderItemModel.IsSingleLegSell ? basketTraderItemModel.NetDeltaAdjTheo - maxTheo : basketTraderItemModel.NetDeltaAdjTheo + maxTheo;
                                                    signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                                }
                                            }
                                            else
                                            {
                                                throw new SlimException("Wait for theo timeout");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Theo Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        if (!double.IsNaN(maxVola))
                                        {
                                            stopwatch.Restart();
                                            if (await basketTraderItemModel.WaitForAdjTheoLoadAsync())
                                            {
                                                var result = await basketTraderItemModel.GetTheoAsync(volaModel, false, OmsCore.Config.PerformanceModeEnabled);
                                                var vola = result.NetDeltaAdjTheo;
                                                var edgeToVola = basketTraderItemModel.IsSingleLegSell ? vola - deltaAdjContraPrice : deltaAdjContraPrice - vola;
                                                if (edgeToVola > maxVola)
                                                {
                                                    _log.Info($"Vola Cross Adj S. Id: {signalModel.Description}, Contra Price: {contraPrice}, Vola: {vola}, Limit: {maxVola}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                    contraPrice = basketTraderItemModel.IsSingleLegSell ? vola - maxVola : vola + maxVola;
                                                    signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                                }
                                            }
                                            else
                                            {
                                                throw new SlimException("Wait for Vola timeout");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Vola Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        if (!double.IsNaN(maxEma))
                                        {
                                            if (!basketTraderItemModel.SubscribedToEma)
                                            {
                                                _log.Info(
                                                    $"Source: Edge scan feed auto trader, Message: Not subscribed to EMA, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" +
                                                    signalModel);
                                            }
                                            else
                                            {
                                                stopwatch.Restart();
                                                if (await basketTraderItemModel.WaitForEmaLoad())
                                                {
                                                    double ema = basketTraderItemModel.GetEma(OmsCore.Config.PerformanceModeEnabled);
                                                    var adjContraPrice = basketTraderItemModel.IsSingleLegSell ? ema - deltaAdjContraPrice : deltaAdjContraPrice - ema;
                                                    if (adjContraPrice > maxEma)
                                                    {
                                                        _log.Info($"EMA Cross Adj S. Id: {signalModel.Description}, Contra Price: {contraPrice}, Ema: {ema}, Limit: {maxEma}, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                        contraPrice = basketTraderItemModel.IsSingleLegSell ? ema - maxEma : ema + maxEma;
                                                        signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                                    }
                                                }
                                                else
                                                {
                                                    throw new SlimException("Wait for ema timeout");
                                                }
                                                _log.Info($"Source: Edge scan feed auto trader, Message: Ema Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                            }
                                        }

                                        if (!double.IsNaN(maxPercentBid))
                                        {
                                            stopwatch.Restart();
                                            if (await basketTraderItemModel.WaitForMarkLoad())
                                            {
                                                var calculateBidPercent = basketTraderItemModel.CalculateBidPercent(maxPercentBid, overrideEdge: false);
                                                double bidPercentPrice = calculateBidPercent.Price;
                                                bool valid = basketTraderItemModel.IsSingleLegSell ? deltaAdjContraPrice >= bidPercentPrice : deltaAdjContraPrice <= bidPercentPrice;
                                                if (!valid)
                                                {
                                                    contraPrice = bidPercentPrice;
                                                    signalModel.BuyTradeUnderlyingMid = basketTraderItemModel.UnderMid;
                                                    _log.Info($"Source: Edge scan feed auto trader, Message: Percent Bid Check Override For Contra, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                }
                                            }
                                            else
                                            {
                                                throw new SlimException("Wait for data timeout");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Percent Bid Check Complete, Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        basketTraderItemModel.SetPrice(contraPrice);
                                        basketTraderItemModel.AveragePrice = contraPrice;
                                        basketTraderItemModel.BestAveragePrice = contraPrice;
                                        basketTraderItemModel.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;

                                        double baseContraPrice = contraPrice;
                                        double baseContraUnderlying = signalModel.BuyTradeUnderlyingMid;
                                        double contraUnderMid = basketTraderItemModel.UnderMid;
                                        double delta = basketTraderItemModel.IsSingleLeg ? tradeDelta : -tradeDelta;
                                        deltaAdjContraPrice = ((contraUnderMid - baseContraUnderlying) * delta) + baseContraPrice;
                                        _log.Info("Delta Adj Edge Feed co. " + "Under Mid: " + contraUnderMid + ", Fill Under Mid: " + baseContraUnderlying + ", Delta: " + delta + ", Fill Px" + baseContraPrice + $", Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                        double totalContraMilliseconds = (DateTime.Now - signalModel.CreationTime).TotalMilliseconds;
                                        signalModel.Message = "Co Order Sent";
                                        var msg = " - (" + totalContraMilliseconds + ")";
                                        if (signalModel.Reason == null)
                                        {
                                            signalModel.Reason = msg;
                                        }
                                        else
                                        {
                                            signalModel.Reason += msg;
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: all checks passed contra order sent. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        basketTraderItemModel.ModuleTypeSuffix = $"`{basketTraderItemModel.CloseEdgeOveride:F0}`{timespan:F0}`{signalModel.BuyConditionCode}`{deltaAdjContraPrice:F2}`{signalModel.BuyQty}`{signalModel.BuyPrice:F2}`{signalModel.BuyTime.ToUnixEpoch()}`{signalModel.SellQty}`{signalModel.SellPrice:F2}`{signalModel.SellTime.ToUnixEpoch()}`{totalContraMilliseconds:F2}`{(int)signalModel.EdgeScannerType}`{EDGE_SCAN_FEED_SUBTYPE}";
                                        basketTraderItemModel.EdgeScanFeedRespondLatency = totalContraMilliseconds;
                                        referenceTradeModel = new ReferenceTradeModel()
                                        {
                                            TradeBid = basketTraderItemModel.IsComplexOrder ? -signalModel.BuyTradeAsk : signalModel.BuyTradeBid,
                                            TradeAsk = basketTraderItemModel.IsComplexOrder ? -signalModel.BuyTradeBid : signalModel.BuyTradeAsk,
                                            TradePrice = baseContraPrice,
                                            TradeUnderMid = baseContraUnderlying,
                                            TradeTime = DateTime.Now,
                                        };
                                        _ = basketTraderItemModel.SubmitOrder(resting: false,
                                                                   skipAdjPxBeforeSubmit: AutoTraderUseTradePrice,
                                                                   totalResubmitCount: 0,
                                                                   markForRemoval: true,
                                                                   doNotTradeThroughFillPrice: AutoTraderDoNotTradeThroughFillPrice,
                                                                   subType: null,
                                                                   restOverride: restOverride,
                                                                   referenceTradeModel: referenceTradeModel,
                                                                   clearDetailsContainer: false,
                                                                   referenceTradeOriginalPrice: signalModel.SellPrice,
                                                                   payUpTicks: AutoTraderEnablePayUpTicks ? AutoTraderPayUpTicks : 0);
                                        _totalSubmission++;
                                        _totalSubmissionRate++;
                                        _totalSubmissionRateSec++;
                                    }
                                });
                            }
                            else if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker)
                            {
                                stopwatch.Restart();
                                await basketTraderItemModel.OrderClosedEvent.WaitOneAsync(wait).ContinueWith(async t =>
                                {
                                    if (t.Result && basketTraderItemModel.OrderStatus == OrderStatus.Canceled && basketTraderItemModel.TotalFills == 0 && !basketTraderItemModel.IsActive)
                                    {
                                        _log.Info($"Source: Edge scan feed auto trader, Message: Send Opposing Side Signal. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);

                                        var reversed = basketTraderItemModel.Reverse();
                                        if (!reversed)
                                        {
                                            throw new SlimException("Order failed to reverse!");
                                        }

                                        if (!double.IsNaN(maxPercentBid))
                                        {
                                            stopwatch.Restart();
                                            if (await basketTraderItemModel.WaitForMarkLoad())
                                            {
                                                double bidPercentPrice = basketTraderItemModel.CalculateBidPercent(maxPercentBid, overrideEdge: false).Price;
                                                bool valid = basketTraderItemModel.IsSingleLegSell ? contraPrice >= bidPercentPrice : contraPrice <= bidPercentPrice;
                                                if (!valid)
                                                {
                                                    contraPrice = bidPercentPrice;
                                                    _log.Info($"Source: Edge scan feed auto trader, Message: Contra Percent Bid Check Override. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                                }
                                            }
                                            else
                                            {
                                                throw new SlimException("Wait for data timeout");
                                            }
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Contra Percent Bid Check Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        basketTraderItemModel.SetPrice(contraPrice);
                                        basketTraderItemModel.AveragePrice = contraPrice;
                                        basketTraderItemModel.BestAveragePrice = contraPrice;
                                        basketTraderItemModel.LastMainUnderMidAtFill = signalModel.SellTradeUnderlyingMid;
                                        basketTraderItemModel.LastMainUnderMidAtBestFill = signalModel.SellTradeUnderlyingMid;

                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEnabled)
                                        {
                                            stopwatch.Restart();
                                            double edge = basketTraderItemModel.CloseEdgeOveride + AutoTraderBasketViewModel.BasketSettings.MinEdgeToMarketCheckEdge;
                                            double minTick = (double)basketTraderItemModel.PriceIncrement;
                                            if (edge < minTick)
                                            {
                                                edge = minTick;
                                            }
                                            if (basketTraderItemModel.IsSingleLeg)
                                            {
                                                if (basketTraderItemModel.Side == Side.Buy)
                                                {
                                                    if (basketTraderItemModel.Price >= basketTraderItemModel.High - edge)
                                                    {
                                                        throw new SlimException("Px crosses min edge from mkt");
                                                    }
                                                }
                                                else
                                                {
                                                    if (basketTraderItemModel.Price <= basketTraderItemModel.Low + edge)
                                                    {
                                                        throw new SlimException("Px crosses min edge from mkt");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (basketTraderItemModel.Price >= basketTraderItemModel.High - edge)
                                                {
                                                    throw new SlimException("Px crosses min edge from mkt");
                                                }
                                            }
                                        }

                                        if ((AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled || AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled) && OmsCore.Config.PriceCacheClearIntervalEnabled)
                                        {
                                            if (basketTraderItemModel.TryGetPriceCache(out PriceCache chain))
                                            {
                                                double edge = basketTraderItemModel.CloseEdgeOveride;
                                                if (basketTraderItemModel.IsSingleLeg)
                                                {
                                                    double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                    double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                    if (basketTraderItemModel.Side == Side.Buy)
                                                    {
                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        stopwatch.Restart();
                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid + edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (basketTraderItemModel.Side == Side.Buy)
                                                    {
                                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);
                                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, basketTraderItemModel.TotalDelta);

                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price > adjAsk - edge)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}X{adjAsk}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price < adjBid)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}X{adjAsk}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        double adjBid = chain.GetAdjustedHighestBid(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);
                                                        double adjAsk = chain.GetAdjustedLowestAsk(basketTraderItemModel.UnderMid, -basketTraderItemModel.TotalDelta);

                                                        if (AutoTraderBasketViewModel.BasketSettings.MinEdgeToPreviousAttemptCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjBid))
                                                            {
                                                                if (basketTraderItemModel.Price > -(adjBid + edge))
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjBid}, Edge: {edge}";
                                                                    throw new SlimException("Px crosses min edge from last attempt");
                                                                }
                                                            }
                                                        }

                                                        if (AutoTraderBasketViewModel.BasketSettings.PreviousAttemptCrossCheckEnabled)
                                                        {
                                                            stopwatch.Restart();
                                                            if (!double.IsNaN(adjAsk))
                                                            {
                                                                if (basketTraderItemModel.Price < adjAsk)
                                                                {
                                                                    signalModel.Reason = $"Px: {basketTraderItemModel.Price}, Prev: {adjAsk}";
                                                                    throw new SlimException("Px crosses prev attempt");
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        stopwatch.Restart();
                                        if (basketTraderItemModel.Lcd != qty)
                                        {
                                            basketTraderItemModel.UpdateQty(qty);
                                            _log.Info($"Source: Edge scan feed auto trader, Message: Contra Qty Update Complete. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        }

                                        basketTraderItemModel.ClearOrderDetails();
                                        if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker &&
                                            !string.IsNullOrWhiteSpace(signalModel.ExtraTag))
                                        {
                                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade", signalModel.ExtraTag);
                                            basketTraderItemModel.SetOrderDetailTag("Trigger Trade Time", signalModel.BuyTime.ToString("hh:mm:ss.ffffff") + "," + signalModel.SellTime.ToString("hh:mm:ss.ffffff"));
                                        }

                                        double baseContraPrice = contraPrice;
                                        double baseContraUnderlying = signalModel.BuyTradeUnderlyingMid;
                                        double contraUnderMid = basketTraderItemModel.UnderMid;
                                        double delta = basketTraderItemModel.IsSingleLeg ? tradeDelta : -tradeDelta;
                                        double deltaAdjContraPrice = ((contraUnderMid - baseContraUnderlying) * delta) + baseContraPrice;
                                        _log.Info("Delta Adj Edge Feed co. " + "Under Mid: " + contraUnderMid + ", Fill Under Mid: " + baseContraUnderlying + ", Delta: " + delta + ", Fill Px" + baseContraPrice + $", Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                                        double totalContraMilliseconds = (DateTime.Now - signalModel.CreationTime).TotalMilliseconds;
                                        signalModel.Message = "Co Order Sent";
                                        var msg = " - (" + totalContraMilliseconds + ")";
                                        if (signalModel.Reason == null)
                                        {
                                            signalModel.Reason = msg;
                                        }
                                        else
                                        {
                                            signalModel.Reason += msg;
                                        }
                                        _log.Info($"Source: Edge scan feed auto trader, Message: all checks passed contra order sent. Time: {stopwatch.ElapsedMilliseconds}, Total Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}, TotalTimeMicro: {(DateTime.Now - signalModel.ReceiveTime).TotalMicroseconds}" + signalModel);
                                        basketTraderItemModel.ModuleTypeSuffix = $"`{basketTraderItemModel.CloseEdgeOveride:F0}`{timespan:F0}`{signalModel.BuyConditionCode}`{deltaAdjContraPrice:F2}`{signalModel.BuyQty}`{signalModel.BuyPrice:F2}`{signalModel.BuyTime.ToUnixEpoch()}`{signalModel.SellQty}`{signalModel.SellPrice:F2}`{signalModel.SellTime.ToUnixEpoch()}`{totalContraMilliseconds:F2}`{(int)signalModel.EdgeScannerType}`{EDGE_SCAN_FEED_SUBTYPE}";
                                        basketTraderItemModel.EdgeScanFeedRespondLatency = totalContraMilliseconds;
                                        referenceTradeModel = new ReferenceTradeModel()
                                        {
                                            TradeBid = basketTraderItemModel.IsComplexOrder ? -signalModel.BuyTradeAsk : signalModel.BuyTradeBid,
                                            TradeAsk = basketTraderItemModel.IsComplexOrder ? -signalModel.BuyTradeBid : signalModel.BuyTradeAsk,
                                            TradePrice = baseContraPrice,
                                            TradeUnderMid = baseContraUnderlying,
                                            TradeTime = DateTime.Now,
                                        };
                                        _ = basketTraderItemModel.SubmitOrder(resting: false,
                                                                   skipAdjPxBeforeSubmit: AutoTraderUseTradePrice,
                                                                   totalResubmitCount: 0,
                                                                   markForRemoval: true,
                                                                   doNotTradeThroughFillPrice: AutoTraderDoNotTradeThroughFillPrice,
                                                                   subType: null,
                                                                   restOverride: restOverride,
                                                                   referenceTradeModel: referenceTradeModel,
                                                                   clearDetailsContainer: false,
                                                                   referenceTradeOriginalPrice: signalModel.SellPrice,
                                                                   payUpTicks: AutoTraderEnablePayUpTicks ? AutoTraderPayUpTicks : 0);
                                        _totalSubmission++;
                                        _totalSubmissionRate++;
                                        _totalSubmissionRateSec++;
                                    }
                                });
                            }
                        }

                        if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker)
                        {
                            _log.Info($"Cross Market Order. " +
                                      $"Symbol: {signalModel.BuySymbol}, " +
                                      $"Trigger: {signalModel.ExtraTag}, " +
                                      $"Trigger Time: {signalModel.BuyTime.ToString("hh:mm:ss.ffffff") + "," + signalModel.SellTime.ToString("hh:mm:ss.ffffff")}, " +
                                      $"Order Id: {basketTraderItemModel.OrderId}, " +
                                      $"Order Sent: {signalModel.OrderSent}, " +
                                      $"Order Filled: {signalModel.OrderFilled}.");
                        }
                    }
                    catch (SlimException se)
                    {
                        signalModel.Message = se.Message;
                        basketItem?.Dispose();
                        LogStatus(signalModel, stopwatch);
                    }
                });
            }
            catch (SlimException se)
            {
                signalModel.Message = se.Message;
                basketItem?.Dispose();
                LogStatus(signalModel, stopwatch);
            }
            finally
            {
                signalModel.Traded = true;
            }
        }

        private async Task CheckForIbCob(EdgeScanFeedModel signalModel, bool checkBuySide, bool checkSellSide,
            BasketTraderItemModel basketItem)
        {
            basketItem.SubscribeToIbCommand();
            var loaded = await basketItem.WaitForIbQuoteLoadAsync();
            if (!loaded)
            {
                throw new SlimException("Wait for IB data timeout");
            }

            if (ConfirmWithIbCob)
            {
                if (basketItem.TwsVolume < signalModel.BuyQty + signalModel.SellQty)
                {
                    throw new SlimException("IB CoB volume check failed!");
                }

                if (checkBuySide)
                {
                    if (basketItem.TwsLow >= signalModel.BuyPrice)
                    {
                        throw new SlimException("IB CoB px check failed!");
                    }
                }
                else if (checkSellSide)
                {
                    if (basketItem.TwsHigh <= signalModel.BuyPrice)
                    {
                        throw new SlimException("IB CoB px check failed!");
                    }
                }

                if (signalModel.EdgeScannerType != EdgeScannerType.CopyCatWithEdge)
                {
                    basketItem.UnsubscribeIbDataCommand();
                }
            }
        }

        private static void LogStatus(EdgeScanFeedModel signalModel, Stopwatch stopwatch)
        {
            var time = DateTime.Now - signalModel.ReceiveTime;
            _log.Info($"Source: Edge scan feed auto trader, Message: {signalModel.Message}, Reason: {signalModel.Reason}, Time: {stopwatch.ElapsedMilliseconds}, TotalTime: {time.TotalMilliseconds}, TotalTimeMicro: {time.TotalMicroseconds}" + signalModel);
        }

        private async Task<RiskWarningMessageResponse> ShowMessage(string message)
        {
            var response = RiskWarningMessageResponse.Cancel;
            await Dispatcher.BeginInvoke(() =>
            {
                response = VerificationService.GetRiskVerification(message, "Edge Scan Feed", showCancelAll: false);
            });
            return response;
        }

        private async Task<BasketTraderItemModel> LoadBasketItem(EdgeScanFeedModel signalModel, bool checkBuySide, bool checkSellSide)
        {
            BasketTraderItemModel basketItem = new(AutoTraderBasketViewModel, AutoTraderBasketViewModel.Dispatcher, OmsCore);
            var loadedBuy = true;
            switch (signalModel.EdgeScannerType)
            {
                case EdgeScannerType.LoopFinder:
                case EdgeScannerType.TheoEdgeFinder:
                case EdgeScannerType.MarketPercentFinder:
                case EdgeScannerType.FullAuto:
                case EdgeScannerType.DeltaAdjustedLoopFinder:
                case EdgeScannerType.IvChangeDeltaAdjLoopFinder:
                case EdgeScannerType.CopyCatWithEdge:
                case EdgeScannerType.SweepFinder:
                case EdgeScannerType.CrossedMarketMaker:
                case EdgeScannerType.MarketMakerFinder:
                case EdgeScannerType.EdgeToTheoDivergence:
                    await basketItem.LoadLegsFromTosAsync(signalModel.BuySymbol);
                    break;
                case EdgeScannerType.PermAdjustedLoopFinder:
                    if (checkBuySide)
                    {
                        await basketItem.LoadLegsFromTosAsync(signalModel.BuySymbol);
                    }
                    else if (checkSellSide)
                    {
                        await basketItem.LoadLegsFromTosAsync(signalModel.SellSymbol, Side.Sell);
                        loadedBuy = false;
                    }
                    break;
                case EdgeScannerType.OutOfMarketTrade:
                case EdgeScannerType.SideScan:
                case EdgeScannerType.EqSideScan:
                    if (checkBuySide)
                    {
                        await basketItem.LoadLegsFromTosAsync(signalModel.BuySymbol);
                    }
                    else if (checkSellSide)
                    {
                        await basketItem.LoadLegsFromTosAsync(signalModel.SellSymbol);
                        loadedBuy = false;
                    }
                    break;
            }

            if (LoadWithStockTiedLeg)
            {
                var delta = loadedBuy ? signalModel.BuyTradeDelta : signalModel.SellTradeDelta;
                await basketItem.SetupStockTieAsync(delta);
            }

            return basketItem;
        }

        private bool CheckForRecentAttempt(BasketTraderItemModel basketItem)
        {
            bool failed = false;
            if (basketItem.TryGetGenericAttemptCache(out var genericChain))
            {
                if (basketItem.Side == Side.Buy)
                {
                    if ((DateTime.Now - genericChain.LastBuyAttempt).TotalSeconds < AutoTraderBasketViewModel.BasketSettings.MinTimeToPreviousAttemptIntervalSeconds)
                    {
                        failed = true;
                    }
                }
                else
                {
                    if ((DateTime.Now - genericChain.LastSellAttempt).TotalSeconds < AutoTraderBasketViewModel.BasketSettings.MinTimeToPreviousAttemptIntervalSeconds)
                    {
                        failed = true;
                    }
                }
            }
            return failed;
        }

        private bool CheckForPermLoserAttempt(BasketTraderItemModel basketItem)
        {
            bool failed = false;
            if (basketItem.TryGetGenericAttemptCache(out var genericChain))
            {
                if ((DateTime.Now - genericChain.LastLoserTime).TotalSeconds < AutoTraderBasketViewModel.BasketSettings.MinTimeToPermLoserIntervalSeconds)
                {
                    failed = true;
                }
            }
            return failed;
        }

        private bool SetPrice(EdgeScanFeedModel signalModel, bool checkBuySide, bool checkSellSide, BasketTraderItemModel basketItem)
        {
            if (checkBuySide)
            {
                if (signalModel.EdgeScannerType != EdgeScannerType.SideScan && signalModel.EdgeScannerType != EdgeScannerType.EqSideScan)
                {
                    ConfirmBuySide(basketItem);
                }

                if (basketItem.IsDisposed)
                {
                    return false;
                }

                basketItem.SetPrice(signalModel.BuyPrice);
                basketItem.AveragePrice = signalModel.BuyPrice;
                basketItem.BestAveragePrice = signalModel.BuyPrice;
                basketItem.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                basketItem.EdgeScanFeedSellPrice = signalModel.SellPrice;
                basketItem.EdgeScanFeedUnderlying = signalModel.BuyTradeUnderlyingMid;
                basketItem.LastMainUnderMidAtFill = signalModel.BuyTradeUnderlyingMid;
                basketItem.LastMainUnderMidAtBestFill = signalModel.BuyTradeUnderlyingMid;
                return true;
            }
            if (checkSellSide)
            {
                if (signalModel.EdgeScannerType != EdgeScannerType.SideScan && signalModel.EdgeScannerType != EdgeScannerType.EqSideScan)
                {
                    ConfirmSellSide(basketItem);
                }

                if (basketItem.IsDisposed)
                {
                    return false;
                }

                basketItem.SetPrice(signalModel.SellPrice);
                basketItem.AveragePrice = signalModel.SellPrice;
                basketItem.BestAveragePrice = signalModel.SellPrice;
                basketItem.EdgeScanFeedBuyPrice = signalModel.BuyPrice;
                basketItem.EdgeScanFeedSellPrice = signalModel.SellPrice;
                basketItem.EdgeScanFeedUnderlying = signalModel.SellTradeUnderlyingMid;
                basketItem.LastMainUnderMidAtFill = signalModel.SellTradeUnderlyingMid;
                basketItem.LastMainUnderMidAtBestFill = signalModel.SellTradeUnderlyingMid;
                return true;
            }
            return false;
        }

        private bool ValidateEdgeAndSetSizeAndEdgeOverrides(EdgeScanFeedModel signalModel, BasketTraderItemModel basketItem)
        {
            if (signalModel.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CrossedMarketMaker or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence)
            {
                double edgeOverride;
                switch (AutoTraderEdgeOverride)
                {
                    case AutoTraderEdgeOverride.Edge:
                        edgeOverride = Math.Abs(Math.Abs(signalModel.SellPrice) - Math.Abs(signalModel.BuyPrice));
                        break;
                    case AutoTraderEdgeOverride.DeltaAdjEdge:
                        edgeOverride = Math.Abs(signalModel.DeltaAdjEdge);
                        break;
                    default:
                        edgeOverride = double.NaN;
                        break;
                }

                if (edgeOverride is > 15 or < 0)
                {
                    signalModel.Message = "Invalid Edge";
                    _log.Info($"Source: Edge scan feed auto trader, Message: invalid edge {edgeOverride}. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    basketItem.Dispose();
                    return false;
                }

                basketItem.CloseEdgeOveride = edgeOverride;
            }

            basketItem.SizeOveride = Math.Min(signalModel.BuyQty, signalModel.SellQty);
            return true;
        }

        private bool IsValidBasketItemAsync(EdgeScanFeedModel signalModel, BasketTraderItemModel basketItem)
        {
            if (string.IsNullOrEmpty(basketItem.Description) ||
                basketItem.Description.StartsWith("CUSTOM") ||
                basketItem.Description.StartsWith("INVALID"))
            {
                signalModel.Message = "Invalid Basket Item";
                _log.Info($"Source: Edge scan feed auto trader, Message: basket item not ready. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                basketItem.Dispose();
                return false;
            }
            else if (basketItem.MainResting ||
                     basketItem.ContraResting ||
                     basketItem.IsActive)
            {
                signalModel.Message = "Resting Order Detected";
                _log.Info("Source: Edge scan feed auto trader, Message: resting order detected. MR: " + basketItem.MainResting + ", CR: " + basketItem.ContraResting + ", LO: " + basketItem.IsActive + $", Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                basketItem.Dispose();
                return false;
            }
            else if (signalModel.EdgeScannerType == EdgeScannerType.SweepFinder && basketItem.TraderSpreadPositionInitialized)
            {
                signalModel.Message = "Sweep Already Traded";
                _log.Info($"Source: Edge scan feed auto trader, Message: sweep already traded. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                basketItem.Dispose();
                return false;
            }
            else if (basketItem.SpreadPosition != 0)
            {
                signalModel.Message = "Open Pos Found";
                _log.Info($"Source: Edge scan feed auto trader, Message: open pos found. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                basketItem.Dispose();
                return false;
            }
            else if (MinPnlForAutoTraderEnabled)
            {
                if (!MinPnlMaxQtyCheckEnabled || signalModel.BuyQty < MinPnlMaxQty)
                {
                    if (basketItem.AdjustedPnl <= MinPnlForAutoTrader)
                    {
                        signalModel.Message = "Min PnL Check Failed";
                        _log.Info($"Source: Edge scan feed auto trader, Message: min PnL check found. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                        basketItem.Dispose();
                        return false;
                    }
                }
            }

            return true;
        }

        private bool CheckForVisualFilters(EdgeScanFeedModel signalModel)
        {
            if (AutoTraderCheckForVisualFilters)
            {
                bool found = GetItemsByVisualOrderService.ItemIsVisible(signalModel);
                if (!found)
                {
                    signalModel.Message = "Visual Check Failed";
                    _log.Info($"Source: Edge scan feed auto trader, Message: visual filter check failed. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    return false;
                }
                _log.Info($"Source: Edge scan feed auto trader, Message: Visual Filter Confirmation Complete. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
            }
            return true;
        }

        private bool CheckForBlockList(EdgeScanFeedModel signalModel)
        {
            if (BlockedSymbolModel != null &&
                            BlockedSymbolModel.SymbolsSet != null &&
                            BlockedSymbolModel.SymbolsSet.Count > 0 &&
                            (BlockedSymbolModel.SymbolsSet.Contains(signalModel.SpreadId) || BlockedSymbolModel.SymbolsSet.Contains(signalModel.UnderSymbol)))
            {
                signalModel.Message = "Banned Symbol";
                _log.Info($"Source: Edge scan feed auto trader, Message: visual filter check failed. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                return false;
            }
            _log.Info($"Source: Edge scan feed auto trader, Message: Banned List Check Complete. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
            return true;
        }

        private bool CheckForRecentTrades(EdgeScanFeedModel signalModel)
        {
            if (TransactionConsumerModel.TryGetLastTradeTime(signalModel.SpreadId, out DateTime lastTradeTime))
            {
                if ((DateTime.Now - lastTradeTime).TotalSeconds < OmsCore.Config.RecentTradeLookback)
                {
                    signalModel.Message = "Recently Traded";
                    _log.Info($"Source: Edge scan feed auto trader, Message: recent trade found. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    return false;
                }
            }
            _log.Info($"Source: Edge scan feed auto trader, Message: Recent Trade Check Complete. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
            return true;
        }

        private bool IsValidPosition(EdgeScanFeedModel signalModel)
        {
            if (PortfolioManagerModel.TryGetFirmPosition(signalModel.SpreadId, out ZeroPlus.Models.Data.Portfolio.Interfaces.IPosition position))
            {
                if (position.NetQty != 0)
                {
                    signalModel.Message = "Open Pos Found";
                    _log.Info($"Source: Edge scan feed auto trader, Message: firm open pos found. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    return false;
                }

                if (PortfolioManagerModel.TryGetTraderPosition(signalModel.SpreadId, out position))
                {
                    if (MinPnlForAutoTraderEnabled &&
                        position.AdjustedPnl <= MinPnlForAutoTrader &&
                        (!MinPnlMaxQtyCheckEnabled || signalModel.BuyQty < MinPnlMaxQty))
                    {
                        signalModel.Message = "Min PnL Check Failed on Pre-Check";
                        _log.Info($"Source: Edge scan feed auto trader, Message: min PnL check failed on pre-check. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                        return false;
                    }
                    else if (position.NetQty != 0)
                    {
                        signalModel.Message = "Open Pos Found";
                        _log.Info($"Source: Edge scan feed auto trader, Message: open pos found. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                        return false;
                    }
                }
            }
            _log.Info($"Source: Edge scan feed auto trader, Message: Position Check Complete. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
            return true;
        }

        private bool IsValidSignal(EdgeScanFeedModel signalModel, bool checkBuySide, bool checkSellSide)
        {
            if (!AutoTraderRunning || AutoTraderBasketViewModel == null)
            {
                signalModel.Message = "Auto Trader OFF";
                return false;
            }
            if (DateTime.Now.ToEastern().TimeOfDay < EdgeScanFeedStartTimeEastern && !OverrideMarketOpenCheck)
            {
                signalModel.Message = "Auto Trader Not Allowed At This Time";
                return false;
            }
            if (!checkBuySide && !checkSellSide)
            {
                signalModel.Message = "No Side Selected";
                return false;
            }
            if (_totalSubmission >= AutoTraderMaxAllowedOrders)
            {
                signalModel.Message = "Max Order Submission Limit Reached";
                return false;
            }
            if (_totalSubmissionRate >= AutoTraderMaxOrderRate)
            {
                signalModel.Message = "Max Order Rate Limit Reached";
                return false;
            }
            if (_totalSubmissionRateSec >= AutoTraderMaxOrderRateSec)
            {
                signalModel.Message = "Max Order Rate Limit Reached";
                return false;
            }
            if (signalModel.Latency.TotalMilliseconds >= AutoTraderMaxLatency)
            {
                signalModel.Message = "Latency Check Failed";
                _log.Info($"Source: Edge scan feed auto trader, Message: latency check failed. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                return false;
            }
            if (signalModel.PossibleFirm || signalModel.IsFirm)
            {
                signalModel.Message = "Firm Trade Check Failed";
                _log.Info($"Source: Edge scan feed auto trader, Message: is firm trade. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);

                if (BlockFirmTradesForTime)
                {
                    BanSymbolCommand(signalModel);
                    if (!_spreadIdToUnbanTimerMap.TryGetValue(signalModel.SpreadId, out System.Timers.Timer timer))
                    {
                        timer = new System.Timers.Timer
                        {
                            AutoReset = false,
                            Interval = BlockFirmTradesForTimeInterval
                        };
                        timer.Elapsed += (_, _) => UnbanSymbolCommand(signalModel.SpreadId);
                        _spreadIdToUnbanTimerMap[signalModel.SpreadId] = timer;
                    }
                    timer.Stop();
                    timer.Start();
                }
                return false;
            }
            if (signalModel.Traded)
            {
                signalModel.Message = "Signal already traded";
                _log.Info($"Source: Edge scan feed auto trader, Message: signal already traded. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                return false;
            }
            if (signalModel.EdgeScannerType == EdgeScannerType.CrossedMarketMaker)
            {
                if (checkBuySide && signalModel.BuyTime > signalModel.SellTime)
                {
                    signalModel.Message = "Side same as latest trigger side";
                    _log.Info($"Source: Edge scan feed auto trader, Message: Side same as trigger order side. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    return false;
                }
                else if (checkSellSide && signalModel.BuyTime < signalModel.SellTime)
                {
                    signalModel.Message = "Side same as latest trigger side";
                    _log.Info($"Source: Edge scan feed auto trader, Message: Side same as trigger order side. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
                    return false;
                }
            }

            _log.Info($"Source: Edge scan feed auto trader, Message: Check For Trading. Time: {(DateTime.Now - signalModel.ReceiveTime).TotalMilliseconds}" + signalModel);
            return true;
        }

        private void OnItemEdgeAcquiredEvent(BasketTraderItemModel basketItem, double lastEdgeBeforeFees, double lastEdgeAfterFees)
        {
            try
            {
                basketItem.EdgeAcquiredEvent -= OnItemEdgeAcquiredEvent;
                if (BlockArea)
                {
                    foreach (TicketLegModel leg in basketItem.Legs)
                    {
                        DateTime expiration = leg.ExpirationInfo.Expiration;
                        double strike = leg.Strike.Strike;
                        Tuple<string, DateTime> key = Tuple.Create(basketItem.Underlying, expiration);

                        if (!_lastTradeAreaTimeMap.TryGetValue(key, out Dictionary<double, DateTime> dict))
                        {
                            dict = new Dictionary<double, DateTime>();
                            _lastTradeAreaTimeMap[key] = dict;
                        }
                        double block = lastEdgeAfterFees > 0 ? BlockAreaWinnerBlockSeconds : BlockAreaLoserBlockSeconds;
                        dict[strike] = DateTime.Now + TimeSpan.FromSeconds(block);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnItemEdgeAcquiredEvent));
            }
        }

        private static bool ConfirmBuySide(BasketTraderItemModel basketItem)
        {
            if (basketItem.IsSingleLeg)
            {
                if (basketItem.Side != Side.Buy)
                {
                    basketItem.Reverse();
                    return true;
                }
            }
            else
            {
                if (Math.Abs(basketItem.Low - basketItem.High) < .01)
                {
                    throw new SlimException("Side evaluation failed!");
                }

                switch (basketItem.BaseStrategy)
                {
                    case BaseStrategy.CALL_CALENDAR:
                    case BaseStrategy.PUT_CALENDAR:
                        if (basketItem.Side != Side.Buy)
                        {
                            basketItem.Reverse();
                            return true;
                        }
                        break;
                    default:
                        if (Math.Abs(basketItem.Low) > Math.Abs(basketItem.High) || basketItem.High < 0)
                        {
                            basketItem.Reverse();
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        private static bool ConfirmSellSide(BasketTraderItemModel basketItem)
        {
            if (basketItem.IsSingleLeg)
            {
                if (basketItem.Side != Side.Sell)
                {
                    basketItem.Reverse();
                    return true;
                }
            }
            else
            {
                if (Math.Abs(basketItem.Low - basketItem.High) < .01)
                {
                    throw new SlimException("Side evaluation failed!");
                }

                switch (basketItem.BaseStrategy)
                {
                    case BaseStrategy.CALL_CALENDAR:
                    case BaseStrategy.PUT_CALENDAR:
                        if (basketItem.Side != Side.Sell)
                        {
                            basketItem.Reverse();
                            return true;
                        }
                        break;
                    default:
                        if (Math.Abs(basketItem.Low) < Math.Abs(basketItem.High) || basketItem.Low > 0)
                        {
                            basketItem.Reverse();
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        private bool CheckForFilter(EdgeScanFeedModel model, bool checkBuySide, bool checkSellSide, out string errorOut, out EdgeScanFeedTradeFilterRowModel filter)
        {
            filter = null;
            StringBuilder error = OmsCore.Config.IsDevMode || DebugModeEnabled || _log.IsDebugEnabled
                ? new StringBuilder()
                : null;

            bool matchFound = false;

            if ((model.BuyTradeUnderlyingMid == 0 || double.IsNaN(model.BuyTradeUnderlyingMid)) && (model.SellTradeUnderlyingMid == 0 || double.IsNaN(model.SellTradeUnderlyingMid)))
            {
                BulletinMessage message = new BulletinMessage
                {
                    Time = model.BuyTime,
                    Message = $"Invalid Buy Under Px. {model.SpreadId}",
                    Source = model.EdgeScannerType.ToString().FromCamelCase()
                };
                _ = _bulletinBroker.AddMessageAsync(message);

                error?.Append("Buy Underlying value not valid\n");
            }
            else if (model.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence && model.SellTradeUnderlyingMid is 0 or double.NaN)
            {
                BulletinMessage message = new BulletinMessage
                {
                    Time = model.BuyTime,
                    Message = $"Invalid Sell Under Px. {model.SpreadId}",
                    Source = model.EdgeScannerType.ToString().FromCamelCase()
                };
                _ = _bulletinBroker.AddMessageAsync(message);
                error?.Append("Sell Underlying value not valid\n");
            }
            else if (model.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence && ((model.BuyPrice == 0 && !model.Mleg) || double.IsNaN(model.BuyPrice)))
            {
                error?.Append("Buy price not valid\n");
            }
            else if (model.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence && ((model.SellPrice == 0 && !model.Mleg) || double.IsNaN(model.SellPrice)))
            {
                error?.Append("Sell price not valid\n");
            }
            else if (SelectedModel == null)
            {
                matchFound = true;
            }
            else
            {
                for (int i = 0; i < SelectedModel.Filters.Count; i++)
                {
                    filter = SelectedModel.Filters[i];
                    error?.Append($"{SelectedModel.Title}, Filter Id: {i + 1}, \n");
                    if (filter.SelectedEdgeFeedScanners == null || !filter.SelectedEdgeFeedScanners.Contains(model.EdgeScannerType))
                    {
                        error?.Append("Scanner type filter failed.\n");
                        continue;
                    }
                    if (filter.SelectedLegTypes != LegTypes.All && (filter.SelectedLegTypes != LegTypes.MLeg || !model.Mleg) && (filter.SelectedLegTypes != LegTypes.Single || model.Mleg))
                    {
                        error?.Append("Leg type filter failed.\n");
                        continue;
                    }
                    if (filter.MinDte > 0 && (model.NearExpiration.Date - DateTime.Today).TotalDays < filter.MinDte)
                    {
                        error?.Append("Near Expiration Min DTE Check Failed.\n");
                        continue;
                    }
                    if (filter.MaxDte > 0 && (model.FarExpiration.Date - DateTime.Today).TotalDays > filter.MaxDte)
                    {
                        error?.Append("Far Expiration Max DTE Check Failed.\n");
                        continue;
                    }
                    if (model.NearExpiration < filter.MinNearExpirationFilter || model.NearExpiration > filter.MaxNearExpirationFilter)
                    {
                        error?.Append("Near Expiration Check Failed.\n");
                        continue;
                    }
                    if (model.FarExpiration < filter.MinFarExpirationFilter || model.FarExpiration > filter.MaxFarExpirationFilter)
                    {
                        error?.Append("Far Expiration Check Failed.\n");
                        continue;
                    }
                    if (filter.MaxUnderlying > 0)
                    {
                        if (checkBuySide && (model.BuyTradeUnderlyingMid < filter.MinUnderlying || model.BuyTradeUnderlyingMid > filter.MaxUnderlying))
                        {
                            error?.Append("Underlying Range Check Failed.\n");
                            continue;
                        }
                        if (checkSellSide && (model.SellTradeUnderlyingMid < filter.MinUnderlying || model.SellTradeUnderlyingMid > filter.MaxUnderlying))
                        {
                            error?.Append("Underlying Range Check Failed.\n");
                            continue;
                        }
                    }
                    if (filter.MaxDelta > 0 && (model.AbsDelta < filter.MinDelta || model.AbsDelta > filter.MaxDelta))
                    {
                        error?.Append("Delta Filter Check Failed.\n");
                        continue;
                    }
                    if (model.Uncertain && !filter.AllowUncertain)
                    {
                        error?.Append("Uncertain symbols not allowed.\n");
                        continue;
                    }
                    if (model.QtyMismatch && !filter.AllowQtyMismatch)
                    {
                        error?.Append("Qty Mismatch not allowed.\n");
                        continue;
                    }
                    if (filter.MinQty > 0 && ((checkBuySide && model.BuyQty < filter.MinQty) || (checkSellSide && model.SellQty < filter.MinQty)))
                    {
                        error?.Append("Min Qty Check Failed.\n");
                        continue;
                    }
                    if (filter.MaxQty > 0 && ((checkBuySide && model.BuyQty > filter.MaxQty) || (checkSellSide && model.SellQty > filter.MaxQty)))
                    {
                        error?.Append("Max Qty Check Failed.\n");
                        continue;
                    }
                    if (filter.MinBidAskSize > 0)
                    {
                        if (checkBuySide && (model.BuyBidSize < filter.MinBidAskSize || model.BuyAskSize < filter.MinBidAskSize))
                        {
                            error?.Append("Min Bid/Ask Size Check Failed.\n");
                            continue;
                        }
                        if (checkSellSide && (model.SellBidSize < filter.MinBidAskSize || model.SellAskSize < filter.MinBidAskSize))
                        {
                            error?.Append("Min Bid/Ask Size Check Failed.\n");
                            continue;
                        }
                    }
                    if (model.EdgeScannerType is EdgeScannerType.LoopFinder or EdgeScannerType.DeltaAdjustedLoopFinder or EdgeScannerType.IvChangeDeltaAdjLoopFinder or EdgeScannerType.CopyCatWithEdge or EdgeScannerType.EdgeToTheoDivergence)
                    {
                        if (filter.LoopTimeSpan > 0 && Math.Abs((model.BuyTime - model.SellTime).TotalMilliseconds) > filter.LoopTimeSpan)
                        {
                            error?.Append("Loop Time Span Check Failed.\n");
                            continue;
                        }
                    }
                    if (filter.MaxPrice > 0 && ((model.BuyPrice < filter.MinPrice && model.SellPrice < filter.MinPrice) || (model.BuyPrice > filter.MaxPrice && model.SellPrice > filter.MaxPrice)))
                    {
                        error?.Append("Price Check Failed.\n");
                        continue;
                    }
                    var edge = Math.Round(model.Width, 2);
                    if (filter.EdgeRangeEnabled && (edge < filter.MinEdge || (filter.MaxEdge > 0 && edge > filter.MaxEdge)))
                    {
                        error?.Append("Edge Check Failed.\n");
                        continue;
                    }
                    var deltaAdjEdge = Math.Round(model.DeltaAdjEdge, 2);
                    if (filter.DeltaAdjEdgeRangeEnabled && (deltaAdjEdge < filter.MinDeltaAdjEdge || (filter.MaxDeltaAdjEdge > 0 && deltaAdjEdge > filter.MaxDeltaAdjEdge)))
                    {
                        error?.Append("Delta Adj Edge Check Failed.\n");
                        continue;
                    }
                    double buyWidth = Math.Abs(model.BuyTradeAsk - model.BuyTradeBid);
                    double sellWidth = Math.Abs(model.SellTradeAsk - model.SellTradeBid);
                    if (filter.MaxMarketWidth > 0 &&
                        ((checkBuySide && (buyWidth < filter.MinMarketWidth || buyWidth > filter.MaxMarketWidth)) ||
                        (checkSellSide && (sellWidth < filter.MinMarketWidth || sellWidth > filter.MaxMarketWidth))))
                    {
                        error?.Append("Market Width Check Failed.\n");
                        continue;
                    }
                    if (filter.UnderlyingWidth > 0)
                    {
                        if (checkBuySide && model.BuyUnderlyingWidth > filter.UnderlyingWidth)
                        {
                            error?.Append("Underlying Width Check Failed.\n");
                            continue;
                        }
                        if (checkSellSide && model.SellUnderlyingWidth > filter.UnderlyingWidth)
                        {
                            error?.Append("Underlying Width Check Failed.\n");
                            continue;
                        }
                    }
                    if (filter.MaxTimeDelay > 0)
                    {
                        DateTime timeNowEastern = DateTime.Now.ToEastern();
                        DateTime older;
                        if (model.BuyTime.Date == DateTime.Today && model.SellTime.Date == DateTime.Today)
                        {
                            older = model.BuyTime < model.SellTime ? model.BuyTime : model.SellTime;
                        }
                        else if (model.BuyTime.Date == DateTime.Today)
                        {
                            older = model.BuyTime;
                        }
                        else if (model.SellTime.Date == DateTime.Today)
                        {
                            older = model.SellTime;
                        }
                        else
                        {
                            error?.Append("Invalid Time.\n");
                            continue;
                        }

                        if ((timeNowEastern - older).TotalMilliseconds > filter.MaxTimeDelay)
                        {
                            error?.Append("max time delay check failed.\n");
                            continue;
                        }
                    }
                    if (filter.MaxChangeInUnderlyingEnabled && Math.Abs(model.BuyTradeUnderlyingMid - model.SellTradeUnderlyingMid) > filter.MaxChangeInUnderlying)
                    {
                        error?.Append("Underlying change Check Failed.\n");
                        continue;
                    }
                    if (filter.MinLegDeltaEnabled && (double.IsNaN(model.HighestLegDelta) || Math.Abs(model.HighestLegDelta) < filter.MinLegDelta))
                    {
                        error?.Append("Min leg delta check failed.\n");
                        continue;
                    }
                    if (filter.MaxSpreadWeightedVegaEnabled && (double.IsNaN(model.SpreadWeightedVega) || Math.Abs(model.SpreadWeightedVega) > filter.MaxSpreadWeightedVega))
                    {
                        error?.Append("Max spread weighted vega check failed.\n");
                        continue;
                    }
                    if (filter.BlockedExpirationsSet != null && (filter.BlockedExpirationsSet.Contains(model.NearExpiration.Date) || filter.BlockedExpirationsSet.Contains(model.FarExpiration.Date)))
                    {
                        error?.Append("Blocked Expiration Check Failed.\n");
                        continue;
                    }
                    if (filter.SelectedTradeConditionCodes != null)
                    {
                        bool match = filter.SelectedTradeConditionCodes.Contains(model.BuyConditionCode) ||
                                     filter.SelectedTradeConditionCodes.Contains(model.SellConditionCode);
                        if (!match)
                        {
                            error?.Append("Condition Code Check Failed.\n");
                            continue;
                        }
                    }
                    if (filter.BlockedUnderlyingsMapContains(model.UnderSymbol))
                    {
                        error?.Append("Blocked Underlying Check Failed.\n");
                        continue;
                    }
                    if (!filter.AllowUnderlyingsMapContains(model.UnderSymbol))
                    {
                        error?.Append("Underlying List Check Failed.\n");
                        continue;
                    }
                    if (filter.StrategyToModelMap != null && filter.StrategyToModelMap.TryGetValue(model.SpreadType, out StrategyModel strategyModel) && !strategyModel.IsChecked)
                    {
                        error?.Append("Strategy Check Failed.\n");
                        continue;
                    }
                    if (filter.MinEdgeToTheoEnabled)
                    {
                        var buyEdgeToTheo = model.BuyEdgeToTheo;
                        var sellEdgeToTheo = model.SellEdgeToTheo;

                        if (model.EdgeScannerType == EdgeScannerType.FullAuto && checkSellSide)
                        {
                            sellEdgeToTheo = model.BuyPrice - model.BuyTradeTheo;
                        }

                        bool buyEttPassed = checkBuySide && buyEdgeToTheo >= filter.MinEdgeToTheo;
                        bool sellEttPassed = checkSellSide && sellEdgeToTheo >= filter.MinEdgeToTheo;
                        if (!buyEttPassed && !sellEttPassed)
                        {
                            error?.Append("Edge to Theo Check Failed.\n");
                            continue;
                        }
                    }

                    if (filter.MinBidPercentEnabled || filter.MaxBidPercentEnabled)
                    {
                        double modelBuyBidPercent = model.BuyBidPercent;
                        double modelSellBidPercent = 1 - model.SellBidPercent;

                        if (model.EdgeScannerType == EdgeScannerType.FullAuto && checkSellSide)
                        {
                            modelSellBidPercent = 1 - model.BuyBidPercent;
                        }

                        if (filter.MinBidPercentEnabled)
                        {
                            bool buyBidPercent = checkBuySide && modelBuyBidPercent >= filter.MinBidPercent;
                            bool sellBidPercent = checkSellSide && modelSellBidPercent >= filter.MinBidPercent;
                            if (!buyBidPercent && !sellBidPercent)
                            {
                                error?.Append("Min Bid Percent Check Failed.\n");
                                continue;
                            }
                        }

                        if (filter.MaxBidPercentEnabled)
                        {
                            bool buyBidPercent = checkBuySide && modelBuyBidPercent <= filter.MaxBidPercent;
                            bool sellBidPercent = checkSellSide && modelSellBidPercent <= filter.MaxBidPercent;
                            if (!buyBidPercent && !sellBidPercent)
                            {
                                error?.Append("Max Bid Percent Check Failed.\n");
                                continue;
                            }
                        }
                    }

                    if (filter.MinBidEnabled)
                    {
                        bool buyBid = checkBuySide && model.BuyTradeBid >= filter.MinBid;
                        bool sellBid = checkSellSide && model.SellTradeBid >= filter.MinBid;
                        if (!buyBid && !sellBid)
                        {
                            error?.Append("Min Bid Check Failed.\n");
                            continue;
                        }
                    }

                    if (filter.MinNotionalEnabled)
                    {
                        if (model.Notional < filter.MinNotional)
                        {
                            error?.Append("Min Notional Check Failed.\n");
                            continue;
                        }
                    }

                    if (filter.MinLoopCount > 0)
                    {
                        if (model.FlipCount < filter.MinLoopCount)
                        {
                            error?.Append("Loop Count Check Failed.\n");
                            continue;
                        }
                    }

                    if (model.EdgeScannerType == EdgeScannerType.PriceChainDeviation)
                    {
                        double timeDiff = Math.Min(model.PriceChainRecentBidDeviationTimeDiff,
                            model.PriceChainRecentAskDeviationTimeDiff);
                        if (timeDiff > filter.PriceChainDeviationMaxLookBackTime)
                        {
                            error?.Append("Price Chain Deviation Max Look Back Time Check Failed.\n");
                            continue;
                        }

                        double under = Math.Abs(model.BuyTradeUnderlyingMid - model.PriceChainRecentBidDeviationUnderMid) < Math.Abs(model.BuyTradeUnderlyingMid - model.PriceChainRecentAskDeviationUnderMid) ? model.PriceChainRecentBidDeviationUnderMid : model.PriceChainRecentAskDeviationUnderMid;
                        double changeInUnder = Math.Abs(under - model.BuyTradeUnderlyingMid);
                        double avg = (under + model.BuyTradeUnderlyingMid) / 2;
                        if (avg > 0)
                        {
                            double percentageDiff = changeInUnder / avg;
                            if (percentageDiff > filter.PriceChainDeviationMaxChangeInUnder)
                            {
                                error?.Append($"Price Chain Deviation Max Change In Under Check Failed. Under: {under}, Trade Under: {model.BuyTradeUnderlyingMid}\n");
                                continue;
                            }
                        }

                        double deviation = Math.Max(model.PriceChainRecentBidDeviation,
                            model.PriceChainRecentAskDeviation);
                        if (deviation < filter.PriceChainDeviationMinDeviation)
                        {
                            error?.Append("Price Chain Deviation Min Deviation Check Failed.\n");
                            continue;
                        }

                        double tradeWidth = Math.Abs(model.BuyTradeAsk - model.BuyTradeBid);
                        if (tradeWidth < filter.PriceChainDeviationMinMarketWidthAtTrade)
                        {
                            error?.Append("Price Chain Deviation Min Market Width At Trade Check Failed.\n");
                            continue;
                        }

                        double deviationWidth = Math.Min(model.PriceChainRecentBidDeviationWidth,
                            model.PriceChainRecentAskDeviationWidth);
                        if (deviationWidth > filter.PriceChainDeviationMaxMarketWidthAtViolation)
                        {
                            error?.Append("Price Chain Deviation Max Market Width At Violation Check Failed.\n");
                            continue;
                        }
                    }

                    string[] exchanges = model.Exchange.Split(", ");
                    string exchange = exchanges[0];
                    string exchange2 = exchanges.Length == 1 ? exchanges[0] : exchanges[1];
                    if (filter.BlockedExchangeMapContains(exchange) || filter.BlockedExchangeMapContains(exchange2))
                    {
                        error?.Append("Blocked Exchange Check Failed.\n");
                        continue;
                    }
                    if (!filter.AllowExchangeMapContains(exchange) && !filter.AllowExchangeMapContains(exchange2))
                    {
                        error?.Append("Exchange List Check Failed.\n");
                        continue;
                    }

                    matchFound = true;
                    break;
                }
            }

            if (error != null)
            {
                errorOut = error.ToString().TrimEnd();
                if (_log.IsDebugEnabled)
                {
                    _log.Debug($"Filter. Module Id: {ModuleTitle}, Filter: {SelectedModel?.Title}, Model: {model.SpreadId}, Message: \n{errorOut}");
                }
            }
            else
            {
                errorOut = null;
            }

            return matchFound;
        }

        public override void OnDispose()
        {
            StopAutoTraderCommand();
            if (AutoTraderBasketViewModel != null)
            {
                AutoTraderBasketViewModel.IsEdgeScanFeedAutoTrader = false;
                AutoTraderBasketView?.Dispatcher.BeginInvoke(() => AutoTraderBasketView.Close());
            }
        }

        private bool IsAutoTraderBasketActive(bool show)
        {
            bool isActive = false;
            try
            {
                if (AutoTraderBasketView != null && !AutoTraderBasketView.Dispatcher.HasShutdownFinished)
                {
                    AutoTraderBasketView.Dispatcher.Invoke(() =>
                    {
                        if (show)
                        {
                            AutoTraderBasketView.Activate();
                            AutoTraderBasketView.Visibility = Visibility.Visible;
                        }
                        isActive = AutoTraderBasketView.IsLoaded;
                    });
                }
                return isActive;
            }
            catch (Exception)
            {
                return isActive;
            }
        }

        [Command]
        public void ShowUniqueListCommand()
        {
            try
            {
                Trades = ShowUniqueSelected ? _uniqueTrades : _alltrades;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowUniqueListCommand));
            }
        }

        [Command]
        public void ShowFilterSelectorCommand()
        {
            try
            {
                EdgeScanFeedFilterSelectionView view = new();
                if (view.DataContext is EdgeScanFeedFilterSelectionViewModel viewModel)
                {
                    viewModel.EdgeScanFeed = this;
                    view.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowFilterSelectorCommand));
            }
        }

        [Command]
        public void OpenAutoTraderCommand()
        {
            IsAutoTraderBasketActive(show: true);
        }

        [Command]
        public void CancelAllAutoTraderCommand()
        {
            AutoTraderBasketViewModel.CancelAllNoCheck();
        }

        [Command]
        public async Task StartAutoTraderCommand()
        {
            if (!AutoTraderRunning)
            {
                var response = await ShowMessage("Are you sure you want to start auto trader?");
                if (response == RiskWarningMessageResponse.Proceed)
                {
                    if (AutoTraderBasketView != null)
                    {
                        DumpStartRequestForTester();

                        if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Local)
                        {
                            await CheckForSize();

                            if (IsAutoTraderBasketActive(show: false))
                            {
                                CheckForOrderRate();
                                AutoTraderRunning = true;
                            }
                        }
                        else if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Agent)
                        {
                            EdgeScanFeedFilterConfig filterConfig = GetScannerConfig();

                            if (SelectedModel != null)
                            {
                                filterConfig.FilterString = JsonConvert.SerializeObject(SelectedModel);
                            }

                            var startRequest = new EdgeScanFeedRunnerStartRequest
                            {
                                RunnerId = this.RunnerId,
                                FilterConfig = filterConfig,
                                AutoTraderConfig = AutoTraderBasketViewModel.GetAutoTraderConfig(),
                                OrderDefaults = BuildOrderDefaults(),
                                BlockedSymbolModel = BlockedSymbolModel,
                            };

                            TransactionConsumerModel.RegisterServerRunner(startRequest, this);
                            AutoTraderRunning = true;

                            string configJson = JsonConvert.SerializeObject(startRequest, Formatting.Indented);
                            _log.Info("Sending Server Edge Scan Runner Start Request:\n" + configJson);
                        }
                        UpdateStats(false);
                    }
                    else
                    {
                        _ = ShowMessage("Edge Scan Feed Basket not ready!");
                    }
                }
            }
        }

        private OrderSubmissionDefaults BuildOrderDefaults()
        {
            InstanceMode instanceMode = AutoTraderBasketViewModel.GetInstanceMode();
            return new OrderSubmissionDefaults
            {
                Account = OmsCore.Config.DefaultAccount,
                Route = OmsCore.Config.DefaultRoute(instanceMode),
                SingleLegRoute = OmsCore.Config.DefaultSingleLegRoute(instanceMode),
                RouteSpxRutXsp = OmsCore.Config.DefaultRouteSpxRutXsp(instanceMode),
                RouteNdx = OmsCore.Config.DefaultRouteNdx(instanceMode),
            };
        }

        private void DumpStartRequestForTester()
        {
            try
            {
                if (!OmsCore.Config.IsDevMode)
                {
                    return;
                }

                EdgeScanFeedFilterConfig filterConfig = GetScannerConfig();

                // Inline the currently selected filter model into FilterString so the runner
                // tester (which has no access to the filter config DB) can reproduce the exact
                // subscription/filter behavior instead of having to resolve FilterConfigId.
                if (SelectedModel != null)
                {
                    filterConfig.FilterString = JsonConvert.SerializeObject(SelectedModel);
                }

                var startRequest = new
                {
                    RunnerId = this.RunnerId,
                    FilterConfig = filterConfig,
                    AutoTraderConfig = AutoTraderBasketViewModel.GetAutoTraderConfig(),
                    OrderDefaults = BuildOrderDefaults(),
                };

                string json = JsonConvert.SerializeObject(startRequest, Formatting.Indented);

                string dumpDir = Path.Combine(Path.GetTempPath(), "ZeroPlus", "EdgeScanFeed");
                Directory.CreateDirectory(dumpDir);

                string safeRunnerId = EdgeScanFeedRunnerIdFactory.ToSafeFileName(RunnerId);
                string fileName = $"{safeRunnerId}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                string dumpPath = Path.Combine(dumpDir, fileName);

                File.WriteAllText(dumpPath, json);

                _log.Info($"Edge Scan Feed start request dumped for tester: {dumpPath}\n{json}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DumpStartRequestForTester));
            }
        }

        [Command]
        public void StopAutoTraderCommand()
        {
            try
            {
                AutoTraderRunning = false;
                if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Agent)
                {
                    _lastStopRequestUtc = DateTime.UtcNow;
                    TransactionConsumerModel.UnregisterServerRunner(RunnerId, this);
                }
                UpdateStats(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAutoTraderCommand));
            }
        }

        private void CheckForOrderRate()
        {
            _orderRateLastUpdateTime = DateTime.Now;
            _totalSubmissionRate = 0;

            _orderRateLastUpdateTimeSec = DateTime.Now;
            _totalSubmissionRateSec = 0;
        }

        private async Task CheckForSize()
        {
            AutomationConfigModel automationConfig = AutoTraderBasketViewModel.GetAutomationConfig();
            if (automationConfig is { LoopCloseEdgeType: LoopCloseEdgeType.Dynamic })
            {
                if (automationConfig.DynamicEdgeModel is not null and DynamicEdgeModel dynamicEdgeModel)
                {
                    if (dynamicEdgeModel.DteTable.Any(x => x.Qty > 1 || x.VerticalQty > 1))
                    {
                        var message = "Your are about to start an Trader with size.\nWould you like to reset the size before starting?";
                        var qtyResetPrompt = await ShowMessage(message);
                        if (qtyResetPrompt != RiskWarningMessageResponse.Proceed)
                        {
                            return;
                        }
                        foreach (DaysToExpirationEdgeModel table in dynamicEdgeModel.DteTable.Where(x => x.Qty > 1 || x.VerticalQty > 1))
                        {
                            table.Qty = 1;
                            table.VerticalQty = 1;
                        }
                    }
                }
            }
        }

        [Command]
        public void ClearEdgeScanFeedTableCommand()
        {
            try
            {
                Dispatcher?.BeginInvoke(() =>
                {
                    _alltrades.Clear();
                    _uniqueTrades.Clear();
                    _uniqueTradesList.Clear();
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearEdgeScanFeedTableCommand));
            }
        }

        [Command]
        public void BanSymbolCommand(EdgeScanFeedModel model)
        {
            try
            {
                if (BlockedSymbolModel != null && BlockedSymbolModel.SymbolsSet != null)
                {
                    BlockedSymbolModel.Symbols.Add(new BlockedSymbolModelItem()
                    {
                        Symbol = model.SpreadId,
                    });
                    BlockedSymbolModel.UpdateSet();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BanSymbolCommand));
            }
        }

        [Command]
        public void UnbanSymbolCommand(string model)
        {
            try
            {
                if (BlockedSymbolModel != null && BlockedSymbolModel.SymbolsSet != null)
                {
                    foreach (BlockedSymbolModelItem symbol in BlockedSymbolModel.Symbols)
                    {
                        if (symbol.Symbol == model)
                        {
                            BlockedSymbolModel.Symbols.Remove(symbol);
                        }
                    }
                    BlockedSymbolModel.UpdateSet();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnbanSymbolCommand));
            }
        }

        [Command]
        public void ShowBannedListCommand()
        {
            try
            {
                BannedSymbolsListView view = new()
                {
                    DataContext = this
                };
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowBannedListCommand));
            }
        }

        [Command]
        public void RowDoubleClick(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is EdgeScanFeedModel trade)
            {
                OpenInComplexOrderTicket(trade);
            }
        }


        [Command]
        public void OpenInPositionAnalyzerCommand(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) &&
                    parameter is EdgeScanFeedModel orderModel)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        PositionAnalyzerView view = new();
                        view.Loaded += (s, e) =>
                        {
                            if (view.DataContext is PositionAnalyzerViewModel viewModel)
                            {
                                viewModel.InputString = orderModel.BuySymbol;
                                viewModel.BasePrice = orderModel.BuyPrice;
                                viewModel.Side = Side.Buy;
                                viewModel.Quantity = orderModel.BuyQty;
                                _ = viewModel.AddCommand();
                            }
                        };
                        view.Show();
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInPositionAnalyzerCommand));
            }
        }

        [Command]
        public void OpenInComplexOrderTicket(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) &&
                    parameter is EdgeScanFeedModel trade)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        if (!trade.Mleg && OmsCore.Config.UseOrderTicketForSingleLegOrders)
                        {
                            window = new OrderTicketView();
                        }
                        else
                        {
                            switch (OmsCore.Config.DefaultOrderTicketStyle)
                            {
                                case OrderTicketStyle.Complex:
                                    window = new ComplexOrderTicketView();
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView();
                                    break;
                            }
                        }

                        ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window!.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (_, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) =>
                        {
                            _ = viewModel.LoadLegsFromTosAsync(trade.BuySymbol, trade.Side, true);

                            if (OmsCore.Config.OpenSeparateTicketForUnderlying)
                            {
                                double left = 10;
                                double top = 20;
                                double width = 600;
                                double height = 300;
                                window.Dispatcher.Invoke(() =>
                                {
                                    left = window.Left;
                                    top = window.Top;
                                    width = window.Width;
                                    height = window.Height;
                                });
                                _ = viewModel.OpenUnderlyingTicket(left, top, width, height);
                            }
                        };

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        [Command]
        public void OpenInTradePnlChartCommand(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is EdgeScanFeedModel trade)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        TradePnlChartView window = new();
                        TradePnlChartViewModel viewModel = (TradePnlChartViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (_, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                        viewModel.Ready += _ =>
                        {
                            viewModel.LoadFromEdgeScanFeed(trade);
                        };

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInTradePnlChartCommand));
            }
        }

        [Command]
        public void OpenInBasketTrader(object parameter)
        {
            try
            {
                if (parameter is EdgeScanFeedModel trade)
                {
                    if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel })
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
                            viewModel.LoadFromTradeAsync(trade);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
            }
        }

        private void FilterInNewOrderBook(object parameter)
        {
            try
            {
                if (parameter is string filterString)
                {
                    TransactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OrderBookWindowView orderbookWindow = new();

                        orderbookWindow.OrderBookView.Ready += () =>
                        {
                            orderbookWindow.CloneFrom("FilterFromTrade");
                            orderbookWindow.OrderBookView.HideWorkingOrders();
                            ((OrderBookViewModel)orderbookWindow.DataContext).FilterString = filterString;
                        };

                        orderbookWindow.Show();
                    }));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FilterInNewOrderBook));
            }
        }

        [Command]
        public void OpenInOrderBook()
        {
            try
            {
                string commentId = IsServerRunnerMode
                    ? RunnerId
                    : AutoTraderBasketViewModel?.BasketSettings?.Uid;

                if (string.IsNullOrWhiteSpace(commentId))
                {
                    return;
                }

                TransactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                {
                    string filterString = $"([Comment] == '{commentId}')";
                    OrderBookWindowView orderbookWindow = new();

                    orderbookWindow.OrderBookView.Ready += () =>
                    {
                        orderbookWindow.CloneFrom("FilterFromTrade");
                        orderbookWindow.OrderBookView.HideWorkingOrders();
                        ((OrderBookViewModel)orderbookWindow.DataContext).FilterString = filterString;
                    };

                    orderbookWindow.Show();
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInOrderBook));
            }
        }

        private void ChartSymbolBidAskIv(object parameter)
        {
            try
            {
                if (parameter is EdgeScanFeedModel model)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        ChartModuleView window = new();
                        ChartModuleViewModel viewModel = (ChartModuleViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (_, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                        viewModel.Ready += _ => viewModel.LoadSnapshotsChart(model.BuySymbol, model.BuyTradeUnderlyingMid);

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChartSymbolBidAskIv));
            }
        }

        private void SearchInNewTradesModule(object parameter)
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades) &&
                    parameter is string searchTerm)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        TradesView window = new();
                        TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (_, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                        SymbolCodec codec = new(searchTerm);
                        viewModel.Ready += _ =>
                        {
                            viewModel.Symbol = searchTerm;
                            viewModel.LegTypes = codec.LegCount > 1 ? LegTypes.MLeg : LegTypes.Single;
                            viewModel.Refresh();
                        };
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchInNewTradesModule));
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            bool isVisible = false;
            if (!AutoTraderBasketView.Dispatcher.HasShutdownStarted && !AutoTraderBasketView.Dispatcher.HasShutdownFinished)
            {
                AutoTraderBasketView.Dispatcher.Invoke(() =>
                {
                    isVisible = AutoTraderBasketView.IsLoaded && AutoTraderBasketView.Visibility == Visibility.Visible;
                });
            }

            EdgeScanFeedFilterConfig config = GetScannerConfig();
            config.BasketOpen = isVisible;
            config.AutoTraderConfig = AutoTraderBasketView.Basket.GetConfigAsJson();
            string json = JsonConvert.SerializeObject(config);
            return json;
        }

        private EdgeScanFeedFilterConfig GetScannerConfig()
        {
            EdgeScanFeedFilterConfig config = new()
            {
                AutoScrollEdgeFeed = AutoScrollEdgeFeed,
                EnableLogMode = EnableLogMode,
                AutoTraderSkipActiveOrders = AutoTraderSkipActiveOrders,
                MarkPrices = MarkPrices,
                MarkPricesMinEdge = MarkPricesMinEdge,
                CutoffTime = CutoffTime,
                SaveTime = SaveTime,
                AutoSave = AutoSave,
                AutoStop = AutoStop,
                FilterConfig = SelectedModel?.Title,
                FilterConfigId = (SelectedModel?.Details?.Id) ?? 0,
                AutoTraderEdgeOverride = AutoTraderEdgeOverride,
                AutoTraderSideSelector = AutoTraderSideSelector,
                AutoTraderUseTradePrice = AutoTraderUseTradePrice,
                ConfirmWithIbCob = ConfirmWithIbCob,
                AutoTraderAttemptBothSides = AutoTraderAttemptBothSides,
                AutoTraderDoNotTradeThroughFillPrice = AutoTraderDoNotTradeThroughFillPrice,
                AutoTraderMinQty = AutoTraderMinQty,
                AutoTraderRouteOption = AutoTraderRouteOption,
                AutoTraderMaxLatency = AutoTraderMaxLatency,
                AutoTraderMaxOpenPos = AutoTraderMaxOpenPos,
                BlockedSymbolModelId = BlockedSymbolModelId,
                BlockAlreadyTradedSymbols = BlockAlreadyTradedSymbols,
                BlockFirmTradesForTime = BlockFirmTradesForTime,
                BlockArea = BlockArea,
                BlockAreaStrikeRange = BlockAreaStrikeRange,
                BlockFirmTradesForTimeInterval = BlockFirmTradesForTimeInterval,
                BlockAlreadyTradedSymbolsTimeout = BlockAlreadyTradedSymbolsTimeout,
                AutoTraderResubmitCount = AutoTraderResubmitCount,
                MinPnlMaxQtyCheckEnabled = MinPnlMaxQtyCheckEnabled,
                MinPnlMaxQty = MinPnlMaxQty,
                AutoTraderMaxAllowedOrders = AutoTraderMaxAllowedOrders,
                AutoTraderMaxOrderRate = AutoTraderMaxOrderRateSec,
                AutoTraderCheckForVisualFilters = AutoTraderCheckForVisualFilters,
                FilterString = FilterString,
                AudioAlertEnabled = AudioAlertEnabled,
                ExchToRouteMapV3 = ExchToRouteMap,
                AudioAlertSound = AudioAlertSound,
                MinPnlForAutoTraderEnabled = MinPnlForAutoTraderEnabled,
                MinPnlForAutoTrader = MinPnlForAutoTrader,
                AutoTraderEnablePayUpTicks = AutoTraderEnablePayUpTicks,
                AutoTraderPayUpTicks = AutoTraderPayUpTicks,
                EdgeScanFeedRunnerOption = EdgeScanFeedRunnerOption,
                LoadWithStockTiedLeg = LoadWithStockTiedLeg,
            };
            return config;
        }

        public override async Task DeserializeAndLoadConfig(string json, bool withContent)
        {
            try
            {
                json = json.Replace("EdgeScanFeedViewModelConfig", "EdgeScanFeedFilterConfig");

                EdgeScanFeedFilterConfig config = JsonConvert.DeserializeObject<EdgeScanFeedFilterConfig>(json);
                await LoadConfig(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeserializeAndLoadConfig));
            }
        }

        internal async Task LoadConfig(EdgeScanFeedFilterConfig config)
        {
            try
            {
                await TransactionConsumerModel.UnsubscribeAllAsync(this);
                LoadModuleConfig(config);
                ReloadBannedSymbolsList();
                LoadAutoTraderConfig();
                await LoadFilterFromConfig(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfig));
            }
        }

        private void LoadAutoTraderConfig()
        {
            try
            {
                if (AutoTraderBasketView != null && _moduleConfig != null && !string.IsNullOrWhiteSpace(_moduleConfig.AutoTraderConfig))
                {
                    AutoTraderBasketView?.LoadConfigFromJsonAsync(_moduleConfig.AutoTraderConfig);
                }
                if (_moduleConfig is { BasketOpen: true })
                {
                    OpenAutoTraderCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadAutoTraderConfig));
            }
        }

        private async Task LoadFilterFromConfig(EdgeScanFeedFilterConfig config)
        {
            try
            {
                if (config.FilterConfigId != 0)
                {
                    await LoadFilterFromConfigId(config.FilterConfigId);
                }
                else if (!string.IsNullOrWhiteSpace(config.FilterConfig))
                {
                    List<ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedFilter);
                    ConfigSave filter = configs.FirstOrDefault(x => x.Title.Equals(config.FilterConfig, StringComparison.InvariantCultureIgnoreCase) && x.OwnerId == OmsCore.User.ID);
                    if (filter != null)
                    {
                        await LoadFilterFromConfigId(filter.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFilterFromConfig));
            }
        }

        private async Task LoadFilterFromConfigId(int configFilterConfigId)
        {
            ConfigSave configDetail = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(configFilterConfigId));
            if (configDetail != null)
            {
                EdgeScanFeedTradeFilterModel model = await Task.Run(() => JsonConvert.DeserializeObject<EdgeScanFeedTradeFilterModel>(configDetail.ConfigJson));
                model.NormalizeAfterLoad();
                LoadFilterModel(model);
            }
        }

        public void LoadFilterModel(EdgeScanFeedTradeFilterModel model)
        {
            TransactionConsumerModel.UnsubscribeAll(this);
            SelectedModel = model;
            ClearEdgeScanFeedTableCommand();

            if (model != null && model.Filters.Any())
            {
                foreach (var filter in model.Filters)
                {
                    foreach (var edgeScannerType in filter.SelectedEdgeFeedScannersExport)
                    {
                        SubscriptionFieldType type = (SubscriptionFieldType)edgeScannerType;

                        if (string.IsNullOrWhiteSpace(filter.AllowUnderlyings))
                        {
                            if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Local)
                            {
                                TransactionConsumerModel.Subscribe("*", type, this);
                            }
                        }
                        else
                        {
                            var underlyings = filter.AllowUnderlyings.Replace(",", ";").Split(";").Select(x => x.Trim()).Distinct();
                            foreach (var under in underlyings)
                            {
                                if (EdgeScanFeedRunnerOption == EdgeScanFeedRunnerOption.Local)
                                {
                                    TransactionConsumerModel.Subscribe(under, type, this);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.EdgeScanFeedRunnerState
                && value is EdgeScanFeedRunnerState state)
            {
                HandleServerRunnerStateChanged(state);
                return;
            }
            if (value is Tuple<EdgeScanFeedModel, EdgeScanFeedModel> feed)
            {
                OnEdgeScanFeedReceivedEvent(feed.Item1, feed.Item2);
                _totalReceived++;
                _lastReceived++;
            }
        }

        private void HandleServerRunnerStateChanged(EdgeScanFeedRunnerState state)
        {
            try
            {
                _log.Info($"server_runner_state_changed runner={RunnerId} state={state}");

                ServerRunnerState = state;

                bool errored = state == EdgeScanFeedRunnerState.Error;
                bool serverStopped = state == EdgeScanFeedRunnerState.Stopped;

                bool stopAckedFromUserRequest = serverStopped
                    && _lastStopRequestUtc != DateTime.MinValue
                    && (DateTime.UtcNow - _lastStopRequestUtc) <= ServerStopAckGracePeriod;

                bool unexpectedStop = serverStopped && AutoTraderRunning && !stopAckedFromUserRequest;

                if (stopAckedFromUserRequest)
                {
                    _lastStopRequestUtc = DateTime.MinValue;
                    _log.Info($"server_runner_stopped_acked runner={RunnerId}");
                }

                if (errored || unexpectedStop)
                {
                    AutoTraderRunning = false;
                    UpdateStats(false);

                    string reason = errored ? "errored" : "stopped unexpectedly";
                    string message = $"Server runner '{RunnerId}' {reason}.";
                    Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage(message, "Edge Scan Feed Runner", MessageButton.OK, MessageIcon.Warning));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleServerRunnerStateChanged));
            }
        }

        private void LoadModuleConfig(EdgeScanFeedFilterConfig config)
        {
            try
            {
                _moduleConfig = config;
                AutoScrollEdgeFeed = config.AutoScrollEdgeFeed;
                EnableLogMode = config.EnableLogMode;
                MarkPrices = config.MarkPrices;
                MarkPricesMinEdge = config.MarkPricesMinEdge;
                AutoTraderSkipActiveOrders = config.AutoTraderSkipActiveOrders;
                AutoTraderEdgeOverride = config.AutoTraderEdgeOverride;
                AutoTraderSideSelector = config.AutoTraderSideSelector;
                CutoffTime = config.CutoffTime;
                SaveTime = config.SaveTime;
                AutoStop = config.AutoStop;
                AutoSave = config.AutoSave;
                AutoTraderUseTradePrice = config.AutoTraderUseTradePrice;
                ConfirmWithIbCob = config.ConfirmWithIbCob;
                AutoTraderAttemptBothSides = config.AutoTraderAttemptBothSides;
                AutoTraderDoNotTradeThroughFillPrice = config.AutoTraderDoNotTradeThroughFillPrice;
                AutoTraderMinQty = config.AutoTraderMinQty;
                AutoTraderRouteOption = config.AutoTraderRouteOption;
                AutoTraderMaxLatency = config.AutoTraderMaxLatency;
                AutoTraderMaxOpenPos = config.AutoTraderMaxOpenPos;
                BlockedSymbolModelId = config.BlockedSymbolModelId;
                BlockAlreadyTradedSymbols = config.BlockAlreadyTradedSymbols;
                AutoTraderMaxAllowedOrders = config.AutoTraderMaxAllowedOrders;
                BlockFirmTradesForTime = config.BlockFirmTradesForTime;
                BlockArea = config.BlockArea;
                BlockAreaStrikeRange = config.BlockAreaStrikeRange;
                BlockFirmTradesForTimeInterval = config.BlockFirmTradesForTimeInterval;
                AutoTraderMaxOrderRateSec = Math.Min(30, config.AutoTraderMaxOrderRate);
                BlockAlreadyTradedSymbolsTimeout = config.BlockAlreadyTradedSymbolsTimeout;
                AutoTraderResubmitCount = config.AutoTraderResubmitCount;
                MinPnlMaxQtyCheckEnabled = config.MinPnlMaxQtyCheckEnabled;
                MinPnlMaxQty = config.MinPnlMaxQty;
                AutoTraderCheckForVisualFilters = config.AutoTraderCheckForVisualFilters;
                AudioAlertEnabled = config.AudioAlertEnabled;
                ExchToRouteMap = config.ExchToRouteMapV3;
                AudioAlertSound = config.AudioAlertSound;
                FilterString = config.FilterString;
                MinPnlForAutoTraderEnabled = config.MinPnlForAutoTraderEnabled;
                MinPnlForAutoTrader = config.MinPnlForAutoTrader;
                AutoTraderEnablePayUpTicks = config.AutoTraderEnablePayUpTicks;
                AutoTraderPayUpTicks = config.AutoTraderPayUpTicks;
                EdgeScanFeedRunnerOption = config.EdgeScanFeedRunnerOption;
                LoadWithStockTiedLeg = config.LoadWithStockTiedLeg;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadModuleConfig));
            }
        }

        public override void OnSetDispatcher()
        {
            Dispatcher.BeginInvoke(() =>
            {
                AutoTraderBasketView = _moduleFactory.CreateWindow(Module.BasketTrader, null, false) as BasketTraderView;
                if (AutoTraderBasketView != null)
                {
                    AutoTraderBasketViewModel = (BasketTraderViewModel)AutoTraderBasketView.DataContext;
                    AutoTraderBasketViewModel.SetDispatcher(Dispatcher);

                    AutoTraderBasketView.Closing += AutoTraderBasketView_Closing;
                    AutoTraderBasketViewModel.BorderBrush = new SolidColorBrush(BorderBrushColor);

                    if (AutoTraderBasketViewModel.IsReady)
                    {
                        AutoTraderBasketReadyEvent(AutoTraderBasketViewModel);
                    }
                    else
                    {
                        AutoTraderBasketViewModel.Ready += AutoTraderBasketReadyEvent;
                    }

                    AutoTraderBasketView.Show();
                }
            });
        }

        private void AutoTraderBasketReadyEvent(IModuleViewModel module)
        {
            AutoTraderBasketViewModel.Ready -= AutoTraderBasketReadyEvent;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_moduleConfig is not { BasketOpen: true })
                {
                    AutoTraderBasketView.Visibility = Visibility.Hidden;
                    AutoTraderBasketView.Hide();
                }

                if (!string.IsNullOrWhiteSpace(_moduleConfig?.AutoTraderConfig))
                {
                    AutoTraderBasketView.LoadConfigFromJsonAsync(_moduleConfig.AutoTraderConfig);
                }

                AutoTraderBasketViewModel.IsEdgeScanFeedAutoTrader = true;
                AutoTraderBasketViewModel.ShowBasketDeltaAdjLastFillPx = true;
                AutoTraderBasketViewModel.RemoveAllOnInterval = true;
                AutoTraderBasketViewModel.ModuleType = OrderSubType.EdgeScanFeed;
                AutoTraderBasketViewModel.BasketSettings.Uid = "ES" + Guid.NewGuid().ToString().Split('-')[0];
                AutoTraderBasketViewModel.ModuleTitle = "Edge Scan Feed - Auto Trader";
            }));
        }

        private void AutoTraderBasketView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!IsDisposed)
            {
                AutoTraderBasketView.Hide();
                e.Cancel = true;
            }
        }
    }
}
