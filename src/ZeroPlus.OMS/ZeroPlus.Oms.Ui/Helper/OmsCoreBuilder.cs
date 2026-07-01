using System.Collections.Generic;
using ZeroPlus.Cob.Client.Config.Interfaces;
using ZeroPlus.Cob.Client.Interfaces;
using ZeroPlus.Databento.Client.Config.Interfaces;
using ZeroPlus.Databento.Client.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanFeedRunner.Client.Interfaces;
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;
using ZeroPlus.EdgeScanner.Client.Interfaces;
using ZeroPlus.Ema.Client.Config.Interfaces;
using ZeroPlus.Ema.Client.Interfaces;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.IbGateway.Client.Config.Interfaces;
using ZeroPlus.IbGateway.Client.Interfaces;
using ZeroPlus.Interpolator.Client.Config.Interfaces;
using ZeroPlus.Interpolator.Client.Interfaces;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Raptor.Client.Config;
using ZeroPlus.Raptor.Client.Config.Interfaces;
using ZeroPlus.Raptor.Client.Interfaces;
using ZeroPlus.SymbolMap.Client.Config.Interfaces;
using ZeroPlus.SymbolMap.Client.Interfaces;
using ZeroPlus.Telemetry.Client.Config.Interfaces;
using ZeroPlus.Telemetry.Client.Interfaces;
using ZeroPlus.Theos.Client.Config.Interfaces;
using ZeroPlus.Theos.Client.Interfaces;
using ZeroPlus.HubTron.Client.Config.Interfaces;
using ZeroPlus.HubTron.Client.Interfaces;
using ZeroPlus.Oms.Factories;
using ZeroPlus.Pricing.Client.Config.Interfaces;
using ZeroPlus.Pricing.Client.Interfaces;
using ZeroPlus.LiveVol.Client.Config.Interfaces;
using ZeroPlus.LiveVol.Client.Interfaces;
using ZeroPlus.Trades.Client.Config.Interfaces;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class OmsCoreBuilder
    {
        private readonly OmsCore _omsCore;

        public OmsCore Build() => _omsCore;

        public OmsCoreBuilder(ISecurityBook securityBook,
                            
                            IHerculesClientConfig herculesClientConfig,
                            IHerculesClientConfigParser herculesClientConfigParser,
                            IHerculesClient herculesClient,
                            HerculesClient herculesClientWrapper,
                            
                            IRaptorClientConfig raptorClientConfig,
                            IAbstractFactory<IRaptorClient> raptorClientFactory,
                            IRaptorClientConfigParser raptorClientConfigParser,
                            UpdateManager updateManager,
                            
                            IEdgeScannerClientConfig memCacheClientConfig,
                            IEdgeScannerClientConfigParser memCacheClientConfigParser,
                            IEdgeScannerClient edgeScannerClientLib,
                            EdgeScannerClient edgeScannerClient,

                            IEdgeScanFeedRunnerClientConfig edgeScanFeedRunnerClientConfig,
                            IEdgeScanFeedRunnerClientConfigParser edgeScanFeedRunnerClientConfigParser,
                            IEdgeScanFeedRunnerClient edgeScanFeedRunnerClientLib,
                            EdgeScanFeedRunnerClient edgeScanFeedRunnerClient,
                            
                            ISymbolMapClientConfig symbolMapClientConfig,
                            ISymbolMapClientConfigParser symbolMapClientConfigParser,
                            ISymbolMapClient symbolMapClientLib,
                            SymbolMapClient symbolMapClient,

                            ITelemetryClientConfig telemetryClientConfig,
                            ITelemetryClientConfigParser telemetryClientConfigParser,
                            ITelemetryClient telemetryClientLib,
                            TelemetryClient telemetryClient,

                            IEmaClient emaClientLib,
                            IEmaClientConfig emaClientConfig,
                            IEmaClientConfigParser emaClientConfigParser,
                            FullEmaClient fullEmaClient,

                            EMAServer.Client.EMAServerClient emaServerLib,
                            EMAServer.Client.IConfig emaServerClientConfig,
                            EmaServerConfig.EmaServerConfigParser emaServerConfigParser,                            
                            EmaServerClientModel emaServerClient,

                            IInterpolatorClient interpolatorClientLib,
                            IInterpolatorClientConfig interpolatorClientConfig,
                            IInterpolatorClientConfigParser interpolatorClientConfigParser,
                            InterpolatorClient interpolatorClient,

                            ITheosClient theosClientLib,
                            ITheosClientConfig theosClientConfig,
                            ITheosClientConfigParser theosClientConfigParser,
                            TheosClient theosClient,

                            IHubTronClient hubTronClientLib,
                            IHubTronClientConfig hubTronClientConfig,
                            IHubTronClientConfigParser hubTronClientConfigParser,
                            HubTronClient hubTronClient,

                            IIbGatewayClientConfig ibGatewayClientConfig,
                            IIbGatewayClientConfigParser ibGatewayClientConfigParser,
                            IIbGatewayClient ibGatewayClientLib,
                            IbGatewayClient ibGatewayClient,
                            
                            IDatabentoClientConfig databentoClientConfig,
                            IDatabentoClientConfigParser databentoClientConfigParser,
                            IDatabentoClient databentoClientLib,
                            DatabentoClient databentoClient,
                            
                            ICobClientConfig cobClientConfig,
                            ICobClientConfigParser cobClientConfigParser,
                            ICobClient cobClientLib,
                            CobClient cobClient,
                            
                            IPricingClientConfig pricingClientConfig,
                            IPricingClientConfigParser pricingClientConfigParser,
                            IPricingClient pricingClientLib,
                            PricingClient pricingClient,
                            
                            IAutoTraderClient orderGatewayClientLib,
                            IAutoTraderClientConfig orderGatewayClientConfig,
                            IAutoTraderClientConfigParser orderGatewayClientConfigParser,
                            AutoTraderClient orderGatewayClient,
                            
                            AutoTraderClientFactory autoTraderClientFactory,
                            AutoTraderDirectClient autoTraderDirectClient,
                            AutoTraderDirectClientConfig autoTraderDirectClientConfig,
                            AutoTraderDirectClientConfigParser autoTraderDirectClientConfigParser,

                            ILiveVolClientConfig liveVolDataClientConfig,
                            ILiveVolClientConfigParser liveVolDataClientConfigParser,
                            ILiveVolClient liveVolClientLib,
                            LiveVolDataClient liveVolDataClient,

                            ITradesClientConfig tradesClientConfig,
                            ITradesClientConfigParser tradesClientConfigParser,
                            TradesClient tradesClient,

                            PerformanceModeManager performanceModeManager)
        {
            OmsConfig config = OmsConfig.LoadConfig(
                herculesClientConfig,
                herculesClientConfigParser,
                raptorClientConfig,
                raptorClientConfigParser,
                memCacheClientConfig,
                memCacheClientConfigParser,
                edgeScanFeedRunnerClientConfig,
                edgeScanFeedRunnerClientConfigParser,
                symbolMapClientConfig,
                symbolMapClientConfigParser,
                emaClientConfig,
                emaClientConfigParser,     
                emaServerClientConfig,
                emaServerConfigParser,
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
                liveVolDataClientConfig,
                liveVolDataClientConfigParser,
                telemetryClientConfig,
                telemetryClientConfigParser,
                tradesClientConfig,
                tradesClientConfigParser);

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

            config.AppId = "ZeroPlus OMS App";
            updateManager.Initialize(herculesClient);
            updateManager.Initialize(raptorClients);
            performanceModeManager.Initialize(raptorClients);
            updateManager.Initialize(edgeScannerClientLib);
            updateManager.Initialize(emaClientLib);
            performanceModeManager.Initialize(emaClientLib);
            updateManager.Initialize(interpolatorClientLib);
            updateManager.Initialize(theosClientLib);
            updateManager.Initialize(ibGatewayClientLib);
            updateManager.Initialize(databentoClientLib);
            updateManager.Initialize(hubTronClientLib);
            updateManager.Initialize(cobClientLib);
            edgeScannerClient.Initialize(edgeScannerClientLib);
            edgeScanFeedRunnerClient.Initialize(edgeScanFeedRunnerClientLib);
            symbolMapClient.Initialize(symbolMapClientLib);
            telemetryClient.Initialize(telemetryClientLib);
            fullEmaClient.Initialize(emaClientLib);
            emaServerClient.Initialize(emaServerLib);
            interpolatorClient.Initialize(interpolatorClientLib);
            theosClient.Initialize(theosClientLib);
            hubTronClient.Initialize(hubTronClientLib);
            ibGatewayClient.Initialize(ibGatewayClientLib);
            databentoClient.Initialize(databentoClientLib);
            performanceModeManager.Initialize(databentoClientLib);
            cobClient.Initialize(cobClientLib);
            pricingClient.Initialize(pricingClientLib);
            herculesClientWrapper.Initialize(herculesClient);
            orderGatewayClient.Initialize(orderGatewayClientLib);
            autoTraderDirectClient.Initialize(autoTraderClientFactory.CreateAutoTraderClient(autoTraderDirectClientConfig));
            liveVolDataClient.Initialize(liveVolClientLib);

            config.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(OmsConfig.PerformanceModeEnabled))
                    performanceModeManager.OnPerformanceModeChanged(config.PerformanceModeEnabled);
            };

            _omsCore = new OmsCore(config,
                                   herculesClientConfig,
                                   herculesClient,
                                   performanceModeManager,
                                   updateManager,
                                   edgeScannerClient,
                                   edgeScanFeedRunnerClient,
                                   symbolMapClient,
                                   telemetryClient,
                                   herculesClientWrapper,
                                   fullEmaClient,
                                   emaServerClient,
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
                                   liveVolDataClient,
                                   tradesClient);

            performanceModeManager.Initialize(_omsCore.QuoteClient);
        }
    }
}
