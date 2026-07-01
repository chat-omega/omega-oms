using System;

namespace ZeroPlus.Models.Data.Models;

public class TheoResult
{
    public DateTime SnapshotTime { get; set; }
    public double SnapshotUnderlying { get; set; } = double.NaN;
    public double Underlying { get; set; } = double.NaN;
    public double Theo { get; set; } = double.NaN;
    public double Delta { get; set; } = double.NaN;
    public double Gamma { get; set; } = double.NaN;
    public double Vega { get; set; } = double.NaN;
    public double Iv { get; set; } = double.NaN;
    public double DeltaAdjustedTheo { get; set; } = double.NaN;
    public double PriceMetric { get; set; } = double.NaN;
    public double ChangeInPremium { get; set; } = double.NaN;
}