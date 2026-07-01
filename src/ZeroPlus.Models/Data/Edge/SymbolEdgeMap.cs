using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Edge
{
    public class SymbolEdgeMap
    {
        public int Id { get; set; }
        public string? Underlying { get; set; }
        public string? Symbol { get; set; }
        public Side? OpeningSide { get; set; }
        public Side? HardSide { get; set; }
        public DateTime Date { get; set; }
        public double BestBuyPrice { get; set; }
        public double BestBuyPriceUnderlying { get; set; }
        public double BestBuyPriceDelta { get; set; }
        public double BestSellPrice { get; set; }
        public double BestSellPriceUnderlying { get; set; }
        public double BestSellPriceDelta { get; set; }
    }
}
