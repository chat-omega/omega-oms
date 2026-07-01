using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock;

public class SpreadExchPrint
{
    public string? Underlying { get; set; }
    public long PrintNumber { get; set; }
    public SrExch Exch { get; set; }
    public string? StrategyId { get; set; }
    public Side? Side { get; set; }
    public int PrintSize { get; set; }
    public double PrintPrice { get; set; }
    public bool IsPrintPriceValid { get; set; }
    public Side? MinAnchorSide { get; set; }
    public string? MinAnchorLeg { get; set; }
    public Side? MaxAnchorSide { get; set; }
    public string? MaxAnchorLeg { get; set; }
    public bool HasFlexLeg { get; set; }
    public bool HasHedgeLeg { get; set; }
    public Side? StockLegSide { get; set; }
    public Side? FutureLegSide { get; set; }
    public SrStrategyClass StrategyClass { get; set; }
    public byte NumOptLegs { get; set; }
    public long StcTimestamp { get; set; }
    public long NetTimestamp { get; set; }
    public DateTime Timestamp { get; set; }
    public List<SrSpreadLeg> Legs { get; set; } = new();
}