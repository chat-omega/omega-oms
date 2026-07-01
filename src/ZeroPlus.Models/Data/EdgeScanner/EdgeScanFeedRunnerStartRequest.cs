using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.EdgeScanner;

public class EdgeScanFeedRunnerStartRequest
{
    public required string RunnerId { get; init; }
    public required EdgeScanFeedRunnerFilterConfig FilterConfig { get; init; }
    public required AutoTraderConfig AutoTraderConfig { get; init; }
    public required OrderSubmissionDefaults OrderDefaults { get; init; }
    public SubmissionReportFilter ReportFilter { get; set; } = SubmissionReportFilter.AllUpdates;
    public BlockedSymbolModel? BlockedSymbolModel { get; init; }
}
