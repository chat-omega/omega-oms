using System;

namespace ZeroPlus.Models.Data.Models
{
    public class OpraDatabaseTradeModel
    {
        public DateTime MinTime { get; set; }
        public DateTime MaxTime { get; set; }
        public string UnderSymbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public int LegCount { get; set; }
        public string SpreadType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public double UnderPrice { get; set; }
        public double MinTUE { get; set; }
        public double MinBid { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Price { get; set; }
        public double MidMarket { get; set; }
        public double AboveMid { get; set; }
        public double TradeDelta { get; set; }
        public DateTime SQLTime { get; set; }
        public long SpreadID { get; set; }
        public bool UnsureSymbol { get; set; }
        public DateTime TradeTime { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
        public double UnderLast { get; set; }
        public double HWTV { get; set; }
        public DateTime HWTime { get; set; }
        public char Cond1 { get; set; } = '\0';
        public char Cond2 { get; set; } = '\0';
        public char Cond3 { get; set; } = '\0';
        public double DeltaAdjTheo { get; set; }
        public DateTime DeltaAdjTime { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public double HWTheta { get; set; }
        public double HWVega { get; set; }
        public double HWGamma { get; set; }
        public double HWRho { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        public double HWIV { get; set; }
        public double VolaTV { get; set; }
        public double VolaDeltaAdjTheo { get; set; }
        public double VolaIV { get; set; }
        public bool IsFirm { get; set; }
        public string FirmSide { get; set; } = string.Empty;
        public double DeltaAdjEdge { get; set; }
        public DateTime DeltaAdjEdgeRefTime { get; set; }
        public IoiModel? IoiModel { get; set; }

        #region FOR OMS use
        public string Description { get; set; } = string.Empty;
        public string SpreadId { get; set; } = string.Empty;
        public string SpreadTypeOms { get; set; } = string.Empty;
        public DateTime ExpirationOne { get; set; }
        public DateTime ExpirationTwo { get; set; }
        public DateTime ExpirationThree { get; set; }
        public double PriceRange { get; set; } = double.NaN;
        public double AdjPriceRange { get; set; } = double.NaN;
        public int Count { get; set; }
        public double PriceChange { get; set; }
        public string PriceChanges { get; set; } = string.Empty;
        public string TickStyle { get; set; } = string.Empty;
        public double FirmAttemptOffset { get; set; } = double.NaN;
        public string FirmAttemptSummary { get; set; } = "";
        public TradesLowHighEdgeModel TradesLowHighEdgeModel { get; set; } = new();
        public bool ShowIndicator { get; set; }
        public bool BuyIndicator { get; set; }
        public double DeltaAdjustedPrice { get; set; }
        public double Strike { get; set; }
        public double SpacingOne { get; set; }
        public double SpacingTwo { get; set; }
        public double SpacingThree { get; set; }
        public double StrikeSpacing { get; set; }
        public double ExpSpacing { get; set; }
        public double DaysToExp { get; set; }
        public double AdjustedPrice { get; set; }
        public int Position { get; set; }
        public double AdjustedPnl { get; set; }
        public double Last { get; set; }
        public double HanweckTheo { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
        public double Rho { get; set; }
        public double Implied { get; set; }
        public double Theo { get; set; }
        public string TwsMessage { get; set; } = string.Empty;

        public double Width => System.Math.Abs(Ask - Bid);
        public string HanweckTime => HWTime.ToString("hh:mm:ss.ffff");
        public string DeltaAdjTheoTime => DeltaAdjTime.ToString("hh:mm:ss.ffff");
        public double EdgeToTheo => DeltaAdjTheo > Price ? DeltaAdjTheo - Price : Price - DeltaAdjTheo;
        public double VolaEdgeToTheo => VolaDeltaAdjTheo > Price ? VolaDeltaAdjTheo - Price : Price - VolaDeltaAdjTheo;
        public double MinEdgeToTheo => System.Math.Min(EdgeToTheo, VolaEdgeToTheo);
        public double AbsTheoDiff => System.Math.Abs(System.Math.Abs(VolaDeltaAdjTheo) - System.Math.Abs(DeltaAdjTheo));
        public double PercentBid => System.Math.Round((Bid - Price) / (Bid - Ask), 2, MidpointRounding.AwayFromZero);
        #endregion

        public OpraDatabaseTradeModel() { }

        public OpraDatabaseTradeModel(OpraDatabaseTradeModel trade)
        {
            MinTime = trade.MinTime;
            MaxTime = trade.MaxTime;
            UnderSymbol = trade.UnderSymbol;
            Exchange = trade.Exchange;
            Condition = trade.Condition;
            LegCount = trade.LegCount;
            SpreadType = trade.SpreadType;
            Quantity = trade.Quantity;
            Symbol = trade.Symbol;
            UnderPrice = trade.UnderPrice;
            MinTUE = trade.MinTUE;
            MinBid = trade.MinBid;
            Bid = trade.Bid;
            Ask = trade.Ask;
            Price = trade.Price;
            MidMarket = trade.MidMarket;
            AboveMid = trade.AboveMid;
            TradeDelta = trade.TradeDelta;
            SQLTime = trade.SQLTime;
            SpreadId = trade.SpreadId;
            UnsureSymbol = false;
            TradeTime = trade.TradeTime;
            UnderBid = trade.UnderBid;
            UnderAsk = trade.UnderAsk;
            UnderLast = trade.UnderLast;
            HWTV = trade.HWTV;
            HWTime = trade.HWTime;
            Cond1 = trade.Cond1;
            Cond2 = trade.Cond2;
            Cond3 = trade.Cond3;
            DeltaAdjTheo = trade.DeltaAdjTheo;
            DeltaAdjTime = trade.DeltaAdjTime;
            BidSize = trade.BidSize;
            AskSize = trade.AskSize;
            HWTheta = trade.HWTheta;
            HWVega = trade.HWVega;
            HWGamma = trade.HWGamma;
            HWRho = trade.HWRho;
            TimeValue = trade.TimeValue;
            IntrinsicValue = trade.IntrinsicValue;
            FVDivs = trade.FVDivs;
            UFwd = trade.UFwd;
            UFwdFactor = trade.UFwdFactor;
            BorrowCost = trade.BorrowCost;
            BorrowRate = trade.BorrowRate;
            UPrice = trade.UPrice;
            UTheo = trade.UTheo;
            HWIV = trade.HWIV;
            VolaTV = trade.VolaTV;
            VolaDeltaAdjTheo = trade.VolaDeltaAdjTheo;
            VolaIV = trade.VolaIV;
            IsFirm = trade.IsFirm;
            FirmSide = trade.FirmSide;
            DeltaAdjEdge = trade.DeltaAdjEdge;
            DeltaAdjEdgeRefTime = trade.DeltaAdjEdgeRefTime;
            IoiModel = trade.IoiModel;
        }

        public OpraDatabaseTradeModel Clone()
        {
            return new OpraDatabaseTradeModel(this);
        }
    }
}
