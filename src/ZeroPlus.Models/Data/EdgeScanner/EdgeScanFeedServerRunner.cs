using System;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.EdgeScanner;

public class EdgeScanFeedServerRunner
{
    public string? Id { get; set; }
    public string? Account { get; set; }
    public string? RouteSpxRutXsp { get; set; }
    public string? RouteNdx { get; set; }
    public string? HedgeRoute { get; set; }
    public string? Route { get; set; }
    public string? SingleLegRoute { get; set; }
    public DateTime Timestamp { get; set; }
    public IEdgeScanFeedTraderSettings? EdgeScanFeedTraderSettings { get; set; }
    public AutoTraderConfig? AutoTraderConfig { get; set; }
    public EdgeScanFeedFilterConfig? FilterConfig { get; set; }
}