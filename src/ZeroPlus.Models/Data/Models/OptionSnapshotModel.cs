using System;

namespace ZeroPlus.Models.Data.Models;

public class OptionSnapshotModel
{
    public string? Symbol { get; set; }
    public string? UnderSymbol { get; set; }
    public string? OptionType { get; set; }
    public int LastQty { get; set; }
    public int BidQty { get; set; }
    public int AskQty { get; set; }
    public double LastPrice { get; set; }
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double UnderBid1 { get; set; }
    public double UnderAsk1 { get; set; }
    public double UnderLast1 { get; set; }
    public double UnderBid2 { get; set; }
    public double UnderAsk2 { get; set; }
    public double UnderLast2 { get; set; }
    public double HwTV { get; set; }
    public double HwDelta { get; set; }
    public double HwIV { get; set; }
    public double HwVega { get; set; }
    public double DeltaAdjTheo { get; set; }
    public double Strike { get; set; }
    public DateTime Expiration { get; set; }
    public DateTime StoreTime { get; set; }
    public DateTime SnapTime { get; set; }
    public DateTime QuoteTime { get; set; }
    public DateTime TradeTime { get; set; }
    public DateTime UnderTime1 { get; set; }
    public DateTime UnderTime2 { get; set; }
    public DateTime HwTime { get; set; }
}