using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Update;

public class UnderFitResult
{
    public object Lock = new object();

    public uint Index { get; set; }
    public ulong Sequence { get; set; }

    public double UnderlyingSpot { get; set; } = double.NaN;
    public double UnderlyingMid { get; set; } = double.NaN;
    public DateTime SnapshotTime { get; set; } = DateTime.UnixEpoch;
    public double PriceMetric { get; set; } = double.NaN;

    public List<FitResult> FitResults { get; set; } = new();
}