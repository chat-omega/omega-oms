using System;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class EdgeScanFeedStatsModel : IEdgeScanFeedStatisticsSummary
{
    public bool Updated { get; set; }
    public string InstanceId { get; set; }
    public string State { get; set; }
    public string User { get; set; }
    public string ScannerConfig { get; set; }
    public string BasketConfig { get; set; }
    public int TotalAttempts { get; set; }
    public int TotalSubs { get; set; }
    public int Submissions { get; set; }
    public int Received { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime Timestamp { get; set; }
    public double WinLoseRatio { get; set; }
    public int WinningTradesCount { get; set; }
    public int LosingTradesCount { get; set; }
}