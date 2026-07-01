using DevExpress.Mvvm.POCO;
using ExcelDna.ComInterop;
using ExcelDna.Integration;
using ExcelDna.IntelliSense;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Office.Interop.Excel;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.AutoTrader.Client.Config;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Cob.Client.Config;
using ZeroPlus.Cob.Client.Config.Interfaces;
using ZeroPlus.Cob.Client.Interfaces;
using ZeroPlus.Databento.Client.Config;
using ZeroPlus.Databento.Client.Config.Interfaces;
using ZeroPlus.Databento.Client.Interfaces;
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
using ZeroPlus.Models.Buffers;
using ZeroPlus.Models.Buffers.Interfaces;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;
using ZeroPlus.Models.SoupBinTCP.Codecs;
using ZeroPlus.Models.SoupBinTCP.Codecs.Interfaces;
using ZeroPlus.Oms.AddIn.Macro;
using ZeroPlus.Oms.AddIn.Ribbon.Helpers;
using ZeroPlus.Oms.AddIn.Ribbon.ViewModels;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Factories;
using ZeroPlus.Oms.Indicators;
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

namespace ZeroPlus.Oms.AddIn
{
    public class ExcelAddIn : IExcelAddIn
    {
        private const string DIR_LOCK_FILE = "_dir_lock.tmp";
        private const string APP_CODE = "OMS ADDIN";

        private static ILogger _log;
        private FileStream _updateDirLock;

        public IHost AppHost { get; }
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public ExcelAddIn()
        {
            HerculesClientConfigParser herculesConfigParser = new(OmsConfig.GetConfigDirectory());
            System.Collections.Generic.List<string> configList = herculesConfigParser.GetSavedConfigsList();

            RaptorClientConfigParser raptorConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(raptorConfigParser.GetSavedConfigsList()).ToList();

            EdgeScannerClientConfigParser mCacheConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(mCacheConfigParser.GetSavedConfigsList()).ToList();

            EmaClientConfigParser emaClientConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(emaClientConfigParser.GetSavedConfigsList()).ToList();

            AutoTraderClientConfigParser autoTraderClientConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(autoTraderClientConfigParser.GetSavedConfigsList()).ToList();

            AutoTraderDirectClientConfigParser autoTraderDirectConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(autoTraderDirectConfigParser.GetSavedConfigsList()).ToList();

            InterpolatorClientConfigParser interpolatorClientConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(interpolatorClientConfigParser.GetSavedConfigsList()).ToList();

            TheosClientConfigParser theosClientConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(theosClientConfigParser.GetSavedConfigsList()).ToList();

            HubTronClientConfigParser hubTronConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(hubTronConfigParser.GetSavedConfigsList()).ToList();

            TelemetryClientConfigParser telemetryConfigParser = new(OmsConfig.GetConfigDirectory());
            configList = configList.Union(telemetryConfigParser.GetSavedConfigsList()).ToList();

            IHostBuilder builder = Host.CreateDefaultBuilder();
            builder
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    foreach (string configPath in configList)
                    {
                        configBuilder.AddJsonFile(path: configPath, optional: true, reloadOnChange: true);
                    }
                    configBuilder.Build();
                })
                .ConfigureLogging(x =>
                {
                    x.ClearProviders();
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IPortfolioManager, PortfolioManager>();
                    services.AddSingleton<IOrderFactory, BasicOrderFactory>();

                    services.AddSingleton<IHerculesClientConfigParser, HerculesClientConfigParser>();
                    services.AddSingleton<IHerculesClientConfig, HerculesClientConfig>();
                    services.AddSingleton<IHerculesClient, Hercules.Client.HerculesClient>();
                    services.AddSingleton<HerculesClient>();

                    services.AddSingleton<IRaptorClientConfigParser, RaptorClientConfigParser>();
                    services.AddSingleton<IRaptorClientConfig, RaptorClientConfig>();
                    services.AddAbstractFactory<IRaptorClient, Raptor.Client.RaptorClient>();
                    services.AddSingleton<UpdateManager>();

                    services.AddSingleton<IEdgeScannerClientConfigParser, EdgeScannerClientConfigParser>();
                    services.AddSingleton<IEdgeScannerClientConfig, EdgeScannerClientConfig>();
                    services.AddSingleton<IEdgeScannerClient, EdgeScanner.Client.EdgeScannerClient>();
                    services.AddSingleton<EdgeScannerClient>();

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

                    services.AddSingleton(autoTraderDirectConfigParser);
                    services.AddSingleton<AutoTraderDirectClient>();
                    services.AddSingleton<AutoTraderDirectClientConfig>();
                    services.AddSingleton<AutoTraderClientFactory>();

                    services.AddSingleton<ICobClientConfigParser, CobClientConfigParser>();
                    services.AddSingleton<ICobClientConfig, CobClientConfig>();
                    services.AddSingleton<ICobClient, Cob.Client.CobClient>();
                    services.AddSingleton<CobClient>();

                    services.AddSingleton<IPricingClientConfigParser, PricingClientConfigParser>();
                    services.AddSingleton<IPricingClientConfig, PricingClientConfig>();
                    services.AddSingleton<IPricingClient, Pricing.Client.PricingClient>();
                    services.AddSingleton<PricingClient>();

                    services.AddSingleton<IUpdateManager>(s => s.GetRequiredService<UpdateManager>());

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

                    services.AddTransient<SettingsViewModel>();
                });

            AppHost = builder.Build();

            _log = AppHost.Services.GetService<ILogger>();
        }

        public async void AutoOpen()
        {
            try
            {
                await AppHost!.StartAsync();

                DISource.Resolver = AppHost.Services.GetRequiredService;

                ExcelIntegration.RegisterUnhandledExceptionHandler(HandleUnhandledException);
                LockUpdateDirectory();

                IHerculesClientConfig herculesClientConfig = AppHost.Services.GetRequiredService<IHerculesClientConfig>();
                IHerculesClientConfigParser herculesClientConfigParser = AppHost.Services.GetRequiredService<IHerculesClientConfigParser>();
                IHerculesClient herculesClient = AppHost.Services.GetRequiredService<IHerculesClient>();
                HerculesClient herculesClientWrapper = AppHost.Services.GetRequiredService<HerculesClient>();
                herculesClientWrapper.Initialize(herculesClient);

                IRaptorClientConfig raptorClientConfig = AppHost.Services.GetRequiredService<IRaptorClientConfig>();
                IRaptorClientConfigParser raptorClientConfigParser = AppHost.Services.GetRequiredService<IRaptorClientConfigParser>();
                IAbstractFactory<IRaptorClient> raptorClientFactory = AppHost.Services.GetRequiredService<IAbstractFactory<IRaptorClient>>();
                UpdateManager updateManager = AppHost.Services.GetRequiredService<UpdateManager>();

                IEdgeScannerClientConfig edgeScannerClientConfig = AppHost.Services.GetRequiredService<IEdgeScannerClientConfig>();
                IEdgeScannerClientConfigParser edgeScannerClientConfigParser = AppHost.Services.GetRequiredService<IEdgeScannerClientConfigParser>();
                IEdgeScannerClient edgeScannerClientLib = AppHost.Services.GetRequiredService<IEdgeScannerClient>();
                EdgeScannerClient edgeScannerClient = AppHost.Services.GetRequiredService<EdgeScannerClient>();
                edgeScannerClient.Initialize(edgeScannerClientLib);
                updateManager.Initialize(edgeScannerClientLib);

                ISymbolMapClientConfig symbolMapClientConfig = AppHost.Services.GetRequiredService<ISymbolMapClientConfig>();
                ISymbolMapClientConfigParser symbolMapClientConfigParser = AppHost.Services.GetRequiredService<ISymbolMapClientConfigParser>();
                ISymbolMapClient symbolMapClientLib = AppHost.Services.GetRequiredService<ISymbolMapClient>();
                SymbolMapClient symbolMapClient = AppHost.Services.GetRequiredService<SymbolMapClient>();
                symbolMapClient.Initialize(symbolMapClientLib);

                ITelemetryClientConfig telemetryClientConfig = AppHost.Services.GetRequiredService<ITelemetryClientConfig>();
                ITelemetryClientConfigParser telemetryClientConfigParser = AppHost.Services.GetRequiredService<ITelemetryClientConfigParser>();
                ITelemetryClient telemetryClientLib = AppHost.Services.GetRequiredService<ITelemetryClient>();
                Clients.TelemetryClient telemetryClient = AppHost.Services.GetRequiredService<Clients.TelemetryClient>();
                telemetryClient.Initialize(telemetryClientLib);

                IEmaClientConfig emaClientConfig = AppHost.Services.GetRequiredService<IEmaClientConfig>();
                IEmaClientConfigParser emaClientConfigParser = AppHost.Services.GetRequiredService<IEmaClientConfigParser>();
                IEmaClient emaClientLib = AppHost.Services.GetRequiredService<IEmaClient>();
                FullEmaClient fullEmaClient = AppHost.Services.GetRequiredService<FullEmaClient>();
                fullEmaClient.Initialize(emaClientLib);
                updateManager.Initialize(emaClientLib);

                IInterpolatorClientConfig interpolatorClientConfig = AppHost.Services.GetRequiredService<IInterpolatorClientConfig>();
                IInterpolatorClientConfigParser interpolatorClientConfigParser = AppHost.Services.GetRequiredService<IInterpolatorClientConfigParser>();
                IInterpolatorClient interpolatorClientLib = AppHost.Services.GetRequiredService<IInterpolatorClient>();
                InterpolatorClient interpolatorClient = AppHost.Services.GetRequiredService<InterpolatorClient>();
                interpolatorClient.Initialize(interpolatorClientLib);
                updateManager.Initialize(interpolatorClientLib);

                ITheosClientConfig theosClientConfig = AppHost.Services.GetRequiredService<ITheosClientConfig>();
                ITheosClientConfigParser theosClientConfigParser = AppHost.Services.GetRequiredService<ITheosClientConfigParser>();
                ITheosClient theosClientLib = AppHost.Services.GetRequiredService<ITheosClient>();
                TheosClient theosClient = AppHost.Services.GetRequiredService<TheosClient>();
                theosClient.Initialize(theosClientLib);
                updateManager.Initialize(theosClientLib);

                IHubTronClientConfig hubTronClientConfig = AppHost.Services.GetRequiredService<IHubTronClientConfig>();
                IHubTronClientConfigParser hubTronClientConfigParser = AppHost.Services.GetRequiredService<IHubTronClientConfigParser>();
                IHubTronClient hubTronClientLib = AppHost.Services.GetRequiredService<IHubTronClient>();
                HubTronClient hubTronClient = AppHost.Services.GetRequiredService<HubTronClient>();
                hubTronClient.Initialize(hubTronClientLib);
                updateManager.Initialize(hubTronClientLib);

                IIbGatewayClientConfig ibGatewayClientConfig = AppHost.Services.GetRequiredService<IIbGatewayClientConfig>();
                IIbGatewayClientConfigParser ibGatewayClientConfigParser = AppHost.Services.GetRequiredService<IIbGatewayClientConfigParser>();
                IIbGatewayClient ibGatewayClientLib = AppHost.Services.GetRequiredService<IIbGatewayClient>();
                IbGatewayClient ibGatewayClient = AppHost.Services.GetRequiredService<IbGatewayClient>();
                ibGatewayClient.Initialize(ibGatewayClientLib);
                updateManager.Initialize(ibGatewayClientLib);

                IDatabentoClientConfig databentoClientConfig = AppHost.Services.GetRequiredService<IDatabentoClientConfig>();
                IDatabentoClientConfigParser databentoClientConfigParser = AppHost.Services.GetRequiredService<IDatabentoClientConfigParser>();
                IDatabentoClient databentoClientLib = AppHost.Services.GetRequiredService<IDatabentoClient>();
                DatabentoClient databentoClient = AppHost.Services.GetRequiredService<DatabentoClient>();
                databentoClient.Initialize(databentoClientLib);
                updateManager.Initialize(databentoClientLib);

                ICobClientConfig cobClientConfig = AppHost.Services.GetRequiredService<ICobClientConfig>();
                ICobClientConfigParser cobClientConfigParser = AppHost.Services.GetRequiredService<ICobClientConfigParser>();
                ICobClient cobClientLib = AppHost.Services.GetRequiredService<ICobClient>();
                CobClient cobClient = AppHost.Services.GetRequiredService<CobClient>();
                cobClient.Initialize(cobClientLib);
                updateManager.Initialize(cobClientLib);

                IPricingClientConfig pricingClientConfig = AppHost.Services.GetRequiredService<IPricingClientConfig>();
                IPricingClientConfigParser pricingClientConfigParser = AppHost.Services.GetRequiredService<IPricingClientConfigParser>();
                IPricingClient pricingClientLib = AppHost.Services.GetRequiredService<IPricingClient>();
                PricingClient pricingClient = AppHost.Services.GetRequiredService<PricingClient>();
                pricingClient.Initialize(pricingClientLib);

                ISecurityBook securityBook = AppHost.Services.GetRequiredService<ISecurityBook>();
                IAutoTraderClientConfig orderGatewayClientConfig = AppHost.Services.GetRequiredService<IAutoTraderClientConfig>();
                IAutoTraderClientConfigParser orderGatewayClientConfigParser = AppHost.Services.GetRequiredService<IAutoTraderClientConfigParser>();
                IAutoTraderClient orderGatewayClientLib = AppHost.Services.GetRequiredService<IAutoTraderClient>();
                AutoTraderClient orderGatewayClient = AppHost.Services.GetRequiredService<AutoTraderClient>();
                orderGatewayClient.Initialize(orderGatewayClientLib);

                AutoTraderDirectClientConfig autoTraderDirectClientConfig = AppHost.Services.GetRequiredService<AutoTraderDirectClientConfig>();
                AutoTraderDirectClientConfigParser autoTraderDirectClientConfigParser = AppHost.Services.GetRequiredService<AutoTraderDirectClientConfigParser>();
                AutoTraderDirectClient autoTraderDirectClient = AppHost.Services.GetRequiredService<AutoTraderDirectClient>();
                var autoTraderClientFactory = AppHost.Services.GetRequiredService<AutoTraderClientFactory>();
                autoTraderDirectClient.Initialize(autoTraderClientFactory.CreateAutoTraderClient(autoTraderDirectClientConfig));

                var portfolioManager = AppHost.Services.GetRequiredService<IPortfolioManager>();
                ServiceLocator.Instance.AddService(portfolioManager as PortfolioManager);

                OmsConfig config = OmsConfig.LoadConfig(
                    herculesClientConfig,
                    herculesClientConfigParser,
                    raptorClientConfig,
                    raptorClientConfigParser,
                    edgeScannerClientConfig,
                    edgeScannerClientConfigParser,
                    null,
                    null,
                    symbolMapClientConfig,
                    symbolMapClientConfigParser,
                    emaClientConfig,
                    emaClientConfigParser,
                    null,
                    null,
                    interpolatorClientConfig,
                    interpolatorClientConfigParser,
                    theosClientConfig,
                    theosClientConfigParser,
                    hubTronClientConfig,
                    hubTronClientConfigParser,
                    ibGatewayClientConfig,
                    ibGatewayClientConfigParser,
                    databentoClientConfig,
                    databentoClientConfigParser,
                    cobClientConfig,
                    cobClientConfigParser,
                    pricingClientConfig,
                    pricingClientConfigParser,
                    orderGatewayClientConfig,
                    orderGatewayClientConfigParser,
                    autoTraderDirectClientConfig,
                    autoTraderDirectClientConfigParser,
                    null,
                    null,
                    telemetryClientConfig,
                    telemetryClientConfigParser,
                    null,
                    null,
                    APP_CODE.Replace(" ", "_"));
                config.CheckForUpdateOnInterval = false;
                config.AppId = "ZeroPlus OMS AddIn";

                var defaultRaptorClient = raptorClientFactory.Create();
                defaultRaptorClient.UpdateConfig(raptorClientConfig);
                List<IRaptorClient> raptorClients =
                [
                    defaultRaptorClient
                ];
                foreach (RaptorClientConfig configClient in config.RaptorClientConfigs)
                {
                    var client = raptorClientFactory.Create();
                    client.UpdateConfig(configClient);
                    raptorClients.Add(client);
                }
                updateManager.Initialize(raptorClients);

                OmsCore omsCore = new(config,
                    herculesClientConfig,
                    herculesClient,
                    null,
                    updateManager,
                    edgeScannerClient,
                    null,
                    symbolMapClient,
                    telemetryClient,
                    herculesClientWrapper,
                    fullEmaClient,
                    null,
                    interpolatorClient,
                    theosClient,
                    hubTronClient,
                    ibGatewayClient,
                    databentoClient,
                    cobClient,
                    pricingClient,
                    orderGatewayClient,
                    autoTraderDirectClient,
                    securityBook,
                    null,
                    null,
                    libOnly: true);
                ServiceLocator.Instance.AddService(omsCore);

                MacroManager macroManager = new();
                ServiceLocator.Instance.AddService(macroManager);

                EmaConfig emaConfig = new();
                ServiceLocator.Instance.AddService(emaConfig);

                if (OmsCore.DominatorClient != null)
                {
                    OmsCore.DominatorClient.CommandRequestEvent += DominatorClient_CommandRequestEvent;
                }

                if (OmsCore.Config.ConnectClientsOnStartupV2)
                {
                    _ = Task.Run(ConnectClients);
                }

                RegisterComServer();

                RegisterPython();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(AutoOpen));
            }
        }

        private void ConnectClients()
        {
            _ = OmsCore.UpdateManager.StartAsync();
            _ = OmsCore.FullEmaClient.StartAsync();
            _ = OmsCore.InterpolatorClient.StartAsync();
            _ = OmsCore.TheosClient.StartAsync();
            _ = OmsCore.HubTronClient.StartAsync();
            _ = OmsCore.AutoTraderClient.StartAsync();
            _ = OmsCore.HerculesClientWrapper.StartAsync();
        }

        public async void AutoClose()
        {
            try
            {
                await ShutdownPython();
                await AppHost!.StopAsync();
                UnregisterComServer();
                if (OmsCore.DominatorClient != null)
                {
                    _ = OmsCore.DominatorClient.StopAsync();
                }
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(AutoClose));
            }
        }

        private static string HandleUnhandledException(object ex)
        {
            _log?.LogError((Exception)ex, "Unhandled exception occured.");
            return "!!! EXCEPTION: " + ex;
        }

        private void LockUpdateDirectory()
        {
            try
            {
                string lockFilePath = GetLockFilePath();

                if (lockFilePath == null)
                {
                    return;
                }

                if (!File.Exists(lockFilePath))
                {
                    _updateDirLock = File.Create(lockFilePath);
                }

                _updateDirLock = new FileStream(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(LockUpdateDirectory));
            }
        }

        private string GetLockFilePath()
        {
            string executingAssemblyLocation = Path.GetDirectoryName(ExcelDnaUtil.XllPath);
            string lockFilePath = Path.Combine(executingAssemblyLocation, DIR_LOCK_FILE);
            return lockFilePath;
        }

        private static void RegisterComServer()
        {
            try
            {
                ComServer.DllRegisterServer();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(RegisterComServer));
            }
        }

        private void RegisterPython()
        {
            try
            {
                string pythonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
                if (Directory.Exists(pythonPath))
                {
                    string[] dirs = Directory.GetDirectories(pythonPath);
                    string version = "Python310";

                    foreach (string dir in dirs.OrderByDescending(x => x))
                    {
                        try
                        {
                            string fullPath = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
                            string name = Path.GetFileName(fullPath);
                            if (name.StartsWith("Python"))
                            {
                                version = name;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
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
                    else
                    {
                        _log?.LogError(nameof(RegisterPython) + " Python not found");
                    }
                }
                else
                {
                    _log?.LogError(nameof(RegisterPython) + " Python not found");
                }
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(RegisterPython));
            }
        }

        private static async Task ShutdownPython()
        {
            try
            {
                await Task.Run(PythonEngine.Shutdown);
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(ShutdownPython));
            }
        }

        private static void UnregisterComServer()
        {
            try
            {
                ComServer.DllUnregisterServer();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(UnregisterComServer));
            }
        }

        private void InstallIntelliSenseServer()
        {
            try
            {
                IntelliSenseServer.Install();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(InstallIntelliSenseServer));
            }
        }

        private void UninstallIntelliSenseServer()
        {
            try
            {
                IntelliSenseServer.Uninstall();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(UninstallIntelliSenseServer));
            }
        }

        private void DominatorClient_CommandRequestEvent(Comms.Models.Data.Oms.DomsManager.Command command, string[] args, DateTime timestamp)
        {
            try
            {
                Application xlApp = (Application)ExcelDnaUtil.Application;
                xlApp.Quit();
                AutoClose();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, nameof(DominatorClient_CommandRequestEvent));
            }
        }
    }
}
