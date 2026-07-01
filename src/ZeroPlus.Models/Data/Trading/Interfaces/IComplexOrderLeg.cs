using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;

namespace ZeroPlus.Models.Data.Trading.Interfaces
{
    public interface IComplexOrderLeg : IComplexOrderLegMin
    {
        int Ratio { get; set; }
        int Quantity { get; set; }
        int LastQuantity { get; set; }
        int TransactionID { get; set; }
        int LeavesQuantity { get; set; }
        int CumulativeQuantity { get; set; }
        double ExchangeFee2 { get; set; }
        double ExchangeFee1 { get; set; }
        double Fee2 { get; set; }
        double Fee1 { get; set; }
        double Delta { get; set; }
        double TV { get; set; }
        double Ask { get; set; }
        double Bid { get; set; }
        double AveragePrice { get; set; }
        double LastPrice { get; set; }
        double BrokerFee1 { get; set; }
        double BrokerFee2 { get; set; }
        double HanweckTV { get; set; }
        double HanweckGamma { get; set; }
        double HanweckVega { get; set; }
        double HanweckTheta { get; set; }
        double HanweckRho { get; set; }
        double HanweckIV { get; set; }
        double HanweckUnder { get; set; }
        double HanweckUnderBid { get; set; }
        double HanweckUnderAsk { get; set; }
        double HanweckBid { get; set; }
        double HanweckAsk { get; set; }
        double VolaTheo { get; set; }
        double VolaTheoAdj { get; set; }
        double VolaIv { get; set; }
        double TheoBid { get; set; }
        double TheoAsk { get; set; }
        double Ema { get; set; }
        string? LegID { get; set; }
        string? ExecutionID { get; set; }
        string? OrderID { get; set; }
        string? PermID { get; set; }
        string? LastExchange { get; set; }
        DateTime Timestamp { get; set; }
        DateTime LastUpdateTime { get; set; }
        DateTime HanweckBidTime { get; set; }
        DateTime HanweckAskTime { get; set; }
        DateTime HanweckTimestamp { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        Side? Side { get; set; }
        OrderStatus OrderStatus { get; set; }
        PositionEffect PositionEffect { get; set; }
        Security? Security { get; set; }
        string? Symbol { get; set; }
        ISecurityBook? SecurityBook { get; }
        double TotalCommissions { get; }
        double DeltaAdjustedTheo { get; set; }
        int BidSize { get; set; }
        int AskSize { get; set; }
        MinimumTickStyle MinimumTickStyle { get; set; }
        IList<ContraCapacity>? ContraCapacities { get; set; }
        IList<ContraBrokerName>? ContraBrokerNames { get; set; }
        IList<ContraCmta>? ContraCmtas { get; set; }
        IList<ContraTrader>? ContraTraders { get; set; }

        void Clone(IComplexOrderLeg other);
    }
}