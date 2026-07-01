using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading
{
    public class Order : OrderSlim, IOrder
    {
        private string? _symbol;

        public long MsgSequence { get; set; }
        public Venue? Venue { get; set; }
        public int LastQuantity { get; set; }
        public int FilledQty { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public int Quantity { get; set; }
        public int TransactionID { get; set; }
        public int AccountID { get; set; }
        public bool PartiallyFilled { get; set; }
        public double SpreadAvgPrice { get; set; }
        public double AveragePrice { get; set; } = double.NaN;
        public double Price { get; set; } = double.NaN;
        public double LastPrice { get; set; } = double.NaN;
        public double MinPrice { get; set; } = double.NaN;
        public double MaxPrice { get; set; } = double.NaN;
        public double EdgeGiveUp { get; set; } = double.NaN;
        public double CloseSubs { get; set; } = double.NaN;
        public double TagEdge { get; set; }
        public double TagMid { get; set; }
        public double TagBid { get; set; }
        public double TagAsk { get; set; }
        public double TagTheo { get; set; }
        public double TagVolaV0 { get; set; }
        public double TagVolaV1 { get; set; }
        public double TagVolaV2 { get; set; }
        public double TagEma { get; set; }
        public ulong IoiId { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; }
        public SubType SubTypeId { get; set; }
        public ushort SubTypeSequence { get; set; }
        public double Fee1 { get; set; }
        public double Fee2 { get; set; }
        public string? RouteOverride { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Mid { get; set; }
        public double VolaTheoAdj { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
        public double TV { get; set; }
        public double Delta { get; set; }
        public double ExchangeFee1 { get; set; }
        public double ExchangeFee2 { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public double TotalContracts { get; set; }
        public double FillTime { get; set; } = double.NaN;
        public double TradeToNewTime { get; set; } = double.NaN;
        public double SubmitToNewTime { get; set; } = double.NaN;
        public double NewToCancelTime { get; set; } = double.NaN;
        public double BidPercentOfFillPrice { get; set; }
        public double CloseBidPercentOfFillPrice { get; set; }
        public double OmsBidPercentOfFillPrice { get; set; }
        public double OmsBestBidPercent { get; set; }
        public double TotalDelta { get; set; }
        public double HanweckTotalTheo { get; set; }
        public double HanweckTotalGamma { get; set; }
        public double HanweckTotalVega { get; set; }
        public double HanweckTotalTheta { get; set; }
        public double HanweckTotalRho { get; set; }
        public double HanweckTotalIV { get; set; }
        public double HanweckTotalUnder { get; set; }
        public double HanweckTotalUBid { get; set; }
        public double HanweckTotalUAsk { get; set; }
        public double HanweckTotalBid { get; set; }
        public double HanweckTotalAsk { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        public double CloseTV { get; set; }
        public double CloseDelta { get; set; }
        public double CloseTotalDelta { get; set; }
        public double CloseHanweckTotalTheo { get; set; }
        public double CloseHanweckTotalGamma { get; set; }
        public double CloseHanweckTotalVega { get; set; }
        public double CloseHanweckTotalTheta { get; set; }
        public double CloseHanweckTotalRho { get; set; }
        public double CloseHanweckTotalIV { get; set; }
        public double CloseBid { get; set; }
        public double CloseAsk { get; set; }
        public double CloseUnderBid { get; set; }
        public double CloseUnderAsk { get; set; }
        public double CloseHanweckTotalUnder { get; set; }
        public double CloseHanweckTotalUBid { get; set; }
        public double CloseHanweckTotalUAsk { get; set; }
        public double CloseHanweckTotalBid { get; set; }
        public double CloseHanweckTotalAsk { get; set; }
        public double DeltaAdjustedTheo { get; set; }
        public double VolaTheo { get; set; }
        public double CloseDeltaAdjustedTheo { get; set; }
        public double Ema { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public int CloseBidSize { get; set; }
        public int CloseAskSize { get; set; }
        public int UnderlyingBidSize { get; set; }
        public int UnderlyingAskSize { get; set; }
        public int CloseUnderlyingBidSize { get; set; }
        public int CloseUnderlyingAskSize { get; set; }
        public double LastEdge { get; set; } = double.NaN;
        public double DeltaAdjLastEdge { get; set; } = double.NaN;
        public double DeltaAdjLastEdgeNotional { get; set; } = double.NaN;
        public double DeltaAdjChange { get; set; } = double.NaN;
        public double DeltaAdjChangeNotional { get; set; } = double.NaN;
        public double EdgeScanFeedDeltaAdjPrice { get; set; } = double.NaN;
        public double EdgeOverride { get; set; } = double.NaN;
        public double AdjustedEdgeOverride { get; set; } = double.NaN;
        public double EdgeScanFeedEdge { get; set; } = double.NaN;
        public double EdgeScanFeedTimespan { get; set; } = double.NaN;
        public double EdgeScanFeedBuyPrice { get; set; } = double.NaN;
        public int EdgeScanFeedBuyQty { get; set; }
        public double EdgeScanFeedSellPrice { get; set; } = double.NaN;
        public int EdgeScanFeedSellQty { get; set; }
        public DateTime EdgeScanFeedBuyTime { get; set; }
        public DateTime EdgeScanFeedSellTime { get; set; }
        public double EdgeScanFeedRespondLatency { get; set; } = double.NaN;
        public char EdgeScanFeedConditionCode { get; set; } = '\0';
        public int ResubmitCount { get; set; }
        public int TotalEstimatedResubmit { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
        public string? Guid { get; set; }
        public string? Username { get; set; }
        public string? AccountAcronym { get; set; }
        public string? UnderlyingSymbol { get; set; }
        public string? Currency { get; set; }
        public string? Description { get; set; }
        public string? SpreadId { get; set; }
        public string? Source { get; set; }
        public string? Tag { get; set; }
        public string? Trader { get; set; }
        public string? Type { get; set; }
        public OrderSubType? SubType { get; set; }
        public string? Comment { get; set; }
        public string? SmartRoute { get; set; }
        public string? FullTag { get; set; }
        public string? ExchangeOrderID { get; set; }
        public string? ExecutingBroker { get; set; }
        public string? ExecutionID { get; set; }
        public string? ExecutionReferenceID { get; set; }
        public string? LocalID { get; set; }
        public string? PermID { get; set; }
        public string? OrderID { get; set; }
        public string? OriginalOrderID { get; set; }
        public string? RequestAccountAcronym { get; set; }
        public string? Route { get; set; }
        public string? RequestSymbol { get; set; }
        public string? Destination { get; set; }
        public uint DestinationSequence { get; set; }
        public string? SpreadHash { get; set; }
        public string? RoutingSession { get; set; }
        public string? ClearingFirm { get; set; }
        public string? ClearingID { get; set; }
        public DateTime SubmitTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime NewStatusTimeStamp { get; set; }
        public Side? Side { get; set; }
        public ExecutionType ExecutionType { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public OrderSource OrderSource { get; set; }
        public Security? Security { get; set; }
        public bool IsComplexOrder { get; protected set; }
        public ISecurityBook? SecurityBook { get; }
        public double UnderMid => (UnderAsk + UnderBid) * 0.5;
        public double TotalCommissions => Fee1 + Fee2 + BrokerFee1 + BrokerFee2 + ExchangeFee1 + ExchangeFee2;
        public bool IsFirstFill { get; set; }
        public bool FirstEdgeAcquired { get; set; }
        public double FirstEdge { get; set; }
        public double Multiplier { get; set; }
        public bool IsCitadel { get; set; }
        public Side? CitadelSide { get; set; }
        public PositionEffect? SpreadPositionEffect { get; set; }
        public double OrderEdgeToTheo { get; set; }
        public double EdgeToTheo { get; set; }
        public double TagEdgeToTheo { get; set; }
        public double TagEdgeToEma{ get; set; }
        public double TagEdgeToVolaV0 { get; set; }
        public double TagEdgeToVolaV1 { get; set; }
        public double TagEdgeToVolaV2 { get; set; }
        public double TagBestBid { get; set; }
        public double TagBestAsk { get; set; }
        public double TagMktMkrBid { get; set; }
        public double TagMktMkrAsk { get; set; }
        public int TagVolume { get; set; }
        public int TagFirmVolume { get; set; }
        public double InitialEdge { get; set; }
        public double OpenEdge { get; set; }
        public double CloseEdge { get; set; }
        public bool IsAutomation { get; set; }
        public string? AutomationType { get; set; }
        public EdgeType EdgeType { get; set; }
        public double Edge { get; set; } = double.NaN;
        public double TagUnderBid { get; set; } = double.NaN;
        public double TagUnderAsk { get; set; } = double.NaN;
        public double DigBid { get; set; } = double.NaN;
        public double DigAsk { get; set; } = double.NaN;
        public uint DigBidSize { get; set; }
        public uint DigAskSize { get; set; }
        public double WeightedVega { get; set; } = double.NaN;
        public double LoopInitLatency { get; set; } = double.NaN;
        public bool IsDeltaAdjusted { get; set; }
        public bool IsFill => ExecutionType is ExecutionType.Filled or ExecutionType.PartiallyFilled or ExecutionType.Trade;
        public bool IsTagged { get; set; }
        public string? Tagger { get; set; }
        public string? TaggedMessage { get; set; }
        public Side? HardSide { get; set; }
        public DateTime HardSideDesignationTime { get; set; }
        public double HardSideBuyGiveUp { get; set; }
        public double HardSideSellGiveUp { get; set; }
        public Side? HardSideAtTrade { get; set; }
        public DateTime HardSideAtTradeDesignationTime { get; set; }
        public double HardSideAtTradeBuyGiveUp { get; set; }
        public double HardSideAtTradeSellGiveUp { get; set; }
        public double CostOfHedging { get; set; }
        public OrderTagModel? OrderTag { get; set; }
        public OrderType OrderType { get; set; } = OrderType.Limit;
        public string? LastExchange { get; set; }
        public bool SkipNewPriceEvaluation { get; set; }
        public string? Reason { get; set; }
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public StockHedgeOrderModel? StockHedgeOrderModel { get; set; }
        public string? Exchanges { get; set; }
        public IList<ContraCapacity>? ContraCapacities { get; set; }
        public IList<ContraBrokerName>? ContraBrokerNames { get; set; }
        public IList<ContraCmta>? ContraCmtas { get; set; }
        public IList<ContraTrader>? ContraTraders { get; set; }
        public string? Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                if (!IsComplexOrder && !string.IsNullOrEmpty(value))
                {
                    Security = SecurityBook?.GetSecurity(value);
                }
            }
        }

        public Order()
        {
        }

        public Order(ISecurityBook? securityBook)
        {
            SecurityBook = securityBook;
        }

        /// <summary>
        /// Method to append the LastExchange to the Exchanges string if it's not already present.
        /// This should be called after LastExchange is set.
        /// </summary>
        public static IOrder UpdateExchangesOnLastExchangeUpdate(IOrder order)
        {
            if (!string.IsNullOrEmpty(order.LastExchange))
            {
                if (order.Exchanges == null)
                    order.Exchanges = order.LastExchange;
                else if (!order.Exchanges.Contains(order.LastExchange))
                {
                    order.Exchanges += "," + order.LastExchange;
                }
            }
            return order;
        }
    }
}
