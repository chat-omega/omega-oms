using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Generators;
using ZeroPlus.Oms.Managers;

namespace ZeroPlus.Oms
{
    public interface IOmsCore
    {
        string GetSavedUser();
        void RequestSaveWorkspace();
        void SaveUser();
        void SetupOrderClients();

        BasketManager BasketManager { get; }
        BasketManagerClient BasketManagerClient { get; }
        DerivedValueGenerator DerivedValueGenerator { get; }
        DominatorClient DominatorClient { get; }
        DominatorsManager DominatorsManager { get; }
        FullEmaClient FullEmaClient { get; }
        EmaServerClientModel EmaServerClientModel { get; }
        InterpolatorClient InterpolatorClient { get; }
        TheosClient TheosClient { get; }
        HubTronClient HubTronClient { get; }
        GatewayClient GatewayClient { get; }
        GreekClient GreekClient { get; }
        IHerculesClient HerculesClient { get; }
        IHerculesClientConfig HerculesClientConfig { get; }
        HerculesClient HerculesClientWrapper { get; }
        EdgeScannerClient EdgeScannerClient { get; }
        EdgeScanFeedRunnerClient EdgeScanFeedRunnerClient { get; }
        SymbolMapClient SymbolMapClient { get; }
        OrderClient OrderClient { get; }
        AutoTraderClient AutoTraderClient { get; }
        AutoTraderDirectClient AutoTraderDirectClient { get; }
        QuoteClient QuoteClient { get; }
        TradesClient TradesClient { get; }
        UpdateManager UpdateManager { get; }
        PerformanceModeManager PerformanceModeManager { get; }
        LiveVolDataClient LiveVolDataClient { get; }
        Update.Updater AppUpdateManager { get; }
        ISecurityBook SecurityBook { get; }
        User User { get; set; }
        event SaveWorkspaceRequestEventHandler SaveWorkspaceRequestEvent;
    }
}
