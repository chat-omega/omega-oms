using System;

namespace ZeroPlus.Models.Data.Models;

public class HighestBidLowestAskTrackerModel
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double HighestBid { get; set; } = double.NaN;
    public double LowestAsk { get; set; } = double.NaN;
    public ulong HighestBidTime { get; set; }
    public ulong LowestAskTime { get; set; }
    public double HighestBidUnderlyingMid { get; set; } = double.NaN;
    public double LowestAskUnderlyingMid { get; set; } = double.NaN;
    public double Delta { get; set; } = double.NaN;
    public string? Symbol { get; set; }
    public string? Underlying { get; set; }
}