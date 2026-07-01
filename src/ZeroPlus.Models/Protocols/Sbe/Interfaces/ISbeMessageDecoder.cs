using System;
using System.Collections.Generic;
using System.Xml;
using ZeroPlus.Models.Data.Auth;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.Databento;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Data.Subscription;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Generators.SpreadGenerators;

namespace ZeroPlus.Models.Protocols.Sbe.Interfaces
{
    public delegate void ClientAuthenticationHandler(ClientAuthenticationModel model);
    public delegate void ClientRegistrationHandler(ref ClientRegistrationModel model);
    public delegate void LatencyMeterEventHandler(ref LatencyMeterEventModel model);
    public delegate void StateSnapshotHandler(ref StateSnapshotModel model);
    public delegate void SubscribeMarketDataRequestHandler(ref SubscribeMarketDataModel model);
    public delegate void UnsubscribeMarketDataRequestHandler(ref UnsubscribeMarketDataModel model);
    public delegate void SubscribeTransactionRequestHandler(ref SubscribeTransactionModel model);
    public delegate void UnsubscribeTransactionRequestHandler(ref UnsubscribeTransactionModel model);
    public delegate void SubscribePnlRequestHandler(ref SubscribePnlModel model);
    public delegate void UnsubscribePnlRequestHandler(ref UnsubscribePnlModel model);
    public delegate void RequestTransactionsFromArchiveHandler(int requestId,
                                                               DateTime startDateTime,
                                                               DateTime endDateTime,
                                                               bool ordersOnly,
                                                               List<OrderStatus> orderStatusList,
                                                               List<string> apiUsernames,
                                                               List<string> tags,
                                                               List<string> symbols,
                                                               List<string> underlyings);
    public delegate void RequestPnlFromArchiveHandler(int requestId,
                                                      DateTime startDateTime,
                                                      DateTime endDateTime,
                                                      bool requestPreCalcs,
                                                      bool includeBreakdownStats,
                                                      List<string> apiUsernames,
                                                      List<string> tags,
                                                      List<string> symbols,
                                                      List<string> underlyings);
    public delegate void AuditTrailRequestHandler(int requestId, string orderId);
    public delegate void OrderDetailsRequestHandler(int requestId, string orderId);
    public delegate void AuditTrailResponseHandler(int requestId, string orderId, XmlDocument data);
    public delegate void HanweckUpdatesWithMatchingTimestampsRequestHandler(int requestId, List<string> symbols);
    public delegate void HanweckUpdatesWithMatchingTimestampsResponseHandler(HanweckUpdatesWithMatchingTimestampsResponse response);
    public delegate void SymbolMapRequestHandler(int request);
    public delegate void SymbolMapResponseHandler(int request, bool lastGroup, Dictionary<string, int> symbolToIndexMap);
    public delegate void OptionSnapshotRequestHandler(int reqId, string symbol, double delta, DateTime expiration, DateTime startDateTime, DateTime endDateTime);
    public delegate void OptionSnapshotResponseHandler(int reqId, bool found, List<OptionSnapshot> snapshots);
    public delegate void MarketCrossScanRequestHandler(int requestId, double lookbackInSeconds, double minMarketCross, double currentMarketWidth);
    public delegate void MarketCrossScanResponseHandler(int reqId, bool found, List<MarketCrossScanResult> snapshots);
    public delegate void BestEdgeToTheoRequestHandler(int requestId, string underlying, BaseStrategy baseStrategy, int expirationIds);
    public delegate void BestEdgeToTheoResponseHandler(int requestId, double bestBuyEdgeToTheo, double avgBuyEdgeToTheo, double lastBuyEdgeToTheo, DateTime lastBuyEdgeToTheoTime, double bestSellEdgeToTheo, double avgSellEdgeToTheo, double lastSellEdgeToTheo, DateTime lastSellEdgeToTheoTime);
    public delegate void SymbolTradeRequestHandler(int requestId, string symbol);
    public delegate void SymbolsTradeRequestHandler(int requestId, bool includeOutrights, bool includeSpreads, bool includeBackdays, DateTime lastDateToInclude, string underlyings, string symbols, string tags);
    public delegate void SymbolTradeResponseHandler(int requestId, Generated.FishStatus fishStatus, double fishLevelBuy, double fishEdgeBuy, double fishLevelSell, double fishEdgeSell, DateTime lastFishTime);
    public delegate void SymbolsTradeResponseHandler(int requestId, List<SymbolFishStatusResponse> responses, bool lastMessage);
    public delegate void SingleOrderRequestHandler(SingleOrderRequest request);
    public delegate void PairOrderRequestHandler(PairOrderRequest request);
    public delegate void BasketOrderRequestHandler(BasketOrderRequest request);
    public delegate void AccountRequestHandler(int requestId);
    public delegate void AccountResponseHandler(int requestId, List<Account> accounts);
    public delegate void ResetBaseLineRequestHandler(List<string> symbols);
    public delegate void AutoTraderConfigJsonHandler(int requestId, string json);
    public delegate void AutoTraderConfigHandler(AutoTraderConfig config);
    public delegate void SendOrderHandler(IOrderSlim order);
    public delegate void IbQuoteUpdateHandler(IbQuoteUpdateModel model);
    public delegate void EdgeToTheoUpdateHandler(EdgeToTheoUpdateModel model);
    public delegate void CancelOrderRequestHandler(CancelRequest model);
    public delegate void ModifyOrderRequestHandler(ModifyRequest model);
    public delegate void TagOrderHander(string permId, bool isTagged, string tagger, string taggedMessage);
    public delegate void SymbolEdgeMapRequestHander(int requestId, DateTime start, string symbol);
    public delegate void SymbolEdgeMapResponseHander(int requestId, DateTime start, string symbol, IEnumerable<SymbolEdgeMap> symbolEdgeMaps);
    public delegate void MultiplePortfolioAddedHandler(int requestId, HashSet<IPortfolio> portfolios);
    public delegate void BarRequestHandler(int requestId, string symbol, DateTime rangeStart, DateTime rangeEnd);
    public delegate void BarResponseHandler(int requestId, string symbol, bool last, List<BarModel> bars);
    public delegate void AlertMessageHandler(AlertMessageModel alertMessage);
    public delegate void SymbolStrikeRangeRequestHandler(int requestId, double delta, DateTime expiration, string symbol);
    public delegate void SymbolStrikeRangeResponseHandler(int requestId, double strikeRange);
    public delegate void PermEdgeToTheoMappingHandler(string symbol, List<EdgeToTheoTrackerModel> mappings);
    public delegate void EdgeScanFeedServerRunnerRequestHandler(EdgeScanFeedServerRunner serverRunner);
    public delegate void EdgeScanFeedServerRunnerUnregisterHandler(string id);
    public delegate void SpreadGeneratorRequestHandler(int id, SpreadsGeneratorConfig config);
    public delegate void SpreadGeneratorResultsHandler(int id, bool lastGroup, List<string> symbols);
    public delegate void SymbolsRequestHandler(int id, string symbol, string secType, string exchange, string currency);
    public delegate void SymbolsResultHandler(int id, List<string> symbols, bool lastGroup);
    public delegate void OptionChainRequestHandler(int requestId, string underlying, int? expiryFromYyMMdd, int? expiryToYyMMdd, double? strikeMin, double? strikeMax, PutCall? putCallFilter);
    public delegate void OptionChainResponseHandler(int requestId, List<Option> options, bool lastGroup);
    public delegate void FirmOrderAndTradeSummaryHandler(FirmOrderAndTradeSummary summary);
    public delegate void DataRequestMessageHandler(int requestId, SubscriptionFieldType type);
    public delegate void HistoricHighestBidLowestAskRequestHandler(int requestId, int tickerId, string symbol);
    public delegate void HistoricHighestBidLowestAskResponseHandler(int requestId, int tickerId, string symbol, List<HighestBidLowestAskTrackerModel> updates);
    public delegate void PositionsRequestHandler(int requestId, string portfolioName, PortfolioType portfolioType, PositionType positionType);
    public delegate void TheoToMarketSpreadUpdateHandler(TheoToMarketSpread update);
    public delegate void MatrixSyntheticSpreadHandler(SyntheticSpread model);
    public delegate void MatrixSeekerSpreadHandler(SeekerSpread model);
    public delegate void MatrixSeekerHandler(Seeker model);
    public delegate void MatrixScrapeHandler(Scrape model);
    public delegate void ExecutionTransactionHandler(Transaction transaction);
    public delegate void OrderTagMessageHandler(OrderTagModel orderTagModel);
    public delegate void ModifySmartOrderRequestHandler(ModifySmartRequest model);
    public delegate void ModeledTheoUpdateHandler(ModeledTheoUpdate update);
    public delegate void SpreadBookQuoteUpdateHandler(SpreadBookQuote update);
    public delegate void SpreadExchOrderUpdateHandler(SpreadExchOrder update);
    public delegate void SpreadPrintUpdateHandler(SpreadPrint update);
    public delegate void AuctionPrintUpdateHandler(AuctionPrint update);
    public delegate void CobTradeRequestHandler(int reqId, string underlying, DateTime startTime, DateTime endTime, int limit);
    public delegate void CancelDataRequestHandler(int reqId, SubscriptionFieldType fieldType);
    public delegate void CobTradeResponseHandler(int reqId, List<SpreadExchPrint> prints);
    public delegate void ModelDescriptionUpdateHandler(byte modelId, string description);
    public delegate void CancelTokenMessageHandler(int reqId, string token);
    public delegate void ImpliedQuoteUpdateHandler(ImpliedQuoteUpdate update);
    public delegate void GetClosestOptionRequestHandler(int requestId, string underlying, SubscriptionFieldType field, PutCall putCall, DateTime expiration, double value);
    public delegate void GetClosestOptionResponseHandler(int requestId, string symbol);
    public delegate void NextOptionPermsRequestHandler(int requestId, string symbol, PermutationDirection direction, PermMode mode, int count);
    public delegate void NextOptionPermsResponseHandler(int requestId, bool lastGroup, List<string> symbols);
    public delegate void NextSpreadPermsRequestHandler(int requestId, List<PermLegRequest> legs, PermutationDirection direction, PermMode mode, PermSide permSide, int count, BaseStrategy baseStrategy, bool maintainBaseStrategy, bool maintainBaseStrategyFlyException, bool skipCheck);
    public delegate void NextSpreadPermsResponseHandler(int requestId, bool lastGroup, List<PermSpreadResult> perms);
    public delegate void JsonRequestHandler(JsonRequest jsonRequest);
    public delegate void JsonResponseHandler(JsonResponse jsonRequest);
    public delegate void RiskCheckResultHandler(IHaveRisk result);
    public delegate void OrderRiskRequestHandler(OrderRisk result);
    public delegate void CancelRiskRequestHandler(CancelRisk result);
    public delegate void CancelReplaceRiskRequestHandler(CancelReplaceRisk result);
    public delegate void OrderUpdateHandler(OrderUpdateModel orderUpdateModel);
    public delegate void OrderInfoUpdateHandler(OrderInfoUpdate update);
    public delegate void OrderUpdateValueHandler(OrderUpdateValues orderUpdate);
    public delegate void AutomationStateChangedHandler(string id, bool automationRunning);
    public delegate void SubmissionSummaryUpdateHandler(SubmissionsSummary update);
    public delegate void PerformanceModeRequestHandler(bool isPerformanceModeEnabled);
    public delegate void PricingRequestHandler(PricingRequestModel request);
    public delegate void PricingResponseHandler(PricingResponseModel response);
    public delegate void TradesRequestHandler(uint requestId, string symbol, DateTime start, DateTime end);
    public delegate void TradesResponseHandler(uint requestId, bool lastGroup, uint count, List<MbpTradeModel> trades);
    public delegate void AddRemoveMultipleTradesRequestHandler(bool add, List<string> permIds);
    public delegate void TheosBatchUpdatedHandler(long timestamp, IReadOnlyList<IDeltaAdjustedOption> models);
    public delegate void OpenSpreadExchOrderHandler(IOpenSpreadExchOrder model);
    public delegate void RemoveSpreadExchOrderHandler(string underlying, string orderId);
    public delegate void VolSurfaceRequestHandler(VolSurfaceRequestModel model);
    public delegate void VolSurfaceResponseHandler(VolSurfaceResponseModel model);
    public delegate void HerculesEchoMessageHandler(IOrder order, string? source, Venue venue, int updateType);
    public delegate void HerculesEchoRequestMessageHandler(bool requestEcho);
    public delegate void LiveVolDataRequestHandler(int requestId, bool getLatest, string? symbol, DateTime startTime, DateTime endTime);
    public delegate void LiveVolDataResponseHandler(int requestId, List<LiveVolDataModel> data);
    public delegate void SymbolIndexMappingHandler(int tickerId, string symbol, SubscriptionFieldType subscriptionType);
    public delegate void RbboUpdateHandler(RbboUpdateModel model);
    public delegate void RegisterForeignUpdateRouteHandler(RegisterForeignUpdateRouteRequest request);
    public delegate void UnregisterForeignUpdateRouteHandler(UnregisterForeignUpdateRouteRequest request);
    public delegate void ReplaceForeignUpdateRoutesHandler(ReplaceForeignUpdateRoutesRequest request);
    public delegate void ForeignUpdateRoutesResponseHandler(ForeignUpdateRoutesResponse response);
    public delegate void MassCancelRequestHandler(MassCancelRequest request);
    public delegate void OpraDatabaseRequestTradesMessageHandler(OpraDatabaseTradesRequest opraDatabaseTradesRequest);
    public delegate void OpraDatabaseResponseTradesMessageHandler(OpraDatabaseTradesResponse opraDatabaseTradesResponse);
    public delegate void EdgeScanFeedRunnerStartRequestHandler(EdgeScanFeedRunnerStartRequest startRequest);
    public delegate void EdgeScanFeedRunnerStopRequestHandler(string runnerId);
    public delegate void EdgeScanFeedRunnerChangedHandler(string runnerId, EdgeScanFeedRunnerState state);
    public delegate void TradeSlimUpdateHandler(TradeSlim tradeSlim);

    // Auth Server Delegates
    public delegate void AuthLoginRequestHandler(AuthLoginRequestModel model);
    public delegate void AuthLoginResponseHandler(AuthLoginResponseModel model);
    public delegate void AuthUpdatePasswordRequestHandler(AuthUpdatePasswordRequestModel model);
    public delegate void AuthUpdatePasswordResponseHandler(AuthUpdatePasswordResponseModel model);
    public delegate void AuthGetUsersRequestHandler(AuthGetUsersRequestModel model);
    public delegate void AuthGetUsersResponseHandler(AuthGetUsersResponseModel model);
    public delegate void AuthGetConfigsRequestHandler(AuthGetConfigsRequestModel model);
    public delegate void AuthGetConfigsResponseHandler(AuthGetConfigsResponseModel model);
    public delegate void AuthDeleteConfigRequestHandler(AuthDeleteConfigRequestModel model);
    public delegate void AuthDeleteConfigResponseHandler(AuthDeleteConfigResponseModel model);
    public delegate void AuthConfigSaveHandler(AuthConfigSaveModel model);
    public delegate void AuthConfigShareHandler(AuthConfigShareModel model);
    public delegate void AuthGetDomListInfosRequestHandler(AuthGetDomListInfosRequestModel model);
    public delegate void AuthGetDomListInfosResponseHandler(AuthGetDomListInfosResponseModel model);
    public delegate void AuthGetCommissionsRequestHandler(AuthGetCommissionsRequestModel model);
    public delegate void AuthGetCommissionsResponseHandler(AuthGetCommissionsResponseModel model);
    public delegate void SingleFieldUpdateHandler(int tickerId, SubscriptionFieldType updateType, double value);

    public delegate IHaveRisk? RiskRequestHandler(uint id);
    public delegate AutoTraderConfig? AutoTraderConfigRequestHandler(string id);
    public delegate IReadOnlyList<IDeltaAdjustedOption>? GetTheoModelsHandler(int underIndex);
    public delegate RbboUpdateModel? GetRbboUpdateModelHandler(int tickerId);
    public delegate IOpenSpreadExchOrder? GetOpenSpreadExchOrderHandler(string underlying, string orderId);

    public interface ISbeMessageDecoder : IMessageParser
    {
        event ClientAuthenticationHandler ClientAuthentication;
        event ClientRegistrationHandler ClientRegistration;
        event LatencyMeterEventHandler LatencyMeterEvent;
        event StateSnapshotHandler StateSnapshot;
        event SubscribeMarketDataRequestHandler SubscribeMarketDataRequest;
        event UnsubscribeMarketDataRequestHandler UnsubscribeMarketDataRequest;
        event SubscribeTransactionRequestHandler SubscribeTransactionRequest;
        event UnsubscribeTransactionRequestHandler UnsubscribeTransactionRequest;
        event SubscribePnlRequestHandler SubscribePnlRequest;
        event UnsubscribePnlRequestHandler UnsubscribePnlRequest;
        event RequestTransactionsFromArchiveHandler RequestTransactionsFromArchive;
        event RequestPnlFromArchiveHandler RequestPnlFromArchive;
        event AuditTrailResponseHandler AuditTrailResponse;
        event AuditTrailRequestHandler AuditTrailRequest;
        event OrderDetailsRequestHandler OrderDetailsRequest;
        event HanweckUpdatesWithMatchingTimestampsRequestHandler HanweckUpdatesWithMatchingTimestampsRequest;
        event HanweckUpdatesWithMatchingTimestampsResponseHandler HanweckUpdatesWithMatchingTimestampsResponse;
        event SymbolMapRequestHandler SymbolMapRequest;
        event SymbolMapResponseHandler SymbolMapResponse;
        event SymbolMapRequestHandler RootSymbolMapRequest;
        event OptionSnapshotRequestHandler OptionSnapshotRequest;
        event OptionSnapshotResponseHandler OptionSnapshotResponse;
        event MarketCrossScanRequestHandler MarketCrossScanRequest;
        event MarketCrossScanResponseHandler MarketCrossScanResponse;
        event BestEdgeToTheoRequestHandler BestEdgeToTheoRequest;
        event BestEdgeToTheoResponseHandler BestEdgeToTheoResponse;
        event SymbolTradeRequestHandler SymbolTradeRequest;
        event SymbolTradeResponseHandler SymbolTradeResponse;
        event SymbolsTradeRequestHandler SymbolsTradeRequest;
        event SymbolsTradeResponseHandler SymbolsTradeResponse;
        event SingleOrderRequestHandler SingleOrderRequest;
        event PairOrderRequestHandler PairOrderRequest;
        event BasketOrderRequestHandler BasketOrderRequest;
        event AccountRequestHandler AccountRequest;
        event AccountResponseHandler AccountResponse;
        event ResetBaseLineRequestHandler ResetBaseLineRequest;
        event AutoTraderConfigJsonHandler? AutoTraderConfigJson;
        event AutoTraderConfigHandler? AutoTraderConfig;
        event SendOrderHandler SendOrder;
        event IbQuoteUpdateHandler IbQuoteUpdate;
        event EdgeToTheoUpdateHandler EdgeToTheoUpdate;
        event CancelOrderRequestHandler CancelOrderRequest;
        event ModifyOrderRequestHandler ModifyOrderRequest;
        event TagOrderHander TagOrder;
        event SymbolEdgeMapRequestHander SymbolEdgeMapRequest;
        event SymbolEdgeMapResponseHander SymbolEdgeMapResponse;
        event MultiplePortfolioAddedHandler MultiplePortfolioAdded;
        event BarRequestHandler BarRequest;
        event BarResponseHandler BarResponse;
        event AlertMessageHandler AlertMessage;
        event SymbolStrikeRangeRequestHandler SymbolStrikeRangeRequest;
        event SymbolStrikeRangeResponseHandler SymbolStrikeRangeResponse;
        event PermEdgeToTheoMappingHandler PermEdgeToTheoMapping;
        event EdgeScanFeedServerRunnerRequestHandler EdgeScanFeedServerRunnerRequest;
        event EdgeScanFeedServerRunnerUnregisterHandler EdgeScanFeedServerRunnerUnregister;
        event SpreadGeneratorRequestHandler SpreadGeneratorRequest;
        event SpreadGeneratorResultsHandler SpreadGeneratorResults;
        event SymbolsRequestHandler SymbolsRequest;
        event SymbolsResultHandler SymbolsResponse;
        event OptionChainRequestHandler OptionChainRequest;
        event OptionChainResponseHandler OptionChainResponse;
        event FirmOrderAndTradeSummaryHandler FirmOrderAndTradeSummaryReceived;
        event DataRequestMessageHandler DataRequestMessage;
        event HistoricHighestBidLowestAskRequestHandler HistoricHighestBidLowestAskRequest;
        event HistoricHighestBidLowestAskResponseHandler HistoricHighestBidLowestAskResponse;
        event PositionsRequestHandler PositionsRequest;
        event TheoToMarketSpreadUpdateHandler TheoToMarketSpreadUpdate;
        event MatrixSyntheticSpreadHandler MatrixSyntheticSpread;
        event MatrixSeekerSpreadHandler MatrixSeekerSpread;
        event MatrixSeekerHandler MatrixSeeker;
        event MatrixScrapeHandler MatrixScrape;
        event ExecutionTransactionHandler ExecutionTransaction;
        event OrderTagMessageHandler OrderTagMessage;
        event ModifySmartOrderRequestHandler ModifySmartOrderRequest;
        event ModeledTheoUpdateHandler ModeledTheoUpdate;
        event SpreadBookQuoteUpdateHandler SpreadBookQuoteUpdate;
        event SpreadExchOrderUpdateHandler SpreadExchOrderUpdate;
        event SpreadPrintUpdateHandler SpreadPrintUpdate;
        event AuctionPrintUpdateHandler AuctionPrintUpdate;
        event CobTradeRequestHandler CobTradeRequest;
        event CancelDataRequestHandler CancelDataRequest;
        event CobTradeResponseHandler CobTradeResponse;
        event ModelDescriptionUpdateHandler ModelDescriptionUpdate;
        event CancelTokenMessageHandler CancelTokenMessage;
        event ImpliedQuoteUpdateHandler ImpliedQuoteUpdate;
        event GetClosestOptionRequestHandler GetClosestOptionRequest;
        event GetClosestOptionResponseHandler GetClosestOptionResponse;
        event NextOptionPermsRequestHandler? NextOptionPermsRequest;
        event NextOptionPermsResponseHandler? NextOptionPermsResponse;
        event NextSpreadPermsRequestHandler? NextSpreadPermsRequest;
        event NextSpreadPermsResponseHandler? NextSpreadPermsResponse;
        event JsonRequestHandler JsonRequest;
        event JsonResponseHandler JsonResponse;
        event RiskCheckResultHandler RiskCheckResult;
        event OrderRiskRequestHandler OrderRiskRequest;
        event CancelRiskRequestHandler CancelRiskRequest;
        event CancelReplaceRiskRequestHandler CancelReplaceRiskRequest;
        event OrderUpdateHandler OrderUpdate;
        event OrderInfoUpdateHandler OrderInfoUpdate;
        event OrderUpdateValueHandler OrderUpdateValue;
        event AutomationStateChangedHandler AutomationStateChanged;
        event SubmissionSummaryUpdateHandler SubmissionSummaryUpdate;
        event PerformanceModeRequestHandler PerformanceModeRequest;
        event PricingRequestHandler PricingRequest;
        event PricingResponseHandler PricingResponse;
        event TradesRequestHandler TradesRequest;
        event TradesResponseHandler TradesResponse;
        event AddRemoveMultipleTradesRequestHandler AddRemoveMultipleTradesRequest;
        event TheosBatchUpdatedHandler TheosBatchUpdated;
        event OpenSpreadExchOrderHandler OpenSpreadExchOrder;
        event RemoveSpreadExchOrderHandler RemoveSpreadExchOrder;
        event VolSurfaceRequestHandler? VolSurfaceRequest;
        event VolSurfaceResponseHandler? VolSurfaceResponse;
        event HerculesEchoMessageHandler? OnHerculesEchoMessage;
        event HerculesEchoRequestMessageHandler? OnHerculesEchoRequestMessage;
        event LiveVolDataRequestHandler? LiveVolDataRequest;
        event LiveVolDataResponseHandler? LiveVolDataResponse;
        event SymbolIndexMappingHandler? SymbolIndexMapping;
        event RbboUpdateHandler? RbboUpdate;
        event RegisterForeignUpdateRouteHandler? RegisterForeignUpdateRoute;
        event UnregisterForeignUpdateRouteHandler? UnregisterForeignUpdateRoute;
        event ReplaceForeignUpdateRoutesHandler? ReplaceForeignUpdateRoutes;
        event ForeignUpdateRoutesResponseHandler? ForeignUpdateRoutesResponse;
        event MassCancelRequestHandler? MassCancelRequest;
        event OpraDatabaseRequestTradesMessageHandler? OpraDatabaseRequestTrades;
        event OpraDatabaseResponseTradesMessageHandler? OpraDatabaseTradesResponse;
        event EdgeScanFeedRunnerStartRequestHandler? EdgeScanFeedRunnerStartRequest;
        event EdgeScanFeedRunnerStopRequestHandler? EdgeScanFeedRunnerStopRequest;
        event EdgeScanFeedRunnerChangedHandler? EdgeScanFeedRunnerChanged;
        event TradeSlimUpdateHandler? TradeSlimUpdate;

        // Auth Server Events
        event AuthLoginRequestHandler? AuthLoginRequest;
        event AuthLoginResponseHandler? AuthLoginResponse;
        event AuthUpdatePasswordRequestHandler? AuthUpdatePasswordRequest;
        event AuthUpdatePasswordResponseHandler? AuthUpdatePasswordResponse;
        event AuthGetUsersRequestHandler? AuthGetUsersRequest;
        event AuthGetUsersResponseHandler? AuthGetUsersResponse;
        event AuthGetConfigsRequestHandler? AuthGetConfigsRequest;
        event AuthGetConfigsResponseHandler? AuthGetConfigsResponse;
        event AuthDeleteConfigRequestHandler? AuthDeleteConfigRequest;
        event AuthDeleteConfigResponseHandler? AuthDeleteConfigResponse;
        event AuthConfigSaveHandler? AuthConfigSave;
        event AuthConfigShareHandler? AuthConfigShare;
        event AuthGetDomListInfosRequestHandler? AuthGetDomListInfosRequest;
        event AuthGetDomListInfosResponseHandler? AuthGetDomListInfosResponse;
        event AuthGetCommissionsRequestHandler? AuthGetCommissionsRequest;
        event AuthGetCommissionsResponseHandler? AuthGetCommissionsResponse;
        event SingleFieldUpdateHandler? SingleFieldUpdate;

        RiskRequestHandler? RiskRequestHandler { set; }
        AutoTraderConfigRequestHandler? AutoTraderConfigRequestHandler { set; }
        GetTheoModelsHandler? GetTheoModelsHandler { set; }
        GetRbboUpdateModelHandler? GetRbboUpdateModelHandler { set; }
        GetOpenSpreadExchOrderHandler? GetOpenSpreadExchOrderHandler { set; }
    }
}