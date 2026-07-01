
namespace ZeroPlus.Comms.Models.Data.MarketData
{
    public class MDUnderlying
    {
        public string? Symbol { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Last { get; set; }
    }

    public class MDQuote
    {
        public string? Symbol { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double BidSize { get; set; }
        public double AskSize { get; set; }
        public double Last { get; set; }
        public double Theo { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
        public double Vega { get; set; }
        public double Theta { get; set; }
    }
}
