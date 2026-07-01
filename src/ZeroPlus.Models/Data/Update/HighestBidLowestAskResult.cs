using System;

namespace ZeroPlus.Models.Data.Update;

public class HighestBidLowestAskResult
{
    public double HighestBid { get; set; } = double.NaN;
    public double HighestBidBase { get; set; } = double.NaN;
    public ulong HighestBidTime { get; set; }
    public double HighestBidUnderlyingMid { get; set; } = double.NaN;
    public double LowestAsk { get; set; } = double.NaN;
    public double LowestAskBase { get; set; } = double.NaN;
    public ulong LowestAskTime { get; set; }
    public double LowestAskUnderlyingMid { get; set; } = double.NaN;

    public double SkewAdjustedHighestBid { get; set; } = double.NaN;
    public double SkewAdjustedHighestBidBase { get; set; } = double.NaN;
    public ulong SkewAdjustedHighestBidTime { get; set; }
    public double SkewAdjustedHighestBidUnderlyingMid { get; set; } = double.NaN;
    public double SkewAdjustedLowestAsk { get; set; } = double.NaN;
    public double SkewAdjustedLowestAskBase { get; set; } = double.NaN;
    public ulong SkewAdjustedLowestAskTime { get; set; }
    public double SkewAdjustedLowestAskUnderlyingMid { get; set; } = double.NaN;

    public bool Update(HighestBidLowestAskResult? highestBidLowestAsk, double threshold = 0)
    {
        if (highestBidLowestAsk == null)
        {
            return false;
        }

        var updated = double.IsNaN(highestBidLowestAsk.HighestBid) ^ double.IsNaN(HighestBid)
                      || double.IsNaN(highestBidLowestAsk.LowestAsk) ^ double.IsNaN(LowestAsk)
                      || Math.Abs(highestBidLowestAsk.HighestBid - HighestBid) >= threshold
                      || Math.Abs(highestBidLowestAsk.LowestAsk - LowestAsk) >= threshold
                      || double.IsNaN(highestBidLowestAsk.SkewAdjustedHighestBid) ^ double.IsNaN(SkewAdjustedHighestBid)
                      || double.IsNaN(highestBidLowestAsk.SkewAdjustedLowestAsk) ^ double.IsNaN(SkewAdjustedLowestAsk)
                      || Math.Abs(highestBidLowestAsk.SkewAdjustedHighestBid - SkewAdjustedHighestBid) >= threshold
                      || Math.Abs(highestBidLowestAsk.SkewAdjustedLowestAsk - SkewAdjustedLowestAsk) >= threshold;

        HighestBid = highestBidLowestAsk.HighestBid;
        HighestBidUnderlyingMid = highestBidLowestAsk.HighestBidUnderlyingMid;
        HighestBidBase = highestBidLowestAsk.HighestBidBase;
        HighestBidTime = highestBidLowestAsk.HighestBidTime;

        LowestAsk = highestBidLowestAsk.LowestAsk;
        LowestAskUnderlyingMid = highestBidLowestAsk.LowestAskUnderlyingMid;
        LowestAskBase = highestBidLowestAsk.LowestAskBase;
        LowestAskTime = highestBidLowestAsk.LowestAskTime;

        SkewAdjustedHighestBid = highestBidLowestAsk.SkewAdjustedHighestBid;
        SkewAdjustedHighestBidUnderlyingMid = highestBidLowestAsk.SkewAdjustedHighestBidUnderlyingMid;
        SkewAdjustedHighestBidBase = highestBidLowestAsk.SkewAdjustedHighestBidBase;
        SkewAdjustedHighestBidTime = highestBidLowestAsk.SkewAdjustedHighestBidTime;

        SkewAdjustedLowestAsk = highestBidLowestAsk.SkewAdjustedLowestAsk;
        SkewAdjustedLowestAskUnderlyingMid = highestBidLowestAsk.SkewAdjustedLowestAskUnderlyingMid;
        SkewAdjustedLowestAskBase = highestBidLowestAsk.SkewAdjustedLowestAskBase;
        SkewAdjustedLowestAskTime = highestBidLowestAsk.SkewAdjustedLowestAskTime;

        return updated;
    }
}