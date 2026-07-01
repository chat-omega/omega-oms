using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock;

public class SpreadPrint : ICobData
{
    public CobDataType DataType { get; } = CobDataType.Print;

    public bool FromCache { get; set; }
    public Side? Side { get; set; }
    public SrExch PrtExch { get; set; }
    public BaseStrategy BaseStrategy { get; set; }

    public int PrtSize { get; set; }
    public double PrtPrice { get; set; }
    public long SrcTimestamp { get; set; }
    public long NetTimestamp { get; set; }
    public DateTime Timestamp { get; set; }

    public string? Underlying { get; set; }
    public string? Symbol { get; set; }
    public string? SpreadId { get; set; }
    public string? SpreadDescription { get; set; }

    public SpreadPrint()
    {

    }

    public SpreadPrint(SpreadPrint other)
    {
        FromCache = other.FromCache;
        BaseStrategy = other.BaseStrategy;
        Side = other.Side;
        PrtExch = other.PrtExch;

        PrtSize = other.PrtSize;
        PrtPrice = other.PrtPrice;
        SrcTimestamp = other.SrcTimestamp;
        NetTimestamp = other.NetTimestamp;
        Timestamp = other.Timestamp;

        Underlying = other.Underlying;
        Symbol = other.Symbol;
        SpreadId = other.SpreadId;
        SpreadDescription = other.SpreadDescription;
    }
}