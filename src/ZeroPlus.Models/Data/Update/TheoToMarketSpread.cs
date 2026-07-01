namespace ZeroPlus.Models.Data.Update;

public class TheoToMarketSpread
{
    public int TickerId { get; set; }
    public double LastBidTheoSpread { get; set; }
    public double LastAskTheoSpread { get; set; }
    public double BidTheoSpreadEma { get; set; }
    public double AskTheoSpreadEma { get; set; }
}