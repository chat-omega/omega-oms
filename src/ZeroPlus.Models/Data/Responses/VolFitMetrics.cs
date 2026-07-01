namespace ZeroPlus.Models.Data.Responses;

/// <summary>
/// Vol-space fit quality metrics (chi2Reds, diffVolsPC3) from the fitter.
/// Populated from Vola fit results when the native API exposes them; otherwise null/N/A.
/// </summary>
public record VolFitMetrics(
    double? Chi2RedsAvgG,
    double? Chi2RedsAvgM,
    double? Chi2RedsAvgN,
    double? DiffVolsPC3
);
