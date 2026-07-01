using System;

namespace ZeroPlus.Models.Data.Update
{
    public class DerivedValueUpdateModel
    {
        public int TickerId { get; set; }
        public uint UpdateSequence { get; set; }
        public double InterpolatedBidUpdate { get; set; }
        public double InterpolatedAskUpdate { get; set; }
        public double BestBidUpdate { get; set; }
        public double BestAskUpdate { get; set; }
        public double BestBidBase { get; set; }
        public double BestAskBase { get; set; }
        public double BestBidUnderlying { get; set; }
        public double BestAskUnderlying { get; set; }
        public double BidTradeUpdate { get; set; }
        public double AskTradeUpdate { get; set; }
        public double BidTradeBase { get; set; }
        public double AskTradeBase { get; set; }
        public double BidTradeUnderlying { get; set; }
        public double AskTradeUnderlying { get; set; }
        public DateTime BidTradeTimestamp { get; set; }
        public DateTime AskTradeTimestamp { get; set; }
        public int BidTradeCount { get; set; }
        public int AskTradeCount { get; set; }
        public bool BidTradeIsLatest { get; set; }
        public bool AskTradeIsLatest { get; set; }

        public int CustTradeBidCount { get; set; }
        public int CustTradeAskCount { get; set; }
        public double CustTradeBid { get; set; }
        public double CustTradeAsk { get; set; }
        public double CustTradeBidBase { get; set; }
        public double CustTradeAskBase { get; set; }
        public double CustTradeBidNoChange { get; set; }
        public double CustTradeAskNoChange { get; set; }
        public double CustTradeBidBaseNoChange { get; set; }
        public double CustTradeAskBaseNoChange { get; set; }
        public double CustTradeBidAvgChange { get; set; }
        public double CustTradeAskAvgChange { get; set; }
        public double CustTradeBidUnderlyingPrice { get; set; }
        public double CustTradeAskUnderlyingPrice { get; set; }
        public double ImpliedBid { get; set; }
        public double ImpliedAsk { get; set; }
        public double ImpliedBidRecord { get; set; }
        public double ImpliedAskRecord { get; set; }
        public DateTime ImpliedBidRecordTimestamp { get; set; }
        public double ImpliedBidRecordTheo { get; set; }
        public double ImpliedBidRecordTheoMovement { get; set; }
        public double ImpliedBidRecordNonDeltaMovement { get; set; }
        public DateTime ImpliedAskRecordTimestamp { get; set; }
        public double ImpliedAskRecordTheo { get; set; }
        public double ImpliedAskRecordTheoMovement { get; set; }
        public double ImpliedAskRecordNonDeltaMovement { get; set; }
        public bool CustBidTradeIsLatest { get; set; }
        public bool CustAskTradeIsLatest { get; set; }
        public DateTime CustBidTradeTimestamp { get; set; }
        public DateTime CustAskTradeTimestamp { get; set; }
        public HighestBidLowestAskResult? HighestBidLowestAskResult { get; set; } = new();
        public HighestBidLowestAskResult? HighestBidLowestAskResultLong { get; set; } = new();
    }
}
