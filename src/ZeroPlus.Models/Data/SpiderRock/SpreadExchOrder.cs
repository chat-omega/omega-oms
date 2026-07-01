using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock;

public class SpreadExchOrder : IOpenSpreadExchOrder, ICobData
{
    public CobDataType DataType { get; } = CobDataType.Order;

    public bool FromCache { get; set; }
    public bool AllOrNone { get; set; }
    public bool FlipSide { get; set; }
    public bool IsPriceValid { get; set; }

    public SrExch Exch { get; set; }
    public SrFirmType FirmType { get; set; }
    public SrMarketQualifier MarketQualifier { get; set; }
    public SrOrderStatus OrderStatus { get; set; }
    public SrOrderType OrderType { get; set; }
    public SrTimeInForce TimeInForce { get; set; }
    public BaseStrategy BaseStrategy { get; set; }

    public int OrigOrderSize { get; set; }
    public int OrderSize { get; set; }
    public double Price { get; set; }

    public long DgwTimestamp { get; set; }
    public long SrcTimestamp { get; set; }
    public long NetTimestamp { get; set; }
    public DateTime Timestamp { get; set; }

    public string? Underlying { get; set; }
    public string? Symbol { get; set; }
    public string? ClearingAccount { get; set; }
    public string? ClearingFirm { get; set; }
    public string? OrderID { get; set; }
    public string? SpreadKey { get; set; }

    public string? SpreadId { get; set; }
    public string? SpreadDescription { get; set; }

    public SpreadExchOrder()
    {

    }

    public SpreadExchOrder(SpreadExchOrder other)
    {
        FromCache = other.FromCache;
        AllOrNone = other.AllOrNone;
        FlipSide = other.FlipSide;
        IsPriceValid = other.IsPriceValid;
        Exch = other.Exch;
        FirmType = other.FirmType;
        MarketQualifier = other.MarketQualifier;
        OrderStatus = other.OrderStatus;
        OrderType = other.OrderType;
        TimeInForce = other.TimeInForce;
        BaseStrategy = other.BaseStrategy;
        OrigOrderSize = other.OrigOrderSize;
        OrderSize = other.OrderSize;
        Price = other.Price;
        DgwTimestamp = other.DgwTimestamp;
        SrcTimestamp = other.SrcTimestamp;
        NetTimestamp = other.NetTimestamp;
        Timestamp = other.Timestamp;
        Underlying = other.Underlying;
        Symbol = other.Symbol;
        ClearingAccount = other.ClearingAccount;
        ClearingFirm = other.ClearingFirm;
        OrderID = other.OrderID;
        SpreadKey = other.SpreadKey;
        SpreadId = other.SpreadId;
        SpreadDescription = other.SpreadDescription;
    }
}