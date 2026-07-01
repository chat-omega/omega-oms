using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Trading
{
    public class OrderInfoUpdate
    {
        public int SpreadNumLegs { get; set; }
        public int SpreadLegCount { get; set; }
        public int Minmove { get; set; }
        public int RemainingVolume { get; set; }
        public int OrderResidual { get; set; }
        public int VolumeTraded { get; set; }
        public int SpreadLegNumber { get; set; }
        public int PairImbalanceLimitType { get; set; }
        public int UtcOffset { get; set; }
        public int OriginalVolume { get; set; }
        public int Volume { get; set; }
        public int WorkingQty { get; set; }

        public Side Side { get; set; }
        public OrderType OrderType { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public OrderStatus OrderStatus { get; set; }

        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Price { get; set; }
        public double PairTarget { get; set; }
        public double PairLeg2Benchmark { get; set; }
        public double PairLeg1Benchmark { get; set; }
        public double PairImbalanceLimit { get; set; }
        public double PairCash { get; set; }
        public double PairRatio { get; set; }
        public double Latency6 { get; set; }
        public double Latency3 { get; set; }
        public double Basisvalue { get; set; }
        public double OriginalPrice { get; set; }
        public double StrikePrc { get; set; }
        public double StopPrice { get; set; }
        public double ServerArrivalPrice { get; set; }

        public DateTime TimeStamp { get; set; }
        public DateTime SubmitTime { get; set; }
        public DateTime NewsDate { get; set; }
        public DateTime ExpirDate { get; set; }
        public DateTime NewsTime { get; set; }
        public DateTime TrdTime { get; set; }

        public string? BookingType { get; set; }
        public string? RefMgrNotes { get; set; }
        public string? PairSpreadType { get; set; }
        public string? Reason { get; set; }
        public string? LinkedOrderCancellation { get; set; }
        public string? LinkedOrderRelationship { get; set; }
        public string? CommissionRateType { get; set; }
        public string? Account { get; set; }
        public string? Route { get; set; }
        public string? OrderId { get; set; }
        public string? LinkedOrderId { get; set; }
        public string? RefersToId { get; set; }
        public string? TicketId { get; set; }
        public string? OriginalOrderId { get; set; }
        public string? Symbol { get; set; }
        public string? Type { get; set; }
        public string? CurrentStatus { get; set; }
        public string? TraderId { get; set; }
        public string? ClaimedByClerk { get; set; }
        public string? SpreadLegPriceType { get; set; }
        public string? SpreadLegLeanPriority { get; set; }
        public string? OrderFlags { get; set; }
        public string? FornexSourceFlags { get; set; }
        public string? ExternalAcceptanceFlag { get; set; }
        public string? ExtendedStateFlags2 { get; set; }
        public string? ExtendedStateFlags { get; set; }
        public string? CrossFlag { get; set; }
        public string? SpreadClipType { get; set; }
        public string? PairLeg2BenchmarkType { get; set; }
        public string? PairLeg1BenchmarkType { get; set; }
        public string? SharesAllocated { get; set; }
        public string? OrderFlags2 { get; set; }
        public string? AcctType { get; set; }
        public string? Rank { get; set; }
        public string? GwBookSeqNo { get; set; }
        public string? DateIndex { get; set; }
        public string? BookId { get; set; }
        public string? TboAccountId { get; set; }
        public string? OmsClientType { get; set; }
        public string? ExecutionState { get; set; }
        public string? Styp { get; set; }
        public string? TradeTime { get; set; }
        public string? CommissionCode { get; set; }
        public string? ShortLocateId { get; set; }
        public string? Undersym { get; set; }
        public string? Putcallind { get; set; }
        public string? UserMessage { get; set; }
        public string? OppositeParty { get; set; }
        public string? Currency { get; set; }
        public string? DispName { get; set; }
        public string? Deposit { get; set; }
        public string? Customer { get; set; }
        public string? Branch { get; set; }
        public string? Bank { get; set; }
        public string? GoodFrom { get; set; }
        public string? RemoteId { get; set; }
        public string? OriginalTraderId { get; set; }
        public string? ClientOrderId { get; set; }
        public string? NewRemoteId { get; set; }
        public string? PriceType { get; set; }
        public string? VolumeType { get; set; }
        public string? GoodUntil { get; set; }
        public string? Buyorsell { get; set; }
        public string? ExitVehicle { get; set; }
        public string? Table { get; set; }
        public string? TraderCapacity { get; set; }
        public string? FixTraderId { get; set; }
        public string? Exchange { get; set; }
        public string? OrderTag { get; set; }
        public string? CommissionRate { get; set; }
        public string? Commission { get; set; }
        public string? AvgPrice { get; set; }
        public string? PairSpread { get; set; }
        public string? AllocatedValue { get; set; }
        public string? EcnFee { get; set; }
        public string? SpreadClip { get; set; }
        public string? ServerTimeZone { get; set; }

        public List<OrderInfoUpdate> ChildOrderInfoUpdates { get; } = new List<OrderInfoUpdate>();
    }
}
