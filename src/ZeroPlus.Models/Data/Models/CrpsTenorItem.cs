using System;

namespace ZeroPlus.Models.Data.Models;

public record CrpsTenorItem(DateTime Expiry, double Score);

public record SurfaceFitMetrics(
    // Core
    double Threshold,
    int   NumOptions,
    double Good,
    double PriceMSER,
    // Crossings
    int   NumCrossingsOTMP,
    int   NumCrossingsOTMC,
    int   NumCrossingsITMP,
    int   NumCrossingsITMC,
    double SumCrossingsOTMP,
    double SumCrossingsOTMC,
    double SumCrossingsITMP,
    double SumCrossingsITMC
);

/// Per-expiry arrays (VecN → uint[], VecR → double[])
public record SurfaceFitMetricsByTenor(
    CrpsTenorItem[] CrpsTenorItems,
    int[]   NumOptionsByExpiry,
    double[] GoodByExpiry,
    int[]   NumCrossingsOTMPbyExpiry,
    int[]   NumCrossingsOTMCbyExpiry,
    int[]   NumCrossingsITMPbyExpiry,
    int[]   NumCrossingsITMCbyExpiry,
    double[] SumCrossingsOTMPbyExpiry,
    double[] SumCrossingsOTMCbyExpiry,
    double[] SumCrossingsITMPbyExpiry,
    double[] SumCrossingsITMCbyExpiry,
    double[] PriceMSERs);

