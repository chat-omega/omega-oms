using System;
using System.Text.Json.Serialization;

namespace ZeroPlus.Vola.Models;

/// <summary>
/// Single point for a vol curve plotted against normalized strike.
/// </summary>
/// <param name="Strike">Absolute strike (same units as chain), for crosshairs and tooltips.</param>
public record VolByStrikePoint(
    double NormalizedStrike,
    double Vol,
    double Strike = 0);

/// <summary>
/// Low/high market vol interval at one normalized strike.
/// </summary>
/// <param name="Strike">Absolute strike (same units as chain), for crosshairs and tooltips.</param>
public record VolByStrikeRangePoint(
    double NormalizedStrike,
    double Strike,
    double VolLow,
    double VolHigh,
    double Bid,
    double Ask)
{
    /// <summary>Derived for charts; omitted from wire JSON so STJ round-trips only ctor fields (avoids malformed / oversized payloads).</summary>
    [JsonIgnore]
    public double MidVol => (VolLow + VolHigh) * 0.5;
    /// <summary>Positive error for error bars (distance above mid).</summary>
    [JsonIgnore]
    public double PositiveError => VolHigh - MidVol;
    /// <summary>Negative error for error bars (distance below mid).</summary>
    [JsonIgnore]
    public double NegativeError => MidVol - VolLow;
}

/// <summary>
/// Plot-ready vol-by-strike diagnostics data for a tenor.
/// </summary>
public record VolsDataByTenorResponse(
    string Symbol,
    int TenorIndex,
    DateTime Expiry,
    double TimeToExpiry,
    double Forward,
    VolByStrikePoint[] FitCurve,
    VolByStrikeRangePoint[] PutPoints,
    VolByStrikeRangePoint[] CallPoints
);
