using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.Data.Trading
{
    [Serializable]
    public class OmsOrderLeg : IOmsOrderLeg
    {
        public double ExchangeFee2 { get; set; }
        public double ExchangeFee1 { get; set; }
        public double Fee2 { get; set; }
        public double Fee1 { get; set; }
        public double Delta { get; set; }
        public double TV { get; set; }
        public double Ask { get; set; }
        public double Bid { get; set; }
        public double AveragePrice { get; set; }
        public int CumulativeQuantity { get; set; }
        public int LastQuantity { get; set; }
        public int LeavesQuantity { get; set; }
        public double LastPrice { get; set; }
        public string OrderStatus { get; set; }
        public Side? Side { get; set; }
        public int Quantity { get; set; }
        public int Ratio { get; set; }
        public string PositionEffect { get; set; }
        public string Symbol { get; set; }
        public string LegID { get; set; }
        public string ExecutionID { get; set; }
        public string OrderID { get; set; }
        public string PermID { get; set; }
        public int TransactionID { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public DateTime HanweckTimestamp { get; set; }
        public double HanweckTV { get; set; }
        public double HanweckGamma { get; set; }
        public double HanweckVega { get; set; }
        public double HanweckTheta { get; set; }
        public double HanweckRho { get; set; }
        public double HanweckIV { get; set; }
        public double HanweckUnder { get; set; }
        public double HanweckUnderBid { get; set; }
        public double HanweckUnderAsk { get; set; }
        public double HanweckBid { get; set; }
        public double HanweckAsk { get; set; }
        public DateTime HanweckBidTime { get; set; }
        public DateTime HanweckAskTime { get; set; }
        public Option Security { get; set; }
        public double DeltaAdjustedTheo { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }

        public double TotalCommissions => Fee1 + Fee2 + BrokerFee1 + BrokerFee2 + ExchangeFee1 + ExchangeFee2;
    }
}
