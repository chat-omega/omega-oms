using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Models;

/// <summary>
/// Response DTO for VCT (Volatility Curve Type) suggestion from VOLA's VCT selector.
/// </summary>
public record VctSuggestionResult
{
    /// <summary>Recommended VCT, e.g. "C12W" or VolCurveType name.</summary>
    public string BestVCT { get; init; } = string.Empty;

    /// <summary>Whether the full VCT selector ran; false if only current config was returned.</summary>
    public bool UsedVctSelector { get; init; }

    /// <summary>VCTs that were tried as candidates (when selector was used).</summary>
    public string[]? FitVCTs { get; init; }

    /// <summary>Final average metric per VCT (VCT name -> value).</summary>
    public Dictionary<string, double>? Chi2MetricFinalAvg { get; init; }

    /// <summary>Standard deviation of final metric per VCT.</summary>
    public Dictionary<string, double>? Chi2MetricFinalStd { get; init; }

    /// <summary>Fly arbitrage penalty per VCT.</summary>
    public Dictionary<string, double>? PenaltyFlyArb { get; init; }

    /// <summary>Calendar arbitrage penalty per VCT.</summary>
    public Dictionary<string, double>? PenaltyCalArb { get; init; }

    /// <summary>Optional message when selector was not used or on partial success.</summary>
    public string? Message { get; init; }
}
