using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Responses;

/// <summary>
/// Combined price and vol fit diagnostics by tenor (per-expiry).
/// </summary>
public record DiagnosticsByTenorResponse(
    SurfaceFitMetricsByTenor Price,
    VolFitMetrics? Vol,
    VolFitMetricsByTenor? VolByTenor
);
