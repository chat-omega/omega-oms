using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;

namespace ZeroPlus.Models.Data.Trading.Interfaces
{
    public interface IOrder : IOrderSlim
    {
        long MsgSequence { get; set; }
        Venue? Venue { get; set; }
        int LastQuantity { get; set; }
        int FilledQty { get; set; }
        int LeavesQuantity { get; set; }
        int CumulativeQuantity { get; set; }
        int Quantity { get; set; }
        int TransactionID { get; set; }
        int AccountID { get; set; }
        bool PartiallyFilled { get; set; }
        double SpreadAvgPrice { get; set; }
        double AveragePrice { get; set; }
        double Price { get; set; }
        double LastPrice { get; set; }
        double MinPrice { get; set; }
        double MaxPrice { get; set; }
        double EdgeGiveUp { get; set; }
        double CloseSubs { get; set; }
        double TagEdge { get; set; }
        double TagMid { get; set; }
        double TagBid { get; set; }
        double TagAsk { get; set; }
        double TagTheo { get; set; }
        double TagVolaV0 { get; set; }
        double TagVolaV1 { get; set; }
        double TagVolaV2 { get; set; }
        double TagEma { get; set; }
        public ulong IoiId { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; }
        public SubType SubTypeId { get; set; }
        public ushort SubTypeSequence { get; set; }
        double Fee1 { get; set; }
        double Fee2 { get; set; }
        double Bid { get; set; }
        double Ask { get; set; }
        double Mid { get; set; }
        double UnderBid { get; set; }
        double UnderAsk { get; set; }
        double TV { get; set; }
        double Delta { get; set; }
        double ExchangeFee1 { get; set; }
        double ExchangeFee2 { get; set; }
        double BrokerFee1 { get; set; }
        double BrokerFee2 { get; set; }
        double TotalContracts { get; set; }
        double FillTime { get; set; }
        double TradeToNewTime { get; set; }
        double SubmitToNewTime { get; set; }
        double NewToCancelTime { get; set; }
        double BidPercentOfFillPrice { get; set; }
        double CloseBidPercentOfFillPrice { get; set; }
        double OmsBidPercentOfFillPrice { get; set; }
        double OmsBestBidPercent { get; set; }
        double TotalDelta { get; set; }
        double HanweckTotalTheo { get; set; }
        double HanweckTotalGamma { get; set; }
        double HanweckTotalVega { get; set; }
        double HanweckTotalTheta { get; set; }
        double HanweckTotalRho { get; set; }
        double HanweckTotalIV { get; set; }
        double HanweckTotalUnder { get; set; }
        double HanweckTotalUBid { get; set; }
        double HanweckTotalUAsk { get; set; }
        double HanweckTotalBid { get; set; }
        double HanweckTotalAsk { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        double CloseTV { get; set; }
        double CloseDelta { get; set; }
        double CloseTotalDelta { get; set; }
        double CloseHanweckTotalTheo { get; set; }
        double CloseHanweckTotalGamma { get; set; }
        double CloseHanweckTotalVega { get; set; }
        double CloseHanweckTotalTheta { get; set; }
        double CloseHanweckTotalRho { get; set; }
        double CloseHanweckTotalIV { get; set; }
        double CloseBid { get; set; }
        double CloseAsk { get; set; }
        double CloseUnderBid { get; set; }
        double CloseUnderAsk { get; set; }
        double CloseHanweckTotalUnder { get; set; }
        double CloseHanweckTotalUBid { get; set; }
        double CloseHanweckTotalUAsk { get; set; }
        double CloseHanweckTotalBid { get; set; }
        double CloseHanweckTotalAsk { get; set; }
        double DeltaAdjustedTheo { get; set; }
        double CloseDeltaAdjustedTheo { get; set; }
        double Ema { get; set; }
        int BidSize { get; set; }
        int AskSize { get; set; }
        int CloseBidSize { get; set; }
        int CloseAskSize { get; set; }
        int UnderlyingBidSize { get; set; }
        int UnderlyingAskSize { get; set; }
        int CloseUnderlyingBidSize { get; set; }
        int CloseUnderlyingAskSize { get; set; }
        double EdgeOverride { get; set; }
        double AdjustedEdgeOverride { get; set; }
        double LastEdge { get; set; }
        double DeltaAdjLastEdge { get; set; }
        double DeltaAdjLastEdgeNotional { get; set; }
        double DeltaAdjChange { get; set; }
        double DeltaAdjChangeNotional { get; set; }
        double EdgeScanFeedDeltaAdjPrice { get; set; }
        double EdgeScanFeedEdge { get; set; }
        double EdgeScanFeedTimespan { get; set; }
        double EdgeScanFeedBuyPrice { get; set; }
        int EdgeScanFeedBuyQty { get; set; }
        double EdgeScanFeedSellPrice { get; set; }
        int EdgeScanFeedSellQty { get; set; }
        DateTime EdgeScanFeedBuyTime { get; set; }
        DateTime EdgeScanFeedSellTime { get; set; }
        double EdgeScanFeedRespondLatency { get; set; }
        char EdgeScanFeedConditionCode { get; set; }
        int ResubmitCount { get; set; }
        int TotalEstimatedResubmit { get; set; }
        MinimumTickStyle MinimumTickStyle { get; set; }
        string? Guid { get; set; }
        string? Username { get; set; }
        string? AccountAcronym { get; set; }
        string? UnderlyingSymbol { get; set; }
        string? Description { get; set; }
        string? SpreadId { get; set; }
        string? Source { get; set; }
        string? Tag { get; set; }
        string? Trader { get; set; }
        string? Type { get; set; }
        OrderSubType? SubType { get; set; }
        string? Comment { get; set; }
        string? SmartRoute { get; set; }
        string? FullTag { get; set; }
        string? ExchangeOrderID { get; set; }
        string? ExecutingBroker { get; set; }
        string? ExecutionID { get; set; }
        string? ExecutionReferenceID { get; set; }
        string? LocalID { get; set; }
        string? PermID { get; set; }
        string? OrderID { get; set; }
        string? OriginalOrderID { get; set; }
        string? RequestAccountAcronym { get; set; }
        string? Route { get; set; }
        string? RequestSymbol { get; set; }
        string? Destination { get; set; }
        string? SpreadHash { get; set; }
        PositionEffect PositionEffect { get; set; }
        TimeInForce TimeInForce { get; set; }
        OrderSource OrderSource { get; set; }
        string? RoutingSession { get; set; }
        string? ClearingFirm { get; set; }
        string? ClearingID { get; set; }
        DateTime SubmitTime { get; set; }
        DateTime LastUpdateTime { get; set; }
        DateTime Timestamp { get; set; }
        DateTime NewStatusTimeStamp { get; set; }
        Side? Side { get; set; }
        ExecutionType ExecutionType { get; set; }
        OrderStatus OrderStatus { get; set; }
        BaseStrategy BaseStrategy { get; set; }
        Security? Security { get; set; }
        bool IsComplexOrder { get; }
        ISecurityBook? SecurityBook { get; }
        double UnderMid => (UnderAsk + UnderBid) * 0.5;
        double TotalCommissions => Fee1 + Fee2 + BrokerFee1 + BrokerFee2 + ExchangeFee1 + ExchangeFee2;
        string? LastExchange { get; set; }
        string? Reason { get; set; }
        string? Symbol { get; set; }
        double OrderEdgeToTheo { get; set; }
        double EdgeToTheo { get; set; }
        double TagEdgeToTheo { get; set; }
        double TagEdgeToEma{ get; set; }
        double TagEdgeToVolaV0 { get; set; }
        double TagEdgeToVolaV1 { get; set; }
        double TagEdgeToVolaV2 { get; set; }
        double TagBestBid { get; set; }
        double TagBestAsk { get; set; }
        double TagMktMkrBid { get; set; }
        double TagMktMkrAsk { get; set; }
        int TagVolume { get; set; }
        int TagFirmVolume { get; set; }
        double InitialEdge { get; set; }
        double OpenEdge { get; set; }
        double CloseEdge { get; set; }
        bool IsFirstFill { get; set; }
        bool FirstEdgeAcquired { get; set; }
        double FirstEdge { get; set; }
        double Multiplier { get; set; }
        bool IsCitadel { get; set; }
        Side? CitadelSide { get; set; }
        PositionEffect? SpreadPositionEffect { get; set; }
        string? AutomationType { get; set; }
        bool IsFill { get; }
        bool IsAutomation { get; set; }
        EdgeType EdgeType { get; set; }
        double Edge { get; set; }
        bool IsDeltaAdjusted { get; set; }
        double LoopInitLatency { get; set; }
        double TagUnderBid { get; set; }
        double TagUnderAsk { get; set; }
        double DigBid { get; set; }
        double DigAsk { get; set; }
        uint DigBidSize { get; set; }
        uint DigAskSize { get; set; }
        double WeightedVega { get; set; }
        bool IsTagged { get; set; }
        string? Tagger { get; set; }
        string? TaggedMessage { get; set; }
        Side? HardSide { get; set; }
        DateTime HardSideDesignationTime { get; set; }
        double HardSideBuyGiveUp { get; set; }
        double HardSideSellGiveUp { get; set; }
        Side? HardSideAtTrade { get; set; }
        DateTime HardSideAtTradeDesignationTime { get; set; }
        double HardSideAtTradeBuyGiveUp { get; set; }
        double HardSideAtTradeSellGiveUp { get; set; }
        double CostOfHedging { get; set; }
        OrderTagModel? OrderTag { get; set; }
        OrderType OrderType { get; set; }
        string? Exchanges { get; set; }
        IList<ContraCapacity>? ContraCapacities { get; set; }
        IList<ContraBrokerName>? ContraBrokerNames { get; set; }
        IList<ContraCmta>? ContraCmtas { get; set; }
        IList<ContraTrader>? ContraTraders { get; set; }
    }
}
