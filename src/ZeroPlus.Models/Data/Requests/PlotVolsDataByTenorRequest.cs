namespace ZeroPlus.Vola.Models;

/// <summary>
/// Request payload for retrieving vol-by-strike plot data for a specific tenor index.
/// </summary>
public record PlotVolsDataByTenorRequest(
    string Symbol,
    int TenorIndex
);
