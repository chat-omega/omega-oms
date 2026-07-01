namespace ZeroPlus.Models.Data.EdgeScanner;

public class EdgeScanFeedTraderSettings : IEdgeScanFeedTraderSettings
{
    public bool PreviousAttemptCrossCheckEnabled { get; set; }
    public bool MinEdgeToPreviousAttemptCheckEnabled { get; set; }
    public double MinEdgeToMarketCheckEdge { get; set; }
    public bool MinEdgeToMarketCheckEnabled { get; set; }
    public double CancelWithTimer { get; set; }
    public bool MinTimeToPermLoserCheckEnabled { get; set; }
    public bool MinTimeToPreviousAttemptCheckEnabled { get; set; }
    public bool MinBidCheckEnabled { get; set; }
    public double MinBidCheckBidValue { get; set; }
    public double MinTimeToPreviousAttemptIntervalSeconds { get; set; }
    public double MinTimeToPermLoserIntervalSeconds { get; set; }
}