using System;

namespace ZeroPlus.Models.Data.Responses
{
    public class OptionSnapshot
    {
        public string? Symbol { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
        public double AdjTheo { get; set; }
        public double Theo { get; set; }
        public double Delta { get; set; }
        public double Vega { get; set; }
        public double Iv { get; set; }
        public DateTime QuoteTime { get; set; }
        public DateTime SnapshotTime { get; set; }
        public DateTime HanweckCalcTime { get; set; }
        public DateTime AdjTheoTime { get; set; }
        public double UnderMid => (UnderBid + UnderAsk) / 2;
    }
}
