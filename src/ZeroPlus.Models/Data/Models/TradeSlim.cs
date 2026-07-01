using System;

namespace ZeroPlus.Models.Data.Models
{
    public class TradeSlim
    {
        public string Symbol { get; set; } = string.Empty;
        public string UnderSymbol { get; set; } = string.Empty;
        public int LegCount { get; set; }
        public string Exchange { get; set; } = string.Empty;
        public char Condition { get; set; }

        public DateTime TradeTime { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public double TradeDelta { get; set; }
        public bool UnsureSymbol { get; set; }

        public double Bid { get; set; }
        public double Ask { get; set; }
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }

        public double DeltaAdjTheo { get; set; }
        public double VolaDeltaAdjTheo { get; set; }

        public double ImpliedVol { get; set; }
        public double Vega { get; set; }
    }
}
