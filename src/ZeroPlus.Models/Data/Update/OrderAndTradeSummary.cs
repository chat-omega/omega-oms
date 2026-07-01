using System;

namespace ZeroPlus.Models.Data.Update;

public class OrderAndTradeSummary
{
    public double LastAttemptPx { get; set; }
    public double LastAttemptUnderPx { get; set; }
    public DateTime LastAttemptTime { get; set; }

    public double LastFillPx { get; set; }
    public double LastFillUnderPx { get; set; }
    public DateTime LastFillTime { get; set; }

    public double LowestAttemptedEdgeToTheo { get; set; }
    public double HighestFilledEdgeToTheo { get; set; }
}