using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.Cob.Client.Config.Interfaces;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Databento.Client.Config.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;
using ZeroPlus.Ema.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.HubTron.Client.Config.Interfaces;
using ZeroPlus.IbGateway.Client.Config.Interfaces;
using ZeroPlus.Interpolator.Client.Config.Interfaces;
using ZeroPlus.LiveVol.Client.Config.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Api;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.Pricing.Client.Config.Interfaces;
using ZeroPlus.Raptor.Client.Config;
using ZeroPlus.Raptor.Client.Config.Interfaces;
using ZeroPlus.SymbolMap.Client.Config.Interfaces;
using ZeroPlus.Telemetry.Client.Config.Interfaces;
using ZeroPlus.Theos.Client.Config.Interfaces;
using ZeroPlus.Trades.Client.Config.Interfaces;
using GreekSource = ZeroPlus.Oms.Enums.GreekSource;
using Strategy = ZeroPlus.Models.Generators.SpreadGenerators.Strategy;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SettingsViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public OmsConfig _Config;
        public SecureString _CurrentPassword;
        public SecureString _NewPassword;
        public SecureString _ConfirmPassword;
        public string _Message;
        public ObservableCollection<SmartRouteModel> _SmartRoutes;
        public ObservableCollection<FishRouteModel> _FishRoutes;
        public ObservableCollection<QuickRouteModel> _QuickRoutes;
        public ObservableCollection<SymbolsLookupModel> _SymbolsLookup;
        public ObservableCollection<ContraEdgeLookupModel> _ContraEdgeLookup;
        public ObservableCollection<QuickRouteModel> _BlockTraderRoutes;
        public ObservableCollection<CancelTimerModel> _CancelTimerLookup;
        public ObservableCollection<TicketStopLossModel> _TicketStopLossLookup;
        public ObservableCollection<HedgeLookupModel> _BasketHedgeLookup;
        public ObservableCollection<BasketMarketMakerOffsetLookupModel> _BasketMarketMakerOffsetLookup;
        public ObservableCollection<DerivedValueConfigModel> _DerivedValueConfigModelLookup;
        public ObservableCollection<LockTraderPriceLimitModel> _LockTraderPriceLimits;
        public ObservableCollection<AccountConfigModel> _AccountConfigs;
        public ObservableCollection<BasketLayoutQuickAccessModel> _BasketLayoutQuickAccess;
        public ObservableCollection<BasketDefaultLayoutModel> _BasketDefaultLayouts;
        public AccountConfigModel _AccountConfig;
        public ObservableCollection<ConfigSave> _BasketLayouts;
        public ObservableCollection<BasketAutoPermModel> _BasketAutoPermSettings;

        public ITradesClientConfig TradesClientConfig { get; }
        public ITradesClientConfigParser TradesClientConfigParser { get; }
        public IHerculesClientConfig HerculesClientConfig { get; }
        public IHerculesClientConfigParser HerculesClientConfigParser { get; }
        public IRaptorClientConfig RaptorClientConfig { get; }
        public IRaptorClientConfigParser RaptorClientConfigParser { get; }
        public IEdgeScannerClientConfig EdgeScannerClientConfig { get; }
        public IEdgeScannerClientConfigParser EdgeScannerClientConfigParser { get; }
        public IEdgeScanFeedRunnerClientConfig EdgeScanFeedRunnerClientConfig { get; }
        public IEdgeScanFeedRunnerClientConfigParser EdgeScanFeedRunnerClientConfigParser { get; }
        public ISymbolMapClientConfig SymbolMapClientConfig { get; }
        public ISymbolMapClientConfigParser SymbolMapClientConfigParser { get; }
        public ITelemetryClientConfig TelemetryClientConfig { get; }
        public ITelemetryClientConfigParser TelemetryClientConfigParser { get; }
        public byte AutoDetectedBoxId => Oms.Helpers.TelemetryHelper.GetBoxId();
        public byte AutoDetectedInstanceId => Oms.Helpers.TelemetryHelper.GetInstanceId();
        public IEmaClientConfig EmaClientConfig { get; }
        public IEmaClientConfigParser EmaClientConfigParser { get; }
        public EMAServer.Client.IConfig EmaServerConfig { get; }
        public EmaServerConfig.EmaServerConfigParser EmaServerConfigParser { get; }
        public IInterpolatorClientConfig InterpolatorClientConfig { get; }
        public IInterpolatorClientConfigParser InterpolatorClientConfigParser { get; }
        public ITheosClientConfig TheosClientConfig { get; }
        public ITheosClientConfigParser TheosClientConfigParser { get; }
        public IHubTronClientConfig HubTronClientConfig { get; }
        public IHubTronClientConfigParser HubTronClientConfigParser { get; }
        public IIbGatewayClientConfig IbGatewayClientConfig { get; }
        public IIbGatewayClientConfigParser IbGatewayClientConfigParser { get; }
        public IDatabentoClientConfig DatabentoClientConfig { get; }
        public IDatabentoClientConfigParser DatabentoClientConfigParser { get; }
        public ICobClientConfig CobClientConfig { get; }
        public ICobClientConfigParser CobClientConfigParser { get; }
        public IPricingClientConfig PricingClientConfig { get; }
        public IPricingClientConfigParser PricingClientConfigParser { get; }
        public IAutoTraderClientConfig OrderGatewayClientConfig { get; }
        public IAutoTraderClientConfigParser OrderGatewayClientConfigParser { get; }
        public AutoTraderDirectClientConfig AutoTraderDirectClientConfig { get; }
        public AutoTraderDirectClientConfigParser AutoTraderDirectClientConfigParser { get; }
        public ILiveVolClientConfig LiveVolClientConfig { get; }
        public ILiveVolClientConfigParser LiveVolClientConfigParser { get; }

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public RestApi RestApi { get; }
        public NotificationManager NotificationManager { get; }
        public Dispatcher Dispatcher { get; set; }
        public IEnumerable<string> LogLevels { get; } = LogLevel.AllLevels.Select(x => x.Name).ToList();
        public IEnumerable<string> Strategies { get; } = Enum.GetNames<BaseStrategy>().OrderBy(x => x).ToList();
        public IEnumerable<BaseStrategy> LockBaseStrategies { get; } = Enum.GetValues<BaseStrategy>().ToList();
        public IEnumerable<string> OptionModelNames { get; } = Enum.GetNames(typeof(OptionModels)).ToList();
        public IEnumerable<SubscriptionFieldType> SubscriptionFields { get; } = new List<SubscriptionFieldType>() { SubscriptionFieldType.DeltaAdjTheo };
        public IEnumerable<QuoteSource> QuoteSources { get; } = Enum.GetValues<QuoteSource>().ToList();
        public IEnumerable<LegTypes> LegTypes { get; } = Enum.GetValues<LegTypes>().ToList();
        public IEnumerable<Strategy> BaseStrategies { get; } = Enum.GetValues<Strategy>().ToList();
        public IEnumerable<GreekSource> GreekSources { get; } = Enum.GetValues<GreekSource>().ToList();
        public IEnumerable<string> OrderbookSubscriptions { get; } = ((TransactionSubscriptionMode[])Enum.GetValues(typeof(TransactionSubscriptionMode))).Select(x => CamelCaseFormatter.CamelCaseConverter().Replace(x.ToString(), " $1"));
        public IEnumerable<string> TifsList { get; } = new List<string>
        {
            "DAY",
            "GTC",
            "ETH",
            "IOC",
            "FOK",
            "GTX",
            "OPG",
        };

        [Bindable]
        public partial OmsConfig Config { get; set; }
        [Bindable]
        public partial SecureString CurrentPassword { get; set; }
        [Bindable]
        public partial SecureString NewPassword { get; set; }
        [Bindable]
        public partial SecureString ConfirmPassword { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial ObservableCollection<SmartRouteModel> SmartRoutes { get; set; }
        [Bindable]
        public partial ObservableCollection<FishRouteModel> FishRoutes { get; set; }
        [Bindable]
        public partial ObservableCollection<QuickRouteModel> QuickRoutes { get; set; }
        [Bindable]
        public partial ObservableCollection<SymbolsLookupModel> SymbolsLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<ContraEdgeLookupModel> ContraEdgeLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<QuickRouteModel> BlockTraderRoutes { get; set; }
        [Bindable]
        public partial ObservableCollection<CancelTimerModel> CancelTimerLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<TicketStopLossModel> TicketStopLossLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<HedgeLookupModel> BasketHedgeLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<BasketMarketMakerOffsetLookupModel> BasketMarketMakerOffsetLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<DerivedValueConfigModel> DerivedValueConfigModelLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<LockTraderPriceLimitModel> LockTraderPriceLimits { get; set; }
        [Bindable]
        public partial ObservableCollection<AccountConfigModel> AccountConfigs { get; set; }
        [Bindable]
        public partial ObservableCollection<BasketLayoutQuickAccessModel> BasketLayoutQuickAccess { get; set; }
        [Bindable]
        public partial ObservableCollection<BasketDefaultLayoutModel> BasketDefaultLayouts { get; set; }
        [Bindable]
        public partial AccountConfigModel AccountConfig { get; set; }
        [Bindable]
        public partial ObservableCollection<ConfigSave> BasketLayouts { get; set; }
        [Bindable]
        public partial ObservableCollection<BasketAutoPermModel> BasketAutoPermSettings { get; set; }
        [Bindable]
        public partial FishLossConfigViewModel BasketFishLossConfig { get; set; }
        [Bindable]
        public partial FishLossConfigViewModel EdgeScanFishLossConfig { get; set; }
        [Bindable]
        public partial string TransactionSubscriptionMode { get; set; }
        [Bindable]
        public partial ObservableCollection<RaptorClientConfig> RaptorClientConfigs { get; set; }

        public StartupHelpers.BootstrapConfig BootstrapConfig { get; set; }

        public SettingsViewModel(IHerculesClientConfig herculesClientConfig,
                                 IHerculesClientConfigParser herculesClientConfigParser,
                                 IRaptorClientConfigParser raptorClientConfigParser,
                                 IEdgeScannerClientConfig edgeScannerClientConfig,
                                 IEdgeScannerClientConfigParser edgeScannerClientConfigParser,
                                 IEdgeScanFeedRunnerClientConfig edgeScanFeedRunnerClientConfig,
                                 IEdgeScanFeedRunnerClientConfigParser edgeScanFeedRunnerClientConfigParser,
                                 ISymbolMapClientConfig symbolMapClientConfig,
                                 ISymbolMapClientConfigParser symbolMapClientConfigParser,
                                 ITelemetryClientConfig telemetryClientConfig,
                                 ITelemetryClientConfigParser telemetryClientConfigParser,
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
                                 ILiveVolClientConfig liveVolClientConfig,
                                 ILiveVolClientConfigParser liveVolClientConfigParser,
                                 ITradesClientConfig tradesClientConfig,
                                 ITradesClientConfigParser tradesClientConfigParser,
                                 UpdateManager updateManager,
                                 RestApi restApi,
                                 NotificationManager notificationManager)
        {
            NotificationManager = notificationManager;
            RestApi = restApi;
            Config = OmsCore.Config;
            TradesClientConfig = tradesClientConfig;
            TradesClientConfigParser = tradesClientConfigParser;
            HerculesClientConfig = herculesClientConfig;
            HerculesClientConfigParser = herculesClientConfigParser;
            RaptorClientConfig = updateManager.RaptorClientConfigs.FirstOrDefault();
            RaptorClientConfigs = Config.RaptorClientConfigs.ToObservableCollection();
            RaptorClientConfigParser = raptorClientConfigParser;
            EdgeScannerClientConfig = edgeScannerClientConfig;
            EdgeScannerClientConfigParser = edgeScannerClientConfigParser;
            EdgeScanFeedRunnerClientConfig = edgeScanFeedRunnerClientConfig;
            EdgeScanFeedRunnerClientConfigParser = edgeScanFeedRunnerClientConfigParser;
            SymbolMapClientConfig = symbolMapClientConfig;
            SymbolMapClientConfigParser = symbolMapClientConfigParser;
            TelemetryClientConfig = telemetryClientConfig;
            TelemetryClientConfigParser = telemetryClientConfigParser;
            EmaClientConfig = emaClientConfig;
            EmaClientConfigParser = emaClientConfigParser;
            EmaServerConfig = emaServerConfig;
            EmaServerConfigParser = emaServerConfigParser;
            InterpolatorClientConfig = interpolatorClientConfig;
            InterpolatorClientConfigParser = interpolatorClientConfigParser;
            TheosClientConfig = theosClientConfig;
            TheosClientConfigParser = theosClientConfigParser;
            HubTronClientConfig = hubTronClientConfig;
            HubTronClientConfigParser = hubTronClientConfigParser;
            IbGatewayClientConfig = ibGatewayClientConfig;
            IbGatewayClientConfigParser = ibGatewayClientConfigParser;
            DatabentoClientConfig = databentoClientConfig;
            DatabentoClientConfigParser = databentoClientConfigParser;
            CobClientConfig = cobClientConfig;
            CobClientConfigParser = cobClientConfigParser;
            PricingClientConfig = pricingClientConfig;
            PricingClientConfigParser = pricingClientConfigParser;
            OrderGatewayClientConfig = orderGatewayClientConfig;
            OrderGatewayClientConfigParser = orderGatewayClientConfigParser;
            AutoTraderDirectClientConfig = autoTraderDirectClientConfig;
            AutoTraderDirectClientConfigParser = autoTraderDirectClientConfigParser;
            LiveVolClientConfig = liveVolClientConfig;
            LiveVolClientConfigParser = liveVolClientConfigParser;
            SymbolsLookup = new();
            ContraEdgeLookup = new();
            CancelTimerLookup = new();
            TicketStopLossLookup = new();
            BasketHedgeLookup = new();
            BasketMarketMakerOffsetLookup = new();
            DerivedValueConfigModelLookup = new();
            LockTraderPriceLimits = new();
            SmartRoutes = new();
            QuickRoutes = new();
            FishRoutes = new();
            BlockTraderRoutes = new();
            BasketLayouts = new();
            BasketLayoutQuickAccess = new();
            BasketDefaultLayouts = new();
            Config.ConfigMessageEvent += ShowMessage;
            Config.ConfigChangedEvent += OnConfigChangedEvent;

            AccountConfigs = new ObservableCollection<AccountConfigModel>();
            foreach (AccountConfigModel config in OmsCore.Config.AccountConfigs)
            {
                AccountConfigs.Add(config);
            }
            AccountConfig = OmsCore.Config.AccountConfig;

            TransactionSubscriptionMode = CamelCaseFormatter.CamelCaseConverter().Replace(HerculesClientConfig.TransactionSubscriptionMode.ToString(), "$1");

            foreach (Tuple<string, string, double, bool> symbolLookup in Config.SymbolsLookup.Values)
            {
                SymbolsLookup.Add(new SymbolsLookupModel(symbolLookup.Item1, symbolLookup.Item2, symbolLookup.Item3, symbolLookup.Item4));
            }

            foreach (var kvp in Config.ContraEdgeLookup)
            {
                ContraEdgeLookup.Add(new ContraEdgeLookupModel(kvp.Key, kvp.Value));
            }

            foreach (FishRoute fishRoute in Config.FishRoutes)
            {
                FishRoutes.Add(new FishRouteModel(fishRoute.Routes, fishRoute.Edge, fishRoute.Increment, fishRoute.Interval));
            }

            foreach (KeyValuePair<string, Dictionary<int, Tuple<string, double>>> smartRoute in Config.SmartRoutes)
            {
                SmartRoutes.Add(new SmartRouteModel(smartRoute.Key, smartRoute.Value));
            }

            foreach (Tuple<string, string> quickRoute in Config.QuickRoutes)
            {
                QuickRoutes.Add(new QuickRouteModel(quickRoute.Item1, quickRoute.Item2));
            }

            foreach (Tuple<string, string> blockTraderRoute in Config.BlockTraderRoutes)
            {
                BlockTraderRoutes.Add(new QuickRouteModel(blockTraderRoute.Item1, blockTraderRoute.Item2));
            }

            foreach (Tuple<string, string, double, double> cancelTimer in Config.CancelTimerLookup)
            {
                CancelTimerLookup.Add(new CancelTimerModel(cancelTimer.Item1, cancelTimer.Item2, cancelTimer.Item3, cancelTimer.Item4));
            }

            foreach (Tuple<string, double, double, int> TicketStopLoss in Config.TicketStopLossLookup)
            {
                TicketStopLossLookup.Add(new TicketStopLossModel(TicketStopLoss.Item1, TicketStopLoss.Item2, TicketStopLoss.Item3, TicketStopLoss.Item4));
            }

            foreach (Tuple<string, string, double> lookup in Config.BasketHedgeLookup)
            {
                BasketHedgeLookup.Add(new HedgeLookupModel(lookup.Item1, lookup.Item2, lookup.Item3));
            }

            foreach (KeyValuePair<string, BasketMarketMakerOffsetLookupModel> lookup in Config.BasketMarketMakerOffsetLookup)
            {
                BasketMarketMakerOffsetLookup.Add(lookup.Value);
            }

            foreach (KeyValuePair<string, DerivedValueConfigModel> lookup in Config.DerivedValueConfigModelLookup)
            {
                DerivedValueConfigModelLookup.Add(lookup.Value);
            }

            foreach (var lookup in Config.LockTraderPriceLimits.Values)
            {
                LockTraderPriceLimits.Add(lookup);
            }

            BasketFishLossConfig = new FishLossConfigViewModel(Config.BasketFishLossConfig);
            EdgeScanFishLossConfig = new FishLossConfigViewModel(Config.EdgeScanFishLossConfig);

            FishRoutes.CollectionChanged += (s, e) => SaveFishRoutes();
            SmartRoutes.CollectionChanged += (s, e) => SaveSmartRoutes();
            QuickRoutes.CollectionChanged += (s, e) => SaveQuickRoutes();
            BlockTraderRoutes.CollectionChanged += (s, e) => SaveBlockTraderRoutes();
            BootstrapConfig = StartupHelpers.BootstrapConfig.LoadUIBoostrapConfig();
        }

        private void LoadBasketAutoPermModels()
        {
            try
            {
                BasketAutoPermSettings = new ObservableCollection<BasketAutoPermModel>();
                string path = Path.Combine(Config.GetWorkspaceDirectory(), "BasketAutoPermModels.json");
                if (File.Exists(path))
                {
                    Task.Run(() =>
                    {
                        string content = File.ReadAllText(path);
                        List<BasketAutoPermModel> models = JsonConvert.DeserializeObject<List<BasketAutoPermModel>>(content);
                        if (models != null)
                        {
                            Dispatcher?.BeginInvoke(() =>
                            {
                                foreach (BasketAutoPermModel model in models)
                                {
                                    BasketAutoPermSettings.Add(model);
                                }
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadBasketAutoPermModels));
            }
        }

        private void SaveBasketAutoPermModels()
        {
            try
            {
                List<BasketAutoPermModel> autoPermSettings = BasketAutoPermSettings.ToList();
                OrderBookViewModel.AutoPermSettings = autoPermSettings;
                string content = JsonConvert.SerializeObject(autoPermSettings);
                if (content != null)
                {
                    string path = Path.Combine(Config.GetWorkspaceDirectory(), "BasketAutoPermModels.json");
                    File.WriteAllText(path, content);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveBasketAutoPermModels));
            }
        }

        public async void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            LoadBasketAutoPermModels();
            await RefreshBasketLayoutsCommand();
            LoadSavedBasketLayoutConfigsCommand();
            LoadSavedBasketDefaultLayoutModelsCommand();
        }

        internal void SaveSettings()
        {
            SaveBasketAutoPermModels();
            Config.BasketFishLossConfig = BasketFishLossConfig.GetConfig();
            Config.EdgeScanFishLossConfig = EdgeScanFishLossConfig.GetConfig();
            SaveRaptorClientConfigs();
        }

        [Command]
        public void QuoteSourcesChangedCommand()
        {
            try
            {
                OmsCore.QuoteClient.Resubscribe();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(QuoteSourcesChangedCommand));
            }
        }

        [Command]
        public void AddBasketAutoPermSettingCommand()
        {
            BasketAutoPermModel model = new();
            BasketAutoPermSettings.Add(model);
            OpenBasketAutoPermSettingCommand(model);
        }

        [Command]
        public void OpenBasketAutoPermSettingCommand(BasketAutoPermModel model)
        {
            AutoPermConfigView view = new();
            AutoPermConfigViewModel viewModel = view.DataContext as AutoPermConfigViewModel;
            viewModel.Model = model;
            viewModel.AutoPermConfigs = model.AutoPermConfigs.ToObservableCollection();
            viewModel.PermOperationModels = Config.CustomPermCombinations.Where(x => x.Key != null && x.Value != null && x.Value.Count > 0).Select(x => new PermOperationModel(x.Key, x.Value)).ToObservableCollection();
            view.Show();
        }

        [Command]
        public void RemoveBasketAutoPermSettingCommand(BasketAutoPermModel model)
        {
            BasketAutoPermSettings.Remove(model);
        }

        #region Routes

        [Command]
        public void AddNewBasketLayoutQuickAccessCommand()
        {
            BasketLayoutQuickAccess.Add(new BasketLayoutQuickAccessModel()
            {
                Index = BasketLayoutQuickAccess.Count,
            });
        }

        [Command]
        public async Task RefreshBasketLayoutsCommand()
        {
            Task<List<ConfigSave>> layoutTask = OmsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTraderLayout);
            Task<List<ConfigSave>> configTask = OmsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTrader);
            await Task.WhenAll(new Task[] { layoutTask, configTask }).ContinueWith(async t =>
            {
                if (layoutTask.Result != null || configTask.Result != null)
                {
                    List<ConfigSave> basketLayouts = new();
                    if (layoutTask.Result != null)
                    {
                        foreach (ConfigSave config in layoutTask.Result)
                        {
                            basketLayouts.Add(config);
                        }
                    }
                    if (configTask.Result != null)
                    {
                        foreach (ConfigSave config in configTask.Result)
                        {
                            basketLayouts.Add(config);
                        }
                    }
                    await Dispatcher.BeginInvoke(() =>
                    {
                        BasketLayouts.Clear();
                        foreach (ConfigSave item in basketLayouts.OrderBy(x => x.FullTitle))
                        {
                            BasketLayouts.Add(item);
                        }
                    });
                }
            });
        }

        [Command]
        public void LoadSavedBasketLayoutConfigsCommand()
        {
            List<Tuple<int, string, ConfigSave>> savedBasketQuickAccessLayouts = Config.SavedBasketQuickAccessLayouts;
            if (savedBasketQuickAccessLayouts != null)
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (Tuple<int, string, ConfigSave> layoutSave in savedBasketQuickAccessLayouts.OrderBy(x => x.Item1))
                    {
                        ConfigSave config = BasketLayouts.FirstOrDefault(x => x.Id == layoutSave.Item3.Id);
                        config ??= layoutSave.Item3;
                        BasketLayoutQuickAccessModel model = new()
                        {
                            Index = layoutSave.Item1,
                            Title = layoutSave.Item2,
                            Layout = config,
                        };

                        if (model.IsValid())
                        {
                            BasketLayoutQuickAccess.Add(model);
                        }
                    }
                });
            }
        }

        [Command]
        public void BasketLayoutQuickAccessCellValueChangedCommand(CellValueChangedArgs args)
        {
            try
            {
                SaveLayoutQuickAccessCommand();
            }
            catch (Exception) { }
        }

        [Command]
        public void ValidateNewBasketLayoutQuickAccessCommand(RowValidationArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                BasketLayoutQuickAccessModel model = (BasketLayoutQuickAccessModel)args.Item;
                if (model != null)
                {
                    if (string.IsNullOrWhiteSpace(model.Title) && model.Layout == null)
                    {
                        if (BasketLayoutQuickAccess.Contains(model))
                        {
                            BasketLayoutQuickAccess.Remove(model);
                        }
                    }
                    else
                    {
                        SaveLayoutQuickAccessCommand();
                    }
                }
            }
            catch (Exception) { }
        }

        [Command]
        public void RemoveBasketLayoutQuickAccessCommand(BasketLayoutQuickAccessModel model)
        {
            try
            {
                if (model == null)
                {
                    return;
                }

                var ok = MessageBoxService?.Show($"Are you sure you want to remove {model.Title}?",
                    "Verification",
                    MessageButton.YesNo,
                    MessageIcon.Exclamation,
                    MessageResult.No) == MessageResult.Yes;

                if (ok)
                {

                    if (BasketLayoutQuickAccess.Contains(model))
                    {
                        BasketLayoutQuickAccess.Remove(model);
                    }
                    SaveLayoutQuickAccessCommand();
                }
            }
            catch (Exception) { }
        }

        [Command]
        public void SaveLayoutQuickAccessCommand()
        {
            try
            {
                Config.SaveBasketQuickAccessLayouts(BasketLayoutQuickAccess.Where(x => x.IsValid()).Select(x => x.Export).ToList());
                StartupWindowViewModel.CacheBasketConfigs();
            }
            catch (Exception) { }
        }

        [Command]
        public void AddNewBasketDefaultLayoutCommand()
        {
            BasketDefaultLayouts.Add(new BasketDefaultLayoutModel()
            {
                Index = BasketDefaultLayouts.Count,
            });
        }

        [Command]
        public void LoadSavedBasketDefaultLayoutModelsCommand()
        {
            List<Tuple<int, string, LegTypes, Strategy, ConfigSave>> layouts = Config.SavedBasketDefaultLayouts;
            if (layouts != null)
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (Tuple<int, string, LegTypes, Strategy, ConfigSave> layoutSave in layouts.OrderBy(x => x.Item1))
                    {
                        ConfigSave config = BasketLayouts.FirstOrDefault(x => x.Id == layoutSave.Item5.Id);
                        config ??= layoutSave.Item5;
                        BasketDefaultLayoutModel model = new()
                        {
                            Index = layoutSave.Item1,
                            Underlying = layoutSave.Item2,
                            LegType = layoutSave.Item3,
                            BaseStrategy = layoutSave.Item4,
                            Layout = config,
                        };

                        if (model.IsValid())
                        {
                            BasketDefaultLayouts.Add(model);
                        }
                    }
                });
            }
        }

        [Command]
        public void BasketDefaultLayoutsCellValueChangedCommand(CellValueChangedArgs args)
        {
            try
            {
                SaveBasketDefaultLayoutsCommand();
            }
            catch (Exception) { }
        }

        [Command]
        public void ValidateNewBasketDefaultLayoutCommand(RowValidationArgs args)
        {
            try
            {
                if (args == null)
                {
                    return;
                }

                BasketDefaultLayoutModel model = (BasketDefaultLayoutModel)args.Item;
                if (model != null)
                {
                    if (model.Layout == null)
                    {
                        if (BasketDefaultLayouts.Contains(model))
                        {
                            BasketDefaultLayouts.Remove(model);
                        }
                    }
                    else
                    {
                        SaveBasketDefaultLayoutsCommand();
                    }
                }
            }
            catch (Exception) { }
        }

        [Command]
        public void RemoveBasketDefaultLayoutCommand(BasketDefaultLayoutModel model)
        {
            try
            {
                if (model == null)
                {
                    return;
                }

                bool ok;
                ok = MessageBoxService?.Show($"Are you sure you want to remove {model.Underlying}?",
                                             "Verification",
                                             MessageButton.YesNo,
                                             MessageIcon.Exclamation,
                                             MessageResult.No) == MessageResult.Yes;

                if (ok)
                {

                    if (BasketDefaultLayouts.Contains(model))
                    {
                        BasketDefaultLayouts.Remove(model);
                    }
                    SaveBasketDefaultLayoutsCommand();
                }
            }
            catch (Exception) { }
        }

        [Command]
        public void SaveBasketDefaultLayoutsCommand()
        {
            try
            {
                Config.SaveBasketDefaultLayouts(BasketDefaultLayouts.Where(x => x.IsValid()).Select(x => x.Export).ToList());
                StartupWindowViewModel.CacheBasketConfigs();
            }
            catch (Exception) { }
        }

        [Command]
        public void ResetAddresses()
        {
            Config.ResetAddresses(forced: false, OmsCore?.User?.Username);
        }

        [Command]
        public void ForceResetAddresses()
        {
            Config.ResetAddresses(forced: true, OmsCore?.User?.Username);
        }

        [Command]
        public void SwitchToBackup()
        {
            Config.SwitchToBackup();
        }

        [Command]
        public void RaptorConfigChanged()
        {
            try
            {
                string status = RaptorClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), RaptorClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RaptorClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void EdgeScannerConfigChanged()
        {
            try
            {
                string status = EdgeScannerClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), EdgeScannerClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeScannerClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void EdgeScanFeedRunnerConfigChanged()
        {
            try
            {
                string status = EdgeScanFeedRunnerClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), EdgeScanFeedRunnerClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EdgeScanFeedRunnerClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void SymbolMapConfigChanged()
        {
            try
            {
                string status = SymbolMapClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), SymbolMapClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SymbolMapClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void TelemetryConfigChanged()
        {
            try
            {
                string status = TelemetryClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), TelemetryClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TelemetryClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void EmaConfigChanged()
        {
            try
            {
                string status = EmaClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), EmaClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmaClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void EmaServerConfigChanged()
        {
            try
            {
                string status = EmaServerConfigParser.SaveConfig(EmaServerConfig, OmsConfig.GetConfigDirectory());
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmaServerConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void InterpolatorConfigChanged()
        {
            try
            {
                string status = InterpolatorClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), InterpolatorClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(InterpolatorClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void TheosConfigChanged()
        {
            try
            {
                string status = TheosClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), TheosClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TheosClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void HubTronConfigChanged()
        {
            try
            {
                string status = HubTronClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), HubTronClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HubTronConfigChanged));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void IbGatewayConfigChanged()
        {
            try
            {
                string status = IbGatewayClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), IbGatewayClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(IbGatewayClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void DatabentoConfigChanged()
        {
            try
            {
                string status = DatabentoClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), DatabentoClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DatabentoClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void CobConfigChanged()
        {
            try
            {
                string status = CobClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), CobClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CobClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void PricingConfigChanged()
        {
            try
            {
                string status = PricingClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), PricingClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PricingClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void OrderGatewayConfigChanged()
        {
            try
            {
                string status = OrderGatewayClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), OrderGatewayClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OrderGatewayClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void AutoTraderDirectConfigChanged()
        {
            try
            {
                string status = AutoTraderDirectClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), AutoTraderDirectClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AutoTraderDirectClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void HerculesConfigChanged()
        {
            try
            {
                var mode = string.Join("", TransactionSubscriptionMode.Split(" "));
                if (Enum.TryParse(typeof(TransactionSubscriptionMode), mode, out var transactionSubscriptionMode))
                {
                    HerculesClientConfig.TransactionSubscriptionMode = (TransactionSubscriptionMode)transactionSubscriptionMode;
                }
                string status = HerculesClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), HerculesClientConfig);
                ShowMessage(status);

                if (Config.TransactionClientEnabled && OmsCore.HerculesClient.IsConnected)
                {
                    Task.Run(() => OmsCore.HerculesClient.Reconnect());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HerculesClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void TradesConfigChanged()
        {
            try
            {
                string status = TradesClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), TradesClientConfig);
                ShowMessage(status);

                if (Config.RequestClientEnabled && OmsCore.TradesClient.IsConnected)
                {
                    Task.Run(OmsCore.TradesClient.RestartAsync);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TradesClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void LiveVolConfigChanged()
        {
            try
            {
                string status = LiveVolClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), LiveVolClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LiveVolClientConfig));
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void OpenPermComboEditorCommand()
        {
            PermComboEditorView view = new();
            view.Show();
        }

        [Command]
        public void AccountChangedCommand()
        {
            Config.SaveAccountConfigs();
        }

        [Command]
        public void SmartRouteCellValueChanged(CellValueChangedArgs args)
        {
            SaveSmartRoutes();
        }

        [Command]
        public void ValidateNewSmartRoute(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            SmartRouteModel SmartRoute = (SmartRouteModel)args.Item;
            if (SmartRoute != null)
            {
                if (string.IsNullOrWhiteSpace(SmartRoute.Routes))
                {
                    RemoveSmartRoute(SmartRoute);
                }
                else
                {
                    SaveSmartRoutes();
                }
            }
        }

        [Command]
        public void RemoveSmartRoute(SmartRouteModel smartRoute)
        {
            if (smartRoute == null)
            {
                return;
            }

            bool ok;
            ok = MessageBoxService?.Show($"Are you sure you want to remove {smartRoute.Name}?",
                                         "Verification",
                                         MessageButton.YesNo,
                                         MessageIcon.Exclamation,
                                         MessageResult.No) == MessageResult.Yes;

            if (ok)
            {

                if (SmartRoutes.Contains(smartRoute))
                {
                    SmartRoutes.Remove(smartRoute);
                }
                SaveSmartRoutes();
            }
        }

        [Command]
        public void ShareAllSmartRoutes()
        {
            try
            {
                List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export = OmsCore.Config.SmartRoutes.Select(x => Tuple.Create(x.Key, x.Value)).ToList();

                ShareSmartRoutes(export);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareAllSmartRoutes));
            }
        }

        [Command]
        public void ShareSmartRoute(SmartRouteModel smartRoute)
        {
            try
            {
                if (smartRoute == null)
                {
                    return;
                }

                List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export = OmsCore.Config.SmartRoutes.Where(x => x.Key == smartRoute.Name).Select(x => Tuple.Create(x.Key, x.Value)).ToList();
                ShareSmartRoutes(export);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareSmartRoute));
            }
        }

        [Command]
        public void EditSmartRouteCommand(SmartRouteModel smartRoute)
        {
            try
            {
                if (smartRoute == null)
                {
                    return;
                }
                SmartRouteEditorView view = new();
                if (view.DataContext is SmartRouteEditorViewModel viewModel)
                {
                    viewModel.Route = smartRoute;
                    viewModel.SaveAction = SaveSmartRoutes;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditSmartRouteCommand));
            }
        }

        private void ShareSmartRoutes(List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export)
        {
            ShareWithView view = new();
            ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

            viewModel.Module = Module.SmartRoutes;

            viewModel.Message = string.Join(", ", export.Select(x => x.Item1));
            viewModel.Config = JsonConvert.SerializeObject(export);

            view.ShowDialog();
        }

        [Command]
        public void AddNewSmartRoute()
        {
            SmartRoutes.Insert(0, new SmartRouteModel("", new()));
        }

        public void SaveSmartRoutes()
        {
            Config.SmartRoutes = SmartRoutes.GroupBy(x => x.Name)
                                            .Select(g => g.First())
                                            .ToDictionary(x => x.Name, x => x.IndexToRouteMap);
            Config.SaveSmartRoutes();
        }

        [Command]
        public void FishRouteCellValueChanged(CellValueChangedArgs args)
        {
            SaveFishRoutes();
        }

        [Command]
        public void ValidateNewFishRoute(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            FishRouteModel FishRoute = (FishRouteModel)args.Item;
            if (FishRoute != null)
            {
                if (string.IsNullOrWhiteSpace(FishRoute.Routes))
                {
                    RemoveFishRoute(FishRoute);
                }
                else
                {
                    SaveFishRoutes();
                }
            }
        }

        [Command]
        public void RemoveFishRoute(FishRouteModel fishRoute)
        {
            if (fishRoute == null)
            {
                return;
            }

            bool ok = MessageBoxService?.Show($"Are you sure you want to remove {fishRoute.Routes}?",
                                         "Verification",
                                         MessageButton.YesNo,
                                         MessageIcon.Exclamation,
                                         MessageResult.No) == MessageResult.Yes;

            if (ok)
            {

                if (FishRoutes.Contains(fishRoute))
                {
                    FishRoutes.Remove(fishRoute);
                }
                SaveFishRoutes();
            }
        }

        [Command]
        public void ShareAllFishRoutes()
        {
            try
            {
                List<FishRoute> export = OmsCore.Config.FishRoutes;

                ShareFishRoutes(export);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareAllFishRoutes));
            }
        }

        [Command]
        public void ShareFishRoute(FishRouteModel fishRoute)
        {
            try
            {
                if (fishRoute == null)
                {
                    return;
                }
                List<FishRoute> export = new()
                {
                    new FishRoute()
                    {
                        Edge = fishRoute.Edge,
                        Increment = fishRoute.Increment,
                        Interval = fishRoute.Interval,
                        RoutesList = fishRoute.RoutesList,
                    }
                };
                ShareFishRoutes(export);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareFishRoute));
            }
        }

        [Command]
        public void ViewExecutingBrokerFeeModelCommand(ExecutingBrokerFeeModel model)
        {
            try
            {
                ExecutingBrokerFeeModelView view = new();
                if (view.DataContext is ExecutingBrokerFeeModelViewModel viewModel)
                {
                    viewModel.Model = model;
                }
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ViewExecutingBrokerFeeModelCommand));
            }
        }

        private static void ShareFishRoutes(List<FishRoute> export)
        {
            ShareWithView view = new();
            ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

            viewModel.Module = Module.FishRoutes;

            viewModel.Message = string.Join(", ", export.Select(x => x.Routes));
            viewModel.Config = JsonConvert.SerializeObject(export);

            view.ShowDialog();
        }

        [Command]
        public void AddNewFishRoute()
        {
            FishRoutes.Insert(0, new FishRouteModel("", 0.10, 0.01, 250));
        }

        public void SaveFishRoutes()
        {
            Config.FishRoutes = FishRoutes.GroupBy(x => x.Routes)
                                          .Select(g => g.First())
                                          .Select(x => new FishRoute()
                                          {
                                              Edge = x.Edge,
                                              Increment = x.Increment,
                                              Interval = x.Interval,
                                              RoutesList = x.RoutesList,
                                          })
                                          .ToList();
            Config.SaveFishRoutes();
        }

        [Command]
        public void QuickRouteCellValueChanged(CellValueChangedArgs args)
        {
            SaveQuickRoutes();
        }

        [Command]
        public void ValidateNewQuickRoute(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            QuickRouteModel quickRoute = (QuickRouteModel)args.Item;
            if (quickRoute != null)
            {
                if (string.IsNullOrWhiteSpace(quickRoute.Route))
                {
                    RemoveQuickRoute(quickRoute);
                }
                else
                {
                    SaveQuickRoutes();
                }
            }
        }

        [Command]
        public void RemoveQuickRoute(QuickRouteModel quickRoute)
        {
            if (quickRoute == null)
            {
                return;
            }
            bool ok = MessageBoxService?.Show($"Are you sure you want to remove {quickRoute.Name}?",
                                         "Verification",
                                         MessageButton.YesNo,
                                         MessageIcon.Exclamation,
                                         MessageResult.No) == MessageResult.Yes;

            if (ok)
            {
                if (QuickRoutes.Contains(quickRoute))
                {
                    QuickRoutes.Remove(quickRoute);
                }
                SaveQuickRoutes();
            }
        }

        [Command]
        public void AddNewQuickRoute()
        {
            QuickRoutes.Insert(0, new QuickRouteModel("", ""));
        }

        public void SaveQuickRoutes()
        {
            Config.QuickRoutes = QuickRoutes.GroupBy(x => x.Id)
                                            .Select(g => g.First())
                                            .Select(x => Tuple.Create(x.Name, x.Route))
                                            .ToList();
            Config.SaveQuickRoutes();
        }

        [Command]
        public void CancelTimerCellValueChanged(CellValueChangedArgs args)
        {
            SaveCancelTimerLookup();
        }

        [Command]
        public void ValidateNewCancelTimer(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            CancelTimerModel model = (CancelTimerModel)args.Item;
            if (model != null)
            {
                if ((string.IsNullOrWhiteSpace(model.Route) &&
                    string.IsNullOrWhiteSpace(model.Symbol)) ||
                    model.Interval < 0)
                {
                    RemoveCancelTimer(model);
                }
                else
                {
                    SaveCancelTimerLookup();
                }
            }
        }

        [Command]
        public void RemoveCancelTimer(CancelTimerModel model)
        {
            if (model == null)
            {
                return;
            }

            if (CancelTimerLookup.Contains(model))
            {
                CancelTimerLookup.Remove(model);
            }
            SaveCancelTimerLookup();
        }

        [Command]
        public void AddNewCancelTimer()
        {
            CancelTimerLookup.Insert(0, new CancelTimerModel("", "", 1100, 1100));
        }

        private void SaveCancelTimerLookup()
        {
            var items = CancelTimerLookup
                  .GroupBy(x => x.Id)
                  .Select(g => g.First())
                  .Select(x => Tuple.Create(x.Symbol, x.Route, x.Interval, x.SingleLegInterval))
                  .ToList();
            Config.ClearCancelTimers();
            foreach (var item in items)
            {
                Config.AddCancelTimer(item);
            }
            Config.SaveCancelTimerLookup();
        }

        [Command]
        public void TicketStopLossCellValueChanged(CellValueChangedArgs args)
        {
            SaveTicketStopLossLookup();
        }

        [Command]
        public void ValidateNewTicketStopLoss(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            TicketStopLossModel model = (TicketStopLossModel)args.Item;
            if (model != null)
            {
                if (string.IsNullOrWhiteSpace(model.UnderlyingSymbol) || model.Interval < 0)
                {
                    RemoveTicketStopLoss(model);
                }
                else
                {
                    SaveTicketStopLossLookup();
                }
            }
        }

        [Command]
        public void RemoveTicketStopLoss(TicketStopLossModel model)
        {
            if (model == null)
            {
                return;
            }

            if (TicketStopLossLookup.Contains(model))
            {
                TicketStopLossLookup.Remove(model);
            }
            SaveTicketStopLossLookup();
        }

        [Command]
        public void AddNewTicketStopLoss()
        {
            TicketStopLossLookup.Insert(0, new TicketStopLossModel("", 250.0, .01, 5));
        }

        private void SaveTicketStopLossLookup()
        {
            Config.TicketStopLossLookup = TicketStopLossLookup
                  .GroupBy(x => x.Id)
                  .Select(g => g.First())
                  .Select(x => Tuple.Create(x.UnderlyingSymbol, x.Interval, x.Increment, x.Count))
                  .ToList();
            Config.SaveTicketStopLossLookup();
        }

        #region Basket Hedge
        [Command]
        public void BasketHedgeLookupCellValueChangedCommand(CellValueChangedArgs args)
        {
            SaveBasketHedgeLookup();
        }

        [Command]
        public void ValidateNewBasketHedgeLookupCommand(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            HedgeLookupModel model = (HedgeLookupModel)args.Item;
            if (model != null)
            {
                if (string.IsNullOrWhiteSpace(model.OrderSymbol) ||
                    string.IsNullOrWhiteSpace(model.HedgeSymbol) ||
                    model.Multiplier <= 0)
                {
                    RemoveBasketHedgeLookupCommand(model);
                }
                else
                {
                    SaveBasketHedgeLookup();
                }
            }
        }

        [Command]
        public void RemoveBasketHedgeLookupCommand(HedgeLookupModel model)
        {
            if (model == null)
            {
                return;
            }

            if (BasketHedgeLookup.Contains(model))
            {
                BasketHedgeLookup.Remove(model);
            }
            SaveBasketHedgeLookup();
        }

        [Command]
        public void AddNewBasketHedgeLookupCommand()
        {
            BasketHedgeLookup.Insert(0, new HedgeLookupModel("", "", 0.0));
        }

        private void SaveBasketHedgeLookup()
        {
            Config.BasketHedgeLookup = BasketHedgeLookup
                  .GroupBy(x => x.Id)
                  .Select(g => g.First())
                  .Select(x => Tuple.Create(x.OrderSymbol, x.HedgeSymbol, x.Multiplier))
                  .ToList();
            Config.SaveBasketHedgeLookupLookup();
        }
        #endregion

        #region Basket Market Maker Offset
        [Command]
        public void BasketMarketMakerOffsetLookupCellValueChangedCommand(CellValueChangedArgs args)
        {
            SaveBasketMarketMakerOffsetLookup();
        }

        [Command]
        public void ValidateNewBasketMarketMakerOffsetLookupCommand(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            BasketMarketMakerOffsetLookupModel model = (BasketMarketMakerOffsetLookupModel)args.Item;
            if (model != null)
            {
                if (string.IsNullOrWhiteSpace(model.Symbol) ||
                    model.StrikeOffset < 0 ||
                    model.MaxStrikeOffset < 0 ||
                    model.MinPriceDiff < 0)
                {
                    RemoveBasketMarketMakerOffsetLookupCommand(model);
                }
                else
                {
                    SaveBasketMarketMakerOffsetLookup();
                }
            }
        }

        [Command]
        public void RemoveBasketMarketMakerOffsetLookupCommand(BasketMarketMakerOffsetLookupModel model)
        {
            if (model == null)
            {
                return;
            }

            if (BasketMarketMakerOffsetLookup.Contains(model))
            {
                BasketMarketMakerOffsetLookup.Remove(model);
            }
            SaveBasketMarketMakerOffsetLookup();
        }

        [Command]
        public void AddNewBasketMarketMakerOffsetLookupCommand()
        {
            BasketMarketMakerOffsetLookup.Insert(0, new BasketMarketMakerOffsetLookupModel());
        }

        private void SaveBasketMarketMakerOffsetLookup()
        {
            Config.BasketMarketMakerOffsetLookup = BasketMarketMakerOffsetLookup.Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
                                                                                .DistinctBy(x => x.Symbol)
                                                                                .ToDictionary(x => x.Symbol, x => x);
            Config.SaveBasketMarketMakerOffsetLookup();
        }
        #endregion

        #region Basket Market Maker Offset
        [Command]
        public void DerivedValueConfigModelLookupCellValueChangedCommand(CellValueChangedArgs args)
        {
            SaveDerivedValueConfigModelLookup();
        }

        [Command]
        public void ValidateNewDerivedValueConfigModelLookupCommand(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            DerivedValueConfigModel model = (DerivedValueConfigModel)args.Item;
            if (model != null)
            {
                if (string.IsNullOrWhiteSpace(model.Symbol) ||
                    string.IsNullOrWhiteSpace(model.DerivedSymbol) ||
                    model.Multiplier < 0)
                {
                    RemoveDerivedValueConfigModelLookupCommand(model);
                }
                else
                {
                    SaveDerivedValueConfigModelLookup();
                }
            }
        }

        [Command]
        public void RemoveDerivedValueConfigModelLookupCommand(DerivedValueConfigModel model)
        {
            if (model == null)
            {
                return;
            }

            if (DerivedValueConfigModelLookup.Contains(model))
            {
                DerivedValueConfigModelLookup.Remove(model);
            }
            SaveDerivedValueConfigModelLookup();
        }

        [Command]
        public void AddNewDerivedValueConfigModelLookupCommand()
        {
            DerivedValueConfigModelLookup.Insert(0, new DerivedValueConfigModel());
        }

        private void SaveDerivedValueConfigModelLookup()
        {
            Config.DerivedValueConfigModelLookup = DerivedValueConfigModelLookup.Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
                                                                                .DistinctBy(x => x.Symbol)
                                                                                .ToDictionary(x => x.Symbol, x => x);
            Config.SaveDerivedValueConfigModelLookup();
        }
        #endregion

        [Command]
        public void LockTraderPriceLimitsCellValueChangedCommand(CellValueChangedArgs args)
        {
            SaveLockTraderPriceLimits();
        }

        [Command]
        public void ValidateNewLockTraderPriceLimitsCommand(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }
            LockTraderPriceLimitModel model = (LockTraderPriceLimitModel)args.Item;
            if (model != null)
            {
                if (model.Strategy == null)
                {
                    RemoveLockTraderPriceLimitsCommand(model);
                }
                else
                {
                    SaveLockTraderPriceLimits();
                }
            }
        }

        [Command]
        public void RemoveLockTraderPriceLimitsCommand(LockTraderPriceLimitModel model)
        {
            if (model == null)
            {
                return;
            }

            if (LockTraderPriceLimits.Contains(model))
            {
                LockTraderPriceLimits.Remove(model);
            }
            SaveLockTraderPriceLimits();
        }

        [Command]
        public void AddNewLockTraderPriceLimitsCommand()
        {
            LockTraderPriceLimits.Insert(0, new LockTraderPriceLimitModel());
        }

        private void SaveLockTraderPriceLimits()
        {
            Dictionary<BaseStrategy, LockTraderPriceLimitModel> dictionary = new Dictionary<BaseStrategy, LockTraderPriceLimitModel>();
            foreach (var x in LockTraderPriceLimits)
            {
                if (x.Strategy.HasValue)
                {
                    dictionary.Add(x.Strategy.Value, x);
                }
            }

            Config.LockTraderPriceLimits = dictionary;
            Config.SaveLockTraderPriceLimits();
        }
        #endregion

        [Command]
        public void SymbolsLookupCellValueChanged(CellValueChangedArgs args)
        {
            SaveSymbolsLookups();
        }

        [Command]
        public void ValidateNewSymbolsLookup(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            SymbolsLookupModel SymbolsLookup = (SymbolsLookupModel)args.Item;
            if (SymbolsLookup != null)
            {
                if (string.IsNullOrWhiteSpace(SymbolsLookup.New))
                {
                    RemoveSymbolsLookup(SymbolsLookup);
                }
                else
                {
                    SaveSymbolsLookups();
                }
            }
        }

        [Command]
        public void RemoveSymbolsLookup(SymbolsLookupModel symbolsLookup)
        {
            if (symbolsLookup == null)
            {
                return;
            }

            if (SymbolsLookup.Contains(symbolsLookup))
            {
                SymbolsLookup.Remove(symbolsLookup);
            }
            SaveSymbolsLookups();
        }

        [Command]
        public void AddNewSymbolsLookup()
        {
            SymbolsLookup.Insert(0, new SymbolsLookupModel("", "", 1D, false));
        }

        private void SaveSymbolsLookups()
        {
            Config.SymbolsLookup = SymbolsLookup
                .GroupBy(x => x.Old)
                .Select(g => g.First())
                .Select(x => Tuple.Create(x.Old, x.New, x.Multiplier, x.SubscribeToTicks))
                .ToDictionary(x => x.Item1, x => x);
            Config.ReverseSymbolsLookup.Clear();
            foreach (var tuple in Config.SymbolsLookup.Values)
            {
                if (!Config.ReverseSymbolsLookup.TryGetValue(tuple.Item2, out var list))
                {
                    list = [];
                    Config.ReverseSymbolsLookup[tuple.Item2] = list;
                }
                list.Add(tuple);
            }
            Config.SaveSymbolsLookup();
        }

        [Command]
        public void ContraEdgeLookupCellValueChanged(CellValueChangedArgs args)
        {
            SaveContraEdgeLookups();
        }

        [Command]
        public void ValidateNewContraEdgeLookup(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            ContraEdgeLookupModel contraEdgeLookup = (ContraEdgeLookupModel)args.Item;
            if (contraEdgeLookup != null)
            {
                if (string.IsNullOrWhiteSpace(contraEdgeLookup.Symbol))
                {
                    RemoveContraEdgeLookup(contraEdgeLookup);
                }
                else
                {
                    SaveContraEdgeLookups();
                }
            }
        }

        [Command]
        public void RemoveContraEdgeLookup(ContraEdgeLookupModel contraEdgeLookup)
        {
            if (contraEdgeLookup == null)
            {
                return;
            }

            if (ContraEdgeLookup.Contains(contraEdgeLookup))
            {
                ContraEdgeLookup.Remove(contraEdgeLookup);
            }
            SaveContraEdgeLookups();
        }

        [Command]
        public void AddNewContraEdgeLookup()
        {
            ContraEdgeLookup.Insert(0, new ContraEdgeLookupModel("", .15D));
        }

        private void SaveContraEdgeLookups()
        {
            Config.ContraEdgeLookup = ContraEdgeLookup
                .GroupBy(x => x.Symbol)
                .Select(g => g.First())
                .ToDictionary(x => x.Symbol, x => x.Edge);
            Config.SaveContraEdgeLookup();
        }

        [Command]
        public void RemoveRaptorClientConfig(RaptorClientConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (RaptorClientConfigs.Contains(config))
            {
                RaptorClientConfigs.Remove(config);
            }

            SaveRaptorClientConfigs();
        }

        [Command]
        public void AddNewRaptorClientConfig()
        {
            RaptorClientConfigs.Insert(0, Raptor.Client.Config.RaptorClientConfig.GetDefaultConfig());
        }

        private void SaveRaptorClientConfigs()
        {
            Config.RaptorClientConfigs = RaptorClientConfigs.ToList();
            Config.SaveRaptorClientConfig();
        }

        [Command]
        public void BlockTraderRouteCellValueChanged(CellValueChangedArgs args)
        {
            SaveBlockTraderRoutes();
        }

        [Command]
        public void ValidateNewBlockTraderRoute(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            QuickRouteModel BlockTraderRoute = (QuickRouteModel)args.Item;
            if (BlockTraderRoute != null)
            {
                if (string.IsNullOrWhiteSpace(BlockTraderRoute.Route))
                {
                    RemoveBlockTraderRoute(BlockTraderRoute);
                }
                else
                {
                    SaveBlockTraderRoutes();
                }
            }
        }

        [Command]
        public void RemoveBlockTraderRoute(QuickRouteModel BlockTraderRoute)
        {
            if (BlockTraderRoute == null)
            {
                return;
            }

            if (BlockTraderRoutes.Contains(BlockTraderRoute))
            {
                BlockTraderRoutes.Remove(BlockTraderRoute);
            }
            SaveBlockTraderRoutes();
        }

        [Command]
        public void AddNewBlockTraderRoute()
        {
            BlockTraderRoutes.Insert(0, new QuickRouteModel("", ""));
        }

        [Command]
        public void RestartRestApiCommand()
        {
            if (Config.RestApiEnabled)
            {
                RestApi?.Start();
            }
            else
            {
                RestApi?.Stop();
            }
        }

        public void SaveBlockTraderRoutes()
        {
            Config.BlockTraderRoutes = BlockTraderRoutes.GroupBy(x => x.Id)
                                                        .Select(g => g.First())
                                                        .Select(x => Tuple.Create(x.Name, x.Route))
                                                        .ToList();
            Config.SaveBlockTraderRoutes();
        }

        #region Custom Notifications

        [Command]
        public void CellValueChanged(CellValueChangedArgs args)
        {
            CellEditValueChanged();
        }

        [Command]
        public void CellEditValueChanged()
        {
            NotificationManager.SaveCustomNotifications();
            ShowMessage($"Notifications saved to {OmsConfig.GetCustomNotificationsExportPath()}");
        }

        [Command]
        public void ValidateNewCustomNotification(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            CustomNotificationModel customNotificationModel = (CustomNotificationModel)args.Item;
            if (customNotificationModel != null)
            {
                if (string.IsNullOrWhiteSpace(customNotificationModel.Tag))
                {
                    RemoveFilter(customNotificationModel);
                }
                else
                {
                    CellEditValueChanged();
                }
            }
        }

        [Command]
        public void RemoveFilter(CustomNotificationModel customNotificationModel)
        {
            if (customNotificationModel == null)
            {
                return;
            }

            if (customNotificationModel != null)
            {
                if (NotificationManager.CustomNotifications.Contains(customNotificationModel))
                {
                    NotificationManager.CustomNotifications.Remove(customNotificationModel);
                }
                CellEditValueChanged();
            }
        }

        [Command]
        public void TestSound(string soundId)
        {
            if (!string.IsNullOrWhiteSpace(soundId))
            {
                SoundManager.Play(soundId);
            }
        }

        [Command]
        public void AddNewCustomNotification()
        {
            NotificationManager.CustomNotifications.Insert(0, new CustomNotificationModel()
            {
                Sound = NotificationManager.SoundsList.FirstOrDefault()
            });
        }

        #endregion

        #region Underlying Risk Settings

        [Command]
        public void UnderlyingRiskModelCellValueChanged(CellValueChangedArgs args)
        {
            UnderlyingRiskModelCellEditValueChanged();
        }

        [Command]
        public void UnderlyingRiskModelCellEditValueChanged()
        {
            try
            {
                string file = Config.SaveUnderlyingRiskModel();
                ShowMessage($"Risk models saved to {file}");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnderlyingRiskModelCellEditValueChanged));
            }
        }

        [Command]
        public void ValidateNewUnderlyingRiskModelCustomNotification(RowValidationArgs args)
        {
            if (args == null)
            {
                return;
            }

            UnderlyingRiskModel customNotificationModel = (UnderlyingRiskModel)args.Item;
            if (customNotificationModel != null)
            {
                if (string.IsNullOrWhiteSpace(customNotificationModel.UnderlyingSymbols))
                {
                    RemoveUnderlyingRiskModelFilter(customNotificationModel);
                }
                else
                {
                    UnderlyingRiskModelCellEditValueChanged();
                }
            }
        }

        [Command]
        public void RemoveUnderlyingRiskModelFilter(UnderlyingRiskModel customNotificationModel)
        {
            if (customNotificationModel == null)
            {
                return;
            }

            if (customNotificationModel != null)
            {
                if (Config.UnderlyingRiskSettings.Contains(customNotificationModel))
                {
                    Config.UnderlyingRiskSettings.Remove(customNotificationModel);
                }
                UnderlyingRiskModelCellEditValueChanged();
            }
        }

        [Command]
        public void AddNewUnderlyingRiskModelCommand()
        {
            Config.UnderlyingRiskSettings.Insert(0, new UnderlyingRiskModel());
        }

        #endregion

        #region Account
        [Command]
        public async void UpdatePassword()
        {
            try
            {
                if (OmsCore.CalculateHash(NewPassword, "") == OmsCore.CalculateHash(ConfirmPassword, ""))
                {
                    bool success = await OmsCore.GatewayClient.RequestPasswordChangeAsync(CurrentPassword, NewPassword);
                    if (success)
                    {
                        ShowMessage("Password updated successfully.");
                    }
                    else
                    {
                        throw new SlimException("Password update failed.");
                    }
                }
                else
                {
                    throw new SlimException("New and Confirm passwords must be the same.");
                }
            }
            catch (Exception ex)
            {
                _ = Dispatcher.BeginInvoke(new Action(() => MessageBoxService.ShowMessage(ex.Message,
                                                            "ZeroPlus OMS",
                                                            MessageButton.OK,
                                                            MessageIcon.Error)));
            }
        }
        #endregion

        private void ShowMessage(string message)
        {
            Message = message;
            SetupClearMessageTimer();
        }

        private void SetupClearMessageTimer()
        {
            Timer timer = new(5000);
            timer.Elapsed += ClearMessage;
            timer.AutoReset = false;
            timer.Start();
        }

        private void ClearMessage(object sender, ElapsedEventArgs e)
        {
            Message = "";
        }

        private void OnConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            _ = Task.Run(() => OnConfigChangedEventAsync(requiresRestart));
        }

        private void OnConfigChangedEventAsync(bool requiresRestart)
        {
            if (requiresRestart)
            {
                bool ok = false;
                Dispatcher.Invoke(() =>
                {
                    ok = MessageBoxService.ShowMessage("The config change you made requires a restart.\n"
                                                       + "Would you like to restart now?",
                        "ZeroPlus OMS",
                        MessageButton.YesNo,
                        MessageIcon.Question,
                        MessageResult.Yes) == MessageResult.Yes;
                });

                if (ok)
                {
                    MessageResult? result = MessageResult.No;
                    Dispatcher.Invoke(new Action(() => result = MessageBoxService?.ShowMessage($"Do you want to save your workspace before quitting?", "ZeroPlus OMS", MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No)));

                    switch (result)
                    {
                        case MessageResult.Cancel:
                            return;
                        case MessageResult.Yes:
                            OmsCore.RequestSaveWorkspace();
                            break;
                    }

                    StartupWindowViewModel.RestartProgram("Config Update");
                }
            }
        }
    }
}