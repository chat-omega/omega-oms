using Generated;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using Org.SbeTool.Sbe.Dll;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml;
using ZeroPlus.Models.Data.Auth;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.Databento;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Data.Subscription;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings.Generic;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;
using BaseStrategy = ZeroPlus.Models.Data.Enums.BaseStrategy;
using OrderSource = ZeroPlus.Models.Data.Enums.OrderSource;
using OrderStatus = ZeroPlus.Models.Data.Enums.OrderStatus;
using PortfolioType = ZeroPlus.Models.Data.Enums.PortfolioType;
using PositionEffect = ZeroPlus.Models.Data.Enums.PositionEffect;
using PositionType = ZeroPlus.Models.Data.Enums.PositionType;
using PutCall = Generated.PutCall;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Models.Protocols.Sbe
{
    public class SbeMessageDecoder : ISbeMessageDecoder
    {
        public const short SchemaVersion = 0;
        private readonly DirectBuffer _directBuffer;
        private readonly MessageHeader _messageHeader = new();
        private readonly IUpdateManager? _updateManager;
        private readonly IOrderFactory? _orderFactory;
        private readonly IStatsProcessor? _statsProcessor;
        private readonly IPortfolioManager? _portfolioManager;
        private readonly ILogger<SbeMessageDecoder> _logger;

        public event ClientAuthenticationHandler? ClientAuthentication;
        public event ClientRegistrationHandler? ClientRegistration;
        public event LatencyMeterEventHandler? LatencyMeterEvent;
        public event StateSnapshotHandler? StateSnapshot;
        public event SubscribeMarketDataRequestHandler? SubscribeMarketDataRequest;
        public event UnsubscribeMarketDataRequestHandler? UnsubscribeMarketDataRequest;
        public event SubscribeTransactionRequestHandler? SubscribeTransactionRequest;
        public event UnsubscribeTransactionRequestHandler? UnsubscribeTransactionRequest;
        public event SubscribePnlRequestHandler? SubscribePnlRequest;
        public event UnsubscribePnlRequestHandler? UnsubscribePnlRequest;
        public event RequestTransactionsFromArchiveHandler? RequestTransactionsFromArchive;
        public event RequestPnlFromArchiveHandler? RequestPnlFromArchive;
        public event AuditTrailRequestHandler? AuditTrailRequest;
        public event AuditTrailResponseHandler? AuditTrailResponse;
        public event OrderDetailsRequestHandler? OrderDetailsRequest;
        public event HanweckUpdatesWithMatchingTimestampsRequestHandler? HanweckUpdatesWithMatchingTimestampsRequest;
        public event HanweckUpdatesWithMatchingTimestampsResponseHandler? HanweckUpdatesWithMatchingTimestampsResponse;
        public event SymbolMapRequestHandler? SymbolMapRequest;
        public event SymbolMapResponseHandler? SymbolMapResponse;
        public event SymbolMapRequestHandler? RootSymbolMapRequest;
        public event OptionSnapshotRequestHandler? OptionSnapshotRequest;
        public event OptionSnapshotResponseHandler? OptionSnapshotResponse;
        public event MarketCrossScanRequestHandler? MarketCrossScanRequest;
        public event MarketCrossScanResponseHandler? MarketCrossScanResponse;
        public event BestEdgeToTheoRequestHandler? BestEdgeToTheoRequest;
        public event BestEdgeToTheoResponseHandler? BestEdgeToTheoResponse;
        public event SymbolTradeRequestHandler? SymbolTradeRequest;
        public event SymbolsTradeRequestHandler? SymbolsTradeRequest;
        public event SymbolTradeResponseHandler? SymbolTradeResponse;
        public event SymbolsTradeResponseHandler? SymbolsTradeResponse;
        public event SingleOrderRequestHandler? SingleOrderRequest;
        public event PairOrderRequestHandler? PairOrderRequest;
        public event BasketOrderRequestHandler? BasketOrderRequest;
        public event AccountRequestHandler? AccountRequest;
        public event AccountResponseHandler? AccountResponse;
        public event ResetBaseLineRequestHandler? ResetBaseLineRequest;
        public event AutoTraderConfigJsonHandler? AutoTraderConfigJson;
        public event AutoTraderConfigHandler? AutoTraderConfig;
        public event SendOrderHandler? SendOrder;
        public event IbQuoteUpdateHandler? IbQuoteUpdate;
        public event EdgeToTheoUpdateHandler? EdgeToTheoUpdate;
        public event CancelOrderRequestHandler? CancelOrderRequest;
        public event ModifyOrderRequestHandler? ModifyOrderRequest;
        public event TagOrderHander? TagOrder;
        public event SymbolEdgeMapRequestHander? SymbolEdgeMapRequest;
        public event SymbolEdgeMapResponseHander? SymbolEdgeMapResponse;
        public event MultiplePortfolioAddedHandler? MultiplePortfolioAdded;
        public event BarRequestHandler? BarRequest;
        public event BarResponseHandler? BarResponse;
        public event AlertMessageHandler? AlertMessage;
        public event SymbolStrikeRangeRequestHandler? SymbolStrikeRangeRequest;
        public event SymbolStrikeRangeResponseHandler? SymbolStrikeRangeResponse;
        public event PermEdgeToTheoMappingHandler? PermEdgeToTheoMapping;
        public event EdgeScanFeedServerRunnerRequestHandler? EdgeScanFeedServerRunnerRequest;
        public event EdgeScanFeedServerRunnerUnregisterHandler? EdgeScanFeedServerRunnerUnregister;
        public event SpreadGeneratorRequestHandler? SpreadGeneratorRequest;
        public event SpreadGeneratorResultsHandler? SpreadGeneratorResults;
        public event SymbolsRequestHandler? SymbolsRequest;
        public event SymbolsResultHandler? SymbolsResponse;
        public event OptionChainRequestHandler? OptionChainRequest;
        public event OptionChainResponseHandler? OptionChainResponse;
        public event FirmOrderAndTradeSummaryHandler? FirmOrderAndTradeSummaryReceived;
        public event DataRequestMessageHandler? DataRequestMessage;
        public event HistoricHighestBidLowestAskRequestHandler? HistoricHighestBidLowestAskRequest;
        public event HistoricHighestBidLowestAskResponseHandler? HistoricHighestBidLowestAskResponse;
        public event PositionsRequestHandler? PositionsRequest;
        public event TheoToMarketSpreadUpdateHandler? TheoToMarketSpreadUpdate;
        public event MatrixSyntheticSpreadHandler? MatrixSyntheticSpread;
        public event MatrixSeekerSpreadHandler? MatrixSeekerSpread;
        public event MatrixSeekerHandler? MatrixSeeker;
        public event MatrixScrapeHandler? MatrixScrape;
        public event ExecutionTransactionHandler? ExecutionTransaction;
        public event OrderTagMessageHandler? OrderTagMessage;
        public event ModifySmartOrderRequestHandler? ModifySmartOrderRequest;
        public event ModeledTheoUpdateHandler? ModeledTheoUpdate;
        public event SpreadBookQuoteUpdateHandler? SpreadBookQuoteUpdate;
        public event SpreadExchOrderUpdateHandler? SpreadExchOrderUpdate;
        public event SpreadPrintUpdateHandler? SpreadPrintUpdate;
        public event AuctionPrintUpdateHandler? AuctionPrintUpdate;
        public event CobTradeRequestHandler? CobTradeRequest;
        public event CancelDataRequestHandler? CancelDataRequest;
        public event CobTradeResponseHandler? CobTradeResponse;
        public event ModelDescriptionUpdateHandler? ModelDescriptionUpdate;
        public event CancelTokenMessageHandler? CancelTokenMessage;
        public event ImpliedQuoteUpdateHandler? ImpliedQuoteUpdate;
        public event GetClosestOptionRequestHandler? GetClosestOptionRequest;
        public event GetClosestOptionResponseHandler? GetClosestOptionResponse;
        public event NextOptionPermsRequestHandler? NextOptionPermsRequest;
        public event NextOptionPermsResponseHandler? NextOptionPermsResponse;
        public event NextSpreadPermsRequestHandler? NextSpreadPermsRequest;
        public event NextSpreadPermsResponseHandler? NextSpreadPermsResponse;
        public event JsonRequestHandler? JsonRequest;
        public event JsonResponseHandler? JsonResponse;
        public event RiskCheckResultHandler? RiskCheckResult;
        public event OrderRiskRequestHandler? OrderRiskRequest;
        public event CancelRiskRequestHandler? CancelRiskRequest;
        public event CancelReplaceRiskRequestHandler? CancelReplaceRiskRequest;
        public event OrderUpdateHandler? OrderUpdate;
        public event OrderInfoUpdateHandler? OrderInfoUpdate;
        public event OrderUpdateValueHandler? OrderUpdateValue;
        public event AutomationStateChangedHandler? AutomationStateChanged;
        public event SubmissionSummaryUpdateHandler? SubmissionSummaryUpdate;
        public event PerformanceModeRequestHandler? PerformanceModeRequest;
        public event PricingRequestHandler? PricingRequest;
        public event PricingResponseHandler? PricingResponse;
        public event TradesRequestHandler? TradesRequest;
        public event TradesResponseHandler? TradesResponse;
        public event AddRemoveMultipleTradesRequestHandler? AddRemoveMultipleTradesRequest;
        public event TheosBatchUpdatedHandler? TheosBatchUpdated;
        public event OpenSpreadExchOrderHandler? OpenSpreadExchOrder;
        public event RemoveSpreadExchOrderHandler? RemoveSpreadExchOrder;
        public event VolSurfaceRequestHandler? VolSurfaceRequest;
        public event VolSurfaceResponseHandler? VolSurfaceResponse;
        public event HerculesEchoMessageHandler? OnHerculesEchoMessage;
        public event HerculesEchoRequestMessageHandler? OnHerculesEchoRequestMessage;
        public event LiveVolDataRequestHandler? LiveVolDataRequest;
        public event LiveVolDataResponseHandler? LiveVolDataResponse;
        public event SymbolIndexMappingHandler? SymbolIndexMapping;
        public event RbboUpdateHandler? RbboUpdate;
        public event RegisterForeignUpdateRouteHandler? RegisterForeignUpdateRoute;
        public event UnregisterForeignUpdateRouteHandler? UnregisterForeignUpdateRoute;
        public event ReplaceForeignUpdateRoutesHandler? ReplaceForeignUpdateRoutes;
        public event ForeignUpdateRoutesResponseHandler? ForeignUpdateRoutesResponse;
        public event MassCancelRequestHandler? MassCancelRequest;
        public event OpraDatabaseRequestTradesMessageHandler? OpraDatabaseRequestTrades;
        public event OpraDatabaseResponseTradesMessageHandler? OpraDatabaseTradesResponse;
        public event EdgeScanFeedRunnerStartRequestHandler? EdgeScanFeedRunnerStartRequest;
        public event EdgeScanFeedRunnerStopRequestHandler? EdgeScanFeedRunnerStopRequest;
        public event EdgeScanFeedRunnerChangedHandler? EdgeScanFeedRunnerChanged;
        public event TradeSlimUpdateHandler? TradeSlimUpdate;

        // Auth Server Events
        public event AuthLoginRequestHandler? AuthLoginRequest;
        public event AuthLoginResponseHandler? AuthLoginResponse;
        public event AuthUpdatePasswordRequestHandler? AuthUpdatePasswordRequest;
        public event AuthUpdatePasswordResponseHandler? AuthUpdatePasswordResponse;
        public event AuthGetUsersRequestHandler? AuthGetUsersRequest;
        public event AuthGetUsersResponseHandler? AuthGetUsersResponse;
        public event AuthGetConfigsRequestHandler? AuthGetConfigsRequest;
        public event AuthGetConfigsResponseHandler? AuthGetConfigsResponse;
        public event AuthDeleteConfigRequestHandler? AuthDeleteConfigRequest;
        public event AuthDeleteConfigResponseHandler? AuthDeleteConfigResponse;
        public event AuthConfigSaveHandler? AuthConfigSave;
        public event AuthConfigShareHandler? AuthConfigShare;
        public event AuthGetDomListInfosRequestHandler? AuthGetDomListInfosRequest;
        public event AuthGetDomListInfosResponseHandler? AuthGetDomListInfosResponse;
        public event AuthGetCommissionsRequestHandler? AuthGetCommissionsRequest;
        public event AuthGetCommissionsResponseHandler? AuthGetCommissionsResponse;
        public event SingleFieldUpdateHandler? SingleFieldUpdate;

        public RiskRequestHandler? RiskRequestHandler { private get; set; }
        public AutoTraderConfigRequestHandler? AutoTraderConfigRequestHandler { private get; set; }
        public GetTheoModelsHandler? GetTheoModelsHandler { private get; set; }
        public GetRbboUpdateModelHandler? GetRbboUpdateModelHandler { private get; set; }
        public GetOpenSpreadExchOrderHandler? GetOpenSpreadExchOrderHandler { private get; set; }


        private static readonly JsonSerializerSettings _edgeScanFeedSerializationSettings = new JsonSerializerSettings
        {
            Converters = { new InterfaceToConcreteConverter<IEdgeScanFeedTraderSettings, EdgeScanFeedTraderSettings>() }
        };

        private static readonly JsonSerializerSettings _spreadGeneratorConfigSerializationSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new InterfaceToConcreteConverter<ISpreadGeneratorIntFilter, GenericSpreadGeneratorIntFilter>(),
                new InterfaceToConcreteConverter<ISingleLegSpreadsGeneratorSettings, GenericSingleLegSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IVerticalSpreadsGeneratorSettings, GenericVerticalSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IRatioSpreadsGeneratorSettings, GenericRatioSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IRatioSpreadsGeneratorSettings, GenericRatioSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IRatioSpreadsGeneratorSettings, GenericRatioSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<ICalendarSpreadsGeneratorSettings, GenericCalendarSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IDiagonalSpreadsGeneratorSettings, GenericDiagonalSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IButterflySpreadsGeneratorSettings, GenericButterflySpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<ISkewedButterflySpreadsGeneratorSettings, GenericSkewedButterflySpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<ITreeSpreadsGeneratorSettings, GenericTreeSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<ICalendarButterflySpreadsGeneratorSettings, GenericCalendarButterflySpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IIronButterflySpreadsGeneratorSettings, GenericIronButterflySpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IIronGutFlySpreadsGeneratorSettings, GenericIronButterflySpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<ICondorSpreadsGeneratorSettings, GenericCondorSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IOneThreeThreeOneSpreadsGeneratorSettings, GenericOneThreeThreeOneSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IIronCondorSpreadsGeneratorSettings, GenericIronCondorSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IOneThreeTwoSpreadsGeneratorSettings, GenericOneThreeTwoSpreadsGeneratorSettings>(),
                new InterfaceToConcreteConverter<IBoxSpreadsGeneratorSettings, GenericBoxSpreadsGeneratorSettings>(),
            }
        };

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IOrderFactory orderFactory)
        {
            _logger = logger;
            _orderFactory = orderFactory;
            _directBuffer = directBufferPool.Get();
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IUpdateManager updateManager)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _updateManager = updateManager;
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IPortfolioManager portfolioManager)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _portfolioManager = portfolioManager;
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IOrderFactory orderFactory, IPortfolioManager portfolioManager)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _orderFactory = orderFactory;
            _portfolioManager = portfolioManager;
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IOrderFactory orderFactory, IPortfolioManager portfolioManager, IUpdateManager updateManager)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _orderFactory = orderFactory;
            _portfolioManager = portfolioManager;
            _updateManager = updateManager;
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IOrderFactory orderFactory, IPortfolioManager portfolioManager, IUpdateManager updateManager, IStatsProcessor statsProcessor)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _orderFactory = orderFactory;
            _portfolioManager = portfolioManager;
            _updateManager = updateManager;
            _statsProcessor = statsProcessor;
        }

        public SbeMessageDecoder(ILogger<SbeMessageDecoder> logger, ObjectPool<DirectBuffer> directBufferPool, IOrderFactory orderFactory, IPortfolioManager portfolioManager, IStatsProcessor statsProcessor)
        {
            _logger = logger;
            _directBuffer = directBufferPool.Get();
            _orderFactory = orderFactory;
            _portfolioManager = portfolioManager;
            _statsProcessor = statsProcessor;
        }

        public unsafe void Parse(byte[] buffer, int offset, int length)
        {
            int bufferOffset = 0;

            fixed (byte* pBuffer = &buffer[offset])
            {
                _directBuffer.Wrap(pBuffer, length);

                _messageHeader.Wrap(_directBuffer, bufferOffset, SchemaVersion);

                ushort templateId = _messageHeader.TemplateId;
                int actingBlockLength = _messageHeader.BlockLength;
                int actingVersion = _messageHeader.Version;

                bufferOffset += MessageHeader.Size;

                DecodeByTemplateId(_directBuffer, bufferOffset, actingBlockLength, actingVersion, templateId);
            }
        }

        public void Parse(byte[] message)
        {
            int bufferOffset = 0;

            _directBuffer.Wrap(message);

            _messageHeader.Wrap(_directBuffer, bufferOffset, SchemaVersion);

            ushort templateId = _messageHeader.TemplateId;
            int actingBlockLength = _messageHeader.BlockLength;
            int actingVersion = _messageHeader.Version;

            bufferOffset += MessageHeader.Size;

            DecodeByTemplateId(_directBuffer, bufferOffset, actingBlockLength, actingVersion, templateId);
        }

        private void DecodeByTemplateId(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion, ushort templateId)
        {
            switch (templateId)
            {
                case Generated.ClientAuthentication.TemplateId:
                    DecodeClientAuthentication(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ClientRegistration.TemplateId:
                    DecodeClientRegistration(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SubscribeMarketDataRequest.TemplateId:
                    DecodeSubscribeMarketDataRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.UnsubscribeMarketDataRequest.TemplateId:
                    DecodeUnsubscribeMarketDataRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SubscribeSpreadDataRequest.TemplateId:
                    DecodeSubscribeSpreadDataRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case UnsubscribeSpreadDataRequest.TemplateId:
                    DecodeUnsubscribeSpreadDataRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SubscribeTransactionRequest.TemplateId:
                    DecodeSubscribeTransactionRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.UnsubscribeTransactionRequest.TemplateId:
                    DecodeUnsubscribeTransactionRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SubscribePnlRequest.TemplateId:
                    DecodeSubscribePnlRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.UnsubscribePnlRequest.TemplateId:
                    DecodeUnsubscribePnlRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderAdded.TemplateId:
                    DecodeOrderAdded(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderRemoved.TemplateId:
                    DecodeOrderRemoved(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SendOrder.TemplateId:
                    DecodeSendOrder(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderUpdateValuesMessage.TemplateId:
                    DecodeOrderUpdateValuesMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MultipleOrderAdded.TemplateId:
                    DecodeMultipleOrderAdded(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderUpdated.TemplateId:
                    DecodeOrderUpdate(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderIndicatorsUpdate.TemplateId:
                    DecodeOrderIndicatorUpdate(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderTagUpdate.TemplateId:
                    DecodeOrderTagUpdate(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PortfolioAddedMessage.TemplateId:
                    DecodePortfolioAddedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PositionAddedMessage.TemplateId:
                    DecodePortfolioPositionAddedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PortfolioUpdateMessage.TemplateId:
                    DecodePortfolioUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PortfolioUpdateMessageSlim.TemplateId:
                    DecodePortfolioUpdateMessageSlim(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SecurityDecimalUpdate.TemplateId:
                    DecodeSecurityDecimalUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case DeltaAdjTheoDetailsUpdate.TemplateId:
                    DecodeDeltaAdjTheoDetailsUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case DeltaAdjustedTheoUpdateMessage.TemplateId:
                    DecodeDeltaAdjustedTheoUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MultiplePortfoliosAddedMessage.TemplateId:
                    DecodeMultiplePortfoliosAddedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RequestTransactionsFromArchive.TemplateId:
                    DecodeRequestTransactionsFromArchiveMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RequestPnlFromArchive.TemplateId:
                    DecodeRequestPnlFromArchiveMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case RequestAuditTrail.TemplateId:
                    DecodeRequestAuditTrailMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuditTrailResponse.TemplateId:
                    DecodeAuditTrailResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.HanweckUpdatesWithMatchingTimestampsRequest.TemplateId:
                    DecodeHanweckUpdatesWithMatchingTimestampsRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.HanweckUpdatesWithMatchingTimestampsResponse.TemplateId:
                    DecodeHanweckUpdatesWithMatchingTimestampsResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolMapRequestMessage.TemplateId:
                    DecodeSymbolMapRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolMapResponseMessage.TemplateId:
                    DecodeSymbolMapResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case RootSymbolMapRequestMessage.TemplateId:
                    DecodeRootSymbolMapRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case RootSymbolMapResponseMessage.TemplateId:
                    DecodeRootSymbolMapResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MultipleSecurityDecimalUpdateMessage.TemplateId:
                    DecodeMultipleSecurityDecimalUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case DoubleUpdate.TemplateId:
                    DecodeDoubleUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OptionSnapshotsRequest.TemplateId:
                    DecodeOptionSnapshotRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OptionSnapshotsResponse.TemplateId:
                    DecodeOptionSnapshotResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.MarketCrossScanRequest.TemplateId:
                    DecodeMarketCrossScanRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.MarketCrossScanResponse.TemplateId:
                    DecodeMarketCrossScanResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.BestEdgeToTheoRequest.TemplateId:
                    DecodeBestEdgeToTheoRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.BestEdgeToTheoResponse.TemplateId:
                    DecodeBestEdgeToTheoResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OptionSymbolMapResponseMessage.TemplateId:
                    DecodeOptionSymbolMapResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolTradeRequestMessage.TemplateId:
                    DecodeSymbolTradeRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolTradeResponseMessage.TemplateId:
                    DecodeSymbolTradeResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolsTradeRequestMessage.TemplateId:
                    DecodeSymbolsTradeRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolsTradeResponseMessage.TemplateId:
                    DecodeSymbolsTradeResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case EdgeScanFeedModelMessage.TemplateId:
                    DecodeEdgeScanFeedModelMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case TimeUpdateMessage.TemplateId:
                    DecodeTimeUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case TradeUpdateMessage.TemplateId:
                    DecodeTradeUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SecurityEmaUpdate.TemplateId:
                    DecodeSecurityEmaUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SingleOrderRequestMessage.TemplateId:
                    DecodeSingleOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PairOrderRequestMessage.TemplateId:
                    DecodePairOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case AccountRequestMessage.TemplateId:
                    DecodeAccountRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case AccountResponseMessage.TemplateId:
                    DecodeAccountResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderInfoUpdateMessage.TemplateId:
                    DecodeOrderInfoUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case BasketOrderRequestMessage.TemplateId:
                    DecodeBasketOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case ResetBaseLineRequestMessage.TemplateId:
                    DecodeResetBaseLineRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case DerivedValueUpdate.TemplateId:
                    DecodeDerivedValueUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case RequestOrderDetails.TemplateId:
                    DecodeRequestOrderDetailsMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case OrderDetailsResponse.TemplateId:
                    DecodeOrderDetailsResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case AutoTraderConfigMessage.TemplateId:
                    DecodeAutoTraderConfigMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolStatModelAddedMessage.TemplateId:
                    DecodeSymbolStatModelAddedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolStatModelUpdateMessage.TemplateId:
                    DecodeSymbolStatModelUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case TradeFeedMessage.TemplateId:
                    DecodeTradeFeedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case IbQuoteUpdateMessage.TemplateId:
                    DecodeIbQuoteUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case EdgeToTheoUpdateMessage.TemplateId:
                    DecodeEdgeToTheoUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadRiskModelUpdateMessage.TemplateId:
                    DecodeSpreadRiskModelUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SelfTradeWarningMessage.TemplateId:
                    DecodeSelfTradeWarningMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case CancelOrderRequestMessage.TemplateId:
                    DecodeCancelOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case TagOrderMessage.TemplateId:
                    DecodeTagOrderMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SymbolEdgeMapRequest.TemplateId:
                    DecodeSymbolEdgeMapRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SymbolEdgeMapResponse.TemplateId:
                    DecodeSymbolEdgeMapResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SendOrderMinimal.TemplateId:
                    DecodeSendOrderMinimal(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case BarRequestMessage.TemplateId:
                    DecodeBarRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case BarResponseMessage.TemplateId:
                    DecodeBarResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AlertMessage.TemplateId:
                    DecodeAlertMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case GreekUpdateMessage.TemplateId:
                    DecodeGreekMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case ModifyOrderRequestMessage.TemplateId:
                    DecodeModifyOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolStrikeRangeRequestMessage.TemplateId:
                    DecodeSymbolStrikeRangeRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SymbolStrikeRangeResponseMessage.TemplateId:
                    DecodeSymbolStrikeRangeResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PermEdgeToTheoMappingMessage.TemplateId:
                    DecodePermEdgeToTheoMappingMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case RegisterEdgeScanFeedServerRunnerJson.TemplateId:
                    DecodeRegisterEdgeScanFeedServerRunnerJson(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case UnregisterEdgeScanFeedServerRunner.TemplateId:
                    DecodeUnregisterEdgeScanFeedServerRunner(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadGeneratorRequestMessage.TemplateId:
                    DecodeSpreadGeneratorRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadGeneratorResultsMessage.TemplateId:
                    DecodeSpreadGeneratorResultsMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OptionChainRequest.TemplateId:
                    DecodeOptionChainRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OptionChainResponse.TemplateId:
                    DecodeOptionChainResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SymbolsRequest.TemplateId:
                    DecodeSymbolsRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SymbolsResponse.TemplateId:
                    DecodeSymbolsResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case EdgeScanFeedStatisticsMessage.TemplateId:
                    DecodeEdgeScanFeedStatisticsMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case EdgeScanFeedStatisticsUpdateMessage.TemplateId:
                    DecodeEdgeScanFeedStatisticsUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case ChartSeriesUpdateMessage.TemplateId:
                    DecodeChartSeriesUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case FirmOrderAndTradeSummaryMessage.TemplateId:
                    DecodeFirmOrderAndTradeSummary(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.DataRequestMessage.TemplateId:
                    DecodeDataRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case HistoricHighestBidLowestAskRequestMessage.TemplateId:
                    DecodeHistoricHighestBidLowestAskRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case HistoricHighestBidLowestAskResponseMessage.TemplateId:
                    DecodeHistoricHighestBidLowestAskResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PositionsRequestMessage.TemplateId:
                    DecodePositionsRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MultiplePositionsAddedMessage.TemplateId:
                    DecodeMultiplePositionsAddedMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.TheoToMarketSpreadUpdate.TemplateId:
                    DecodeTheoToMarketSpreadUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case PriceChainModelMessage.TemplateId:
                    DecodePriceChainModelMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MatrixSyntheticSpreadMessage.TemplateId:
                    DecodeMatrixSyntheticSpreadMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MatrixScrapeMessage.TemplateId:
                    DecodeMatrixScrapeMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MatrixSeekerMessage.TemplateId:
                    DecodeMatrixSeekerMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SeekerSpreadMessage.TemplateId:
                    DecodeMatrixSeekerSpreadMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case ExecutionTransactionMessage.TemplateId:
                    DecodeExecutionTransactionMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OrderTagMessage.TemplateId:
                    DecodeOrderTagMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ModifySmartOrderRequestMessage.TemplateId:
                    DecodeModifySmartOrderRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ModeledTheoUpdateMessage.TemplateId:
                    DecodeModeledTheoUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadBookQuoteMessage.TemplateId:
                    DecodeSpreadBookQuoteMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case CobTradeRequestMessage.TemplateId:
                    DecodeCobTradeRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case CancelDataRequestMessage.TemplateId:
                    DecodeCancelDataRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadExchPrintMessage.TemplateId:
                    DecodeSpreadExchPrintMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadExchOrderMessage.TemplateId:
                    DecodeSpreadExchOrderMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SpreadPrintMessage.TemplateId:
                    DecodeSpreadPrintMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case ModelDescriptionMessage.TemplateId:
                    DecodeModelDescriptionMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case SlimGreekUpdateMessage.TemplateId:
                    DecodeSlimGreekUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.CancelTokenMessage.TemplateId:
                    DecodeCancelTokenMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ImpliedQuoteUpdateMessage.TemplateId:
                    DecodeImpliedQuoteMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.GetClosestOptionRequestMessage.TemplateId:
                    DecodeGetClosestOptionRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.GetClosestOptionResponseMessage.TemplateId:
                    DecodeGetClosestOptionResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.NextOptionPermsRequestMessage.TemplateId:
                    DecodeNextOptionPermsRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.NextOptionPermsResponseMessage.TemplateId:
                    DecodeNextOptionPermsResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.NextSpreadPermsRequestMessage.TemplateId:
                    DecodeNextSpreadPermsRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.NextSpreadPermsResponseMessage.TemplateId:
                    DecodeNextSpreadPermsResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuctionPrintMessage.TemplateId:
                    DecodeAuctionPrintMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.JsonRequestMessage.TemplateId:
                    DecodeJsonRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.JsonResponseMessage.TemplateId:
                    DecodeJsonResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RiskCheckResultMessage.TemplateId:
                    DecodeRiskCheckResultMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OrderRiskRequestMessage.TemplateId:
                    DecodeOrderRiskRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.CancelRiskRequestMessage.TemplateId:
                    DecodeCancelRiskRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.CancelReplaceRiskRequestMessage.TemplateId:
                    DecodeCancelReplaceRiskRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OrderUpdateModelMessage.TemplateId:
                    DecodeOrderUpdateModelMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.FitUpdateMessage.TemplateId:
                    DecodeFitUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AutomationStateChangeMessage.TemplateId:
                    DecodeAutomationStateChangeMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SubmissionSummaryUpdateMessage.TemplateId:
                    DecodeSubmissionSummaryUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.PerformanceModeRequestMessage.TemplateId:
                    DecodePerformanceModeRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.PricingRequestMessage.TemplateId:
                    DecodePricingRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.PricingResponseMessage.TemplateId:
                    DecodePricingResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.TradesRequestMessage.TemplateId:
                    DecodeTradesRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.TradesResponseMessage.TemplateId:
                    DecodeTradesResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AddRemoveMultipleTradesRequestMessage.TemplateId:
                    DecodeAddRemoveMultipleTradesRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.MultipleContrapartyReportsAdded.TemplateId:
                    DecodeMultipleContrapartyReportsAdded(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AutoTraderConfigBinaryMessage.TemplateId:
                    DecodeAutoTraderConfigBinaryMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.TheoBatchUpdateMessage.TemplateId:
                    DecodeTheoBatchUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AdjTheoBatchUpdateMessage.TemplateId:
                    DecodeAdjTheoBatchUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OpenSpreadExchOrderMessage.TemplateId:
                    DecodeOpenSpreadExchOrderMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RemoveSpreadExchOrderMessage.TemplateId:
                    DecodeRemoveSpreadExchOrderMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.VolSurfaceRequest.TemplateId:
                    DecodeVolSurfaceRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.VolSurfaceResponse.TemplateId:
                    DecodeVolSurfaceResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.HerculesEchoMessage.TemplateId:
                    DecodeHerculesEchoMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.HerculesEchoRequestMessage.TemplateId:
                    DecodeHerculesEchoRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ZpTheoUpdateMessage.TemplateId:
                    DecodeZpTheoUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.LiveVolRequestMessage.TemplateId:
                    DecodeLiveVolDataRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.LiveVolResponseMessage.TemplateId:
                    DecodeLiveVolDataResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RbboUpdateMessage.TemplateId:
                    DecodeRbboUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.SymbolIndexMappingMessage.TemplateId:
                    DecodeSymbolIndexMappingMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.RegisterForeignUpdateRouteMessage.TemplateId:
                    DecodeRegisterForeignUpdateRoute(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.UnregisterForeignUpdateRouteMessage.TemplateId:
                    DecodeUnregisterForeignUpdateRoute(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ReplaceForeignUpdateRoutesMessage.TemplateId:
                    DecodeReplaceForeignUpdateRoutes(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.ForeignUpdateRoutesMessage.TemplateId:
                    DecodeForeignUpdateRoutes(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case MassCancelRequestMessage.TemplateId:
                    DecodeMassCancelRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.LatencyMeterEventMessage.TemplateId:
                    DecodeLatencyMeterEvent(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.StateSnapshotMessage.TemplateId:
                    DecodeStateSnapshot(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OpraDatabaseTradesRequestMessage.TemplateId:
                    DecodeOpraDatabaseTradesRequestMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.OpraDatabaseTradesResponseMessage.TemplateId:
                    DecodeOpraDatabaseTradesResponseMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;

                // Auth Server Messages
                case Generated.AuthLoginRequest.TemplateId:
                    DecodeAuthLoginRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthLoginResponse.TemplateId:
                    DecodeAuthLoginResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthUpdatePasswordRequest.TemplateId:
                    DecodeAuthUpdatePasswordRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthUpdatePasswordResponse.TemplateId:
                    DecodeAuthUpdatePasswordResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetUsersRequest.TemplateId:
                    DecodeAuthGetUsersRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetUsersResponse.TemplateId:
                    DecodeAuthGetUsersResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetConfigsRequest.TemplateId:
                    DecodeAuthGetConfigsRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetConfigsResponse.TemplateId:
                    DecodeAuthGetConfigsResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthDeleteConfigRequest.TemplateId:
                    DecodeAuthDeleteConfigRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthDeleteConfigResponse.TemplateId:
                    DecodeAuthDeleteConfigResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthConfigSave.TemplateId:
                    DecodeAuthConfigSave(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthConfigShare.TemplateId:
                    DecodeAuthConfigShare(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetDomListInfosRequest.TemplateId:
                    DecodeAuthGetDomListInfosRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetDomListInfosResponse.TemplateId:
                    DecodeAuthGetDomListInfosResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetCommissionsRequest.TemplateId:
                    DecodeAuthGetCommissionsRequest(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.AuthGetCommissionsResponse.TemplateId:
                    DecodeAuthGetCommissionsResponse(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;

                case Generated.SingleFieldUpdateMessage.TemplateId:
                    DecodeSingleFieldUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.EdgeScanFeedRunnerStartMessage.TemplateId:
                    DecodeEdgeScanFeedRunnerStartMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.EdgeScanFeedRunnerStopMessage.TemplateId:
                    DecodeEdgeScanFeedRunnerStopMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.EdgeScanFeedRunnerUpdateMessage.TemplateId:
                    DecodeEdgeScanFeedRunnerUpdateMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
                case Generated.TradeSlimMessage.TemplateId:
                    DecodeTradeSlimMessage(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                    break;
            }
        }

        private void DecodeClientAuthentication(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ClientAuthentication != null)
            {
                ClientAuthentication message = new ClientAuthentication();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                string versionString = message.GetAppVersion();
                if (Version.TryParse(versionString, out Version? version))
                {
                    ClientAuthenticationModel model = new ClientAuthenticationModel(message.UserId,
                                                                                    message.GetUsername(),
                                                                                    message.GetUserToken(),
                                                                                    message.GetAppId(),
                                                                                    version,
                                                                                    message.GetHostname());
                    ClientAuthentication?.Invoke(model);
                }
            }
        }

        private void DecodeClientRegistration(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ClientRegistration != null)
            {
                ClientRegistration message = new ClientRegistration();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                string versionString = message.GetAppVersion();
                if (Version.TryParse(versionString, out Version? version))
                {
                    ClientRegistrationModel model = new ClientRegistrationModel(message.GetUsername(),
                                                                                message.GetAppId(),
                                                                                version,
                                                                                message.GetHostname());
                    ClientRegistration?.Invoke(ref model);
                }
            }
        }

        private void DecodeLatencyMeterEvent(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (LatencyMeterEvent != null)
            {
                LatencyMeterEventMessage message = new LatencyMeterEventMessage();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                LatencyMeterEventModel model = new LatencyMeterEventModel(
                    message.BoxId,
                    message.ProgId,
                    message.InstanceId,
                    message.EventType,
                    message.GetEventId(),
                    message.TimingSource,
                    message.TimestampNanos);
                LatencyMeterEvent?.Invoke(ref model);
            }
        }

        private void DecodeStateSnapshot(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (StateSnapshot != null)
            {
                StateSnapshotMessage message = new StateSnapshotMessage();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

                StateSnapshotMessage.EntriesGroup entriesGroup = message.Entries;
                StateSnapshotEntryModel[] entries = new StateSnapshotEntryModel[entriesGroup.Count];
                int i = 0;
                while (entriesGroup.HasNext)
                {
                    entriesGroup.Next();
                    entries[i++] = new StateSnapshotEntryModel(entriesGroup.GetKey(), entriesGroup.GetValue());
                }

                StateSnapshotModel model = new StateSnapshotModel(
                    message.BoxId,
                    message.ProgId,
                    message.InstanceId,
                    message.GetSnapshotName(),
                    message.TimestampNanos,
                    entries);
                StateSnapshot?.Invoke(ref model);
            }
        }

        private void DecodeSubscribeMarketDataRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SubscribeMarketDataRequest != null)
            {
                SubscribeMarketDataRequest message = new SubscribeMarketDataRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                SubscribeMarketDataModel model = new SubscribeMarketDataModel(message.RequestID,
                                                                              (SubscriptionFieldType)message.MarketDataType,
                                                                              message.GetSymbol());
                SubscribeMarketDataRequest?.Invoke(ref model);
            }
        }

        private void DecodeUnsubscribeMarketDataRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (UnsubscribeMarketDataRequest != null)
            {
                UnsubscribeMarketDataRequest message = new UnsubscribeMarketDataRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                UnsubscribeMarketDataModel model = new UnsubscribeMarketDataModel(message.RequestID,
                                                                                  (SubscriptionFieldType)message.MarketDataType,
                                                                                  message.GetSymbol());
                UnsubscribeMarketDataRequest?.Invoke(ref model);
            }
        }

        private void DecodeSubscribeSpreadDataRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SubscribeMarketDataRequest != null)
            {
                SubscribeSpreadDataRequest message = new SubscribeSpreadDataRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                SubscribeMarketDataModel model = new SubscribeMarketDataModel(message.RequestID,
                                                                              (SubscriptionFieldType)message.MarketDataType,
                                                                              message.GetSymbol());
                SubscribeMarketDataRequest?.Invoke(ref model);
            }
        }

        private void DecodeUnsubscribeSpreadDataRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (UnsubscribeMarketDataRequest != null)
            {
                UnsubscribeSpreadDataRequest message = new UnsubscribeSpreadDataRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                UnsubscribeMarketDataModel model = new UnsubscribeMarketDataModel(message.RequestID,
                                                                                  (SubscriptionFieldType)message.MarketDataType,
                                                                                  message.GetSymbol());
                UnsubscribeMarketDataRequest?.Invoke(ref model);
            }
        }

        private void DecodeSubscribeTransactionRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SubscribeTransactionRequest != null)
            {
                SubscribeTransactionRequest message = new SubscribeTransactionRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                SubscribeTransactionModel model = new SubscribeTransactionModel(message.RequestID,
                                                                                message.RequestTime.FromUnixEpoch(),
                                                                                message.SequenceNumber,
                                                                                message.FillsOnly == BooleanEnum.True,
                                                                                message.AllOwn == BooleanEnum.True);

                SubscribeTransactionRequest.AccountsGroup accounts = message.Accounts;
                while (accounts.HasNext)
                {
                    SubscribeTransactionRequest.AccountsGroup nextAccount = accounts.Next();
                    string account = nextAccount.GetAccount();
                    if (!string.IsNullOrWhiteSpace(account))
                    {
                        model.Accounts.Add(account);
                    }
                }

                SubscribeTransactionRequest?.Invoke(ref model);
            }
        }

        private void DecodeUnsubscribeTransactionRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (UnsubscribeTransactionRequest != null)
            {
                UnsubscribeTransactionRequest message = new UnsubscribeTransactionRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                UnsubscribeTransactionModel model = new UnsubscribeTransactionModel(message.RequestID,
                                                                                    message.RequestTime.FromUnixEpoch());
                UnsubscribeTransactionRequest?.Invoke(ref model);
            }
        }

        private void DecodeSubscribePnlRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SubscribePnlRequest != null)
            {
                SubscribePnlRequest message = new SubscribePnlRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                SubscribePnlModel model = new SubscribePnlModel(message.RequestID,
                                                                message.RequestTime.FromUnixEpoch(),
                                                                (PositionSubscriptionMode)message.PositionSubscription);
                SubscribePnlRequest?.Invoke(ref model);
            }
        }

        private void DecodeUnsubscribePnlRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (UnsubscribePnlRequest != null)
            {
                UnsubscribePnlRequest message = new UnsubscribePnlRequest();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                UnsubscribePnlModel model = new UnsubscribePnlModel(message.RequestID,
                                                                    message.RequestTime.FromUnixEpoch());
                UnsubscribePnlRequest?.Invoke(ref model);
            }
        }

        private void DecodeOrderAdded(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            OrderAdded message = new OrderAdded();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermID();
            bool isComplex = message.IsComplexOrder == BooleanEnum.True;
            IOrder? model = _orderFactory.GetOrder(isComplex: isComplex, orderId: orderId);

            if (model == null)
            {
                return;
            }

            model.PermID = orderId;

            model.PartiallyFilled = message.PartiallyFilled == BooleanEnum.True;
            model.IsFirstFill = message.IsFirstFill == BooleanEnum.True;

            model.LastQuantity = message.LastQuantity;
            model.FilledQty = message.FilledQty;
            model.LeavesQuantity = message.LeavesQuantity;
            model.CumulativeQuantity = message.CumulativeQuantity;
            model.Quantity = message.Quantity;

            model.SpreadAvgPrice = DecodeDoubleNull2(message.SpreadAvgPrice);
            model.AveragePrice = DecodeDoubleNull2(message.AveragePrice);
            model.Price = DecodeDoubleNull2(message.Price);
            model.LastPrice = DecodeDoubleNull2(message.LastPrice);
            model.MinPrice = DecodeDoubleNull2(message.MinPrice);
            model.MaxPrice = DecodeDoubleNull2(message.MaxPrice);
            model.TagEdge = DecodeDoubleNull2(message.TagEdge);
            model.TagMid = DecodeDoubleNull2(message.TagMid);
            model.TagBid = DecodeDoubleNull2(message.TagBid);
            model.TagAsk = DecodeDoubleNull2(message.TagAsk);
            model.TagTheo = DecodeDoubleNull2(message.TagTheo);
            model.TagVolaV0 = DecodeDoubleNull2(message.TagVolaV0);
            model.TagVolaV1 = DecodeDoubleNull2(message.TagVolaV1);
            model.TagVolaV2 = DecodeDoubleNull2(message.TagVolaV2);
            model.VolaIv = message.TagVolaIv;
            model.TheoBid = DecodeDoubleNull2(message.TheoBid);
            model.TheoAsk = DecodeDoubleNull2(message.TheoAsk);
            model.TagEma = DecodeDoubleNull2(message.TagEma);
            model.Fee1 = DecodeDoubleNull2(message.Fee1);
            model.Fee2 = DecodeDoubleNull2(message.Fee2);
            model.Bid = DecodeDoubleNull2(message.Bid);
            model.Ask = DecodeDoubleNull2(message.Ask);
            model.UnderBid = DecodeDoubleNull2(message.UnderBid);
            model.UnderAsk = DecodeDoubleNull2(message.UnderAsk);
            model.TV = DecodeDoubleNull2(message.TV);
            model.Delta = DecodeDoubleNull4(message.Delta);
            model.ExchangeFee1 = DecodeDoubleNull2(message.ExchangeFee1);
            model.ExchangeFee2 = DecodeDoubleNull2(message.ExchangeFee2);
            model.BrokerFee1 = DecodeDoubleNull2(message.BrokerFee1);
            model.BrokerFee2 = DecodeDoubleNull2(message.BrokerFee2);
            model.TotalContracts = DecodeDoubleNull2(message.TotalContracts);
            model.FillTime = DecodeDoubleNull2(message.FillTime);
            model.TradeToNewTime = DecodeDoubleNull2(message.TradeToNewTime);
            model.SubmitToNewTime = DecodeDoubleNull2(message.SubmitToNewTime);
            model.NewToCancelTime = DecodeDoubleNull2(message.NewToCancelTime);
            model.BidPercentOfFillPrice = DecodeDoubleNull2(message.BidPercentOfFillPrice);
            model.OmsBidPercentOfFillPrice = DecodeDoubleNull2(message.OmsBidPercentOfFillPrice);
            model.TotalDelta = DecodeDoubleNull4(message.TotalDelta);
            model.HanweckTotalTheo = DecodeDoubleNull2(message.HanweckTotalTheo);
            model.HanweckTotalGamma = DecodeDoubleNull4(message.HanweckTotalGamma);
            model.HanweckTotalVega = DecodeDoubleNull4(message.HanweckTotalVega);
            model.HanweckTotalTheta = DecodeDoubleNull4(message.HanweckTotalTheta);
            model.HanweckTotalRho = DecodeDoubleNull4(message.HanweckTotalRho);
            model.HanweckTotalIV = DecodeDoubleNull4(message.HanweckTotalIV);
            model.HanweckTotalUnder = DecodeDoubleNull2(message.HanweckTotalUnder);
            model.HanweckTotalUBid = DecodeDoubleNull2(message.HanweckTotalUBid);
            model.HanweckTotalUAsk = DecodeDoubleNull2(message.HanweckTotalUAsk);
            model.HanweckTotalBid = DecodeDoubleNull2(message.HanweckTotalBid);
            model.HanweckTotalAsk = DecodeDoubleNull2(message.HanweckTotalAsk);
            model.EdgeOverride = DecodeDoubleNull2(message.EdgeOverride);
            model.AdjustedEdgeOverride = DecodeDoubleNull2(message.AdjustedEdgeOverride);
            model.EdgeToTheo = DecodeDoubleNull2(message.EdgeToTheo);
            model.TagEdgeToTheo = DecodeDoubleNull2(message.TagEdgeToTheo);
            model.TagEdgeToEma = DecodeDoubleNull2(message.TagEdgeToEma);
            model.TagEdgeToVolaV0 = DecodeDoubleNull2(message.TagEdgeToVolaV0);
            model.TagEdgeToVolaV1 = DecodeDoubleNull2(message.TagEdgeToVolaV1);
            model.TagEdgeToVolaV2 = DecodeDoubleNull2(message.TagEdgeToVolaV2);
            model.TagBestBid = DecodeDoubleNull2(message.TagBestBid);
            model.TagBestAsk = DecodeDoubleNull2(message.TagBestAsk);
            model.TagMktMkrBid = DecodeDoubleNull2(message.TagMktMkrBid);
            model.TagMktMkrAsk = DecodeDoubleNull2(message.TagMktMkrAsk);
            model.InitialEdge = DecodeDoubleNull2(message.InitialEdge);
            model.OpenEdge = DecodeDoubleNull2(message.OpenEdge);
            model.CloseEdge = DecodeDoubleNull2(message.CloseEdge);
            model.LastEdge = DecodeDoubleNull2(message.LastEdge);
            model.DeltaAdjLastEdge = DecodeDoubleNull2(message.DeltaAdjLastEdge);
            model.DeltaAdjLastEdgeNotional = DecodeDoubleNull2(message.DeltaAdjLastEdgeNotional);
            model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);
            model.DeltaAdjChange = DecodeDoubleNull2(message.DeltaAdjChange);
            model.DeltaAdjChangeNotional = DecodeDoubleNull2(message.DeltaAdjChangeNotional);
            model.EdgeScanFeedEdge = DecodeDoubleNull2(message.EdgeScanFeedEdge);
            model.EdgeScanFeedTimespan = DecodeDoubleNull2(message.EdgeScanFeedTimespan);
            model.EdgeScanFeedBuyPrice = DecodeDoubleNull2(message.EdgeScanFeedBuyPrice);
            model.EdgeScanFeedBuyQty = message.EdgeScanFeedBuyQty;
            model.EdgeScanFeedSellPrice = DecodeDoubleNull2(message.EdgeScanFeedSellPrice);
            model.EdgeScanFeedSellQty = message.EdgeScanFeedSellQty;
            model.EdgeScanFeedBuyTime = message.EdgeScanFeedBuyTime.FromUnixEpoch();
            model.EdgeScanFeedSellTime = message.EdgeScanFeedSellTime.FromUnixEpoch();
            model.EdgeScanFeedRespondLatency = DecodeDoubleNull2(message.EdgeScanFeedRespondLatency);

            model.EdgeScanFeedConditionCode = (char)message.EdgeScanFeedConditionCode;
            model.ResubmitCount = message.ResubmitCount;
            model.TotalEstimatedResubmit = message.TotalEstimatedResubmit;

            model.Side = message.AggressorSide == AggressorSide.Buy ? Side.Buy : Side.Sell;
            model.OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus;
            model.BaseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
            model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;
            model.TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce;

            model.OrderSource = (Data.Enums.OrderSource)message.OrderSource;

            model.Username = message.GetUsername();
            model.UnderlyingSymbol = message.GetUnderlyingSymbol();

            model.SubmitTime = message.SubmitTime.FromUnixEpoch();
            model.LastUpdateTime = message.LastUpdateTime.FromUnixEpoch();
            model.Timestamp = message.Timestamp.FromUnixEpoch();
            model.NewStatusTimeStamp = message.NewStatusTimeStamp.FromUnixEpoch();

            model.DeltaAdjustedTheo = DecodeDoubleNull2(message.DeltaAdjustedTheo);
            model.BidSize = message.BidSize;
            model.AskSize = message.AskSize;
            model.UnderlyingBidSize = message.UnderlyingBidSize;
            model.UnderlyingAskSize = message.UnderlyingAskSize;

            model.EdgeType = (EdgeType)message.EdgeType;
            model.Edge = DecodeDoubleNull2(message.Edge);
            model.IsDeltaAdjusted = message.IsDeltaAdjusted == BooleanEnum.True;
            model.LoopInitLatency = DecodeDoubleNull2(message.LoopInitLatency);
            model.TagUnderBid = DecodeDoubleNull2(message.TagUnderBid);
            model.TagUnderAsk = DecodeDoubleNull2(message.TagUnderAsk);
            model.DigBid = DecodeDoubleNull2(message.DigBid);
            model.DigAsk = DecodeDoubleNull2(message.DigAsk);
            model.WeightedVega = DecodeDoubleNull2(message.WeightedVega);
            model.DigBidSize = message.DigBidSize;
            model.DigAskSize = message.DigAskSize;
            model.IsTagged = message.IsTagged == BooleanEnum.True;
            model.HardSide = message.HardSide == Generated.Side.NULL_VALUE ? null : (Side)message.HardSide;
            model.HardSideDesignationTime = message.HardSideDesignationTime.FromUnixEpoch();
            model.HardSideBuyGiveUp = DecodeDoubleNull2(message.HardSideBuyGiveUp);
            model.HardSideSellGiveUp = DecodeDoubleNull2(message.HardSideSellGiveUp);
            model.HardSideAtTrade = message.HardSideAtTrade == Generated.Side.NULL_VALUE ? null : (Side)message.HardSideAtTrade;
            model.HardSideAtTradeDesignationTime = message.HardSideAtTradeDesignationTime.FromUnixEpoch();
            model.HardSideAtTradeBuyGiveUp = DecodeDoubleNull2(message.HardSideAtTradeBuyGiveUp);
            model.HardSideAtTradeSellGiveUp = DecodeDoubleNull2(message.HardSideAtTradeSellGiveUp);

            model.EdgeGiveUp = DecodeDoubleNull2(message.EdgeGiveUp);
            model.CloseSubs = DecodeDoubleNull2(message.CloseSubs);
            model.OrderEdgeToTheo = DecodeDoubleNull2(message.OrderEdgeToTheo);

            model.TimeValue = DecodeDoubleNull2(message.TimeValue);
            model.IntrinsicValue = DecodeDoubleNull2(message.IntrinsicValue);
            model.FVDivs = DecodeDoubleNull2(message.FVDivs);
            model.UFwd = DecodeDoubleNull2(message.UFwd);
            model.UFwdFactor = DecodeDoubleNull2(message.UFwdFactor);
            model.BorrowCost = DecodeDoubleNull2(message.BorrowCost);
            model.BorrowRate = DecodeDoubleNull2(message.BorrowRate);
            model.UPrice = DecodeDoubleNull2(message.UPrice);
            model.UTheo = DecodeDoubleNull2(message.UTheo);

            model.SharedId = message.SharedId;
            model.Sequence = message.Sequence;
            model.TypeId = (ModuleType)message.TypeId;
            model.SubTypeId = (SubType)message.SubTypeCode;
            model.SubTypeSequence = message.SubTypeSequence;
            model.Venue = message.Venue == OrderAdded.VenueNullValue ? null : (Venue)message.Venue;
            model.CostOfHedging = DecodeDoubleNull2(message.CostOfHedging);

            model.SubType = message.SubType == OrderAdded.SubTypeNullValue ? null : (OrderSubType)message.SubType;

            OrderAdded.NoLegsGroup legs = message.NoLegs;
            if (model.IsComplexOrder)
            {
                IComplexOrder complexOrderModel = (IComplexOrder)model;
                while (legs.HasNext)
                {
                    OrderAdded.NoLegsGroup nextLeg = legs.Next();

                    string legId = nextLeg.GetLegID();

                    IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);
                    legModel.LegID = legId;

                    legModel.Ratio = nextLeg.Ratio;
                    legModel.Quantity = nextLeg.Quantity;
                    legModel.LastQuantity = nextLeg.LastQuantity;
                    legModel.LeavesQuantity = nextLeg.LeavesQuantity;
                    legModel.CumulativeQuantity = nextLeg.CumulativeQuantity;

                    legModel.Fee1 = DecodeDoubleNull2(nextLeg.Fee1);
                    legModel.Fee2 = DecodeDoubleNull2(nextLeg.Fee2);
                    legModel.BrokerFee1 = DecodeDoubleNull2(nextLeg.BrokerFee1);
                    legModel.BrokerFee2 = DecodeDoubleNull2(nextLeg.BrokerFee2);
                    legModel.ExchangeFee1 = DecodeDoubleNull2(nextLeg.ExchangeFee1);
                    legModel.ExchangeFee2 = DecodeDoubleNull2(nextLeg.ExchangeFee2);
                    legModel.Delta = DecodeDoubleNull4(nextLeg.Delta);
                    legModel.TV = DecodeDoubleNull2(nextLeg.TV);
                    legModel.Ask = DecodeDoubleNull2(nextLeg.Ask);
                    legModel.Bid = DecodeDoubleNull2(nextLeg.Bid);
                    legModel.AveragePrice = DecodeDoubleNull2(nextLeg.AveragePrice);
                    legModel.LastPrice = DecodeDoubleNull2(nextLeg.LastPrice);
                    legModel.HanweckTV = DecodeDoubleNull2(nextLeg.HanweckTV);
                    legModel.HanweckGamma = DecodeDoubleNull4(nextLeg.HanweckGamma);
                    legModel.HanweckVega = DecodeDoubleNull4(nextLeg.HanweckVega);
                    legModel.HanweckTheta = DecodeDoubleNull4(nextLeg.HanweckTheta);
                    legModel.HanweckRho = DecodeDoubleNull4(nextLeg.HanweckRho);
                    legModel.HanweckIV = DecodeDoubleNull4(nextLeg.HanweckIV);
                    legModel.HanweckUnder = DecodeDoubleNull2(nextLeg.HanweckUnder);
                    legModel.HanweckUnderBid = DecodeDoubleNull2(nextLeg.HanweckUnderBid);
                    legModel.HanweckUnderAsk = DecodeDoubleNull2(nextLeg.HanweckUnderAsk);
                    legModel.HanweckBid = DecodeDoubleNull2(nextLeg.HanweckBid);
                    legModel.HanweckAsk = DecodeDoubleNull2(nextLeg.HanweckAsk);
                    legModel.DeltaAdjustedTheo = DecodeDoubleNull2(nextLeg.DeltaAdjustedTheo);
                    legModel.BidSize = nextLeg.BidSize;
                    legModel.AskSize = nextLeg.AskSize;

                    legModel.PositionEffect = (Data.Enums.PositionEffect)nextLeg.PositionEffect;
                    legModel.Side = nextLeg.LegSide == LegSide.BuySide ? Side.Buy : Side.Sell;
                    legModel.OrderStatus = (Data.Enums.OrderStatus)nextLeg.OrderStatus;

                    legModel.Timestamp = nextLeg.Timestamp.FromUnixEpoch();
                    legModel.LastUpdateTime = nextLeg.LastUpdateTime.FromUnixEpoch();
                    legModel.HanweckBidTime = nextLeg.HanweckBidTime.FromUnixEpoch();
                    legModel.HanweckAskTime = nextLeg.HanweckAskTime.FromUnixEpoch();
                    legModel.HanweckTimestamp = nextLeg.HanweckTimestamp.FromUnixEpoch();

                    legModel.TimeValue = DecodeDoubleNull2(nextLeg.TimeValue);
                    legModel.IntrinsicValue = DecodeDoubleNull2(nextLeg.IntrinsicValue);
                    legModel.FVDivs = DecodeDoubleNull2(nextLeg.FVDivs);
                    legModel.UFwd = DecodeDoubleNull2(nextLeg.UFwd);
                    legModel.UFwdFactor = DecodeDoubleNull2(nextLeg.UFwdFactor);
                    legModel.BorrowCost = DecodeDoubleNull2(nextLeg.BorrowCost);
                    legModel.BorrowRate = DecodeDoubleNull2(nextLeg.BorrowRate);
                    legModel.UPrice = DecodeDoubleNull2(nextLeg.UPrice);
                    legModel.UTheo = DecodeDoubleNull2(nextLeg.UTheo);

                    DecodeLegContraFields_OrderAdded(nextLeg, legModel);

                    legModel.PermID = nextLeg.GetPermID();
                    legModel.OrderID = nextLeg.GetOrderID();
                    legModel.Symbol = nextLeg.GetSymbol();
                }
            }

            DecodeOrderContraFields_OrderAdded(message, model);

            model.LastExchange = message.GetLastExchange();
            model.Exchanges = message.GetExchanges();
            model.Reason = message.GetReason();
            model.Source = message.GetSource();
            model.AccountAcronym = message.GetAccountAcronym();
            model.Tag = message.GetTag();
            model.Trader = message.GetTrader();
            model.Type = message.GetOrderType();
            model.OrderID = message.GetOrderID();
            model.Route = message.GetRoute();
            model.Symbol = message.GetSymbol();
            model.Description = message.GetDescription();
            model.SpreadId = message.GetSpreadId();
            model.FullTag = message.GetFullTag();
            model.Comment = message.GetComment();
            model.AutomationType = message.GetAutomationType();
            model.SpreadHash = message.GetSpreadHash();
            model.Tagger = message.GetTagger();
            model.TaggedMessage = message.GetTaggedMessage();
            _orderFactory.OrderAdded(model);
        }

        private static void DecodeLegContraFields_OrderAdded(OrderAdded.NoLegsGroup leg, IComplexOrderLeg legModel)
        {
            var caps = leg.NoLegContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                legModel.ContraCapacities = list;
            }

            var brokers = leg.NoLegContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                legModel.ContraBrokerNames = list;
            }

            var cmtas = leg.NoLegContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                legModel.ContraCmtas = list;
            }

            var traders = leg.NoLegContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                legModel.ContraTraders = list;
            }
        }

        private static void DecodeOrderContraFields_OrderAdded(OrderAdded message, IOrder model)
        {
            var caps = message.NoContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                model.ContraCapacities = list;
            }

            var brokers = message.NoContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                model.ContraBrokerNames = list;
            }

            var cmtas = message.NoContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                model.ContraCmtas = list;
            }

            var traders = message.NoContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                model.ContraTraders = list;
            }
        }

        private void DecodeOrderRemoved(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            OrderRemoved message = new OrderRemoved();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermId();

            _orderFactory.OrderRemoved(orderId);
        }

        private void DecodeSendOrder(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SendOrder == null)
            {
                return;
            }

            SendOrder message = new SendOrder();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetLocalID();
            bool isComplex = message.IsComplexOrder == BooleanEnum.True;

            IOrderSlim model = _orderFactory?.GetOrderSlim(isComplex: isComplex, orderId: orderId) ?? (isComplex ? new ComplexOrderSlim() : new OrderSlim());

            model.LocalID = orderId;
            model.Quantity = message.Quantity;

            model.Price = message.Price;
            model.AveragePrice = message.AveragePrice;
            model.Bid = message.Bid;
            model.Ask = message.Ask;
            model.UnderBid = message.UnderBid;
            model.UnderAsk = message.UnderAsk;
            model.NewToCancelTime = message.NewToCancelTime;
            model.TotalDelta = message.TotalDelta;
            model.HanweckTotalTheo = message.HanweckTotalTheo;
            model.EdgeOverride = message.EdgeOverride;
            model.AdjustedEdgeOverride = message.AdjustedEdgeOverride;

            model.Side = message.Side == Generated.Side.Buy ? Side.Buy : Side.Sell;
            model.BaseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
            model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;
            model.TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce;

            model.UnderlyingSymbol = message.GetUnderlyingSymbol();

            model.Currency = message.GetCurrency();

            model.CloseUnderBid = message.CloseUnderBid;
            model.CloseUnderAsk = message.CloseUnderAsk;

            model.DeltaAdjustedTheo = message.DeltaAdjustedTheo;
            model.MinimumTickStyle = (Data.Enums.MinimumTickStyle)message.MinimumTickStyle;
            model.Multiplier = message.Multiplier;

            model.TagEdge = message.TagEdge;
            model.SkipNewPriceEvaluation = message.SkipNewPriceEvaluation == BooleanEnum.True;
            model.IsGTH = message.IsGTH == BooleanEnum.True;

            model.Venue = message.Venue == Generated.SendOrder.VenueNullValue ? null : (Venue)message.Venue;
            model.SubType = message.SubType == Generated.SendOrder.SubTypeNullValue ? null : (OrderSubType)message.SubType;

            model.DestinationSequence = message.DestinationSequence;

            model.UserId = message.UserId;
            model.RiskCheckId = message.RiskCheckId;

            if (actingVersion >= Generated.SendOrder.IoiIdSinceVersion)
            {
                model.IoiId = message.IoiId;
            }

            model.SharedId = message.SharedId;
            model.Sequence = message.Sequence;
            model.TypeId = (ModuleType)message.TypeId;
            model.SubTypeId = (SubType)message.SubTypeCode;
            model.SubTypeSequence = message.SubTypeSequence;
            model.OrderSource = (OrderSource)message.OrderSource;
            model.SubmitTime = message.SubmitTime.FromUnixEpoch();

            model.VolaTheo = message.VolaTheo;
            model.VolaTheoAdj = message.VolaTheoAdj;
            model.VolaIv = message.VolaIv;
            model.TheoBid = message.TheoBid;
            model.TheoAsk = message.TheoAsk;
            model.EdgeType = (EdgeType)message.EdgeType;

            model.DigBid = message.DigBid;
            model.DigAsk = message.DigAsk;
            model.DigBidSize = message.DigBidSize;
            model.DigAskSize = message.DigAskSize;
            model.WeightedVega = message.WeightedVega;
            model.CloseEdgeOverride = message.CloseEdgeOverride;

            SendOrder.NoLegsGroup legs = message.NoLegs;
            if (model.IsComplexOrder)
            {
                IComplexOrderSlim complexOrderModel = (IComplexOrderSlim)model;
                while (legs.HasNext)
                {
                    SendOrder.NoLegsGroup nextLeg = legs.Next();

                    string legId = nextLeg.GetLegID();

                    IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);
                    legModel.LegID = legId;

                    legModel.Ratio = nextLeg.Ratio;
                    legModel.Quantity = nextLeg.Quantity;
                    legModel.LastQuantity = nextLeg.LastQuantity;
                    legModel.TransactionID = nextLeg.TransactionID;
                    legModel.LeavesQuantity = nextLeg.LeavesQuantity;
                    legModel.CumulativeQuantity = nextLeg.CumulativeQuantity;

                    legModel.Fee1 = nextLeg.Fee1;
                    legModel.Fee2 = nextLeg.Fee2;
                    legModel.BrokerFee1 = nextLeg.BrokerFee1;
                    legModel.BrokerFee2 = nextLeg.BrokerFee2;
                    legModel.ExchangeFee1 = nextLeg.ExchangeFee1;
                    legModel.ExchangeFee2 = nextLeg.ExchangeFee2;
                    legModel.Delta = nextLeg.Delta;
                    legModel.TV = nextLeg.TV;
                    legModel.Ask = nextLeg.Ask;
                    legModel.Bid = nextLeg.Bid;
                    legModel.AveragePrice = nextLeg.AveragePrice;
                    legModel.LastPrice = nextLeg.LastPrice;
                    legModel.HanweckTV = nextLeg.HanweckTV;
                    legModel.HanweckGamma = nextLeg.HanweckGamma;
                    legModel.HanweckVega = nextLeg.HanweckVega;
                    legModel.HanweckTheta = nextLeg.HanweckTheta;
                    legModel.HanweckRho = nextLeg.HanweckRho;
                    legModel.HanweckIV = nextLeg.HanweckIV;
                    legModel.HanweckUnder = nextLeg.HanweckUnder;
                    legModel.HanweckUnderBid = nextLeg.HanweckUnderBid;
                    legModel.HanweckUnderAsk = nextLeg.HanweckUnderAsk;
                    legModel.HanweckBid = nextLeg.HanweckBid;
                    legModel.HanweckAsk = nextLeg.HanweckAsk;

                    legModel.DeltaAdjustedTheo = nextLeg.DeltaAdjustedTheo;
                    legModel.BidSize = nextLeg.BidSize;
                    legModel.AskSize = nextLeg.AskSize;

                    legModel.PositionEffect = (Data.Enums.PositionEffect)nextLeg.PositionEffect;
                    legModel.Side = nextLeg.LegSide == LegSide.BuySide ? Side.Buy : Side.Sell;
                    legModel.OrderStatus = (Data.Enums.OrderStatus)nextLeg.OrderStatus;

                    legModel.Timestamp = nextLeg.Timestamp.FromUnixEpoch();
                    legModel.LastUpdateTime = nextLeg.LastUpdateTime.FromUnixEpoch();
                    legModel.HanweckBidTime = nextLeg.HanweckBidTime.FromUnixEpoch();
                    legModel.HanweckAskTime = nextLeg.HanweckAskTime.FromUnixEpoch();
                    legModel.HanweckTimestamp = nextLeg.HanweckTimestamp.FromUnixEpoch();

                    legModel.ExecutionID = nextLeg.GetExecutionID();
                    legModel.PermID = nextLeg.GetPermID();
                    legModel.OrderID = nextLeg.GetOrderID();
                    legModel.Symbol = nextLeg.GetSymbol();
                }
            }

            model.Route = message.GetRoute();
            model.Destination = message.GetDestination();
            model.Symbol = message.GetSymbol();
            model.SpreadId = message.GetSpreadId();
            model.AccountAcronym = message.GetAccountAcronym();
            model.Tag = message.GetTag();
            model.Comment = message.GetComment();
            model.SmartRoute = message.GetSmartRoute();
            model.RouteOverride = message.GetRouteOverride();

            if (actingVersion >= Generated.SendOrder.PrimaryExchangeSinceVersion)
            {
                model.PrimaryExchange = message.GetPrimaryExchange();
            }

            if (actingVersion >= Generated.SendOrder.HasOrderTagSinceVersion
                && message.HasOrderTag == BooleanEnum.True)
            {
                bool isEdgeScanFeed = message.OrderTagIsEdgeScanFeed == BooleanEnum.True;
                OrderTagModel orderTag = isEdgeScanFeed
                    ? new EdgeScanFeedOrderTagModel()
                    : new OrderTagModel();

                orderTag.OrderDate = message.OrderTagOrderDate.FromUnixEpoch();
                orderTag.Bid = message.OrderTagBid;
                orderTag.Ask = message.OrderTagAsk;
                orderTag.BidSize = message.OrderTagBidSize;
                orderTag.AskSize = message.OrderTagAskSize;
                orderTag.Theo = message.OrderTagTheo;
                orderTag.Ema = message.OrderTagEma;
                orderTag.Edge = message.OrderTagEdge;
                orderTag.EdgeType = (EdgeType)message.OrderTagEdgeType;
                orderTag.VolaTheo = message.OrderTagVolaTheo;
                orderTag.VolaTheoAdj = message.OrderTagVolaTheoAdj;
                orderTag.VolaIv = message.OrderTagVolaIv;
                orderTag.TheoBid = message.OrderTagTheoBid;
                orderTag.TheoAsk = message.OrderTagTheoAsk;
                orderTag.UnderBid = message.OrderTagUnderBid;
                orderTag.UnderAsk = message.OrderTagUnderAsk;
                orderTag.UnderBidSize = message.OrderTagUnderBidSize;
                orderTag.UnderAskSize = message.OrderTagUnderAskSize;
                orderTag.DigBid = message.OrderTagDigBid;
                orderTag.DigAsk = message.OrderTagDigAsk;
                orderTag.DigBidSize = message.OrderTagDigBidSize;
                orderTag.DigAskSize = message.OrderTagDigAskSize;
                orderTag.WeightedVega = message.OrderTagWeightedVega;
                orderTag.OrderSource = (Data.Enums.OrderSource)message.OrderTagOrderSource;
                orderTag.ModuleType = (ModuleType)message.OrderTagModuleType;
                orderTag.SubType = (SubType)message.OrderTagSubTypeCode;
                orderTag.SharedId = message.OrderTagSharedId;
                orderTag.Sequence = message.OrderTagSequence;
                orderTag.SubTypeSequence = message.OrderTagSubTypeSequence;
                orderTag.OrderSubType = (OrderSubType)message.OrderTagOrderSubType;
                orderTag.ResubmitCount = message.OrderTagResubmitCount;
                orderTag.TotalEstimatedResubmit = message.OrderTagTotalEstimatedResubmit;
                orderTag.SessionId = message.OrderTagSessionId;

                if (orderTag is EdgeScanFeedOrderTagModel esfOrderTag)
                {
                    esfOrderTag.EdgeScannerType = (EdgeScannerType)message.OrderTagEsfEdgeScannerType;
                    esfOrderTag.EdgeScanFeedConditionCode = (char)message.OrderTagEsfConditionCode;
                    esfOrderTag.EdgeScanFeedEdge = message.OrderTagEsfEdge;
                    esfOrderTag.EdgeScanFeedTimespan = message.OrderTagEsfTimespan;
                    esfOrderTag.EdgeScanFeedRespondLatency = message.OrderTagEsfRespondLatency;
                    esfOrderTag.EdgeScanFeedDeltaAdjPrice = message.OrderTagEsfDeltaAdjPrice;
                    esfOrderTag.EdgeScanFeedBuyPrice = message.OrderTagEsfBuyPrice;
                    esfOrderTag.EdgeScanFeedSellPrice = message.OrderTagEsfSellPrice;
                    esfOrderTag.EdgeScanFeedBuyQty = message.OrderTagEsfBuyQty;
                    esfOrderTag.EdgeScanFeedSellQty = message.OrderTagEsfSellQty;
                    esfOrderTag.EdgeScanFeedBuyTime = message.OrderTagEsfBuyTime.FromUnixEpoch();
                    esfOrderTag.EdgeScanFeedSellTime = message.OrderTagEsfSellTime.FromUnixEpoch();
                }

                string orderTagPermId = message.GetOrderTagPermId();
                orderTag.PermId = string.IsNullOrEmpty(orderTagPermId) ? null : orderTagPermId;

                string orderTagTrader = message.GetOrderTagTrader();
                orderTag.Trader = string.IsNullOrEmpty(orderTagTrader) ? null : orderTagTrader;

                string orderTagInstance = message.GetOrderTagInstance();
                orderTag.Instance = string.IsNullOrEmpty(orderTagInstance) ? null : orderTagInstance;

                string orderTagParentSpreadHash = message.GetOrderTagParentSpreadHash();
                orderTag.ParentSpreadHash = string.IsNullOrEmpty(orderTagParentSpreadHash) ? null : orderTagParentSpreadHash;

                model.OrderTag = orderTag;
            }

            SendOrder?.Invoke(model);
        }

        private void DecodeSendOrderMinimal(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SendOrder == null)
            {
                return;
            }

            SendOrderMinimal message = new SendOrderMinimal();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetLocalID();
            bool isComplex = message.IsComplexOrder == BooleanEnum.True;

            IOrderSlim model = _orderFactory?.GetOrderSlim(isComplex: isComplex, orderId: orderId) ?? (isComplex ? new ComplexOrderSlim() : new OrderSlim());
            model.LocalID = orderId;
            model.Side = (Side?)message.Side;
            model.Quantity = message.Quantity;
            model.Price = message.Price;
            model.UnderBid = message.UnderBid;
            model.UnderAsk = message.UnderAsk;
            model.NewToCancelTime = message.NewToCancelTime;
            model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;
            model.TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce;
            model.UnderlyingSymbol = message.GetUnderlyingSymbol();
            model.AccountAcronym = message.GetAccountAcronym();
            model.Tag = message.GetTag();
            model.Route = message.GetRoute();
            model.Destination = message.GetDestination();
            model.Symbol = message.GetSymbol();
            model.Venue = message.Venue == Generated.SendOrder.VenueNullValue ? null : (Venue)message.Venue;

            model.UserId = message.UserId;
            model.RiskCheckId = message.RiskCheckId;

            SendOrderMinimal.NoLegsGroup legs = message.NoLegs;
            if (model.IsComplexOrder)
            {
                IComplexOrderSlim complexOrderModel = (IComplexOrderSlim)model;
                while (legs.HasNext)
                {
                    SendOrderMinimal.NoLegsGroup nextLeg = legs.Next();

                    string legId = nextLeg.GetLegID();

                    IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);
                    legModel.LegID = legId;

                    legModel.Ratio = nextLeg.Ratio;
                    legModel.Quantity = nextLeg.Quantity;

                    legModel.PositionEffect = (Data.Enums.PositionEffect)nextLeg.PositionEffect;
                    legModel.Side = nextLeg.LegSide == LegSide.BuySide ? Side.Buy : Side.Sell;

                    legModel.Symbol = nextLeg.GetSymbol();
                }
            }

            SendOrder?.Invoke(model);
        }

        private void DecodeMultipleOrderAdded(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            MultipleOrderAdded multipleOrdersMessage = new MultipleOrderAdded();
            multipleOrdersMessage.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = multipleOrdersMessage.RequestId;
            int totalQueued = multipleOrdersMessage.IncomingOrdersCount;
            int lastMessageIndex = multipleOrdersMessage.LastOrderIndex;
            List<IOrder> orders = new List<IOrder>();
            MultipleOrderAdded.NoOrdersGroup ordersMap = multipleOrdersMessage.NoOrders;
            while (ordersMap.HasNext)
            {
                try
                {
                    MultipleOrderAdded.NoOrdersGroup message = ordersMap.Next();
                    string orderId = message.GetPermID();
                    bool isComplex = message.IsComplexOrder == BooleanEnum.True;
                    IOrder? model = _orderFactory.GetOrder(isComplex: isComplex, orderId: orderId);

                    if (model == null)
                    {
                        continue;
                    }

                    model.PermID = orderId;

                    model.PartiallyFilled = message.PartiallyFilled == BooleanEnum.True;
                    model.IsFirstFill = message.IsFirstFill == BooleanEnum.True;

                    model.LastQuantity = message.LastQuantity;
                    model.FilledQty = message.FilledQty;
                    model.LeavesQuantity = message.LeavesQuantity;
                    model.CumulativeQuantity = message.CumulativeQuantity;
                    model.Quantity = message.Quantity;

                    model.SpreadAvgPrice = DecodeDoubleNull2(message.SpreadAvgPrice);
                    model.AveragePrice = DecodeDoubleNull2(message.AveragePrice);
                    model.Price = DecodeDoubleNull2(message.Price);
                    model.LastPrice = DecodeDoubleNull2(message.LastPrice);
                    model.MinPrice = DecodeDoubleNull2(message.MinPrice);
                    model.MaxPrice = DecodeDoubleNull2(message.MaxPrice);
                    model.TagEdge = DecodeDoubleNull2(message.TagEdge);
                    model.TagMid = DecodeDoubleNull2(message.TagMid);
                    model.TagBid = DecodeDoubleNull2(message.TagBid);
                    model.TagAsk = DecodeDoubleNull2(message.TagAsk);
                    model.TagTheo = DecodeDoubleNull2(message.TagTheo);
                    model.TagVolaV0 = DecodeDoubleNull2(message.TagVolaV0);
                    model.TagVolaV1 = DecodeDoubleNull2(message.TagVolaV1);
                    model.TagVolaV2 = DecodeDoubleNull2(message.TagVolaV2);
                    model.VolaIv = message.TagVolaIv;
                    model.TheoBid = DecodeDoubleNull2(message.TheoBid);
                    model.TheoAsk = DecodeDoubleNull2(message.TheoAsk);
                    model.TagEma = DecodeDoubleNull2(message.TagEma);
                    model.Fee1 = DecodeDoubleNull2(message.Fee1);
                    model.Fee2 = DecodeDoubleNull2(message.Fee2);
                    model.Bid = DecodeDoubleNull2(message.Bid);
                    model.Ask = DecodeDoubleNull2(message.Ask);
                    model.UnderBid = DecodeDoubleNull2(message.UnderBid);
                    model.UnderAsk = DecodeDoubleNull2(message.UnderAsk);
                    model.TV = DecodeDoubleNull2(message.TV);
                    model.Delta = DecodeDoubleNull4(message.Delta);
                    model.ExchangeFee1 = DecodeDoubleNull2(message.ExchangeFee1);
                    model.ExchangeFee2 = DecodeDoubleNull2(message.ExchangeFee2);
                    model.BrokerFee1 = DecodeDoubleNull2(message.BrokerFee1);
                    model.BrokerFee2 = DecodeDoubleNull2(message.BrokerFee2);
                    model.TotalContracts = DecodeDoubleNull2(message.TotalContracts);
                    model.FillTime = DecodeDoubleNull2(message.FillTime);
                    model.TradeToNewTime = DecodeDoubleNull2(message.TradeToNewTime);
                    model.SubmitToNewTime = DecodeDoubleNull2(message.SubmitToNewTime);
                    model.NewToCancelTime = DecodeDoubleNull2(message.NewToCancelTime);
                    model.BidPercentOfFillPrice = DecodeDoubleNull2(message.BidPercentOfFillPrice);
                    model.OmsBidPercentOfFillPrice = DecodeDoubleNull2(message.OmsBidPercentOfFillPrice);
                    model.TotalDelta = DecodeDoubleNull4(message.TotalDelta);
                    model.HanweckTotalTheo = DecodeDoubleNull2(message.HanweckTotalTheo);
                    model.HanweckTotalGamma = DecodeDoubleNull4(message.HanweckTotalGamma);
                    model.HanweckTotalVega = DecodeDoubleNull4(message.HanweckTotalVega);
                    model.HanweckTotalTheta = DecodeDoubleNull4(message.HanweckTotalTheta);
                    model.HanweckTotalRho = DecodeDoubleNull4(message.HanweckTotalRho);
                    model.HanweckTotalIV = DecodeDoubleNull4(message.HanweckTotalIV);
                    model.HanweckTotalUnder = DecodeDoubleNull2(message.HanweckTotalUnder);
                    model.HanweckTotalUBid = DecodeDoubleNull2(message.HanweckTotalUBid);
                    model.HanweckTotalUAsk = DecodeDoubleNull2(message.HanweckTotalUAsk);
                    model.HanweckTotalBid = DecodeDoubleNull2(message.HanweckTotalBid);
                    model.HanweckTotalAsk = DecodeDoubleNull2(message.HanweckTotalAsk);
                    model.EdgeOverride = DecodeDoubleNull2(message.EdgeOverride);
                    model.AdjustedEdgeOverride = DecodeDoubleNull2(message.AdjustedEdgeOverride);
                    model.EdgeToTheo = DecodeDoubleNull2(message.EdgeToTheo);
                    model.TagEdgeToTheo = DecodeDoubleNull2(message.TagEdgeToTheo);
                    model.TagEdgeToEma = DecodeDoubleNull2(message.TagEdgeToEma);
                    model.TagEdgeToVolaV0 = DecodeDoubleNull2(message.TagEdgeToVolaV0);
                    model.TagEdgeToVolaV1 = DecodeDoubleNull2(message.TagEdgeToVolaV1);
                    model.TagEdgeToVolaV2 = DecodeDoubleNull2(message.TagEdgeToVolaV2);
                    model.TagBestBid = DecodeDoubleNull2(message.TagBestBid);
                    model.TagBestAsk = DecodeDoubleNull2(message.TagBestAsk);
                    model.TagMktMkrBid = DecodeDoubleNull2(message.TagMktMkrBid);
                    model.TagMktMkrAsk = DecodeDoubleNull2(message.TagMktMkrAsk);
                    model.InitialEdge = DecodeDoubleNull2(message.InitialEdge);
                    model.OpenEdge = DecodeDoubleNull2(message.OpenEdge);
                    model.CloseEdge = DecodeDoubleNull2(message.CloseEdge);
                    model.FirstEdgeAcquired = message.FirstEdgeAcquired == BooleanEnum.True;
                    model.FirstEdge = DecodeDoubleNull2(message.FirstEdge);
                    model.LastEdge = DecodeDoubleNull2(message.LastEdge);
                    model.DeltaAdjLastEdge = DecodeDoubleNull2(message.DeltaAdjLastEdge);
                    model.DeltaAdjLastEdgeNotional = DecodeDoubleNull2(message.DeltaAdjLastEdgeNotional);
                    model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);

                    model.DeltaAdjChange = DecodeDoubleNull2(message.DeltaAdjChange);
                    model.DeltaAdjChangeNotional = DecodeDoubleNull2(message.DeltaAdjChangeNotional);

                    model.EdgeScanFeedEdge = DecodeDoubleNull2(message.EdgeScanFeedEdge);
                    model.EdgeScanFeedTimespan = DecodeDoubleNull2(message.EdgeScanFeedTimespan);

                    model.EdgeScanFeedBuyPrice = DecodeDoubleNull2(message.EdgeScanFeedBuyPrice);
                    model.EdgeScanFeedBuyQty = message.EdgeScanFeedBuyQty;
                    model.EdgeScanFeedSellPrice = DecodeDoubleNull2(message.EdgeScanFeedSellPrice);
                    model.EdgeScanFeedSellQty = message.EdgeScanFeedSellQty;
                    model.EdgeScanFeedBuyTime = message.EdgeScanFeedBuyTime.FromUnixEpoch();
                    model.EdgeScanFeedSellTime = message.EdgeScanFeedSellTime.FromUnixEpoch();
                    model.EdgeScanFeedRespondLatency = DecodeDoubleNull2(message.EdgeScanFeedRespondLatency);

                    model.EdgeScanFeedConditionCode = (char)message.EdgeScanFeedConditionCode;
                    model.ResubmitCount = message.ResubmitCount;
                    model.TotalEstimatedResubmit = message.TotalEstimatedResubmit;

                    model.Side = message.AggressorSide == AggressorSide.Buy ? Side.Buy : Side.Sell;
                    model.OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus;
                    model.BaseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
                    model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;
                    model.TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce;

                    model.OrderSource = (Data.Enums.OrderSource)message.OrderSource;

                    model.Username = message.GetUsername();
                    model.UnderlyingSymbol = message.GetUnderlyingSymbol();

                    model.SubmitTime = message.SubmitTime.FromUnixEpoch();
                    model.LastUpdateTime = message.LastUpdateTime.FromUnixEpoch();
                    model.Timestamp = message.Timestamp.FromUnixEpoch();
                    model.NewStatusTimeStamp = message.NewStatusTimeStamp.FromUnixEpoch();

                    model.DeltaAdjustedTheo = DecodeDoubleNull2(message.DeltaAdjustedTheo);
                    model.BidSize = message.BidSize;
                    model.AskSize = message.AskSize;
                    model.UnderlyingBidSize = message.UnderlyingBidSize;
                    model.UnderlyingAskSize = message.UnderlyingAskSize;

                    model.EdgeType = (EdgeType)message.EdgeType;
                    model.Edge = DecodeDoubleNull2(message.Edge);
                    model.IsDeltaAdjusted = message.IsDeltaAdjusted == BooleanEnum.True;
                    model.LoopInitLatency = DecodeDoubleNull2(message.LoopInitLatency);
                    model.TagUnderBid = DecodeDoubleNull2(message.TagUnderBid);
                    model.TagUnderAsk = DecodeDoubleNull2(message.TagUnderAsk);
                    model.DigBid = DecodeDoubleNull2(message.DigBid);
                    model.DigAsk = DecodeDoubleNull2(message.DigAsk);
                    model.WeightedVega = DecodeDoubleNull2(message.WeightedVega);
                    model.DigBidSize = message.DigBidSize;
                    model.DigAskSize = message.DigAskSize;

                    model.IsTagged = message.IsTagged == BooleanEnum.True;

                    model.HardSide = message.HardSide == Generated.Side.NULL_VALUE ? null : (Side)message.HardSide;
                    model.HardSideDesignationTime = message.HardSideDesignationTime.FromUnixEpoch();
                    model.HardSideBuyGiveUp = DecodeDoubleNull2(message.HardSideBuyGiveUp);
                    model.HardSideSellGiveUp = DecodeDoubleNull2(message.HardSideSellGiveUp);
                    model.HardSideAtTrade = message.HardSideAtTrade == Generated.Side.NULL_VALUE ? null : (Side)message.HardSideAtTrade;
                    model.HardSideAtTradeDesignationTime = message.HardSideAtTradeDesignationTime.FromUnixEpoch();
                    model.HardSideAtTradeBuyGiveUp = DecodeDoubleNull2(message.HardSideAtTradeBuyGiveUp);
                    model.HardSideAtTradeSellGiveUp = DecodeDoubleNull2(message.HardSideAtTradeSellGiveUp);

                    model.EdgeGiveUp = DecodeDoubleNull2(message.EdgeGiveUp);
                    model.CloseSubs = DecodeDoubleNull2(message.CloseSubs);
                    model.OrderEdgeToTheo = DecodeDoubleNull2(message.OrderEdgeToTheo);

                    model.TimeValue = DecodeDoubleNull2(message.TimeValue);
                    model.IntrinsicValue = DecodeDoubleNull2(message.IntrinsicValue);
                    model.FVDivs = DecodeDoubleNull2(message.FVDivs);
                    model.UFwd = DecodeDoubleNull2(message.UFwd);
                    model.UFwdFactor = DecodeDoubleNull2(message.UFwdFactor);
                    model.BorrowCost = DecodeDoubleNull2(message.BorrowCost);
                    model.BorrowRate = DecodeDoubleNull2(message.BorrowRate);
                    model.UPrice = DecodeDoubleNull2(message.UPrice);
                    model.UTheo = DecodeDoubleNull2(message.UTheo);

                    model.SharedId = message.SharedId;
                    model.Sequence = message.Sequence;
                    model.TypeId = (ModuleType)message.TypeId;
                    model.SubTypeId = (SubType)message.SubTypeCode;
                    model.SubTypeSequence = message.SubTypeSequence;
                    model.Venue = message.Venue == MultipleOrderAdded.NoOrdersGroup.VenueNullValue ? null : (Venue)message.Venue;
                    model.CostOfHedging = DecodeDoubleNull2(message.CostOfHedging);

                    model.SubType = message.SubType == MultipleOrderAdded.NoOrdersGroup.SubTypeNullValue ? null : (OrderSubType)message.SubType;

                    MultipleOrderAdded.NoOrdersGroup.NoLegsGroup legs = message.NoLegs;
                    if (model.IsComplexOrder)
                    {
                        IComplexOrder complexOrderModel = (IComplexOrder)model;
                        while (legs.HasNext)
                        {
                            MultipleOrderAdded.NoOrdersGroup.NoLegsGroup nextLeg = legs.Next();

                            string legId = nextLeg.GetLegID();

                            IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);
                            legModel.LegID = legId;

                            legModel.Ratio = nextLeg.Ratio;
                            legModel.Quantity = nextLeg.Quantity;
                            legModel.LastQuantity = nextLeg.LastQuantity;
                            legModel.LeavesQuantity = nextLeg.LeavesQuantity;
                            legModel.CumulativeQuantity = nextLeg.CumulativeQuantity;

                            legModel.Fee1 = DecodeDoubleNull2(nextLeg.Fee1);
                            legModel.Fee2 = DecodeDoubleNull2(nextLeg.Fee2);
                            legModel.BrokerFee1 = DecodeDoubleNull2(nextLeg.BrokerFee1);
                            legModel.BrokerFee2 = DecodeDoubleNull2(nextLeg.BrokerFee2);
                            legModel.ExchangeFee1 = DecodeDoubleNull2(nextLeg.ExchangeFee1);
                            legModel.ExchangeFee2 = DecodeDoubleNull2(nextLeg.ExchangeFee2);
                            legModel.Delta = DecodeDoubleNull4(nextLeg.Delta);
                            legModel.TV = DecodeDoubleNull2(nextLeg.TV);
                            legModel.Ask = DecodeDoubleNull2(nextLeg.Ask);
                            legModel.Bid = DecodeDoubleNull2(nextLeg.Bid);
                            legModel.AveragePrice = DecodeDoubleNull2(nextLeg.AveragePrice);
                            legModel.LastPrice = DecodeDoubleNull2(nextLeg.LastPrice);
                            legModel.HanweckTV = DecodeDoubleNull2(nextLeg.HanweckTV);
                            legModel.HanweckGamma = DecodeDoubleNull4(nextLeg.HanweckGamma);
                            legModel.HanweckVega = DecodeDoubleNull4(nextLeg.HanweckVega);
                            legModel.HanweckTheta = DecodeDoubleNull4(nextLeg.HanweckTheta);
                            legModel.HanweckRho = DecodeDoubleNull4(nextLeg.HanweckRho);
                            legModel.HanweckIV = DecodeDoubleNull4(nextLeg.HanweckIV);
                            legModel.HanweckUnder = DecodeDoubleNull2(nextLeg.HanweckUnder);
                            legModel.HanweckUnderBid = DecodeDoubleNull2(nextLeg.HanweckUnderBid);
                            legModel.HanweckUnderAsk = DecodeDoubleNull2(nextLeg.HanweckUnderAsk);
                            legModel.HanweckBid = DecodeDoubleNull2(nextLeg.HanweckBid);
                            legModel.HanweckAsk = DecodeDoubleNull2(nextLeg.HanweckAsk);

                            legModel.DeltaAdjustedTheo = DecodeDoubleNull2(nextLeg.DeltaAdjustedTheo);
                            legModel.BidSize = nextLeg.BidSize;
                            legModel.AskSize = nextLeg.AskSize;

                            legModel.PositionEffect = (Data.Enums.PositionEffect)nextLeg.PositionEffect;
                            legModel.Side = nextLeg.LegSide == LegSide.BuySide ? Side.Buy : Side.Sell;
                            legModel.OrderStatus = (Data.Enums.OrderStatus)nextLeg.OrderStatus;

                            legModel.Timestamp = nextLeg.Timestamp.FromUnixEpoch();
                            legModel.LastUpdateTime = nextLeg.LastUpdateTime.FromUnixEpoch();
                            legModel.HanweckBidTime = nextLeg.HanweckBidTime.FromUnixEpoch();
                            legModel.HanweckAskTime = nextLeg.HanweckAskTime.FromUnixEpoch();
                            legModel.HanweckTimestamp = nextLeg.HanweckTimestamp.FromUnixEpoch();

                            legModel.TimeValue = DecodeDoubleNull2(nextLeg.TimeValue);
                            legModel.IntrinsicValue = DecodeDoubleNull2(nextLeg.IntrinsicValue);
                            legModel.FVDivs = DecodeDoubleNull2(nextLeg.FVDivs);
                            legModel.UFwd = DecodeDoubleNull2(nextLeg.UFwd);
                            legModel.UFwdFactor = DecodeDoubleNull2(nextLeg.UFwdFactor);
                            legModel.BorrowCost = DecodeDoubleNull2(nextLeg.BorrowCost);
                            legModel.BorrowRate = DecodeDoubleNull2(nextLeg.BorrowRate);
                            legModel.UPrice = DecodeDoubleNull2(nextLeg.UPrice);
                            legModel.UTheo = DecodeDoubleNull2(nextLeg.UTheo);

                            DecodeLegContraFields_MultipleOrderAdded(nextLeg, legModel);

                            legModel.PermID = nextLeg.GetPermID();
                            legModel.OrderID = nextLeg.GetOrderID();
                            legModel.Symbol = nextLeg.GetSymbol();
                        }
                    }

                    DecodeOrderContraFields_MultipleOrderAdded(message, model);

                    model.LastExchange = message.GetLastExchange();
                    model.Exchanges = message.GetExchanges();
                    model.Reason = message.GetReason();
                    model.Source = message.GetSource();
                    model.AccountAcronym = message.GetAccountAcronym();
                    model.Tag = message.GetTag();
                    model.Trader = message.GetTrader();
                    model.Type = message.GetOrderType();
                    model.OrderID = message.GetOrderID();
                    model.Route = message.GetRoute();
                    model.Destination = message.GetDestination();
                    model.Symbol = message.GetSymbol();
                    model.Description = message.GetDescription();
                    model.SpreadId = message.GetSpreadId();
                    model.FullTag = message.GetFullTag();
                    model.Comment = message.GetComment();
                    model.AutomationType = message.GetAutomationType();
                    model.SpreadHash = message.GetSpreadHash();
                    model.Tagger = message.GetTagger();
                    model.TaggedMessage = message.GetTaggedMessage();
                    orders.Add(model);
                }
                catch
                {
                    // ignored
                }
            }
            _orderFactory.MultipleOrderAdded(requestId, ref orders, totalQueued, lastMessageIndex);
        }

        private static void DecodeLegContraFields_MultipleOrderAdded(MultipleOrderAdded.NoOrdersGroup.NoLegsGroup leg, IComplexOrderLeg legModel)
        {
            var caps = leg.NoLegContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                legModel.ContraCapacities = list;
            }

            var brokers = leg.NoLegContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                legModel.ContraBrokerNames = list;
            }

            var cmtas = leg.NoLegContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                legModel.ContraCmtas = list;
            }

            var traders = leg.NoLegContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                legModel.ContraTraders = list;
            }
        }

        private static void DecodeOrderContraFields_MultipleOrderAdded(MultipleOrderAdded.NoOrdersGroup message, IOrder model)
        {
            var caps = message.NoContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                model.ContraCapacities = list;
            }

            var brokers = message.NoContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                model.ContraBrokerNames = list;
            }

            var cmtas = message.NoContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                model.ContraCmtas = list;
            }

            var traders = message.NoContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                model.ContraTraders = list;
            }
        }

        private void DecodeOrderUpdate(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            OrderUpdated message = new OrderUpdated();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermID();
            IOrder? model = _orderFactory.GetOrder(isComplex: message.IsComplexOrder == BooleanEnum.True, orderId: orderId);

            if (model == null)
            {
                return;
            }

            model.PermID = orderId;
            model.PartiallyFilled = message.PartiallyFilled == BooleanEnum.True;
            model.LastQuantity = message.LastQuantity;
            model.FilledQty = message.FilledQty;
            model.LeavesQuantity = message.LeavesQuantity;
            model.CumulativeQuantity = message.CumulativeQuantity;
            model.Quantity = message.Quantity;
            model.SpreadAvgPrice = DecodeDoubleNull2(message.SpreadAvgPrice);
            model.AveragePrice = DecodeDoubleNull2(message.AveragePrice);
            model.Price = DecodeDoubleNull2(message.Price);
            model.LastPrice = DecodeDoubleNull2(message.LastPrice);
            model.MinPrice = DecodeDoubleNull2(message.MinPrice);
            model.MaxPrice = DecodeDoubleNull2(message.MaxPrice);
            model.Fee1 = DecodeDoubleNull2(message.Fee1);
            model.Fee2 = DecodeDoubleNull2(message.Fee2);
            model.Bid = DecodeDoubleNull2(message.Bid);
            model.Ask = DecodeDoubleNull2(message.Ask);
            model.UnderBid = DecodeDoubleNull2(message.UnderBid);
            model.UnderAsk = DecodeDoubleNull2(message.UnderAsk);
            model.TV = DecodeDoubleNull2(message.TV);
            model.Delta = DecodeDoubleNull4(message.Delta);
            model.ExchangeFee1 = DecodeDoubleNull2(message.ExchangeFee1);
            model.ExchangeFee2 = DecodeDoubleNull2(message.ExchangeFee2);
            model.BrokerFee1 = DecodeDoubleNull2(message.BrokerFee1);
            model.BrokerFee2 = DecodeDoubleNull2(message.BrokerFee2);
            model.TotalContracts = DecodeDoubleNull2(message.TotalContracts);
            model.FillTime = DecodeDoubleNull2(message.FillTime);
            model.TradeToNewTime = DecodeDoubleNull2(message.TradeToNewTime);
            model.SubmitToNewTime = DecodeDoubleNull2(message.SubmitToNewTime);
            model.NewToCancelTime = DecodeDoubleNull2(message.NewToCancelTime);
            model.BidPercentOfFillPrice = DecodeDoubleNull2(message.BidPercentOfFillPrice);
            model.OmsBidPercentOfFillPrice = DecodeDoubleNull2(message.OmsBidPercentOfFillPrice);
            model.TotalDelta = DecodeDoubleNull4(message.TotalDelta);
            model.HanweckTotalTheo = DecodeDoubleNull2(message.HanweckTotalTheo);
            model.HanweckTotalGamma = DecodeDoubleNull4(message.HanweckTotalGamma);
            model.HanweckTotalVega = DecodeDoubleNull4(message.HanweckTotalVega);
            model.HanweckTotalTheta = DecodeDoubleNull4(message.HanweckTotalTheta);
            model.HanweckTotalRho = DecodeDoubleNull4(message.HanweckTotalRho);
            model.HanweckTotalIV = DecodeDoubleNull4(message.HanweckTotalIV);
            model.HanweckTotalUnder = DecodeDoubleNull2(message.HanweckTotalUnder);
            model.HanweckTotalUBid = DecodeDoubleNull2(message.HanweckTotalUBid);
            model.HanweckTotalUAsk = DecodeDoubleNull2(message.HanweckTotalUAsk);
            model.HanweckTotalBid = DecodeDoubleNull2(message.HanweckTotalBid);
            model.HanweckTotalAsk = DecodeDoubleNull2(message.HanweckTotalAsk);
            model.EdgeToTheo = DecodeDoubleNull2(message.EdgeToTheo);
            model.TagEdgeToTheo = DecodeDoubleNull2(message.TagEdgeToTheo);
            model.TagEdgeToEma = DecodeDoubleNull2(message.TagEdgeToEma);
            model.TagEdgeToVolaV0 = DecodeDoubleNull2(message.TagEdgeToVolaV0);
            model.TagEdgeToVolaV1 = DecodeDoubleNull2(message.TagEdgeToVolaV1);
            model.TagEdgeToVolaV2 = DecodeDoubleNull2(message.TagEdgeToVolaV2);
            model.InitialEdge = DecodeDoubleNull2(message.InitialEdge);
            model.OpenEdge = DecodeDoubleNull2(message.OpenEdge);
            model.CloseEdge = DecodeDoubleNull2(message.CloseEdge);

            model.LastUpdateTime = message.LastUpdateTime.FromUnixEpoch();
            model.NewStatusTimeStamp = message.NewStatusTimeStamp.FromUnixEpoch();

            model.OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus;

            model.DeltaAdjustedTheo = DecodeDoubleNull2(message.DeltaAdjustedTheo);
            model.BidSize = message.BidSize;
            model.AskSize = message.AskSize;
            model.UnderlyingBidSize = message.UnderlyingBidSize;
            model.UnderlyingAskSize = message.UnderlyingAskSize;

            model.LastEdge = DecodeDoubleNull2(message.LastEdge);
            model.DeltaAdjLastEdge = DecodeDoubleNull2(message.DeltaAdjLastEdge);
            model.DeltaAdjLastEdgeNotional = DecodeDoubleNull2(message.DeltaAdjLastEdgeNotional);
            model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);

            model.DeltaAdjChange = DecodeDoubleNull2(message.DeltaAdjChange);
            model.DeltaAdjChangeNotional = DecodeDoubleNull2(message.DeltaAdjChangeNotional);

            model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;

            model.LoopInitLatency = DecodeDoubleNull2(message.LoopInitLatency);
            model.IsTagged = message.IsTagged == BooleanEnum.True;

            model.HardSideAtTrade = message.HardSideAtTrade == Generated.Side.NULL_VALUE ? null : (Side)message.HardSideAtTrade;
            model.HardSideAtTradeDesignationTime = message.HardSideAtTradeDesignationTime.FromUnixEpoch();

            model.EdgeGiveUp = DecodeDoubleNull2(message.EdgeGiveUp);
            model.CloseSubs = DecodeDoubleNull2(message.CloseSubs);
            model.OrderEdgeToTheo = DecodeDoubleNull2(message.OrderEdgeToTheo);

            model.TimeValue = DecodeDoubleNull2(message.TimeValue);
            model.IntrinsicValue = DecodeDoubleNull2(message.IntrinsicValue);
            model.FVDivs = DecodeDoubleNull2(message.FVDivs);
            model.UFwd = DecodeDoubleNull2(message.UFwd);
            model.UFwdFactor = DecodeDoubleNull2(message.UFwdFactor);
            model.BorrowCost = DecodeDoubleNull2(message.BorrowCost);
            model.BorrowRate = DecodeDoubleNull2(message.BorrowRate);
            model.UPrice = DecodeDoubleNull2(message.UPrice);
            model.UTheo = DecodeDoubleNull2(message.UTheo);
            model.CostOfHedging = DecodeDoubleNull2(message.CostOfHedging);
            model.DigBid = DecodeDoubleNull2(message.DigBid);
            model.DigAsk = DecodeDoubleNull2(message.DigAsk);
            model.WeightedVega = DecodeDoubleNull2(message.WeightedVega);
            model.DigBidSize = message.DigBidSize;
            model.DigAskSize = message.DigAskSize;

            OrderUpdated.NoLegsGroup legs = message.NoLegs;
            if (model.IsComplexOrder)
            {
                IComplexOrder complexOrderModel = (IComplexOrder)model;
                while (legs.HasNext)
                {
                    OrderUpdated.NoLegsGroup nextLeg = legs.Next();
                    string legId = nextLeg.GetLegID();
                    IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);

                    legModel.LastQuantity = nextLeg.LastQuantity;
                    legModel.LeavesQuantity = nextLeg.LeavesQuantity;
                    legModel.CumulativeQuantity = nextLeg.CumulativeQuantity;

                    legModel.ExchangeFee2 = DecodeDoubleNull2(nextLeg.ExchangeFee2);
                    legModel.ExchangeFee1 = DecodeDoubleNull2(nextLeg.ExchangeFee1);
                    legModel.Fee2 = DecodeDoubleNull2(nextLeg.Fee2);
                    legModel.Fee1 = DecodeDoubleNull2(nextLeg.Fee1);
                    legModel.Delta = DecodeDoubleNull4(nextLeg.Delta);
                    legModel.TV = DecodeDoubleNull2(nextLeg.TV);
                    legModel.Ask = DecodeDoubleNull2(nextLeg.Ask);
                    legModel.Bid = DecodeDoubleNull2(nextLeg.Bid);
                    legModel.AveragePrice = DecodeDoubleNull2(nextLeg.AveragePrice);
                    legModel.LastPrice = DecodeDoubleNull2(nextLeg.LastPrice);
                    legModel.BrokerFee1 = DecodeDoubleNull2(nextLeg.BrokerFee1);
                    legModel.BrokerFee2 = DecodeDoubleNull2(nextLeg.BrokerFee2);
                    legModel.HanweckTV = DecodeDoubleNull2(nextLeg.HanweckTV);
                    legModel.HanweckGamma = DecodeDoubleNull4(nextLeg.HanweckGamma);
                    legModel.HanweckVega = DecodeDoubleNull2(nextLeg.HanweckVega);
                    legModel.HanweckTheta = DecodeDoubleNull2(nextLeg.HanweckTheta);
                    legModel.HanweckRho = DecodeDoubleNull2(nextLeg.HanweckRho);
                    legModel.HanweckIV = DecodeDoubleNull2(nextLeg.HanweckIV);
                    legModel.HanweckUnder = DecodeDoubleNull2(nextLeg.HanweckUnder);
                    legModel.HanweckUnderBid = DecodeDoubleNull2(nextLeg.HanweckUnderBid);
                    legModel.HanweckUnderAsk = DecodeDoubleNull2(nextLeg.HanweckUnderAsk);
                    legModel.HanweckBid = DecodeDoubleNull2(nextLeg.HanweckBid);
                    legModel.HanweckAsk = DecodeDoubleNull2(nextLeg.HanweckAsk);

                    legModel.OrderStatus = (Data.Enums.OrderStatus)nextLeg.OrderStatus;

                    legModel.LastUpdateTime = nextLeg.LastUpdateTime.FromUnixEpoch();
                    legModel.HanweckBidTime = nextLeg.HanweckBidTime.FromUnixEpoch();
                    legModel.HanweckAskTime = nextLeg.HanweckAskTime.FromUnixEpoch();
                    legModel.HanweckTimestamp = nextLeg.HanweckTimestamp.FromUnixEpoch();

                    legModel.TimeValue = DecodeDoubleNull2(nextLeg.TimeValue);
                    legModel.IntrinsicValue = DecodeDoubleNull2(nextLeg.IntrinsicValue);
                    legModel.FVDivs = DecodeDoubleNull2(nextLeg.FVDivs);
                    legModel.UFwd = DecodeDoubleNull2(nextLeg.UFwd);
                    legModel.UFwdFactor = DecodeDoubleNull2(nextLeg.UFwdFactor);
                    legModel.BorrowCost = DecodeDoubleNull2(nextLeg.BorrowCost);
                    legModel.BorrowRate = DecodeDoubleNull2(nextLeg.BorrowRate);
                    legModel.UPrice = DecodeDoubleNull2(nextLeg.UPrice);
                    legModel.UTheo = DecodeDoubleNull2(nextLeg.UTheo);

                    legModel.DeltaAdjustedTheo = DecodeDoubleNull2(nextLeg.DeltaAdjustedTheo);
                    legModel.BidSize = nextLeg.BidSize;
                    legModel.AskSize = nextLeg.AskSize;

                    DecodeLegContraFields_OrderUpdated(nextLeg, legModel);
                }
            }

            DecodeOrderContraFields_OrderUpdated(message, model);

            model.LastExchange = message.GetLastExchange();
            model.Exchanges = message.GetExchanges();
            model.Reason = message.GetReason();
            model.OrderID = message.GetOrderID();
            model.Tagger = message.GetTagger();
            model.TaggedMessage = message.GetTaggedMessage();
            _orderFactory.OrderUpdated(model);
        }

        private static void DecodeLegContraFields_OrderUpdated(OrderUpdated.NoLegsGroup leg, IComplexOrderLeg legModel)
        {
            var caps = leg.NoLegContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                legModel.ContraCapacities = list;
            }

            var brokers = leg.NoLegContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                legModel.ContraBrokerNames = list;
            }

            var cmtas = leg.NoLegContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                legModel.ContraCmtas = list;
            }

            var traders = leg.NoLegContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                legModel.ContraTraders = list;
            }
        }

        private static void DecodeOrderContraFields_OrderUpdated(OrderUpdated message, IOrder model)
        {
            var caps = message.NoContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                model.ContraCapacities = list;
            }

            var brokers = message.NoContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                model.ContraBrokerNames = list;
            }

            var cmtas = message.NoContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                model.ContraCmtas = list;
            }

            var traders = message.NoContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                model.ContraTraders = list;
            }
        }

        private void DecodeOrderIndicatorUpdate(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            OrderIndicatorsUpdate message = new OrderIndicatorsUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermID();
            IOrder? model = _orderFactory.GetOrder(isComplex: message.IsComplexOrder == BooleanEnum.True, orderId: orderId);

            if (model == null)
            {
                return;
            }

            model.IsFirstFill = message.IsFirstFill == BooleanEnum.True;
            model.IsCitadel = message.IsCitadel == BooleanEnum.True;
            model.FirstEdgeAcquired = message.FirstEdgeAcquired == BooleanEnum.True;
            switch (message.CitadelSide)
            {
                case AggressorSide.NoAggressor:
                    model.CitadelSide = null;
                    break;
                case AggressorSide.Buy:
                    model.CitadelSide = Side.Buy;
                    break;
                case AggressorSide.Sell:
                    model.CitadelSide = Side.Sell;
                    break;
            }
            switch (message.SpreadPositionEffect)
            {
                case Generated.PositionEffect.Open:
                    model.SpreadPositionEffect = Data.Enums.PositionEffect.Open;
                    break;
                case Generated.PositionEffect.Close:
                    model.SpreadPositionEffect = Data.Enums.PositionEffect.Close;
                    break;
                case Generated.PositionEffect.AUTO:
                    model.SpreadPositionEffect = null;
                    break;
            }
            model.FirstEdge = DecodeDoubleNull2(message.FirstEdge);
            model.LastEdge = DecodeDoubleNull2(message.LastEdge);
            model.DeltaAdjLastEdge = DecodeDoubleNull2(message.DeltaAdjLastEdge);
            model.DeltaAdjLastEdgeNotional = DecodeDoubleNull2(message.DeltaAdjLastEdgeNotional);
            model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);
            model.DeltaAdjChange = DecodeDoubleNull2(message.DeltaAdjChange);
            model.DeltaAdjChangeNotional = DecodeDoubleNull2(message.DeltaAdjChangeNotional);
            model.LoopInitLatency = DecodeDoubleNull2(message.LoopInitLatency);
            model.IsTagged = message.IsTagged == BooleanEnum.True;
            model.EdgeGiveUp = DecodeDoubleNull2(message.EdgeGiveUp);
            model.CloseSubs = DecodeDoubleNull2(message.CloseSubs);

            model.Tagger = message.GetTagger();
            model.TaggedMessage = message.GetTaggedMessage();

            _orderFactory.OrderIndicatorUpdated(model);
        }

        private void DecodeOrderTagUpdate(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            OrderTagUpdate message = new OrderTagUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermID();
            var found = _orderFactory.GetExistingOrder(orderId: orderId, out var model);

            if (!found)
            {
                return;
            }

            model!.TagEdge = DecodeDoubleNull2(message.TagEdge);
            model.TagMid = DecodeDoubleNull2(message.TagMid);
            model.TagBid = DecodeDoubleNull2(message.TagBid);
            model.TagAsk = DecodeDoubleNull2(message.TagAsk);
            model.TagUnderBid = DecodeDoubleNull2(message.TagUnderBid);
            model.TagUnderAsk = DecodeDoubleNull2(message.TagUnderAsk);
            model.TagTheo = DecodeDoubleNull2(message.TagTheo);
            model.TagVolaV0 = DecodeDoubleNull2(message.TagVolaV0);
            model.TagVolaV1 = DecodeDoubleNull2(message.TagVolaV1);
            model.TagVolaV2 = DecodeDoubleNull2(message.TagVolaV2);
            model.TagEma = DecodeDoubleNull2(message.TagEma);
            model.VolaIv = message.TagVolaIv;
            model.TheoBid = DecodeDoubleNull2(message.TheoBid);
            model.TheoAsk = DecodeDoubleNull2(message.TheoAsk);

            model.SharedId = message.SharedId;
            model.Sequence = message.Sequence;
            model.TypeId = (ModuleType)message.TypeId;
            model.SubTypeId = (SubType)message.SubTypeCode;
            model.SubTypeSequence = message.SubTypeSequence;
            model.SubType = message.SubType == OrderTagUpdate.SubTypeNullValue ? null : (OrderSubType)message.SubType;

            model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);
            model.EdgeScanFeedEdge = DecodeDoubleNull2(message.EdgeScanFeedEdge);
            model.EdgeScanFeedTimespan = DecodeDoubleNull2(message.EdgeScanFeedTimespan);
            model.EdgeScanFeedBuyPrice = DecodeDoubleNull2(message.EdgeScanFeedBuyPrice);
            model.EdgeScanFeedSellPrice = DecodeDoubleNull2(message.EdgeScanFeedSellPrice);

            model.EdgeScanFeedBuyQty = message.EdgeScanFeedBuyQty;
            model.EdgeScanFeedSellQty = message.EdgeScanFeedSellQty;

            model.EdgeScanFeedBuyTime = message.EdgeScanFeedBuyTime.FromUnixEpoch();
            model.EdgeScanFeedSellTime = message.EdgeScanFeedSellTime.FromUnixEpoch();

            model.EdgeScanFeedRespondLatency = DecodeDoubleNull2(message.EdgeScanFeedRespondLatency);
            model.EdgeScanFeedConditionCode = (char)message.EdgeScanFeedConditionCode;


            model.TradeToNewTime = DecodeDoubleNull2(message.TradeToNewTime);
            model.OmsBidPercentOfFillPrice = DecodeDoubleNull2(message.OmsBidPercentOfFillPrice);
            model.OrderSource = (OrderSource)message.OrderSource;
            model.EdgeToTheo = DecodeDoubleNull2(message.EdgeToTheo);
            model.TagEdgeToTheo = DecodeDoubleNull2(message.TagEdgeToTheo);
            model.TagEdgeToEma = DecodeDoubleNull2(message.TagEdgeToEma);
            model.TagEdgeToVolaV0 = DecodeDoubleNull2(message.TagEdgeToVolaV0);
            model.TagEdgeToVolaV1 = DecodeDoubleNull2(message.TagEdgeToVolaV1);
            model.TagEdgeToVolaV2 = DecodeDoubleNull2(message.TagEdgeToVolaV2);
            model.OrderEdgeToTheo = DecodeDoubleNull2(message.OrderEdgeToTheo);
            model.InitialEdge = DecodeDoubleNull2(message.InitialEdge);
            model.OpenEdge = DecodeDoubleNull2(message.OpenEdge);
            model.CloseEdge = DecodeDoubleNull2(message.CloseEdge);
            model.CostOfHedging = DecodeDoubleNull2(message.CostOfHedging);
            model.EdgeType = (EdgeType)message.EdgeType;
            model.DigBid = DecodeDoubleNull2(message.DigBid);
            model.DigAsk = DecodeDoubleNull2(message.DigAsk);
            model.WeightedVega = DecodeDoubleNull2(message.WeightedVega);
            model.DigBidSize = message.DigBidSize;
            model.DigAskSize = message.DigAskSize;

            model.Comment = message.GetComment();
            model.Reason = message.GetReason();
            model.AutomationType = message.GetAutomationType();
            model.Tag = message.GetTag();
            model.Trader = message.GetTrader();

            _orderFactory.OrderTagUpdated(model);
        }

        private void DecodePortfolioAddedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            PortfolioAddedMessage message = new PortfolioAddedMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.PortfolioId;
            PortfolioType portfolioType = (PortfolioType)message.PortfolioType;
            IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType);

            portfolio.Id = id;
            portfolio.PortfolioType = portfolioType;
            portfolio.PortfolioDate = message.PortfolioDate.FromUnixEpoch();

            portfolio.Name = message.GetPortfolioName();
            _portfolioManager.PortfolioAdded(portfolio);
        }

        private void DecodePortfolioPositionAddedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            PositionAddedMessage message = new PositionAddedMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.PortfolioId;
            PortfolioType portfolioType = (PortfolioType)message.PortfolioType;
            IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType);
            portfolio.Id = id;
            portfolio.PortfolioType = portfolioType;
            portfolio.PortfolioDate = message.PortfolioDate.FromUnixEpoch();

            int positionId = message.PositionId;
            int parentPositionId = message.ParentPositionId;
            PositionType type = (PositionType)message.PositionType;
            IPosition position;
            if (parentPositionId <= 0)
            {
                position = portfolio.GetPosition(positionId, type);
            }
            else
            {
                IPosition parentPosition = portfolio.GetPosition(parentPositionId, PositionType.Underlying);
                position = parentPosition.GetPosition(positionId, PositionType.Expiration);
            }
            position.ParentPositionId = parentPositionId;
            position.Id = positionId;
            position.PositionType = type;
            position.PositionDate = message.PositionDate.FromUnixEpoch();

            portfolio.Name = message.GetPortfolioName();
            position.Name = message.GetPositionName();
            position.Symbol = message.GetPositionSymbol();
            portfolio.AddPosition(position);
            _portfolioManager.PositionAdded(portfolio, position);
        }

        private void DecodePortfolioUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            PortfolioUpdateMessage message = new PortfolioUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.PortfolioId;
            PortfolioType portfolioType = (PortfolioType)message.PortfolioType;
            IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType);
            portfolio.Id = id;
            portfolio.PortfolioType = portfolioType;
            portfolio.TotalSubmissions = message.TotalSubmissions;
            portfolio.TotalSingleLegSubmissions = message.TotalSingleLegSubmissions;
            portfolio.TotalSpreadSubmissions = message.TotalSpreadSubmissions;
            portfolio.TotalSingleFills = message.TotalSingleFills;
            portfolio.TotalSpreadFills = message.TotalSpreadFills;
            portfolio.UniqueSubmissions = message.UniqueSubmissions;
            portfolio.UniqueSpreadSubmissions = message.UniqueSpreadSubmissions;
            portfolio.TotalFills = message.TotalFills;
            portfolio.UniqueFills = message.UniqueFills;
            portfolio.UniqueSpreadFills = message.UniqueSpreadFills;
            portfolio.StockContracts = message.StockContracts;
            portfolio.TotalContracts = message.TotalContracts;
            portfolio.UniqueContracts = message.UniqueContracts;
            portfolio.UniqueSpreadContracts = message.UniqueSpreadContracts;
            portfolio.NetQty = message.NetQty;
            portfolio.ShortQty = message.ShortQty;
            portfolio.LongQty = message.LongQty;
            portfolio.FillRate = DecodeDoubleNull4(message.FillRate);
            portfolio.OrderFillRate = DecodeDoubleNull4(message.OrderFillRate);
            portfolio.IbOrderFillRate = DecodeDoubleNull4(message.IbOrderFillRate);
            portfolio.LowestRealizedPnl = DecodeDoubleNull2(message.LowestRealizedPnl);
            portfolio.HighestRealizedPnl = DecodeDoubleNull2(message.HighestRealizedPnl);
            portfolio.RealizedPnl = DecodeDoubleNull2(message.RealizedPnl);
            portfolio.LowestAdjustedPnl = DecodeDoubleNull2(message.LowestAdjustedPnl);
            portfolio.HighestAdjustedPnl = DecodeDoubleNull2(message.HighestAdjustedPnl);
            portfolio.AdjustedPnl = DecodeDoubleNull2(message.AdjustedPnl);
            portfolio.SingleLegAdjustedPnl = DecodeDoubleNull2(message.SingleLegAdjustedPnl);
            portfolio.SpreadAdjustedPnl = DecodeDoubleNull2(message.SpreadAdjustedPnl);
            portfolio.UnrealizedPnl = DecodeDoubleNull2(message.UnrealizedPnl);
            portfolio.NetDelta = DecodeDoubleNull2(message.NetDelta);

            portfolio.MaxResubmitEstimate = message.MaxResubmitEstimate;
            portfolio.MaxResubmitForFill = message.MaxResubmitForFill;
            portfolio.AvgResubmitEstimate = message.AvgResubmitEstimate;
            portfolio.AvgResubmitForFill = message.AvgResubmitForFill;

            portfolio.DeltaAdjustedBurn = DecodeDoubleNull2(message.DeltaAdjustedBurn);
            portfolio.DeltaAdjustedHelp = DecodeDoubleNull2(message.DeltaAdjustedHelp);
            portfolio.HighestOpenNotional = DecodeDoubleNull2(message.HighestOpenNotional);
            portfolio.TotalOpenNotional = DecodeDoubleNull2(message.TotalOpenNotional);

            portfolio.TotalOutOfMarketOrders = message.TotalOutOfMarketOrders;
            portfolio.TotalOutOfMarketFills = message.TotalOutOfMarketFills;

            portfolio.SubmissionRatePerSec = message.SubmissionRatePerSec;
            portfolio.MaxOrdersPerSec = message.MaxOrdersPerSec;

            portfolio.WinnerTrades = message.WinnerTrades;
            portfolio.LoserTrades = message.LoserTrades;
            portfolio.SizeWinnerTrades = message.SizeWinnerTrades;
            portfolio.SizeLoserTrades = message.SizeLoserTrades;
            portfolio.AvgCloseSubs = message.AvgCloseSubs;

            portfolio.IntroducingBrokerFee = DecodeDoubleNull2(message.IntroducingBrokerFee);
            portfolio.ExecutingBrokerFee = DecodeDoubleNull2(message.ExecutingBrokerFee);
            portfolio.ExchangeFee = DecodeDoubleNull2(message.ExchangeFee);
            portfolio.OrfFee = DecodeDoubleNull2(message.OrfFee);
            portfolio.SecFee = DecodeDoubleNull2(message.SecFee);
            portfolio.TotalFees = DecodeDoubleNull2(message.TotalFees);
            portfolio.AvgOpenSubsCount = DecodeDoubleNull2(message.AvgOpenSubsCount);
            portfolio.AvgSubsBetweenFillsCount = DecodeDoubleNull2(message.AvgSubsBetweenFillsCount);

            portfolio.GroupSubmissionsAvg = message.GroupSubmissionsAvg;
            portfolio.FillRate = DecodeDoubleNull4(message.GroupAvgFillRate);

            var isReplay = message.IsReplay == BooleanEnum.True;

            List<IPosition> positionsList = new List<IPosition>();
            PortfolioUpdateMessage.PositionsGroup positions = message.Positions;
            while (positions.HasNext)
            {
                PortfolioUpdateMessage.PositionsGroup positionMessage = positions.Next();

                int positionId = positionMessage.PositionId;
                int parentPositionId = positionMessage.ParentPositionId;
                PositionType type = (PositionType)positionMessage.PositionType;
                IPosition position;
                if (parentPositionId <= 0)
                {
                    position = portfolio.GetPosition(positionId, type);
                }
                else
                {
                    IPosition parentPosition = portfolio.GetPosition(parentPositionId, PositionType.Underlying);
                    position = parentPosition.GetPosition(positionId, PositionType.Expiration);
                }
                position.ParentPositionId = parentPositionId;
                position.Id = positionId;
                position.PositionType = type;
                position.NetQty = positionMessage.NetQty;
                position.RealizedPnl = DecodeDoubleNull2(positionMessage.RealizedPnl);
                position.AdjustedPnl = DecodeDoubleNull2(positionMessage.AdjustedPnl);
                position.UnrealizedPnl = DecodeDoubleNull2(positionMessage.UnrealizedPnl);
                position.NetDelta = DecodeDoubleNull2(positionMessage.NetDelta);
                position.BestSellPrice = DecodeDoubleNull2(positionMessage.BestSellPrice);
                position.BestSellPriceUnderMid = DecodeDoubleNull2(positionMessage.BestSellPriceUnderMid);
                position.BestBuyPrice = DecodeDoubleNull2(positionMessage.BestBuyPrice);
                position.BestBuyPriceUnderMid = DecodeDoubleNull2(positionMessage.BestBuyPriceUnderMid);
                position.TotalSubmissions = positionMessage.TotalSubmissions;
                position.TotalSingleLegSubmissions = positionMessage.TotalSingleLegSubmissions;
                position.TotalSpreadSubmissions = positionMessage.TotalSpreadSubmissions;
                position.TotalSingleFills = positionMessage.TotalSingleFills;
                position.TotalSpreadFills = positionMessage.TotalSpreadFills;
                position.UniqueSubmissions = positionMessage.UniqueSubmissions;
                position.TotalFills = positionMessage.TotalFills;
                position.UniqueFills = positionMessage.UniqueFills;
                position.TotalContracts = positionMessage.TotalContracts;
                position.UniqueContracts = positionMessage.UniqueContracts;
                position.FillRate = DecodeDoubleNull4(positionMessage.FillRate);
                position.OrderFillRate = DecodeDoubleNull4(positionMessage.OrderFillRate);
                position.IbOrderFillRate = DecodeDoubleNull4(positionMessage.IbOrderFillRate);
                position.OpenPositionAveragePrice = DecodeDoubleNull2(positionMessage.OpenPositionAveragePrice);
                position.OpenPositionFillUnderPrice = DecodeDoubleNull2(positionMessage.OpenPositionFillUnderPrice);
                position.LastTradeTime = positionMessage.LastTradeTime.FromUnixEpoch();
                position.LastEdge = DecodeDoubleNull2(positionMessage.LastEdge);
                position.LastBuyEdge = DecodeDoubleNull2(positionMessage.LastBuyEdge);
                position.LastSellEdge = DecodeDoubleNull2(positionMessage.LastSellEdge);
                position.LastBuyEdgeToTheo = DecodeDoubleNull2(positionMessage.LastBuyEdgeToTheo);
                position.LastSellEdgeToTheo = DecodeDoubleNull2(positionMessage.LastSellEdgeToTheo);
                position.LastBuyFillEdgeToTheo = DecodeDoubleNull2(positionMessage.LastBuyFillEdgeToTheo);
                position.LastSellFillEdgeToTheo = DecodeDoubleNull2(positionMessage.LastSellFillEdgeToTheo);
                position.LastBuyAttemptEdgeToTheo = DecodeDoubleNull2(positionMessage.LastBuyAttemptEdgeToTheo);
                position.LastSellAttemptEdgeToTheo = DecodeDoubleNull2(positionMessage.LastSellAttemptEdgeToTheo);
                position.LastPermBuyFillEdgeToTheo = DecodeDoubleNull2(positionMessage.LastPermBuyFillEdgeToTheo);
                position.LastPermSellFillEdgeToTheo = DecodeDoubleNull2(positionMessage.LastPermSellFillEdgeToTheo);
                position.LastPermBuyAttemptEdgeToTheo = DecodeDoubleNull2(positionMessage.LastPermBuyAttemptEdgeToTheo);
                position.LastPermSellAttemptEdgeToTheo = DecodeDoubleNull2(positionMessage.LastPermSellAttemptEdgeToTheo);
                position.BestBuyEdgeToTheo = DecodeDoubleNull2(positionMessage.BestBuyEdgeToTheo);
                position.WorstBuyEdgeToTheo = DecodeDoubleNull2(positionMessage.WorstBuyEdgeToTheo);
                position.BestSellEdgeToTheo = DecodeDoubleNull2(positionMessage.BestSellEdgeToTheo);
                position.WorstSellEdgeToTheo = DecodeDoubleNull2(positionMessage.WorstSellEdgeToTheo);
                position.OpenNotional = DecodeDoubleNull2(positionMessage.OpenNotional);

                position.MaxResubmitEstimate = positionMessage.MaxResubmitEstimate;
                position.MaxResubmitForFill = positionMessage.MaxResubmitForFill;
                position.AvgResubmitEstimate = positionMessage.AvgResubmitEstimate;
                position.AvgResubmitForFill = positionMessage.AvgResubmitForFill;

                position.FirstEdge = DecodeDoubleNull2(positionMessage.FirstEdge);

                position.TotalOutOfMarketOrders = positionMessage.TotalOutOfMarketOrders;
                position.TotalOutOfMarketFills = positionMessage.TotalOutOfMarketFills;

                position.HardSide = positionMessage.HardSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.HardSide;
                position.HardSideDesignationTime = positionMessage.HardSideDesignationTime.FromUnixEpoch();
                position.HardSideBuyGiveUp = DecodeDoubleNull2(positionMessage.HardSideBuyGiveUp);
                position.HardSideSellGiveUp = DecodeDoubleNull2(positionMessage.HardSideSellGiveUp);

                position.SubmissionRatePerSec = positionMessage.SubmissionRatePerSec;
                position.MaxOrdersPerSec = positionMessage.MaxOrdersPerSec;

                position.WinnerTrades = positionMessage.WinnerTrades;
                position.LoserTrades = positionMessage.LoserTrades;
                position.SizeWinnerTrades = positionMessage.SizeWinnerTrades;
                position.SizeLoserTrades = positionMessage.SizeLoserTrades;
                position.AvgCloseSubs = positionMessage.AvgCloseSubs;
                position.OpenSubsCount = positionMessage.OpenSubsCount;
                position.SubsBetweenFillsCount = positionMessage.SubsBetweenFillsCount;

                position.IntroducingBrokerFee = DecodeDoubleNull2(positionMessage.IntroducingBrokerFee);
                position.ExecutingBrokerFee = DecodeDoubleNull2(positionMessage.ExecutingBrokerFee);
                position.ExchangeFee = DecodeDoubleNull2(positionMessage.ExchangeFee);
                position.OrfFee = DecodeDoubleNull2(positionMessage.OrfFee);
                position.SecFee = DecodeDoubleNull2(positionMessage.SecFee);
                position.TotalFees = DecodeDoubleNull2(positionMessage.TotalFees);
                position.LastTradeSide = positionMessage.LastTradeSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.LastTradeSide;

                position.LastBuyAttempt = DecodeDoubleNull2(positionMessage.LastBuyAttempt);
                position.LastBuyAttemptUnderlying = DecodeDoubleNull2(positionMessage.LastBuyAttemptUnderlying);
                position.LastSellAttempt = DecodeDoubleNull2(positionMessage.LastSellAttempt);
                position.LastSellAttemptUnderlying = DecodeDoubleNull2(positionMessage.LastSellAttemptUnderlying);

                position.RawNetQty = positionMessage.RawNetQty;

                position.LastInstanceId = positionMessage.LastInstance;
                position.LastTraderId = positionMessage.LastTrader;
                position.AccountId = positionMessage.Account;

                position.SingleLegAdjustedPnl = DecodeDoubleNull2(positionMessage.SingleLegAdjustedPnl);
                position.SpreadAdjustedPnl = DecodeDoubleNull2(positionMessage.SpreadAdjustedPnl);

                portfolio.AddPosition(position);
                positionsList.Add(position);
            }

            _portfolioManager.PositionUpdated(portfolio, positionsList, isReplay);
        }

        private void DecodePortfolioUpdateMessageSlim(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            PortfolioUpdateMessageSlim message = new PortfolioUpdateMessageSlim();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.PortfolioId;
            PortfolioType portfolioType = (PortfolioType)message.PortfolioType;
            IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType);
            portfolio.Id = id;
            portfolio.PortfolioType = portfolioType;
            portfolio.TotalSubmissions = message.TotalSubmissions;
            portfolio.TotalSingleLegSubmissions = message.TotalSingleLegSubmissions;
            portfolio.TotalSpreadSubmissions = message.TotalSpreadSubmissions;
            portfolio.TotalSingleFills = message.TotalSingleFills;
            portfolio.TotalSpreadFills = message.TotalSpreadFills;
            portfolio.UniqueSubmissions = message.UniqueSubmissions;
            portfolio.UniqueSpreadSubmissions = message.UniqueSpreadSubmissions;
            portfolio.TotalFills = message.TotalFills;
            portfolio.UniqueFills = message.UniqueFills;
            portfolio.UniqueSpreadFills = message.UniqueSpreadFills;
            portfolio.StockContracts = message.StockContracts;
            portfolio.TotalContracts = message.TotalContracts;
            portfolio.UniqueContracts = message.UniqueContracts;
            portfolio.UniqueSpreadContracts = message.UniqueSpreadContracts;
            portfolio.NetQty = message.NetQty;
            portfolio.ShortQty = message.ShortQty;
            portfolio.LongQty = message.LongQty;
            portfolio.FillRate = DecodeDoubleNull4(message.FillRate);
            portfolio.OrderFillRate = DecodeDoubleNull4(message.OrderFillRate);
            portfolio.IbOrderFillRate = DecodeDoubleNull4(message.IbOrderFillRate);
            portfolio.RealizedPnl = DecodeDoubleNull2(message.RealizedPnl);
            portfolio.AdjustedPnl = DecodeDoubleNull2(message.AdjustedPnl);
            portfolio.SingleLegAdjustedPnl = DecodeDoubleNull2(message.SingleLegAdjustedPnl);
            portfolio.SpreadAdjustedPnl = DecodeDoubleNull2(message.SpreadAdjustedPnl);
            portfolio.UnrealizedPnl = DecodeDoubleNull2(message.UnrealizedPnl);
            portfolio.NetDelta = DecodeDoubleNull2(message.NetDelta);

            portfolio.MaxResubmitEstimate = message.MaxResubmitEstimate;
            portfolio.MaxResubmitForFill = message.MaxResubmitForFill;
            portfolio.AvgResubmitEstimate = message.AvgResubmitEstimate;
            portfolio.AvgResubmitForFill = message.AvgResubmitForFill;

            portfolio.DeltaAdjustedBurn = DecodeDoubleNull2(message.DeltaAdjustedBurn);
            portfolio.DeltaAdjustedHelp = DecodeDoubleNull2(message.DeltaAdjustedHelp);
            portfolio.HighestOpenNotional = DecodeDoubleNull2(message.HighestOpenNotional);
            portfolio.TotalOpenNotional = DecodeDoubleNull2(message.TotalOpenNotional);

            portfolio.TotalOutOfMarketOrders = message.TotalOutOfMarketOrders;
            portfolio.TotalOutOfMarketFills = message.TotalOutOfMarketFills;

            portfolio.SubmissionRatePerSec = message.SubmissionRatePerSec;
            portfolio.MaxOrdersPerSec = message.MaxOrdersPerSec;

            portfolio.WinnerTrades = message.WinnerTrades;
            portfolio.LoserTrades = message.LoserTrades;
            portfolio.SizeWinnerTrades = message.SizeWinnerTrades;
            portfolio.SizeLoserTrades = message.SizeLoserTrades;
            portfolio.AvgCloseSubs = message.AvgCloseSubs;

            portfolio.IntroducingBrokerFee = DecodeDoubleNull2(message.IntroducingBrokerFee);
            portfolio.ExecutingBrokerFee = DecodeDoubleNull2(message.ExecutingBrokerFee);
            portfolio.ExchangeFee = DecodeDoubleNull2(message.ExchangeFee);
            portfolio.OrfFee = DecodeDoubleNull2(message.OrfFee);
            portfolio.SecFee = DecodeDoubleNull2(message.SecFee);
            portfolio.TotalFees = DecodeDoubleNull2(message.TotalFees);

            portfolio.AvgOpenSubsCount = DecodeDoubleNull2(message.AvgOpenSubsCount);
            portfolio.AvgSubsBetweenFillsCount = DecodeDoubleNull2(message.AvgSubsBetweenFillsCount);

            portfolio.GroupSubmissionsAvg = message.GroupSubmissionsAvg;
            portfolio.FillRate = DecodeDoubleNull4(message.GroupAvgFillRate);

            var isReplay = message.IsReplay == BooleanEnum.True;

            List<IPosition> positionsList = new List<IPosition>();
            PortfolioUpdateMessageSlim.PositionsGroup positions = message.Positions;
            while (positions.HasNext)
            {
                PortfolioUpdateMessageSlim.PositionsGroup positionMessage = positions.Next();

                int positionId = positionMessage.PositionId;
                int parentPositionId = positionMessage.ParentPositionId;
                PositionType type = (PositionType)positionMessage.PositionType;
                IPosition position;
                if (parentPositionId <= 0)
                {
                    position = portfolio.GetPosition(positionId, type);
                }
                else
                {
                    IPosition parentPosition = portfolio.GetPosition(parentPositionId, PositionType.Underlying);
                    position = parentPosition.GetPosition(positionId, PositionType.Expiration);
                }
                position.ParentPositionId = parentPositionId;
                position.Id = positionId;
                position.PositionType = type;
                position.NetQty = positionMessage.NetQty;
                position.RealizedPnl = DecodeDoubleNull2(positionMessage.RealizedPnl);
                position.AdjustedPnl = DecodeDoubleNull2(positionMessage.AdjustedPnl);
                position.UnrealizedPnl = DecodeDoubleNull2(positionMessage.UnrealizedPnl);
                position.NetDelta = DecodeDoubleNull2(positionMessage.NetDelta);
                position.TotalSubmissions = positionMessage.TotalSubmissions;
                position.TotalFills = positionMessage.TotalFills;
                position.OpenPositionAveragePrice = DecodeDoubleNull2(positionMessage.OpenPositionAveragePrice);
                position.OpenPositionFillUnderPrice = DecodeDoubleNull2(positionMessage.OpenPositionFillUnderPrice);
                position.LastTradeTime = positionMessage.LastTradeTime.FromUnixEpoch();
                position.OpenNotional = DecodeDoubleNull2(positionMessage.OpenNotional);

                position.MaxResubmitEstimate = positionMessage.MaxResubmitEstimate;
                position.MaxResubmitForFill = positionMessage.MaxResubmitForFill;
                position.AvgResubmitEstimate = positionMessage.AvgResubmitEstimate;
                position.AvgResubmitForFill = positionMessage.AvgResubmitForFill;

                position.FirstEdge = DecodeDoubleNull2(positionMessage.FirstEdge);

                position.TotalOutOfMarketOrders = positionMessage.TotalOutOfMarketOrders;
                position.TotalOutOfMarketFills = positionMessage.TotalOutOfMarketFills;

                position.SubmissionRatePerSec = positionMessage.SubmissionRatePerSec;
                position.MaxOrdersPerSec = positionMessage.MaxOrdersPerSec;

                position.AvgCloseSubs = positionMessage.AvgCloseSubs;
                position.OpenSubsCount = positionMessage.OpenSubsCount;
                position.SubsBetweenFillsCount = positionMessage.SubsBetweenFillsCount;

                position.IntroducingBrokerFee = DecodeDoubleNull2(positionMessage.IntroducingBrokerFee);
                position.ExecutingBrokerFee = DecodeDoubleNull2(positionMessage.ExecutingBrokerFee);
                position.ExchangeFee = DecodeDoubleNull2(positionMessage.ExchangeFee);
                position.OrfFee = DecodeDoubleNull2(positionMessage.OrfFee);
                position.SecFee = DecodeDoubleNull2(positionMessage.SecFee);
                position.TotalFees = DecodeDoubleNull2(positionMessage.TotalFees);

                position.RawNetQty = positionMessage.RawNetQty;

                position.LastBuyEdgeToTheo = DecodeDoubleNull2(positionMessage.LastBuyEdgeToTheo);
                position.LastSellEdgeToTheo = DecodeDoubleNull2(positionMessage.LastSellEdgeToTheo);

                position.BestBuyEdgeToTheo = DecodeDoubleNull2(positionMessage.BestBuyEdgeToTheo);
                position.BestSellEdgeToTheo = DecodeDoubleNull2(positionMessage.BestSellEdgeToTheo);

                position.LastTraderId = positionMessage.LastTrader;

                position.SingleLegAdjustedPnl = DecodeDoubleNull2(positionMessage.SingleLegAdjustedPnl);
                position.SpreadAdjustedPnl = DecodeDoubleNull2(positionMessage.SpreadAdjustedPnl);

                portfolio.AddPosition(position);
                positionsList.Add(position);
            }

            _portfolioManager.PositionUpdated(portfolio, positionsList, isReplay);
        }

        private void DecodeMultiplePortfoliosAddedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            MultiplePortfoliosAddedMessage message = new MultiplePortfoliosAddedMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int requestId = message.RequestId;
            HashSet<IPortfolio> portfolios = new HashSet<IPortfolio>();
            MultiplePortfoliosAddedMessage.NoPortfoliosGroup portfoliosGroup = message.NoPortfolios;
            while (portfoliosGroup.HasNext)
            {
                MultiplePortfoliosAddedMessage.NoPortfoliosGroup portfolioMessage = portfoliosGroup.Next();
                int id = portfolioMessage.PortfolioId;
                PortfolioType portfolioType = (PortfolioType)portfolioMessage.PortfolioType;
                IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType, requestId);
                portfolio.TotalSubmissions = portfolioMessage.TotalSubmissions;
                portfolio.TotalSingleLegSubmissions = portfolioMessage.TotalSingleLegSubmissions;
                portfolio.TotalSpreadSubmissions = portfolioMessage.TotalSpreadSubmissions;
                portfolio.TotalSingleFills = portfolioMessage.TotalSingleFills;
                portfolio.TotalSpreadFills = portfolioMessage.TotalSpreadFills;
                portfolio.UniqueSubmissions = portfolioMessage.UniqueSubmissions;
                portfolio.UniqueSpreadSubmissions = portfolioMessage.UniqueSpreadSubmissions;
                portfolio.TotalFills = portfolioMessage.TotalFills;
                portfolio.UniqueFills = portfolioMessage.UniqueFills;
                portfolio.UniqueSpreadFills = portfolioMessage.UniqueSpreadFills;
                portfolio.StockContracts = portfolioMessage.StockContracts;
                portfolio.TotalContracts = portfolioMessage.TotalContracts;
                portfolio.UniqueContracts = portfolioMessage.UniqueContracts;
                portfolio.UniqueSpreadContracts = portfolioMessage.UniqueSpreadContracts;
                portfolio.NetQty = portfolioMessage.NetQty;
                portfolio.ShortQty = portfolioMessage.ShortQty;
                portfolio.LongQty = portfolioMessage.LongQty;
                portfolio.FillRate = DecodeDoubleNull4(portfolioMessage.FillRate);
                portfolio.OrderFillRate = DecodeDoubleNull4(portfolioMessage.OrderFillRate);
                portfolio.IbOrderFillRate = DecodeDoubleNull4(portfolioMessage.IbOrderFillRate);
                portfolio.LowestRealizedPnl = portfolioMessage.LowestRealizedPnl;
                portfolio.HighestRealizedPnl = portfolioMessage.HighestRealizedPnl;
                portfolio.RealizedPnl = portfolioMessage.RealizedPnl;
                portfolio.LowestAdjustedPnl = portfolioMessage.LowestAdjustedPnl;
                portfolio.HighestAdjustedPnl = portfolioMessage.HighestAdjustedPnl;
                portfolio.AdjustedPnl = portfolioMessage.AdjustedPnl;
                portfolio.SingleLegAdjustedPnl = double.IsNaN(portfolioMessage.SingleLegAdjustedPnl) ? 0.0 : portfolioMessage.SingleLegAdjustedPnl;
                portfolio.SpreadAdjustedPnl = double.IsNaN(portfolioMessage.SpreadAdjustedPnl) ? 0.0 : portfolioMessage.SpreadAdjustedPnl;
                portfolio.UnrealizedPnl = portfolioMessage.UnrealizedPnl;
                portfolio.NetDelta = portfolioMessage.NetDelta;

                portfolio.MaxResubmitEstimate = portfolioMessage.MaxResubmitEstimate;
                portfolio.MaxResubmitForFill = portfolioMessage.MaxResubmitForFill;
                portfolio.AvgResubmitEstimate = portfolioMessage.AvgResubmitEstimate;
                portfolio.AvgResubmitForFill = portfolioMessage.AvgResubmitForFill;

                portfolio.DeltaAdjustedBurn = portfolioMessage.DeltaAdjustedBurn;
                portfolio.DeltaAdjustedHelp = portfolioMessage.DeltaAdjustedHelp;
                portfolio.HighestOpenNotional = portfolioMessage.HighestOpenNotional;
                portfolio.TotalOpenNotional = portfolioMessage.TotalOpenNotional;

                portfolio.TotalOutOfMarketOrders = portfolioMessage.TotalOutOfMarketOrders;
                portfolio.TotalOutOfMarketFills = portfolioMessage.TotalOutOfMarketFills;

                portfolio.SubmissionRatePerSec = portfolioMessage.SubmissionRatePerSec;
                portfolio.MaxOrdersPerSec = portfolioMessage.MaxOrdersPerSec;

                portfolio.WinnerTrades = portfolioMessage.WinnerTrades;
                portfolio.LoserTrades = portfolioMessage.LoserTrades;
                portfolio.SizeWinnerTrades = portfolioMessage.SizeWinnerTrades;
                portfolio.SizeLoserTrades = portfolioMessage.SizeLoserTrades;
                portfolio.AvgCloseSubs = portfolioMessage.AvgCloseSubs;

                portfolio.IntroducingBrokerFee = portfolioMessage.IntroducingBrokerFee;
                portfolio.ExecutingBrokerFee = portfolioMessage.ExecutingBrokerFee;
                portfolio.ExchangeFee = portfolioMessage.ExchangeFee;
                portfolio.OrfFee = portfolioMessage.OrfFee;
                portfolio.SecFee = portfolioMessage.SecFee;
                portfolio.TotalFees = portfolioMessage.TotalFees;
                portfolio.AvgOpenSubsCount = portfolioMessage.AvgOpenSubsCount;
                portfolio.AvgSubsBetweenFillsCount = portfolioMessage.AvgSubsBetweenFillsCount;

                portfolio.GroupSubmissionsAvg = portfolioMessage.GroupSubmissionsAvg;
                portfolio.FillRate = DecodeDoubleNull4(portfolioMessage.GroupAvgFillRate);

                MultiplePortfoliosAddedMessage.NoPortfoliosGroup.PositionsGroup positions = portfolioMessage.Positions;
                while (positions.HasNext)
                {
                    MultiplePortfoliosAddedMessage.NoPortfoliosGroup.PositionsGroup positionMessage = positions.Next();

                    int positionId = positionMessage.PositionId;
                    int parentPositionId = positionMessage.ParentPositionId;
                    PositionType type = (PositionType)positionMessage.PositionType;
                    IPosition position;
                    if (parentPositionId <= 0)
                    {
                        position = portfolio.GetPosition(positionId, type);
                    }
                    else
                    {
                        IPosition parentPosition = portfolio.GetPosition(parentPositionId, PositionType.Underlying);
                        position = parentPosition.GetPosition(positionId, PositionType.Expiration);
                    }

                    position.ParentPositionId = parentPositionId;
                    position.Id = positionId;
                    position.PositionType = type;
                    position.NetQty = positionMessage.NetQty;
                    position.RealizedPnl = positionMessage.RealizedPnl;
                    position.AdjustedPnl = positionMessage.AdjustedPnl;
                    position.UnrealizedPnl = positionMessage.UnrealizedPnl;
                    position.NetDelta = positionMessage.NetDelta;
                    position.BestSellPrice = positionMessage.BestSellPrice;
                    position.BestSellPriceUnderMid = positionMessage.BestSellPriceUnderMid;
                    position.BestBuyPrice = positionMessage.BestBuyPrice;
                    position.BestBuyPriceUnderMid = positionMessage.BestBuyPriceUnderMid;
                    position.TotalSubmissions = positionMessage.TotalSubmissions;
                    position.TotalSingleLegSubmissions = positionMessage.TotalSingleLegSubmissions;
                    position.TotalSpreadSubmissions = positionMessage.TotalSpreadSubmissions;
                    position.TotalSingleFills = positionMessage.TotalSingleFills;
                    position.TotalSpreadFills = positionMessage.TotalSpreadFills;
                    position.UniqueSubmissions = positionMessage.UniqueSubmissions;
                    position.TotalFills = positionMessage.TotalFills;
                    position.UniqueFills = positionMessage.UniqueFills;
                    position.TotalContracts = positionMessage.TotalContracts;
                    position.UniqueContracts = positionMessage.UniqueContracts;
                    position.FillRate = DecodeDoubleNull4(positionMessage.FillRate);
                    position.OrderFillRate = DecodeDoubleNull4(positionMessage.OrderFillRate);
                    position.IbOrderFillRate = DecodeDoubleNull4(positionMessage.IbOrderFillRate);
                    position.OpenPositionAveragePrice = positionMessage.OpenPositionAveragePrice;
                    position.OpenPositionFillUnderPrice = positionMessage.OpenPositionFillUnderPrice;
                    position.LastTradeTime = positionMessage.LastTradeTime.FromUnixEpoch();
                    position.PositionDate = positionMessage.PositionDate.FromUnixEpoch();
                    position.LastEdge = positionMessage.LastEdge;
                    position.LastBuyEdge = positionMessage.LastBuyEdge;
                    position.LastSellEdge = positionMessage.LastSellEdge;

                    position.LastBuyEdgeToTheo = positionMessage.LastBuyEdgeToTheo;
                    position.LastSellEdgeToTheo = positionMessage.LastSellEdgeToTheo;
                    position.LastBuyFillEdgeToTheo = positionMessage.LastBuyFillEdgeToTheo;
                    position.LastSellFillEdgeToTheo = positionMessage.LastSellFillEdgeToTheo;
                    position.LastBuyAttemptEdgeToTheo = positionMessage.LastBuyAttemptEdgeToTheo;
                    position.LastSellAttemptEdgeToTheo = positionMessage.LastSellAttemptEdgeToTheo;

                    position.LastPermBuyFillEdgeToTheo = positionMessage.LastPermBuyFillEdgeToTheo;
                    position.LastPermSellFillEdgeToTheo = positionMessage.LastPermSellFillEdgeToTheo;
                    position.LastPermBuyAttemptEdgeToTheo = positionMessage.LastPermBuyAttemptEdgeToTheo;
                    position.LastPermSellAttemptEdgeToTheo = positionMessage.LastPermSellAttemptEdgeToTheo;

                    position.BestBuyEdgeToTheo = positionMessage.BestBuyEdgeToTheo;
                    position.WorstBuyEdgeToTheo = positionMessage.WorstBuyEdgeToTheo;
                    position.BestSellEdgeToTheo = positionMessage.BestSellEdgeToTheo;
                    position.WorstSellEdgeToTheo = positionMessage.WorstSellEdgeToTheo;
                    position.OpenNotional = positionMessage.OpenNotional;

                    position.MaxResubmitEstimate = positionMessage.MaxResubmitEstimate;
                    position.MaxResubmitForFill = positionMessage.MaxResubmitForFill;
                    position.AvgResubmitEstimate = positionMessage.AvgResubmitEstimate;
                    position.AvgResubmitForFill = positionMessage.AvgResubmitForFill;

                    position.FirstEdge = positionMessage.FirstEdge;

                    position.TotalOutOfMarketOrders = positionMessage.TotalOutOfMarketOrders;
                    position.TotalOutOfMarketFills = positionMessage.TotalOutOfMarketFills;

                    position.HardSide = positionMessage.HardSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.HardSide;
                    position.HardSideDesignationTime = positionMessage.HardSideDesignationTime.FromUnixEpoch();
                    position.HardSideBuyGiveUp = positionMessage.HardSideBuyGiveUp;
                    position.HardSideSellGiveUp = positionMessage.HardSideSellGiveUp;

                    position.SubmissionRatePerSec = positionMessage.SubmissionRatePerSec;
                    position.MaxOrdersPerSec = positionMessage.MaxOrdersPerSec;

                    position.WinnerTrades = positionMessage.WinnerTrades;
                    position.LoserTrades = positionMessage.LoserTrades;
                    position.SizeWinnerTrades = positionMessage.SizeWinnerTrades;
                    position.SizeLoserTrades = positionMessage.SizeLoserTrades;
                    position.AvgCloseSubs = positionMessage.AvgCloseSubs;
                    position.OpenSubsCount = positionMessage.OpenSubsCount;
                    position.SubsBetweenFillsCount = positionMessage.SubsBetweenFillsCount;

                    position.IntroducingBrokerFee = positionMessage.IntroducingBrokerFee;
                    position.ExecutingBrokerFee = positionMessage.ExecutingBrokerFee;
                    position.ExchangeFee = positionMessage.ExchangeFee;
                    position.OrfFee = positionMessage.OrfFee;
                    position.SecFee = positionMessage.SecFee;
                    position.TotalFees = positionMessage.TotalFees;

                    position.LastTradeSide = positionMessage.LastTradeSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.LastTradeSide;

                    position.LastBuyAttempt = positionMessage.LastBuyAttempt;
                    position.LastBuyAttemptUnderlying = positionMessage.LastBuyAttemptUnderlying;
                    position.LastSellAttempt = positionMessage.LastSellAttempt;
                    position.LastSellAttemptUnderlying = positionMessage.LastSellAttemptUnderlying;

                    position.RawNetQty = positionMessage.RawNetQty;

                    string lastInstance = positionMessage.GetLastInstance();
                    if (!string.IsNullOrWhiteSpace(lastInstance))
                    {
                        position.LastInstance = lastInstance;
                    }

                    string lastTrader = positionMessage.GetLastTrader();
                    if (!string.IsNullOrWhiteSpace(lastTrader))
                    {
                        position.LastTrader = lastTrader;
                    }

                    string account = positionMessage.GetAccount();
                    if (!string.IsNullOrWhiteSpace(account))
                    {
                        position.Account = account;
                    }
                    position.Name = positionMessage.GetPositionName();
                    position.Symbol = positionMessage.GetPositionSymbol();

                    position.SingleLegAdjustedPnl = double.IsNaN(positionMessage.SingleLegAdjustedPnl) ? 0.0 : positionMessage.SingleLegAdjustedPnl;
                    position.SpreadAdjustedPnl = double.IsNaN(positionMessage.SpreadAdjustedPnl) ? 0.0 : positionMessage.SpreadAdjustedPnl;

                    portfolio.AddPosition(position);
                }
                portfolio.Name = portfolioMessage.GetPortfolioName();
                portfolios.Add(portfolio);
            }

            _portfolioManager.MultiplePortfoliosAdded(requestId, portfolios);
            MultiplePortfolioAdded?.Invoke(requestId, portfolios);
        }

        private void DecodeSecurityDecimalUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            SecurityDecimalUpdate message = new SecurityDecimalUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SubscriptionFieldType updateType = (SubscriptionFieldType)message.UpdateType;
            int tickerId = message.TickerId;
            double bidUpdate = DecodeDoubleNull2(message.BidUpdate);
            double askUpdate = DecodeDoubleNull2(message.AskUpdate);
            double lastPrice = DecodeDoubleNull2(message.LastPrice);
            DateTime timestamp = message.Timestamp.FromUnixEpoch();
            QuoteChangeType bidChange = message.BidChange;
            QuoteChangeType askChange = message.AskChange;
            int bidSize = message.BidSize;
            int askSize = message.AskSize;
            double latencyMs = message.LatencyMsInActingVersion() ? DecodeDoubleNull4(message.LatencyMs) : 0;

            _updateManager.HandleUpdate(tickerId, updateType, bidUpdate, askUpdate, timestamp, bidChange, askChange, bidSize, askSize, lastPrice, latencyMs);
        }

        private void DecodeDerivedValueUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            DerivedValueUpdate message = new DerivedValueUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int tickerId = message.TickerId;
            DerivedValueUpdateModel? model = _updateManager.GetDerivedValueUpdateModel(tickerId);
            if (model == null)
            {
                return;
            }

            model.TickerId = tickerId;
            model.InterpolatedBidUpdate = message.InterpolatedBidUpdate;
            model.InterpolatedAskUpdate = message.InterpolatedAskUpdate;
            model.BestBidUpdate = message.BestBidUpdate;
            model.BestAskUpdate = message.BestAskUpdate;
            model.BestBidBase = message.BestBidBase;
            model.BestAskBase = message.BestAskBase;
            model.BestBidUnderlying = message.BestBidUnderlying;
            model.BestAskUnderlying = message.BestAskUnderlying;
            model.BidTradeUpdate = message.BidTradeUpdate;
            model.AskTradeUpdate = message.AskTradeUpdate;
            model.BidTradeBase = message.BidTradeBase;
            model.AskTradeBase = message.AskTradeBase;
            model.BidTradeUnderlying = message.BidTradeUnderlying;
            model.AskTradeUnderlying = message.AskTradeUnderlying;
            model.BidTradeTimestamp = message.BidTradeTimestamp.FromUnixEpoch();
            model.AskTradeTimestamp = message.AskTradeTimestamp.FromUnixEpoch();
            model.BidTradeCount = message.BidTradeCount;
            model.AskTradeCount = message.AskTradeCount;
            model.BidTradeIsLatest = message.BidTradeIsLatest == BooleanEnum.True;
            model.AskTradeIsLatest = message.AskTradeIsLatest == BooleanEnum.True;

            model.CustTradeBidCount = message.CustTradeBidCount;
            model.CustTradeAskCount = message.CustTradeAskCount;
            model.CustTradeBid = message.CustTradeBid;
            model.CustTradeAsk = message.CustTradeAsk;
            model.CustTradeBidBase = message.CustTradeBidBase;
            model.CustTradeAskBase = message.CustTradeAskBase;
            model.CustTradeBidNoChange = message.CustTradeBidNoChange;
            model.CustTradeAskNoChange = message.CustTradeAskNoChange;
            model.CustTradeBidBaseNoChange = message.CustTradeBidBaseNoChange;
            model.CustTradeAskBaseNoChange = message.CustTradeAskBaseNoChange;
            model.CustTradeBidUnderlyingPrice = message.CustTradeBidUnderlyingPrice;
            model.CustTradeAskUnderlyingPrice = message.CustTradeAskUnderlyingPrice;
            model.CustBidTradeIsLatest = message.CustBidTradeIsLatest == BooleanEnum.True;
            model.CustAskTradeIsLatest = message.CustAskTradeIsLatest == BooleanEnum.True;
            model.CustBidTradeTimestamp = message.CustBidTradeTimestamp.FromUnixEpoch();
            model.CustAskTradeTimestamp = message.CustAskTradeTimestamp.FromUnixEpoch();

            model.HighestBidLowestAskResult!.HighestBid = message.HighestBid;
            model.HighestBidLowestAskResult!.LowestAsk = message.LowestAsk;
            model.HighestBidLowestAskResult!.HighestBidTime = message.HighestBidTime;
            model.HighestBidLowestAskResult!.LowestAskTime = message.LowestAskTime;
            model.HighestBidLowestAskResult!.HighestBidBase = message.HighestBidBase;
            model.HighestBidLowestAskResult!.LowestAskBase = message.LowestAskBase;
            model.HighestBidLowestAskResult!.HighestBidUnderlyingMid = message.HighestBidUnderlyingMid;
            model.HighestBidLowestAskResult!.LowestAskUnderlyingMid = message.LowestAskUnderlyingMid;
            model.HighestBidLowestAskResult!.SkewAdjustedHighestBid = message.SkewAdjustedHighestBid;
            model.HighestBidLowestAskResult!.SkewAdjustedLowestAsk = message.SkewAdjustedLowestAsk;
            model.HighestBidLowestAskResult!.SkewAdjustedHighestBidTime = message.SkewAdjustedHighestBidTime;
            model.HighestBidLowestAskResult!.SkewAdjustedLowestAskTime = message.SkewAdjustedLowestAskTime;
            model.HighestBidLowestAskResult!.SkewAdjustedHighestBidBase = message.SkewAdjustedHighestBidBase;
            model.HighestBidLowestAskResult!.SkewAdjustedLowestAskBase = message.SkewAdjustedLowestAskBase;
            model.HighestBidLowestAskResult!.SkewAdjustedHighestBidUnderlyingMid = message.SkewAdjustedHighestBidUnderlyingMid;
            model.HighestBidLowestAskResult!.SkewAdjustedLowestAskUnderlyingMid = message.SkewAdjustedLowestAskUnderlyingMid;

            model.HighestBidLowestAskResultLong!.HighestBid = message.HighestBidLong;
            model.HighestBidLowestAskResultLong!.LowestAsk = message.LowestAskLong;
            model.HighestBidLowestAskResultLong!.HighestBidTime = message.HighestBidTimeLong;
            model.HighestBidLowestAskResultLong!.LowestAskTime = message.LowestAskTimeLong;
            model.HighestBidLowestAskResultLong!.HighestBidBase = message.HighestBidBaseLong;
            model.HighestBidLowestAskResultLong!.LowestAskBase = message.LowestAskBaseLong;
            model.HighestBidLowestAskResultLong!.HighestBidUnderlyingMid = message.HighestBidUnderlyingMidLong;
            model.HighestBidLowestAskResultLong!.LowestAskUnderlyingMid = message.LowestAskUnderlyingMidLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedHighestBid = message.SkewAdjustedHighestBidLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedLowestAsk = message.SkewAdjustedLowestAskLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedHighestBidTime = message.SkewAdjustedHighestBidTimeLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedLowestAskTime = message.SkewAdjustedLowestAskTimeLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedHighestBidBase = message.SkewAdjustedHighestBidBaseLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedLowestAskBase = message.SkewAdjustedLowestAskBaseLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedHighestBidUnderlyingMid = message.SkewAdjustedHighestBidUnderlyingMidLong;
            model.HighestBidLowestAskResultLong!.SkewAdjustedLowestAskUnderlyingMid = message.SkewAdjustedLowestAskUnderlyingMidLong;

            model.ImpliedBid = DecodePriceNull3(message.ImpliedBid);
            model.ImpliedAsk = DecodePriceNull3(message.ImpliedAsk);
            model.ImpliedBidRecord = DecodePriceNull3(message.ImpliedBidRecord);
            model.ImpliedAskRecord = DecodePriceNull3(message.ImpliedAskRecord);
            model.ImpliedBidRecordTheo = DecodePriceNull3(message.ImpliedBidRecordTheo);
            model.ImpliedBidRecordTheoMovement = DecodePriceNull3(message.ImpliedBidRecordTheoMovement);
            model.ImpliedBidRecordNonDeltaMovement = DecodePriceNull3(message.ImpliedBidRecordNonDeltaMovement);
            model.ImpliedBidRecordTimestamp = message.ImpliedBidRecordTimestamp.FromUnixEpoch();
            model.ImpliedAskRecordTheo = DecodePriceNull3(message.ImpliedAskRecordTheo);
            model.ImpliedAskRecordTheoMovement = DecodePriceNull3(message.ImpliedAskRecordTheoMovement);
            model.ImpliedAskRecordNonDeltaMovement = DecodePriceNull3(message.ImpliedAskRecordNonDeltaMovement);
            model.ImpliedAskRecordTimestamp = message.ImpliedAskRecordTimestamp.FromUnixEpoch();

            _updateManager.HandleUpdate(model);
        }

        private void DecodeDoubleUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            DoubleUpdate message = new DoubleUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            _updateManager.HandleUpdate(message.TickerId,
                                        (SubscriptionFieldType)message.UpdateType,
                                        message.Sequence,
                                        message.UnderlyingTimestamp,
                                        message.SnapshotTimestamp,
                                        message.HanweckTimestamp,
                                        message.Theo,
                                        message.Delta,
                                        message.Gamma,
                                        message.Vega,
                                        message.Theta,
                                        message.Rho,
                                        message.Implied,
                                        message.LatestMidPrice,
                                        message.SnapshotMidPrice,
                                        message.DeltaAdjustedTheo,
                                        message.JumpDetected == BooleanEnum.True);
        }

        private void DecodeDeltaAdjTheoDetailsUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            DeltaAdjTheoDetailsUpdate message = new DeltaAdjTheoDetailsUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            DeltaAdjustedTheoDetailsModel deltaAdjTheoDetails = new DeltaAdjustedTheoDetailsModel(message.Delta, message.Theo, message.MidPrice, message.DeltaAdjustedTheo, message.BidUpdate, message.AskUpdate, message.HanweckTimestamp.FromUnixEpoch(), message.GetSymbol());
            _updateManager.HandleUpdate(ref deltaAdjTheoDetails);
        }

        private void DecodeDeltaAdjustedTheoUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            DeltaAdjustedTheoUpdateMessage message = new DeltaAdjustedTheoUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var tickerId = (message.TickerId[0] << 16) | (message.TickerId[1] << 8) | message.TickerId[2];
            var sequence = (uint)(message.Sequence[0] << 16) | (uint)(message.Sequence[1] << 8) | message.Sequence[2];
            var theoUpdate = new AdjTheoUpdate(
                tickerId,
                sequence,
                DecodePriceNull3(message.Theo),
                DecodePriceNull3(message.SmoothedTheo),
                DecodePriceNull3(message.Underlying),
                message.JumpDetected == BooleanEnum.True,
                DecodePriceNull3(message.SecondaryTheo),
                DecodePriceNull3(message.SecondaryTheoAdj),
                DecodePriceNull3(message.PriceMetric),
                message.ModelId,
                message.SecondaryVol,
                DecodePriceNull3(message.ChangeInPremium),
                DecodePriceNull3(message.SecondarySpot),
                DecodePriceNull3(message.DaEma),
                DecodePriceNull3(message.VolaEma));
            _updateManager.HandleUpdate(ref theoUpdate);
        }

        private void DecodeRequestTransactionsFromArchiveMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RequestTransactionsFromArchive message = new RequestTransactionsFromArchive();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            DateTime startDateTime = message.StartDateTime.FromUnixEpoch();
            DateTime endDateTime = message.EndDateTime.FromUnixEpoch();
            bool ordersOnly = message.FillsOnly == BooleanEnum.True;

            List<Data.Enums.OrderStatus> orderStatusList = new List<Data.Enums.OrderStatus>();
            List<string> apiUsernames = new List<string>();
            List<string> tags = new List<string>();
            List<string> symbols = new List<string>();
            List<string> underlyings = new List<string>();

            RequestTransactionsFromArchive.ApiUsernamesGroup apiUsernamesGroup = message.ApiUsernames;
            while (apiUsernamesGroup.HasNext)
            {
                RequestTransactionsFromArchive.ApiUsernamesGroup apiUsername = apiUsernamesGroup.Next();
                apiUsernames.Add(apiUsername.GetApiUsername());
            }

            RequestTransactionsFromArchive.TagsGroup tagsGroup = message.Tags;
            while (tagsGroup.HasNext)
            {
                RequestTransactionsFromArchive.TagsGroup tag = tagsGroup.Next();
                tags.Add(tag.GetTag());
            }

            RequestTransactionsFromArchive.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                RequestTransactionsFromArchive.SymbolsGroup symbol = symbolsGroup.Next();
                symbols.Add(symbol.GetSymbol());
            }

            RequestTransactionsFromArchive.UnderlyingsGroup underlyingsGroup = message.Underlyings;
            while (underlyingsGroup.HasNext)
            {
                RequestTransactionsFromArchive.UnderlyingsGroup underlying = underlyingsGroup.Next();
                underlyings.Add(underlying.GetUnderlying());
            }

            try
            {
                RequestTransactionsFromArchive.OrderStatusGroup orderStatusGroup = message.OrderStatus;
                while (orderStatusGroup.HasNext)
                {
                    RequestTransactionsFromArchive.OrderStatusGroup orderStatus = orderStatusGroup.Next();
                    orderStatusList.Add((Data.Enums.OrderStatus)orderStatus.OrderStatus);
                }
            }
            catch
            {
                // ignored
            }

            RequestTransactionsFromArchive?.Invoke(requestId, startDateTime, endDateTime, ordersOnly, orderStatusList, apiUsernames, tags, symbols, underlyings);
        }

        private void DecodeRequestPnlFromArchiveMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RequestPnlFromArchive message = new RequestPnlFromArchive();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            DateTime startDateTime = message.StartDateTime.FromUnixEpoch();
            DateTime endDateTime = message.EndDateTime.FromUnixEpoch();
            bool requestPreCalc = message.RequestPreCalcs == BooleanEnum.True;
            bool includeBreakdownStats = message.IncludeBreakdownStats == BooleanEnum.True;

            List<string> apiUsernames = new List<string>();
            List<string> tags = new List<string>();
            List<string> symbols = new List<string>();
            List<string> underlyings = new List<string>();

            RequestPnlFromArchive.ApiUsernamesGroup apiUsernamesGroup = message.ApiUsernames;
            while (apiUsernamesGroup.HasNext)
            {
                RequestPnlFromArchive.ApiUsernamesGroup apiUsername = apiUsernamesGroup.Next();
                apiUsernames.Add(apiUsername.GetApiUsername());
            }

            RequestPnlFromArchive.TagsGroup tagsGroup = message.Tags;
            while (tagsGroup.HasNext)
            {
                RequestPnlFromArchive.TagsGroup tag = tagsGroup.Next();
                tags.Add(tag.GetTag());
            }

            RequestPnlFromArchive.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                RequestPnlFromArchive.SymbolsGroup symbol = symbolsGroup.Next();
                symbols.Add(symbol.GetSymbol());
            }

            RequestPnlFromArchive.UnderlyingsGroup underlyingsGroup = message.Underlyings;
            while (underlyingsGroup.HasNext)
            {
                RequestPnlFromArchive.UnderlyingsGroup underlying = underlyingsGroup.Next();
                underlyings.Add(underlying.GetUnderlying());
            }

            RequestPnlFromArchive?.Invoke(requestId, startDateTime, endDateTime, requestPreCalc, includeBreakdownStats, apiUsernames, tags, symbols, underlyings);
        }

        private void DecodeRequestAuditTrailMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RequestAuditTrail message = new RequestAuditTrail();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string orderId = message.GetOrderId();
            AuditTrailRequest?.Invoke(requestId, orderId);
        }

        private void DecodeAuditTrailResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            AuditTrailResponse message = new AuditTrailResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string orderId = message.GetOrderId();
            byte[] data = message.GetRawDataBytes();

            XmlDocument xmlDocument = new XmlDocument();
            MemoryStream memoryStream = new MemoryStream(data, true);
            xmlDocument.Load(memoryStream);

            AuditTrailResponse?.Invoke(requestId, orderId, xmlDocument);
        }

        private void DecodeRequestOrderDetailsMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RequestOrderDetails message = new RequestOrderDetails();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string orderId = message.GetOrderId();
            OrderDetailsRequest?.Invoke(requestId, orderId);
        }

        private void DecodeOrderDetailsResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory != null)
            {
                OrderDetailsResponse message = new OrderDetailsResponse();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                string orderId = message.GetOrderId();
                if (_orderFactory.GetExistingOrder(orderId, out IOrder? order))
                {
                    string json = message.GetJson();
                    _orderFactory.HandleOrderDetailsUpdate(order, json);
                }
            }
        }

        private void DecodeAutoTraderConfigMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AutoTraderConfigJson != null)
            {
                AutoTraderConfigMessage message = new AutoTraderConfigMessage();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                int requestId = message.RequestId;
                string json = message.GetJson();
                AutoTraderConfigJson?.Invoke(requestId, json);
            }
        }

        private void DecodeSymbolStatModelAddedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager != null)
            {
                SymbolStatModelAddedMessage message = new SymbolStatModelAddedMessage();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                var id = message.Id;
                var symbol = message.GetSymbol();
                ISymbolStatModel? model = _portfolioManager.GetSymbolStatModel(id, symbol);
                if (model != null)
                {
                    model.Id = id;
                    model.Symbol = symbol;

                    model.MultiLegTradesCount = message.MultiLegTradesCount;
                    model.SingleLegTradesCount = message.SingleLegTradesCount;
                    model.MultiLegTradesPerHour = message.MultiLegTradesPerHour;
                    model.SingleLegTradesPerHour = message.SingleLegTradesPerHour;
                    model.MultiLegTradesPerMinute = message.MultiLegTradesPerMinute;
                    model.SingleLegTradesPerMinute = message.SingleLegTradesPerMinute;

                    model.Volume = message.Volume;
                    model.OptionVolume = message.OptionVolume;
                    model.DayPercentChange = message.DayPercentChange;
                    model.HourPercentChange = message.HourPercentChange;
                    model.HalfHourPercentChange = message.HalfHourPercentChange;
                    model.QuarterHourPercentChange = message.QuarterHourPercentChange;

                    model.DayNetChange = message.DayNetChange;
                    model.HourNetChange = message.HourNetChange;
                    model.HalfHourNetChange = message.HalfHourNetChange;
                    model.QuarterHourNetChange = message.QuarterHourNetChange;
                    model.Last = message.Last;

                    _portfolioManager.SymbolStatModelUpdated(model);
                }
            }
        }

        private void DecodeSymbolStatModelUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager != null)
            {
                SymbolStatModelUpdateMessage message = new SymbolStatModelUpdateMessage();
                message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
                var id = message.Id;
                ISymbolStatModel? model = _portfolioManager.GetSymbolStatModel(id);
                if (model != null)
                {
                    model.Id = id;
                    model.MultiLegTradesCount = message.MultiLegTradesCount;
                    model.SingleLegTradesCount = message.SingleLegTradesCount;
                    model.MultiLegTradesPerHour = message.MultiLegTradesPerHour;
                    model.SingleLegTradesPerHour = message.SingleLegTradesPerHour;
                    model.MultiLegTradesPerMinute = message.MultiLegTradesPerMinute;
                    model.SingleLegTradesPerMinute = message.SingleLegTradesPerMinute;

                    model.Volume = message.Volume;
                    model.OptionVolume = message.OptionVolume;
                    model.DayPercentChange = message.DayPercentChange;
                    model.HourPercentChange = message.HourPercentChange;
                    model.HalfHourPercentChange = message.HalfHourPercentChange;
                    model.QuarterHourPercentChange = message.QuarterHourPercentChange;

                    model.DayNetChange = message.DayNetChange;
                    model.HourNetChange = message.HourNetChange;
                    model.HalfHourNetChange = message.HalfHourNetChange;
                    model.QuarterHourNetChange = message.QuarterHourNetChange;
                    model.Last = message.Last;

                    _portfolioManager.SymbolStatModelUpdated(model);
                }
            }
        }

        private void DecodeHanweckUpdatesWithMatchingTimestampsRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            HanweckUpdatesWithMatchingTimestampsRequest message = new HanweckUpdatesWithMatchingTimestampsRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            List<string> symbols = new List<string>();

            HanweckUpdatesWithMatchingTimestampsRequest.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                HanweckUpdatesWithMatchingTimestampsRequest.SymbolsGroup nextSymbol = symbolsGroup.Next();
                symbols.Add(nextSymbol.GetSymbol());
            }

            HanweckUpdatesWithMatchingTimestampsRequest?.Invoke(requestId, symbols);
        }

        private void DecodeHanweckUpdatesWithMatchingTimestampsResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            HanweckUpdatesWithMatchingTimestampsResponse message = new HanweckUpdatesWithMatchingTimestampsResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool updateFound = message.UpdateFound == BooleanEnum.True;
            DateTime timestamp = message.Timestamp.FromUnixEpoch();
            double price = message.Price;
            Dictionary<string, double> symbolToTheoMap = new Dictionary<string, double>();
            HanweckUpdatesWithMatchingTimestampsResponse.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                HanweckUpdatesWithMatchingTimestampsResponse.SymbolsGroup nextSymbol = symbolsGroup.Next();
                symbolToTheoMap[nextSymbol.GetSymbol()] = nextSymbol.Theo;
            }
            Data.Responses.HanweckUpdatesWithMatchingTimestampsResponse response = new Data.Responses.HanweckUpdatesWithMatchingTimestampsResponse(requestId, updateFound, timestamp, price, symbolToTheoMap);
            HanweckUpdatesWithMatchingTimestampsResponse?.Invoke(response);
        }

        private void DecodeSymbolMapRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            SymbolMapRequestMessage message = new SymbolMapRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            SymbolMapRequest?.Invoke(requestId);
        }

        private void DecodeSymbolMapResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            SymbolMapResponseMessage message = new SymbolMapResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            Dictionary<string, int> symbolToIndexMap = new Dictionary<string, int>(count);
            SymbolMapResponseMessage.SymbolMapGroup symbolsMapGroup = message.SymbolMap;
            while (symbolsMapGroup.HasNext)
            {
                SymbolMapResponseMessage.SymbolMapGroup nextSymbol = symbolsMapGroup.Next();
                int index = nextSymbol.Index;
                string symbol = nextSymbol.GetSymbol();
                if (!string.IsNullOrEmpty(symbol))
                {
                    symbolToIndexMap[symbol] = index;
                }
            }
            SymbolMapResponse?.Invoke(requestId, lastGroup, symbolToIndexMap);
        }

        private void DecodeRootSymbolMapRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RootSymbolMapRequestMessage message = new RootSymbolMapRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            RootSymbolMapRequest?.Invoke(requestId);
        }

        private void DecodeRootSymbolMapResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            RootSymbolMapResponseMessage message = new RootSymbolMapResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            Dictionary<string, int> symbolToIndexMap = new Dictionary<string, int>(count);

            RootSymbolMapResponseMessage.RootSymbolMapGroup symbolsMapGroup = message.RootSymbolMap;

            while (symbolsMapGroup.HasNext)
            {
                RootSymbolMapResponseMessage.RootSymbolMapGroup rootSymbolGroup = symbolsMapGroup.Next();
                string? root = rootSymbolGroup.GetRootSymbol();
                string? rootSymbol = root?.ToUpper();

                RootSymbolMapResponseMessage.RootSymbolMapGroup.ExpirationSymbolMapGroup expirationGroup = rootSymbolGroup.ExpirationSymbolMap;
                while (expirationGroup.HasNext)
                {
                    RootSymbolMapResponseMessage.RootSymbolMapGroup.ExpirationSymbolMapGroup nextExpiration = expirationGroup.Next();
                    int index = nextExpiration.Index;
                    if (rootSymbol == null || nextExpiration.Expiration > 20_00_00)
                    {
                        string expiration = nextExpiration.Expiration.ToString()[2..];
                        string callPut = nextExpiration.PutCall == PutCall.Put ? "P" : "C";
                        double strike = Math.Round(nextExpiration.Strike, 2);
                        string symbol = $".{rootSymbol}{expiration}{callPut}{strike}";
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            symbolToIndexMap[symbol] = index;
                        }
                    }
                }
            }
            SymbolMapResponse?.Invoke(requestId, lastGroup, symbolToIndexMap);
        }

        private void DecodeOptionSymbolMapResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            OptionSymbolMapResponseMessage message = new OptionSymbolMapResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            Dictionary<string, int> symbolToIndexMap = new Dictionary<string, int>();

            OptionSymbolMapResponseMessage.SymbolMapGroup symbolsMapGroup = message.SymbolMap;
            while (symbolsMapGroup.HasNext)
            {
                OptionSymbolMapResponseMessage.SymbolMapGroup nextSymbol = symbolsMapGroup.Next();
                string root = nextSymbol.GetRoot();
                if (!string.IsNullOrWhiteSpace(root))
                {
                    OptionSymbolMapResponseMessage.SymbolMapGroup.RootMapGroup optionGroup = nextSymbol.RootMap;
                    while (optionGroup.HasNext)
                    {
                        OptionSymbolMapResponseMessage.SymbolMapGroup.RootMapGroup option = optionGroup.Next();
                        int index = option.Index;
                        string callPut = option.PutCall == PutCall.Put ? "P" : "C";
                        string expiration = option.Expiration.ToString();
                        string strike = option.Strike.ToString(CultureInfo.InvariantCulture);
                        string symbol = "." + root.ToUpper() + expiration + callPut + strike;
                        symbolToIndexMap[symbol] = index;
                    }
                }
            }
            SymbolMapResponse?.Invoke(requestId, lastGroup, symbolToIndexMap);
        }

        private void DecodeMultipleSecurityDecimalUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            MultipleSecurityDecimalUpdateMessage message = new MultipleSecurityDecimalUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            Dictionary<int, (double update, double bidUpdate, double askUpdate)> indexToUpdateMap = new Dictionary<int, (double update, double bidUpdate, double askUpdate)>();

            SubscriptionFieldType updateType = (SubscriptionFieldType)message.UpdateType;

            MultipleSecurityDecimalUpdateMessage.UpdatesGroup updatesGroup = message.Updates;
            while (updatesGroup.HasNext)
            {
                MultipleSecurityDecimalUpdateMessage.UpdatesGroup nextUpdate = updatesGroup.Next();
                int index = nextUpdate.SymbolId;
                double update = nextUpdate.Update;
                double bidUpdate = nextUpdate.BidUpdate;
                double askUpdate = nextUpdate.AskUpdate;
                indexToUpdateMap[index] = (update, bidUpdate, askUpdate);
            }

            _updateManager.HandleUpdate(updateType, indexToUpdateMap);
        }

        private void DecodeOptionSnapshotRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OptionSnapshotRequest == null)
            {
                return;
            }

            OptionSnapshotsRequest message = new OptionSnapshotsRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            double delta = message.Delta;
            DateTime expiration = message.Expiration.FromUnixEpoch();
            DateTime startDateTime = message.StartDateTime.FromUnixEpoch();
            DateTime endDateTime = message.EndDateTime.FromUnixEpoch();
            string symbol = message.GetSymbol();

            OptionSnapshotRequest?.Invoke(reqId, symbol, delta, expiration, startDateTime, endDateTime);
        }

        private void DecodeOptionSnapshotResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OptionSnapshotResponse == null)
            {
                return;
            }

            OptionSnapshotsResponse message = new OptionSnapshotsResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            bool found = message.UpdateFound == BooleanEnum.True;

            List<Data.Responses.OptionSnapshot> snapshots = new List<Data.Responses.OptionSnapshot>();

            OptionSnapshotsResponse.SnapshotsGroup snapshotsGroup = message.Snapshots;
            while (snapshotsGroup.HasNext)
            {
                OptionSnapshotsResponse.SnapshotsGroup nextUpdate = snapshotsGroup.Next();

                Data.Responses.OptionSnapshot optionSnapshot = new Data.Responses.OptionSnapshot()
                {
                    AdjTheo = nextUpdate.AdjTheo,
                    Theo = nextUpdate.Theo,
                    Delta = nextUpdate.Delta,
                    Vega = nextUpdate.Vega,
                    Iv = nextUpdate.Iv,
                    Bid = nextUpdate.Bid,
                    Ask = nextUpdate.Ask,
                    UnderBid = nextUpdate.UnderBid,
                    UnderAsk = nextUpdate.UnderAsk,
                    QuoteTime = nextUpdate.QuoteTime.FromUnixEpoch(),
                    SnapshotTime = nextUpdate.SnapshotTime.FromUnixEpoch(),
                    HanweckCalcTime = nextUpdate.HanweckCalcTime.FromUnixEpoch(),
                    AdjTheoTime = nextUpdate.AdjTheoTime.FromUnixEpoch(),
                    Symbol = nextUpdate.GetSymbol(),
                };

                snapshots.Add(optionSnapshot);
            }

            OptionSnapshotResponse?.Invoke(reqId, found, snapshots);
        }

        private void DecodeMarketCrossScanRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MarketCrossScanRequest == null)
            {
                return;
            }

            MarketCrossScanRequest message = new MarketCrossScanRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            double lookbackInSeconds = message.LookbackInSeconds;
            double minMarketCross = message.MinMarketCross;
            double currentMarketWidth = message.CurrentMarketWidth;

            MarketCrossScanRequest?.Invoke(reqId, lookbackInSeconds, minMarketCross, currentMarketWidth);
        }

        private void DecodeMarketCrossScanResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MarketCrossScanResponse == null)
            {
                return;
            }

            MarketCrossScanResponse message = new MarketCrossScanResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            bool found = message.UpdateFound == BooleanEnum.True;

            List<Data.Responses.MarketCrossScanResult> snapshots = new List<Data.Responses.MarketCrossScanResult>();

            MarketCrossScanResponse.SnapshotsGroup snapshotsGroup = message.Snapshots;
            while (snapshotsGroup.HasNext)
            {
                MarketCrossScanResponse.SnapshotsGroup nextUpdate = snapshotsGroup.Next();

                Data.Responses.MarketCrossScanResult optionSnapshot = new Data.Responses.MarketCrossScanResult()
                {
                    HighestBid = nextUpdate.HighestBid,
                    LowestAsk = nextUpdate.LowestAsk,
                    UnderMid = nextUpdate.UnderMid,
                    Symbol = nextUpdate.GetSymbol(),
                };

                snapshots.Add(optionSnapshot);
            }

            MarketCrossScanResponse?.Invoke(reqId, found, snapshots);
        }

        private void DecodeBestEdgeToTheoRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (BestEdgeToTheoRequest == null)
            {
                return;
            }

            BestEdgeToTheoRequest message = new BestEdgeToTheoRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            int expirationIds = message.ExpirationId;
            Data.Enums.BaseStrategy baseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
            string underlying = message.GetUnderlyingSymbol();

            BestEdgeToTheoRequest?.Invoke(reqId, underlying, baseStrategy, expirationIds);
        }

        private void DecodeBestEdgeToTheoResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (BestEdgeToTheoResponse == null)
            {
                return;
            }

            BestEdgeToTheoResponse message = new BestEdgeToTheoResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;

            double bestBuyEdgeToTheo = message.BestBuyEdgeToTheo;
            double avgBuyEdgeToTheo = message.AvgBuyEdgeToTheo;
            double lastBuyEdgeToTheo = message.LastBuyEdgeToTheo;
            DateTime lastBuyEdgeToTheoTime = message.LastBuyEdgeToTheoTime.FromUnixEpoch();

            double bestSellEdgeToTheo = message.BestSellEdgeToTheo;
            double avgSellEdgeToTheo = message.AvgSellEdgeToTheo;
            double lastSellEdgeToTheo = message.LastSellEdgeToTheo;
            DateTime lastSellEdgeToTheoTime = message.LastSellEdgeToTheoTime.FromUnixEpoch();

            BestEdgeToTheoResponse?.Invoke(reqId, bestBuyEdgeToTheo, avgBuyEdgeToTheo, lastBuyEdgeToTheo, lastBuyEdgeToTheoTime, bestSellEdgeToTheo, avgSellEdgeToTheo, lastSellEdgeToTheo, lastSellEdgeToTheoTime);
        }

        private void DecodeSymbolTradeRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolTradeRequest == null)
            {
                return;
            }

            SymbolTradeRequestMessage message = new SymbolTradeRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            string symbol = message.GetSymbol();
            SymbolTradeRequest?.Invoke(reqId, symbol);
        }

        private void DecodeSymbolsTradeRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolsTradeRequest == null)
            {
                return;
            }

            SymbolsTradeRequestMessage message = new SymbolsTradeRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            bool includeOutright = message.IncludeOutrights == BooleanEnum.True;
            bool includeSpread = message.IncludeSpreads == BooleanEnum.True;
            bool includeBackday = message.IncludeBackdays == BooleanEnum.True;
            DateTime lastDateToInclud = message.LastDate.FromUnixEpoch();
            string underlying = message.GetUnderlyings();
            string symbol = message.GetSymbols();
            string tag = message.GetTags();
            SymbolsTradeRequest?.Invoke(reqId, includeOutright, includeSpread, includeBackday, lastDateToInclud, underlying, symbol, tag);
        }

        private void DecodeSymbolTradeResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolTradeResponse == null)
            {
                return;
            }

            SymbolTradeResponseMessage message = new SymbolTradeResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            FishStatus fishStatus = message.FishStatus;
            double fishLevel = message.FishLevelBuy;
            double fishEdge = message.FishEdgeBuy;
            double fishLevelSell = message.FishLevelSell;
            double fishEdgeSell = message.FishEdgeSell;
            DateTime lastFishTime = message.LastFishTime.FromUnixEpoch();

            SymbolTradeResponse?.Invoke(reqId, fishStatus, fishLevel, fishEdge, fishLevelSell, fishEdgeSell, lastFishTime);
        }

        private void DecodeSymbolsTradeResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolsTradeResponse == null)
            {
                return;
            }

            SymbolsTradeResponseMessage message = new SymbolsTradeResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int reqId = message.RequestId;
            bool lastMessage = message.LastGroup == BooleanEnum.True;

            List<Data.Responses.SymbolFishStatusResponse> responses = new List<Data.Responses.SymbolFishStatusResponse>();

            SymbolsTradeResponseMessage.TradedSymbolsGroup group = message.TradedSymbols;
            while (group.HasNext)
            {
                SymbolsTradeResponseMessage.TradedSymbolsGroup nextUpdate = group.Next();
                Data.Responses.SymbolFishStatusResponse optionSnapshot = new Data.Responses.SymbolFishStatusResponse(nextUpdate.GetSymbol(), nextUpdate.FishStatus, nextUpdate.FishLevelBuy, nextUpdate.FishEdgeBuy, nextUpdate.FishLevelSell, nextUpdate.FishEdgeSell, nextUpdate.LastFishTime.FromUnixEpoch());
                responses.Add(optionSnapshot);
            }

            SymbolsTradeResponse?.Invoke(reqId, responses, lastMessage);
        }

        private void DecodeEdgeScanFeedModelMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            EdgeScanFeedModelMessage message = new EdgeScanFeedModelMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            IEdgeScanFeedModel? model = _orderFactory.GetEdgeScanFeedModel();

            if (model == null)
            {
                return;
            }

            model.IsFirm = message.IsFirm == BooleanEnum.True;
            model.PossibleFirm = message.PossibleFirm == BooleanEnum.True;
            model.PossibleCopyCat = message.PossibleCopyCat == BooleanEnum.True;
            model.Uncertain = message.Uncertain == BooleanEnum.True;

            model.QtyMismatch = message.QtyMismatch == BooleanEnum.True;

            model.EdgeScannerType = (EdgeScannerType)message.ScannerId;

            model.BuyConditionCode = (char)message.BuyConditionCode;
            model.SellConditionCode = (char)message.SellConditionCode;

            model.AdjSide = (Side)message.AdjSide;
            model.IbCobSide = (Side)message.IbCobSide;

            model.LegsCount = message.LegsCount;

            model.BuyQty = message.BuyQty;
            model.SellQty = message.SellQty;

            model.BuyBidSize = message.BuyBidSize;
            model.BuyAskSize = message.BuyAskSize;
            model.SellBidSize = message.SellBidSize;
            model.SellAskSize = message.SellAskSize;

            model.FlipCount = message.FlipCount;


            model.IbCobBid = DecodePriceNull3(message.IbCobBid);
            model.IbCobAsk = DecodePriceNull3(message.IbCobAsk);
            model.AdjustedPnl = DecodePriceNull3(message.AdjustedPnl);
            model.BuyPrice = DecodePriceNull3(message.BuyPrice);
            model.BuyTradeOriginalPrice = DecodePriceNull3(message.BuyTradeOriginalPrice);
            model.SellPrice = DecodePriceNull3(message.SellPrice);
            model.SellTradeOriginalPrice = DecodePriceNull3(message.SellTradeOriginalPrice);
            model.BuyEdgeToTheo = DecodePriceNull3(message.BuyEdgeToTheo);
            model.BuyVolaEdgeToTheo = DecodePriceNull3(message.BuyVolaEdgeToTheo);
            model.SellEdgeToTheo = DecodePriceNull3(message.SellEdgeToTheo);
            model.SellVolaEdgeToTheo = DecodePriceNull3(message.SellVolaEdgeToTheo);
            model.Ttl = DecodePriceNull3(message.Ttl);
            model.SpreadWidth = DecodePriceNull3(message.SpreadWidth);
            model.BuyTradeBid = DecodePriceNull3(message.BuyTradeBid);
            model.BuyTradeMid = DecodePriceNull3(message.BuyTradeMid);
            model.BuyTradeAsk = DecodePriceNull3(message.BuyTradeAsk);
            model.BuyTradeTheo = DecodePriceNull3(message.BuyTradeTheo);
            model.BuyTradeDelta = DecodePriceNull3(message.BuyTradeDelta);
            model.SellTradeBid = DecodePriceNull3(message.SellTradeBid);
            model.SellTradeMid = DecodePriceNull3(message.SellTradeMid);
            model.SellTradeAsk = DecodePriceNull3(message.SellTradeAsk);
            model.SellTradeTheo = DecodePriceNull3(message.SellTradeTheo);
            model.SellTradeDelta = DecodePriceNull3(message.SellTradeDelta);
            model.BuyTradeUnderlyingMid = DecodePriceNull3(message.BuyTradeUnderlyingMid);
            model.SellTradeUnderlyingMid = DecodePriceNull3(message.SellTradeUnderlyingMid);
            model.BuyUnderlyingWidth = DecodePriceNull3(message.BuyUnderlyingWidth);
            model.SellUnderlyingWidth = DecodePriceNull3(message.SellUnderlyingWidth);
            model.DeltaAdjEdge = DecodePriceNull3(message.DeltaAdjEdge);
            model.HighestLegDelta = DecodePriceNull3(message.HighestLegDelta);
            model.SpreadWeightedVega = DecodePriceNull3(message.SpreadWeightedVega);
            model.ReceiveLatency = DecodePriceNull3(message.ReceiveLatency);
            model.IvPctChange = DecodePriceNull3(message.IvPctChange);

            model.BuyTime = message.BuyTime.FromUnixEpoch();
            model.SellTime = message.SellTime.FromUnixEpoch();

            model.NearExpiration = message.NearExpiration.FromUnixEpoch();
            model.FarExpiration = message.FarExpiration.FromUnixEpoch();

            model.UnderSymbol = message.GetUnderSymbol();
            model.SpreadId = message.GetSpreadId();
            model.SpreadType = message.GetSpreadType();
            model.BuySymbol = message.GetBuySymbol();
            model.SellSymbol = message.GetSellSymbol();
            model.ExtraTag = message.GetExtraTag();
            model.Exchange = message.GetExchange();
            model.SessionId = message.GetSessionId();
            model.Description = message.GetDescription();
            model.Message = message.GetMessage();
            model.Reason = message.GetReason();

            _orderFactory?.EdgeScanUpdate(model);
        }

        private void DecodeEdgeScanFeedRunnerStartMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            EdgeScanFeedRunnerStartMessage message = new EdgeScanFeedRunnerStartMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            // filter config
            EdgeScanFeedRunnerFilterConfig filterconfig = new();
            filterconfig.AutoTraderSkipActiveOrders = message.AutoTraderSkipActiveOrders == BooleanEnum.True;
            filterconfig.MarkPrices = message.MarkPrices == BooleanEnum.True;
            filterconfig.MarkPricesMinEdge = message.MarkPricesMinEdge;
            filterconfig.FilterConfigId = message.FilterConfigID;
            filterconfig.AutoTraderEdgeOverride = (AutoTraderEdgeOverride)message.AutoTraderEdgeOverride;
            filterconfig.AutoTraderUseTradePrice = message.AutoTraderUseTradePrice == BooleanEnum.True;
            filterconfig.AutoTraderAttemptBothSides = message.AutoTraderAttemptBothSides == BooleanEnum.True;
            filterconfig.AutoTraderDoNotTradeThroughFillPrice = message.AutoTraderDoNotTradeThroughFillPrice == BooleanEnum.True;
            filterconfig.AutoTraderMinQty = message.AutoTraderMinQty;
            filterconfig.AutoTraderMaxLatency = message.AutoTraderMaxLatency;
            filterconfig.AutoTraderMaxOpenPos = message.AutoTraderMaxOpenPos;
            filterconfig.AutoTraderResubmitCount = message.AutoTraderResubmitCount;
            filterconfig.AutoTraderMaxAllowedOrders = message.AutoTraderMaxAllowedOrders;
            filterconfig.AutoTraderMaxOrderRate = message.AutoTraderMaxOrderRate;
            filterconfig.BlockAlreadyTradedSymbols = message.BlockAlreadyTradedSymbols == BooleanEnum.True;
            filterconfig.BlockAlreadyTradedSymbolsTimeout = message.BlockAlreadyTradedSymbolsTimeout;
            filterconfig.AutoTraderSideSelector = (AutoTraderSideSelection)message.AutoTraderSideSelector;
            filterconfig.AutoTraderRouteOption = (AutoTraderRouteOption)message.AutoTraderRouteOption;
            filterconfig.CutoffTime = message.CutoffTime.FromUnixEpoch();
            filterconfig.AutoStop = message.AutoStop == BooleanEnum.True;
            filterconfig.BlockFirmTradesForTime = message.BlockFirmTradesForTime == BooleanEnum.True;
            filterconfig.BlockFirmTradesForTimeInterval = message.BlockFirmTradesForTimeInterval;
            filterconfig.BlockArea = message.BlockArea == BooleanEnum.True;
            filterconfig.BlockAreaStrikeRange = message.BlockAreaStrikeRange;
            filterconfig.MinPnlForAutoTraderEnabled = message.MinPnlForAutoTraderEnabled == BooleanEnum.True;
            filterconfig.MinPnlForAutoTrader = message.MinPnlForAutoTrader;
            filterconfig.AutoTraderEnablePayUpTicks = message.AutoTraderEnablePayUpTicks == BooleanEnum.True;
            filterconfig.AutoTraderPayUpTicks = message.AutoTraderPayUpTicks;
            filterconfig.MinPnlMaxQty = message.MinPnlMaxQty;
            filterconfig.MinPnlMaxQtyCheckEnabled = message.MinPnlMaxQtyCheckEnabled == BooleanEnum.True;
            Dictionary<string, string> exchToRouteMap = new Dictionary<string, string>();
            EdgeScanFeedRunnerStartMessage.ExchToRouteMapV3Group exchGroup = message.ExchToRouteMapV3;
            while (exchGroup.HasNext)
            {
                EdgeScanFeedRunnerStartMessage.ExchToRouteMapV3Group nextAccount = exchGroup.Next();
                string exch = nextAccount.GetExch();
                string route = nextAccount.GetRoute();
                if (!string.IsNullOrWhiteSpace(exch))
                {
                    exchToRouteMap[exch] = route;
                }
            }
            filterconfig.ExchToRouteMapV3 = exchToRouteMap;

            // AT config
            AutoTraderConfig autoTraderConfig = new();
            autoTraderConfig.UserId = message.UserId;
            autoTraderConfig.RiskCheckId = message.RiskCheckId;
            autoTraderConfig.RiskCheckPassed = message.RiskCheckPassed == BooleanEnum.True;
            autoTraderConfig.Sequence = message.Sequence;
            autoTraderConfig.Venue = (Venue)message.Venue;
            autoTraderConfig.EdgeType = (EdgeType)message.EdgeType;
            autoTraderConfig.EdgeValue = message.EdgeValue;
            autoTraderConfig.TheoModel = (TheoModel)message.TheoModel;
            autoTraderConfig.FishLossTheoModel = (TheoModel)message.FishLossTheoModel;
            autoTraderConfig.AutoCancelTheoModel = (TheoModel)message.AutoCancelTheoModel;
            autoTraderConfig.ForMarketCrossPriceUseSweepEnabled = message.ForMarketCrossPriceUseSweepEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithMaxSizeEnabled = message.CancelWithMaxSizeEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithOrderPriceEdgeToTheoEnabled = message.CancelWithOrderPriceEdgeToTheoEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithOrderPriceEdgeToModelTheoEnabled = message.CancelWithOrderPriceEdgeToModelTheoEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithTimerEnabled = message.CancelWithTimerEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithEdgeToTheoEnabled = message.CancelWithEdgeToTheoEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithEdgeToAdjTheoEnabled = message.CancelWithEdgeToAdjTheoEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithChangeInUnderlyingPxEnabled = message.CancelWithChangeInUnderlyingPxEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithChangeInUnderlyingDeltaPxEnabled = message.CancelWithChangeInUnderlyingDeltaPxEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithEdgeToMidEnabled = message.CancelWithEdgeToMidEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithChangeInWidthEnabled = message.CancelWithChangeInWidthEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithMaxWidthEnabled = message.CancelWithMaxWidthEnabled == BooleanEnum.True;
            autoTraderConfig.CancelWithMaxSizeLimit = message.CancelWithMaxSizeLimit;
            autoTraderConfig.CancelWithOrderPriceEdgeToTheo = message.CancelWithOrderPriceEdgeToTheo;
            autoTraderConfig.CancelWithOrderPriceEdgeToModelTheo = message.CancelWithOrderPriceEdgeToModelTheo;
            autoTraderConfig.CancelWithTimer = message.CancelWithTimer;
            autoTraderConfig.CancelWithTheoEdge = message.CancelWithTheoEdge;
            autoTraderConfig.CancelWithAdjTheoEdge = message.CancelWithAdjTheoEdge;
            autoTraderConfig.CancelWithUnderlyingPxThreshold = message.CancelWithUnderlyingPxThreshold;
            autoTraderConfig.CancelWithUnderlyingDeltaPx = message.CancelWithUnderlyingDeltaPx;
            autoTraderConfig.CancelWithMidEdge = message.CancelWithMidEdge;
            autoTraderConfig.CancelWithWidthThreshold = message.CancelWithWidthThreshold;
            autoTraderConfig.CancelWithMaxWidthThreshold = message.CancelWithMaxWidthThreshold;
            autoTraderConfig.MinEdgeToTheoCheckEnabled = message.MinEdgeToTheoCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToHwTheoCheckEnabled = message.MinEdgeToHwTheoCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToV0TheoCheckEnabled = message.MinEdgeToV0TheoCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToMidCheckEnabled = message.MinEdgeToMidCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToEmaCheckEnabled = message.MinEdgeToEmaCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToMarketCheckEnabled = message.MinEdgeToMarketCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinBidPercentCheckEnabled = message.MinBidPercentCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MaxBidPercentCheckEnabled = message.MaxBidPercentCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinBidAskSizeCheckEnabled = message.MinBidAskSizeCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEmaWidthPercentEdgeToTheoCheckEnabled = message.MinEmaWidthPercentEdgeToTheoCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinBidCheckEnabled = message.MinBidCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinTheoCheckEnabled = message.MinTheoCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MinEdgeToTheo = message.MinEdgeToTheo;
            autoTraderConfig.MinEdgeToHwTheo = message.MinEdgeToHwTheo;
            autoTraderConfig.MinEdgeToV0Theo = message.MinEdgeToV0Theo;
            autoTraderConfig.MinEdgeToMid = message.MinEdgeToMid;
            autoTraderConfig.MinEdgeToEma = message.MinEdgeToEma;
            autoTraderConfig.MinEdgeToMarket = message.MinEdgeToMarket;
            autoTraderConfig.MinBidPercent = message.MinBidPercent;
            autoTraderConfig.MaxBidPercent = message.MaxBidPercent;
            autoTraderConfig.MinBidAskSize = message.MinBidAskSize;
            autoTraderConfig.MinEmaWidthPercentEdgeToTheoCheckEdge = message.MinEmaWidthPercentEdgeToTheoCheckEdge;
            autoTraderConfig.MinBidCheckBidValue = message.MinBidCheckBidValue;
            autoTraderConfig.MinTheoCheckTheoValue = message.MinTheoCheckTheoValue;
            autoTraderConfig.EdgeToAdjTheoWithOverrideUsePercentage = message.EdgeToAdjTheoWithOverrideUsePercentage == BooleanEnum.True;
            autoTraderConfig.EdgeToAdjTheoWithOverrideStatic = message.EdgeToAdjTheoWithOverrideStatic;
            autoTraderConfig.EdgeToAdjTheoWithOverridePercent = message.EdgeToAdjTheoWithOverridePercent;
            autoTraderConfig.CheckForRecentAttempt = message.CheckForRecentAttempt == BooleanEnum.True;
            autoTraderConfig.CheckForRecentAttemptTimespan = message.CheckForRecentAttemptTimespan;
            autoTraderConfig.CheckForRecentFill = message.CheckForRecentFill == BooleanEnum.True;
            autoTraderConfig.CheckForRecentFillTimespan = message.CheckForRecentFillTimespan;
            autoTraderConfig.MinSpxAuction = message.MinSpxAuction;
            autoTraderConfig.MinSpxSpreadAuction = message.MinSpxSpreadAuction;
            autoTraderConfig.MinSingleLegAuction = message.MinSingleLegAuction;
            autoTraderConfig.MinSpreadAuction = message.MinSpreadAuction;
            autoTraderConfig.BestOfAdjTheoEnabled = message.BestOfAdjTheoEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfAdjTheoEdge = message.BestOfAdjTheoEdge;
            autoTraderConfig.BestOfAdjTheoModel = message.BestOfAdjTheoModel;
            autoTraderConfig.BestOfHwTheoEnabled = message.BestOfHwTheoEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfHwTheoEdge = message.BestOfHwTheoEdge;
            autoTraderConfig.BestOfV0TheoEnabled = message.BestOfV0TheoEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfV0TheoEdge = message.BestOfV0TheoEdge;
            autoTraderConfig.BestOfMidEnabled = message.BestOfMidEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfMidEdge = message.BestOfMidEdge;
            autoTraderConfig.BestOfEmaEnabled = message.BestOfEmaEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfEmaEdge = message.BestOfEmaEdge;
            autoTraderConfig.BestOfBidPercentEnabled = message.BestOfBidPercentEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfBidPercentEdge = message.BestOfBidPercentEdge;
            autoTraderConfig.BestOfDigBidPercentEnabled = message.BestOfDigBidPercentEnabled == BooleanEnum.True;
            autoTraderConfig.BestOfDigBidPercentEdge = message.BestOfDigBidPercentEdge;
            autoTraderConfig.MaxDigBidPercentCheckEnabled = message.MaxDigBidPercentCheckEnabled == BooleanEnum.True;
            autoTraderConfig.MaxDigBidPercent = message.MaxDigBidPercent;

            autoTraderConfig.AutoPermEnabled = message.AutoPermEnabled == BooleanEnum.True;
            autoTraderConfig.AutoPermMinEdge = message.AutoPermMinEdge;
            autoTraderConfig.AutoPermOrderCount = message.AutoPermOrderCount;
            autoTraderConfig.AutoPermMaxGeneration = message.AutoPermMaxGeneration;
            autoTraderConfig.AutoPermSubmissionStyle = message.AutoPermSubmissionStyleInActingVersion()
                ? (AutoPermSubmissionStyle)message.AutoPermSubmissionStyle
                : AutoPermSubmissionStyle.Sequential;
            autoTraderConfig.AutoPermOrderInitialSize = message.AutoPermOrderInitialSizeInActingVersion()
                ? message.AutoPermOrderInitialSize
                : 1;

            EdgeScanFeedRunnerStartMessage.AutomationConfigsGroup? automationConfigs = message.AutomationConfigs;
            autoTraderConfig.UnderlyingToAutomationConfigs.Clear();
            while (automationConfigs.HasNext)
            {
                EdgeScanFeedRunnerStartMessage.AutomationConfigsGroup? automationConfigMessage = automationConfigs.Next();
                AutomationConfig automationConfig = new AutomationConfig();
                var isDefault = automationConfigMessage.IsDefault == BooleanEnum.True;
                var underlying = automationConfigMessage.GetUnderlyingSymbol();
                var increment = automationConfigMessage.Increment;
                if (underlying != null)
                {
                    automationConfig.ConfigKey = new ConfigKey()
                    {
                        Underlying = underlying,
                        Increment = increment,
                    };
                }

                automationConfig.LoopingEnabled = automationConfigMessage.LoopingEnabled == BooleanEnum.True;
                automationConfig.CloseEdgeType = (SelectionType)automationConfigMessage.CloseEdgeType;
                automationConfig.StaticCloseEdge = automationConfigMessage.StaticCloseEdge;
                automationConfig.StaticMinLoopEdge = automationConfigMessage.StaticMinLoopEdge;
                automationConfig.StaticMaxLoss = automationConfigMessage.StaticMaxLoss;
                automationConfig.LooperDynamicRouting = automationConfigMessage.LooperDynamicRouting == BooleanEnum.True;
                automationConfig.AttemptIncrementUsingDynamicRoute = automationConfigMessage.AttemptIncrementUsingDynamicRoute == BooleanEnum.True;
                automationConfig.EnableDynamicRouteForOpeningOrders = automationConfigMessage.EnableDynamicRouteForOpeningOrders == BooleanEnum.True;
                automationConfig.EnableDynamicRouteForClosingOrders = automationConfigMessage.EnableDynamicRouteForClosingOrders == BooleanEnum.True;
                automationConfig.CloseIntervalType = (SelectionType)automationConfigMessage.CloseIntervalType;
                automationConfig.StaticCloseInterval = automationConfigMessage.StaticCloseInterval;
                automationConfig.StaticCloseIntervalMax = automationConfigMessage.StaticCloseIntervalMax;
                automationConfig.StaticLoopInterval = automationConfigMessage.StaticLoopInterval;
                automationConfig.StaticLoopIntervalMax = automationConfigMessage.StaticLoopIntervalMax;
                automationConfig.IncrementType = (SelectionType)automationConfigMessage.IncrementType;
                automationConfig.StaticIncrement = automationConfigMessage.StaticIncrement;
                automationConfig.SizeUpType = (SelectionType)automationConfigMessage.SizeUpType;
                automationConfig.StaticSizeUpLoopCountBeforeSizeup = automationConfigMessage.StaticSizeUpLoopCountBeforeSizeup;
                automationConfig.StaticSizeUp = automationConfigMessage.StaticSizeUp;
                automationConfig.AutoAggressorEnabled = automationConfigMessage.AutoAggressorEnabled == BooleanEnum.True;
                automationConfig.AutoAggressorMode = (AutoAggressorMode)automationConfigMessage.AutoAggressorMode;
                automationConfig.AutoAggressorEdgeTightenMode = (AutoAggressorEdgeTightenMode)automationConfigMessage.AutoAggressorEdgeTightenMode;
                automationConfig.AutoAggressorEdgeTightenPercentage = automationConfigMessage.AutoAggressorEdgeTightenPercentage;
                automationConfig.ScratchOnLowDeltaSize = automationConfigMessage.ScratchOnLowDeltaSize == BooleanEnum.True;
                automationConfig.ScratchOnLowDeltaMax = automationConfigMessage.ScratchOnLowDeltaMax;
                automationConfig.ScratchOnLowDeltaMaxLoss = automationConfigMessage.ScratchOnLowDeltaMaxLoss;
                automationConfig.ScratchOnLowDeltaMinSize = automationConfigMessage.ScratchOnLowDeltaMinSize;
                automationConfig.FreeLookRequireMinFillTime = automationConfigMessage.FreeLookRequireMinFillTime == BooleanEnum.True;
                automationConfig.FreeLookMinFillTime = automationConfigMessage.FreeLookMinFillTime;
                automationConfig.FreeLookOnLosers = automationConfigMessage.FreeLookOnLosers == BooleanEnum.True;
                automationConfig.FreeLookOnLosersMax = automationConfigMessage.FreeLookOnLosersMax;
                automationConfig.FreeLookOnAll = automationConfigMessage.FreeLookOnAll == BooleanEnum.True;
                automationConfig.FreeWhenGettingCloseEdge = automationConfigMessage.FreeWhenGettingCloseEdge == BooleanEnum.True;
                automationConfig.FreeLookAfterLastAttempt = automationConfigMessage.FreeLookAfterLastAttempt == BooleanEnum.True;
                automationConfig.FreeLookBackUpIncrement = automationConfigMessage.FreeLookBackUpIncrement;
                automationConfig.FreeLookOnAllWalkBackIncrement = automationConfigMessage.FreeLookOnAllWalkBackIncrement;
                automationConfig.LoopFreeLookOnAllUsingTicks = automationConfigMessage.LoopFreeLookOnAllUsingTicks == BooleanEnum.True;
                automationConfig.FreeLookOnAllIncrementTicks = automationConfigMessage.FreeLookOnAllIncrementTicks;
                automationConfig.FreeLookOnAllWalkBackIncrementTicks = automationConfigMessage.FreeLookOnAllWalkBackIncrementTicks;
                automationConfig.LoopFreeLookOnNickelNames = automationConfigMessage.LoopFreeLookOnNickelNames == BooleanEnum.True;
                automationConfig.LoopFreeLookOnNickelNamesIncrement = automationConfigMessage.LoopFreeLookOnNickelNamesIncrement;
                automationConfig.LoopFreeLookOnDimeNames = automationConfigMessage.LoopFreeLookOnDimeNames == BooleanEnum.True;
                automationConfig.LoopFreeLookOnDimeNamesIncrement = automationConfigMessage.LoopFreeLookOnDimeNamesIncrement;
                automationConfig.MaintainLastEdge = automationConfigMessage.MaintainLastEdge == BooleanEnum.True;
                automationConfig.AttemptResubmitCount = automationConfigMessage.AttemptResubmitCount;
                automationConfig.LastFillResubmitCount = automationConfigMessage.LastFillResubmitCount;
                automationConfig.MaxNumberOfLoops = automationConfigMessage.MaxNumberOfLoops;
                automationConfig.PartialFillPercentage = automationConfigMessage.PartialFillPercentage;
                automationConfig.PartialFillResubmit = automationConfigMessage.PartialFillResubmit;
                automationConfig.LoopPricingMode = (LoopPricingMode)automationConfigMessage.LoopPricingMode;
                automationConfig.AdjustClosingPriceToMarketWinnersOnly = automationConfigMessage.AdjustClosingPriceToMarketWinnersOnly == BooleanEnum.True;
                automationConfig.PxCrossOption = (PxCrossOption)automationConfigMessage.PxCrossOption;
                automationConfig.ClosePxCrossOption = (PxCrossOption)automationConfigMessage.ClosePxCrossOption;
                automationConfig.AutoHedgeOnClose = automationConfigMessage.AutoHedgeOnClose == BooleanEnum.True;
                automationConfig.AutoHedgeOnCloseSizeOnly = automationConfigMessage.AutoHedgeOnCloseSizeOnly == BooleanEnum.True;
                automationConfig.MinHedgeHouseEdge = automationConfigMessage.MinHedgeHouseEdge;
                automationConfig.AutoHedgeOnFailure = automationConfigMessage.AutoHedgeOnFailure == BooleanEnum.True;
                automationConfig.AutoHedgePartial = automationConfigMessage.AutoHedgePartial == BooleanEnum.True;
                automationConfig.AutoLegEnabled = automationConfigMessage.AutoLegEnabled == BooleanEnum.True;
                automationConfig.AutoLegMaxWidth = automationConfigMessage.AutoLegMaxWidth;
                automationConfig.AutoLegCloseEdge = automationConfigMessage.AutoLegCloseEdge;
                automationConfig.AutoLegMaxLoss = automationConfigMessage.AutoLegMaxLoss;
                automationConfig.AutoLegCloseIncrement = automationConfigMessage.AutoLegCloseIncrement;
                automationConfig.AutoLegRestTime = automationConfigMessage.AutoLegRestTime;
                automationConfig.OpenRoute = automationConfigMessage.GetOpenRoute();
                automationConfig.CloseRoute = automationConfigMessage.GetCloseRoute();
                automationConfig.OpenRouteSingleLeg = automationConfigMessage.GetOpenRouteSingleLeg();
                automationConfig.CloseRouteSingleLeg = automationConfigMessage.GetCloseRouteSingleLeg();
                automationConfig.OpenRouteSize = automationConfigMessage.GetOpenRouteSize();
                automationConfig.CloseRouteSize = automationConfigMessage.GetCloseRouteSize();
                automationConfig.OpenRouteSingleLegSize = automationConfigMessage.GetOpenRouteSingleLegSize();
                automationConfig.CloseRouteSingleLegSize = automationConfigMessage.GetCloseRouteSingleLegSize();
                automationConfig.LoopFreeLookOnNickelNamesRoute = automationConfigMessage.GetLoopFreeLookOnNickelNamesRoute();
                automationConfig.LoopFreeLookOnDimeNamesRoute = automationConfigMessage.GetLoopFreeLookOnDimeNamesRoute();
                automationConfig.AutoLegCloseRoute = automationConfigMessage.GetAutoLegCloseRoute();

                automationConfig.DynamicCloseEdge ??= new();
                automationConfig.DynamicCloseEdge.PercentBidRangeEnabled =
                    automationConfigMessage.DynamicEdgePercentBidRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.BaseEdgeEnabled =
                    automationConfigMessage.DynamicEdgeBaseEdgeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.EmaRangeEnabled =
                    automationConfigMessage.DynamicEdgeEmaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.TradePxRangeEnabled =
                    automationConfigMessage.DynamicEdgeTradePxRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.MinMarketWidthEnabled =
                    automationConfigMessage.DynamicEdgeMinMarketWidthEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.MinMarketCrossEnabled =
                    automationConfigMessage.DynamicEdgeMinMarketCrossEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.TheoRangeEnabled =
                    automationConfigMessage.DynamicEdgeTheoRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.VolaRangeEnabled =
                    automationConfigMessage.DynamicEdgeVolaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.VolaModel =
                    (TheoModel)automationConfigMessage.DynamicEdgeVolaModel;
                automationConfig.DynamicCloseEdge.DynamicVolaRangeEnabled =
                    automationConfigMessage.DynamicEdgeDynamicVolaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.DynamicVolaModel =
                    (TheoModel)automationConfigMessage.DynamicEdgeDynamicVolaModel;
                automationConfig.DynamicCloseEdge.DynamicLookupMode =
                    automationConfigMessage.DynamicEdgeDynamicLookupMode == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.UnderDivisor = automationConfigMessage.DynamicEdgeUnderDivisor;

                automationConfig.DynamicCloseEdge.DteTable?.Clear();
                automationConfig.DynamicCloseEdge.DynamicDteTable?.Clear();
                var dteConfigs = automationConfigMessage.DteConfigs;
                while (dteConfigs.HasNext)
                {
                    var dteConfigMessage = dteConfigs.Next();
                    var isDynamic = dteConfigMessage.IsDynamic == BooleanEnum.True;

                    DaysToExpirationEdgeModel dteConfig = new DaysToExpirationEdgeModel();
                    dteConfig.Active = dteConfigMessage.Active == BooleanEnum.True;
                    dteConfig.DaysToExpiration = dteConfigMessage.DaysToExpiration;
                    dteConfig.MinBidAskSize = dteConfigMessage.MinBidAskSize;
                    dteConfig.MinIncrement = dteConfigMessage.MinIncrement;
                    dteConfig.MinWidth = dteConfigMessage.MinWidth;
                    dteConfig.MinSpacingForVertical = dteConfigMessage.MinSpacingForVertical;
                    dteConfig.MinSpacingForFlys = dteConfigMessage.MinSpacingForFlys;
                    dteConfig.MinSpacingForVerticalPercentage = dteConfigMessage.MinSpacingForVerticalPercentage;
                    dteConfig.MinSpacingForFlysPercentage = dteConfigMessage.MinSpacingForFlysPercentage;
                    dteConfig.BaseEdge = dteConfigMessage.BaseEdge;
                    dteConfig.CloseEdge = dteConfigMessage.CloseEdge;
                    dteConfig.LoopMinEdge = dteConfigMessage.LoopMinEdge;
                    dteConfig.AutoPermMinEdge = dteConfigMessage.AutoPermMinEdge;
                    dteConfig.VerticalQty = dteConfigMessage.VerticalQty;
                    dteConfig.Qty = dteConfigMessage.Qty;
                    dteConfig.MaxPercentBid = dteConfigMessage.MaxPercentBid;
                    dteConfig.LoopMaxLoss = dteConfigMessage.LoopMaxLoss;
                    dteConfig.AdditionalEdgePerContract = dteConfigMessage.AdditionalEdgePerContract;
                    dteConfig.AdditionalEdgePerWeightedVega = dteConfigMessage.AdditionalEdgePerWeightedVega;
                    dteConfig.MaxAllowedAboveEma = dteConfigMessage.MaxAllowedAboveEma;
                    dteConfig.MaxAllowedAboveTheo = dteConfigMessage.MaxAllowedAboveTheo;
                    dteConfig.MaxAllowedAboveVola = dteConfigMessage.MaxAllowedAboveVola;
                    dteConfig.MinMarketWidth = dteConfigMessage.MinMarketWidth;
                    dteConfig.MaxThroughTradePx = dteConfigMessage.MaxThroughTradePx;
                    dteConfig.MinMarketCross = dteConfigMessage.MinMarketCross;

                    if (!isDynamic)
                    {
                        automationConfig.DynamicCloseEdge.DteTable ??= [];
                        automationConfig.DynamicCloseEdge.DteTable.Add(dteConfig);
                    }
                    else
                    {
                        dteConfig.DynamicBaseEdge = dteConfigMessage.DynamicBaseEdge;
                        dteConfig.DynamicBaseEdgeAddition = dteConfigMessage.DynamicBaseEdgeAddition;
                        dteConfig.AdditionalEdgePerWidth = dteConfigMessage.AdditionalEdgePerWidth;
                        dteConfig.DynamicCloseEdge = dteConfigMessage.DynamicCloseEdge;
                        dteConfig.DynamicCloseEdgeAddition = dteConfigMessage.DynamicCloseEdgeAddition;
                        dteConfig.AdditionalCloseEdgePerWidth = dteConfigMessage.AdditionalCloseEdgePerWidth;
                        dteConfig.DynamicAutoPermMinEdge = dteConfigMessage.DynamicAutoPermMinEdge;
                        dteConfig.DynamicAutoPermMinEdgeAddition = dteConfigMessage.DynamicAutoPermMinEdgeAddition;
                        dteConfig.DynamicLoopMinEdge = dteConfigMessage.DynamicLoopMinEdge;
                        dteConfig.DynamicLoopMinEdgeAddition = dteConfigMessage.DynamicLoopMinEdgeAddition;
                        dteConfig.DynamicLoopMaxLoss = dteConfigMessage.DynamicLoopMaxLoss;
                        dteConfig.DynamicLoopMaxLossAddition = dteConfigMessage.DynamicLoopMaxLossAddition;
                        dteConfig.DynamicAdditionalEdgePerContract =
                            dteConfigMessage.DynamicAdditionalEdgePerContract;
                        dteConfig.DynamicAdditionalEdgePerContractAddition =
                            dteConfigMessage.DynamicAdditionalEdgePerContractAddition;
                        dteConfig.DynamicAdditionalEdgePerWeightedVega =
                            dteConfigMessage.DynamicAdditionalEdgePerWeightedVega;
                        dteConfig.DynamicAdditionalEdgePerWeightedVegaAddition =
                            dteConfigMessage.DynamicAdditionalEdgePerWeightedVegaAddition;
                        dteConfig.DynamicMaxAllowedPercentBid = dteConfigMessage.DynamicMaxAllowedPercentBid;
                        dteConfig.DynamicMaxAllowedPercentBidAddition =
                            dteConfigMessage.DynamicMaxAllowedPercentBidAddition;
                        dteConfig.DynamicMaxAllowedAboveEma = dteConfigMessage.DynamicMaxAllowedAboveEma;
                        dteConfig.DynamicMaxAllowedAboveEmaAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveEmaAddition;
                        dteConfig.DynamicMaxAllowedAboveTheo = dteConfigMessage.DynamicMaxAllowedAboveTheo;
                        dteConfig.DynamicMaxAllowedAboveTheoAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveTheoAddition;
                        dteConfig.DynamicMaxAllowedAboveVola = dteConfigMessage.DynamicMaxAllowedAboveVola;
                        dteConfig.DynamicMaxAllowedAboveVolaAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveVolaAddition;
                        dteConfig.DynamicMinMarketWidth = dteConfigMessage.DynamicMinMarketWidth;
                        dteConfig.DynamicMinMarketWidthAddition = dteConfigMessage.DynamicMinMarketWidthAddition;
                        automationConfig.DynamicCloseEdge.DynamicDteTable ??= [];
                        automationConfig.DynamicCloseEdge.DynamicDteTable.Add(dteConfig);
                    }
                }

                automationConfig.DynamicCloseEdge.DeltaTable?.Clear();
                var deltaConfigs = automationConfigMessage.DeltaConfigs;
                while (deltaConfigs.HasNext)
                {
                    var deltaConfigMessage = deltaConfigs.Next();
                    DeltaEdgeModel deltaConfig = new DeltaEdgeModel();
                    deltaConfig.Active = deltaConfigMessage.Active == BooleanEnum.True;
                    deltaConfig.Delta = deltaConfigMessage.Delta;
                    deltaConfig.AdditionalEdgePerContract = deltaConfigMessage.AdditionalEdgePerContract;
                    deltaConfig.AddedEdge = deltaConfigMessage.AddedEdge;

                    automationConfig.DynamicCloseEdge.DeltaTable ??= [];
                    automationConfig.DynamicCloseEdge.DeltaTable.Add(deltaConfig);
                }
                if (automationConfigMessage.DynamicCloseEdgeEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicCloseEdge = null;
                }

                var dynamicSizeUpConfigs = automationConfigMessage.DynamicSizeUpConfigs;
                while (dynamicSizeUpConfigs.HasNext)
                {
                    var dynamicSizeUpConfig = dynamicSizeUpConfigs.Next();

                    SizeupConfigModel sizeUpConfig = new SizeupConfigModel();
                    sizeUpConfig.Enabled = dynamicSizeUpConfig.Enabled == BooleanEnum.True;
                    sizeUpConfig.Edge = dynamicSizeUpConfig.Edge;
                    sizeUpConfig.AdditionalEdgePerContract = dynamicSizeUpConfig.AdditionalEdgePerContract;
                    sizeUpConfig.MaxAbsDelta = dynamicSizeUpConfig.MaxAbsDelta;
                    sizeUpConfig.MaxUnderWidth = dynamicSizeUpConfig.MaxUnderWidth;
                    sizeUpConfig.Size = dynamicSizeUpConfig.Size;
                    sizeUpConfig.ResubmitSizeOption = (ResubmitSizeOption)dynamicSizeUpConfig.ResubmitSizeOption;
                    sizeUpConfig.RequiredLoop = dynamicSizeUpConfig.RequiredLoop;
                    sizeUpConfig.ResubmitCount = dynamicSizeUpConfig.ResubmitCount;
                    sizeUpConfig.MatchSignalQtyLimit = dynamicSizeUpConfig.MatchSignalQtyLimit;

                    automationConfig.DynamicSizeUp ??= new();
                    automationConfig.DynamicSizeUp.SizeUpConfigs ??= [];
                    automationConfig.DynamicSizeUp.SizeUpConfigs.Add(sizeUpConfig);
                }
                if (automationConfigMessage.DynamicSizeUpEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicSizeUp = null;
                }

                var dynamicCloseIntervals = automationConfigMessage.DynamicIntervalConfigs;
                while (dynamicCloseIntervals.HasNext)
                {
                    var dynamicCloseInterval = dynamicCloseIntervals.Next();

                    IntervalModel intervalModel = new IntervalModel();
                    intervalModel.Active = dynamicCloseInterval.Active == BooleanEnum.True;
                    intervalModel.MinDelta = dynamicCloseInterval.MinDelta;
                    intervalModel.MaxDelta = dynamicCloseInterval.MaxDelta;
                    intervalModel.AttemptedEdge = dynamicCloseInterval.AttemptedEdge;
                    intervalModel.Interval = dynamicCloseInterval.Interval;
                    intervalModel.ResubmitCount = dynamicCloseInterval.ResubmitCount;
                    intervalModel.Route = dynamicCloseInterval.GetRoute();
                    intervalModel.DisableRounding = dynamicCloseInterval.DisableRounding == BooleanEnum.True;

                    automationConfig.DynamicCloseInterval ??= new();
                    automationConfig.DynamicCloseInterval.IntervalTable ??= [];
                    automationConfig.DynamicCloseInterval.IntervalTable.Add(intervalModel);
                }

                if (automationConfigMessage.DynamicCloseIntervalEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicCloseInterval = null;
                }
                else
                {
                    automationConfig.DynamicCloseInterval ??= new();
                    automationConfig.DynamicCloseInterval.DefaultInterval = automationConfigMessage.DynamicIntervalDefaultInterval;
                    automationConfig.DynamicCloseInterval.DefaultResubmit = automationConfigMessage.DynamicIntervalDefaultResubmitCount;
                }

                automationConfig.ExchToRouteList?.Clear();
                var exchToRouteList = automationConfigMessage.ExchToRouteList;
                while (exchToRouteList.HasNext)
                {
                    var exchRoutePair = exchToRouteList.Next();
                    automationConfig.ExchToRouteList ??= [];
                    automationConfig.ExchToRouteList.Add(Tuple.Create(exchRoutePair.GetExch(), exchRoutePair.GetRoute()));
                }

                automationConfig.DynamicIncrement?.Clear();
                var dynamicIncrements = automationConfigMessage.DynamicIncrementConfigs;
                while (dynamicIncrements.HasNext)
                {
                    var dynamicIncrement = dynamicIncrements.Next();
                    var dynamicIncrementModel = new DynamicIncrementModel()
                    {
                        Edge = dynamicIncrement.Edge,
                        Increment = dynamicIncrement.Increment
                    };
                    automationConfig.DynamicIncrement ??= [];
                    automationConfig.DynamicIncrement.Add(dynamicIncrementModel);
                }

                if (isDefault)
                {
                    autoTraderConfig.DefaultAutomationConfig = automationConfig;
                }
                else if (automationConfig.ConfigKey != null)
                {
                    autoTraderConfig.UnderlyingToAutomationConfigs.Add(automationConfig);
                }
            }

            autoTraderConfig.OpenRouteSmartMap?.Clear();
            var openRouteSmartMap = message.OpenRouteSmartMap;
            while (openRouteSmartMap.HasNext)
            {
                var routeTimerPair = openRouteSmartMap.Next();
                autoTraderConfig.OpenRouteSmartMap ??= [];
                autoTraderConfig.OpenRouteSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            autoTraderConfig.CloseRouteSmartMap?.Clear();
            var CloseRouteSmartMap = message.CloseRouteSmartMap;
            while (CloseRouteSmartMap.HasNext)
            {
                var routeTimerPair = CloseRouteSmartMap.Next();
                autoTraderConfig.CloseRouteSmartMap ??= [];
                autoTraderConfig.CloseRouteSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            autoTraderConfig.OpenRouteSingleLegSmartMap?.Clear();
            var openRouteSingleLegSmartMap = message.OpenRouteSingleLegSmartMap;
            while (openRouteSingleLegSmartMap.HasNext)
            {
                var routeTimerPair = openRouteSingleLegSmartMap.Next();
                autoTraderConfig.OpenRouteSingleLegSmartMap ??= [];
                autoTraderConfig.OpenRouteSingleLegSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            autoTraderConfig.CloseRouteSingleLegSmartMap?.Clear();
            var CloseRouteSingleLegSmartMap = message.CloseRouteSingleLegSmartMap;
            while (CloseRouteSingleLegSmartMap.HasNext)
            {
                var routeTimerPair = CloseRouteSingleLegSmartMap.Next();
                autoTraderConfig.CloseRouteSingleLegSmartMap ??= [];
                autoTraderConfig.CloseRouteSingleLegSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }
            string runnerId = message.GetRunnerId();
            filterconfig.FilterString = message.GetFilterString();
            filterconfig.FilterConfig = message.GetFilterConfig();
            autoTraderConfig.RiskCheckMessage = message.GetRiskCheckMessage();
            autoTraderConfig.ConfigId = message.GetConfigId();
            autoTraderConfig.ConfigName = message.GetConfigName();
            autoTraderConfig.SweepRoute = message.GetSweepRoute();

            string orderDefaultAccount = message.GetOrderDefaultAccount();
            string orderDefaultRoute = message.GetOrderDefaultRoute();
            string orderDefaultSingleLegRoute = message.GetOrderDefaultSingleLegRoute();
            string orderDefaultRouteSpxRutXsp = message.GetOrderDefaultRouteSpxRutXsp();
            string orderDefaultRouteNdx = message.GetOrderDefaultRouteNdx();
            OrderSubmissionDefaults orderDefaults = new OrderSubmissionDefaults
            {
                Account = string.IsNullOrEmpty(orderDefaultAccount) ? null : orderDefaultAccount,
                Route = string.IsNullOrEmpty(orderDefaultRoute) ? null : orderDefaultRoute,
                SingleLegRoute = string.IsNullOrEmpty(orderDefaultSingleLegRoute) ? null : orderDefaultSingleLegRoute,
                RouteSpxRutXsp = string.IsNullOrEmpty(orderDefaultRouteSpxRutXsp) ? null : orderDefaultRouteSpxRutXsp,
                RouteNdx = string.IsNullOrEmpty(orderDefaultRouteNdx) ? null : orderDefaultRouteNdx,
            };

            // BlockedSymbolModel was added in schema v17 (data id=134); older senders won't
            // include it. Empty / whitespace JSON means "no instance ban list".
            string blockedSymbolJson = message.GetBlockedSymbolModel();
            BlockedSymbolModel? blockedSymbolModel = null;
            if (!string.IsNullOrWhiteSpace(blockedSymbolJson))
            {
                try
                {
                    blockedSymbolModel = JsonConvert.DeserializeObject<BlockedSymbolModel>(blockedSymbolJson);
                    blockedSymbolModel?.UpdateSet();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "decode_blocked_symbol_model_failed runner={RunnerId}", runnerId);
                }
            }

            EdgeScanFeedRunnerStartRequest startRequest = new EdgeScanFeedRunnerStartRequest
            {
                RunnerId = runnerId,
                AutoTraderConfig = autoTraderConfig,
                FilterConfig = filterconfig,
                OrderDefaults = orderDefaults,
                ReportFilter = (SubmissionReportFilter)message.ReportFilter,
                BlockedSymbolModel = blockedSymbolModel,
            };
            EdgeScanFeedRunnerStartRequest?.Invoke(startRequest);

        }
        private void DecodeEdgeScanFeedRunnerStopMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            EdgeScanFeedRunnerStopMessage message = new EdgeScanFeedRunnerStopMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string runnerId = message.GetRunnerId();
            EdgeScanFeedRunnerStopRequest?.Invoke(runnerId);
        }
        private void DecodeEdgeScanFeedRunnerUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            EdgeScanFeedRunnerUpdateMessage message = new EdgeScanFeedRunnerUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string runnerId = message.GetRunnerId();
            EdgeScanFeedRunnerState runnerState = (EdgeScanFeedRunnerState)message.RunnerState;
            EdgeScanFeedRunnerChanged?.Invoke(runnerId, runnerState);
        }

        private void DecodeTradeSlimMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (TradeSlimUpdate == null)
            {
                return;
            }

            TradeSlimMessage message = new TradeSlimMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            TradeSlim tradeSlim = new TradeSlim
            {
                UnsureSymbol = message.UnsureSymbol == BooleanEnum.True,
                Condition = (char)message.Condition,

                LegCount = message.LegCount,
                Quantity = message.Quantity,
                BidSize = message.BidSize,
                AskSize = message.AskSize,

                Price = message.Price,
                TradeDelta = message.TradeDelta,
                Bid = message.Bid,
                Ask = message.Ask,
                UnderBid = message.UnderBid,
                UnderAsk = message.UnderAsk,
                DeltaAdjTheo = message.DeltaAdjTheo,
                VolaDeltaAdjTheo = message.VolaDeltaAdjTheo,
                ImpliedVol = message.ImpliedVol,
                Vega = message.Vega,

                TradeTime = message.TradeTime.FromUnixEpoch(),

                Symbol = message.GetSymbol(),
                UnderSymbol = message.GetUnderSymbol(),
                Exchange = message.GetExchange(),
            };

            TradeSlimUpdate?.Invoke(tradeSlim);
        }

        private void DecodeTradeFeedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            TradeFeedMessage message = new TradeFeedMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.Id;
            bool isLast = message.IsLast == BooleanEnum.True;
            List<ITradeFeedModel> updates = new();
            TradeFeedMessage.TradesGroup tradesGroup = message.Trades;
            while (tradesGroup.HasNext)
            {
                var nextUpdate = tradesGroup.Next();

                ITradeFeedModel? model = _orderFactory.GetTradeFeedModel();

                if (model == null)
                {
                    break;
                }

                model.Id = nextUpdate.Id;
                model.IsFirm = nextUpdate.IsFirm == BooleanEnum.True;
                model.IsCopyCat = nextUpdate.IsCopyCat == BooleanEnum.True;
                model.Quantity = nextUpdate.Quantity;
                model.BaseStrategy = (Data.Enums.BaseStrategy)nextUpdate.BaseStrategy;
                model.Side = (Side)nextUpdate.Side;
                model.Price = nextUpdate.Price;
                model.Bid = nextUpdate.Bid;
                model.Ask = nextUpdate.Ask;
                model.Delta = nextUpdate.Delta;
                model.TradeTime = nextUpdate.TradeTime.FromUnixEpoch();
                model.Exchange = nextUpdate.GetExchange();
                model.Description = nextUpdate.GetDescription();
                model.Underlying = nextUpdate.GetUnderlying();

                updates.Add(model);
            }

            if (updates.Count > 0)
            {
                _orderFactory?.TradeFeedUpdate(id, updates, isLast);
            }
        }

        private void DecodeIbQuoteUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (IbQuoteUpdate == null)
            {
                return;
            }

            IbQuoteUpdateMessage message = new IbQuoteUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            IbQuoteUpdateModel model = new IbQuoteUpdateModel
            {
                TickerId = message.TickerId,
                BidSize = message.BidSize,
                AskSize = message.AskSize,
                LastSize = message.LastSize,
                Volume = message.Volume,
                Bid = message.Bid,
                Ask = message.Ask,
                Last = message.Last,
                High = message.High,
                Low = message.Low,
                Open = message.Open,
                Close = message.Close,
                ImpliedVolatility = message.ImpliedVolatility,
                Delta = message.Delta,
                OptPrice = message.OptPrice,
                PvDividend = message.PvDividend,
                Gamma = message.Gamma,
                Vega = message.Vega,
                Theta = message.Theta,
                UndPrice = message.UndPrice,
                BidExch = message.GetBidExch(),
                AskExch = message.GetAskExch(),
                LastExch = message.GetLastExch(),
                Symbol = message.GetSymbol()
            };

            IbQuoteUpdate?.Invoke(model);
        }

        private void DecodeEdgeToTheoUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (EdgeToTheoUpdate == null)
            {
                return;
            }

            EdgeToTheoUpdateMessage message = new EdgeToTheoUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            EdgeToTheoUpdateModel model = new EdgeToTheoUpdateModel
            {
                BuyEdgeToTheo = message.BuyEdgeToTheo,
                SellEdgeToTheo = message.SellEdgeToTheo,
                Symbol = message.GetSymbol()
            };

            EdgeToTheoUpdate?.Invoke(model);
        }

        private void DecodeSpreadRiskModelUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            SpreadRiskModelUpdateMessage message = new SpreadRiskModelUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int id = message.Id;
            int totalOpen = message.TotalOpen;
            int totalClose = message.TotalClose;
            bool action = message.Action == BooleanEnum.True;
            DateTime lastTradeTime = message.LastTradeTime.FromUnixEpoch();
            string spreadId = message.GetSpreadDescription();
            string underlying = message.GetUnderlying();
            string tags = message.GetTags();

            ISpreadRiskModel? model = _portfolioManager.GetSpreadRiskModel(spreadId);

            if (model == null)
            {
                return;
            }

            model.Id = id;
            model.TotalOpen = totalOpen;
            model.TotalClose = totalClose;
            model.Action = action;
            model.LastTradeTime = lastTradeTime;
            model.SpreadDescription = spreadId;
            model.Underlying = underlying;
            model.Tags = tags;

            _portfolioManager.SpreadRiskUpdate(model);
        }

        private void DecodeSelfTradeWarningMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            ISelfTradeModel? model = _portfolioManager.GetSelfTradeWarningModel();

            if (model == null)
            {
                return;
            }

            SelfTradeWarningMessage message = new SelfTradeWarningMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            model.Qty = message.Qty;
            model.TradeTime = message.TradeTime.FromUnixEpoch();
            model.Symbol = message.GetSymbol();

            _portfolioManager.SelfTradeWarning(model);
        }

        private void DecodeTimeUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            TimeUpdateMessage message = new TimeUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            _updateManager.HandleUpdate((TimeFeedType)message.UpdateType, message.Timestamp.FromUnixEpoch());
        }

        private void DecodeTradeUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            TradeUpdateMessage message = new TradeUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            Side side = message.Side == Generated.Side.Buy ? Side.Buy : Side.Sell;
            int qty = message.Qty;
            double price = message.Price;
            double underBid = message.UnderBid;
            double underAsk = message.UnderAsk;
            string spreadId = message.GetSpreadId();

            TradeUpdateModel tradeUpdateModel = new TradeUpdateModel(spreadId, side, qty, price, underBid, underAsk);

            _updateManager.HandleUpdate(ref tradeUpdateModel);
        }

        private void DecodeSecurityEmaUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }
            SecurityEmaUpdate message = new SecurityEmaUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var messageTickerId = message.TickerId;
            var tickerId = (messageTickerId[0] << 16) | (messageTickerId[1] << 8) | messageTickerId[2];

            var messageSequenceNumber = message.SequenceNumber;
            var sequence = (uint)(messageSequenceNumber[0] << 16) | (uint)(messageSequenceNumber[1] << 8) | messageSequenceNumber[2];

            EmaUpdateModel emaMessage = new EmaUpdateModel(sequence,
                                                           DecodePriceNull3(message.LowPeriodEma),
                                                           DecodePriceNull3(message.LowPeriodEmaAdj),
                                                           DecodePriceNull3(message.LowPeriodEmaUnderlying),
                                                           DecodePriceNull3(message.MidPeriodEma),
                                                           DecodePriceNull3(message.MidPeriodEmaAdj),
                                                           DecodePriceNull3(message.MidPeriodEmaUnderlying),
                                                           DecodePriceNull3(message.HighPeriodEma),
                                                           DecodePriceNull3(message.HighPeriodEmaAdj),
                                                           DecodePriceNull3(message.HighPeriodEmaUnderlying),
                                                           DecodePriceNull3(message.MidPeriodBidEma),
                                                           DecodePriceNull3(message.MidPeriodBidEmaAdj),
                                                           DecodePriceNull3(message.MidPeriodAskEma),
                                                           DecodePriceNull3(message.MidPeriodAskEmaAdj),
                                                           message.QuoteTimestampNanos,
                                                           message.CalculationTimestampNanos,
                                                           message.LowPeriodEmaTimestampNanos,
                                                           message.MidPeriodEmaTimestampNanos,
                                                           message.HighPeriodEmaTimestampNanos);

            _updateManager.HandleUpdate(tickerId,
                                        (SubscriptionFieldType)message.UpdateType,
                                        emaMessage);
        }

        private void DecodeSingleOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SingleOrderRequest == null)
            {
                return;
            }

            SingleOrderRequestMessage message = new SingleOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            SingleOrderRequest request = new SingleOrderRequest()
            {
                OrderType = (Data.Enums.OrderType)message.OrderType,
                Price = message.Price,
                Staged = message.Staged == BooleanEnum.True,
                ClaimRequire = message.ClaimRequire == BooleanEnum.True,
                Quantity = message.Quantity,
                Side = (Side)message.Side,
                Symbol = message.GetSymbol(),
                Account = message.GetAccount(),
                Route = message.GetRoute(),
                Tag = message.GetTag(),
                ClientOrderId = message.GetClientOrderId(),
                Locate = message.GetLocate(),
            };
            SingleOrderRequest.Invoke(request);
        }

        private void DecodePairOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PairOrderRequest == null)
            {
                return;
            }

            PairOrderRequestMessage message = new PairOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            PairOrderRequest request = new PairOrderRequest()
            {
                PairOrderRequestType = (Data.Enums.PairOrderRequestType)message.PairOrderRequestType,
                OrderType = (Data.Enums.OrderType)message.OrderType,
                TriggerValue = message.TriggerValue,
                BuyTermsRatio = message.BuyTermsRatio,
                SellTermsRatio = message.SellTermsRatio,
                InitSide = (InitSide)message.InitSide,
                TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce,
                Staged = message.Staged == BooleanEnum.True,
                ClaimRequire = message.ClaimRequire == BooleanEnum.True,

                Leg1Quantity = message.Leg1Quantity,
                Leg1Side = (Side)message.Leg1Side,

                Leg2Quantity = message.Leg2Quantity,
                Leg2Side = (Side)message.Leg2Side,

                Leg1Symbol = message.GetLeg1Symbol(),
                Leg2Symbol = message.GetLeg2Symbol(),

                Account = message.GetAccount(),
                Route = message.GetRoute(),
                Tag = message.GetTag(),

                ClientOrderId = message.GetClientOrderId(),
                ClientOrderIdLeg1 = message.GetClientOrderIdLeg1(),
                ClientOrderIdLeg2 = message.GetClientOrderIdLeg2(),

                Locate = message.GetLocate(),
                Style = message.GetStyle(),
                TriggerMethod = message.GetTriggerMethod(),
                TriggerValueCurrency = message.GetTriggerValueCurrency(),

                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };
            PairOrderRequest.Invoke(request);
        }

        private void DecodeOrderUpdateValuesMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OrderUpdateValue == null)
            {
                return;
            }

            OrderUpdateValuesMessage message = new OrderUpdateValuesMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            OrderUpdateValues orderUpdate = new OrderUpdateValues()
            {
                ClearOrderIdSet = message.ClearOrderIdSet == BooleanEnum.True,
                IsCancelEnabled = message.IsCancelEnabled == BooleanEnum.True,
                IsModifyEnabled = message.IsModifyEnabled == BooleanEnum.True,
                IsSubmitEnabled = message.IsSubmitEnabled == BooleanEnum.True,
                IsMainOrder = message.IsMainOrder == BooleanEnum.True,
                IsContraOrder = message.IsContraOrder == BooleanEnum.True,
                IsHedgeOrder = message.IsHedgeOrder == BooleanEnum.True,
                IsLooping = message.IsLooping == BooleanEnum.True,
                RequiresManualIntervention = message.RequiresManualIntervention == BooleanEnum.True,
                Filled = message.Filled,
                CumQuantity = message.CumQuantity,
                LastQuantity = message.LastQuantity,
                LeavesQuantity = message.LeavesQuantity,
                StatusMode = (StatusMode)message.StatusMode,
                LastUpdateTime = message.LastUpdateTime.FromUnixEpoch(),
                OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus,
                LastPrice = message.LastPrice,
                AveragePrice = message.AveragePrice,
                AveragePriceAfterFees = message.AveragePriceAfterFees,
                Status = message.GetStatus(),
                OrderId = message.GetOrderId(),
                LocalOrderId = message.GetLocalOrderId(),
                ParentLocalOrderId = message.GetParentLocalOrderId(),
                OriginalOrderId = message.GetOriginalOrderId(),
                Message = message.GetMessage(),
                UnderlyingMidPrice = message.UnderlyingMidPrice,
                Price = message.Price,
                AutomationRunning = message.AutomationRunning == BooleanEnum.True,
                ContraTrader = message.ContraTrader == OrderUpdateValuesMessage.ContraTraderNullValue ? null : (ContraTrader)message.ContraTrader,
            };

            OrderUpdateValue?.Invoke(orderUpdate);
        }

        private void DecodeAccountRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AccountRequest == null)
            {
                return;
            }
            AccountRequestMessage message = new AccountRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            AccountRequest.Invoke(requestId);
        }

        private void DecodeAccountResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AccountResponse == null)
            {
                return;
            }
            AccountResponseMessage message = new AccountResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            var count = message.Count;
            List<Account> accounts = new List<Account>(count);
            AccountResponseMessage.AccountsGroup accountsGroup = message.Accounts;
            while (accountsGroup.HasNext)
            {
                AccountResponseMessage.AccountsGroup nextAccount = accountsGroup.Next();
                Account account = new Account()
                {
                    Id = nextAccount.Id,
                    AccountId = nextAccount.AccountId,
                };
                AccountResponseMessage.AccountsGroup.RoutesGroup routesGroup = nextAccount.Routes;
                while (routesGroup.HasNext)
                {
                    AccountResponseMessage.AccountsGroup.RoutesGroup nextRoute = routesGroup.Next();
                    OrderRoutingInfoModel routingInfoModel = new OrderRoutingInfoModel
                    {
                        Id = nextRoute.Id,
                        AccountId = account.AccountId,
                        VenueId = nextRoute.Venue_Id,
                        OrderRouteId = nextRoute.OrderRoute_Id,
                        OrderTypeId = nextRoute.OrderType_Id,
                        BrokerId = nextRoute.Broker_Id,
                        RouteTypeId = nextRoute.RouteType_Id,
                        RouteId = nextRoute.Route_Id,
                        Active = nextRoute.Active == BooleanEnum.True,
                        Venue = nextRoute.GetVenue(),
                        OrderType = nextRoute.GetOrderType(),
                        Broker = nextRoute.GetBroker(),
                        RouteType = nextRoute.GetRouteType(),
                        Route = nextRoute.GetRoute(),
                        ExpectedName = nextRoute.GetExpectedName(),
                        FixExpectedName = nextRoute.GetFixExpectedName(),
                    };
                    account.Routes.Add(routingInfoModel);
                }

                account.Acronym = nextAccount.GetAcronym();

                foreach (var routingInfo in account.Routes)
                {
                    routingInfo.Acronym = account.Acronym;
                }

                accounts.Add(account);
            }
            AccountResponse.Invoke(requestId, accounts);
        }

        private void DecodeOrderInfoUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OrderInfoUpdate == null)
            {
                return;
            }

            OrderInfoUpdateMessage message = new OrderInfoUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            OrderInfoUpdate orderInfo = new OrderInfoUpdate
            {
                SpreadNumLegs = message.SpreadNumLegs,
                SpreadLegCount = message.SpreadLegCount,
                Minmove = message.Minmove,
                RemainingVolume = message.RemainingVolume,
                OrderResidual = message.OrderResidual,
                VolumeTraded = message.VolumeTraded,
                SpreadLegNumber = message.SpreadLegNumber,
                PairImbalanceLimitType = message.PairImbalanceLimitType,
                UtcOffset = message.UtcOffset,
                OriginalVolume = message.OriginalVolume,
                Volume = message.Volume,
                WorkingQty = message.WorkingQty,

                Side = (Side)message.Side,
                OrderType = (Data.Enums.OrderType)message.OrderType,
                TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce,
                OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus,

                Ask = message.Ask,
                Bid = message.Bid,
                Price = message.Price,
                PairTarget = message.PairTarget,
                PairLeg2Benchmark = message.PairLeg2Benchmark,
                PairLeg1Benchmark = message.PairLeg1Benchmark,
                PairImbalanceLimit = message.PairImbalanceLimit,
                PairCash = message.PairCash,
                PairRatio = message.PairRatio,
                Latency6 = message.Latency6,
                Latency3 = message.Latency3,
                Basisvalue = message.Basisvalue,
                OriginalPrice = message.OriginalPrice,
                StrikePrc = message.StrikePrc,
                StopPrice = message.StopPrice,
                ServerArrivalPrice = message.ServerArrivalPrice,

                TimeStamp = message.TimeStamp.FromUnixEpoch(),
                SubmitTime = message.SubmitTime.FromUnixEpoch(),
                NewsDate = message.NewsDate.FromUnixEpoch(),
                ExpirDate = message.ExpirDate.FromUnixEpoch(),
                NewsTime = message.NewsTime.FromUnixEpoch(),
                TrdTime = message.TrdTime.FromUnixEpoch(),
            };

            OrderInfoUpdateMessage.LegsGroup legsGroup = message.Legs;
            while (legsGroup.HasNext)
            {
                OrderInfoUpdateMessage.LegsGroup nextLegUpdate = legsGroup.Next();
                if (nextLegUpdate != null)
                {
                    OrderInfoUpdate legInfo = new OrderInfoUpdate
                    {
                        SpreadNumLegs = nextLegUpdate.SpreadNumLegs,
                        SpreadLegCount = nextLegUpdate.SpreadLegCount,
                        Minmove = nextLegUpdate.Minmove,
                        RemainingVolume = nextLegUpdate.RemainingVolume,
                        OrderResidual = nextLegUpdate.OrderResidual,
                        VolumeTraded = nextLegUpdate.VolumeTraded,
                        SpreadLegNumber = nextLegUpdate.SpreadLegNumber,
                        PairImbalanceLimitType = nextLegUpdate.PairImbalanceLimitType,
                        UtcOffset = nextLegUpdate.UtcOffset,
                        OriginalVolume = nextLegUpdate.OriginalVolume,
                        Volume = nextLegUpdate.Volume,
                        WorkingQty = nextLegUpdate.WorkingQty,

                        Side = (Side)nextLegUpdate.Side,
                        OrderType = (Data.Enums.OrderType)nextLegUpdate.OrderType,
                        TimeInForce = (Data.Enums.TimeInForce)nextLegUpdate.TimeInForce,
                        OrderStatus = (Data.Enums.OrderStatus)nextLegUpdate.OrderStatus,

                        Ask = nextLegUpdate.Ask,
                        Bid = nextLegUpdate.Bid,
                        Price = nextLegUpdate.Price,
                        PairTarget = nextLegUpdate.PairTarget,
                        PairLeg2Benchmark = nextLegUpdate.PairLeg2Benchmark,
                        PairLeg1Benchmark = nextLegUpdate.PairLeg1Benchmark,
                        PairImbalanceLimit = nextLegUpdate.PairImbalanceLimit,
                        PairCash = nextLegUpdate.PairCash,
                        PairRatio = nextLegUpdate.PairRatio,
                        Latency6 = nextLegUpdate.Latency6,
                        Latency3 = nextLegUpdate.Latency3,
                        Basisvalue = nextLegUpdate.Basisvalue,
                        OriginalPrice = nextLegUpdate.OriginalPrice,
                        StrikePrc = nextLegUpdate.StrikePrc,
                        StopPrice = nextLegUpdate.StopPrice,
                        ServerArrivalPrice = nextLegUpdate.ServerArrivalPrice,

                        TimeStamp = nextLegUpdate.TimeStamp.FromUnixEpoch(),
                        SubmitTime = nextLegUpdate.SubmitTime.FromUnixEpoch(),
                        NewsDate = nextLegUpdate.NewsDate.FromUnixEpoch(),
                        ExpirDate = nextLegUpdate.ExpirDate.FromUnixEpoch(),
                        NewsTime = nextLegUpdate.NewsTime.FromUnixEpoch(),
                        TrdTime = nextLegUpdate.TrdTime.FromUnixEpoch(),

                        BookingType = nextLegUpdate.GetBookingType(),
                        RefMgrNotes = nextLegUpdate.GetRefMgrNotes(),
                        PairSpreadType = nextLegUpdate.GetPairSpreadType(),
                        Reason = nextLegUpdate.GetReason(),
                        LinkedOrderCancellation = nextLegUpdate.GetLinkedOrderCancellation(),
                        LinkedOrderRelationship = nextLegUpdate.GetLinkedOrderRelationship(),
                        CommissionRateType = nextLegUpdate.GetCommissionRateType(),
                        Account = nextLegUpdate.GetAccount(),
                        Route = nextLegUpdate.GetRoute(),
                        OrderId = nextLegUpdate.GetOrderId(),
                        LinkedOrderId = nextLegUpdate.GetLinkedOrderId(),
                        RefersToId = nextLegUpdate.GetRefersToId(),
                        TicketId = nextLegUpdate.GetTicketId(),
                        OriginalOrderId = nextLegUpdate.GetOriginalOrderId(),
                        Symbol = nextLegUpdate.GetSymbol(),
                        Type = nextLegUpdate.GetType(),
                        CurrentStatus = nextLegUpdate.GetCurrentStatus(),
                        TraderId = nextLegUpdate.GetTraderId(),
                        ClaimedByClerk = nextLegUpdate.GetClaimedByClerk(),
                        SpreadLegPriceType = nextLegUpdate.GetSpreadLegPriceType(),
                        SpreadLegLeanPriority = nextLegUpdate.GetSpreadLegLeanPriority(),
                        OrderFlags = nextLegUpdate.GetOrderFlags(),
                        FornexSourceFlags = nextLegUpdate.GetFornexSourceFlags(),
                        ExternalAcceptanceFlag = nextLegUpdate.GetExternalAcceptanceFlag(),
                        ExtendedStateFlags2 = nextLegUpdate.GetExtendedStateFlags2(),
                        ExtendedStateFlags = nextLegUpdate.GetExtendedStateFlags(),
                        CrossFlag = nextLegUpdate.GetCrossFlag(),
                        SpreadClipType = nextLegUpdate.GetSpreadClipType(),
                        PairLeg2BenchmarkType = nextLegUpdate.GetPairLeg2BenchmarkType(),
                        PairLeg1BenchmarkType = nextLegUpdate.GetPairLeg1BenchmarkType(),
                        SharesAllocated = nextLegUpdate.GetSharesAllocated(),
                        OrderFlags2 = nextLegUpdate.GetOrderFlags2(),
                        AcctType = nextLegUpdate.GetAcctType(),
                        Rank = nextLegUpdate.GetRank(),
                        GwBookSeqNo = nextLegUpdate.GetGwBookSeqNo(),
                        DateIndex = nextLegUpdate.GetDateIndex(),
                        BookId = nextLegUpdate.GetBookId(),
                        TboAccountId = nextLegUpdate.GetTboAccountId(),
                        OmsClientType = nextLegUpdate.GetOmsClientType(),
                        ExecutionState = nextLegUpdate.GetExecutionState(),
                        Styp = nextLegUpdate.GetStyp(),
                        CommissionCode = nextLegUpdate.GetCommissionCode(),
                        ShortLocateId = nextLegUpdate.GetShortLocateId(),
                        Undersym = nextLegUpdate.GetUndersym(),
                        Putcallind = nextLegUpdate.GetPutcallind(),
                        UserMessage = nextLegUpdate.GetUserMessage(),
                        OppositeParty = nextLegUpdate.GetOppositeParty(),
                        Currency = nextLegUpdate.GetCurrency(),
                        DispName = nextLegUpdate.GetDispName(),
                        Deposit = nextLegUpdate.GetDeposit(),
                        Customer = nextLegUpdate.GetCustomer(),
                        Branch = nextLegUpdate.GetBranch(),
                        Bank = nextLegUpdate.GetBank(),
                        GoodFrom = nextLegUpdate.GetGoodFrom(),
                        RemoteId = nextLegUpdate.GetRemoteId(),
                        OriginalTraderId = nextLegUpdate.GetOriginalTraderId(),
                        ClientOrderId = nextLegUpdate.GetClientOrderId(),
                        NewRemoteId = nextLegUpdate.GetNewRemoteId(),
                        PriceType = nextLegUpdate.GetPriceType(),
                        VolumeType = nextLegUpdate.GetVolumeType(),
                        GoodUntil = nextLegUpdate.GetGoodUntil(),
                        Buyorsell = nextLegUpdate.GetBuyorsell(),
                        ExitVehicle = nextLegUpdate.GetExitVehicle(),
                        Table = nextLegUpdate.GetTable(),
                        TraderCapacity = nextLegUpdate.GetTraderCapacity(),
                        FixTraderId = nextLegUpdate.GetFixTraderId(),
                        Exchange = nextLegUpdate.GetExchange(),
                        OrderTag = nextLegUpdate.GetOrderTag(),
                        CommissionRate = nextLegUpdate.GetCommissionRate(),
                        Commission = nextLegUpdate.GetCommission(),
                        AvgPrice = nextLegUpdate.GetAvgPrice(),
                        PairSpread = nextLegUpdate.GetPairSpread(),
                        AllocatedValue = nextLegUpdate.GetAllocatedValue(),
                        EcnFee = nextLegUpdate.GetEcnFee(),
                        SpreadClip = nextLegUpdate.GetSpreadClip(),
                        ServerTimeZone = nextLegUpdate.GetServerTimeZone()
                    };
                    orderInfo.ChildOrderInfoUpdates.Add(legInfo);
                }
            }

            orderInfo.BookingType = message.GetBookingType();
            orderInfo.RefMgrNotes = message.GetRefMgrNotes();
            orderInfo.PairSpreadType = message.GetPairSpreadType();
            orderInfo.Reason = message.GetReason();
            orderInfo.LinkedOrderCancellation = message.GetLinkedOrderCancellation();
            orderInfo.LinkedOrderRelationship = message.GetLinkedOrderRelationship();
            orderInfo.CommissionRateType = message.GetCommissionRateType();
            orderInfo.Account = message.GetAccount();
            orderInfo.Route = message.GetRoute();
            orderInfo.OrderId = message.GetOrderId();
            orderInfo.LinkedOrderId = message.GetLinkedOrderId();
            orderInfo.RefersToId = message.GetRefersToId();
            orderInfo.TicketId = message.GetTicketId();
            orderInfo.OriginalOrderId = message.GetOriginalOrderId();
            orderInfo.Symbol = message.GetSymbol();
            orderInfo.Type = message.GetType();
            orderInfo.CurrentStatus = message.GetCurrentStatus();
            orderInfo.TraderId = message.GetTraderId();
            orderInfo.ClaimedByClerk = message.GetClaimedByClerk();
            orderInfo.SpreadLegPriceType = message.GetSpreadLegPriceType();
            orderInfo.SpreadLegLeanPriority = message.GetSpreadLegLeanPriority();
            orderInfo.OrderFlags = message.GetOrderFlags();
            orderInfo.FornexSourceFlags = message.GetFornexSourceFlags();
            orderInfo.ExternalAcceptanceFlag = message.GetExternalAcceptanceFlag();
            orderInfo.ExtendedStateFlags2 = message.GetExtendedStateFlags2();
            orderInfo.ExtendedStateFlags = message.GetExtendedStateFlags();
            orderInfo.CrossFlag = message.GetCrossFlag();
            orderInfo.SpreadClipType = message.GetSpreadClipType();
            orderInfo.PairLeg2BenchmarkType = message.GetPairLeg2BenchmarkType();
            orderInfo.PairLeg1BenchmarkType = message.GetPairLeg1BenchmarkType();
            orderInfo.SharesAllocated = message.GetSharesAllocated();
            orderInfo.OrderFlags2 = message.GetOrderFlags2();
            orderInfo.AcctType = message.GetAcctType();
            orderInfo.Rank = message.GetRank();
            orderInfo.GwBookSeqNo = message.GetGwBookSeqNo();
            orderInfo.DateIndex = message.GetDateIndex();
            orderInfo.BookId = message.GetBookId();
            orderInfo.TboAccountId = message.GetTboAccountId();
            orderInfo.OmsClientType = message.GetOmsClientType();
            orderInfo.ExecutionState = message.GetExecutionState();
            orderInfo.Styp = message.GetStyp();
            orderInfo.CommissionCode = message.GetCommissionCode();
            orderInfo.ShortLocateId = message.GetShortLocateId();
            orderInfo.Undersym = message.GetUndersym();
            orderInfo.Putcallind = message.GetPutcallind();
            orderInfo.UserMessage = message.GetUserMessage();
            orderInfo.OppositeParty = message.GetOppositeParty();
            orderInfo.Currency = message.GetCurrency();
            orderInfo.DispName = message.GetDispName();
            orderInfo.Deposit = message.GetDeposit();
            orderInfo.Customer = message.GetCustomer();
            orderInfo.Branch = message.GetBranch();
            orderInfo.Bank = message.GetBank();
            orderInfo.GoodFrom = message.GetGoodFrom();
            orderInfo.RemoteId = message.GetRemoteId();
            orderInfo.OriginalTraderId = message.GetOriginalTraderId();
            orderInfo.ClientOrderId = message.GetClientOrderId();
            orderInfo.NewRemoteId = message.GetNewRemoteId();
            orderInfo.PriceType = message.GetPriceType();
            orderInfo.VolumeType = message.GetVolumeType();
            orderInfo.GoodUntil = message.GetGoodUntil();
            orderInfo.Buyorsell = message.GetBuyorsell();
            orderInfo.ExitVehicle = message.GetExitVehicle();
            orderInfo.Table = message.GetTable();
            orderInfo.TraderCapacity = message.GetTraderCapacity();
            orderInfo.FixTraderId = message.GetFixTraderId();
            orderInfo.Exchange = message.GetExchange();
            orderInfo.OrderTag = message.GetOrderTag();
            orderInfo.CommissionRate = message.GetCommissionRate();
            orderInfo.Commission = message.GetCommission();
            orderInfo.AvgPrice = message.GetAvgPrice();
            orderInfo.PairSpread = message.GetPairSpread();
            orderInfo.AllocatedValue = message.GetAllocatedValue();
            orderInfo.EcnFee = message.GetEcnFee();
            orderInfo.SpreadClip = message.GetSpreadClip();
            orderInfo.ServerTimeZone = message.GetServerTimeZone();

            OrderInfoUpdate?.Invoke(orderInfo);
        }

        private void DecodeBasketOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (BasketOrderRequest == null)
            {
                return;
            }

            BasketOrderRequestMessage message = new BasketOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            BasketOrderRequest orderInfo = new BasketOrderRequest();
            BasketOrderRequestMessage.BasketOrderRowsGroup legsGroup = message.BasketOrderRows;
            while (legsGroup.HasNext)
            {
                BasketOrderRequestMessage.BasketOrderRowsGroup basketOrderRowsGroup = legsGroup.Next();
                if (basketOrderRowsGroup != null)
                {
                    BasketOrderRow basketOrderRow = new BasketOrderRow
                    {
                        AcctType = basketOrderRowsGroup.AcctType,
                        BookId = basketOrderRowsGroup.BookId,
                        CommissionRateType = basketOrderRowsGroup.CommissionRateType,
                        CrossFlag = basketOrderRowsGroup.CrossFlag,
                        DateIndex = basketOrderRowsGroup.DateIndex,
                        ExecutionState = basketOrderRowsGroup.ExecutionState,
                        ExtendedStateFlags = basketOrderRowsGroup.ExtendedStateFlags,
                        ExtendedStateFlags2 = basketOrderRowsGroup.ExtendedStateFlags2,
                        ExternalAcceptanceFlag = basketOrderRowsGroup.ExternalAcceptanceFlag,
                        FornexSourceFlags = basketOrderRowsGroup.FornexSourceFlags,
                        GwBookSeqNo = basketOrderRowsGroup.GwBookSeqNo,
                        LinkedOrderCancellation = basketOrderRowsGroup.LinkedOrderCancellation,
                        LinkedOrderRelationship = basketOrderRowsGroup.LinkedOrderRelationship,
                        Minmove = basketOrderRowsGroup.Minmove,
                        OmsClientType = basketOrderRowsGroup.OmsClientType,
                        OrderFlags = basketOrderRowsGroup.OrderFlags,
                        OrderFlags2 = basketOrderRowsGroup.OrderFlags2,
                        OrderResidual = basketOrderRowsGroup.OrderResidual,
                        OriginalVolume = basketOrderRowsGroup.OriginalVolume,
                        PairImbalanceLimitType = basketOrderRowsGroup.PairImbalanceLimitType,
                        PairLeg1BenchmarkType = basketOrderRowsGroup.PairLeg1BenchmarkType,
                        PairLeg2BenchmarkType = basketOrderRowsGroup.PairLeg2BenchmarkType,
                        PairSpreadType = basketOrderRowsGroup.PairSpreadType,
                        Rank = basketOrderRowsGroup.Rank,
                        RemainingVolume = basketOrderRowsGroup.RemainingVolume,
                        SharesAllocated = basketOrderRowsGroup.SharesAllocated,
                        SpreadClipType = basketOrderRowsGroup.SpreadClipType,
                        SpreadLegCount = basketOrderRowsGroup.SpreadLegCount,
                        SpreadLegLeanPriority = basketOrderRowsGroup.SpreadLegLeanPriority,
                        SpreadLegNumber = basketOrderRowsGroup.SpreadLegNumber,
                        SpreadLegPriceType = basketOrderRowsGroup.SpreadLegPriceType,
                        SpreadNumLegs = basketOrderRowsGroup.SpreadNumLegs,
                        Styp = basketOrderRowsGroup.Styp,
                        TboAccountId = basketOrderRowsGroup.TboAccountId,
                        UtcOffset = basketOrderRowsGroup.UtcOffset,
                        Volume = basketOrderRowsGroup.Volume,
                        VolumeTraded = basketOrderRowsGroup.VolumeTraded,
                        WorkingQty = basketOrderRowsGroup.WorkingQty,
                        AllocatedValue = basketOrderRowsGroup.AllocatedValue,
                        AvgPrice = basketOrderRowsGroup.AvgPrice,
                        Basisvalue = basketOrderRowsGroup.Basisvalue,
                        Commission = basketOrderRowsGroup.Commission,
                        EcnFee = basketOrderRowsGroup.EcnFee,
                        Latency3 = basketOrderRowsGroup.Latency3,
                        Latency6 = basketOrderRowsGroup.Latency6,
                        PairCash = basketOrderRowsGroup.PairCash,
                        PairImbalanceLimit = basketOrderRowsGroup.PairImbalanceLimit,
                        PairLeg1Benchmark = basketOrderRowsGroup.PairLeg1Benchmark,
                        PairLeg2Benchmark = basketOrderRowsGroup.PairLeg2Benchmark,
                        PairRatio = basketOrderRowsGroup.PairRatio,
                        PairSpread = basketOrderRowsGroup.PairSpread,
                        PairTarget = basketOrderRowsGroup.PairTarget,
                        SpreadClip = basketOrderRowsGroup.SpreadClip,

                        Buyorsell = (Side)basketOrderRowsGroup.Buyorsell,
                        PriceType = (Data.Enums.OrderType)basketOrderRowsGroup.PriceType,
                        NewsDate = basketOrderRowsGroup.NewsDate.FromUnixEpoch(),
                        ExpirDate = basketOrderRowsGroup.ExpirDate.FromUnixEpoch(),

                        NewsTime = TimeSpan.FromMilliseconds(basketOrderRowsGroup.NewsTime),
                        TrdTime = TimeSpan.FromMilliseconds(basketOrderRowsGroup.TrdTime),

                    };

                    BasketOrderRequestMessage.BasketOrderRowsGroup.ExtendedFieldsGroup extendedFieldsGroup = basketOrderRowsGroup.ExtendedFields;
                    while (extendedFieldsGroup.HasNext)
                    {
                        BasketOrderRequestMessage.BasketOrderRowsGroup.ExtendedFieldsGroup extendedField = extendedFieldsGroup.Next();
                        if (extendedField != null)
                        {
                            string key = extendedField.GetKey();
                            string value = extendedField.GetValue();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                basketOrderRow.ExtendedFields[key] = value;
                            }
                        }
                    }

                    basketOrderRow.Ask = basketOrderRowsGroup.GetAsk();
                    basketOrderRow.Bid = basketOrderRowsGroup.GetBid();
                    basketOrderRow.OriginalPrice = basketOrderRowsGroup.GetOriginalPrice();
                    basketOrderRow.Price = basketOrderRowsGroup.GetPrice();
                    basketOrderRow.StopPrice = basketOrderRowsGroup.GetStopPrice();
                    basketOrderRow.StrikePrc = basketOrderRowsGroup.GetStrikePrc();
                    basketOrderRow.Bank = basketOrderRowsGroup.GetBank();
                    basketOrderRow.Branch = basketOrderRowsGroup.GetBranch();
                    basketOrderRow.ClaimedByClerk = basketOrderRowsGroup.GetClaimedByClerk();
                    basketOrderRow.ClientOrderId = basketOrderRowsGroup.GetClientOrderId();
                    basketOrderRow.CommissionCode = basketOrderRowsGroup.GetCommissionCode();
                    basketOrderRow.Currency = basketOrderRowsGroup.GetCurrency();
                    basketOrderRow.CurrentStatus = basketOrderRowsGroup.GetCurrentStatus();
                    basketOrderRow.Customer = basketOrderRowsGroup.GetCustomer();
                    basketOrderRow.Deposit = basketOrderRowsGroup.GetDeposit();
                    basketOrderRow.DispName = basketOrderRowsGroup.GetDispName();
                    basketOrderRow.Exchange = basketOrderRowsGroup.GetExchange();
                    basketOrderRow.ExitVehicle = basketOrderRowsGroup.GetExitVehicle();
                    basketOrderRow.FixTraderId = basketOrderRowsGroup.GetFixTraderId();
                    basketOrderRow.GoodFrom = basketOrderRowsGroup.GetGoodFrom();
                    basketOrderRow.GoodUntil = basketOrderRowsGroup.GetGoodUntil();
                    basketOrderRow.LinkedOrderId = basketOrderRowsGroup.GetLinkedOrderId();
                    basketOrderRow.NewRemoteId = basketOrderRowsGroup.GetNewRemoteId();
                    basketOrderRow.OppositeParty = basketOrderRowsGroup.GetOppositeParty();
                    basketOrderRow.OrderId = basketOrderRowsGroup.GetOrderId();
                    basketOrderRow.OrderTag = basketOrderRowsGroup.GetOrderTag();
                    basketOrderRow.OriginalOrderId = basketOrderRowsGroup.GetOriginalOrderId();
                    basketOrderRow.OriginalTraderId = basketOrderRowsGroup.GetOriginalTraderId();
                    basketOrderRow.Putcallind = basketOrderRowsGroup.GetPutcallind();
                    basketOrderRow.Reason = basketOrderRowsGroup.GetReason();
                    basketOrderRow.RefersToId = basketOrderRowsGroup.GetRefersToId();
                    basketOrderRow.RemoteId = basketOrderRowsGroup.GetRemoteId();
                    basketOrderRow.ShortLocateId = basketOrderRowsGroup.GetShortLocateId();
                    basketOrderRow.Table = basketOrderRowsGroup.GetTable();
                    basketOrderRow.TicketId = basketOrderRowsGroup.GetTicketId();
                    basketOrderRow.TimeStamp = basketOrderRowsGroup.GetTimeStamp();
                    basketOrderRow.TraderCapacity = basketOrderRowsGroup.GetTraderCapacity();
                    basketOrderRow.TraderId = basketOrderRowsGroup.GetTraderId();
                    basketOrderRow.Type = basketOrderRowsGroup.GetType();
                    basketOrderRow.Undersym = basketOrderRowsGroup.GetUndersym();
                    basketOrderRow.UserMessage = basketOrderRowsGroup.GetUserMessage();
                    basketOrderRow.VolumeType = basketOrderRowsGroup.GetVolumeType();
                    basketOrderRow.Route = basketOrderRowsGroup.GetRoute();

                    orderInfo.BasketOrderRows.Add(basketOrderRow);
                }
            }

            orderInfo.Token = message.GetToken();
            orderInfo.ClientOrderId = message.GetClientOrderId();

            BasketOrderRequest?.Invoke(orderInfo);
        }

        private void DecodeResetBaseLineRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ResetBaseLineRequest == null)
            {
                return;
            }
            ResetBaseLineRequestMessage message = new ResetBaseLineRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            List<string> symbols = new List<string>();
            ResetBaseLineRequestMessage.SymbolsGroup symbolsMapGroup = message.Symbols;
            while (symbolsMapGroup.HasNext)
            {
                ResetBaseLineRequestMessage.SymbolsGroup nextSymbol = symbolsMapGroup.Next();
                string symbol = nextSymbol.GetSymbol();
                if (!string.IsNullOrEmpty(symbol))
                {
                    symbols.Add(symbol);
                }
            }
            ResetBaseLineRequest?.Invoke(symbols);
        }

        private void DecodeCancelOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CancelOrderRequest == null)
            {
                return;
            }

            CancelOrderRequestMessage message = new CancelOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CancelRequest cancelRequest = new CancelRequest()
            {
                LocalId = message.GetLocalId(),
                PermId = message.GetPermId(),
                OrderId = message.GetOrderId(),
                Account = message.GetAccount(),
                Venue = message.Venue == CancelOrderRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            CancelOrderRequest?.Invoke(cancelRequest);
        }

        private void DecodeModifyOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ModifyOrderRequest == null)
            {
                return;
            }

            ModifyOrderRequestMessage message = new ModifyOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            ModifyRequest ModifyRequest = new ModifyRequest()
            {
                Price = message.Price,
                Quantity = message.Quantity,
                LocalId = message.GetLocalId(),
                PermId = message.GetPermId(),
                OrderId = message.GetOrderId(),
                Account = message.GetAccount(),
                Venue = message.Venue == ModifyOrderRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            ModifyOrderRequest?.Invoke(ModifyRequest);
        }

        private void DecodeTagOrderMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (TagOrder == null)
            {
                return;
            }

            TagOrderMessage message = new TagOrderMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var permId = message.GetPermID();
            var isTagged = message.IsTagged == BooleanEnum.True;
            var tagger = message.GetTagger();
            var taggedMessage = message.GetTaggedMessage();

            TagOrder?.Invoke(permId, isTagged, tagger, taggedMessage);
        }

        private void DecodeSymbolEdgeMapRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolEdgeMapRequest == null)
            {
                return;
            }

            SymbolEdgeMapRequest message = new SymbolEdgeMapRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var requestId = message.RequestID;
            var start = message.Start.FromUnixEpoch();
            var symbol = message.GetSymbol();

            SymbolEdgeMapRequest?.Invoke(requestId, start, symbol);
        }

        private void DecodeSymbolEdgeMapResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolEdgeMapResponse == null)
            {
                return;
            }

            SymbolEdgeMapResponse message = new SymbolEdgeMapResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var requestId = message.RequestID;
            var start = message.Start.FromUnixEpoch();
            var symbol = "";

            List<SymbolEdgeMap> symbols = new List<SymbolEdgeMap>();
            SymbolEdgeMapResponse.UpdatesGroup symbolEdgeMapUpdates = message.Updates;
            while (symbolEdgeMapUpdates.HasNext)
            {
                SymbolEdgeMapResponse.UpdatesGroup nextSymbolMap = symbolEdgeMapUpdates.Next();
                SymbolEdgeMap symbolEdgeMap = new()
                {
                    Id = nextSymbolMap.Id,
                    Date = nextSymbolMap.Date.FromUnixEpoch(),
                    BestBuyPrice = nextSymbolMap.BestBuyPrice,
                    BestBuyPriceUnderlying = nextSymbolMap.BestBuyPriceUnderlying,
                    BestBuyPriceDelta = nextSymbolMap.BestBuyPriceDelta,
                    BestSellPrice = nextSymbolMap.BestSellPrice,
                    BestSellPriceUnderlying = nextSymbolMap.BestSellPriceUnderlying,
                    BestSellPriceDelta = nextSymbolMap.BestSellPriceDelta,
                    OpeningSide = nextSymbolMap.OpeningSide == Generated.Side.NULL_VALUE ? null : (Side)nextSymbolMap.OpeningSide,
                    HardSide = nextSymbolMap.HardSide == Generated.Side.NULL_VALUE ? null : (Side)nextSymbolMap.HardSide
                };
                symbols.Add(symbolEdgeMap);
            }

            SymbolEdgeMapResponse?.Invoke(requestId, start, symbol, symbols);
        }

        private void DecodeBarRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            BarRequestMessage message = new BarRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string symbol = message.GetSymbol();
            DateTime rangeStart = message.RangeStart.FromUnixEpoch();
            DateTime rangeEnd = message.RangeEnd.FromUnixEpoch();
            BarRequest?.Invoke(requestId, symbol, rangeStart, rangeEnd);
        }

        private void DecodeBarResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            BarResponseMessage message = new BarResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string symbol = message.GetSymbol();
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<BarModel> bars = new(count);
            BarResponseMessage.BarsGroup symbolsMapGroup = message.Bars;
            while (symbolsMapGroup.HasNext)
            {
                BarResponseMessage.BarsGroup nextBar = symbolsMapGroup.Next();

                var timestamp = nextBar.Timestamp.FromUnixEpoch();
                var open = nextBar.Open;
                var high = nextBar.High;
                var low = nextBar.Low;
                var close = nextBar.Close;

                BarModel barModel = new(0, symbol, timestamp, open, high, low, close);
                bars.Add(barModel);
            }
            BarResponse?.Invoke(requestId, symbol, lastGroup, bars);
        }

        private void DecodeAlertMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AlertMessage == null)
            {
                return;
            }
            AlertMessage message = new();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            AlertMessageModel alertModel = new(message.AlertId, message.Time.FromUnixEpoch(), message.GetMessage());
            AlertMessage?.Invoke(alertModel);
        }

        private void DecodeGreekMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            GreekUpdateModel model = new GreekUpdateModel();
            GreekUpdateMessage message = new();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            model.Index = message.Index;
            model.TimeStamp = message.TimeStamp;
            model.CollectorTimestamp = message.CollectorTimestamp;
            model.CollectorTimestampNanos = message.CollectorTimestampNanos;
            model.CalculationTimestampNanos = message.CalculationTimestampNanos;
            model.BidTimestampNanos = message.BidTimestampNanos;
            model.AskTimestampNanos = message.AskTimestampNanos;
            model.SequenceNumber = message.SequenceNumber;
            model.TradeVolume = message.TradeVolume;
            model.BidSize = message.BidSize;
            model.AskSize = message.AskSize;
            model.BidPrice = message.BidPrice;
            model.AskPrice = message.AskPrice;
            model.Theo = message.Theo;
            model.ImpliedVolatility = message.ImpliedVolatility;
            model.Delta = message.Delta;
            model.Gamma = message.Gamma;
            model.Vega = message.Vega;
            model.Theta = message.Theta;
            model.Rho = message.Rho;
            model.BidVol = message.BidVol;
            model.AskVol = message.AskVol;
            model.MidVol = message.MidVol;
            model.BidMCID = message.BidMCID;
            model.AskMCID = message.AskMCID;
            model.UBidPrice = message.UBidPrice;
            model.UAskPrice = message.UAskPrice;
            model.UTimestampNanos = message.UTimestampNanos;
            model.PersistorTimestampNanos = message.PersistorTimestampNanos;
            model.PersistorSeqNum = message.PersistorSeqNum;
            model.InfoBits = message.InfoBits;
            model.TimeValue = message.TimeValue;
            model.IntrinsicValue = message.IntrinsicValue;
            model.FvDivs = message.FvDivs;

            _updateManager?.HandleUpdate(model);
        }

        private void DecodeSymbolStrikeRangeRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolStrikeRangeRequest == null)
            {
                return;
            }
            SymbolStrikeRangeRequestMessage message = new();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SymbolStrikeRangeRequest?.Invoke(message.RequestID, message.Delta, message.Expiration.FromUnixEpoch(), message.GetSymbol());
        }

        private void DecodeSymbolStrikeRangeResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolStrikeRangeResponse == null)
            {
                return;
            }

            SymbolStrikeRangeResponseMessage message = new();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SymbolStrikeRangeResponse?.Invoke(message.RequestID, message.StrikeRange);
        }

        private void DecodePermEdgeToTheoMappingMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PermEdgeToTheoMapping == null)
            {
                return;
            }

            PermEdgeToTheoMappingMessage message = new PermEdgeToTheoMappingMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string symbol = message.GetSymbol();
            int count = message.Count;
            List<EdgeToTheoTrackerModel> models = new(count);
            PermEdgeToTheoMappingMessage.MappingsGroup mappingGroup = message.Mappings;
            while (mappingGroup.HasNext)
            {
                PermEdgeToTheoMappingMessage.MappingsGroup? nextMapping = mappingGroup.Next();
                EdgeToTheoTrackerModel edgeToTheoTrackerModel = new EdgeToTheoTrackerModel
                {
                    StrikeStart = nextMapping.StrikeStart,
                    StrikeEnd = nextMapping.StrikeEnd,
                    BuyAttemptEdgeToTheo = nextMapping.BuyAttemptEdgeToTheo,
                    SellAttemptEdgeToTheo = nextMapping.SellAttemptEdgeToTheo,
                    BuyFillEdgeToTheo = nextMapping.BuyFillEdgeToTheo,
                    SellFillEdgeToTheo = nextMapping.SellFillEdgeToTheo,
                };
                models.Add(edgeToTheoTrackerModel);
            }
            PermEdgeToTheoMapping?.Invoke(symbol, models);
        }

        private void DecodeRegisterEdgeScanFeedServerRunnerJson(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (EdgeScanFeedServerRunnerRequest == null)
            {
                return;
            }

            RegisterEdgeScanFeedServerRunnerJson message = new RegisterEdgeScanFeedServerRunnerJson();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string json = message.GetJson();
            _logger.LogDebug("Decoding {}, Json: {}", nameof(RegisterEdgeScanFeedServerRunnerJson), json);
            var request = JsonConvert.DeserializeObject<EdgeScanFeedServerRunner>(json, _edgeScanFeedSerializationSettings);
            if (request != null)
            {
                EdgeScanFeedServerRunnerRequest?.Invoke(request);
            }
        }

        private void DecodeUnregisterEdgeScanFeedServerRunner(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (EdgeScanFeedServerRunnerUnregister == null)
            {
                return;
            }

            UnregisterEdgeScanFeedServerRunner message = new UnregisterEdgeScanFeedServerRunner();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string id = message.GetId();
            _logger.LogDebug("Decoding {}, Id: {}", nameof(UnregisterEdgeScanFeedServerRunner), id);
            EdgeScanFeedServerRunnerUnregister?.Invoke(id);
        }

        private void DecodeSpreadGeneratorRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SpreadGeneratorRequest == null)
            {
                return;
            }

            SpreadGeneratorRequestMessage message = new SpreadGeneratorRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var id = message.RequestId;
            string json = message.GetJson();
            _logger.LogInformation("Decoding {}, Id: {}, Json: {}", nameof(SpreadGeneratorRequest), id, json);
            var request = JsonConvert.DeserializeObject<SpreadsGeneratorConfig>(json, _spreadGeneratorConfigSerializationSettings);
            if (request != null)
            {
                SpreadGeneratorRequest?.Invoke(id, request);
            }
            else
            {
                _logger.LogWarning("Deserialization Failed {}, Id: {}, Json: {}", nameof(SpreadGeneratorRequest), id, json);
            }
        }

        private void DecodeSpreadGeneratorResultsMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SpreadGeneratorResults == null)
            {
                return;
            }

            SpreadGeneratorResultsMessage message = new SpreadGeneratorResultsMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<string> symbols = new List<string>(count);
            SpreadGeneratorResultsMessage.SpreadsGroup? spreadsGroup = message.Spreads;
            while (spreadsGroup.HasNext)
            {
                var nextSpread = spreadsGroup.Next();
                var legsGroup = nextSpread.Legs;
                var spreadSymbol = "";
                while (legsGroup.HasNext)
                {
                    SpreadGeneratorResultsMessage.SpreadsGroup.LegsGroup? leg = legsGroup.Next();
                    string? root = leg.GetRoot();
                    int expiration = leg.Expiration;
                    PutCall callPut = leg.PutCall;
                    double strike = leg.Strike;
                    string symbol = '.' + root + expiration + (callPut == PutCall.Call ? 'C' : 'P') + strike;
                    Generated.Side side = leg.Side;
                    int ratio = leg.Ratio;

                    if (spreadSymbol != "" || side != Generated.Side.Buy)
                    {
                        spreadSymbol += side == Generated.Side.Sell
                            ? "-"
                            : "+";
                    }

                    spreadSymbol += ratio > 1 ?
                        ratio + "*" :
                        "";

                    spreadSymbol += symbol;
                }

                symbols.Add(spreadSymbol);
            }

            SpreadGeneratorResults?.Invoke(requestId, lastGroup, symbols);
        }

        private void DecodeSymbolsRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolsRequest == null)
            {
                return;
            }

            SymbolsRequest message = new SymbolsRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var id = message.RequestId;
            string symbol = message.GetSymbol();
            string secType = message.GetSecType();
            string exchange = message.GetExchange();
            string currency = message.GetCurrency();
            SymbolsRequest?.Invoke(id, symbol, secType, exchange, currency);
        }

        private void DecodeSymbolsResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolsResponse == null)
            {
                return;
            }

            SymbolsResponse message = new SymbolsResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<string> symbols = new List<string>(count);
            SymbolsResponse.SymbolsGroup? spreadsGroup = message.Symbols;
            while (spreadsGroup.HasNext)
            {
                var symbolGroup = spreadsGroup.Next();

                string? root = symbolGroup.GetRoot();
                int expiration = symbolGroup.Expiration;
                PutCall callPut = symbolGroup.PutCall;
                double strike = symbolGroup.Strike;
                string symbol = '.' + root + expiration + (callPut == PutCall.Call ? 'C' : 'P') + strike;

                symbols.Add(symbol);
            }

            SymbolsResponse?.Invoke(requestId, symbols, lastGroup);
        }

        private void DecodeOptionChainRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OptionChainRequest == null)
            {
                return;
            }

            Generated.OptionChainRequest message = new Generated.OptionChainRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            string underlying = message.GetUnderlyingSymbol();
            int? expiryFrom = message.ExpiryFrom == Generated.OptionChainRequest.ExpiryFromNullValue ? null : message.ExpiryFrom;
            int? expiryTo = message.ExpiryTo == Generated.OptionChainRequest.ExpiryToNullValue ? null : message.ExpiryTo;
            double? strikeMin = double.IsNaN(message.StrikeMin) ? null : message.StrikeMin;
            double? strikeMax = double.IsNaN(message.StrikeMax) ? null : message.StrikeMax;
            Data.Enums.PutCall? putCallFilter = message.PutCallFilter == PutCall.NULL_VALUE ? null : (Data.Enums.PutCall)message.PutCallFilter;

            OptionChainRequest?.Invoke(requestId, underlying, expiryFrom, expiryTo, strikeMin, strikeMax, putCallFilter);
        }

        private void DecodeOptionChainResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OptionChainResponse == null)
            {
                return;
            }

            Generated.OptionChainResponse message = new Generated.OptionChainResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<Option> options = new List<Option>(count);
            Generated.OptionChainResponse.OptionsGroup optionsGroup = message.Options;
            while (optionsGroup.HasNext)
            {
                var row = optionsGroup.Next();

                string root = row.GetRoot();
                int expirationYyMMdd = row.Expiration;
                Data.Enums.PutCall putCall = (Data.Enums.PutCall)row.PutCall;
                double strike = row.Strike;
                double minimumTick = row.MinimumTick;
                double multiplier = row.Multiplier;
                Data.Enums.MinimumTickStyle tickStyle = (Data.Enums.MinimumTickStyle)row.MinimumTickStyle;
                string primaryExchange = row.GetPrimaryExchange();

                DateTime expiration = DateTime.TryParseExact(
                    expirationYyMMdd.ToString("D6"),
                    "yyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsedExp)
                    ? parsedExp
                    : DateTime.MinValue;
                double roundedStrike = Math.Round(strike, 2);
                string strikeStr = Math.Abs(roundedStrike - Math.Round(roundedStrike)) < 1e-9
                    ? roundedStrike.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                    : roundedStrike.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string symbol = '.' + root + expirationYyMMdd.ToString("D6") + (putCall == Data.Enums.PutCall.Call ? 'C' : 'P') + strikeStr;

                options.Add(new Option
                {
                    Symbol = symbol,
                    RootSymbol = root,
                    Expiration = expiration,
                    PutCall = putCall,
                    Strike = strike,
                    MinimumTick = minimumTick,
                    Multiplier = multiplier,
                    MinimumTickStyle = tickStyle,
                    PrimaryExchange = primaryExchange,
                    SecurityType = Data.Enums.SecurityType.Option,
                });
            }

            OptionChainResponse?.Invoke(requestId, options, lastGroup);
        }

        private void DecodeEdgeScanFeedStatisticsMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_statsProcessor == null)
            {
                return;
            }

            EdgeScanFeedStatisticsMessage message = new EdgeScanFeedStatisticsMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var instanceId = message.GetInstanceId();

            IEdgeScanFeedStatisticsSummary? model = _statsProcessor.GetEdgeScanFeedStatisticsModel(instanceId);

            if (model == null)
            {
                return;
            }

            model.InstanceId = instanceId;
            model.TotalSubs = message.TotalSubs;
            model.TotalAttempts = message.TotalAttempts;

            model.StartTime = message.StartTime.FromUnixEpoch();

            model.State = message.GetState();
            model.User = message.GetUser();
            model.ScannerConfig = message.GetScannerConfig();
            model.BasketConfig = message.GetBasketConfig();

            _statsProcessor?.EdgeScanFeedStatsUpdate(model);
        }

        private void DecodeEdgeScanFeedStatisticsUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_statsProcessor == null)
            {
                return;
            }

            EdgeScanFeedStatisticsUpdateMessage message = new EdgeScanFeedStatisticsUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var instanceId = message.GetInstanceId();

            IEdgeScanFeedStatisticsSummary? model = _statsProcessor.GetEdgeScanFeedStatisticsModel(instanceId);

            if (model == null)
            {
                return;
            }

            model.InstanceId = instanceId;
            model.TotalSubs = message.TotalSubs;
            model.TotalAttempts = message.TotalAttempts;
            model.Submissions = message.Submissions;
            model.Received = message.Received;
            model.Timestamp = message.Timestamp.FromUnixEpoch();
            model.State = message.GetState();

            _statsProcessor?.EdgeScanFeedStatsUpdate(model);
        }

        private void DecodeChartSeriesUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_statsProcessor == null)
            {
                return;
            }

            ChartSeriesUpdateMessage message = new ChartSeriesUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            string id = message.GetId();
            SubscriptionFieldType type = (SubscriptionFieldType)message.Type;
            ChartSeriesUpdateMessage.UpdatesGroup? updates = message.Updates;
            List<ChartValueModel> updatesList = new List<ChartValueModel>(updates.Count);
            while (updates.HasNext)
            {
                ChartSeriesUpdateMessage.UpdatesGroup? group = updates.Next();
                ChartValueModel model = new ChartValueModel(group.Timestamp.FromUnixEpoch(), group.Value);
                updatesList.Add(model);
            }

            _statsProcessor.HandleUpdate(id, type, updatesList);
        }

        private void DecodeFirmOrderAndTradeSummary(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (FirmOrderAndTradeSummaryReceived == null)
            {
                return;
            }

            FirmOrderAndTradeSummaryMessage message = new FirmOrderAndTradeSummaryMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var firmOrderAndTradeSummary = new FirmOrderAndTradeSummary
            {
                Index = message.Index,
                BuySummary = new OrderAndTradeSummary
                {
                    LastAttemptPx = message.BuyLastAttemptPx,
                    LastAttemptUnderPx = message.BuyLastAttemptUnderPx,
                    LastAttemptTime = message.BuyLastAttemptTime.FromUnixEpoch(),
                    LastFillPx = message.BuyLastFillPx,
                    LastFillUnderPx = message.BuyLastFillUnderPx,
                    LastFillTime = message.BuyLastFillTime.FromUnixEpoch(),
                    LowestAttemptedEdgeToTheo = message.BuyLowestAttemptedEdgeToTheo,
                    HighestFilledEdgeToTheo = message.BuyHighestFilledEdgeToTheo
                },
                SellSummary = new OrderAndTradeSummary
                {
                    LastAttemptPx = message.SellLastAttemptPx,
                    LastAttemptUnderPx = message.SellLastAttemptUnderPx,
                    LastAttemptTime = message.SellLastAttemptTime.FromUnixEpoch(),
                    LastFillPx = message.SellLastFillPx,
                    LastFillUnderPx = message.SellLastFillUnderPx,
                    LastFillTime = message.SellLastFillTime.FromUnixEpoch(),
                    LowestAttemptedEdgeToTheo = message.SellLowestAttemptedEdgeToTheo,
                    HighestFilledEdgeToTheo = message.SellHighestFilledEdgeToTheo
                },
                Id = message.GetId()
            };

            FirmOrderAndTradeSummaryReceived?.Invoke(firmOrderAndTradeSummary);
        }

        private void DecodeDataRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (DataRequestMessage == null)
            {
                return;
            }

            DataRequestMessage message = new DataRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            DataRequestMessage?.Invoke(message.RequestId, (SubscriptionFieldType)message.DataType);
        }

        private void DecodeHistoricHighestBidLowestAskRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (HistoricHighestBidLowestAskRequest == null)
            {
                return;
            }

            HistoricHighestBidLowestAskRequestMessage message = new HistoricHighestBidLowestAskRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            HistoricHighestBidLowestAskRequest?.Invoke(message.RequestId, message.TickerId, message.GetSymbol());
        }

        private void DecodeHistoricHighestBidLowestAskResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (HistoricHighestBidLowestAskResponse == null)
            {
                return;
            }

            HistoricHighestBidLowestAskResponseMessage message = new HistoricHighestBidLowestAskResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var id = message.RequestId;
            var tickerId = message.TickerId;
            var count = message.Count;

            var updates = message.Updates;

            var updatesList = new List<HighestBidLowestAskTrackerModel>(count);

            while (updates.HasNext)
            {
                var group = updates.Next();
                var model = new HighestBidLowestAskTrackerModel
                {
                    StartTime = group.StartTime.FromUnixEpoch(),
                    EndTime = group.EndTime.FromUnixEpoch(),
                    HighestBid = group.HighestBid,
                    HighestBidUnderlyingMid = group.HighestBidUnderlyingMid,
                    HighestBidTime = group.HighestBidTime,
                    LowestAsk = group.LowestAsk,
                    LowestAskUnderlyingMid = group.LowestAskUnderlyingMid,
                    LowestAskTime = group.LowestAskTime,
                    Delta = group.Delta,
                };
                updatesList.Add(model);
            }

            var symbol = message.GetSymbol();

            HistoricHighestBidLowestAskResponse?.Invoke(id, tickerId, symbol, updatesList);
        }

        private void DecodePositionsRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PositionsRequest == null)
            {
                return;
            }

            PositionsRequestMessage message = new PositionsRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var id = message.RequestId;
            var portfolioType = (PortfolioType)message.PortfolioType;
            var positionType = (PositionType)message.PositionType;
            string name = message.GetPortfolioName();

            PositionsRequest?.Invoke(id, name, portfolioType, positionType);
        }

        private void DecodeTheoToMarketSpreadUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (TheoToMarketSpreadUpdate == null)
            {
                return;
            }

            TheoToMarketSpreadUpdate message = new TheoToMarketSpreadUpdate();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var messageTickerId = message.TickerId;
            var tickerId = (messageTickerId[0] << 16) | (messageTickerId[1] << 8) | messageTickerId[2];

            var theoToMarketSpread = new TheoToMarketSpread
            {
                TickerId = tickerId,
                LastBidTheoSpread = message.LastBidTheoSpread / 10_000d,
                LastAskTheoSpread = message.LastAskTheoSpread / 10_000d,
                BidTheoSpreadEma = message.BidTheoSpreadEma / 10_000d,
                AskTheoSpreadEma = message.AskTheoSpreadEma / 10_000d,
            };

            TheoToMarketSpreadUpdate?.Invoke(theoToMarketSpread);
        }

        private void DecodeMultiplePositionsAddedMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null)
            {
                return;
            }

            MultiplePositionsAddedMessage message = new MultiplePositionsAddedMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var requestId = message.RequestId;
            int id = message.PortfolioId;
            PortfolioType portfolioType = (PortfolioType)message.PortfolioType;
            IPortfolio portfolio = _portfolioManager.GetPortfolio(id, portfolioType);
            portfolio.Id = id;
            portfolio.PortfolioType = portfolioType;
            portfolio.PortfolioDate = message.PortfolioDate.FromUnixEpoch();
            portfolio.TotalSubmissions = message.TotalSubmissions;
            portfolio.TotalSingleLegSubmissions = message.TotalSingleLegSubmissions;
            portfolio.TotalSpreadSubmissions = message.TotalSpreadSubmissions;
            portfolio.TotalSingleFills = message.TotalSingleFills;
            portfolio.TotalSpreadFills = message.TotalSpreadFills;
            portfolio.UniqueSubmissions = message.UniqueSubmissions;
            portfolio.UniqueSpreadSubmissions = message.UniqueSpreadSubmissions;
            portfolio.TotalFills = message.TotalFills;
            portfolio.UniqueFills = message.UniqueFills;
            portfolio.UniqueSpreadFills = message.UniqueSpreadFills;
            portfolio.StockContracts = message.StockContracts;
            portfolio.TotalContracts = message.TotalContracts;
            portfolio.UniqueContracts = message.UniqueContracts;
            portfolio.UniqueSpreadContracts = message.UniqueSpreadContracts;
            portfolio.NetQty = message.NetQty;
            portfolio.ShortQty = message.ShortQty;
            portfolio.LongQty = message.LongQty;
            portfolio.FillRate = DecodeDoubleNull4(message.FillRate);
            portfolio.OrderFillRate = DecodeDoubleNull4(message.OrderFillRate);
            portfolio.IbOrderFillRate = DecodeDoubleNull4(message.IbOrderFillRate);
            portfolio.LowestRealizedPnl = message.LowestRealizedPnl;
            portfolio.HighestRealizedPnl = message.HighestRealizedPnl;
            portfolio.RealizedPnl = message.RealizedPnl;
            portfolio.LowestAdjustedPnl = message.LowestAdjustedPnl;
            portfolio.HighestAdjustedPnl = message.HighestAdjustedPnl;
            portfolio.AdjustedPnl = message.AdjustedPnl;
            portfolio.SingleLegAdjustedPnl = double.IsNaN(message.SingleLegAdjustedPnl) ? 0.0 : message.SingleLegAdjustedPnl;
            portfolio.SpreadAdjustedPnl = double.IsNaN(message.SpreadAdjustedPnl) ? 0.0 : message.SpreadAdjustedPnl;
            portfolio.UnrealizedPnl = message.UnrealizedPnl;
            portfolio.NetDelta = message.NetDelta;

            portfolio.MaxResubmitEstimate = message.MaxResubmitEstimate;
            portfolio.MaxResubmitForFill = message.MaxResubmitForFill;
            portfolio.AvgResubmitEstimate = message.AvgResubmitEstimate;
            portfolio.AvgResubmitForFill = message.AvgResubmitForFill;

            portfolio.DeltaAdjustedBurn = message.DeltaAdjustedBurn;
            portfolio.DeltaAdjustedHelp = message.DeltaAdjustedHelp;
            portfolio.HighestOpenNotional = message.HighestOpenNotional;
            portfolio.TotalOpenNotional = message.TotalOpenNotional;

            portfolio.TotalOutOfMarketOrders = message.TotalOutOfMarketOrders;
            portfolio.TotalOutOfMarketFills = message.TotalOutOfMarketFills;

            portfolio.SubmissionRatePerSec = message.SubmissionRatePerSec;
            portfolio.MaxOrdersPerSec = message.MaxOrdersPerSec;

            portfolio.WinnerTrades = message.WinnerTrades;
            portfolio.LoserTrades = message.LoserTrades;
            portfolio.SizeWinnerTrades = message.SizeWinnerTrades;
            portfolio.SizeLoserTrades = message.SizeLoserTrades;
            portfolio.AvgCloseSubs = message.AvgCloseSubs;

            portfolio.IntroducingBrokerFee = message.IntroducingBrokerFee;
            portfolio.ExecutingBrokerFee = message.ExecutingBrokerFee;
            portfolio.ExchangeFee = message.ExchangeFee;
            portfolio.OrfFee = message.OrfFee;
            portfolio.SecFee = message.SecFee;
            portfolio.TotalFees = message.TotalFees;
            portfolio.AvgOpenSubsCount = message.AvgOpenSubsCount;
            portfolio.AvgSubsBetweenFillsCount = message.AvgSubsBetweenFillsCount;

            portfolio.GroupSubmissionsAvg = message.GroupSubmissionsAvg;
            portfolio.FillRate = DecodeDoubleNull4(message.GroupAvgFillRate);

            List<IPosition> positionsList = new List<IPosition>();
            MultiplePositionsAddedMessage.PositionsGroup positions = message.Positions;
            while (positions.HasNext)
            {
                MultiplePositionsAddedMessage.PositionsGroup positionMessage = positions.Next();

                int positionId = positionMessage.PositionId;
                int parentPositionId = positionMessage.ParentPositionId;
                PositionType type = (PositionType)positionMessage.PositionType;
                IPosition position;
                if (parentPositionId <= 0)
                {
                    position = portfolio.GetPosition(positionId, type);
                }
                else
                {
                    IPosition parentPosition = portfolio.GetPosition(parentPositionId, PositionType.Underlying);
                    position = parentPosition.GetPosition(positionId, PositionType.Expiration);
                }
                position.ParentPositionId = parentPositionId;
                position.Id = positionId;
                position.PositionType = type;
                position.NetQty = positionMessage.NetQty;
                position.RealizedPnl = positionMessage.RealizedPnl;
                position.AdjustedPnl = positionMessage.AdjustedPnl;
                position.UnrealizedPnl = positionMessage.UnrealizedPnl;
                position.NetDelta = positionMessage.NetDelta;
                position.BestSellPrice = positionMessage.BestSellPrice;
                position.BestSellPriceUnderMid = positionMessage.BestSellPriceUnderMid;
                position.BestBuyPrice = positionMessage.BestBuyPrice;
                position.BestBuyPriceUnderMid = positionMessage.BestBuyPriceUnderMid;
                position.TotalSubmissions = positionMessage.TotalSubmissions;
                position.TotalSingleLegSubmissions = positionMessage.TotalSingleLegSubmissions;
                position.TotalSpreadSubmissions = positionMessage.TotalSpreadSubmissions;
                position.TotalSingleFills = positionMessage.TotalSingleFills;
                position.TotalSpreadFills = positionMessage.TotalSpreadFills;
                position.UniqueSubmissions = positionMessage.UniqueSubmissions;
                position.TotalFills = positionMessage.TotalFills;
                position.UniqueFills = positionMessage.UniqueFills;
                position.TotalContracts = positionMessage.TotalContracts;
                position.UniqueContracts = positionMessage.UniqueContracts;
                position.FillRate = DecodeDoubleNull4(positionMessage.FillRate);
                position.OrderFillRate = DecodeDoubleNull4(positionMessage.OrderFillRate);
                position.IbOrderFillRate = DecodeDoubleNull4(positionMessage.IbOrderFillRate);
                position.OpenPositionAveragePrice = positionMessage.OpenPositionAveragePrice;
                position.OpenPositionFillUnderPrice = positionMessage.OpenPositionFillUnderPrice;
                position.LastTradeTime = positionMessage.LastTradeTime.FromUnixEpoch();
                position.PositionDate = positionMessage.PositionDate.FromUnixEpoch();
                position.LastEdge = positionMessage.LastEdge;
                position.LastBuyEdge = positionMessage.LastBuyEdge;
                position.LastSellEdge = positionMessage.LastSellEdge;
                position.LastBuyEdgeToTheo = positionMessage.LastBuyEdgeToTheo;
                position.LastSellEdgeToTheo = positionMessage.LastSellEdgeToTheo;
                position.LastBuyFillEdgeToTheo = positionMessage.LastBuyFillEdgeToTheo;
                position.LastSellFillEdgeToTheo = positionMessage.LastSellFillEdgeToTheo;
                position.LastBuyAttemptEdgeToTheo = positionMessage.LastBuyAttemptEdgeToTheo;
                position.LastSellAttemptEdgeToTheo = positionMessage.LastSellAttemptEdgeToTheo;
                position.LastPermBuyFillEdgeToTheo = positionMessage.LastPermBuyFillEdgeToTheo;
                position.LastPermSellFillEdgeToTheo = positionMessage.LastPermSellFillEdgeToTheo;
                position.LastPermBuyAttemptEdgeToTheo = positionMessage.LastPermBuyAttemptEdgeToTheo;
                position.LastPermSellAttemptEdgeToTheo = positionMessage.LastPermSellAttemptEdgeToTheo;
                position.BestBuyEdgeToTheo = positionMessage.BestBuyEdgeToTheo;
                position.WorstBuyEdgeToTheo = positionMessage.WorstBuyEdgeToTheo;
                position.BestSellEdgeToTheo = positionMessage.BestSellEdgeToTheo;
                position.WorstSellEdgeToTheo = positionMessage.WorstSellEdgeToTheo;
                position.OpenNotional = positionMessage.OpenNotional;

                position.MaxResubmitEstimate = positionMessage.MaxResubmitEstimate;
                position.MaxResubmitForFill = positionMessage.MaxResubmitForFill;
                position.AvgResubmitEstimate = positionMessage.AvgResubmitEstimate;
                position.AvgResubmitForFill = positionMessage.AvgResubmitForFill;

                position.FirstEdge = positionMessage.FirstEdge;

                position.TotalOutOfMarketOrders = positionMessage.TotalOutOfMarketOrders;
                position.TotalOutOfMarketFills = positionMessage.TotalOutOfMarketFills;

                position.HardSide = positionMessage.HardSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.HardSide;
                position.HardSideDesignationTime = positionMessage.HardSideDesignationTime.FromUnixEpoch();
                position.HardSideBuyGiveUp = positionMessage.HardSideBuyGiveUp;
                position.HardSideSellGiveUp = positionMessage.HardSideSellGiveUp;

                position.SubmissionRatePerSec = positionMessage.SubmissionRatePerSec;
                position.MaxOrdersPerSec = positionMessage.MaxOrdersPerSec;

                position.WinnerTrades = positionMessage.WinnerTrades;
                position.LoserTrades = positionMessage.LoserTrades;
                position.SizeWinnerTrades = positionMessage.SizeWinnerTrades;
                position.SizeLoserTrades = positionMessage.SizeLoserTrades;
                position.AvgCloseSubs = positionMessage.AvgCloseSubs;
                position.OpenSubsCount = positionMessage.OpenSubsCount;
                position.SubsBetweenFillsCount = positionMessage.SubsBetweenFillsCount;

                position.IntroducingBrokerFee = positionMessage.IntroducingBrokerFee;
                position.ExecutingBrokerFee = positionMessage.ExecutingBrokerFee;
                position.ExchangeFee = positionMessage.ExchangeFee;
                position.OrfFee = positionMessage.OrfFee;
                position.SecFee = positionMessage.SecFee;
                position.TotalFees = positionMessage.TotalFees;
                position.LastTradeSide = positionMessage.LastTradeSide == Generated.Side.NULL_VALUE ? null : (Side)positionMessage.LastTradeSide;

                position.LastBuyAttempt = positionMessage.LastBuyAttempt;
                position.LastBuyAttemptUnderlying = positionMessage.LastBuyAttemptUnderlying;
                position.LastSellAttempt = positionMessage.LastSellAttempt;
                position.LastSellAttemptUnderlying = positionMessage.LastSellAttemptUnderlying;

                position.RawNetQty = positionMessage.RawNetQty;

                string lastInstance = positionMessage.GetLastInstance();
                if (!string.IsNullOrWhiteSpace(lastInstance))
                {
                    position.LastInstance = lastInstance;
                }

                string lastTrader = positionMessage.GetLastTrader();
                if (!string.IsNullOrWhiteSpace(lastTrader))
                {
                    position.LastTrader = lastTrader;
                }

                string account = positionMessage.GetAccount();
                if (!string.IsNullOrWhiteSpace(account))
                {
                    position.Account = account;
                }
                position.Name = positionMessage.GetPositionName();
                position.Symbol = positionMessage.GetPositionSymbol();

                position.SingleLegAdjustedPnl = double.IsNaN(positionMessage.SingleLegAdjustedPnl) ? 0.0 : positionMessage.SingleLegAdjustedPnl;
                position.SpreadAdjustedPnl = double.IsNaN(positionMessage.SpreadAdjustedPnl) ? 0.0 : positionMessage.SpreadAdjustedPnl;

                portfolio.AddPosition(position);
                positionsList.Add(position);
            }

            _portfolioManager.MultiplePositionsAdded(requestId, portfolio, positionsList);
        }

        private void DecodePriceChainModelMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
            {
                return;
            }

            PriceChainModelMessage message = new PriceChainModelMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            IPriceChainModel? model = _orderFactory.GetPriceChainModel();

            if (model == null)
            {
                return;
            }

            model.IsFirm = message.IsFirm == BooleanEnum.True;
            model.PossibleFirm = message.PossibleFirm == BooleanEnum.True;
            model.PossibleCopyCat = message.PossibleCopyCat == BooleanEnum.True;
            model.Uncertain = message.Uncertain == BooleanEnum.True;

            model.QtyMismatch = message.QtyMismatch == BooleanEnum.True;

            model.EdgeScannerType = (EdgeScannerType)message.ScannerId;

            model.BuyConditionCode = (char)message.BuyConditionCode;
            model.SellConditionCode = (char)message.SellConditionCode;

            model.AdjSide = (Side)message.AdjSide;
            model.IbCobSide = (Side)message.IbCobSide;

            model.LegsCount = message.LegsCount;

            model.BuyQty = message.BuyQty;
            model.SellQty = message.SellQty;

            model.BuyBidSize = message.BuyBidSize;
            model.BuyAskSize = message.BuyAskSize;
            model.SellBidSize = message.SellBidSize;
            model.SellAskSize = message.SellAskSize;

            model.FlipCount = message.FlipCount;

            model.IbCobBid = DecodePriceNull3(message.IbCobBid);
            model.IbCobAsk = DecodePriceNull3(message.IbCobAsk);
            model.AdjustedPnl = DecodePriceNull3(message.AdjustedPnl);
            model.BuyPrice = DecodePriceNull3(message.BuyPrice);
            model.BuyTradeOriginalPrice = DecodePriceNull3(message.BuyTradeOriginalPrice);
            model.SellPrice = DecodePriceNull3(message.SellPrice);
            model.SellTradeOriginalPrice = DecodePriceNull3(message.SellTradeOriginalPrice);
            model.BuyEdgeToTheo = DecodePriceNull3(message.BuyEdgeToTheo);
            model.SellEdgeToTheo = DecodePriceNull3(message.SellEdgeToTheo);
            model.Ttl = DecodePriceNull3(message.Ttl);
            model.SpreadWidth = DecodePriceNull3(message.SpreadWidth);
            model.BuyTradeBid = DecodePriceNull3(message.BuyTradeBid);
            model.BuyTradeMid = DecodePriceNull3(message.BuyTradeMid);
            model.BuyTradeAsk = DecodePriceNull3(message.BuyTradeAsk);
            model.BuyTradeTheo = DecodePriceNull3(message.BuyTradeTheo);
            model.BuyTradeDelta = DecodePriceNull3(message.BuyTradeDelta);
            model.SellTradeBid = DecodePriceNull3(message.SellTradeBid);
            model.SellTradeMid = DecodePriceNull3(message.SellTradeMid);
            model.SellTradeAsk = DecodePriceNull3(message.SellTradeAsk);
            model.SellTradeTheo = DecodePriceNull3(message.SellTradeTheo);
            model.SellTradeDelta = DecodePriceNull3(message.SellTradeDelta);
            model.BuyTradeUnderlyingMid = DecodePriceNull3(message.BuyTradeUnderlyingMid);
            model.SellTradeUnderlyingMid = DecodePriceNull3(message.SellTradeUnderlyingMid);
            model.BuyUnderlyingWidth = DecodePriceNull3(message.BuyUnderlyingWidth);
            model.SellUnderlyingWidth = DecodePriceNull3(message.SellUnderlyingWidth);
            model.DeltaAdjEdge = DecodePriceNull3(message.DeltaAdjEdge);
            model.HighestLegDelta = DecodePriceNull3(message.HighestLegDelta);
            model.SpreadWeightedVega = DecodePriceNull3(message.SpreadWeightedVega);
            model.ReceiveLatency = DecodePriceNull3(message.ReceiveLatency);
            model.IvPctChange = DecodePriceNull3(message.IvPctChange);

            model.BuyTime = message.BuyTime.FromUnixEpoch();
            model.SellTime = message.SellTime.FromUnixEpoch();

            model.NearExpiration = message.NearExpiration.FromUnixEpoch();
            model.FarExpiration = message.FarExpiration.FromUnixEpoch();

            model.PriceChainTradePrice = message.PriceChainTradePrice;
            model.PriceChainTotalBidDeviations = message.PriceChainTotalBidDeviations;
            model.PriceChainTotalAskDeviations = message.PriceChainTotalAskDeviations;
            model.PriceChainDeviationSequence = message.PriceChainDeviationSequence;
            model.PriceChainRecentBidDeviation = message.PriceChainRecentBidDeviation;
            model.PriceChainRecentBidDeviationTimeDiff = message.PriceChainRecentBidDeviationTimeDiff;
            model.PriceChainRecentBidDeviationUnderBid = message.PriceChainRecentBidDeviationUnderBid;
            model.PriceChainRecentBidDeviationUnderAsk = message.PriceChainRecentBidDeviationUnderAsk;
            model.PriceChainRecentBidDeviationBid = message.PriceChainRecentBidDeviationBid;
            model.PriceChainRecentBidDeviationAsk = message.PriceChainRecentBidDeviationAsk;
            model.PriceChainRecentAskDeviation = message.PriceChainRecentAskDeviation;
            model.PriceChainRecentAskDeviationTimeDiff = message.PriceChainRecentAskDeviationTimeDiff;
            model.PriceChainRecentAskDeviationUnderBid = message.PriceChainRecentAskDeviationUnderBid;
            model.PriceChainRecentAskDeviationUnderAsk = message.PriceChainRecentAskDeviationUnderAsk;
            model.PriceChainRecentAskDeviationBid = message.PriceChainRecentAskDeviationBid;
            model.PriceChainRecentAskDeviationAsk = message.PriceChainRecentAskDeviationAsk;
            model.PriceChainHighestBidDeviation = message.PriceChainHighestBidDeviation;
            model.PriceChainHighestBidDeviationTimeDiff = message.PriceChainHighestBidDeviationTimeDiff;
            model.PriceChainHighestBidDeviationUnderBid = message.PriceChainHighestBidDeviationUnderBid;
            model.PriceChainHighestBidDeviationUnderAsk = message.PriceChainHighestBidDeviationUnderAsk;
            model.PriceChainHighestBidDeviationBid = message.PriceChainHighestBidDeviationBid;
            model.PriceChainHighestBidDeviationAsk = message.PriceChainHighestBidDeviationAsk;
            model.PriceChainHighestAskDeviation = message.PriceChainHighestAskDeviation;
            model.PriceChainHighestAskDeviationTimeDiff = message.PriceChainHighestAskDeviationTimeDiff;
            model.PriceChainHighestAskDeviationUnderBid = message.PriceChainHighestAskDeviationUnderBid;
            model.PriceChainHighestAskDeviationUnderAsk = message.PriceChainHighestAskDeviationUnderAsk;
            model.PriceChainHighestAskDeviationBid = message.PriceChainHighestAskDeviationBid;
            model.PriceChainHighestAskDeviationAsk = message.PriceChainHighestAskDeviationAsk;

            model.PriceChainRecentBidDeviationIvOffset = message.PriceChainRecentBidDeviationIvOffset;
            model.PriceChainHighestBidDeviationIvOffset = message.PriceChainHighestBidDeviationIvOffset;
            model.PriceChainRecentAskDeviationIvOffset = message.PriceChainRecentAskDeviationIvOffset;
            model.PriceChainHighestAskDeviationIvOffset = message.PriceChainHighestAskDeviationIvOffset;

            model.UnderSymbol = message.GetUnderSymbol();
            model.Description = message.GetDescription();
            model.SpreadId = message.GetSpreadId();
            model.SpreadType = message.GetSpreadType();
            model.BuySymbol = message.GetBuySymbol();
            model.SellSymbol = message.GetSellSymbol();
            model.ExtraTag = message.GetExtraTag();
            model.Exchange = message.GetExchange();
            model.SessionId = message.GetSessionId();

            _orderFactory?.EdgeScanUpdate(model);
        }

        private void DecodeMatrixSyntheticSpreadMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MatrixSyntheticSpread == null)
            {
                return;
            }

            MatrixSyntheticSpreadMessage message = new MatrixSyntheticSpreadMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var model = new SyntheticSpread
            {
                InstrumentType = message.InstrumentType != MatrixSyntheticSpreadMessage.InstrumentTypeNullValue
                    ? (InstrumentType)message.InstrumentType
                    : null,
                OpenClose = message.OpenClose != MatrixSyntheticSpreadMessage.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                Tif = (Tif)message.Tif,
                TifTake = (Tif)message.TifTake,
                PegMethod = message.PegMethod != MatrixSyntheticSpreadMessage.PegMethodNullValue
                    ? (PegMethod)message.PegMethod
                    : null,
                PegDirection = message.PegDirection != MatrixSyntheticSpreadMessage.PegDirectionNullValue
                    ? (PegDirection)message.PegDirection
                    : null,
                Price = DecodeDoubleNull2(message.Price),
                PegOffset = DecodeDoubleNull2Nullable(message.PegOffset),
                Discretion = DecodeDoubleNull2Nullable(message.Discretion),
                OrderQuantity = message.OrderQuantity,
                DisplayQty = message.DisplayQty != MatrixSyntheticSpreadMessage.DisplayQtyNullValue
                    ? message.DisplayQty
                    : null,
                RemoveOnOut = message.RemoveOnOut != BooleanEnum.NULL_VALUE
                    ? message.RemoveOnOut == BooleanEnum.True
                    : null,
                ExtTradingHours = message.ExtTradingHours != BooleanEnum.NULL_VALUE
                    ? message.ExtTradingHours == BooleanEnum.True
                    : null,
                CancelDelay = message.CancelDelay,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            var strategyModel = model.StrategyData;

            strategyModel.TakeHidden = message.StrDataTakeHidden != BooleanEnum.NULL_VALUE
                ? message.StrDataTakeHidden == BooleanEnum.True
                : null;
            strategyModel.AtsMode = message.StrDataAtsMode != BooleanEnum.NULL_VALUE
                ? message.StrDataAtsMode == BooleanEnum.True
                : null;
            strategyModel.CancelOnHalt = message.StrDataCancelOnHalt != BooleanEnum.NULL_VALUE
                ? message.StrDataCancelOnHalt == BooleanEnum.True
                : null;
            strategyModel.SpreadPriceDiscretion = message.StrDataSpreadPriceDiscretion != BooleanEnum.NULL_VALUE
                ? message.StrDataSpreadPriceDiscretion == BooleanEnum.True
                : null;
            strategyModel.SeparateEquityLeg = message.StrDataSeparateEquityLeg != BooleanEnum.NULL_VALUE
                ? message.StrDataSeparateEquityLeg == BooleanEnum.True
                : null;
            strategyModel.ExtTradingHours = message.StrDataExtTradingHours != BooleanEnum.NULL_VALUE
                ? message.StrDataExtTradingHours == BooleanEnum.True
                : null;
            strategyModel.LeggingOnly = message.StrDataLeggingOnly != BooleanEnum.NULL_VALUE
                ? message.StrDataLeggingOnly == BooleanEnum.True
                : null;
            strategyModel.SynthFeeOptimal = message.StrDataSynthFeeOptimal != BooleanEnum.NULL_VALUE
                ? message.StrDataSynthFeeOptimal == BooleanEnum.True
                : null;
            strategyModel.SynthComplexTakeOnly = message.StrDataSynthComplexTakeOnly != BooleanEnum.NULL_VALUE
                ? message.StrDataSynthComplexTakeOnly == BooleanEnum.True
                : null;
            strategyModel.Hedge = message.StrDataHedge != BooleanEnum.NULL_VALUE
                ? message.StrDataHedge == BooleanEnum.True
                : null;

            strategyModel.MakeTake = message.StrDataMakeTake != MatrixSyntheticSpreadMessage.StrDataMakeTakeNullValue
                ? (MakeTake)message.StrDataMakeTake
                : null;
            strategyModel.Algorithm = message.StrDataAlgorithm != MatrixSyntheticSpreadMessage.StrDataAlgorithmNullValue
                ? (Algorithm)message.StrDataAlgorithm
                : null;
            strategyModel.PriceMethod = message.StrDataPriceMethod != MatrixSyntheticSpreadMessage.StrDataPriceMethodNullValue
                ? (PriceMethod)message.StrDataPriceMethod
                : null;
            strategyModel.SynthPassiveMode = message.StrDataSynthPassiveMode != MatrixSyntheticSpreadMessage.StrDataSynthPassiveModeNullValue
                ? (Algorithm)message.StrDataSynthPassiveMode
                : null;
            strategyModel.LegExecType = message.StrDataLegExecType != MatrixSyntheticSpreadMessage.StrDataLegExecTypeNullValue
                ? (ExecType)message.StrDataLegExecType
                : null;
            strategyModel.LegTif = message.StrDataLegTif != MatrixSyntheticSpreadMessage.StrDataLegTifNullValue
                ? (Tif)message.StrDataLegTif
                : null;

            strategyModel.ReminderQty = message.StrDataReminderQty != MatrixSyntheticSpreadMessage.StrDataReminderQtyNullValue
                ? message.StrDataReminderQty
                : null;
            strategyModel.MinWorkingQty = message.StrDataMinWorkingQty != MatrixSyntheticSpreadMessage.StrDataMinWorkingQtyNullValue
                ? message.StrDataMinWorkingQty
                : null;
            strategyModel.MinQuoteQty = message.StrDataMinQuoteQty != MatrixSyntheticSpreadMessage.StrDataMinQuoteQtyNullValue
                ? message.StrDataMinQuoteQty
                : null;
            strategyModel.NumOfTries = message.StrDataNumOfTries != MatrixSyntheticSpreadMessage.StrDataNumOfTriesNullValue
                ? message.StrDataNumOfTries
                : null;
            strategyModel.BadRatioTryThreshold = message.StrDataBadRatioTryThreshold != MatrixSyntheticSpreadMessage.StrDataBadRatioTryThresholdNullValue
                ? message.StrDataBadRatioTryThreshold
                : null;
            strategyModel.WorkingQty = message.StrDataWorkingQty != MatrixSyntheticSpreadMessage.StrDataWorkingQtyNullValue
                ? message.StrDataWorkingQty
                : null;
            strategyModel.SynthPassiveCancelDelayMs = message.StrDataSynthPassiveCancelDelayMs != MatrixSyntheticSpreadMessage.StrDataSynthPassiveCancelDelayMsNullValue
                ? message.StrDataSynthPassiveCancelDelayMs
                : null;
            strategyModel.LegTimeout = message.StrDataLegTimeout != MatrixSyntheticSpreadMessage.StrDataLegTimeoutNullValue
                ? message.StrDataLegTimeout
                : null;
            strategyModel.DisplayQty = message.StrDataDisplayQty != MatrixSyntheticSpreadMessage.StrDataDisplayQtyNullValue
                ? message.StrDataDisplayQty
                : null;
            strategyModel.QtyIncluded = message.StrDataQtyIncluded != MatrixSyntheticSpreadMessage.StrDataQtyIncludedNullValue
                ? message.StrDataQtyIncluded
                : null;

            strategyModel.BadRatioTimeout = DecodeDoubleNull2Nullable(message.StrDataBadRatioTimeout);
            strategyModel.DiscretionTake = DecodeDoubleNull2Nullable(message.StrDataDiscretionTake);
            strategyModel.UndPrice = DecodeDoubleNull2Nullable(message.StrDataUndPrice);
            strategyModel.MaxPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMaxPriceUnd);
            strategyModel.MinPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMinPriceUnd);
            strategyModel.PriceRange = DecodeDoubleNull2Nullable(message.StrDataPriceRange);
            strategyModel.BadRatioPriceDiscretion = DecodeDoubleNull2Nullable(message.StrDataBadRatioPriceDiscretion);

            MatrixSyntheticSpreadMessage.StrDataExchangesGroup? exchanges = message.StrDataExchanges;
            while (exchanges.HasNext)
            {
                MatrixSyntheticSpreadMessage.StrDataExchangesGroup? exch = exchanges.Next();
                var exchange = exch.GetExchange();
                strategyModel.Exchanges?.Add(exchange);
            }

            MatrixSyntheticSpreadMessage.StrDataExchangesTakeGroup? exchangesTake = message.StrDataExchangesTake;
            while (exchangesTake.HasNext)
            {
                MatrixSyntheticSpreadMessage.StrDataExchangesTakeGroup? exch = exchangesTake.Next();
                var exchange = exch.GetExchange();
                strategyModel.ExchangesTake?.Add(exchange);
            }

            MatrixSyntheticSpreadMessage.LegsGroup legs = message.Legs;
            while (legs.HasNext)
            {
                var nextLeg = legs.Next();

                var legModel = new Data.Matrix.SpreadLeg
                {
                    InstrumentType = (InstrumentType)nextLeg.InstrumentType,
                    Side = (Data.Enums.Matrix.Side)nextLeg.Side,
                    LegRatio = nextLeg.LegRatio,
                    OpenClose = message.OpenClose != MatrixSyntheticSpreadMessage.LegsGroup.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                    Symbol = nextLeg.ClientGuidLength() == 0 ? null : nextLeg.GetSymbol(),
                    ClientGuid = nextLeg.ClientGuidLength() == 0 ? null : nextLeg.GetClientGuid(),
                };
                model.Legs.Add(legModel);
            }

            model.ClientGuid = message.ClientGuidLength() == 0 ? null : message.GetClientGuid();
            model.Account = message.AccountLength() == 0 ? null : message.GetAccount();
            model.Exchange = message.ExchangeLength() == 0 ? null : message.GetExchange();
            model.Memo = message.MemoLength() == 0 ? null : message.GetMemo();
            model.Source = message.SourceLength() == 0 ? null : message.GetSource();
            model.Destination = message.DestinationLength() == 0 ? null : message.GetDestination();

            MatrixSyntheticSpread?.Invoke(model);
        }

        private void DecodeMatrixScrapeMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MatrixScrape == null)
            {
                return;
            }

            MatrixScrapeMessage message = new MatrixScrapeMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var model = new Scrape()
            {
                InstrumentType = message.InstrumentType != MatrixScrapeMessage.InstrumentTypeNullValue
                    ? (InstrumentType)message.InstrumentType
                    : null,
                OpenClose = message.OpenClose != MatrixScrapeMessage.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                Side = message.Side != MatrixScrapeMessage.SideNullValue
                    ? (Data.Enums.Matrix.Side)message.Side
                    : null,
                Tif = (Tif)message.Tif,
                TifTake = (Tif)message.TifTake,
                PegMethod = message.PegMethod != MatrixScrapeMessage.PegMethodNullValue
                    ? (PegMethod)message.PegMethod
                    : null,
                PegDirection = message.PegDirection != MatrixScrapeMessage.PegDirectionNullValue
                    ? (PegDirection)message.PegDirection
                    : null,
                Price = DecodeDoubleNull2(message.Price),
                PegOffset = DecodeDoubleNull2Nullable(message.PegOffset),
                Discretion = DecodeDoubleNull2Nullable(message.Discretion),
                OrderQuantity = message.OrderQuantity,
                DisplayQty = message.DisplayQty != MatrixScrapeMessage.DisplayQtyNullValue
                    ? message.DisplayQty
                    : null,
                RemoveOnOut = message.RemoveOnOut != BooleanEnum.NULL_VALUE
                    ? message.RemoveOnOut == BooleanEnum.True
                    : null,
                ExtTradingHours = message.ExtTradingHours != BooleanEnum.NULL_VALUE
                    ? message.ExtTradingHours == BooleanEnum.True
                    : null,
                CancelDelay = message.CancelDelay,
                MinimumTickStyle = (Data.Enums.MinimumTickStyle)message.MinimumTickStyle,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            var strategyModel = model.StrategyData;

            strategyModel.TakeHidden = message.StrDataTakeHidden != BooleanEnum.NULL_VALUE
                ? message.StrDataTakeHidden == BooleanEnum.True
                : null;
            strategyModel.AtsMode = message.StrDataAtsMode != BooleanEnum.NULL_VALUE
                ? message.StrDataAtsMode == BooleanEnum.True
                : null;
            strategyModel.CancelOnHalt = message.StrDataCancelOnHalt != BooleanEnum.NULL_VALUE
                ? message.StrDataCancelOnHalt == BooleanEnum.True
                : null;

            strategyModel.MakeTake = message.StrDataMakeTake != MatrixScrapeMessage.StrDataMakeTakeNullValue
                ? (MakeTake)message.StrDataMakeTake
                : null;
            strategyModel.Algorithm = message.StrDataAlgorithm != MatrixScrapeMessage.StrDataAlgorithmNullValue
                ? (Algorithm)message.StrDataAlgorithm
                : null;
            strategyModel.PriceMethod = message.StrDataPriceMethod != MatrixScrapeMessage.StrDataPriceMethodNullValue
                ? (PriceMethod)message.StrDataPriceMethod
                : null;

            strategyModel.ReminderQty = message.StrDataReminderQty != MatrixScrapeMessage.StrDataReminderQtyNullValue
                ? message.StrDataReminderQty
                : null;
            strategyModel.MinWorkingQty = message.StrDataMinWorkingQty != MatrixScrapeMessage.StrDataMinWorkingQtyNullValue
                ? message.StrDataMinWorkingQty
                : null;
            strategyModel.MinQuoteQty = message.StrDataMinQuoteQty != MatrixScrapeMessage.StrDataMinQuoteQtyNullValue
                ? message.StrDataMinQuoteQty
                : null;
            strategyModel.WorkingQty = message.StrDataWorkingQty != MatrixScrapeMessage.StrDataWorkingQtyNullValue
                ? message.StrDataWorkingQty
                : null;

            strategyModel.DiscretionTake = DecodeDoubleNull2Nullable(message.StrDataDiscretionTake);
            strategyModel.UndPrice = DecodeDoubleNull2Nullable(message.StrDataUndPrice);
            strategyModel.MaxPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMaxPriceUnd);
            strategyModel.MinPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMinPriceUnd);
            strategyModel.PriceRange = DecodeDoubleNull2Nullable(message.StrDataPriceRange);

            strategyModel.LimitToMarketTime = message.StrDataLimitToMarketTime != MatrixScrapeMessage.StrDataLimitToMarketTimeNullValue
                ? message.StrDataLimitToMarketTime.FromUnixEpoch()
                : null;

            MatrixScrapeMessage.StrDataExchangesGroup? exchanges = message.StrDataExchanges;
            while (exchanges.HasNext)
            {
                MatrixScrapeMessage.StrDataExchangesGroup? exch = exchanges.Next();
                var exchange = exch.GetExchange();
                strategyModel.Exchanges?.Add(exchange);
            }

            MatrixScrapeMessage.StrDataExchangesTakeGroup? exchangesTake = message.StrDataExchangesTake;
            while (exchangesTake.HasNext)
            {
                MatrixScrapeMessage.StrDataExchangesTakeGroup? exch = exchangesTake.Next();
                var exchange = exch.GetExchange();
                strategyModel.ExchangesTake?.Add(exchange);
            }

            model.ClientGuid = message.ClientGuidLength() == 0 ? null : message.GetClientGuid();
            model.Account = message.AccountLength() == 0 ? null : message.GetAccount();
            model.Symbol = message.SymbolLength() == 0 ? null : message.GetSymbol();
            model.Exchange = message.ExchangeLength() == 0 ? null : message.GetExchange();
            model.Memo = message.MemoLength() == 0 ? null : message.GetMemo();
            model.Source = message.SourceLength() == 0 ? null : message.GetSource();
            model.Destination = message.DestinationLength() == 0 ? null : message.GetDestination();

            MatrixScrape?.Invoke(model);
        }

        private void DecodeMatrixSeekerMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MatrixSeeker == null)
            {
                return;
            }

            MatrixSeekerMessage message = new MatrixSeekerMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var model = new Seeker()
            {
                InstrumentType = message.InstrumentType != MatrixSeekerMessage.InstrumentTypeNullValue
                    ? (InstrumentType)message.InstrumentType
                    : null,
                OpenClose = message.OpenClose != MatrixSeekerMessage.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                Side = message.Side != MatrixSeekerMessage.SideNullValue
                    ? (Data.Enums.Matrix.Side)message.Side
                    : null,
                Tif = (Tif)message.Tif,
                TifTake = (Tif)message.TifTake,
                PegMethod = message.PegMethod != MatrixSeekerMessage.PegMethodNullValue
                    ? (PegMethod)message.PegMethod
                    : null,
                PegDirection = message.PegDirection != MatrixSeekerMessage.PegDirectionNullValue
                    ? (PegDirection)message.PegDirection
                    : null,
                Price = DecodeDoubleNull2(message.Price),
                PegOffset = DecodeDoubleNull2Nullable(message.PegOffset),
                Discretion = DecodeDoubleNull2Nullable(message.Discretion),
                OrderQuantity = message.OrderQuantity,
                DisplayQty = message.DisplayQty != MatrixSeekerMessage.DisplayQtyNullValue
                    ? message.DisplayQty
                    : null,
                RemoveOnOut = message.RemoveOnOut != BooleanEnum.NULL_VALUE
                    ? message.RemoveOnOut == BooleanEnum.True
                    : null,
                ExtTradingHours = message.ExtTradingHours != BooleanEnum.NULL_VALUE
                    ? message.ExtTradingHours == BooleanEnum.True
                    : null,
                CancelDelay = message.CancelDelay,
                MinimumTickStyle = (Data.Enums.MinimumTickStyle)message.MinimumTickStyle,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            var strategyModel = model.StrategyData;

            strategyModel.TakeHidden = message.StrDataTakeHidden != BooleanEnum.NULL_VALUE
                ? message.StrDataTakeHidden == BooleanEnum.True
                : null;
            strategyModel.AtsMode = message.StrDataAtsMode != BooleanEnum.NULL_VALUE
                ? message.StrDataAtsMode == BooleanEnum.True
                : null;
            strategyModel.CancelOnHalt = message.StrDataCancelOnHalt != BooleanEnum.NULL_VALUE
                ? message.StrDataCancelOnHalt == BooleanEnum.True
                : null;

            strategyModel.MakeTake = message.StrDataMakeTake != MatrixSeekerMessage.StrDataMakeTakeNullValue
                ? (MakeTake)message.StrDataMakeTake
                : null;
            strategyModel.Algorithm = message.StrDataAlgorithm != MatrixSeekerMessage.StrDataAlgorithmNullValue
                ? (Algorithm)message.StrDataAlgorithm
                : null;
            strategyModel.PriceMethod = message.StrDataPriceMethod != MatrixSeekerMessage.StrDataPriceMethodNullValue
                ? (PriceMethod)message.StrDataPriceMethod
                : null;

            strategyModel.ReminderQty = message.StrDataReminderQty != MatrixSeekerMessage.StrDataReminderQtyNullValue
                ? message.StrDataReminderQty
                : null;
            strategyModel.MinWorkingQty = message.StrDataMinWorkingQty != MatrixSeekerMessage.StrDataMinWorkingQtyNullValue
                ? message.StrDataMinWorkingQty
                : null;
            strategyModel.MinQuoteQty = message.StrDataMinQuoteQty != MatrixSeekerMessage.StrDataMinQuoteQtyNullValue
                ? message.StrDataMinQuoteQty
                : null;
            strategyModel.WorkingQty = message.StrDataWorkingQty != MatrixSeekerMessage.StrDataWorkingQtyNullValue
                ? message.StrDataWorkingQty
                : null;

            strategyModel.DiscretionTake = DecodeDoubleNull2Nullable(message.StrDataDiscretionTake);
            strategyModel.UndPrice = DecodeDoubleNull2Nullable(message.StrDataUndPrice);
            strategyModel.MaxPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMaxPriceUnd);
            strategyModel.MinPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMinPriceUnd);
            strategyModel.PriceRange = DecodeDoubleNull2Nullable(message.StrDataPriceRange);

            MatrixSeekerMessage.StrDataExchangesGroup? exchanges = message.StrDataExchanges;
            while (exchanges.HasNext)
            {
                MatrixSeekerMessage.StrDataExchangesGroup? exch = exchanges.Next();
                var exchange = exch.GetExchange();
                strategyModel.Exchanges?.Add(exchange);
            }

            MatrixSeekerMessage.StrDataExchangesTakeGroup? exchangesTake = message.StrDataExchangesTake;
            while (exchangesTake.HasNext)
            {
                MatrixSeekerMessage.StrDataExchangesTakeGroup? exch = exchangesTake.Next();
                var exchange = exch.GetExchange();
                strategyModel.ExchangesTake?.Add(exchange);
            }

            model.ClientGuid = message.ClientGuidLength() == 0 ? null : message.GetClientGuid();
            model.Account = message.AccountLength() == 0 ? null : message.GetAccount();
            model.Symbol = message.SymbolLength() == 0 ? null : message.GetSymbol();
            model.Exchange = message.ExchangeLength() == 0 ? null : message.GetExchange();
            model.Memo = message.MemoLength() == 0 ? null : message.GetMemo();
            model.Source = message.SourceLength() == 0 ? null : message.GetSource();
            model.Destination = message.DestinationLength() == 0 ? null : message.GetDestination();

            MatrixSeeker?.Invoke(model);
        }

        private void DecodeMatrixSeekerSpreadMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MatrixSeekerSpread == null)
            {
                return;
            }

            SeekerSpreadMessage message = new SeekerSpreadMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var model = new SeekerSpread()
            {
                InstrumentType = message.InstrumentType != SeekerSpreadMessage.InstrumentTypeNullValue
                    ? (InstrumentType)message.InstrumentType
                    : null,
                OpenClose = message.OpenClose != SeekerSpreadMessage.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                Tif = (Tif)message.Tif,
                TifTake = (Tif)message.TifTake,
                PegMethod = message.PegMethod != SeekerSpreadMessage.PegMethodNullValue
                    ? (PegMethod)message.PegMethod
                    : null,
                PegDirection = message.PegDirection != SeekerSpreadMessage.PegDirectionNullValue
                    ? (PegDirection)message.PegDirection
                    : null,
                Price = DecodeDoubleNull2(message.Price),
                PegOffset = DecodeDoubleNull2Nullable(message.PegOffset),
                Discretion = DecodeDoubleNull2Nullable(message.Discretion),
                OrderQuantity = message.OrderQuantity,
                DisplayQty = message.DisplayQty != SeekerSpreadMessage.DisplayQtyNullValue
                    ? message.DisplayQty
                    : null,
                RemoveOnOut = message.RemoveOnOut != BooleanEnum.NULL_VALUE
                    ? message.RemoveOnOut == BooleanEnum.True
                    : null,
                ExtTradingHours = message.ExtTradingHours != BooleanEnum.NULL_VALUE
                    ? message.ExtTradingHours == BooleanEnum.True
                    : null,
                CancelDelay = message.CancelDelay,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            var strategyModel = model.StrategyData;

            strategyModel.TakeHidden = message.StrDataTakeHidden != BooleanEnum.NULL_VALUE
                ? message.StrDataTakeHidden == BooleanEnum.True
                : null;
            strategyModel.AtsMode = message.StrDataAtsMode != BooleanEnum.NULL_VALUE
                ? message.StrDataAtsMode == BooleanEnum.True
                : null;
            strategyModel.CancelOnHalt = message.StrDataCancelOnHalt != BooleanEnum.NULL_VALUE
                ? message.StrDataCancelOnHalt == BooleanEnum.True
                : null;

            strategyModel.MakeTake = message.StrDataMakeTake != SeekerSpreadMessage.StrDataMakeTakeNullValue
                ? (MakeTake)message.StrDataMakeTake
                : null;
            strategyModel.Algorithm = message.StrDataAlgorithm != SeekerSpreadMessage.StrDataAlgorithmNullValue
                ? (Algorithm)message.StrDataAlgorithm
                : null;
            strategyModel.PriceMethod = message.StrDataPriceMethod != SeekerSpreadMessage.StrDataPriceMethodNullValue
                ? (PriceMethod)message.StrDataPriceMethod
                : null;

            strategyModel.ReminderQty = message.StrDataReminderQty != SeekerSpreadMessage.StrDataReminderQtyNullValue
                ? message.StrDataReminderQty
                : null;
            strategyModel.MinWorkingQty = message.StrDataMinWorkingQty != SeekerSpreadMessage.StrDataMinWorkingQtyNullValue
                ? message.StrDataMinWorkingQty
                : null;
            strategyModel.MinQuoteQty = message.StrDataMinQuoteQty != SeekerSpreadMessage.StrDataMinQuoteQtyNullValue
                ? message.StrDataMinQuoteQty
                : null;
            strategyModel.WorkingQty = message.StrDataWorkingQty != SeekerSpreadMessage.StrDataWorkingQtyNullValue
                ? message.StrDataWorkingQty
                : null;

            strategyModel.DiscretionTake = DecodeDoubleNull2Nullable(message.StrDataDiscretionTake);
            strategyModel.UndPrice = DecodeDoubleNull2Nullable(message.StrDataUndPrice);
            strategyModel.MaxPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMaxPriceUnd);
            strategyModel.MinPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMinPriceUnd);
            strategyModel.PriceRange = DecodeDoubleNull2Nullable(message.StrDataPriceRange);

            SeekerSpreadMessage.StrDataExchangesGroup? exchanges = message.StrDataExchanges;
            while (exchanges.HasNext)
            {
                SeekerSpreadMessage.StrDataExchangesGroup? exch = exchanges.Next();
                var exchange = exch.GetExchange();
                strategyModel.Exchanges?.Add(exchange);
            }

            SeekerSpreadMessage.StrDataExchangesTakeGroup? exchangesTake = message.StrDataExchangesTake;
            while (exchangesTake.HasNext)
            {
                SeekerSpreadMessage.StrDataExchangesTakeGroup? exch = exchangesTake.Next();
                var exchange = exch.GetExchange();
                strategyModel.ExchangesTake?.Add(exchange);
            }

            SeekerSpreadMessage.LegsGroup legs = message.Legs;
            while (legs.HasNext)
            {
                var nextLeg = legs.Next();

                var legModel = new Data.Matrix.SpreadLeg
                {
                    InstrumentType = (InstrumentType)nextLeg.InstrumentType,
                    Side = (Data.Enums.Matrix.Side)nextLeg.Side,
                    LegRatio = nextLeg.LegRatio,
                    OpenClose = message.OpenClose != SeekerSpreadMessage.LegsGroup.OpenCloseNullValue
                    ? (OpenClose)message.OpenClose
                    : null,
                    Symbol = nextLeg.ClientGuidLength() == 0 ? null : nextLeg.GetSymbol(),
                    ClientGuid = nextLeg.ClientGuidLength() == 0 ? null : nextLeg.GetClientGuid(),
                };
                model.Legs.Add(legModel);
            }

            model.ClientGuid = message.ClientGuidLength() == 0 ? null : message.GetClientGuid();
            model.Account = message.AccountLength() == 0 ? null : message.GetAccount();
            model.Exchange = message.ExchangeLength() == 0 ? null : message.GetExchange();
            model.Memo = message.MemoLength() == 0 ? null : message.GetMemo();
            model.Source = message.SourceLength() == 0 ? null : message.GetSource();
            model.Destination = message.DestinationLength() == 0 ? null : message.GetDestination();

            MatrixSeekerSpread?.Invoke(model);
        }

        private void DecodeExecutionTransactionMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ExecutionTransaction == null)
            {
                return;
            }

            ExecutionTransactionMessage message = new ExecutionTransactionMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var model = new Transaction(null,
                message.UpdateTime.FromUnixEpoch(),
                message.Venue == ExecutionTransactionMessage.VenueNullValue ? null : (Venue)message.Venue,
                message.GetAccount(),
                message.GetOrderId(),
                null,
                message.GetUnderlying(),
                message.GetSymbol(),
                message.GetTrader(),
                null,
                null,
                double.NaN,
                DecodeDoubleNull2(message.Price),
                double.NaN,
                (Side)message.Side,
                message.OrderQty,
                message.FilledQty,
                0,
                0,
                (ExecutionType)message.ExecutionType)
            {
                Multiplier = DecodeDoubleNull2(message.Multiplier)
            };

            ExecutionTransaction?.Invoke(model);
        }

        private void DecodeOrderTagMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OrderTagMessage == null)
            {
                return;
            }

            OrderTagMessage message = new OrderTagMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var moduleType = (ModuleType)message.TypeId;

            OrderTagModel model;
            if (moduleType == ModuleType.EdgeScanFeed)
                model = new EdgeScanFeedOrderTagModel()
                {
                    PermId = message.PermId.IsEmpty ? null : message.GetPermId(),
                    Trader = message.Trader.IsEmpty ? null : message.GetTrader(),
                    ParentSpreadHash = message.ParentSpreadHash.IsEmpty ? null : message.GetParentSpreadHash(),

                    Bid = DecodeDoubleNull2(message.Bid),
                    Ask = DecodeDoubleNull2(message.Ask),
                    BidSize = message.BidSize,
                    AskSize = message.AskSize,

                    Theo = DecodeDoubleNull2(message.Theo),
                    Ema = DecodeDoubleNull2(message.Ema),
                    Edge = DecodeDoubleNull2(message.Edge),

                    UnderBid = DecodeDoubleNull2(message.UnderBid),
                    UnderAsk = DecodeDoubleNull2(message.UnderAsk),
                    UnderBidSize = message.UnderBidSize,
                    UnderAskSize = message.UnderAskSize,

                    ModuleType = moduleType,
                    SubType = (SubType)message.SubTypeCode,
                    SharedId = message.SharedId,
                    Sequence = message.Sequence,
                    SubTypeSequence = message.SubTypeSequence,
                    OrderSubType = (OrderSubType)message.OrderSubType,

                    ResubmitCount = message.ResubmitCount,
                    TotalEstimatedResubmit = message.TotalEstimatedResubmit,
                    EdgeScannerType = (EdgeScannerType)message.EdgeScannerType,
                    EdgeScanFeedConditionCode = (char)message.EdgeScanFeedConditionCode,
                    EdgeScanFeedEdge = DecodeDoubleNull2(message.EdgeScanFeedEdge),
                    EdgeScanFeedTimespan = DecodeDoubleNull2(message.EdgeScanFeedTimespan),
                    EdgeScanFeedRespondLatency = DecodeDoubleNull2(message.EdgeScanFeedRespondLatency),
                    EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice),
                    EdgeScanFeedBuyPrice = DecodeDoubleNull2(message.EdgeScanFeedBuyPrice),
                    EdgeScanFeedSellPrice = DecodeDoubleNull2(message.EdgeScanFeedSellPrice),
                    EdgeScanFeedBuyQty = message.EdgeScanFeedBuyQty,
                    EdgeScanFeedSellQty = message.EdgeScanFeedSellQty,
                    EdgeScanFeedBuyTime = message.EdgeScanFeedBuyTime.FromUnixEpoch(),
                    EdgeScanFeedSellTime = message.EdgeScanFeedSellTime.FromUnixEpoch(),

                    VolaTheo = DecodeDoubleNull2(message.VolaTheo),
                    VolaTheoAdj = DecodeDoubleNull2(message.VolaTheoAdj),
                    VolaIv = message.VolaIv,
                    TheoBid = DecodeDoubleNull2(message.TheoBid),
                    TheoAsk = DecodeDoubleNull2(message.TheoAsk),
                    OrderSource = (OrderSource)message.OrderSource,
                    EdgeType = (EdgeType)message.EdgeType,
                    SessionId = message.SessionId,
                    DigBid = DecodeDoubleNull2(message.DigBid),
                    DigAsk = DecodeDoubleNull2(message.DigAsk),
                    DigBidSize = message.DigBidSize,
                    DigAskSize = message.DigAskSize,
                    WeightedVega = DecodeDoubleNull2(message.WeightedVega),
                };
            else
                model = new OrderTagModel()
                {
                    PermId = message.PermId.IsEmpty ? null : message.GetPermId(),
                    Trader = message.Trader.IsEmpty ? null : message.GetTrader(),
                    ParentSpreadHash = message.ParentSpreadHash.IsEmpty ? null : message.GetParentSpreadHash(),

                    Bid = DecodeDoubleNull2(message.Bid),
                    Ask = DecodeDoubleNull2(message.Ask),
                    BidSize = message.BidSize,
                    AskSize = message.AskSize,

                    Theo = DecodeDoubleNull2(message.Theo),
                    Ema = DecodeDoubleNull2(message.Ema),
                    Edge = DecodeDoubleNull2(message.Edge),

                    UnderBid = DecodeDoubleNull2(message.UnderBid),
                    UnderAsk = DecodeDoubleNull2(message.UnderAsk),
                    UnderBidSize = message.UnderBidSize,
                    UnderAskSize = message.UnderAskSize,

                    ModuleType = moduleType,
                    SubType = (SubType)message.SubTypeCode,
                    SharedId = message.SharedId,
                    Sequence = message.Sequence,
                    SubTypeSequence = message.SubTypeSequence,
                    OrderSubType = (OrderSubType)message.OrderSubType,

                    ResubmitCount = message.ResubmitCount,
                    TotalEstimatedResubmit = message.TotalEstimatedResubmit,

                    VolaTheo = DecodeDoubleNull2(message.VolaTheo),
                    VolaTheoAdj = DecodeDoubleNull2(message.VolaTheoAdj),
                    VolaIv = message.VolaIv,
                    TheoBid = DecodeDoubleNull2(message.TheoBid),
                    TheoAsk = DecodeDoubleNull2(message.TheoAsk),
                    OrderSource = (OrderSource)message.OrderSource,
                    EdgeType = (EdgeType)message.EdgeType,
                    DigBid = DecodeDoubleNull2(message.DigBid),
                    DigAsk = DecodeDoubleNull2(message.DigAsk),
                    DigBidSize = message.DigBidSize,
                    DigAskSize = message.DigAskSize,
                    WeightedVega = DecodeDoubleNull2(message.WeightedVega),
                };

            if (message.InstanceLength() > 0)
            {
                model.Instance = message.GetInstance();
            }
            else
            {
                var legacyInstance = message.GetInstanceLegacy();
                model.Instance = string.IsNullOrEmpty(legacyInstance) ? null : legacyInstance;
            }

            OrderTagMessage?.Invoke(model);
        }

        private void DecodeModifySmartOrderRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ModifySmartOrderRequest == null)
            {
                return;
            }

            ModifySmartOrderRequestMessage message = new ModifySmartOrderRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            ModifySmartRequest modify = new ModifySmartRequest()
            {
                Price = message.Price,
                Quantity = message.Quantity,
                LocalId = message.GetLocalId(),
                PermId = message.GetPermId(),
                OrderId = message.GetOrderId(),
                Account = message.GetAccount(),
                Venue = message.Venue == ModifySmartOrderRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
            };

            var strategyModel = modify.ScrapeStrategyData;

            strategyModel.TakeHidden = message.StrDataTakeHidden != BooleanEnum.NULL_VALUE
                ? message.StrDataTakeHidden == BooleanEnum.True
                : null;
            strategyModel.AtsMode = message.StrDataAtsMode != BooleanEnum.NULL_VALUE
                ? message.StrDataAtsMode == BooleanEnum.True
                : null;
            strategyModel.CancelOnHalt = message.StrDataCancelOnHalt != BooleanEnum.NULL_VALUE
                ? message.StrDataCancelOnHalt == BooleanEnum.True
                : null;

            strategyModel.MakeTake = message.StrDataMakeTake != ModifySmartOrderRequestMessage.StrDataMakeTakeNullValue
                ? (MakeTake)message.StrDataMakeTake
                : null;
            strategyModel.Algorithm = message.StrDataAlgorithm != ModifySmartOrderRequestMessage.StrDataAlgorithmNullValue
                ? (Algorithm)message.StrDataAlgorithm
                : null;
            strategyModel.PriceMethod = message.StrDataPriceMethod != ModifySmartOrderRequestMessage.StrDataPriceMethodNullValue
                ? (PriceMethod)message.StrDataPriceMethod
                : null;

            strategyModel.ReminderQty = message.StrDataReminderQty != ModifySmartOrderRequestMessage.StrDataReminderQtyNullValue
                ? message.StrDataReminderQty
                : null;
            strategyModel.MinWorkingQty = message.StrDataMinWorkingQty != ModifySmartOrderRequestMessage.StrDataMinWorkingQtyNullValue
                ? message.StrDataMinWorkingQty
                : null;
            strategyModel.MinQuoteQty = message.StrDataMinQuoteQty != ModifySmartOrderRequestMessage.StrDataMinQuoteQtyNullValue
                ? message.StrDataMinQuoteQty
                : null;
            strategyModel.WorkingQty = message.StrDataWorkingQty != ModifySmartOrderRequestMessage.StrDataWorkingQtyNullValue
                ? message.StrDataWorkingQty
                : null;

            strategyModel.DiscretionTake = DecodeDoubleNull2Nullable(message.StrDataDiscretionTake);
            strategyModel.UndPrice = DecodeDoubleNull2Nullable(message.StrDataUndPrice);
            strategyModel.MaxPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMaxPriceUnd);
            strategyModel.MinPriceUnd = DecodeDoubleNull2Nullable(message.StrDataMinPriceUnd);
            strategyModel.PriceRange = DecodeDoubleNull2Nullable(message.StrDataPriceRange);

            strategyModel.LimitToMarketTime = message.StrDataLimitToMarketTime != ModifySmartOrderRequestMessage.StrDataLimitToMarketTimeNullValue
                ? message.StrDataLimitToMarketTime.FromUnixEpoch()
                : null;

            ModifySmartOrderRequest?.Invoke(modify);
        }

        private void DecodeModeledTheoUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ModeledTheoUpdate == null)
            {
                return;
            }

            ModeledTheoUpdateMessage message = new ModeledTheoUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var update = new ModeledTheoUpdate()
            {
                ModelId = message.ModelId,
                UnderlyingPrice = message.UnderlyingPrice,
                CalcTime = message.CalcTime,
            };

            ModeledTheoUpdateMessage.TheosGroup theosGroup = message.Theos;
            while (theosGroup.HasNext)
            {
                ModeledTheoUpdateMessage.TheosGroup? theo = theosGroup.Next();
                ModeledTheo theoModel = new ModeledTheo()
                {
                    SymbolId = theo.SymbolId,
                    Theo = theo.Theo,
                };
                update.Theos.Add(theoModel);
            }

            ModeledTheoUpdate?.Invoke(update);
        }

        private void DecodeSpreadBookQuoteMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SpreadBookQuoteUpdate == null)
            {
                return;
            }

            SpreadBookQuoteMessage message = new SpreadBookQuoteMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SpreadBookQuote model = new()
            {
                FromCache = message.FromCache == BooleanEnum.True,
                IsBidPrice1Valid = message.IsBidPrice1Valid == BooleanEnum.True,
                IsAskPrice1Valid = message.IsAskPrice1Valid == BooleanEnum.True,
                IsBidPrice2Valid = message.IsBidPrice2Valid == BooleanEnum.True,
                IsAskPrice2Valid = message.IsAskPrice2Valid == BooleanEnum.True,
                BidExch1 = (SrExch)message.BidExch1,
                AskExch1 = (SrExch)message.AskExch1,
                UpdateType = (SrUpdateType)message.UpdateType,
                BidMask1 = message.BidMask1,
                AskMask1 = message.AskMask1,
                BidSize1 = message.BidSize1,
                AskSize1 = message.AskSize1,
                BidSize2 = message.BidSize2,
                AskSize2 = message.AskSize2,
                PrintVolume = message.PrintVolume,
                BidPrice1 = message.BidPrice1,
                AskPrice1 = message.AskPrice1,
                BidPrice2 = message.BidPrice2,
                AskPrice2 = message.AskPrice2,
                BidTime = message.BidTime.FromUnixEpoch(),
                AskTime = message.AskTime.FromUnixEpoch(),
                Timestamp = message.Timestamp.FromUnixEpoch(),
                SrcTimestamp = message.SrcTimestamp,
                NetTimestamp = message.NetTimestamp,
                SpreadKey = message.SpreadKey,
                Underlying = message.GetUnderlyingSymbol(),
                Symbol = message.GetSpreadSymbol(),
            };

            SpreadBookQuoteUpdate?.Invoke(model);
        }

        private void DecodeSpreadPrintMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SpreadPrintUpdate == null)
            {
                return;
            }

            SpreadPrintMessage message = new SpreadPrintMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SpreadPrint model = new()
            {
                FromCache = message.FromCache == BooleanEnum.True,
                Side = message.Side == SpreadPrintMessage.SideNullValue ? null : (Side)message.Side,
                PrtExch = (SrExch)message.PrtExch,
                BaseStrategy = (BaseStrategy)message.BaseStrategy,

                PrtSize = message.PrtSize,
                PrtPrice = message.PrtPrice,

                SrcTimestamp = message.SrcTimestamp,
                NetTimestamp = message.NetTimestamp,
                Timestamp = message.Timestamp.FromUnixEpoch(),

                Underlying = message.GetUnderlying(),
                Symbol = message.GetSymbol(),
                SpreadId = message.GetSpreadId(),
                SpreadDescription = message.GetSpreadDescription(),
            };

            SpreadPrintUpdate?.Invoke(model);
        }

        private void DecodeCobTradeRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CobTradeRequest == null)
            {
                return;
            }

            CobTradeRequestMessage message = new CobTradeRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CobTradeRequest.Invoke(message.RequestID, message.GetUnderlyingSymbol(), message.StartTime.FromUnixEpoch(), message.EndTime.FromUnixEpoch(), message.LimitCount);
        }

        private void DecodeAuctionPrintMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuctionPrintUpdate == null)
            {
                return;
            }

            AuctionPrintMessage message = new AuctionPrintMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var update = new AuctionPrint();

            update.FromCache = message.FromCache == BooleanEnum.True;
            update.ContainsFlex = message.ContainsFlex == BooleanEnum.NULL_VALUE ? null : message.ContainsFlex == BooleanEnum.True;
            update.ContainsHedge = message.ContainsHedge == BooleanEnum.NULL_VALUE ? null : message.ContainsHedge == BooleanEnum.True;
            update.ContainsMultiHedge = message.ContainsMultiHedge == BooleanEnum.NULL_VALUE ? null : message.ContainsMultiHedge == BooleanEnum.True;
            update.HasCustPrc = message.HasCustPrc == BooleanEnum.NULL_VALUE ? null : message.HasCustPrc == BooleanEnum.True;
            update.IsTestAuction = message.IsTestAuction == BooleanEnum.NULL_VALUE ? null : message.IsTestAuction == BooleanEnum.True;
            update.BidMask = message.BidMask;
            update.AskMask = message.AskMask;
            update.PrtSize = message.PrtSize;
            update.PrtSize2 = message.PrtSize2;
            update.CustQty = message.CustQty;
            update.NumOptLegs = message.NumOptLegs;
            update.ExchBidSz = message.ExchBidSz;
            update.ExchAskSz = message.ExchAskSz;
            update.BidPrc = message.BidPrc;
            update.AskPrc = message.AskPrc;
            update.BidPrc10M = message.BidPrc10M;
            update.AskPrc10M = message.AskPrc10M;
            update.BidPrc1M = message.BidPrc1M;
            update.AskPrc1M = message.AskPrc1M;
            update.UAvgDailyVlm = message.UAvgDailyVlm;
            update.UPrc10M = message.UPrc10M;
            update.UPrc1M = message.UPrc1M;
            update.PrtSurfPrc = message.PrtSurfPrc;
            update.PrtSurfVol = message.PrtSurfVol;
            update.CommEnhancement = message.CommEnhancement;
            update.NetDe = message.NetDe;
            update.NetGa = message.NetGa;
            update.NetTh = message.NetTh;
            update.NetVe = message.NetVe;
            update.ExchAskPrc = message.ExchAskPrc;
            update.ExchBidPrc = message.ExchBidPrc;
            update.SurfVol1M = message.SurfVol1M;
            update.SurfVol10M = message.SurfVol10M;
            update.SurfPrc1M = message.SurfPrc1M;
            update.SurfPrc10M = message.SurfPrc10M;
            update.PkgAskPrc = message.PkgAskPrc;
            update.PkgBidPrc = message.PkgBidPrc;
            update.PkgSurfPrc = message.PkgSurfPrc;
            update.UBid = message.UBid;
            update.UAsk = message.UAsk;
            update.PrtUBid = message.PrtUBid;
            update.PrtUAsk = message.PrtUAsk;
            update.PrtUPrc = message.PrtUPrc;
            update.PrtPrice = message.PrtPrice;
            update.PrtPrice2 = message.PrtPrice2;
            update.CustPrc = message.CustPrc;
            update.PrtType = (PrtType)message.PrtType;
            update.AuctionSource = (AuctionSource)message.AuctionSource;
            update.AuctionType = (AuctionType)message.AuctionType;
            update.CustFirmType = (FirmType)message.CustFirmType;
            update.SpreadClass = (SpreadClass)message.SpreadClass;
            update.SpreadFlavor = (SpreadFlavor)message.SpreadFlavor;
            update.CustSide = message.CustSide == AuctionPrintMessage.CustSideNullValue ? null : (Side)message.CustSide;
            update.PrtTime = message.PrtTime.FromUnixEpoch();
            update.Timestamp = message.Timestamp.FromUnixEpoch();
            update.NoticeTime = message.NoticeTime.FromUnixEpoch();
            update.TradeDate = message.TradeDate.FromUnixEpoch();
            update.Pkey = message.Pkey;
            update.Underlying = message.GetUnderlying();

            var legsGroup = message.Legs;
            while (legsGroup.HasNext)
            {
                var legMessage = legsGroup.Next();
                AuctionPrintLeg auctionPrintLeg = new AuctionPrintLeg();

                auctionPrintLeg.LegSymbol = legMessage.GetSymbol();
                auctionPrintLeg.LegSecType = (SpdrKeyType)legMessage.LegSecType;
                auctionPrintLeg.LegSide = legMessage.LegSide == AuctionPrintMessage.LegsGroup.LegSideNullValue ? null : (Side)legMessage.LegSide;
                auctionPrintLeg.LegExpType = (ExpiryType)legMessage.LegExpType;
                auctionPrintLeg.LegBidMask = legMessage.LegBidMask;
                auctionPrintLeg.LegAskMask = legMessage.LegAskMask;
                auctionPrintLeg.LegRatio = legMessage.LegRatio;
                auctionPrintLeg.LegBidSz = legMessage.LegBidSz;
                auctionPrintLeg.LegAskSz = legMessage.LegAskSz;
                auctionPrintLeg.LegUndPerCn = legMessage.LegUndPerCn;
                auctionPrintLeg.LegPointValue = legMessage.LegPointValue;
                auctionPrintLeg.LegYears = legMessage.LegYears;
                auctionPrintLeg.LegRate = legMessage.LegRate;
                auctionPrintLeg.LegAtmVol = legMessage.LegAtmVol;
                auctionPrintLeg.LegDdivPv = legMessage.LegDdivPv;
                auctionPrintLeg.LegTVol = legMessage.LegTVol;
                auctionPrintLeg.LegSVol = legMessage.LegSVol;
                auctionPrintLeg.LegSDiv = legMessage.LegSDiv;
                auctionPrintLeg.LegSPrc = legMessage.LegSPrc;
                auctionPrintLeg.LegDe = legMessage.LegDe;
                auctionPrintLeg.LegGa = legMessage.LegGa;
                auctionPrintLeg.LegTh = legMessage.LegTh;
                auctionPrintLeg.LegVe = legMessage.LegVe;
                auctionPrintLeg.LegBid = legMessage.LegBid;
                auctionPrintLeg.LegAsk = legMessage.LegAsk;
                auctionPrintLeg.LegSVolOk = legMessage.LegSVolOk == BooleanEnum.NULL_VALUE ? null : legMessage.LegSVolOk == BooleanEnum.True;

                update.Legs.Add(auctionPrintLeg);
            }

            update.Symbol = message.GetSymbol();
            update.SpreadId = message.GetSpreadId();
            update.SpreadDescription = message.GetSpreadDescription();
            update.CustAgentMPID = message.GetCustAgentMPID();
            update.Industry = message.GetIndustry();

            AuctionPrintUpdate.Invoke(update);
        }

        private void DecodeCancelDataRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CancelDataRequest == null)
            {
                return;
            }

            CancelDataRequestMessage message = new CancelDataRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CancelDataRequest.Invoke(message.RequestID, (SubscriptionFieldType)message.FieldType);
        }

        private void DecodeSpreadExchPrintMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CobTradeResponse == null)
            {
                return;
            }

            SpreadExchPrintMessage message = new SpreadExchPrintMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var reqId = message.RequestID;
            var count = message.Count;

            List<SpreadExchPrint> updates = new(count);


            SpreadExchPrintMessage.PrintsGroup printsGroup = message.Prints;
            while (printsGroup.HasNext)
            {
                SpreadExchPrintMessage.PrintsGroup? print = printsGroup.Next();

                var model = new SpreadExchPrint();

                model.IsPrintPriceValid = print.IsPrintPriceValid == BooleanEnum.True;
                model.HasFlexLeg = print.HasFlexLeg == BooleanEnum.True;
                model.HasHedgeLeg = print.HasHedgeLeg == BooleanEnum.True;

                model.Exch = (SrExch)print.Exch;
                model.StrategyClass = (SrStrategyClass)print.StrategyClass;

                model.Side = print.Side == SpreadExchPrintMessage.PrintsGroup.SideNullValue ? null : (Side)print.Side;
                model.MinAnchorSide = print.MinAnchorSide == SpreadExchPrintMessage.PrintsGroup.SideNullValue ? null : (Side)print.MinAnchorSide;
                model.MaxAnchorSide = print.MaxAnchorSide == SpreadExchPrintMessage.PrintsGroup.SideNullValue ? null : (Side)print.MaxAnchorSide;
                model.StockLegSide = print.StockLegSide == SpreadExchPrintMessage.PrintsGroup.SideNullValue ? null : (Side)print.StockLegSide;
                model.FutureLegSide = print.FutureLegSide == SpreadExchPrintMessage.PrintsGroup.SideNullValue ? null : (Side)print.FutureLegSide;

                model.PrintSize = print.PrintSize;
                model.PrintPrice = DecodeDoubleNull2(print.PrintPrice);
                model.PrintNumber = print.PrintNumber;
                model.StcTimestamp = print.StcTimestamp;
                model.NetTimestamp = print.NetTimestamp;
                model.Timestamp = print.Timestamp.FromUnixEpoch();
                model.MinAnchorLeg = print.GetMinAnchorLeg();
                model.MaxAnchorLeg = print.GetMaxAnchorLeg();
                model.Underlying = print.GetUnderlyingSymbol();
                model.NumOptLegs = print.NumOptLegs;

                var legsGroup = print.Legs;
                while (legsGroup.HasNext)
                {
                    SpreadExchPrintMessage.PrintsGroup.LegsGroup? leg = legsGroup.Next();

                    var legModel = new SrSpreadLeg();
                    legModel.LegSecurity = leg.GetSecurity();
                    legModel.LegSide = leg.Side == SpreadExchPrintMessage.PrintsGroup.LegsGroup.SideNullValue
                        ? null
                        : (Side)leg.Side;
                    legModel.LegRatio = leg.LegRatio;
                    legModel.LegPositionType = (PositionEffect)leg.OpenClose;

                    model.Legs.Add(legModel);
                }

                updates.Add(model);
            }

            CobTradeResponse?.Invoke(reqId, updates);
        }

        private void DecodeSpreadExchOrderMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SpreadExchOrderUpdate == null)
            {
                return;
            }

            SpreadExchOrderMessage message = new SpreadExchOrderMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SpreadExchOrder model = new SpreadExchOrder()
            {
                FromCache = message.FromCache == BooleanEnum.True,
                AllOrNone = message.AllOrNone == BooleanEnum.True,
                FlipSide = message.FlipSide == BooleanEnum.True,
                IsPriceValid = message.IsPriceValid == BooleanEnum.True,

                Exch = (SrExch)message.Exch,
                FirmType = (SrFirmType)message.FirmType,
                MarketQualifier = (SrMarketQualifier)message.MarketQualifier,
                OrderStatus = (SrOrderStatus)message.OrderStatus,
                OrderType = (SrOrderType)message.OrderType,
                TimeInForce = (SrTimeInForce)message.TimeInForce,
                BaseStrategy = (BaseStrategy)message.BaseStrategy,

                OrigOrderSize = message.OrigOrderSize,
                OrderSize = message.OrderSize,
                Price = message.Price,

                DgwTimestamp = message.DgwTimestamp,
                SrcTimestamp = message.SrcTimestamp,
                NetTimestamp = message.NetTimestamp,
                Timestamp = message.Timestamp.FromUnixEpoch(),

                Underlying = message.GetUnderlying(),
                Symbol = message.GetSpreadSymbol(),
                ClearingAccount = message.GetClearingAccount(),
                ClearingFirm = message.GetClearingFirm(),
                OrderID = message.GetOrderID(),
                SpreadKey = message.GetSpreadKey(),

                SpreadId = message.GetSpreadId(),
                SpreadDescription = message.GetSpreadDescription(),
            };

            SpreadExchOrderUpdate?.Invoke(model);
        }

        private void DecodeSlimGreekUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            SlimGreekUpdateMessage message = new SlimGreekUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var tickerId = (message.TickerId[0] << 16) | (message.TickerId[1] << 8) | message.TickerId[2];

            var update = new SlimGreekUpdateModel()
            {
                TickerId = tickerId,
                ModelId = message.ModelId,
                Theo = DecodePriceNull3(message.Theo),
                Delta = DecodePriceNull3(message.Delta),
                Gamma = DecodePriceNull3(message.Gamma),
                Vega = DecodePriceNull3(message.Vega),
                Vol = DecodePriceNull3(message.Vol),
                TimeStamp = message.TimeStamp,
            };

            _updateManager.HandleUpdate(update);
        }

        private void DecodeModelDescriptionMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ModelDescriptionUpdate == null)
            {
                return;
            }

            ModelDescriptionMessage message = new ModelDescriptionMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            ModelDescriptionUpdate?.Invoke(message.ModelId, message.GetDescription());
        }

        private void DecodeCancelTokenMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CancelTokenMessage == null)
            {
                return;
            }

            CancelTokenMessage message = new CancelTokenMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CancelTokenMessage?.Invoke(message.RequestID, message.GetToken());
        }

        private void DecodeImpliedQuoteMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ImpliedQuoteUpdate == null)
            {
                return;
            }

            ImpliedQuoteUpdateMessage message = new ImpliedQuoteUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var impliedQuoteUpdate = new ImpliedQuoteUpdate()
            {
                Index = (message.Index[0] << 16) | (message.Index[1] << 8) | message.Index[2],
                Underlying = message.GetUnderlyingSymbol(),
                Bid = DecodePriceNull3(message.Bid),
                Ask = DecodePriceNull3(message.Ask),
                Theo = DecodePriceNull3(message.Theo),
                UnderBid = DecodePriceNull3(message.UnderBid),
                UnderAsk = DecodePriceNull3(message.UnderAsk),
                ImpliedBid = DecodePriceNull3(message.ImpliedQuoteBid),
                ImpliedAsk = DecodePriceNull3(message.ImpliedQuoteAsk),
                ImpliedBidRecordPrice = DecodePriceNull3(message.ImpliedQuoteBidRecordPrice),
                ImpliedBidRecordTheo = DecodePriceNull3(message.ImpliedQuoteBidRecordTheo),
                ImpliedBidRecordMovement = DecodePriceNull3(message.ImpliedQuoteBidRecordMovement),
                ImpliedBidRecordNonDeltaMovement = DecodePriceNull3(message.ImpliedBidRecordNonDeltaMovement),
                ImpliedBidRecordTime = message.ImpliedQuoteBidRecordTime.FromUnixEpoch(),
                ImpliedAskRecordPrice = DecodePriceNull3(message.ImpliedQuoteAskRecordPrice),
                ImpliedAskRecordTheo = DecodePriceNull3(message.ImpliedQuoteAskRecordTheo),
                ImpliedAskRecordMovement = DecodePriceNull3(message.ImpliedQuoteAskRecordMovement),
                ImpliedAskRecordNonDeltaMovement = DecodePriceNull3(message.ImpliedAskRecordNonDeltaMovement),
                ImpliedAskRecordTime = message.ImpliedQuoteAskRecordTime.FromUnixEpoch(),
            };
            ImpliedQuoteUpdate?.Invoke(impliedQuoteUpdate);
        }

        private void DecodeGetClosestOptionRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (GetClosestOptionRequest == null)
            {
                return;
            }

            GetClosestOptionRequestMessage message = new GetClosestOptionRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            GetClosestOptionRequest?.Invoke(message.RequestID, message.GetUnderlyingSymbol(), (SubscriptionFieldType)message.Field, (Data.Enums.PutCall)message.PutCall, message.Expiration.FromUnixEpoch(), message.Value);
        }

        private void DecodeGetClosestOptionResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (GetClosestOptionResponse == null)
            {
                return;
            }

            GetClosestOptionResponseMessage message = new GetClosestOptionResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            GetClosestOptionResponse?.Invoke(message.RequestID, message.GetSymbol());
        }

        private void DecodeNextOptionPermsRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (NextOptionPermsRequest == null)
            {
                return;
            }

            NextOptionPermsRequestMessage message = new NextOptionPermsRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int requestId = message.RequestId;
            PermutationDirection direction = (PermutationDirection)message.Direction;
            PermMode mode = (PermMode)message.Mode;
            int count = message.Count;
            string symbol = message.GetSymbol();

            NextOptionPermsRequest?.Invoke(requestId, symbol, direction, mode, count);
        }

        private void DecodeNextOptionPermsResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (NextOptionPermsResponse == null)
            {
                return;
            }

            NextOptionPermsResponseMessage message = new NextOptionPermsResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<string> symbols = new List<string>(count);
            NextOptionPermsResponseMessage.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                NextOptionPermsResponseMessage.SymbolsGroup next = symbolsGroup.Next();
                symbols.Add(next.GetSymbol());
            }

            NextOptionPermsResponse?.Invoke(requestId, lastGroup, symbols);
        }

        private void DecodeNextSpreadPermsRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (NextSpreadPermsRequest == null)
            {
                return;
            }

            NextSpreadPermsRequestMessage message = new NextSpreadPermsRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int requestId = message.RequestId;
            PermutationDirection direction = (PermutationDirection)message.Direction;
            PermMode mode = (PermMode)message.Mode;
            Data.Enums.PermSide permSide = (Data.Enums.PermSide)message.PermSide;
            int count = message.Count;
            Data.Enums.BaseStrategy baseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
            bool maintainBaseStrategy = message.MaintainBaseStrategy == BooleanEnum.True;
            bool maintainBaseStrategyFlyException = message.MaintainBaseStrategyFlyException == BooleanEnum.True;
            bool skipCheck = message.SkipCheck == BooleanEnum.True;

            NextSpreadPermsRequestMessage.LegsGroup legsGroup = message.Legs;
            List<PermLegRequest> legs = new List<PermLegRequest>(legsGroup.Count);
            while (legsGroup.HasNext)
            {
                NextSpreadPermsRequestMessage.LegsGroup next = legsGroup.Next();
                legs.Add(new PermLegRequest
                {
                    Symbol = next.GetSymbol(),
                    Side = (Data.Enums.Side)next.Side,
                    Ratio = next.Ratio,
                });
            }

            NextSpreadPermsRequest?.Invoke(requestId, legs, direction, mode, permSide, count, baseStrategy, maintainBaseStrategy, maintainBaseStrategyFlyException, skipCheck);
        }

        private void DecodeNextSpreadPermsResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (NextSpreadPermsResponse == null)
            {
                return;
            }

            NextSpreadPermsResponseMessage message = new NextSpreadPermsResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int requestId = message.RequestId;
            bool lastGroup = message.LastGroup == BooleanEnum.True;
            int count = message.Count;
            List<PermSpreadResult> perms = new List<PermSpreadResult>(count);
            NextSpreadPermsResponseMessage.PermsGroup permsGroup = message.Perms;
            while (permsGroup.HasNext)
            {
                NextSpreadPermsResponseMessage.PermsGroup nextPerm = permsGroup.Next();
                NextSpreadPermsResponseMessage.PermsGroup.LegsGroup legsGroup = nextPerm.Legs;
                List<PermLegResult> legs = new List<PermLegResult>(legsGroup.Count);
                while (legsGroup.HasNext)
                {
                    NextSpreadPermsResponseMessage.PermsGroup.LegsGroup nextLeg = legsGroup.Next();
                    legs.Add(new PermLegResult
                    {
                        Symbol = nextLeg.GetSymbol(),
                        Side = (Data.Enums.Side)nextLeg.Side,
                        Ratio = nextLeg.Ratio,
                    });
                }
                perms.Add(new PermSpreadResult { Legs = legs });
            }

            NextSpreadPermsResponse?.Invoke(requestId, lastGroup, perms);
        }

        private void DecodeJsonRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (JsonRequest == null)
            {
                return;
            }

            JsonRequestMessage message = new JsonRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            JsonRequest jsonRequest = new JsonRequest()
            {
                RequestId = message.RequestId,
                RequestType = (RequestType)message.RequestType,
                Content = message.GetContent(),
            };

            JsonRequest?.Invoke(jsonRequest);
        }

        private void DecodeJsonResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (JsonResponse == null)
            {
                return;
            }

            JsonResponseMessage message = new JsonResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            JsonResponse jsonResponse = new JsonResponse()
            {
                RequestId = message.RequestId,
                IsSuccess = message.IsSuccess == BooleanEnum.True,
                Content = message.GetContent(),
            };

            JsonResponse?.Invoke(jsonResponse);
        }

        private void DecodeRiskCheckResultMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (RiskCheckResult == null)
            {
                return;
            }


            RiskCheckResultMessage message = new RiskCheckResultMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            IHaveRisk? riskModel = RiskRequestHandler?.Invoke(message.RiskCheckId);
            if (riskModel != null)
            {
                riskModel.RiskCheckPassed = message.Passed == BooleanEnum.True;
                riskModel.RiskCheckMessage = message.GetMessage();

                RiskCheckResult?.Invoke(riskModel);
            }
        }

        private void DecodeOrderRiskRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OrderRiskRequest == null)
            {
                return;
            }

            OrderRiskRequestMessage message = new OrderRiskRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            OrderRisk request = new OrderRisk()
            {
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
                IsOpening = message.IsOpening == BooleanEnum.True,
                Venue = message.Venue == OrderRiskRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                Side = message.Side == OrderRiskRequestMessage.SideNullValue ? null : (Side)message.Side,
                Qty = message.Qty,
                Price = DecodeDoubleNull2(message.Price),
                Route = message.GetRoute(),
                BaseStrategy = (BaseStrategy)message.BaseStrategy,
                StrikeSpacing = message.StrikeSpacing,
                OrderId = message.GetOrderId(),
                UnderlyingSymbol = message.GetUnderlyingSymbol(),
                Broker = message.BrokerId == OrderRiskRequestMessage.BrokerIdNullValue ? null : (Broker)message.BrokerId,
                Exchange = message.ExchangeId == OrderRiskRequestMessage.ExchangeIdNullValue ? null : (Exchange)message.ExchangeId,
                SubType = message.SubType == OrderRiskRequestMessage.SubTypeNullValue ? null : (OrderSubType)message.SubType,
                Description = message.GetDescription(),
                Symbol = message.GetSymbol(),
            };

            OrderRiskRequest?.Invoke(request);
        }

        private void DecodeCancelRiskRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CancelRiskRequest == null)
            {
                return;
            }

            CancelRiskRequestMessage message = new CancelRiskRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CancelRisk request = new CancelRisk()
            {
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
                IsOpening = message.IsOpening == BooleanEnum.True,
                Venue = message.Venue == CancelRiskRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                Side = message.Side == CancelRiskRequestMessage.SideNullValue ? null : (Side)message.Side,
                Qty = message.Qty,
                Price = DecodeDoubleNull2(message.Price),
                Route = message.GetRoute(),
                BaseStrategy = (BaseStrategy)message.BaseStrategy,
                StrikeSpacing = message.StrikeSpacing,
                SubmitTime = message.SubmitTime.FromUnixEpoch(),
                OrderId = message.GetOrderId(),
                UnderlyingSymbol = message.GetUnderlyingSymbol(),
                Description = message.GetDescription(),
                Symbol = message.GetSymbol(),
            };

            CancelRiskRequest?.Invoke(request);
        }

        private void DecodeCancelReplaceRiskRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (CancelReplaceRiskRequest == null)
            {
                return;
            }

            CancelReplaceRiskRequestMessage message = new CancelReplaceRiskRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            CancelReplaceRisk request = new CancelReplaceRisk()
            {
                UserId = message.UserId,
                RiskCheckId = message.RiskCheckId,
                IsOpening = message.IsOpening == BooleanEnum.True,
                Venue = message.Venue == CancelReplaceRiskRequestMessage.VenueNullValue ? null : (Venue)message.Venue,
                Side = message.Side == CancelReplaceRiskRequestMessage.SideNullValue ? null : (Side)message.Side,
                Qty = message.Qty,
                Price = DecodeDoubleNull2(message.Price),
                Route = message.GetRoute(),
                NewQty = message.NewQty,
                NewPrice = DecodeDoubleNull2(message.NewPrice),
                NewRoute = message.GetNewRoute(),
                BaseStrategy = (BaseStrategy)message.BaseStrategy,
                StrikeSpacing = message.StrikeSpacing,
                SubmitTime = message.SubmitTime.FromUnixEpoch(),
                OrderId = message.GetOrderId(),
                UnderlyingSymbol = message.GetUnderlyingSymbol(),
                Description = message.GetDescription(),
                Symbol = message.GetSymbol(),
            };

            CancelReplaceRiskRequest?.Invoke(request);
        }

        private void DecodeOrderUpdateModelMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OrderUpdate == null)
            {
                return;
            }

            OrderUpdateModelMessage message = new OrderUpdateModelMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            OrderUpdateModel request = new OrderUpdateModel()
            {
                OrderStatus = (OrderStatus)message.OrderStatus,
                ExecutionType = (ExecutionType)message.ExecutionType,
                Price = DecodeDoubleNull2(message.Price),
                AvgPrice = DecodeDoubleNull2(message.AvgPrice),
                LastPx = DecodeDoubleNull2(message.LastPrice),
                LastQty = message.LastQty,
                CumQty = message.CumQty,
                LeavesQty = message.LeavesQty,
                Qty = message.Qty,
                LastUpdateTime = message.LastUpdateTime.FromUnixEpoch(),
                IsCancelReject = message.IsCancelReject == BooleanEnum.True,
                Side = message.Side == CancelReplaceRiskRequestMessage.SideNullValue ? null : (Side)message.Side,
                ClientOrderId = message.GetClientOrderId(),
                PrevClientOrderId = message.GetPrevClientOrderId(),
                OrigOrderId = message.GetOrigOrderId(),
                OrderId = message.GetOrderId(),
                LastExchange = message.GetLastExchange(),
                Message = message.GetMessage(),
                Route = message.GetRoute(),
                ContraTrader = message.ContraTrader == OrderUpdateModelMessage.ContraTraderNullValue ? null : (ContraTrader)message.ContraTrader,
            };

            OrderUpdate?.Invoke(request);
        }

        private void DecodeFitUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            FitUpdateMessage message = new FitUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            UnderFitResult? model = _updateManager.GetUnderFitResultModel(message.Index);

            if (model == null)
            {
                return;
            }

            lock (model.Lock)
            {
                model.Index = message.Index;
                model.Sequence = message.Sequence;
                model.UnderlyingSpot = DecodePriceNull3(message.UnderlyingSpot);
                model.UnderlyingMid = DecodePriceNull3(message.UnderlyingMid);
                model.PriceMetric = DecodeDoubleNull4(message.PriceMetric);
                model.SnapshotTime = message.SnapshotTime.FromUnixEpoch();

                FitUpdateMessage.FitResultsGroup fitResultsGroup = message.FitResults;
                var index = 0;
                while (fitResultsGroup.HasNext)
                {
                    FitUpdateMessage.FitResultsGroup? fitResult = fitResultsGroup.Next();

                    var i = index++;
                    FitResult fitResultModel;
                    if (i < model.FitResults.Count)
                    {
                        fitResultModel = model.FitResults[i];
                    }
                    else
                    {
                        fitResultModel = new FitResult();
                        model.FitResults.Add(fitResultModel);
                    }

                    fitResultModel.Index = fitResult.Index;
                    fitResultModel.Theo = DecodePriceNull3(fitResult.Theo);
                    fitResultModel.Delta = DecodeDoubleNull4(fitResult.Delta);
                    fitResultModel.Gamma = DecodeDoubleNull4(fitResult.Gamma);
                    fitResultModel.Vega = DecodeDoubleNull4(fitResult.Vega);
                    fitResultModel.Iv = fitResult.Iv;
                }
            }

            _updateManager.HandleUpdate(model);
        }

        private void DecodeAutomationStateChangeMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AutomationStateChanged == null)
            {
                return;
            }

            AutomationStateChangeMessage message = new AutomationStateChangeMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var id = message.GetLocalOrderId();
            var automationRunning = message.AutomationRunning == BooleanEnum.True;

            AutomationStateChanged?.Invoke(id, automationRunning);
        }

        private void DecodePerformanceModeRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PerformanceModeRequest == null)
            {
                return;
            }

            PerformanceModeRequestMessage message = new PerformanceModeRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var isPerformanceModeEnabled = message.IsPerformanceModeEnabled == BooleanEnum.True;

            PerformanceModeRequest?.Invoke(isPerformanceModeEnabled);
        }

        private void DecodeSubmissionSummaryUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_portfolioManager == null && SubmissionSummaryUpdate == null)
            {
                return;
            }

            SubmissionSummaryUpdateMessage message = new SubmissionSummaryUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            SubmissionsSummary summary = new SubmissionsSummary()
            {
                Broker = (Broker)message.BrokerId,
                BrokerTotalSubmissions = message.BrokerTotalSubmissions,
                BrokerUniqueSubmissions = message.BrokerUniqueSubmissions,
                Exchange = (Exchange)message.ExchangeId,
                ExchangeTotalSubmissions = message.ExchangeTotalSubmissions,
                ExchangeUniqueSubmissions = message.ExchangeUniqueSubmissions,
                Underlying = message.GetUnderlyingSymbol(),
                UnderlyingTotalSubmissions = message.UnderlyingTotalSubmissions,
                UnderlyingUniqueSubmissions = message.UnderlyingUniqueSubmissions,
                Trader = message.GetTrader(),
                TraderTotalSubmissions = message.TraderTotalSubmissions,
                TraderUniqueSubmissions = message.TraderUniqueSubmissions,
            };

            _portfolioManager?.SubmissionSummaryUpdate(summary);
            SubmissionSummaryUpdate?.Invoke(summary);
        }

        private void DecodePricingRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PricingRequest == null)
            {
                return;
            }

            PricingRequestMessage message = new PricingRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            PricingRequestModel request = new PricingRequestModel();
            request.RequestId = message.RequestId;


            var legs = message.Legs;
            while (legs.HasNext)
            {
                var nextLeg = legs.Next();
                var legModel = new PricingRequestLeg();
                legModel.TickerId = (nextLeg.TickerId[0] << 16) | (nextLeg.TickerId[1] << 8) | nextLeg.TickerId[2];
                legModel.Side = (Side)nextLeg.Side;
                legModel.Ratio = nextLeg.Ratio;
                request.Legs.Add(legModel);
            }

            PricingRequest?.Invoke(request);
        }

        private void DecodePricingResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (PricingResponse == null)
            {
                return;
            }

            PricingResponseMessage message = new PricingResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            PricingResponseModel response = new PricingResponseModel();
            response.RequestId = message.RequestId;
            response.Bid = DecodePriceNull3(message.Bid);
            response.Ask = DecodePriceNull3(message.Ask);
            response.HwTheo = DecodePriceNull3(message.HwTheo);
            response.HwAdjTheo = DecodePriceNull3(message.HwAdjTheo);
            response.HwDelta = DecodePriceNull3(message.HwDelta);
            response.VolaTheo = DecodePriceNull3(message.VolaTheo);
            response.VolaAdjTheo = DecodePriceNull3(message.VolaAdjTheo);
            response.AdjVolaEma = DecodePriceNull3(message.AdjVolaEma);
            response.AdjDaEma = DecodePriceNull3(message.AdjDaEma);
            response.UnderBid = DecodePriceNull3(message.UnderBid);
            response.UnderAsk = DecodePriceNull3(message.UnderAsk);

            PricingResponse?.Invoke(response);
        }

        private void DecodeTradesRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (TradesRequest == null)
            {
                return;
            }

            TradesRequestMessage message = new TradesRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            TradesRequest?.Invoke(message.RequestId, message.GetSymbol(), message.StartTime.FromUnixEpoch(), message.EndTime.FromUnixEpoch());
        }

        private void DecodeTradesResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (TradesResponse == null)
            {
                return;
            }

            TradesResponseMessage message = new TradesResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var id = message.RequestId;
            var lastGroup = message.LastGroup;
            var batchCount = message.BatchCount;
            var totalCount = message.TotalCount;

            List<MbpTradeModel> trades = new List<MbpTradeModel>((int)batchCount);

            var tradesGroup = message.Trades;
            while (tradesGroup.HasNext)
            {
                var nextTrade = tradesGroup.Next();
                var trade = new MbpTradeModel();
                trades.Add(trade);
                trade.Publisher = (DbPublisher)nextTrade.Publisher;
                trade.InstrumentId = nextTrade.InstrumentId;
                trade.TsEvent = nextTrade.TsEvent;
                trade.TsRecv = nextTrade.TsRecv;
                trade.Price = DecodePriceNull3(nextTrade.Price);
                trade.Size = nextTrade.Size;
                trade.Action = (DbAction)nextTrade.Action;
                trade.Side = (DbSide)nextTrade.Side;
                trade.Flags = (DbFlagSet)nextTrade.Flags;
                trade.Depth = nextTrade.Depth;
                trade.Sequence = nextTrade.Sequence;
            }

            TradesResponse.Invoke(id, lastGroup == BooleanEnum.True, totalCount, trades);
        }

        private void DecodeAddRemoveMultipleTradesRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AddRemoveMultipleTradesRequest == null)
            {
                return;
            }

            AddRemoveMultipleTradesRequestMessage message = new AddRemoveMultipleTradesRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            List<string> permIds = new();
            AddRemoveMultipleTradesRequestMessage.PermIdsGroup permIdsGroup = message.PermIds;
            while (permIdsGroup.HasNext)
            {
                AddRemoveMultipleTradesRequestMessage.PermIdsGroup permId = permIdsGroup.Next();
                permIds.Add(permId.GetPermId());
            }
            var add = message.Add == BooleanEnum.True;

            AddRemoveMultipleTradesRequest?.Invoke(add, permIds);
        }

        private void DecodeMultipleContrapartyReportsAdded(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_orderFactory == null)
                return;

            MultipleContrapartyReportsAdded message = new MultipleContrapartyReportsAdded();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var targetDate = message.TargetDate.FromUnixEpoch();
            List<ContraPartyReportModel> reports = [];
            MultipleContrapartyReportsAdded.NoReportsGroup reportsGroup = message.NoReports;
            while (reportsGroup.HasNext)
            {
                MultipleContrapartyReportsAdded.NoReportsGroup report = reportsGroup.Next();
                var tradeDate = DateOnly.FromDateTime(report.TradeDate.FromUnixEpoch().Date);
                var contrapartyReport = new ContraPartyReportModel()
                {
                    Account = report.GetAccount(),
                    ExecutionTime = report.ExecutionTime.FromUnixEpoch(),
                    ClOrdID = report.GetClOrdID(),
                    OCCID = report.GetOCCID(),
                    TradeDate = tradeDate == DateOnly.MinValue ? null : tradeDate,
                    Side = report.GetSide(),
                    Quantity = report.Quantity,
                    Price = DecodePriceNull3(report.Price),
                    Symbol = report.GetSymbol(),
                    RQDClOrdID = report.GetRQDClOrdID(),
                    ContraClearingFirm = report.ContraClearingFirm,
                    ContraOpenClose = report.GetContraOpenClose(),
                    ContraAccountType = report.GetContraAccountType(),
                    MarketMakerSubAccountCode = report.GetMarketMakerSubAccountCode(),
                    TheirExtraText = report.GetTheirExtraText(),
                    TheirClientOrderID = report.GetTheirClientOrderID(),
                    TheirBrokerID = report.GetTheirBrokerID(),
                    Exchange = report.GetExchange(),
                    LiquidityIndicator = report.GetLiquidityIndicator()
                };
                reports.Add(contrapartyReport);
            }
            _orderFactory.MultipleContrapartyReportsAdded(targetDate, reports);
        }

        private void DecodeAutoTraderConfigBinaryMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AutoTraderConfig == null || AutoTraderConfigRequestHandler == null)
            {
                return;
            }

            AutoTraderConfigBinaryMessage message = new AutoTraderConfigBinaryMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var configId = message.GetConfigId();
            var config = AutoTraderConfigRequestHandler.Invoke(configId);

            if (config == null)
            {
                return;
            }

            config.ConfigId = configId;
            config.UserId = message.UserId;
            config.RiskCheckId = message.RiskCheckId;
            config.RiskCheckPassed = message.RiskCheckPassed == BooleanEnum.True;
            config.Sequence = message.Sequence;
            config.Venue = (Venue)message.Venue;
            config.EdgeType = (EdgeType)message.EdgeType;
            config.EdgeValue = message.EdgeValue;
            config.TheoModel = (TheoModel)message.TheoModel;
            config.FishLossTheoModel = (TheoModel)message.FishLossTheoModel;
            config.AutoCancelTheoModel = (TheoModel)message.AutoCancelTheoModel;
            config.ForMarketCrossPriceUseSweepEnabled = message.ForMarketCrossPriceUseSweepEnabled == BooleanEnum.True;
            config.CancelWithMaxSizeEnabled = message.CancelWithMaxSizeEnabled == BooleanEnum.True;
            config.CancelWithOrderPriceEdgeToTheoEnabled = message.CancelWithOrderPriceEdgeToTheoEnabled == BooleanEnum.True;
            config.CancelWithOrderPriceEdgeToModelTheoEnabled = message.CancelWithOrderPriceEdgeToModelTheoEnabled == BooleanEnum.True;
            config.CancelWithTimerEnabled = message.CancelWithTimerEnabled == BooleanEnum.True;
            config.CancelWithEdgeToTheoEnabled = message.CancelWithEdgeToTheoEnabled == BooleanEnum.True;
            config.CancelWithEdgeToAdjTheoEnabled = message.CancelWithEdgeToAdjTheoEnabled == BooleanEnum.True;
            config.CancelWithChangeInUnderlyingPxEnabled = message.CancelWithChangeInUnderlyingPxEnabled == BooleanEnum.True;
            config.CancelWithChangeInUnderlyingDeltaPxEnabled = message.CancelWithChangeInUnderlyingDeltaPxEnabled == BooleanEnum.True;
            config.CancelWithEdgeToMidEnabled = message.CancelWithEdgeToMidEnabled == BooleanEnum.True;
            config.CancelWithChangeInWidthEnabled = message.CancelWithChangeInWidthEnabled == BooleanEnum.True;
            config.CancelWithMaxWidthEnabled = message.CancelWithMaxWidthEnabled == BooleanEnum.True;
            config.CancelWithMaxSizeLimit = message.CancelWithMaxSizeLimit;
            config.CancelWithOrderPriceEdgeToTheo = message.CancelWithOrderPriceEdgeToTheo;
            config.CancelWithOrderPriceEdgeToModelTheo = message.CancelWithOrderPriceEdgeToModelTheo;
            config.CancelWithTimer = message.CancelWithTimer;
            config.CancelWithTheoEdge = message.CancelWithTheoEdge;
            config.CancelWithAdjTheoEdge = message.CancelWithAdjTheoEdge;
            config.CancelWithUnderlyingPxThreshold = message.CancelWithUnderlyingPxThreshold;
            config.CancelWithUnderlyingDeltaPx = message.CancelWithUnderlyingDeltaPx;
            config.CancelWithMidEdge = message.CancelWithMidEdge;
            config.CancelWithWidthThreshold = message.CancelWithWidthThreshold;
            config.CancelWithMaxWidthThreshold = message.CancelWithMaxWidthThreshold;
            config.MinEdgeToTheoCheckEnabled = message.MinEdgeToTheoCheckEnabled == BooleanEnum.True;
            config.MinEdgeToHwTheoCheckEnabled = message.MinEdgeToHwTheoCheckEnabled == BooleanEnum.True;
            config.MinEdgeToV0TheoCheckEnabled = message.MinEdgeToV0TheoCheckEnabled == BooleanEnum.True;
            config.MinEdgeToMidCheckEnabled = message.MinEdgeToMidCheckEnabled == BooleanEnum.True;
            config.MinEdgeToEmaCheckEnabled = message.MinEdgeToEmaCheckEnabled == BooleanEnum.True;
            config.MinEdgeToMarketCheckEnabled = message.MinEdgeToMarketCheckEnabled == BooleanEnum.True;
            config.MinBidPercentCheckEnabled = message.MinBidPercentCheckEnabled == BooleanEnum.True;
            config.MaxBidPercentCheckEnabled = message.MaxBidPercentCheckEnabled == BooleanEnum.True;
            config.MinBidAskSizeCheckEnabled = message.MinBidAskSizeCheckEnabled == BooleanEnum.True;
            config.MinEmaWidthPercentEdgeToTheoCheckEnabled = message.MinEmaWidthPercentEdgeToTheoCheckEnabled == BooleanEnum.True;
            config.MinBidCheckEnabled = message.MinBidCheckEnabled == BooleanEnum.True;
            config.MinTheoCheckEnabled = message.MinTheoCheckEnabled == BooleanEnum.True;
            config.MinEdgeToTheo = message.MinEdgeToTheo;
            config.MinEdgeToHwTheo = message.MinEdgeToHwTheo;
            config.MinEdgeToV0Theo = message.MinEdgeToV0Theo;
            config.MinEdgeToMid = message.MinEdgeToMid;
            config.MinEdgeToEma = message.MinEdgeToEma;
            config.MinEdgeToMarket = message.MinEdgeToMarket;
            config.MinBidPercent = message.MinBidPercent;
            config.MaxBidPercent = message.MaxBidPercent;
            config.MinBidAskSize = message.MinBidAskSize;
            config.MinEmaWidthPercentEdgeToTheoCheckEdge = message.MinEmaWidthPercentEdgeToTheoCheckEdge;
            config.MinBidCheckBidValue = message.MinBidCheckBidValue;
            config.MinTheoCheckTheoValue = message.MinTheoCheckTheoValue;
            config.EdgeToAdjTheoWithOverrideUsePercentage = message.EdgeToAdjTheoWithOverrideUsePercentage == BooleanEnum.True;
            config.EdgeToAdjTheoWithOverrideStatic = message.EdgeToAdjTheoWithOverrideStatic;
            config.EdgeToAdjTheoWithOverridePercent = message.EdgeToAdjTheoWithOverridePercent;
            config.CheckForRecentAttempt = message.CheckForRecentAttempt == BooleanEnum.True;
            config.CheckForRecentAttemptTimespan = message.CheckForRecentAttemptTimespan;
            config.CheckForRecentFill = message.CheckForRecentFill == BooleanEnum.True;
            config.CheckForRecentFillTimespan = message.CheckForRecentFillTimespan;
            config.MinSpxAuction = message.MinSpxAuction;
            config.MinSpxSpreadAuction = message.MinSpxSpreadAuction;
            config.MinSingleLegAuction = message.MinSingleLegAuction;
            config.MinSpreadAuction = message.MinSpreadAuction;

            config.SweepRoute = message.GetSweepRoute();

            config.BestOfAdjTheoEnabled = message.BestOfAdjTheoEnabled == BooleanEnum.True;
            config.BestOfAdjTheoEdge = message.BestOfAdjTheoEdge;
            config.BestOfAdjTheoModel = message.BestOfAdjTheoModel;
            config.BestOfHwTheoEnabled = message.BestOfHwTheoEnabled == BooleanEnum.True;
            config.BestOfHwTheoEdge = message.BestOfHwTheoEdge;
            config.BestOfV0TheoEnabled = message.BestOfV0TheoEnabled == BooleanEnum.True;
            config.BestOfV0TheoEdge = message.BestOfV0TheoEdge;
            config.BestOfMidEnabled = message.BestOfMidEnabled == BooleanEnum.True;
            config.BestOfMidEdge = message.BestOfMidEdge;
            config.BestOfEmaEnabled = message.BestOfEmaEnabled == BooleanEnum.True;
            config.BestOfEmaEdge = message.BestOfEmaEdge;
            config.BestOfBidPercentEnabled = message.BestOfBidPercentEnabled == BooleanEnum.True;
            config.BestOfBidPercentEdge = message.BestOfBidPercentEdge;
            config.BestOfDigBidPercentEnabled = message.BestOfDigBidPercentEnabled == BooleanEnum.True;
            config.BestOfDigBidPercentEdge = message.BestOfDigBidPercentEdge;
            config.MaxDigBidPercentCheckEnabled = message.MaxDigBidPercentCheckEnabled == BooleanEnum.True;
            config.MaxDigBidPercent = message.MaxDigBidPercent;

            config.AutoPermEnabled = message.AutoPermEnabled == BooleanEnum.True;
            config.AutoPermMinEdge = message.AutoPermMinEdge;
            config.AutoPermOrderCount = message.AutoPermOrderCount;
            config.AutoPermMaxGeneration = message.AutoPermMaxGeneration;
            config.AutoPermSubmissionStyle = message.AutoPermSubmissionStyleInActingVersion()
                ? (AutoPermSubmissionStyle)message.AutoPermSubmissionStyle
                : AutoPermSubmissionStyle.Sequential;
            config.AutoPermOrderInitialSize = message.AutoPermOrderInitialSizeInActingVersion()
                ? message.AutoPermOrderInitialSize
                : 1;

            AutoTraderConfigBinaryMessage.AutomationConfigsGroup? automationConfigs = message.AutomationConfigs;
            config.UnderlyingToAutomationConfigs.Clear();
            while (automationConfigs.HasNext)
            {
                AutoTraderConfigBinaryMessage.AutomationConfigsGroup? automationConfigMessage = automationConfigs.Next();
                AutomationConfig automationConfig = new AutomationConfig();
                var isDefault = automationConfigMessage.IsDefault == BooleanEnum.True;
                var underlying = automationConfigMessage.GetUnderlyingSymbol();
                var increment = automationConfigMessage.Increment;
                if (underlying != null)
                {
                    automationConfig.ConfigKey = new ConfigKey()
                    {
                        Underlying = underlying,
                        Increment = increment,
                    };
                }

                automationConfig.LoopingEnabled = automationConfigMessage.LoopingEnabled == BooleanEnum.True;
                automationConfig.CloseEdgeType = (SelectionType)automationConfigMessage.CloseEdgeType;
                automationConfig.StaticCloseEdge = automationConfigMessage.StaticCloseEdge;
                automationConfig.StaticMinLoopEdge = automationConfigMessage.StaticMinLoopEdge;
                automationConfig.StaticMaxLoss = automationConfigMessage.StaticMaxLoss;
                automationConfig.LooperDynamicRouting = automationConfigMessage.LooperDynamicRouting == BooleanEnum.True;
                automationConfig.AttemptIncrementUsingDynamicRoute = automationConfigMessage.AttemptIncrementUsingDynamicRoute == BooleanEnum.True;
                automationConfig.EnableDynamicRouteForOpeningOrders = automationConfigMessage.EnableDynamicRouteForOpeningOrders == BooleanEnum.True;
                automationConfig.EnableDynamicRouteForClosingOrders = automationConfigMessage.EnableDynamicRouteForClosingOrders == BooleanEnum.True;
                automationConfig.CloseIntervalType = (SelectionType)automationConfigMessage.CloseIntervalType;
                automationConfig.StaticCloseInterval = automationConfigMessage.StaticCloseInterval;
                automationConfig.StaticCloseIntervalMax = automationConfigMessage.StaticCloseIntervalMax;
                automationConfig.StaticLoopInterval = automationConfigMessage.StaticLoopInterval;
                automationConfig.StaticLoopIntervalMax = automationConfigMessage.StaticLoopIntervalMax;
                automationConfig.IncrementType = (SelectionType)automationConfigMessage.IncrementType;
                automationConfig.StaticIncrement = automationConfigMessage.StaticIncrement;
                automationConfig.SizeUpType = (SelectionType)automationConfigMessage.SizeUpType;
                automationConfig.StaticSizeUpLoopCountBeforeSizeup = automationConfigMessage.StaticSizeUpLoopCountBeforeSizeup;
                automationConfig.StaticSizeUp = automationConfigMessage.StaticSizeUp;
                automationConfig.AutoAggressorEnabled = automationConfigMessage.AutoAggressorEnabled == BooleanEnum.True;
                automationConfig.AutoAggressorMode = (AutoAggressorMode)automationConfigMessage.AutoAggressorMode;
                automationConfig.AutoAggressorEdgeTightenMode = (AutoAggressorEdgeTightenMode)automationConfigMessage.AutoAggressorEdgeTightenMode;
                automationConfig.AutoAggressorEdgeTightenPercentage = automationConfigMessage.AutoAggressorEdgeTightenPercentage;
                automationConfig.ScratchOnLowDeltaSize = automationConfigMessage.ScratchOnLowDeltaSize == BooleanEnum.True;
                automationConfig.ScratchOnLowDeltaMax = automationConfigMessage.ScratchOnLowDeltaMax;
                automationConfig.ScratchOnLowDeltaMaxLoss = automationConfigMessage.ScratchOnLowDeltaMaxLoss;
                automationConfig.ScratchOnLowDeltaMinSize = automationConfigMessage.ScratchOnLowDeltaMinSize;
                automationConfig.FreeLookRequireMinFillTime = automationConfigMessage.FreeLookRequireMinFillTime == BooleanEnum.True;
                automationConfig.FreeLookMinFillTime = automationConfigMessage.FreeLookMinFillTime;
                automationConfig.FreeLookOnLosers = automationConfigMessage.FreeLookOnLosers == BooleanEnum.True;
                automationConfig.FreeLookOnLosersMax = automationConfigMessage.FreeLookOnLosersMax;
                automationConfig.FreeLookOnAll = automationConfigMessage.FreeLookOnAll == BooleanEnum.True;
                automationConfig.FreeWhenGettingCloseEdge = automationConfigMessage.FreeWhenGettingCloseEdge == BooleanEnum.True;
                automationConfig.FreeLookAfterLastAttempt = automationConfigMessage.FreeLookAfterLastAttempt == BooleanEnum.True;
                automationConfig.FreeLookBackUpIncrement = automationConfigMessage.FreeLookBackUpIncrement;
                automationConfig.FreeLookOnAllWalkBackIncrement = automationConfigMessage.FreeLookOnAllWalkBackIncrement;
                automationConfig.LoopFreeLookOnAllUsingTicks = automationConfigMessage.LoopFreeLookOnAllUsingTicks == BooleanEnum.True;
                automationConfig.FreeLookOnAllIncrementTicks = automationConfigMessage.FreeLookOnAllIncrementTicks;
                automationConfig.FreeLookOnAllWalkBackIncrementTicks = automationConfigMessage.FreeLookOnAllWalkBackIncrementTicks;
                automationConfig.LoopFreeLookOnNickelNames = automationConfigMessage.LoopFreeLookOnNickelNames == BooleanEnum.True;
                automationConfig.LoopFreeLookOnNickelNamesIncrement = automationConfigMessage.LoopFreeLookOnNickelNamesIncrement;
                automationConfig.LoopFreeLookOnDimeNames = automationConfigMessage.LoopFreeLookOnDimeNames == BooleanEnum.True;
                automationConfig.LoopFreeLookOnDimeNamesIncrement = automationConfigMessage.LoopFreeLookOnDimeNamesIncrement;
                automationConfig.MaintainLastEdge = automationConfigMessage.MaintainLastEdge == BooleanEnum.True;
                automationConfig.AttemptResubmitCount = automationConfigMessage.AttemptResubmitCount;
                automationConfig.LastFillResubmitCount = automationConfigMessage.LastFillResubmitCount;
                automationConfig.MaxNumberOfLoops = automationConfigMessage.MaxNumberOfLoops;
                automationConfig.PartialFillPercentage = automationConfigMessage.PartialFillPercentage;
                automationConfig.PartialFillResubmit = automationConfigMessage.PartialFillResubmit;
                automationConfig.LoopPricingMode = (LoopPricingMode)automationConfigMessage.LoopPricingMode;
                automationConfig.AdjustClosingPriceToMarketWinnersOnly = automationConfigMessage.AdjustClosingPriceToMarketWinnersOnly == BooleanEnum.True;
                automationConfig.PxCrossOption = (PxCrossOption)automationConfigMessage.PxCrossOption;
                automationConfig.ClosePxCrossOption = (PxCrossOption)automationConfigMessage.ClosePxCrossOption;
                automationConfig.AutoHedgeOnClose = automationConfigMessage.AutoHedgeOnClose == BooleanEnum.True;
                automationConfig.AutoHedgeOnCloseSizeOnly = automationConfigMessage.AutoHedgeOnCloseSizeOnly == BooleanEnum.True;
                automationConfig.MinHedgeHouseEdge = automationConfigMessage.MinHedgeHouseEdge;
                automationConfig.AutoHedgeOnFailure = automationConfigMessage.AutoHedgeOnFailure == BooleanEnum.True;
                automationConfig.AutoHedgePartial = automationConfigMessage.AutoHedgePartial == BooleanEnum.True;
                automationConfig.AutoLegEnabled = automationConfigMessage.AutoLegEnabled == BooleanEnum.True;
                automationConfig.AutoLegMaxWidth = automationConfigMessage.AutoLegMaxWidth;
                automationConfig.AutoLegCloseEdge = automationConfigMessage.AutoLegCloseEdge;
                automationConfig.AutoLegMaxLoss = automationConfigMessage.AutoLegMaxLoss;
                automationConfig.AutoLegCloseIncrement = automationConfigMessage.AutoLegCloseIncrement;
                automationConfig.AutoLegRestTime = automationConfigMessage.AutoLegRestTime;
                automationConfig.OpenRoute = automationConfigMessage.GetOpenRoute();
                automationConfig.CloseRoute = automationConfigMessage.GetCloseRoute();
                automationConfig.OpenRouteSingleLeg = automationConfigMessage.GetOpenRouteSingleLeg();
                automationConfig.CloseRouteSingleLeg = automationConfigMessage.GetCloseRouteSingleLeg();
                automationConfig.OpenRouteSize = automationConfigMessage.GetOpenRouteSize();
                automationConfig.CloseRouteSize = automationConfigMessage.GetCloseRouteSize();
                automationConfig.OpenRouteSingleLegSize = automationConfigMessage.GetOpenRouteSingleLegSize();
                automationConfig.CloseRouteSingleLegSize = automationConfigMessage.GetCloseRouteSingleLegSize();
                automationConfig.LoopFreeLookOnNickelNamesRoute = automationConfigMessage.GetLoopFreeLookOnNickelNamesRoute();
                automationConfig.LoopFreeLookOnDimeNamesRoute = automationConfigMessage.GetLoopFreeLookOnDimeNamesRoute();
                automationConfig.AutoLegCloseRoute = automationConfigMessage.GetAutoLegCloseRoute();

                automationConfig.DynamicCloseEdge ??= new();
                automationConfig.DynamicCloseEdge.PercentBidRangeEnabled =
                    automationConfigMessage.DynamicEdgePercentBidRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.BaseEdgeEnabled =
                    automationConfigMessage.DynamicEdgeBaseEdgeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.EmaRangeEnabled =
                    automationConfigMessage.DynamicEdgeEmaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.TradePxRangeEnabled =
                    automationConfigMessage.DynamicEdgeTradePxRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.MinMarketWidthEnabled =
                    automationConfigMessage.DynamicEdgeMinMarketWidthEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.MinMarketCrossEnabled =
                    automationConfigMessage.DynamicEdgeMinMarketCrossEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.TheoRangeEnabled =
                    automationConfigMessage.DynamicEdgeTheoRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.VolaRangeEnabled =
                    automationConfigMessage.DynamicEdgeVolaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.VolaModel =
                    (TheoModel)automationConfigMessage.DynamicEdgeVolaModel;
                automationConfig.DynamicCloseEdge.DynamicVolaRangeEnabled =
                    automationConfigMessage.DynamicEdgeDynamicVolaRangeEnabled == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.DynamicVolaModel =
                    (TheoModel)automationConfigMessage.DynamicEdgeDynamicVolaModel;
                automationConfig.DynamicCloseEdge.DynamicLookupMode =
                    automationConfigMessage.DynamicEdgeDynamicLookupMode == BooleanEnum.True;
                automationConfig.DynamicCloseEdge.UnderDivisor = automationConfigMessage.DynamicEdgeUnderDivisor;

                automationConfig.DynamicCloseEdge.DteTable?.Clear();
                automationConfig.DynamicCloseEdge.DynamicDteTable?.Clear();
                var dteConfigs = automationConfigMessage.DteConfigs;
                while (dteConfigs.HasNext)
                {
                    var dteConfigMessage = dteConfigs.Next();
                    var isDynamic = dteConfigMessage.IsDynamic == BooleanEnum.True;

                    DaysToExpirationEdgeModel dteConfig = new DaysToExpirationEdgeModel();
                    dteConfig.Active = dteConfigMessage.Active == BooleanEnum.True;
                    dteConfig.DaysToExpiration = dteConfigMessage.DaysToExpiration;
                    dteConfig.MinBidAskSize = dteConfigMessage.MinBidAskSize;
                    dteConfig.MinIncrement = dteConfigMessage.MinIncrement;
                    dteConfig.MinWidth = dteConfigMessage.MinWidth;
                    dteConfig.MinSpacingForVertical = dteConfigMessage.MinSpacingForVertical;
                    dteConfig.MinSpacingForFlys = dteConfigMessage.MinSpacingForFlys;
                    dteConfig.MinSpacingForVerticalPercentage = dteConfigMessage.MinSpacingForVerticalPercentage;
                    dteConfig.MinSpacingForFlysPercentage = dteConfigMessage.MinSpacingForFlysPercentage;
                    dteConfig.BaseEdge = dteConfigMessage.BaseEdge;
                    dteConfig.CloseEdge = dteConfigMessage.CloseEdge;
                    dteConfig.LoopMinEdge = dteConfigMessage.LoopMinEdge;
                    dteConfig.AutoPermMinEdge = dteConfigMessage.AutoPermMinEdge;
                    dteConfig.VerticalQty = dteConfigMessage.VerticalQty;
                    dteConfig.Qty = dteConfigMessage.Qty;
                    dteConfig.MaxPercentBid = dteConfigMessage.MaxPercentBid;
                    dteConfig.LoopMaxLoss = dteConfigMessage.LoopMaxLoss;
                    dteConfig.AdditionalEdgePerContract = dteConfigMessage.AdditionalEdgePerContract;
                    dteConfig.AdditionalEdgePerWeightedVega = dteConfigMessage.AdditionalEdgePerWeightedVega;
                    dteConfig.MaxAllowedAboveEma = dteConfigMessage.MaxAllowedAboveEma;
                    dteConfig.MaxAllowedAboveTheo = dteConfigMessage.MaxAllowedAboveTheo;
                    dteConfig.MaxAllowedAboveVola = dteConfigMessage.MaxAllowedAboveVola;
                    dteConfig.MinMarketWidth = dteConfigMessage.MinMarketWidth;
                    dteConfig.MaxThroughTradePx = dteConfigMessage.MaxThroughTradePx;
                    dteConfig.MinMarketCross = dteConfigMessage.MinMarketCross;

                    if (!isDynamic)
                    {
                        automationConfig.DynamicCloseEdge.DteTable ??= [];
                        automationConfig.DynamicCloseEdge.DteTable.Add(dteConfig);
                    }
                    else
                    {
                        dteConfig.DynamicBaseEdge = dteConfigMessage.DynamicBaseEdge;
                        dteConfig.DynamicBaseEdgeAddition = dteConfigMessage.DynamicBaseEdgeAddition;
                        dteConfig.AdditionalEdgePerWidth = dteConfigMessage.AdditionalEdgePerWidth;
                        dteConfig.DynamicCloseEdge = dteConfigMessage.DynamicCloseEdge;
                        dteConfig.DynamicCloseEdgeAddition = dteConfigMessage.DynamicCloseEdgeAddition;
                        dteConfig.AdditionalCloseEdgePerWidth = dteConfigMessage.AdditionalCloseEdgePerWidth;
                        dteConfig.DynamicAutoPermMinEdge = dteConfigMessage.DynamicAutoPermMinEdge;
                        dteConfig.DynamicAutoPermMinEdgeAddition = dteConfigMessage.DynamicAutoPermMinEdgeAddition;
                        dteConfig.DynamicLoopMinEdge = dteConfigMessage.DynamicLoopMinEdge;
                        dteConfig.DynamicLoopMinEdgeAddition = dteConfigMessage.DynamicLoopMinEdgeAddition;
                        dteConfig.DynamicLoopMaxLoss = dteConfigMessage.DynamicLoopMaxLoss;
                        dteConfig.DynamicLoopMaxLossAddition = dteConfigMessage.DynamicLoopMaxLossAddition;
                        dteConfig.DynamicAdditionalEdgePerContract =
                            dteConfigMessage.DynamicAdditionalEdgePerContract;
                        dteConfig.DynamicAdditionalEdgePerContractAddition =
                            dteConfigMessage.DynamicAdditionalEdgePerContractAddition;
                        dteConfig.DynamicAdditionalEdgePerWeightedVega =
                            dteConfigMessage.DynamicAdditionalEdgePerWeightedVega;
                        dteConfig.DynamicAdditionalEdgePerWeightedVegaAddition =
                            dteConfigMessage.DynamicAdditionalEdgePerWeightedVegaAddition;
                        dteConfig.DynamicMaxAllowedPercentBid = dteConfigMessage.DynamicMaxAllowedPercentBid;
                        dteConfig.DynamicMaxAllowedPercentBidAddition =
                            dteConfigMessage.DynamicMaxAllowedPercentBidAddition;
                        dteConfig.DynamicMaxAllowedAboveEma = dteConfigMessage.DynamicMaxAllowedAboveEma;
                        dteConfig.DynamicMaxAllowedAboveEmaAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveEmaAddition;
                        dteConfig.DynamicMaxAllowedAboveTheo = dteConfigMessage.DynamicMaxAllowedAboveTheo;
                        dteConfig.DynamicMaxAllowedAboveTheoAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveTheoAddition;
                        dteConfig.DynamicMaxAllowedAboveVola = dteConfigMessage.DynamicMaxAllowedAboveVola;
                        dteConfig.DynamicMaxAllowedAboveVolaAddition =
                            dteConfigMessage.DynamicMaxAllowedAboveVolaAddition;
                        dteConfig.DynamicMinMarketWidth = dteConfigMessage.DynamicMinMarketWidth;
                        dteConfig.DynamicMinMarketWidthAddition = dteConfigMessage.DynamicMinMarketWidthAddition;
                        automationConfig.DynamicCloseEdge.DynamicDteTable ??= [];
                        automationConfig.DynamicCloseEdge.DynamicDteTable.Add(dteConfig);
                    }
                }

                automationConfig.DynamicCloseEdge.DeltaTable?.Clear();
                var deltaConfigs = automationConfigMessage.DeltaConfigs;
                while (deltaConfigs.HasNext)
                {
                    var deltaConfigMessage = deltaConfigs.Next();
                    DeltaEdgeModel deltaConfig = new DeltaEdgeModel();
                    deltaConfig.Active = deltaConfigMessage.Active == BooleanEnum.True;
                    deltaConfig.Delta = deltaConfigMessage.Delta;
                    deltaConfig.AdditionalEdgePerContract = deltaConfigMessage.AdditionalEdgePerContract;
                    deltaConfig.AddedEdge = deltaConfigMessage.AddedEdge;

                    automationConfig.DynamicCloseEdge.DeltaTable ??= [];
                    automationConfig.DynamicCloseEdge.DeltaTable.Add(deltaConfig);
                }
                if (automationConfigMessage.DynamicCloseEdgeEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicCloseEdge = null;
                }

                var dynamicSizeUpConfigs = automationConfigMessage.DynamicSizeUpConfigs;
                while (dynamicSizeUpConfigs.HasNext)
                {
                    var dynamicSizeUpConfig = dynamicSizeUpConfigs.Next();

                    SizeupConfigModel sizeUpConfig = new SizeupConfigModel();
                    sizeUpConfig.Enabled = dynamicSizeUpConfig.Enabled == BooleanEnum.True;
                    sizeUpConfig.Edge = dynamicSizeUpConfig.Edge;
                    sizeUpConfig.AdditionalEdgePerContract = dynamicSizeUpConfig.AdditionalEdgePerContract;
                    sizeUpConfig.MaxAbsDelta = dynamicSizeUpConfig.MaxAbsDelta;
                    sizeUpConfig.MaxUnderWidth = dynamicSizeUpConfig.MaxUnderWidth;
                    sizeUpConfig.Size = dynamicSizeUpConfig.Size;
                    sizeUpConfig.ResubmitSizeOption = (ResubmitSizeOption)dynamicSizeUpConfig.ResubmitSizeOption;
                    sizeUpConfig.RequiredLoop = dynamicSizeUpConfig.RequiredLoop;
                    sizeUpConfig.ResubmitCount = dynamicSizeUpConfig.ResubmitCount;
                    sizeUpConfig.MatchSignalQtyLimit = dynamicSizeUpConfig.MatchSignalQtyLimit;

                    automationConfig.DynamicSizeUp ??= new();
                    automationConfig.DynamicSizeUp.SizeUpConfigs ??= [];
                    automationConfig.DynamicSizeUp.SizeUpConfigs.Add(sizeUpConfig);
                }
                if (automationConfigMessage.DynamicSizeUpEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicSizeUp = null;
                }

                var dynamicCloseIntervals = automationConfigMessage.DynamicIntervalConfigs;
                while (dynamicCloseIntervals.HasNext)
                {
                    var dynamicCloseInterval = dynamicCloseIntervals.Next();

                    IntervalModel intervalModel = new IntervalModel();
                    intervalModel.Active = dynamicCloseInterval.Active == BooleanEnum.True;
                    intervalModel.MinDelta = dynamicCloseInterval.MinDelta;
                    intervalModel.MaxDelta = dynamicCloseInterval.MaxDelta;
                    intervalModel.AttemptedEdge = dynamicCloseInterval.AttemptedEdge;
                    intervalModel.Interval = dynamicCloseInterval.Interval;
                    intervalModel.ResubmitCount = dynamicCloseInterval.ResubmitCount;
                    intervalModel.Route = dynamicCloseInterval.GetRoute();
                    intervalModel.DisableRounding = dynamicCloseInterval.DisableRounding == BooleanEnum.True;

                    automationConfig.DynamicCloseInterval ??= new();
                    automationConfig.DynamicCloseInterval.IntervalTable ??= [];
                    automationConfig.DynamicCloseInterval.IntervalTable.Add(intervalModel);
                }

                if (automationConfigMessage.DynamicCloseIntervalEnabled != BooleanEnum.True)
                {
                    automationConfig.DynamicCloseInterval = null;
                }
                else
                {
                    automationConfig.DynamicCloseInterval ??= new();
                    automationConfig.DynamicCloseInterval.DefaultInterval = automationConfigMessage.DynamicIntervalDefaultInterval;
                    automationConfig.DynamicCloseInterval.DefaultResubmit = automationConfigMessage.DynamicIntervalDefaultResubmitCount;
                }

                automationConfig.ExchToRouteList?.Clear();
                var exchToRouteList = automationConfigMessage.ExchToRouteList;
                while (exchToRouteList.HasNext)
                {
                    var exchRoutePair = exchToRouteList.Next();
                    automationConfig.ExchToRouteList ??= [];
                    automationConfig.ExchToRouteList.Add(Tuple.Create(exchRoutePair.GetExch(), exchRoutePair.GetRoute()));
                }

                automationConfig.DynamicIncrement?.Clear();
                var dynamicIncrements = automationConfigMessage.DynamicIncrementConfigs;
                while (dynamicIncrements.HasNext)
                {
                    var dynamicIncrement = dynamicIncrements.Next();
                    var dynamicIncrementModel = new DynamicIncrementModel()
                    {
                        Edge = dynamicIncrement.Edge,
                        Increment = dynamicIncrement.Increment
                    };
                    automationConfig.DynamicIncrement ??= [];
                    automationConfig.DynamicIncrement.Add(dynamicIncrementModel);
                }

                if (isDefault)
                {
                    config.DefaultAutomationConfig = automationConfig;
                }
                else if (automationConfig.ConfigKey != null)
                {
                    config.UnderlyingToAutomationConfigs.Add(automationConfig);
                }
            }

            config.OpenRouteSmartMap?.Clear();
            var openRouteSmartMap = message.OpenRouteSmartMap;
            while (openRouteSmartMap.HasNext)
            {
                var routeTimerPair = openRouteSmartMap.Next();
                config.OpenRouteSmartMap ??= [];
                config.OpenRouteSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            config.CloseRouteSmartMap?.Clear();
            var CloseRouteSmartMap = message.CloseRouteSmartMap;
            while (CloseRouteSmartMap.HasNext)
            {
                var routeTimerPair = CloseRouteSmartMap.Next();
                config.CloseRouteSmartMap ??= [];
                config.CloseRouteSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            config.OpenRouteSingleLegSmartMap?.Clear();
            var openRouteSingleLegSmartMap = message.OpenRouteSingleLegSmartMap;
            while (openRouteSingleLegSmartMap.HasNext)
            {
                var routeTimerPair = openRouteSingleLegSmartMap.Next();
                config.OpenRouteSingleLegSmartMap ??= [];
                config.OpenRouteSingleLegSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            config.CloseRouteSingleLegSmartMap?.Clear();
            var CloseRouteSingleLegSmartMap = message.CloseRouteSingleLegSmartMap;
            while (CloseRouteSingleLegSmartMap.HasNext)
            {
                var routeTimerPair = CloseRouteSingleLegSmartMap.Next();
                config.CloseRouteSingleLegSmartMap ??= [];
                config.CloseRouteSingleLegSmartMap.Add(Tuple.Create(routeTimerPair.GetRoute(), routeTimerPair.Delay));
            }

            config.RiskCheckMessage = message.GetRiskCheckMessage();
            config.ConfigName = message.GetConfigName();

            AutoTraderConfig.Invoke(config);
        }

        private void DecodeTheoBatchUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (GetTheoModelsHandler == null || TheosBatchUpdated == null)
            {
                return;
            }

            TheoBatchUpdateMessage message = new TheoBatchUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var underIndex = message.UnderIndex;
            var tickerId = (underIndex[0] << 16) | (underIndex[1] << 8) | underIndex[2];
            var timestamp = message.Timestamp;
            var list = GetTheoModelsHandler(tickerId);
            if (list == null)
            {
                return;
            }
            TheoBatchUpdateMessage.UpdatesGroup updates = message.Updates;
            while (updates.HasNext)
            {
                var update = updates.Next();
                var model = list[update.OptionIndex];
                model.Theo = DecodeDoubleNull2(update.HanweckTheo);
                model.DeltaAdjustedTheo = DecodeDoubleNull2(update.HanweckAdjTheo);
                model.VolaTheoResult.Theo = DecodeDoubleNull2(update.VolaTheo);
                model.VolaTheoResult.DeltaAdjustedTheo = DecodeDoubleNull2(update.VolaAdjTheo);
                model.Delta = DecodeDoubleNull4(update.Delta);
                model.VolaTheoResult.Underlying = DecodeDoubleNull2(update.VolaUnderlyingSpot);
                model.VolaTheoResult.SnapshotUnderlying = DecodeDoubleNull2(update.VolaUnderlyingSnap);
                model.VolaTheoResult.Delta = DecodeDoubleNull4(update.VolaDelta);
                model.SnapshotTicks = update.SnapshotTicks;
                model.VolaTheoResult.Iv = update.VolaIv;
            }
            TheosBatchUpdated.Invoke(timestamp, list);
        }

        private void DecodeAdjTheoBatchUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (GetTheoModelsHandler == null || TheosBatchUpdated == null)
            {
                return;
            }

            AdjTheoBatchUpdateMessage message = new AdjTheoBatchUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var underIndex = message.UnderIndex;
            var tickerId = (underIndex[0] << 16) | (underIndex[1] << 8) | underIndex[2];
            var timestamp = message.Timestamp;
            var list = GetTheoModelsHandler(tickerId);
            if (list == null)
            {
                return;
            }
            AdjTheoBatchUpdateMessage.UpdatesGroup updates = message.Updates;
            while (updates.HasNext)
            {
                var update = updates.Next();
                var model = list[update.OptionIndex];
                model.DeltaAdjustedTheo = DecodeDoubleNull2(update.HanweckAdjTheo);
                model.VolaTheoResult.DeltaAdjustedTheo = DecodeDoubleNull2(update.VolaAdjTheo);
            }
            TheosBatchUpdated!.Invoke(timestamp, list);
        }

        private void DecodeZpTheoUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null)
            {
                return;
            }

            ZpTheoUpdateMessage message = new ZpTheoUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var tickerId = (message.TickerId[0] << 16) | (message.TickerId[1] << 8) | message.TickerId[2];
            var theoBid = DecodePriceNull3(message.TheoBid);
            var theoAsk = DecodePriceNull3(message.TheoAsk);

            _updateManager.HandleUpdate(tickerId,
                                        SubscriptionFieldType.ZpTheo,
                                        theoBid,
                                        theoAsk,
                                        DateTime.UtcNow,
                                        QuoteChangeType.None,
                                        QuoteChangeType.None,
                                        0,
                                        0,
                                        double.NaN);
        }

        private void DecodeSingleFieldUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (_updateManager == null && SingleFieldUpdate == null)
            {
                return;
            }

            SingleFieldUpdateMessage message = new SingleFieldUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var tickerId = message.TickerId;
            var updateType = (SubscriptionFieldType)message.UpdateType;
            var value = message.Value;

            _updateManager?.HandleUpdate(tickerId, updateType, value);
            SingleFieldUpdate?.Invoke(tickerId, updateType, value);
        }

        private void DecodeOpenSpreadExchOrderMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OpenSpreadExchOrder == null)
            {
                return;
            }

            OpenSpreadExchOrderMessage message = new OpenSpreadExchOrderMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var underlying = message.GetUnderlying();
            var orderId = message.GetOrderID();

            IOpenSpreadExchOrder? model = GetOpenSpreadExchOrderHandler?.Invoke(underlying, orderId);
            if (model == null)
            {
                return;
            }

            model.FlipSide = message.FlipSide == BooleanEnum.True;

            model.Exch = (SrExch)message.Exch;

            model.OrigOrderSize = message.OrigOrderSize;
            model.OrderSize = message.OrderSize;
            model.Price = message.Price;
            model.Timestamp = message.Timestamp.FromUnixEpoch();

            model.Underlying = underlying;
            model.OrderID = orderId;
            model.Symbol = message.GetSpreadSymbol();

            OpenSpreadExchOrder?.Invoke(model);
        }

        private void DecodeRemoveSpreadExchOrderMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (RemoveSpreadExchOrder == null)
            {
                return;
            }

            RemoveSpreadExchOrderMessage message = new RemoveSpreadExchOrderMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            RemoveSpreadExchOrder?.Invoke(message.GetUnderlying(), message.GetOrderID());
        }

        private void DecodeVolSurfaceRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (VolSurfaceRequest == null)
            {
                return;
            }

            Generated.VolSurfaceRequest message = new Generated.VolSurfaceRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            VolSurfaceRequestModel model = new VolSurfaceRequestModel
            {
                RequestId = message.RequestID,
                RequestTime = message.RequestTime.FromUnixEpoch(),
                SymbolId = message.SymbolId,
                TenorIndex = message.TenorIndex
            };

            VolSurfaceRequest?.Invoke(model);
        }

        private void DecodeVolSurfaceResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (VolSurfaceResponse == null)
            {
                return;
            }

            Generated.VolSurfaceResponse message = new Generated.VolSurfaceResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            VolSurfaceResponseModel model = new VolSurfaceResponseModel
            {
                RequestId = message.RequestID,
                Success = message.Success == BooleanEnum.True,
                MarketDataSnapshotTime = new DateTime(message.MarketDataSnapshotTime)
            };

            // Decode VolaCurvePoints group
            var volaCurvePointsGroup = message.VolaCurvePoints;
            volaCurvePointsGroup.WrapForDecode(message, directBuffer, actingVersion);
            while (volaCurvePointsGroup.HasNext)
            {
                volaCurvePointsGroup.Next();
                model.VolaCurvePoints.Add(new VolaCurvePointModel
                {
                    NormalizedStrike = volaCurvePointsGroup.NormalizedStrike,
                    Volatility = volaCurvePointsGroup.Volatility,
                    Strike = volaCurvePointsGroup.Strike,
                    TheoPrice = volaCurvePointsGroup.TheoPrice
                });
            }

            // Decode PutsMarketData group
            var putsMarketDataGroup = message.PutsMarketData;
            putsMarketDataGroup.WrapForDecode(message, directBuffer, actingVersion);
            while (putsMarketDataGroup.HasNext)
            {
                putsMarketDataGroup.Next();
                model.PutsMarketData.Add(new PutMarketDataModel
                {
                    NormalizedStrike = putsMarketDataGroup.NormalizedStrike,
                    BidIV = putsMarketDataGroup.PutBidIV,
                    AskIV = putsMarketDataGroup.PutAskIV,
                    Bid = putsMarketDataGroup.PutBid,
                    Ask = putsMarketDataGroup.PutAsk
                });
            }

            // Decode CallsMarketData group
            var callsMarketDataGroup = message.CallsMarketData;
            callsMarketDataGroup.WrapForDecode(message, directBuffer, actingVersion);
            while (callsMarketDataGroup.HasNext)
            {
                callsMarketDataGroup.Next();
                model.CallsMarketData.Add(new CallMarketDataModel
                {
                    NormalizedStrike = callsMarketDataGroup.NormalizedStrike,
                    BidIV = callsMarketDataGroup.CallBidIV,
                    AskIV = callsMarketDataGroup.CallAskIV,
                    Bid = callsMarketDataGroup.CallBid,
                    Ask = callsMarketDataGroup.CallAsk
                });
            }

            VolSurfaceResponse?.Invoke(model);
        }

        private void DecodeHerculesEchoRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OnHerculesEchoRequestMessage == null)
            {
                return;
            }

            HerculesEchoRequestMessage message = new HerculesEchoRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var echoRequest = message.RequestEcho == BooleanEnum.True;

            OnHerculesEchoRequestMessage?.Invoke(echoRequest);
        }

        private void DecodeHerculesEchoMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OnHerculesEchoMessage == null)
            {
                return;
            }

            HerculesEchoMessage message = new HerculesEchoMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            string orderId = message.GetPermID();
            bool isComplex = message.IsComplexOrder == BooleanEnum.True;
            IOrder? model = _orderFactory?.GetOrder(isComplex: isComplex, orderId: orderId);

            if (model == null)
            {
                return;
            }

            model.PermID = orderId;

            model.PartiallyFilled = message.PartiallyFilled == BooleanEnum.True;
            model.IsFirstFill = message.IsFirstFill == BooleanEnum.True;
            model.ExecutionType = (ExecutionType)message.ExecutionType;

            model.LastQuantity = message.LastQuantity;
            model.FilledQty = message.FilledQty;
            model.LeavesQuantity = message.LeavesQuantity;
            model.CumulativeQuantity = message.CumulativeQuantity;
            model.Quantity = message.Quantity;

            model.SpreadAvgPrice = DecodeDoubleNull2(message.SpreadAvgPrice);
            model.AveragePrice = DecodeDoubleNull2(message.AveragePrice);
            model.Price = DecodeDoubleNull2(message.Price);
            model.LastPrice = DecodeDoubleNull2(message.LastPrice);
            model.MinPrice = DecodeDoubleNull2(message.MinPrice);
            model.MaxPrice = DecodeDoubleNull2(message.MaxPrice);
            model.TagEdge = DecodeDoubleNull2(message.TagEdge);
            model.TagMid = DecodeDoubleNull2(message.TagMid);
            model.TagBid = DecodeDoubleNull2(message.TagBid);
            model.TagAsk = DecodeDoubleNull2(message.TagAsk);
            model.TagTheo = DecodeDoubleNull2(message.TagTheo);
            model.TagVolaV0 = DecodeDoubleNull2(message.TagVolaV0);
            model.TagVolaV1 = DecodeDoubleNull2(message.TagVolaV1);
            model.TagVolaV2 = DecodeDoubleNull2(message.TagVolaV2);
            model.VolaIv = message.TagVolaIv;
            model.TheoBid = DecodeDoubleNull2(message.TheoBid);
            model.TheoAsk = DecodeDoubleNull2(message.TheoAsk);
            model.TagEma = DecodeDoubleNull2(message.TagEma);
            model.Fee1 = DecodeDoubleNull2(message.Fee1);
            model.Fee2 = DecodeDoubleNull2(message.Fee2);
            model.Bid = DecodeDoubleNull2(message.Bid);
            model.Ask = DecodeDoubleNull2(message.Ask);
            model.UnderBid = DecodeDoubleNull2(message.UnderBid);
            model.UnderAsk = DecodeDoubleNull2(message.UnderAsk);
            model.TV = DecodeDoubleNull2(message.TV);
            model.Delta = DecodeDoubleNull4(message.Delta);
            model.ExchangeFee1 = DecodeDoubleNull2(message.ExchangeFee1);
            model.ExchangeFee2 = DecodeDoubleNull2(message.ExchangeFee2);
            model.BrokerFee1 = DecodeDoubleNull2(message.BrokerFee1);
            model.BrokerFee2 = DecodeDoubleNull2(message.BrokerFee2);
            model.TotalContracts = DecodeDoubleNull2(message.TotalContracts);
            model.FillTime = DecodeDoubleNull2(message.FillTime);
            model.TradeToNewTime = DecodeDoubleNull2(message.TradeToNewTime);
            model.SubmitToNewTime = DecodeDoubleNull2(message.SubmitToNewTime);
            model.NewToCancelTime = DecodeDoubleNull2(message.NewToCancelTime);
            model.BidPercentOfFillPrice = DecodeDoubleNull2(message.BidPercentOfFillPrice);
            model.OmsBidPercentOfFillPrice = DecodeDoubleNull2(message.OmsBidPercentOfFillPrice);
            model.TotalDelta = DecodeDoubleNull4(message.TotalDelta);
            model.HanweckTotalTheo = DecodeDoubleNull2(message.HanweckTotalTheo);
            model.HanweckTotalGamma = DecodeDoubleNull4(message.HanweckTotalGamma);
            model.HanweckTotalVega = DecodeDoubleNull4(message.HanweckTotalVega);
            model.HanweckTotalTheta = DecodeDoubleNull4(message.HanweckTotalTheta);
            model.HanweckTotalRho = DecodeDoubleNull4(message.HanweckTotalRho);
            model.HanweckTotalIV = DecodeDoubleNull4(message.HanweckTotalIV);
            model.HanweckTotalUnder = DecodeDoubleNull2(message.HanweckTotalUnder);
            model.HanweckTotalUBid = DecodeDoubleNull2(message.HanweckTotalUBid);
            model.HanweckTotalUAsk = DecodeDoubleNull2(message.HanweckTotalUAsk);
            model.HanweckTotalBid = DecodeDoubleNull2(message.HanweckTotalBid);
            model.HanweckTotalAsk = DecodeDoubleNull2(message.HanweckTotalAsk);
            model.EdgeOverride = DecodeDoubleNull2(message.EdgeOverride);
            model.AdjustedEdgeOverride = DecodeDoubleNull2(message.AdjustedEdgeOverride);
            model.EdgeToTheo = DecodeDoubleNull2(message.EdgeToTheo);
            model.TagEdgeToTheo = DecodeDoubleNull2(message.TagEdgeToTheo);
            model.TagEdgeToEma = DecodeDoubleNull2(message.TagEdgeToEma);
            model.TagEdgeToVolaV0 = DecodeDoubleNull2(message.TagEdgeToVolaV0);
            model.TagEdgeToVolaV1 = DecodeDoubleNull2(message.TagEdgeToVolaV1);
            model.TagEdgeToVolaV2 = DecodeDoubleNull2(message.TagEdgeToVolaV2);
            model.TagBestBid = DecodeDoubleNull2(message.TagBestBid);
            model.TagBestAsk = DecodeDoubleNull2(message.TagBestAsk);
            model.TagMktMkrBid = DecodeDoubleNull2(message.TagMktMkrBid);
            model.TagMktMkrAsk = DecodeDoubleNull2(message.TagMktMkrAsk);
            model.InitialEdge = DecodeDoubleNull2(message.InitialEdge);
            model.OpenEdge = DecodeDoubleNull2(message.OpenEdge);
            model.CloseEdge = DecodeDoubleNull2(message.CloseEdge);
            model.LastEdge = DecodeDoubleNull2(message.LastEdge);
            model.DeltaAdjLastEdge = DecodeDoubleNull2(message.DeltaAdjLastEdge);
            model.DeltaAdjLastEdgeNotional = DecodeDoubleNull2(message.DeltaAdjLastEdgeNotional);
            model.EdgeScanFeedDeltaAdjPrice = DecodeDoubleNull2(message.EdgeScanFeedDeltaAdjPrice);
            model.DeltaAdjChange = DecodeDoubleNull2(message.DeltaAdjChange);
            model.DeltaAdjChangeNotional = DecodeDoubleNull2(message.DeltaAdjChangeNotional);
            model.EdgeScanFeedEdge = DecodeDoubleNull2(message.EdgeScanFeedEdge);
            model.EdgeScanFeedTimespan = DecodeDoubleNull2(message.EdgeScanFeedTimespan);
            model.EdgeScanFeedBuyPrice = DecodeDoubleNull2(message.EdgeScanFeedBuyPrice);
            model.EdgeScanFeedBuyQty = message.EdgeScanFeedBuyQty;
            model.EdgeScanFeedSellPrice = DecodeDoubleNull2(message.EdgeScanFeedSellPrice);
            model.EdgeScanFeedSellQty = message.EdgeScanFeedSellQty;
            model.EdgeScanFeedBuyTime = message.EdgeScanFeedBuyTime.FromUnixEpoch();
            model.EdgeScanFeedSellTime = message.EdgeScanFeedSellTime.FromUnixEpoch();
            model.EdgeScanFeedRespondLatency = DecodeDoubleNull2(message.EdgeScanFeedRespondLatency);

            model.EdgeScanFeedConditionCode = (char)message.EdgeScanFeedConditionCode;
            model.ResubmitCount = message.ResubmitCount;
            model.TotalEstimatedResubmit = message.TotalEstimatedResubmit;

            model.Side = message.AggressorSide == AggressorSide.Buy ? Side.Buy : Side.Sell;
            model.OrderStatus = (Data.Enums.OrderStatus)message.OrderStatus;
            model.BaseStrategy = (Data.Enums.BaseStrategy)message.BaseStrategy;
            model.PositionEffect = (Data.Enums.PositionEffect)message.PositionEffect;
            model.TimeInForce = (Data.Enums.TimeInForce)message.TimeInForce;

            model.OrderSource = (Data.Enums.OrderSource)message.OrderSource;

            model.Username = message.GetUsername();
            model.UnderlyingSymbol = message.GetUnderlyingSymbol();

            model.SubmitTime = message.SubmitTime.FromUnixEpoch();
            model.LastUpdateTime = message.LastUpdateTime.FromUnixEpoch();
            model.Timestamp = message.Timestamp.FromUnixEpoch();
            model.NewStatusTimeStamp = message.NewStatusTimeStamp.FromUnixEpoch();

            model.DeltaAdjustedTheo = DecodeDoubleNull2(message.DeltaAdjustedTheo);
            model.BidSize = message.BidSize;
            model.AskSize = message.AskSize;
            model.UnderlyingBidSize = message.UnderlyingBidSize;
            model.UnderlyingAskSize = message.UnderlyingAskSize;

            model.EdgeType = (EdgeType)message.EdgeType;
            model.Edge = DecodeDoubleNull2(message.Edge);
            model.IsDeltaAdjusted = message.IsDeltaAdjusted == BooleanEnum.True;
            model.LoopInitLatency = DecodeDoubleNull2(message.LoopInitLatency);
            model.TagUnderBid = DecodeDoubleNull2(message.TagUnderBid);
            model.TagUnderAsk = DecodeDoubleNull2(message.TagUnderAsk);
            model.IsTagged = message.IsTagged == BooleanEnum.True;
            model.HardSide = message.HardSide == Generated.Side.NULL_VALUE ? null : (Side)message.HardSide;
            model.HardSideDesignationTime = message.HardSideDesignationTime.FromUnixEpoch();
            model.HardSideBuyGiveUp = DecodeDoubleNull2(message.HardSideBuyGiveUp);
            model.HardSideSellGiveUp = DecodeDoubleNull2(message.HardSideSellGiveUp);
            model.HardSideAtTrade = message.HardSideAtTrade == Generated.Side.NULL_VALUE ? null : (Side)message.HardSideAtTrade;
            model.HardSideAtTradeDesignationTime = message.HardSideAtTradeDesignationTime.FromUnixEpoch();
            model.HardSideAtTradeBuyGiveUp = DecodeDoubleNull2(message.HardSideAtTradeBuyGiveUp);
            model.HardSideAtTradeSellGiveUp = DecodeDoubleNull2(message.HardSideAtTradeSellGiveUp);

            model.EdgeGiveUp = DecodeDoubleNull2(message.EdgeGiveUp);
            model.CloseSubs = DecodeDoubleNull2(message.CloseSubs);
            model.OrderEdgeToTheo = DecodeDoubleNull2(message.OrderEdgeToTheo);

            model.TimeValue = DecodeDoubleNull2(message.TimeValue);
            model.IntrinsicValue = DecodeDoubleNull2(message.IntrinsicValue);
            model.FVDivs = DecodeDoubleNull2(message.FVDivs);
            model.UFwd = DecodeDoubleNull2(message.UFwd);
            model.UFwdFactor = DecodeDoubleNull2(message.UFwdFactor);
            model.BorrowCost = DecodeDoubleNull2(message.BorrowCost);
            model.BorrowRate = DecodeDoubleNull2(message.BorrowRate);
            model.UPrice = DecodeDoubleNull2(message.UPrice);
            model.UTheo = DecodeDoubleNull2(message.UTheo);

            model.SharedId = message.SharedId;
            model.Sequence = message.Sequence;
            model.TypeId = (ModuleType)message.TypeId;
            model.SubTypeId = (SubType)message.SubTypeCode;
            model.SubTypeSequence = message.SubTypeSequence;
            model.Venue = message.Venue == HerculesEchoMessage.VenueNullValue ? null : (Venue)message.Venue;
            model.CostOfHedging = DecodeDoubleNull2(message.CostOfHedging);

            model.SubType = message.SubType == HerculesEchoMessage.SubTypeNullValue ? null : (OrderSubType)message.SubType;

            HerculesEchoMessage.NoLegsGroup legs = message.NoLegs;
            if (model.IsComplexOrder)
            {
                IComplexOrder complexOrderModel = (IComplexOrder)model;
                while (legs.HasNext)
                {
                    HerculesEchoMessage.NoLegsGroup nextLeg = legs.Next();

                    string legId = nextLeg.GetLegID();

                    IComplexOrderLeg legModel = complexOrderModel.GetLeg(legId);
                    legModel.LegID = legId;

                    legModel.Ratio = nextLeg.Ratio;
                    legModel.Quantity = nextLeg.Quantity;
                    legModel.LastQuantity = nextLeg.LastQuantity;
                    legModel.LeavesQuantity = nextLeg.LeavesQuantity;
                    legModel.CumulativeQuantity = nextLeg.CumulativeQuantity;

                    legModel.Fee1 = DecodeDoubleNull2(nextLeg.Fee1);
                    legModel.Fee2 = DecodeDoubleNull2(nextLeg.Fee2);
                    legModel.BrokerFee1 = DecodeDoubleNull2(nextLeg.BrokerFee1);
                    legModel.BrokerFee2 = DecodeDoubleNull2(nextLeg.BrokerFee2);
                    legModel.ExchangeFee1 = DecodeDoubleNull2(nextLeg.ExchangeFee1);
                    legModel.ExchangeFee2 = DecodeDoubleNull2(nextLeg.ExchangeFee2);
                    legModel.Delta = DecodeDoubleNull4(nextLeg.Delta);
                    legModel.TV = DecodeDoubleNull2(nextLeg.TV);
                    legModel.Ask = DecodeDoubleNull2(nextLeg.Ask);
                    legModel.Bid = DecodeDoubleNull2(nextLeg.Bid);
                    legModel.AveragePrice = DecodeDoubleNull2(nextLeg.AveragePrice);
                    legModel.LastPrice = DecodeDoubleNull2(nextLeg.LastPrice);
                    legModel.HanweckTV = DecodeDoubleNull2(nextLeg.HanweckTV);
                    legModel.HanweckGamma = DecodeDoubleNull4(nextLeg.HanweckGamma);
                    legModel.HanweckVega = DecodeDoubleNull4(nextLeg.HanweckVega);
                    legModel.HanweckTheta = DecodeDoubleNull4(nextLeg.HanweckTheta);
                    legModel.HanweckRho = DecodeDoubleNull4(nextLeg.HanweckRho);
                    legModel.HanweckIV = DecodeDoubleNull4(nextLeg.HanweckIV);
                    legModel.HanweckUnder = DecodeDoubleNull2(nextLeg.HanweckUnder);
                    legModel.HanweckUnderBid = DecodeDoubleNull2(nextLeg.HanweckUnderBid);
                    legModel.HanweckUnderAsk = DecodeDoubleNull2(nextLeg.HanweckUnderAsk);
                    legModel.HanweckBid = DecodeDoubleNull2(nextLeg.HanweckBid);
                    legModel.HanweckAsk = DecodeDoubleNull2(nextLeg.HanweckAsk);
                    legModel.DeltaAdjustedTheo = DecodeDoubleNull2(nextLeg.DeltaAdjustedTheo);
                    legModel.BidSize = nextLeg.BidSize;
                    legModel.AskSize = nextLeg.AskSize;

                    legModel.PositionEffect = (Data.Enums.PositionEffect)nextLeg.PositionEffect;
                    legModel.Side = nextLeg.LegSide == LegSide.BuySide ? Side.Buy : Side.Sell;
                    legModel.OrderStatus = (Data.Enums.OrderStatus)nextLeg.OrderStatus;

                    legModel.Timestamp = nextLeg.Timestamp.FromUnixEpoch();
                    legModel.LastUpdateTime = nextLeg.LastUpdateTime.FromUnixEpoch();
                    legModel.HanweckBidTime = nextLeg.HanweckBidTime.FromUnixEpoch();
                    legModel.HanweckAskTime = nextLeg.HanweckAskTime.FromUnixEpoch();
                    legModel.HanweckTimestamp = nextLeg.HanweckTimestamp.FromUnixEpoch();

                    legModel.TimeValue = DecodeDoubleNull2(nextLeg.TimeValue);
                    legModel.IntrinsicValue = DecodeDoubleNull2(nextLeg.IntrinsicValue);
                    legModel.FVDivs = DecodeDoubleNull2(nextLeg.FVDivs);
                    legModel.UFwd = DecodeDoubleNull2(nextLeg.UFwd);
                    legModel.UFwdFactor = DecodeDoubleNull2(nextLeg.UFwdFactor);
                    legModel.BorrowCost = DecodeDoubleNull2(nextLeg.BorrowCost);
                    legModel.BorrowRate = DecodeDoubleNull2(nextLeg.BorrowRate);
                    legModel.UPrice = DecodeDoubleNull2(nextLeg.UPrice);
                    legModel.UTheo = DecodeDoubleNull2(nextLeg.UTheo);

                    DecodeLegContraFields_HerculesEcho(nextLeg, legModel);

                    legModel.PermID = nextLeg.GetPermID();
                    legModel.OrderID = nextLeg.GetOrderID();
                    legModel.Symbol = nextLeg.GetSymbol();
                }
            }

            DecodeOrderContraFields_HerculesEcho(message, model);

            model.LastExchange = message.GetLastExchange();
            model.Exchanges = message.GetExchanges();
            model.Reason = message.GetReason();
            model.Source = message.GetSource();
            model.AccountAcronym = message.GetAccountAcronym();
            model.Tag = message.GetTag();
            model.Trader = message.GetTrader();
            model.Type = message.GetOrderType();
            model.OrderID = message.GetOrderID();
            model.Route = message.GetRoute();
            model.Symbol = message.GetSymbol();
            model.Description = message.GetDescription();
            model.SpreadId = message.GetSpreadId();
            model.FullTag = message.GetFullTag();
            model.Comment = message.GetComment();
            model.AutomationType = message.GetAutomationType();
            model.SpreadHash = message.GetSpreadHash();
            model.Tagger = message.GetTagger();
            model.TaggedMessage = message.GetTaggedMessage();

            var source = message.GetSource();
            var venue = message.VenueTypeId;

            OnHerculesEchoMessage?.Invoke(model, source, (Venue)venue, message.UpdateTypeId);
        }

        private static void DecodeLegContraFields_HerculesEcho(HerculesEchoMessage.NoLegsGroup leg, IComplexOrderLeg legModel)
        {
            var caps = leg.NoLegContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                legModel.ContraCapacities = list;
            }

            var brokers = leg.NoLegContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                legModel.ContraBrokerNames = list;
            }

            var cmtas = leg.NoLegContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                legModel.ContraCmtas = list;
            }

            var traders = leg.NoLegContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                legModel.ContraTraders = list;
            }
        }

        private static void DecodeOrderContraFields_HerculesEcho(HerculesEchoMessage message, IOrder model)
        {
            var caps = message.NoContraCapacities;
            if (caps.Count > 0)
            {
                var list = new List<ContraCapacity>(caps.Count);
                while (caps.HasNext) list.Add((ContraCapacity)caps.Next().Value);
                model.ContraCapacities = list;
            }

            var brokers = message.NoContraBrokerNames;
            if (brokers.Count > 0)
            {
                var list = new List<ContraBrokerName>(brokers.Count);
                while (brokers.HasNext) list.Add((ContraBrokerName)brokers.Next().Value);
                model.ContraBrokerNames = list;
            }

            var cmtas = message.NoContraCmtas;
            if (cmtas.Count > 0)
            {
                var list = new List<ContraCmta>(cmtas.Count);
                while (cmtas.HasNext) list.Add((ContraCmta)cmtas.Next().Value);
                model.ContraCmtas = list;
            }

            var traders = message.NoContraTraders;
            if (traders.Count > 0)
            {
                var list = new List<ContraTrader>(traders.Count);
                while (traders.HasNext) list.Add((ContraTrader)traders.Next().Value);
                model.ContraTraders = list;
            }
        }

        private static double DecodePriceNull3(PRICENULL3 value)
        {
            if (value == null || value.Mantissa == PRICENULL3.MantissaNullValue)
            {
                return double.NaN;
            }

            return Math.Round(value.Mantissa * Math.Pow(10, value.Exponent), Math.Abs(value.Exponent));
        }

        private static double DecodeDoubleNull2(DOUBLENULL2 value)
        {
            if (value == null || value.Mantissa == DOUBLENULL2.MantissaNullValue)
            {
                return double.NaN;
            }

            return Math.Round(value.Mantissa * Math.Pow(10, value.Exponent), Math.Abs(value.Exponent));
        }

        private static double? DecodeDoubleNull2Nullable(DOUBLENULL2 value)
        {
            if (value == null || value.Mantissa == DOUBLENULL2.MantissaNullValue)
            {
                return null;
            }

            return Math.Round(value.Mantissa * Math.Pow(10, value.Exponent), Math.Abs(value.Exponent));
        }

        private static double DecodeDoubleNull4(DOUBLENULL4 value)
        {
            if (value == null || value.Mantissa == DOUBLENULL4.MantissaNullValue)
            {
                return double.NaN;
            }

            return Math.Round(value.Mantissa * Math.Pow(10, value.Exponent), Math.Abs(value.Exponent));
        }

        private void DecodeLiveVolDataRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            LiveVolRequestMessage message = new LiveVolRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            bool getLatest = message.GetLatest == BooleanEnum.True;
            string symbol = message.GetSymbol();
            DateTime startTime = message.StartTimestamp.FromUnixEpoch();
            DateTime endTime = message.EndTimestamp.FromUnixEpoch();
            LiveVolDataRequest?.Invoke(requestId, getLatest, symbol, startTime, endTime);
        }

        private void DecodeLiveVolDataResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            LiveVolResponseMessage message = new LiveVolResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            int requestId = message.RequestId;
            int count = message.Count;
            LiveVolResponseMessage.LiveVolDataGroup liveVolDataGroup = message.LiveVolData;
            List<LiveVolDataModel> liveVolData = new List<LiveVolDataModel>(count);

            while (liveVolDataGroup.HasNext)
            {
                liveVolDataGroup.Next();

                LiveVolDataModel liveVolRow = new LiveVolDataModel
                {
                    UploadTimestamp = liveVolDataGroup.UploadTimestamp.FromUnixEpoch(),
                    Week52High = DecodeDoubleNull2Nullable(liveVolDataGroup.Week52High),
                    Week52Low = DecodeDoubleNull2Nullable(liveVolDataGroup.Week52Low),
                    ClosePrice = DecodeDoubleNull2Nullable(liveVolDataGroup.ClosePrice),
                    HighPrice = DecodeDoubleNull2Nullable(liveVolDataGroup.HighPrice),
                    LastPrice = DecodeDoubleNull2Nullable(liveVolDataGroup.LastPrice),
                    LowPrice = DecodeDoubleNull2Nullable(liveVolDataGroup.LowPrice),
                    OpenPrice = DecodeDoubleNull2Nullable(liveVolDataGroup.OpenPrice),
                    PercentChangeFromClose = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentChangeFromClose),
                    PercentChangeFromOpen = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentChangeFromOpen),
                    PriceChangeFromClose = DecodeDoubleNull2Nullable(liveVolDataGroup.PriceChangeFromClose),
                    PriceChangeFromOpen = DecodeDoubleNull2Nullable(liveVolDataGroup.PriceChangeFromOpen),
                    PricePercentOf52WeekRange = DecodeDoubleNull2Nullable(liveVolDataGroup.PricePercentOf52WeekRange),
                    PricePercentileRank = DecodeDoubleNull2Nullable(liveVolDataGroup.PricePercentileRank),
                    SdChangeFromClose = DecodeDoubleNull2Nullable(liveVolDataGroup.SdChangeFromClose),
                    AverageIv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageIv30),
                    Expiry1Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1Iv),
                    Expiry1Iv1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1Iv1DayClose),
                    Expiry1Iv1WeekClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1Iv1WeekClose),
                    Expiry1IvChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1IvChange),
                    Expiry1IvPercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1IvPercentageChange),
                    Expiry2Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2Iv),
                    Expiry2Iv1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2Iv1DayClose),
                    Expiry2Iv1WeekClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2Iv1WeekClose),
                    Expiry2IvChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2IvChange),
                    Expiry2IvPercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2IvPercentageChange),
                    Expiry3Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry3Iv),
                    Expiry3IvChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry3IvChange),
                    Expiry3IvPercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry3IvPercentageChange),
                    Expiry4Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry4Iv),
                    Expiry5Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry5Iv),
                    Expiry6Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry6Iv),
                    Expiry7Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry7Iv),
                    Expiry8Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry8Iv),
                    Hv10 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv10),
                    Hv180 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv180),
                    Hv20 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv20),
                    Hv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30),
                    Hv30_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30_1DayClose),
                    Hv30_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30_3DayClose),
                    Hv30_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30_5DayClose),
                    Hv30PercentOf52WeekRange = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30PercentOf52WeekRange),
                    Hv30PercentileRank = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30PercentileRank),
                    Hv30WeekAgo = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv30WeekAgo),
                    Hv360 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv360),
                    Hv60 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv60),
                    Hv60_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv60_1DayClose),
                    Hv60_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv60_3DayClose),
                    Hv60_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv60_5DayClose),
                    Hv90 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv90),
                    Hv90_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv90_1DayClose),
                    Hv90_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv90_3DayClose),
                    Hv90_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv90_5DayClose),
                    Iv180 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv180),
                    Iv180_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv180_1DayClose),
                    Iv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30),
                    Iv30_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_1DayClose),
                    Iv30_1MonthClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_1MonthClose),
                    Iv30_1WeekClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_1WeekClose),
                    Iv30_3DayChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_3DayChange),
                    Iv30_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_3DayClose),
                    Iv30_5DayChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_5DayChange),
                    Iv30_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_5DayClose),
                    Iv30_52WeekHigh = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_52WeekHigh),
                    Iv30_52WeekLow = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30_52WeekLow),
                    Iv30Change = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30Change),
                    Iv30Open = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30Open),
                    Iv30PercentOf52WeekRange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30PercentOf52WeekRange),
                    Iv30PercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30PercentageChange),
                    Iv30PercentileRank = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30PercentileRank),
                    Iv360 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv360),
                    Iv360_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv360_1DayClose),
                    Iv60 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60),
                    Iv60_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60_1DayClose),
                    Iv60_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60_3DayClose),
                    Iv60_5DayChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60_5DayChange),
                    Iv60_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60_5DayClose),
                    Iv60Change = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60Change),
                    Iv60PercentOf52WeekRange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60PercentOf52WeekRange),
                    Iv60PercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60PercentageChange),
                    Iv60PercentileRank = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60PercentileRank),
                    Iv90 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90),
                    Iv90_1DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90_1DayClose),
                    Iv90_3DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90_3DayClose),
                    Iv90_5DayChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90_5DayChange),
                    Iv90_5DayClose = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90_5DayClose),
                    Iv90Change = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90Change),
                    Iv90PercentOf52WeekRange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90PercentOf52WeekRange),
                    Iv90PercentageChange = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90PercentageChange),
                    Iv90PercentileRank = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90PercentileRank),
                    OneDayStandardDeviation = DecodeDoubleNull2Nullable(liveVolDataGroup.OneDayStandardDeviation),
                    PercentOfAverageIv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfAverageIv30),
                    Expiry1IvVsExpiry2Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1IvVsExpiry2Iv),
                    Expiry1IvVsExpiry3Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1IvVsExpiry3Iv),
                    Expiry1IvVsExpiry4Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1IvVsExpiry4Iv),
                    Expiry1VsExpiry2VolRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1VsExpiry2VolRatio),
                    Expiry1VsExpiry3VolRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry1VsExpiry3VolRatio),
                    Expiry2IvVsExpiry3Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2IvVsExpiry3Iv),
                    Expiry2IvVsExpiry4Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2IvVsExpiry4Iv),
                    Expiry2VsExpiry3IvRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry2VsExpiry3IvRatio),
                    Expiry3IvVsExpiry4Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry3IvVsExpiry4Iv),
                    Expiry4IvVsExpiry5Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry4IvVsExpiry5Iv),
                    Expiry5IvVsExpiry6Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry5IvVsExpiry6Iv),
                    Expiry6IvVsExpiry7Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry6IvVsExpiry7Iv),
                    Expiry7IvVsExpiry8Iv = DecodeDoubleNull2Nullable(liveVolDataGroup.Expiry7IvVsExpiry8Iv),
                    Hv180VsIv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv180VsIv30),
                    Hv180VsIv60 = DecodeDoubleNull2Nullable(liveVolDataGroup.Hv180VsIv60),
                    Iv30Hv30Ratio = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30Hv30Ratio),
                    Iv30VsHv10 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30VsHv10),
                    Iv30VsHv20 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30VsHv20),
                    Iv30VsHv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30VsHv30),
                    Iv30VsIv60 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30VsIv60),
                    Iv30VsIv90 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv30VsIv90),
                    Iv360VsHv360 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv360VsHv360),
                    Iv60Hv60Ratio = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60Hv60Ratio),
                    Iv60VsHv10 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60VsHv10),
                    Iv60VsHv20 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60VsHv20),
                    Iv60VsHv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60VsHv30),
                    Iv60VsHv60 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60VsHv60),
                    Iv60VsIv90 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv60VsIv90),
                    Iv90VsHv10 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90VsHv10),
                    Iv90VsHv20 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90VsHv20),
                    Iv90VsHv30 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90VsHv30),
                    Iv90VsHv90 = DecodeDoubleNull2Nullable(liveVolDataGroup.Iv90VsHv90),
                    AverageUnderlyingVolume = liveVolDataGroup.AverageUnderlyingVolume == LiveVolResponseMessage.LiveVolDataGroup.AverageUnderlyingVolumeNullValue ? null : (long)liveVolDataGroup.AverageUnderlyingVolume,
                    PercentAverageUnderlyingVolume = liveVolDataGroup.PercentAverageUnderlyingVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentAverageUnderlyingVolumeNullValue ? null : (long)liveVolDataGroup.PercentAverageUnderlyingVolume,
                    UnderlyingVolume = liveVolDataGroup.UnderlyingVolume == LiveVolResponseMessage.LiveVolDataGroup.UnderlyingVolumeNullValue ? null : (long)liveVolDataGroup.UnderlyingVolume,
                    Vwap = DecodeDoubleNull2Nullable(liveVolDataGroup.Vwap),
                    AverageCallDelta = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageCallDelta),
                    AverageCallGamma = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageCallGamma),
                    AverageCallOpenInterest = liveVolDataGroup.AverageCallOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.AverageCallOpenInterestNullValue ? null : (long)liveVolDataGroup.AverageCallOpenInterest,
                    AverageCallPremium = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageCallPremium),
                    AverageCallVega = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageCallVega),
                    AverageCallVolume = liveVolDataGroup.AverageCallVolume == LiveVolResponseMessage.LiveVolDataGroup.AverageCallVolumeNullValue ? null : (long)liveVolDataGroup.AverageCallVolume,
                    AverageCallsBetweenBidAsk = liveVolDataGroup.AverageCallsBetweenBidAsk == LiveVolResponseMessage.LiveVolDataGroup.AverageCallsBetweenBidAskNullValue ? null : (long)liveVolDataGroup.AverageCallsBetweenBidAsk,
                    AverageCallsOnAsk = liveVolDataGroup.AverageCallsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.AverageCallsOnAskNullValue ? null : (long)liveVolDataGroup.AverageCallsOnAsk,
                    AverageCallsOnBid = liveVolDataGroup.AverageCallsOnBid == LiveVolResponseMessage.LiveVolDataGroup.AverageCallsOnBidNullValue ? null : (long)liveVolDataGroup.AverageCallsOnBid,
                    AverageOpenInterest = liveVolDataGroup.AverageOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.AverageOpenInterestNullValue ? null : (long)liveVolDataGroup.AverageOpenInterest,
                    AverageOptionVolume = liveVolDataGroup.AverageOptionVolume == LiveVolResponseMessage.LiveVolDataGroup.AverageOptionVolumeNullValue ? null : (long)liveVolDataGroup.AverageOptionVolume,
                    AverageOtmCallsOnAsk = liveVolDataGroup.AverageOtmCallsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.AverageOtmCallsOnAskNullValue ? null : (long)liveVolDataGroup.AverageOtmCallsOnAsk,
                    AverageOtmCallsOnBid = liveVolDataGroup.AverageOtmCallsOnBid == LiveVolResponseMessage.LiveVolDataGroup.AverageOtmCallsOnBidNullValue ? null : (long)liveVolDataGroup.AverageOtmCallsOnBid,
                    AverageOtmPutsOnAsk = liveVolDataGroup.AverageOtmPutsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.AverageOtmPutsOnAskNullValue ? null : (long)liveVolDataGroup.AverageOtmPutsOnAsk,
                    AverageOtmPutsOnBid = liveVolDataGroup.AverageOtmPutsOnBid == LiveVolResponseMessage.LiveVolDataGroup.AverageOtmPutsOnBidNullValue ? null : (long)liveVolDataGroup.AverageOtmPutsOnBid,
                    AveragePutDelta = DecodeDoubleNull2Nullable(liveVolDataGroup.AveragePutDelta),
                    AveragePutGamma = DecodeDoubleNull2Nullable(liveVolDataGroup.AveragePutGamma),
                    AveragePutOpenInterest = liveVolDataGroup.AveragePutOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.AveragePutOpenInterestNullValue ? null : (long)liveVolDataGroup.AveragePutOpenInterest,
                    AveragePutPremium = DecodeDoubleNull2Nullable(liveVolDataGroup.AveragePutPremium),
                    AveragePutVega = DecodeDoubleNull2Nullable(liveVolDataGroup.AveragePutVega),
                    AveragePutVolume = liveVolDataGroup.AveragePutVolume == LiveVolResponseMessage.LiveVolDataGroup.AveragePutVolumeNullValue ? null : (long)liveVolDataGroup.AveragePutVolume,
                    AveragePutsBetweenBidAsk = liveVolDataGroup.AveragePutsBetweenBidAsk == LiveVolResponseMessage.LiveVolDataGroup.AveragePutsBetweenBidAskNullValue ? null : (long)liveVolDataGroup.AveragePutsBetweenBidAsk,
                    AveragePutsOnAsk = liveVolDataGroup.AveragePutsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.AveragePutsOnAskNullValue ? null : (long)liveVolDataGroup.AveragePutsOnAsk,
                    AveragePutsOnBid = liveVolDataGroup.AveragePutsOnBid == LiveVolResponseMessage.LiveVolDataGroup.AveragePutsOnBidNullValue ? null : (long)liveVolDataGroup.AveragePutsOnBid,
                    AverageTradeSize = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageTradeSize),
                    CallOpenInterest = liveVolDataGroup.CallOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.CallOpenInterestNullValue ? null : (long)liveVolDataGroup.CallOpenInterest,
                    CallOpenInterest1DayAgo = liveVolDataGroup.CallOpenInterest1DayAgo == LiveVolResponseMessage.LiveVolDataGroup.CallOpenInterest1DayAgoNullValue ? null : (long)liveVolDataGroup.CallOpenInterest1DayAgo,
                    CallOpenInterest1DayChangePercent = DecodeDoubleNull2Nullable(liveVolDataGroup.CallOpenInterest1DayChangePercent),
                    CallPremium = DecodeDoubleNull2Nullable(liveVolDataGroup.CallPremium),
                    CallPutRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.CallPutRatio),
                    CallTradeCount = liveVolDataGroup.CallTradeCount == LiveVolResponseMessage.LiveVolDataGroup.CallTradeCountNullValue ? null : (long)liveVolDataGroup.CallTradeCount,
                    CallVolume = liveVolDataGroup.CallVolume == LiveVolResponseMessage.LiveVolDataGroup.CallVolumeNullValue ? null : (long)liveVolDataGroup.CallVolume,
                    CallVolume1DayAgo = liveVolDataGroup.CallVolume1DayAgo == LiveVolResponseMessage.LiveVolDataGroup.CallVolume1DayAgoNullValue ? null : (long)liveVolDataGroup.CallVolume1DayAgo,
                    CallVolumePercentOfCallOpenInterest = DecodeDoubleNull2Nullable(liveVolDataGroup.CallVolumePercentOfCallOpenInterest),
                    CallsBetweenBidAndAsk = liveVolDataGroup.CallsBetweenBidAndAsk == LiveVolResponseMessage.LiveVolDataGroup.CallsBetweenBidAndAskNullValue ? null : (long)liveVolDataGroup.CallsBetweenBidAndAsk,
                    CallsOnAsk = liveVolDataGroup.CallsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.CallsOnAskNullValue ? null : (long)liveVolDataGroup.CallsOnAsk,
                    CallsOnBid = liveVolDataGroup.CallsOnBid == LiveVolResponseMessage.LiveVolDataGroup.CallsOnBidNullValue ? null : (long)liveVolDataGroup.CallsOnBid,
                    CumulativeCallDelta = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativeCallDelta),
                    CumulativeCallGamma = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativeCallGamma),
                    CumulativeCallVega = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativeCallVega),
                    CumulativePutDelta = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativePutDelta),
                    CumulativePutGamma = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativePutGamma),
                    CumulativePutVega = DecodeDoubleNull2Nullable(liveVolDataGroup.CumulativePutVega),
                    OptionVolume = liveVolDataGroup.OptionVolume == LiveVolResponseMessage.LiveVolDataGroup.OptionVolumeNullValue ? null : (long)liveVolDataGroup.OptionVolume,
                    OptionVolumePercentOfOptionOpenInterest = DecodeDoubleNull2Nullable(liveVolDataGroup.OptionVolumePercentOfOptionOpenInterest),
                    OtmCallsOnAsk = liveVolDataGroup.OtmCallsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.OtmCallsOnAskNullValue ? null : (long)liveVolDataGroup.OtmCallsOnAsk,
                    OtmCallsOnBid = liveVolDataGroup.OtmCallsOnBid == LiveVolResponseMessage.LiveVolDataGroup.OtmCallsOnBidNullValue ? null : (long)liveVolDataGroup.OtmCallsOnBid,
                    OtmPutsOnAsk = liveVolDataGroup.OtmPutsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.OtmPutsOnAskNullValue ? null : (long)liveVolDataGroup.OtmPutsOnAsk,
                    OtmPutsOnBid = liveVolDataGroup.OtmPutsOnBid == LiveVolResponseMessage.LiveVolDataGroup.OtmPutsOnBidNullValue ? null : (long)liveVolDataGroup.OtmPutsOnBid,
                    PartialDayAverageCallVolume = liveVolDataGroup.PartialDayAverageCallVolume == LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageCallVolumeNullValue ? null : (long)liveVolDataGroup.PartialDayAverageCallVolume,
                    PartialDayAverageOptionVolume = liveVolDataGroup.PartialDayAverageOptionVolume == LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageOptionVolumeNullValue ? null : (long)liveVolDataGroup.PartialDayAverageOptionVolume,
                    PartialDayAveragePutVolume = liveVolDataGroup.PartialDayAveragePutVolume == LiveVolResponseMessage.LiveVolDataGroup.PartialDayAveragePutVolumeNullValue ? null : (long)liveVolDataGroup.PartialDayAveragePutVolume,
                    PartialDayAverageUnderlyingVolume = liveVolDataGroup.PartialDayAverageUnderlyingVolume == LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageUnderlyingVolumeNullValue ? null : (long)liveVolDataGroup.PartialDayAverageUnderlyingVolume,
                    PercentOfAverageCallVolume = liveVolDataGroup.PercentOfAverageCallVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfAverageCallVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfAverageCallVolume,
                    PercentOfAveragePutVolume = liveVolDataGroup.PercentOfAveragePutVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfAveragePutVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfAveragePutVolume,
                    PercentOfAverageVolume = liveVolDataGroup.PercentOfAverageVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfAverageVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfAverageVolume,
                    PercentOfCallsOnAsk = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfCallsOnAsk),
                    PercentOfCallsOnBid = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfCallsOnBid),
                    PercentOfOtmCallsOnAsk = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfOtmCallsOnAsk),
                    PercentOfOtmCallsOnBid = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfOtmCallsOnBid),
                    PercentOfOtmPutsOnAsk = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfOtmPutsOnAsk),
                    PercentOfOtmPutsOnBid = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfOtmPutsOnBid),
                    PercentOfPartialDayAverageCallVolume = liveVolDataGroup.PercentOfPartialDayAverageCallVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageCallVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfPartialDayAverageCallVolume,
                    PercentOfPartialDayAverageOptionVolume = liveVolDataGroup.PercentOfPartialDayAverageOptionVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageOptionVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfPartialDayAverageOptionVolume,
                    PercentOfPartialDayAveragePutVolume = liveVolDataGroup.PercentOfPartialDayAveragePutVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAveragePutVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfPartialDayAveragePutVolume,
                    PercentOfPartialDayAverageUnderlyingVolume = liveVolDataGroup.PercentOfPartialDayAverageUnderlyingVolume == LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageUnderlyingVolumeNullValue ? null : (long)liveVolDataGroup.PercentOfPartialDayAverageUnderlyingVolume,
                    PercentOfPutsOnAsk = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfPutsOnAsk),
                    PercentOfPutsOnBid = DecodeDoubleNull2Nullable(liveVolDataGroup.PercentOfPutsOnBid),
                    PutCallRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.PutCallRatio),
                    PutOpenInterest = liveVolDataGroup.PutOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.PutOpenInterestNullValue ? null : (long)liveVolDataGroup.PutOpenInterest,
                    PutOpenInterest1DayAgo = liveVolDataGroup.PutOpenInterest1DayAgo == LiveVolResponseMessage.LiveVolDataGroup.PutOpenInterest1DayAgoNullValue ? null : (long)liveVolDataGroup.PutOpenInterest1DayAgo,
                    PutOpenInterest1DayChangePercent = DecodeDoubleNull2Nullable(liveVolDataGroup.PutOpenInterest1DayChangePercent),
                    PutPremium = DecodeDoubleNull2Nullable(liveVolDataGroup.PutPremium),
                    PutTradeCount = liveVolDataGroup.PutTradeCount == LiveVolResponseMessage.LiveVolDataGroup.PutTradeCountNullValue ? null : (long)liveVolDataGroup.PutTradeCount,
                    PutVolume = liveVolDataGroup.PutVolume == LiveVolResponseMessage.LiveVolDataGroup.PutVolumeNullValue ? null : (long)liveVolDataGroup.PutVolume,
                    PutVolume1DayAgo = liveVolDataGroup.PutVolume1DayAgo == LiveVolResponseMessage.LiveVolDataGroup.PutVolume1DayAgoNullValue ? null : (long)liveVolDataGroup.PutVolume1DayAgo,
                    PutVolumePercentOfPutOpenInterest = DecodeDoubleNull2Nullable(liveVolDataGroup.PutVolumePercentOfPutOpenInterest),
                    PutsBetweenBidAndAsk = liveVolDataGroup.PutsBetweenBidAndAsk == LiveVolResponseMessage.LiveVolDataGroup.PutsBetweenBidAndAskNullValue ? null : (long)liveVolDataGroup.PutsBetweenBidAndAsk,
                    PutsOnAsk = liveVolDataGroup.PutsOnAsk == LiveVolResponseMessage.LiveVolDataGroup.PutsOnAskNullValue ? null : (long)liveVolDataGroup.PutsOnAsk,
                    PutsOnBid = liveVolDataGroup.PutsOnBid == LiveVolResponseMessage.LiveVolDataGroup.PutsOnBidNullValue ? null : (long)liveVolDataGroup.PutsOnBid,
                    SumCallVolume3Day = liveVolDataGroup.SumCallVolume3Day == LiveVolResponseMessage.LiveVolDataGroup.SumCallVolume3DayNullValue ? null : (long)liveVolDataGroup.SumCallVolume3Day,
                    SumCallVolume5Day = liveVolDataGroup.SumCallVolume5Day == LiveVolResponseMessage.LiveVolDataGroup.SumCallVolume5DayNullValue ? null : (long)liveVolDataGroup.SumCallVolume5Day,
                    SumCallVolumeLast2D = liveVolDataGroup.SumCallVolumeLast2D == LiveVolResponseMessage.LiveVolDataGroup.SumCallVolumeLast2DNullValue ? null : (long)liveVolDataGroup.SumCallVolumeLast2D,
                    SumCallVolumeLast4D = liveVolDataGroup.SumCallVolumeLast4D == LiveVolResponseMessage.LiveVolDataGroup.SumCallVolumeLast4DNullValue ? null : (long)liveVolDataGroup.SumCallVolumeLast4D,
                    SumPutVolume3Day = liveVolDataGroup.SumPutVolume3Day == LiveVolResponseMessage.LiveVolDataGroup.SumPutVolume3DayNullValue ? null : (long)liveVolDataGroup.SumPutVolume3Day,
                    SumPutVolume5Day = liveVolDataGroup.SumPutVolume5Day == LiveVolResponseMessage.LiveVolDataGroup.SumPutVolume5DayNullValue ? null : (long)liveVolDataGroup.SumPutVolume5Day,
                    SumPutVolumeLast2D = liveVolDataGroup.SumPutVolumeLast2D == LiveVolResponseMessage.LiveVolDataGroup.SumPutVolumeLast2DNullValue ? null : (long)liveVolDataGroup.SumPutVolumeLast2D,
                    SumPutVolumeLast4D = liveVolDataGroup.SumPutVolumeLast4D == LiveVolResponseMessage.LiveVolDataGroup.SumPutVolumeLast4DNullValue ? null : (long)liveVolDataGroup.SumPutVolumeLast4D,
                    TotalOpenInterest = liveVolDataGroup.TotalOpenInterest == LiveVolResponseMessage.LiveVolDataGroup.TotalOpenInterestNullValue ? null : (long)liveVolDataGroup.TotalOpenInterest,
                    TotalOpenInterest1DayChangePercent = DecodeDoubleNull2Nullable(liveVolDataGroup.TotalOpenInterest1DayChangePercent),
                    TotalOptionTradesOnTheDay = liveVolDataGroup.TotalOptionTradesOnTheDay == LiveVolResponseMessage.LiveVolDataGroup.TotalOptionTradesOnTheDayNullValue ? null : (long)liveVolDataGroup.TotalOptionTradesOnTheDay,
                    MarketCapitalization = liveVolDataGroup.MarketCapitalization == LiveVolResponseMessage.LiveVolDataGroup.MarketCapitalizationNullValue ? null : (long)liveVolDataGroup.MarketCapitalization,
                    PriceToEarningsRatio = DecodeDoubleNull2Nullable(liveVolDataGroup.PriceToEarningsRatio),
                    SharesOutstanding = liveVolDataGroup.SharesOutstanding == LiveVolResponseMessage.LiveVolDataGroup.SharesOutstandingNullValue ? null : (long)liveVolDataGroup.SharesOutstanding,
                    AverageHistoricalEarningsMove = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageHistoricalEarningsMove),
                    AverageImpliedEarningsMove = DecodeDoubleNull2Nullable(liveVolDataGroup.AverageImpliedEarningsMove),
                    DaysAfterEarnings = liveVolDataGroup.DaysAfterEarnings == LiveVolResponseMessage.LiveVolDataGroup.DaysAfterEarningsNullValue ? null : (long)liveVolDataGroup.DaysAfterEarnings,
                    DaysToNextEarningsDate = liveVolDataGroup.DaysToNextEarningsDate == LiveVolResponseMessage.LiveVolDataGroup.DaysToNextEarningsDateNullValue ? null : (long)liveVolDataGroup.DaysToNextEarningsDate,
                    DaysUntilNextDividendDate = liveVolDataGroup.DaysUntilNextDividendDate == LiveVolResponseMessage.LiveVolDataGroup.DaysUntilNextDividendDateNullValue ? null : (long)liveVolDataGroup.DaysUntilNextDividendDate,
                    ForwardVolatility = DecodeDoubleNull2Nullable(liveVolDataGroup.ForwardVolatility),
                    ImpliedEarningsMove = DecodeDoubleNull2Nullable(liveVolDataGroup.ImpliedEarningsMove),
                    LastEarningsDate = liveVolDataGroup.LastEarningsDate == LiveVolResponseMessage.LiveVolDataGroup.LastEarningsDateNullValue ? null : liveVolDataGroup.LastEarningsDate.FromUnixEpoch(),
                    NextDividendAmount = DecodeDoubleNull2Nullable(liveVolDataGroup.NextDividendAmount),
                    NextDividendDate = liveVolDataGroup.NextDividendDate == LiveVolResponseMessage.LiveVolDataGroup.NextDividendDateNullValue ? null : liveVolDataGroup.NextDividendDate.FromUnixEpoch(),
                    Symbol = liveVolDataGroup.GetSymbol(),
                    CompanyName = liveVolDataGroup.GetCompanyName(),
                    Industry = liveVolDataGroup.GetIndustry(),
                    Sector = liveVolDataGroup.GetSector(),
                    LastEarningsTimeOfDay = liveVolDataGroup.GetLastEarningsTimeOfDay(),
                    NextEarningsStatus = liveVolDataGroup.GetNextEarningsStatus(),
                    NextEarningsTime = liveVolDataGroup.GetNextEarningsTime(),
                };

                liveVolData.Add(liveVolRow);
            }

            LiveVolDataResponse?.Invoke(requestId, liveVolData);
        }

        private void DecodeRbboUpdateMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (GetRbboUpdateModelHandler == null || RbboUpdate == null)
            {
                return;
            }

            RbboUpdateMessage message = new RbboUpdateMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            RbboUpdateModel? model = GetRbboUpdateModelHandler.Invoke(message.TickerId);
            if (model == null)
            {
                return;
            }

            model.SymbolIndex = message.TickerId;
            model.KnownMcids = message.KnownMcids;
            model.ChangedMcids = message.ChangedMcids;

            var slotsGroup = message.Slots;
            model.SlotCount = slotsGroup.Count;
            int i = 0;
            while (slotsGroup.HasNext)
            {
                slotsGroup.Next();
                model.Slots[i].Mcid = slotsGroup.Mcid;
                model.Slots[i].BidPrice = DecodeDoubleNull2(slotsGroup.BidPrice);
                model.Slots[i].BidQty = slotsGroup.BidQty;
                model.Slots[i].AskPrice = DecodeDoubleNull2(slotsGroup.AskPrice);
                model.Slots[i].AskQty = slotsGroup.AskQty;
                model.Slots[i].Flags = slotsGroup.Flags;
                i++;
            }

            RbboUpdate.Invoke(model);
        }

        private void DecodeSymbolIndexMappingMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (SymbolIndexMapping == null)
            {
                return;
            }

            SymbolIndexMappingMessage message = new SymbolIndexMappingMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            int tickerId = message.TickerId;
            SubscriptionFieldType subscriptionType = (SubscriptionFieldType)message.SubscriptionType;
            string symbol = message.GetSymbol();

            SymbolIndexMapping?.Invoke(tickerId, symbol, subscriptionType);
        }

        private void DecodeRegisterForeignUpdateRoute(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (RegisterForeignUpdateRoute == null)
            {
                return;
            }

            RegisterForeignUpdateRouteMessage message = new RegisterForeignUpdateRouteMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var key = new ForeignUpdateRouteKey(
                (OrderSource)message.OrderSource,
                (OrderSubType)message.OrderSubType,
                message.GetDestination());

            var rule = new ForeignUpdateRouteRule(key, message.GetProfileId(), message.GetProfileName());
            var request = new RegisterForeignUpdateRouteRequest(rule);

            RegisterForeignUpdateRoute?.Invoke(request);
        }

        private void DecodeUnregisterForeignUpdateRoute(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (UnregisterForeignUpdateRoute == null)
            {
                return;
            }

            UnregisterForeignUpdateRouteMessage message = new UnregisterForeignUpdateRouteMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var key = new ForeignUpdateRouteKey(
                (OrderSource)message.OrderSource,
                (OrderSubType)message.OrderSubType,
                message.GetDestination());

            var request = new UnregisterForeignUpdateRouteRequest(key);

            UnregisterForeignUpdateRoute?.Invoke(request);
        }

        private void DecodeReplaceForeignUpdateRoutes(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ReplaceForeignUpdateRoutes == null)
            {
                return;
            }

            ReplaceForeignUpdateRoutesMessage message = new ReplaceForeignUpdateRoutesMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var rules = new List<ForeignUpdateRouteRule>();
            var rulesGroup = message.Rules;
            while (rulesGroup.HasNext)
            {
                rulesGroup.Next();
                var key = new ForeignUpdateRouteKey(
                    (OrderSource)rulesGroup.OrderSource,
                    (OrderSubType)rulesGroup.OrderSubType,
                    rulesGroup.GetDestination());

                var rule = new ForeignUpdateRouteRule(key, rulesGroup.GetProfileId(), rulesGroup.GetProfileName());
                rules.Add(rule);
            }

            var request = new ReplaceForeignUpdateRoutesRequest(rules.ToImmutableArray());

            ReplaceForeignUpdateRoutes?.Invoke(request);
        }

        private void DecodeForeignUpdateRoutes(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (ForeignUpdateRoutesResponse == null)
            {
                return;
            }

            ForeignUpdateRoutesMessage message = new ForeignUpdateRoutesMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var rules = new List<ForeignUpdateRouteRule>();
            var rulesGroup = message.Rules;
            while (rulesGroup.HasNext)
            {
                rulesGroup.Next();
                var key = new ForeignUpdateRouteKey(
                    (OrderSource)rulesGroup.OrderSource,
                    (OrderSubType)rulesGroup.OrderSubType,
                    rulesGroup.GetDestination());

                var rule = new ForeignUpdateRouteRule(key, rulesGroup.GetProfileId(), rulesGroup.GetProfileName());
                rules.Add(rule);
            }

            var response = new ForeignUpdateRoutesResponse(rules.ToImmutableArray());

            ForeignUpdateRoutesResponse?.Invoke(response);
        }

        private void DecodeMassCancelRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (MassCancelRequest == null)
            {
                return;
            }

            MassCancelRequestMessage message = new MassCancelRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            var request = new Data.Trading.MassCancelRequest
            {
                Venue = (Venue)message.Venue,
                Broker = (Broker)message.Broker,
                CancelType = (MassCancelType)message.CancelType,
                Exchange = message.GetExchange(),
                Account = message.GetAccount(),
                Symbol = message.GetSymbol(),
            };

            MassCancelRequest?.Invoke(request);
        }

        private void DecodeOpraDatabaseTradesRequestMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OpraDatabaseRequestTrades == null)
                return;

            OpraDatabaseTradesRequestMessage message = new OpraDatabaseTradesRequestMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            List<string> symbols = [];
            OpraDatabaseTradesRequestMessage.SymbolsGroup symbolsGroup = message.Symbols;
            while (symbolsGroup.HasNext)
            {
                OpraDatabaseTradesRequestMessage.SymbolsGroup nextSymbol = symbolsGroup.Next();
                symbols.Add(nextSymbol.GetSymbol());
            }

            List<string> underlyingSymbols = [];
            OpraDatabaseTradesRequestMessage.UnderlyingSymbolsGroup underlyingSymbolsGroup = message.UnderlyingSymbols;
            while (underlyingSymbolsGroup.HasNext)
            {
                OpraDatabaseTradesRequestMessage.UnderlyingSymbolsGroup nextSymbol = underlyingSymbolsGroup.Next();
                underlyingSymbols.Add(nextSymbol.GetUnderlyingSymbol());
            }

            var request = new OpraDatabaseTradesRequest(
                (int)message.RequestId,
                underlyingSymbols,
                symbols,
                message.RequestSpreads == BooleanEnum.True,
                message.RealTime == BooleanEnum.True,
                message.StartTime.FromUnixEpoch(),
                message.EndTime.FromUnixEpoch(),
                message.GetConstraint1(),
                message.GetConstraint2(),
                message.DeltaAdjEdgeIntervalSeconds,
                message.IsStopRequest == BooleanEnum.True,
                message.MatchIoiTrades == BooleanEnum.True);

            OpraDatabaseRequestTrades?.Invoke(request);
        }

        private void DecodeOpraDatabaseTradesResponseMessage(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (OpraDatabaseTradesResponse == null)
                return;

            OpraDatabaseTradesResponseMessage message = new OpraDatabaseTradesResponseMessage();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);

            List<OpraDatabaseTradeModel> trades = [];
            OpraDatabaseTradesResponseMessage.TradesGroup tradesGroup = message.Trades;
            while (tradesGroup.HasNext)
            {
                OpraDatabaseTradesResponseMessage.TradesGroup nextTrade = tradesGroup.Next();

                OpraDatabaseTradeModel item = new()
                {
                    MinTime = nextTrade.MinTime.FromUnixEpoch(),
                    MaxTime = nextTrade.MaxTime.FromUnixEpoch(),
                    UnderSymbol = nextTrade.GetUnderSymbol(),
                    Exchange = nextTrade.GetExchange(),
                    Condition = nextTrade.GetCondition(),
                    LegCount = nextTrade.LegCount,
                    SpreadType = nextTrade.GetSpreadType(),
                    Quantity = nextTrade.Quantity,
                    Symbol = nextTrade.GetSymbol(),
                    UnderPrice = DecodeDoubleNull2(nextTrade.UnderPrice),
                    MinTUE = DecodeDoubleNull2(nextTrade.MinTue),
                    MinBid = DecodeDoubleNull2(nextTrade.MinBid),
                    Bid = DecodeDoubleNull2(nextTrade.Bid),
                    Ask = DecodeDoubleNull2(nextTrade.Ask),
                    Price = DecodeDoubleNull2(nextTrade.Price),
                    MidMarket = DecodeDoubleNull2(nextTrade.MidMarket),
                    AboveMid = DecodeDoubleNull2(nextTrade.AboveMid),
                    TradeDelta = nextTrade.TradeDelta,
                    SQLTime = nextTrade.SQLTime.FromUnixEpoch(),
                    SpreadID = (long)nextTrade.SpreadID,
                    UnsureSymbol = nextTrade.UnsureSymbol == BooleanEnum.True,
                    TradeTime = nextTrade.TradeTime.FromUnixEpoch(),
                    UnderBid = DecodeDoubleNull2(nextTrade.UnderBid),
                    UnderAsk = DecodeDoubleNull2(nextTrade.UnderAsk),
                    UnderLast = DecodeDoubleNull2(nextTrade.UnderLast),
                    HWTV = DecodeDoubleNull2(nextTrade.HwTv),
                    HWTime = nextTrade.HWTime.FromUnixEpoch(),
                    Cond1 = (char)nextTrade.Cond1,
                    Cond2 = (char)nextTrade.Cond2,
                    Cond3 = (char)nextTrade.Cond3,
                    DeltaAdjTheo = DecodeDoubleNull2(nextTrade.DeltaAdjTheo),
                    DeltaAdjTime = nextTrade.DeltaAdjTime.FromUnixEpoch(),
                    BidSize = nextTrade.BidSize,
                    AskSize = nextTrade.AskSize,
                    HWTheta = nextTrade.HwTheta,
                    HWVega = nextTrade.HwVega,
                    HWGamma = nextTrade.HwGamma,
                    HWRho = nextTrade.HwRho,
                    TimeValue = nextTrade.TimeValue,
                    IntrinsicValue = nextTrade.IntrinsicValue,
                    FVDivs = nextTrade.FvDivs,
                    UFwd = nextTrade.UFwd,
                    UFwdFactor = nextTrade.UFwdFactor,
                    BorrowCost = nextTrade.BorrowCost,
                    BorrowRate = nextTrade.BorrowRate,
                    UPrice = DecodeDoubleNull2(nextTrade.UPrice),
                    UTheo = DecodeDoubleNull2(nextTrade.UTheo),
                    HWIV = nextTrade.HwIv,
                    VolaTV = DecodeDoubleNull2(nextTrade.VolaTv),
                    VolaDeltaAdjTheo = DecodeDoubleNull2(nextTrade.VolaDeltaAdjTheo),
                    VolaIV = nextTrade.VolaIv,
                    IsFirm = nextTrade.IsFirm == BooleanEnum.True,
                    FirmSide = nextTrade.GetFirmSide(),
                    DeltaAdjEdge = DecodeDoubleNull2(nextTrade.DeltaAdjEdge),
                    DeltaAdjEdgeRefTime = nextTrade.DeltaAdjEdgeRefTime.FromUnixEpoch(),
                };
                List<IoiLegModel> ioiLegs = [];
                item.IoiModel = new()
                {
                    Timestamp = nextTrade.IoiTimestamp.FromUnixEpoch(),
                    Description = nextTrade.GetIoiDescription(),
                    IoiId = nextTrade.IoiId,
                    LimitPrice = DecodeDoubleNull2(nextTrade.IoiLimitPrice),
                    OrderQuantity = nextTrade.IoiOrderQuantity,
                    Route = nextTrade.GetIoiRoute(),
                    Legs = ioiLegs
                };
                OpraDatabaseTradesResponseMessage.TradesGroup.IoiLegsGroup legsGroup = nextTrade.IoiLegs;
                while (legsGroup.HasNext)
                {
                    OpraDatabaseTradesResponseMessage.TradesGroup.IoiLegsGroup nextLeg = legsGroup.Next();
                    Enum.TryParse(nextLeg.GetIoiLegSide(), out Side side);
                    ioiLegs.Add(new IoiLegModel
                    {
                        UnderlyingSymbol = nextLeg.GetIoiLegUnderlyingSymbol(),
                        SecurityType = (IoiSecurityType)nextLeg.IoiLegSecurityType,
                        Side = side,
                        Type = (char)nextLeg.IoiLegType,
                        Strike = DecodeDoubleNull2(nextLeg.IoiLegStrike),
                        Expiration = nextLeg.IoiLegExpiration
                    });
                }
                
                trades.Add(item);
            }

            var response = new OpraDatabaseTradesResponse(
                (int)message.RequestId,
                message.IsLastMessage == BooleanEnum.True,
                trades);

            OpraDatabaseTradesResponse?.Invoke(response);
        }

        #region Auth Server Decode Methods

        private void DecodeAuthLoginRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthLoginRequest == null) return;
            var message = new Generated.AuthLoginRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthLoginRequestModel
            {
                RequestId = message.RequestId,
                IsReauth = message.IsReauth == BooleanEnum.True,
                Username = message.GetUsername(),
                Password = message.GetPassword(),
                AppCode = message.GetAppCode(),
                Version = message.GetVersion(),
                SystemInfo = message.GetSystemInfo(),
                AuthCode = message.GetAuthCode(),
            };
            AuthLoginRequest.Invoke(model);
        }

        private void DecodeAuthLoginResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthLoginResponse == null) return;
            var message = new Generated.AuthLoginResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthLoginResponseModel
            {
                RequestId = message.RequestId,
                IsAuthenticated = message.IsAuthenticated == BooleanEnum.True,
                UserId = message.UserId,
                ServerTime = new DateTime((long)message.ServerTime),
                MaxDuplicateSessions = message.MaxDuplicateSessions,
                AuthCode = message.GetAuthCode(),
                UserJson = message.GetUserJson(),
            };
            AuthLoginResponse.Invoke(model);
        }

        private void DecodeAuthUpdatePasswordRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthUpdatePasswordRequest == null) return;
            var message = new Generated.AuthUpdatePasswordRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthUpdatePasswordRequestModel
            {
                RequestId = message.RequestId,
                NewPassword = message.GetNewPassword(),
            };
            AuthUpdatePasswordRequest.Invoke(model);
        }

        private void DecodeAuthUpdatePasswordResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthUpdatePasswordResponse == null) return;
            var message = new Generated.AuthUpdatePasswordResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthUpdatePasswordResponseModel
            {
                RequestId = message.RequestId,
                IsSuccess = message.IsSuccess == BooleanEnum.True,
                Comment = message.GetComment(),
            };
            AuthUpdatePasswordResponse.Invoke(model);
        }

        private void DecodeAuthGetUsersRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetUsersRequest == null) return;
            var message = new Generated.AuthGetUsersRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetUsersRequestModel { RequestId = message.RequestId };
            AuthGetUsersRequest.Invoke(model);
        }

        private void DecodeAuthGetUsersResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetUsersResponse == null) return;
            var message = new Generated.AuthGetUsersResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetUsersResponseModel
            {
                RequestId = message.RequestId,
                UsersJson = message.GetUsersJson(),
            };
            AuthGetUsersResponse.Invoke(model);
        }

        private void DecodeAuthGetConfigsRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetConfigsRequest == null) return;
            var message = new Generated.AuthGetConfigsRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetConfigsRequestModel
            {
                RequestId = message.RequestId,
                ModuleId = message.ModuleId,
            };
            AuthGetConfigsRequest.Invoke(model);
        }

        private void DecodeAuthGetConfigsResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetConfigsResponse == null) return;
            var message = new Generated.AuthGetConfigsResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetConfigsResponseModel
            {
                RequestId = message.RequestId,
                ConfigsJson = message.GetConfigsJson(),
            };
            AuthGetConfigsResponse.Invoke(model);
        }

        private void DecodeAuthDeleteConfigRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthDeleteConfigRequest == null) return;
            var message = new Generated.AuthDeleteConfigRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthDeleteConfigRequestModel
            {
                RequestId = message.RequestId,
                ConfigId = message.ConfigId,
            };
            AuthDeleteConfigRequest.Invoke(model);
        }

        private void DecodeAuthDeleteConfigResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthDeleteConfigResponse == null) return;
            var message = new Generated.AuthDeleteConfigResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthDeleteConfigResponseModel
            {
                RequestId = message.RequestId,
                Message = message.GetMessage(),
            };
            AuthDeleteConfigResponse.Invoke(model);
        }

        private void DecodeAuthConfigSave(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthConfigSave == null) return;
            var message = new Generated.AuthConfigSave();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthConfigSaveModel
            {
                RequestId = message.RequestId,
                OwnerId = message.OwnerId,
                ModuleId = message.ModuleId,
                ConfigId = message.ConfigId,
                SaveTime = new DateTime((long)message.SaveTime),
                Username = message.GetUsername(),
                Title = message.GetTitle(),
                GroupName = message.GetGroupName(),
                ConfigJson = message.GetConfigJson(),
            };
            AuthConfigSave.Invoke(model);
        }

        private void DecodeAuthConfigShare(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthConfigShare == null) return;
            var message = new Generated.AuthConfigShare();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthConfigShareModel
            {
                RequestId = message.RequestId,
                ConfigJson = message.GetConfigJson(),
                ReceiverIdsJson = message.GetReceiverIdsJson(),
            };
            AuthConfigShare.Invoke(model);
        }

        private void DecodeAuthGetDomListInfosRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetDomListInfosRequest == null) return;
            var message = new Generated.AuthGetDomListInfosRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetDomListInfosRequestModel { RequestId = message.RequestId };
            AuthGetDomListInfosRequest.Invoke(model);
        }

        private void DecodeAuthGetDomListInfosResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetDomListInfosResponse == null) return;
            var message = new Generated.AuthGetDomListInfosResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetDomListInfosResponseModel
            {
                RequestId = message.RequestId,
                DomListInfosJson = message.GetDomListInfosJson(),
            };
            AuthGetDomListInfosResponse.Invoke(model);
        }

        private void DecodeAuthGetCommissionsRequest(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetCommissionsRequest == null) return;
            var message = new Generated.AuthGetCommissionsRequest();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetCommissionsRequestModel { RequestId = message.RequestId };
            AuthGetCommissionsRequest.Invoke(model);
        }

        private void DecodeAuthGetCommissionsResponse(DirectBuffer directBuffer, int bufferOffset, int actingBlockLength, int actingVersion)
        {
            if (AuthGetCommissionsResponse == null) return;
            var message = new Generated.AuthGetCommissionsResponse();
            message.WrapForDecode(directBuffer, bufferOffset, actingBlockLength, actingVersion);
            var model = new AuthGetCommissionsResponseModel
            {
                RequestId = message.RequestId,
                CommissionsJson = message.GetCommissionsJson(),
            };
            AuthGetCommissionsResponse.Invoke(model);
        }

        #endregion
    }
}