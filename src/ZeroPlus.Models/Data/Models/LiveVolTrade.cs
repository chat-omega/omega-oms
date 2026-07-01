using System;

namespace ZeroPlus.Models.Data.Models
{
    public class LiveVolTrade
    {
        public string? MinTime { get; set; }
        public string? MaxTime { get; set; }
        public DateTime MinTimeValue { get; set; }
        public DateTime MaxTimeValue { get; set; }
        public string? Symbol { get; set; }
        public string? Exchange { get; set; }
        public string? Condition { get; set; }
        public int LegCount { get; set; }
        public string? SpreadType { get; set; }
        public string? SpreadRatio { get; set; }
        public int Quantity { get; set; }
        public string? Title { get; set; }
        public string? SpreadCode { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public decimal MinTUE { get; set; }
        public decimal MinBid { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal Price { get; set; }
        public decimal MidMarket { get; set; }
        public decimal AboveMid { get; set; }
        public decimal TradeDelta { get; set; }
        public int SpreadId { get; set; }
        public DateTime SQLTime { get; set; }
    }
}
