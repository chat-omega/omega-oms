using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading
{
    public class ComplexOrderLeg : IComplexOrderLeg
    {
        private string? _symbol;

        public int Ratio { get; set; }
        public int Quantity { get; set; }
        public int LastQuantity { get; set; }
        public int TransactionID { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public double ExchangeFee2 { get; set; }
        public double ExchangeFee1 { get; set; }
        public double Fee2 { get; set; }
        public double Fee1 { get; set; }
        public double Delta { get; set; }
        public double TV { get; set; }
        public double Ask { get; set; }
        public double Bid { get; set; }
        public double AveragePrice { get; set; }
        public double LastPrice { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
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
        public double VolaTheo { get; set; }
        public double VolaTheoAdj { get; set; }
        public double VolaIv { get; set; }
        public double TheoBid { get; set; }
        public double TheoAsk { get; set; }
        public double Ema { get; set; }
        public string? LegID { get; set; }
        public string? ExecutionID { get; set; }
        public string? OrderID { get; set; }
        public string? PermID { get; set; }
        public string? LastExchange { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime HanweckBidTime { get; set; }
        public DateTime HanweckAskTime { get; set; }
        public DateTime HanweckTimestamp { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        public Side? Side { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public Security? Security { get; set; }
        public double DeltaAdjustedTheo { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
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
                if (!string.IsNullOrEmpty(value))
                {
                    Security = SecurityBook?.GetSecurity(value);
                }
            }
        }
        public ISecurityBook? SecurityBook { get; }
        public double TotalCommissions => Fee1 + Fee2 + BrokerFee1 + BrokerFee2 + ExchangeFee1 + ExchangeFee2;

        public ComplexOrderLeg(ISecurityBook? securityBook)
        {
            SecurityBook = securityBook;
        }

        public void Clone(IComplexOrderLeg other)
        {
            Ratio = other.Ratio;
            Quantity = other.Quantity;
            LastQuantity = other.LastQuantity;
            TransactionID = other.TransactionID;
            LeavesQuantity = other.LeavesQuantity;
            CumulativeQuantity = other.CumulativeQuantity;
            ExchangeFee2 = other.ExchangeFee2;
            ExchangeFee1 = other.ExchangeFee1;
            Fee2 = other.Fee2;
            Fee1 = other.Fee1;
            Delta = other.Delta;
            TV = other.TV;
            Ask = other.Ask;
            Bid = other.Bid;
            AveragePrice = other.AveragePrice;
            LastPrice = other.LastPrice;
            BrokerFee1 = other.BrokerFee1;
            BrokerFee2 = other.BrokerFee2;
            HanweckTV = other.HanweckTV;
            HanweckGamma = other.HanweckGamma;
            HanweckVega = other.HanweckVega;
            HanweckTheta = other.HanweckTheta;
            HanweckRho = other.HanweckRho;
            HanweckIV = other.HanweckIV;
            HanweckUnder = other.HanweckUnder;
            HanweckUnderBid = other.HanweckUnderBid;
            HanweckUnderAsk = other.HanweckUnderAsk;
            HanweckBid = other.HanweckBid;
            HanweckAsk = other.HanweckAsk;
            VolaTheo = other.VolaTheo;
            VolaTheoAdj = other.VolaTheoAdj;
            VolaIv = other.VolaIv;
            TheoBid = other.TheoBid;
            TheoAsk = other.TheoAsk;
            Ema = other.Ema;
            LegID = other.LegID;
            ExecutionID = other.ExecutionID;
            OrderID = other.OrderID;
            PermID = other.PermID;
            LastExchange = other.LastExchange;
            Timestamp = other.Timestamp;
            LastUpdateTime = other.LastUpdateTime;
            HanweckBidTime = other.HanweckBidTime;
            HanweckAskTime = other.HanweckAskTime;
            HanweckTimestamp = other.HanweckTimestamp;
            TimeValue = other.TimeValue;
            IntrinsicValue = other.IntrinsicValue;
            FVDivs = other.FVDivs;
            UFwd = other.UFwd;
            UFwdFactor = other.UFwdFactor;
            BorrowCost = other.BorrowCost;
            BorrowRate = other.BorrowRate;
            UPrice = other.UPrice;
            UTheo = other.UTheo;
            Side = other.Side;
            OrderStatus = other.OrderStatus;
            PositionEffect = other.PositionEffect;
            Security = other.Security;
            DeltaAdjustedTheo = other.DeltaAdjustedTheo;
            BidSize = other.BidSize;
            AskSize = other.AskSize;
            MinimumTickStyle = other.MinimumTickStyle;
            ContraCapacities = other.ContraCapacities is { } cc ? new List<ContraCapacity>(cc) : null;
            ContraBrokerNames = other.ContraBrokerNames is { } cbn ? new List<ContraBrokerName>(cbn) : null;
            ContraCmtas = other.ContraCmtas is { } ccm ? new List<ContraCmta>(ccm) : null;
            ContraTraders = other.ContraTraders is { } ct ? new List<ContraTrader>(ct) : null;
            Symbol = other.Symbol;
        }
    }
}