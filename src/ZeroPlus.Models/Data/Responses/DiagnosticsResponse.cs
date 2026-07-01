using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Responses;

/// <summary>
/// Combined price and vol fit diagnostics for the aggregate (single-call) endpoint.
/// </summary>
public record DiagnosticsResponse(
    SurfaceFitMetrics Price,
    VolFitMetrics? Vol,
    System.DateTime MarketDataSnapshotTime
);
