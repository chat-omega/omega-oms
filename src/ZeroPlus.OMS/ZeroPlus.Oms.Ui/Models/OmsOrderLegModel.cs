using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Ui.Models
{
    public class OmsOrderLegModel : BindableBase, IComplexOrderLeg
    {
        private string _symbol;

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
        public OrderStatus OrderStatus { get; set; }
        public double UTheo { get; set; }
        public Side? Side { get; set; }
        public int Quantity { get; set; }
        public int Ratio { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public string LegID { get; set; }
        public string ExecutionID { get; set; }
        public string OrderID { get; set; }
        public string PermID { get; set; }
        public string LastExchange { get; set; }
        public int TransactionID { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastUpdateTime { get; set; }
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
        public double DeltaAdjustedTheo { get; set; }
        public double Ema { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
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
        public ZeroPlus.Models.Data.Securities.Security Security { get; set; }
        public ISecurityBook SecurityBook { get; }
        public double TotalCommissions { get; }
        public IList<ContraCapacity> ContraCapacities { get; set; }
        public IList<ContraBrokerName> ContraBrokerNames { get; set; }
        public IList<ContraCmta> ContraCmtas { get; set; }
        public IList<ContraTrader> ContraTraders { get; set; }
        public string Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                if (SecurityBook != null && !string.IsNullOrEmpty(_symbol))
                {
                    Security = SecurityBook.GetSecurity(_symbol);
                }
            }
        }

        public OmsOrderLegModel(ISecurityBook securityBook)
        {
            SecurityBook = securityBook;
        }

        public void Clone(IComplexOrderLeg other)
        {
            ExchangeFee2 = other.ExchangeFee2;
            ExchangeFee1 = other.ExchangeFee1;
            Fee2 = other.Fee2;
            Fee1 = other.Fee1;
            Delta = other.Delta;
            TV = other.TV;
            Ask = other.Ask;
            Bid = other.Bid;
            AveragePrice = other.AveragePrice;
            CumulativeQuantity = other.CumulativeQuantity;
            LastQuantity = other.LastQuantity;
            LeavesQuantity = other.LeavesQuantity;
            LastPrice = other.LastPrice;
            OrderStatus = other.OrderStatus;
            UTheo = other.UTheo;
            Side = other.Side;
            Quantity = other.Quantity;
            Ratio = other.Ratio;
            PositionEffect = other.PositionEffect;
            LegID = other.LegID;
            ExecutionID = other.ExecutionID;
            OrderID = other.OrderID;
            PermID = other.PermID;
            LastExchange = other.LastExchange;
            TransactionID = other.TransactionID;
            Timestamp = other.Timestamp;
            LastUpdateTime = other.LastUpdateTime;
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
            DeltaAdjustedTheo = other.DeltaAdjustedTheo;
            Ema = other.Ema;
            BidSize = other.BidSize;
            AskSize = other.AskSize;
            MinimumTickStyle = other.MinimumTickStyle;
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
            Security = other.Security;
        }

        internal void Update(OmsOrderLeg leg)
        {
            ExchangeFee2 = leg.ExchangeFee2;
            ExchangeFee1 = leg.ExchangeFee1;
            Fee2 = leg.Fee2;
            Fee1 = leg.Fee1;
            AveragePrice = leg.AveragePrice;
            CumulativeQuantity = leg.CumulativeQuantity;
            LastQuantity = leg.LastQuantity;
            Side = leg.Side;
            LeavesQuantity = leg.LeavesQuantity;
            LastPrice = leg.LastPrice;
            Quantity = leg.Quantity;
            Ratio = leg.Ratio;
            Symbol = leg.Symbol;
            LegID = leg.LegID;
            ExecutionID = leg.ExecutionID;
            OrderID = leg.OrderID;
            PermID = leg.PermID;
            TransactionID = leg.TransactionID;
            Timestamp = leg.Timestamp;
            LastUpdateTime = leg.LastUpdateTime;
            BrokerFee1 = leg.BrokerFee1;
            BrokerFee2 = leg.BrokerFee2;
            Delta = Math.Round(leg.Delta, 2, MidpointRounding.AwayFromZero);
            TV = Math.Round(leg.TV, 2, MidpointRounding.AwayFromZero);
            Ask = Math.Round(leg.Ask, 2, MidpointRounding.AwayFromZero);
            Bid = Math.Round(leg.Bid, 2, MidpointRounding.AwayFromZero);
            HanweckTimestamp = leg.HanweckTimestamp;
            Delta = leg.Delta;
            HanweckTV = leg.HanweckTV;
            HanweckGamma = leg.HanweckGamma;
            HanweckVega = leg.HanweckVega;
            HanweckTheta = leg.HanweckTheta;
            HanweckRho = leg.HanweckRho;
            HanweckIV = leg.HanweckIV;
            HanweckUnder = leg.HanweckUnder;
            HanweckUnderBid = leg.HanweckUnderBid;
            HanweckUnderAsk = leg.HanweckUnderAsk;
            HanweckBid = leg.HanweckBid;
            HanweckAsk = leg.HanweckAsk;
            HanweckBidTime = leg.HanweckBidTime;
            DeltaAdjustedTheo = leg.DeltaAdjustedTheo;
            BidSize = leg.BidSize;
            AskSize = leg.AskSize;
            HanweckAskTime = leg.HanweckAskTime;
            Side = leg.Side;
        }
    }
}