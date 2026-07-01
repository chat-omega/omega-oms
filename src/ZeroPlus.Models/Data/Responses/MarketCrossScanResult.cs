namespace ZeroPlus.Models.Data.Responses
{
    public class MarketCrossScanResult
    {
        public string? Symbol { get; set; }
        public double HighestBid { get; set; }
        public double LowestAsk { get; set; }
        public double UnderMid { get; set; }
    }
}
