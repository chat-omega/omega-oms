using AutoMapper;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using NLog;
using NLog.Extensions.Logging;
using Python.Runtime;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.AutoTrader.Client.Config;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Cob.Client.Config;
using ZeroPlus.Cob.Client.Config.Interfaces;
using ZeroPlus.Cob.Client.Interfaces;
using ZeroPlus.Databento.Client.Config;
using ZeroPlus.Databento.Client.Config.Interfaces;
using ZeroPlus.Databento.Client.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Config;
using ZeroPlus.EdgeScanFeedRunner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Interfaces;
using ZeroPlus.EdgeScanner.Client.Config;
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanner.Client.Interfaces;
using ZeroPlus.Ema.Client.Config;
using ZeroPlus.Ema.Client.Config.Interfaces;
using ZeroPlus.Ema.Client.Interfaces;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.HubTron.Client.Config;
using ZeroPlus.HubTron.Client.Config.Interfaces;
using ZeroPlus.HubTron.Client.Interfaces;
using ZeroPlus.IbGateway.Client.Config;
using ZeroPlus.IbGateway.Client.Config.Interfaces;
using ZeroPlus.IbGateway.Client.Interfaces;
using ZeroPlus.Interpolator.Client.Config;
using ZeroPlus.Interpolator.Client.Config.Interfaces;
using ZeroPlus.Interpolator.Client.Interfaces;
using ZeroPlus.LiveVol.Client.Config;
using ZeroPlus.LiveVol.Client.Config.Interfaces;
using ZeroPlus.LiveVol.Client.Interfaces;
using ZeroPlus.Models.Buffers;
using ZeroPlus.Models.Buffers.Interfaces;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;
using ZeroPlus.Models.SoupBinTCP.Codecs;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Factories;
using ZeroPlus.Oms.Ui.Api;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.LowLatency;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.StartupHelpers;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.Pricing.Client.Config;
using ZeroPlus.Pricing.Client.Config.Interfaces;
using ZeroPlus.Pricing.Client.Interfaces;
using ZeroPlus.Raptor.Client.Config;
using ZeroPlus.Raptor.Client.Config.Interfaces;
using ZeroPlus.Raptor.Client.Interfaces;
using ZeroPlus.SymbolMap.Client.Config;
using ZeroPlus.SymbolMap.Client.Config.Interfaces;
using ZeroPlus.SymbolMap.Client.Interfaces;
using ZeroPlus.Telemetry.Client.Config;
using ZeroPlus.Telemetry.Client.Config.Interfaces;
using ZeroPlus.Telemetry.Client.Interfaces;
using ZeroPlus.Theos.Client.Config;
using ZeroPlus.Theos.Client.Config.Interfaces;
using ZeroPlus.Theos.Client.Interfaces;
using ZeroPlus.Trades.Client.Config;
using ZeroPlus.Trades.Client.Config.Interfaces;
using ZeroPlus.Trades.Client.Interfaces;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ZeroPlus.Oms.Ui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private static readonly NLog.ILogger _log = LogManager.GetCurrentClassLogger();

        public static IHost AppHost { get; private set; }

        public App()
        {
            string configBaseDir = OmsConfig.GetConfigDirectory();

            HerculesClientConfigParser herculesConfigParser = new(configBaseDir);
            System.Collections.Generic.List<string> configList = herculesConfigParser.GetSavedConfigsList();

            RaptorClientConfigParser raptorConfigParser = new(configBaseDir);
            configList = configList.Union(raptorConfigParser.GetSavedConfigsList()).ToList();

            EdgeScannerClientConfigParser memConfigParser = new(configBaseDir);
            configList = configList.Union(memConfigParser.GetSavedConfigsList()).ToList();

            EdgeScanFeedRunnerClientConfigParser edgeScanFeedRunnerConfigParser = new(configBaseDir);
            configList = configList.Union(edgeScanFeedRunnerConfigParser.GetSavedConfigsList()).ToList();

            SymbolMapClientConfigParser symbolMapClientConfigParser = new(configBaseDir);
            configList = configList.Union(symbolMapClientConfigParser.GetSavedConfigsList()).ToList();

            EmaClientConfigParser emaConfigParser = new(configBaseDir);
            configList = configList.Union(emaConfigParser.GetSavedConfigsList()).ToList();

            EmaServerConfig.EmaServerConfigParser emaServerConfigParser = new();
            configList = configList.Union(emaServerConfigParser.GetSavedConfigsList(configBaseDir)).ToList();

            InterpolatorClientConfigParser interpolatorConfigParser = new(configBaseDir);
            configList = configList.Union(interpolatorConfigParser.GetSavedConfigsList()).ToList();

            TheosClientConfigParser theosConfigParser = new(configBaseDir);
            configList = configList.Union(theosConfigParser.GetSavedConfigsList()).ToList();

            HubTronClientConfigParser hubTronConfigParser = new(configBaseDir);
            configList = configList.Union(hubTronConfigParser.GetSavedConfigsList()).ToList();

            IbGatewayClientConfigParser ibGatewayConfigParser = new(configBaseDir);
            configList = configList.Union(ibGatewayConfigParser.GetSavedConfigsList()).ToList();

            DatabentoClientConfigParser databentoConfigParser = new(configBaseDir);
            configList = configList.Union(databentoConfigParser.GetSavedConfigsList()).ToList();

            CobClientConfigParser cobConfigParser = new(configBaseDir);
            configList = configList.Union(cobConfigParser.GetSavedConfigsList()).ToList();

            PricingClientConfigParser pricingConfigParser = new(configBaseDir);
            configList = configList.Union(pricingConfigParser.GetSavedConfigsList()).ToList();

            AutoTraderClientConfigParser orderGatewayConfigParser = new(configBaseDir);
            configList = configList.Union(orderGatewayConfigParser.GetSavedConfigsList()).ToList();

            AutoTraderDirectClientConfigParser autoTraderDirectConfigParser = new(configBaseDir);
            configList = configList.Union(autoTraderDirectConfigParser.GetSavedConfigsList()).ToList();

            LiveVolClientConfigParser liveVolDataClientConfigParser = new(configBaseDir);
            configList = configList.Union(liveVolDataClientConfigParser.GetSavedConfigsList()).ToList();

            TelemetryClientConfigParser telemetryConfigParser = new(configBaseDir);
            configList = configList.Union(telemetryConfigParser.GetSavedConfigsList()).ToList();

            TradesClientConfigParser tradesClientConfigParser = new(configBaseDir);
            configList = configList.Union(tradesClientConfigParser.GetSavedConfigsList()).ToList();

            IHostBuilder builder = Host.CreateDefaultBuilder();
            builder.ConfigureAppConfiguration((configBuilder) =>
                {
                    foreach (string configPath in configList)
                    {
                        configBuilder.AddJsonFile(path: configPath, optional: true, reloadOnChange: true);
                    }
                    configBuilder.Build();
                }).ConfigureServices((_, services) =>
                {
                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.SetMinimumLevel(LogLevel.Information);
                        loggingBuilder.AddNLog(new NLogProviderOptions { RemoveLoggerFactoryFilter = false });
                    }).BuildServiceProvider();

                    services.AddSingleton<DispatcherStore>();
                    services.AddSingleton<IHerculesClientConfigParser, HerculesClientConfigParser>();
                    services.AddSingleton<IHerculesClientConfig, HerculesClientConfig>();
                    services.AddSingleton<IHerculesClient, Hercules.Client.HerculesClient>();
                    services.AddSingleton<HerculesClient>();
                    services.AddSingleton(_ => new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), new LoggerFactory()).CreateMapper());
                    services.AddSingleton<IAutoTraderConfigFactory, AutoTraderConfigFactory>();

                    services.AddSingleton<IRaptorClientConfigParser, RaptorClientConfigParser>();
                    services.AddTransient<IRaptorClientConfig, RaptorClientConfig>();
                    services.AddAbstractFactory<IRaptorClient, Raptor.Client.RaptorClient>();
                    services.AddSingleton<UpdateManager>();

                    services.AddSingleton<IEdgeScannerClientConfigParser, EdgeScannerClientConfigParser>();
                    services.AddSingleton<IEdgeScannerClientConfig, EdgeScannerClientConfig>();
                    services.AddSingleton<IEdgeScannerClient, EdgeScanner.Client.EdgeScannerClient>();
                    services.AddSingleton<EdgeScannerClient>();

                    services.AddSingleton<IEdgeScanFeedRunnerClientConfigParser, EdgeScanFeedRunnerClientConfigParser>();
                    services.AddSingleton<IEdgeScanFeedRunnerClientConfig, EdgeScanFeedRunnerClientConfig>();
                    services.AddSingleton<IEdgeScanFeedRunnerClient, EdgeScanFeedRunner.Client.EdgeScanFeedRunnerClient>();
                    services.AddSingleton<EdgeScanFeedRunnerClient>();

                    services.AddSingleton<ISymbolMapClientConfigParser, SymbolMapClientConfigParser>();
                    services.AddSingleton<ISymbolMapClientConfig, SymbolMapClientConfig>();
                    services.AddSingleton<ISymbolMapClient, SymbolMap.Client.SymbolMapClient>();
                    services.AddSingleton<SymbolMapClient>();

                    services.AddSingleton<ITelemetryClientConfigParser, TelemetryClientConfigParser>();
                    services.AddSingleton<ITelemetryClientConfig, TelemetryClientConfig>();
                    services.AddSingleton<ITelemetryClient, Telemetry.Client.TelemetryClient>();
                    services.AddSingleton<Clients.TelemetryClient>();

                    services.AddSingleton<IEmaClientConfigParser, EmaClientConfigParser>();
                    services.AddSingleton<IEmaClientConfig, EmaClientConfig>();
                    services.AddSingleton<IEmaClient, Ema.Client.EmaClient>();
                    services.AddSingleton<FullEmaClient>();

                    services.AddSingleton<EmaServerConfig.EmaServerConfigParser>();
                    services.AddSingleton<EMAServer.Client.IConfig, EmaServerConfig>();
                    services.AddSingleton<EMAServer.Client.EMAServerClient>();
                    services.AddSingleton<EmaServerClientModel>();

                    services.AddSingleton<IInterpolatorClientConfigParser, InterpolatorClientConfigParser>();
                    services.AddSingleton<IInterpolatorClientConfig, InterpolatorClientConfig>();
                    services.AddSingleton<IInterpolatorClient, Interpolator.Client.InterpolatorClient>();
                    services.AddSingleton<InterpolatorClient>();

                    services.AddSingleton<ITheosClientConfigParser, TheosClientConfigParser>();
                    services.AddSingleton<ITheosClientConfig, TheosClientConfig>();
                    services.AddSingleton<ITheosClient, Theos.Client.TheosClient>();
                    services.AddSingleton<TheosClient>();

                    services.AddSingleton<IHubTronClientConfigParser, HubTronClientConfigParser>();
                    services.AddSingleton<IHubTronClientConfig, HubTronClientConfig>();
                    services.AddSingleton<IHubTronClient, HubTron.Client.HubTronClient>();
                    services.AddSingleton<HubTronClient>();

                    services.AddSingleton<IOrderUpdateManager, OrderUpdateManager>();
                    services.AddSingleton<IIbGatewayClientConfigParser, IbGatewayClientConfigParser>();
                    services.AddSingleton<IIbGatewayClientConfig, IbGatewayClientConfig>();
                    services.AddSingleton<IIbGatewayClient, IbGateway.Client.IbGatewayClient>();
                    services.AddSingleton<IbGatewayClient>();

                    services.AddSingleton<IDatabentoClientConfigParser, DatabentoClientConfigParser>();
                    services.AddSingleton<IDatabentoClientConfig, DatabentoClientConfig>();
                    services.AddSingleton<IDatabentoClient, Databento.Client.DatabentoClient>();
                    services.AddSingleton<DatabentoClient>();

                    services.AddSingleton<IAutoTraderClientConfigParser, AutoTraderClientConfigParser>();
                    services.AddSingleton<IAutoTraderClientConfig, AutoTraderClientConfig>();
                    services.AddSingleton<IAutoTraderClient, AutoTrader.Client.AutoTraderClient>();
                    services.AddSingleton<AutoTraderClient>();

                    services.AddSingleton<AutoTraderClientFactory>();
                    services.AddSingleton<AutoTraderDirectClient>();
                    services.AddSingleton<AutoTraderDirectClientConfig>();
                    services.AddSingleton(autoTraderDirectConfigParser);

                    services.AddSingleton<ICobClientConfigParser, CobClientConfigParser>();
                    services.AddSingleton<ICobClientConfig, CobClientConfig>();
                    services.AddSingleton<ICobClient, Cob.Client.CobClient>();
                    services.AddSingleton<CobClient>();

                    services.AddSingleton<IPricingClientConfigParser, PricingClientConfigParser>();
                    services.AddSingleton<IPricingClientConfig, PricingClientConfig>();
                    services.AddSingleton<IPricingClient, Pricing.Client.PricingClient>();
                    services.AddSingleton<PricingClient>();

                    services.AddSingleton<ITradesClientConfigParser>(_ => tradesClientConfigParser);
                    services.AddSingleton<ITradesClientConfig, TradesClientConfig>();
                    services.AddSingleton<ITradesClient, Trades.Client.TradesClient>();
                    services.AddSingleton<TradesClient>();

                    services.AddSingleton<IUpdateManager>(s => s.GetRequiredService<UpdateManager>());

                    services.AddSingleton<PerformanceModeManager>();

                    services.AddSingleton<OmsCoreBuilder>();
                    services.AddSingleton(s => s.GetRequiredService<OmsCoreBuilder>().Build());

                    services.AddSingleton<BasketManagerModel>();
                    services.AddSingleton<ModelTradersManagerModel>();
                    services.AddSingleton<PortfolioManagerModel>();
                    services.AddSingleton<DominatorsManagerModel>();
                    services.AddSingleton<BasketGroupManagerModel>();
                    services.AddSingleton<NotificationManager>();

                    services.AddSingleton<ILiveVolClientConfigParser, LiveVolClientConfigParser>();
                    services.AddSingleton<ILiveVolClientConfig, LiveVolClientConfig>();
                    services.AddSingleton<ILiveVolClient, LiveVol.Client.LiveVolClient>();
                    services.AddSingleton<LiveVolDataClient>();

                    services.AddSingleton<IPortfolioManager>(s => s.GetRequiredService<PortfolioManagerModel>());
                    services.AddSingleton<ISecurityBook, SecurityBook>();
                    services.AddTransient<IReadBuffer, RingBuffer>();

                    services.AddSingleton<DirectBufferPooledObjectPolicy>();
                    services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
                    services.AddSingleton(serviceProvider =>
                    {
                        ObjectPoolProvider provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                        DirectBufferPooledObjectPolicy policy = serviceProvider.GetRequiredService<DirectBufferPooledObjectPolicy>();
                        return provider.Create(policy);
                    });

                    services.AddTransient<ISoupBinTcpEncoder, SoupBinTcpEncoder>();
                    services.AddTransient<ISoupBinTcpDecoder, SoupBinTcpDecoder>();

                    services.AddTransient<ISbeMessageEncoder, SbeMessageEncoder>();
                    services.AddTransient<ISbeMessageDecoder, SbeMessageDecoder>();
                    services.AddTransient<ILowLatencyInstance, LowLatencyInstance>();

                    services.AddSingleton<LowLatencyTransactionsProcessor>();
                    services.AddSingleton<TransactionConsumerModel>();
                    services.AddSingleton<PositionUpdateConsumer>();
                    services.AddSingleton<IOrderFactory>(x => x.GetService<TransactionConsumerModel>());
                    services.AddSingleton<IStatsProcessor>(x => x.GetService<TransactionConsumerModel>());
                    services.AddAbstractFactory<WinningTradeModel, WinningTradeModel>();
                    services.AddAbstractFactory<OmsOrderModel, OmsOrderModel>();
                    services.AddAbstractFactory<ThreeWayCloser, ThreeWayCloser>();
                    services.AddAbstractFactory<SymbolHedgeManagerModel, SymbolHedgeManagerModel>();
                    services.AddAbstractFactory<ComplexOrderTicketViewModel, ComplexOrderTicketViewModel>();
                    services.AddAbstractFactory<MainView, MainView>();
                    services.AddAbstractFactory<CustomEdgeFunctionEditorView, CustomEdgeFunctionEditorView>();

                    services.AddAbstractFactory<LowLatencyModel, LowLatencyModel>();
                    services.AddAbstractFactory<DominatorTraderModel, DominatorTraderModel>();

                    services.AddAbstractFactory<RouteSelectionViewModel, RouteSelectionViewModel>();

                    services.AddSingleton<DelayedTicketsManager>();
                    services.AddTransient<GammaScalpOrderTicket>();

                    services.AddSingleton<IModuleFactory, ModuleFactory>();
                    // Singleton modules
                    services.AddTransient<LatencyIndicatorViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<StartupWindowView>();
                    services.AddSingleton<StartupWindowViewModel>();
                    services.AddSingleton<NotificationsView>();
                    services.AddSingleton<NotificationsViewModel>();
                    services.AddSingleton<RestApi>();
                    services.AddSingleton<VolTradersManager>();
                    services.AddSingleton<BulletinBroker>();
                    services.AddSingleton<ExecutionTransactionsContainer>();

                    // Regular modules
                    services.AddTransient<NewDominatorManagerViewModel>();
                    services.AddTransient<ConfigBrowserViewModel>();
                    services.AddTransient<NotificationItemViewModel>();
                    services.AddTransient<ArchiveRequestViewModel>();
                    services.AddTransient<BasketManagerViewModel>();
                    services.AddTransient<BasketTraderViewModel>();
                    services.AddTransient<CoLoTradeManagerViewModel>();
                    services.AddTransient<ChangeLogViewModel>();
                    services.AddTransient<ChartViewModel>();
                    services.AddTransient<ChartModuleViewModel>();
                    services.AddTransient<ComplexOrderTicketViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<UserPositionViewModel>();
                    services.AddTransient<DominatorsManagerViewModel>();
                    services.AddTransient<EmptyOrderBookViewModel>();
                    services.AddTransient<LockTraderViewModel>();
                    services.AddTransient<OptionChainViewModel>();
                    services.AddTransient<OrderBookViewModel>();
                    services.AddTransient<PnlReportViewModel>();
                    services.AddTransient<PortfolioViewModel>();
                    services.AddTransient<DeltaHedgingViewModel>();
                    services.AddTransient<GammaScalpingModuleViewModel>();
                    services.AddTransient<ComboTraderViewModel>();
                    services.AddTransient<AddHedgePositionViewModel>();
                    services.AddTransient<AddHedgeUnderlyingViewModel>();
                    services.AddTransient<HedgePositionManagementViewModel>();
                    services.AddTransient<PositionManagerViewModel>();
                    services.AddTransient<ReleaseNotesViewModel>();
                    services.AddTransient<ReportBugViewModel>();
                    services.AddTransient<SaveViewModel>();
                    services.AddTransient<ShareWithViewModel>();
                    services.AddTransient<SpreadHeatmapViewModel>();
                    services.AddTransient<SpreadsGeneratorViewModel>();
                    services.AddTransient<SpreadTemplateViewModel>();
                    services.AddTransient<TradesViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<DominatorListRollerViewModel>();
                    services.AddTransient<PositionsViewModel>();
                    services.AddTransient<PermComboEditorViewModel>();
                    services.AddTransient<RiskWarningMessageViewModel>();
                    services.AddTransient<VolTraderViewModel>();
                    services.AddTransient<BlockSymbolFromDominatorViewModel>();
                    services.AddTransient<PositionAlertConfigurationViewModel>();
                    services.AddTransient<LiveChartViewModel>();
                    services.AddTransient<FishedSymbolsRequestViewModel>();
                    services.AddTransient<EdgeScanFeedViewModel>();
                    services.AddTransient<ExplorerWindowViewModel>();
                    services.AddTransient<ModelTraderViewModel>();
                    services.AddTransient<SmartRouteEditorViewModel>();
                    services.AddTransient<EdgeScanFeedTradeFilterViewModel>();
                    services.AddTransient<BasketBlockListConfigViewModel>();
                    services.AddTransient<EdgeScanFeedFilterSelectionViewModel>();
                    services.AddTransient<ExecutingBrokerFeeModelViewModel>();
                    services.AddTransient<DynamicEdgeManagementViewModel>();
                    services.AddTransient<DynamicEdgeConfigViewModel>();
                    services.AddTransient<LoopSizeupManagementViewModel>();
                    services.AddTransient<LoopSizeupViewModel>();
                    services.AddTransient<HedgeHouseViewModel>();
                    services.AddTransient<PairTraderViewModel>();
                    services.AddTransient<NagbotIntervalManagementViewModel>();
                    services.AddTransient<NagbotIntervalViewModel>();
                    services.AddTransient<DynamicIntervalConfigViewModel>();
                    services.AddTransient<DynamicIntervalManagementViewModel>();
                    services.AddTransient<EdgeScanFeedBannedSymbolsListManagerViewModel>();
                    services.AddTransient<BannedSymbolsListViewModel>();
                    services.AddTransient<OrderDetailsViewModel>();
                    services.AddTransient<AutoCloseConfigViewModel>();
                    services.AddTransient<TradeFeedViewModel>();
                    services.AddTransient<MlTradersControlViewModel>();
                    services.AddTransient<PairTradersControllerViewModel>();
                    services.AddTransient<PositionAnalyzerViewModel>();
                    services.AddTransient<TradePnlChartViewModel>();
                    services.AddTransient<EodRiskViewModel>();
                    services.AddTransient<ComplexOrderLegsViewModel>();
                    services.AddTransient<AutomationConfigMappingViewModel>();
                    services.AddTransient<OrderTaggerViewModel>();
                    services.AddTransient<PortfolioAdjustmentViewModel>();
                    services.AddTransient<EmaChartViewModel>();
                    services.AddTransient<AutoPermConfigViewModel>();
                    services.AddTransient<DominatorViewModel>();
                    services.AddTransient<MacdContext>();
                    services.AddTransient<EdgeScanFeedFilterStrategySelectorViewModel>();
                    services.AddTransient<EdgeScanFeedFilterBlockedExpirationsConfigViewModel>();
                    services.AddTransient<DominatorConfigurationViewModel>();
                    services.AddTransient<DominatorConfigurationModuleViewModel>();
                    services.AddTransient<ScriptTraderViewModel>();
                    services.AddTransient<ScriptEditorViewModel>();
                    services.AddTransient<EmaConfigViewModel>();
                    services.AddTransient<EmaConfigWindowViewModel>();
                    services.AddTransient<MacdConfigWindowViewModel>();
                    services.AddTransient<LoopAdvancedConfigsViewModel>();
                    services.AddTransient<LowLatencyManagerViewModel>();
                    services.AddTransient<LowLatencyConfigEditorViewModel>();
                    services.AddTransient<LowLatencyOrderBookViewModel>();
                    services.AddTransient<DynamicConfigManagementViewModel>();
                    services.AddTransient<OptionSelectorViewModel>();
                    services.AddTransient<CustomEdgeFunctionEditorViewModel>();
                    services.AddTransient<ExchToRouteMapConfigViewModel>();
                    services.AddTransient<LowLatencyManualAdjustmentRequestViewModel>();
                    services.AddTransient<LoopDynamicIncrementConfigViewModel>();
                    services.AddTransient<BulletinBoardViewModel>();
                    services.AddTransient<CustomListEditorViewModel>();
                    services.AddTransient<MarketMoversViewModel>();
                    services.AddTransient<BasketGroupViewModel>();
                    services.AddTransient<EdgeScanFeedStatisticsViewModel>();
                    services.AddTransient<WinningTradesMonitorViewModel>();
                    services.AddTransient<LoadInBasketTraderPromptViewModel>();
                    services.AddTransient<GammaScalpViewModel>();
                    services.AddTransient<CloseSubsMonitorViewModel>();
                    services.AddTransient<SyntheticSpreadConfigViewModel>();
                    services.AddTransient<ExecutionTransactionsViewModel>();
                    services.AddTransient<CobFeedViewModel>();
                    services.AddTransient<QuotesAndGreeksBoardViewModel>();
                    services.AddTransient<LoadSymbolViewModel>();
                    services.AddTransient<ImpliedQuotesFeedViewModel>();
                    services.AddTransient<CobOrdersViewModel>();
                    services.AddTransient<LiveVolDataViewModel>();
                    services.AddTransient<AdminControlsViewModel>();

                    // Late binding view models
                    services.AddTransient<AddColumnViewModel>();
                    services.AddTransient<AlertConfigurationViewModel>();
                    services.AddTransient<TableCustomizationViewModel>();
                    services.AddTransient<UpdateColumnHeaderViewModel>();
                    services.AddTransient<SpreadGeneratorResultParserInputViewModel>();
                });
            AppHost = builder.Build();
            OmsCore omsCore = AppHost.Services.GetRequiredService<OmsCore>();
            AppHost.Services.GetService<PortfolioManagerModel>().OmsCore = omsCore;
            ServiceLocator.Instance.AddService(omsCore);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await AppHost!.StartAsync();

                DISource.Resolver = type => AppHost.Services.GetRequiredService(type);

                BrochureView.ShowBrochure();

                DispatcherStore dispatcherStore = AppHost.Services.GetRequiredService<DispatcherStore>();
                StartupWindowView startupWindowView = AppHost.Services.GetRequiredService<StartupWindowView>();
                startupWindowView.Dispatcher.Thread.Name = "Main Dispatcher Thread";
                dispatcherStore.SetModuleCommonDispatcher(Module.MainWindow, startupWindowView.Dispatcher);
                if (e.Args.Length == 2)
                {
                    startupWindowView.Loaded += (_, _) =>
                    {
                        StartupWindowViewModel startupWindowViewModel = (StartupWindowViewModel)startupWindowView.DataContext;
                        string username = e.Args[0];
                        string authCode = e.Args[1];
                        startupWindowViewModel.LoginWithAuthCode(username, authCode);
                    };
                }
                else if (e.Args.Length == 4)
                {
                    startupWindowView.Loaded += (_, _) =>
                    {
                        StartupWindowViewModel startupWindowViewModel = (StartupWindowViewModel)startupWindowView.DataContext;
                        string username = e.Args[1];
                        string password = e.Args[2];
                        startupWindowViewModel.Username = username;
                        startupWindowViewModel.SecurePassword = new NetworkCredential("", password).SecurePassword;
                        startupWindowViewModel.Login();
                    };
                }
                startupWindowView.Show();

                AppHost.Services.GetRequiredService<IHerculesClient>();

                StartApi();

                StartKillSwitch();

                RegisterPython();

                if (ApplicationThemeHelper.ApplicationThemeName != Theme.VS2019DarkName)
                {
                    ApplicationThemeHelper.ApplicationThemeName = Theme.VS2019DarkName;
                }

                base.OnStartup(e);
            }
            catch (SlimException ex)
            {
                _log.Error(ex);
                //RestartProgram("config update");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical Exception Report.\n{ex.Message}\n{ex.InnerException?.Message}\nPlease restart program.",
                    "ZeroPlus OMS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                _log.Error(ex);
                Environment.Exit(-1);
            }
        }

        private void StartApi()
        {
            if (OmsCore.Config.RestApiEnabled)
            {
                Task.Run(() =>
                {
                    RestApi restApi = AppHost.Services.GetRequiredService<RestApi>();
                    restApi.Start();
                });
            }
        }

        private void StartKillSwitch()
        {
            try
            {
                string processName = "OMS Kill Switch";
                Process[] processes = Process.GetProcessesByName(processName);
                foreach (Process process in processes)
                {
                    process.Kill();
                }
#if RELEASE
                Process.Start("Resources\\OMS Kill Switch.exe");
#endif
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            base.OnExit(e);
        }

        private void RegisterPython()
        {
            try
            {
                if (OmsCore.Config.EnablePythonEngine)
                {
                    string pythonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
                    if (Directory.Exists(pythonPath))
                    {
                        string[] dirs = Directory.GetDirectories(pythonPath);
                        string version = "Python310";

                        foreach (string dir in dirs.OrderBy(x => x))
                        {
                            try
                            {
                                string fullPath = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
                                string name = Path.GetFileName(fullPath);
                                if (name.StartsWith("Python3"))
                                {
                                    version = name;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex);
                            }
                        }

                        string pythonDll = Path.Combine(pythonPath, version, $"{version.ToLower()}.dll");
                        if (File.Exists(pythonDll))
                        {
                            Runtime.PythonDLL = pythonDll;
                            string pathToVirtualEnv = @"path\to\env";

                            string path = Environment.GetEnvironmentVariable("PATH")?.TrimEnd(';');
                            path = string.IsNullOrEmpty(path) ? pathToVirtualEnv : path + ";" + pathToVirtualEnv;

                            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
                            Environment.SetEnvironmentVariable("PATH", pathToVirtualEnv, EnvironmentVariableTarget.Process);
                            Environment.SetEnvironmentVariable("PYTHONPATH", $"{pathToVirtualEnv}\\Lib\\site-packages;{pathToVirtualEnv}\\Lib", EnvironmentVariableTarget.Process);

                            PythonEngine.Initialize();
                            PythonEngine.BeginAllowThreads();

                            PythonEngine.PythonHome = pathToVirtualEnv;
                            PythonEngine.PythonPath = Environment.GetEnvironmentVariable("PYTHONPATH", EnvironmentVariableTarget.Process) ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }
    }
}
