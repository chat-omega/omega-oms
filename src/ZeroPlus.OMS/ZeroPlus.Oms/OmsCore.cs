using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Generators;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Services;

namespace ZeroPlus.Oms
{
    public delegate void SaveWorkspaceRequestEventHandler();

    public class OmsCore : IOmsCore
    {
        public static readonly Dictionary<string, string[]> RouteToExchangeLookup = new()
        {
            {"ISE", ["Y", "XISX"] },
            {"CBOE",["W", "XCBO"]},
            {"PHLX",["X", "XPHO"]},
            {"ARCA",["N", "ARCO"]},
            {"BOX",["B", "XBOX"]},
            {"MIAX",["M?","XMIO"]},
            {"C2",["C?", "C2OX"]},
            {"EDGX",["E?", "EDGO"]},
            {"AMEX",["1","XAMEX"]},
            {"EMLD",["EM?", "EMLD"]},
            {"MCRY",["MC?", "XMCRY"]},
        };

        public event SaveWorkspaceRequestEventHandler SaveWorkspaceRequestEvent;

        private readonly ILogger _log;
        private string _savedUserName;
        public static OmsConfig Config { get; private set; }
        public User User { get; set; }
        public Update.Updater AppUpdateManager { get; }
        public GatewayClient GatewayClient { get; }
        public virtual QuoteClient QuoteClient { get; }
        public TradesClient TradesClient { get; }
        public GreekClient GreekClient { get; }
        public UpdateManager UpdateManager { get; }
        public PerformanceModeManager PerformanceModeManager { get; }
        public EdgeScannerClient EdgeScannerClient { get; }
        public EdgeScanFeedRunnerClient EdgeScanFeedRunnerClient { get; }
        public SymbolMapClient SymbolMapClient { get; }
        public TelemetryClient TelemetryClient { get; }
        public FullEmaClient FullEmaClient { get; }
        public EmaServerClientModel EmaServerClientModel { get; }
        public InterpolatorClient InterpolatorClient { get; }
        public TheosClient TheosClient { get; }
        public HubTronClient HubTronClient { get; }
        public IbGatewayClient IbGatewayClient { get; }
        public DatabentoClient DatabentoClient { get; }
        public CobClient CobClient { get; }
        public PricingClient PricingClient { get; }
        public AutoTraderClient AutoTraderClient { get; }
        public AutoTraderDirectClient AutoTraderDirectClient { get; }
        public HerculesClient HerculesClientWrapper { get; }
        public IHerculesClientConfig HerculesClientConfig { get; }
        public IHerculesClient HerculesClient { get; }
        public DominatorClient DominatorClient { get; }
        public DominatorsManager DominatorsManager { get; }
        public BasketManagerClient BasketManagerClient { get; }
        public BasketManager BasketManager { get; }
        public DerivedValueGenerator DerivedValueGenerator { get; }
        public OrderClient OrderClient { get; private set; }
        public ISecurityBook SecurityBook { get; }
        public LiveVolDataClient LiveVolDataClient { get; }
        public OmsOrderLifecycleService OrderLifecycleService { get; private set; }
        public AutoPermTelemetryService AutoPermTelemetryService { get; private set; }
        public QuoteSourceScheduler QuoteSourceScheduler { get; private set; }

        public OmsCore(OmsConfig config,
            IHerculesClientConfig herculesClientConfig,
            IHerculesClient herculesClient,
            PerformanceModeManager performanceModeManager,
            UpdateManager updateManager,
            EdgeScannerClient edgeScannerClient,
            EdgeScanFeedRunnerClient edgeScanFeedRunnerClient,
            SymbolMapClient symbolMapClient,
            TelemetryClient telemetryClient,
            HerculesClient herculesClientWrapper,
            FullEmaClient fullEmaClient,
            EmaServerClientModel daEmaClient,
            InterpolatorClient interpolatorClient,
            TheosClient theosClient,
            HubTronClient hubTronClient,
            IbGatewayClient ibGatewayClient,
            DatabentoClient databentoClient,
            CobClient cobClient,
            PricingClient pricingClient,
            AutoTraderClient orderGatewayClient,
            AutoTraderDirectClient autoTraderDirectClient,
            ISecurityBook securityBook,
            LiveVolDataClient liveVolDataClient,
            TradesClient tradesClient,
            bool libOnly = false)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            Config = config;
            LogHelper.SetupLoger(Config);
            _log = LogManager.GetCurrentClassLogger();
            _log.Info("Core staring. " + version);

            AppUpdateManager = new(Config);
            LoadSavedUser();

            _log.Info($"{nameof(OmsCore)} -> Starting Clients. Loaded Config: {Config}");
            GatewayClient = new GatewayClient(Config, this);
            QuoteClient = new QuoteClient(Config, this);
            GreekClient = new GreekClient(Config, this);
            if (Config.DominatorClientEnabled)
            {
                DominatorClient = new DominatorClient(Config, this);
            }
            BasketManagerClient = new BasketManagerClient(Config, this);
            HerculesClientConfig = herculesClientConfig;
            HerculesClient = herculesClient;
            HerculesClientWrapper = herculesClientWrapper;
            SecurityBook = securityBook;
            if (!libOnly)
            {
                HerculesClient.ClientConnected += OnHerculesConnected;
            }
            else
            {
                HerculesClient.ClientConnected += LibHerculesConnected;
            }

            PerformanceModeManager = performanceModeManager;
            UpdateManager = updateManager;
            EdgeScannerClient = edgeScannerClient;
            EdgeScanFeedRunnerClient = edgeScanFeedRunnerClient;
            SymbolMapClient = symbolMapClient;
            TelemetryClient = telemetryClient;
            FullEmaClient = fullEmaClient;
            LiveVolDataClient = liveVolDataClient;

            if (daEmaClient != null)
            {
                daEmaClient.OmsCore = this;
                EmaServerClientModel = daEmaClient;
            }

            InterpolatorClient = interpolatorClient;
            TheosClient = theosClient;
            HubTronClient = hubTronClient;
            IbGatewayClient = ibGatewayClient;
            DatabentoClient = databentoClient;
            CobClient = cobClient;
            PricingClient = pricingClient;
            AutoTraderClient = orderGatewayClient;
            AutoTraderDirectClient = autoTraderDirectClient;
            TradesClient = tradesClient;
            DerivedValueGenerator = new DerivedValueGenerator();

            DominatorsManager = new DominatorsManager(Config, this);
            BasketManager = new BasketManager(Config);
            QuoteSourceScheduler = new QuoteSourceScheduler(Config, this);
            OrderLifecycleService = new OmsOrderLifecycleService(telemetryClient.Client, Config.TelemetryBoxId, Config.TelemetryProgId, Config.TelemetryInstanceId);
            AutoPermTelemetryService = new AutoPermTelemetryService(telemetryClient.Client, Config.TelemetryBoxId, Config.TelemetryProgId, Config.TelemetryInstanceId);

            _ = GatewayClient.StartAsync();
        }

        private void LibHerculesConnected()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            HerculesClient.RegisterClient("Excel", "ZeroPlus OMS AddIn", version!, Dns.GetHostName());
            HerculesClient.SubscribePnl(PositionSubscriptionMode.Full);
        }

        private void OnHerculesConnected()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            HerculesClient.RegisterClient(User?.Username, "ZeroPlus OMS App", version!, Dns.GetHostName());
            if (HerculesClientConfig.TransactionSubscriptionMode != TransactionSubscriptionMode.Off)
            {
                HerculesClient.SubscribeTransaction(User?.Accounts?.ToList(), 
                    HerculesClientConfig.TransactionSubscriptionMode is TransactionSubscriptionMode.Fills or TransactionSubscriptionMode.OwnAndFills,
                    HerculesClientConfig.TransactionSubscriptionMode == TransactionSubscriptionMode.OwnAndFills);
            }
            if (HerculesClientConfig.SubscribePositionsOnConnect)
            {
                HerculesClient.SubscribePnl(OmsCore.Config.PositionSubscriptionMode);
            }
        }

        public void SetupOrderClients()
        {
            OrderClient = new OrderClient(Config, User, this);
        }

        public string GetSavedUser()
        {
            return _savedUserName;
        }

        public void SaveUser()
        {
            try
            {
                string path = OmsConfig.GetUserExportPath();
                File.WriteAllText(path, User.Username);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SaveUser)}");
            }
        }

        public static string CalculateHash(SecureString securePasswordString, string saltString)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePasswordString);
                byte[] passwordBytes = Encoding.UTF8.GetBytes(Marshal.PtrToStringUni(unmanagedString) ?? string.Empty);
                byte[] saltBytes = Encoding.UTF8.GetBytes(saltString);
                byte[] passwordPlusSaltBytes = new byte[passwordBytes.Length + saltBytes.Length];
                Buffer.BlockCopy(passwordBytes, 0, passwordPlusSaltBytes, 0, passwordBytes.Length);
                Buffer.BlockCopy(saltBytes, 0, passwordPlusSaltBytes, passwordBytes.Length, saltBytes.Length);
                HashAlgorithm algorithm = SHA256.Create();
                return Convert.ToBase64String(algorithm.ComputeHash(passwordPlusSaltBytes));
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        public void RequestSaveWorkspace()
        {
            if (SaveWorkspaceRequestEvent != null)
            {
                IEnumerable<SaveWorkspaceRequestEventHandler> eventHandlers = SaveWorkspaceRequestEvent.GetInvocationList().Cast<SaveWorkspaceRequestEventHandler>();
                foreach (SaveWorkspaceRequestEventHandler eventHandler in eventHandlers)
                {
                    try
                    {
                        eventHandler.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(RequestSaveWorkspace));
                    }
                }
            }
        }

        private void LoadSavedUser()
        {
            try
            {
                string path = OmsConfig.GetUserExportPath();

                if (File.Exists(path))
                {
                    _savedUserName = File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(LoadSavedUser)}");
            }
        }
    }
}
