using System;

namespace ZeroPlus.Models.Data.Update;

public interface IEdgeScanFeedStatisticsSummary
{
    public string? InstanceId { get; set; }
    public string? State { get; set; }
    public string? User { get; set; }
    public string? ScannerConfig { get; set; }
    public string? BasketConfig { get; set; }
    public int TotalAttempts { get; set; }
    public int TotalSubs { get; set; }
    public int Submissions { get; set; }
    public int Received { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime Timestamp { get; set; }
}