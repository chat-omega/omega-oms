namespace ZeroPlus.Models.Data.Responses;

/// <summary>
/// Per-expiry vol fit quality metrics. Optional; used when the native API provides per-slice chi2/diffVolsPC3.
/// Chi2RedsNByExpiry is from FitMetrics.chi2RedsN() (VecR), one reduced chi-square per tenor.
/// </summary>
public record VolFitMetricsByTenor(
    double? Chi2RedsAvgG,
    double[] Chi2RedsByExpiry,
    double[] DiffVolsPC3ByExpiry,
    double[]? Chi2RedsNByExpiry = null
);
