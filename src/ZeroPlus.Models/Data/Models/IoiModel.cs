using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models;

/// <summary>
/// UI model for an IOI (enriched from IoiRepresentation with Symbol, Description, BaseStrategy).
/// </summary>
public class IoiModel
{
    public uint epochSec_;
    public uint epochMicros_;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BaseStrategy BaseStrategy { get; set; } = BaseStrategy.CUSTOM;
    public string Route { get; set; } = string.Empty;
    public IoiPriceType PriceType { get; set; }
    public double LimitPrice { get; set; }
    public uint OrderQuantity { get; set; }
    public ulong IoiId { get; set; }
    public List<IoiLegModel> Legs { get; init; } = [];
    public bool IsComplexOrder => Legs.Count > 1;

    public override string ToString()
    {
        return
            $"Timestamp: {Timestamp:HH:mm:ss.fff yyyy-MM-dd}, " +
            $"{Symbol}, " +
            $"LimitPrice: {LimitPrice}, " +
            $"OrderQty: {OrderQuantity}, " +
            $"Legs: {string.Join(", ", Legs.Select(l => l.ToString()) ?? Enumerable.Empty<string>())}, " +
            $"Route: {Route}, " +
            $"PriceType: {PriceType}, " +
            $"IoiId: {IoiId}, " +
            $"IsComplexOrder: {IsComplexOrder}";
    }
}
