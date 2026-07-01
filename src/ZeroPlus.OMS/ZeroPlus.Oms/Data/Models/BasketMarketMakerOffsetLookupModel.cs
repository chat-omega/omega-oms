namespace ZeroPlus.Oms.Data.Models
{
    public class BasketMarketMakerOffsetLookupModel
    {
        public string Symbol { get; set; } = "";
        public double MinPriceDiff { get; set; }
        public double StrikeOffset { get; set; }
        public double MaxStrikeOffset { get; set; }
    }
}
