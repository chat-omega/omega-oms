namespace ZeroPlus.Models.Data.Update
{
    public class DerivedValueUpdateModelContainer
    {
        public object Lock = new object();

        public DerivedValueUpdateModel Working { get; } = new DerivedValueUpdateModel();
        public DerivedValueUpdateModel Final { get; } = new DerivedValueUpdateModel();

        public void Copy()
        {
            lock (Lock)
            {
                Final.TickerId = Working.TickerId;
                Final.UpdateSequence = Working.UpdateSequence;
                Final.InterpolatedBidUpdate = Working.InterpolatedBidUpdate;
                Final.InterpolatedAskUpdate = Working.InterpolatedAskUpdate;
                Final.BestBidUpdate = Working.BestBidUpdate;
                Final.BestAskUpdate = Working.BestAskUpdate;
                Final.BestBidBase = Working.BestBidBase;
                Final.BestAskBase = Working.BestAskBase;
                Final.BestBidUnderlying = Working.BestBidUnderlying;
                Final.BestAskUnderlying = Working.BestAskUnderlying;
                Final.BidTradeUpdate = Working.BidTradeUpdate;
                Final.AskTradeUpdate = Working.AskTradeUpdate;
                Final.BidTradeBase = Working.BidTradeBase;
                Final.AskTradeBase = Working.AskTradeBase;
                Final.BidTradeUnderlying = Working.BidTradeUnderlying;
                Final.AskTradeUnderlying = Working.AskTradeUnderlying;
                Final.BidTradeTimestamp = Working.BidTradeTimestamp;
                Final.AskTradeTimestamp = Working.AskTradeTimestamp;
                Final.BidTradeCount = Working.BidTradeCount;
                Final.AskTradeCount = Working.AskTradeCount;
                Final.BidTradeIsLatest = Working.BidTradeIsLatest;
                Final.AskTradeIsLatest = Working.AskTradeIsLatest;
                Final.CustTradeBidCount = Working.CustTradeBidCount;
                Final.CustTradeAskCount = Working.CustTradeAskCount;
                Final.CustTradeBid = Working.CustTradeBid;
                Final.CustTradeAsk = Working.CustTradeAsk;
                Final.CustTradeBidBase = Working.CustTradeBidBase;
                Final.CustTradeAskBase = Working.CustTradeAskBase;
                Final.CustTradeBidNoChange = Working.CustTradeBidNoChange;
                Final.CustTradeAskNoChange = Working.CustTradeAskNoChange;
                Final.CustTradeBidBaseNoChange = Working.CustTradeBidBaseNoChange;
                Final.CustTradeAskBaseNoChange = Working.CustTradeAskBaseNoChange;
                Final.CustTradeBidAvgChange = Working.CustTradeBidAvgChange;
                Final.CustTradeAskAvgChange = Working.CustTradeAskAvgChange;
                Final.CustTradeBidUnderlyingPrice = Working.CustTradeBidUnderlyingPrice;
                Final.CustTradeAskUnderlyingPrice = Working.CustTradeAskUnderlyingPrice;
                Final.CustBidTradeIsLatest = Working.CustBidTradeIsLatest;
                Final.CustAskTradeIsLatest = Working.CustAskTradeIsLatest;
                Final.CustBidTradeTimestamp = Working.CustBidTradeTimestamp;
                Final.CustAskTradeTimestamp = Working.CustAskTradeTimestamp;
                Final.ImpliedBid = Working.ImpliedBid;
                Final.ImpliedAsk = Working.ImpliedAsk;
                Final.ImpliedBidRecord = Working.ImpliedBidRecord;
                Final.ImpliedAskRecord = Working.ImpliedAskRecord;
                Final.ImpliedBidRecordTimestamp = Working.ImpliedBidRecordTimestamp;
                Final.ImpliedBidRecordTheo = Working.ImpliedBidRecordTheo;
                Final.ImpliedBidRecordTheoMovement = Working.ImpliedBidRecordTheoMovement;
                Final.ImpliedBidRecordNonDeltaMovement = Working.ImpliedBidRecordNonDeltaMovement;
                Final.ImpliedAskRecordTimestamp = Working.ImpliedAskRecordTimestamp;
                Final.ImpliedAskRecordTheo = Working.ImpliedAskRecordTheo;
                Final.ImpliedAskRecordTheoMovement = Working.ImpliedAskRecordTheoMovement;
                Final.ImpliedAskRecordNonDeltaMovement = Working.ImpliedAskRecordNonDeltaMovement;
                Final.HighestBidLowestAskResult?.Update(Working.HighestBidLowestAskResult);
                Final.HighestBidLowestAskResultLong?.Update(Working.HighestBidLowestAskResultLong);
            }
        }
    }
}
