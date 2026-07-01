using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models;

public class EdgeScanFeedOrderTagModel : OrderTagModel
{
    public EdgeScannerType EdgeScannerType { get; set; }
    public char EdgeScanFeedConditionCode { get; set; }
    public double EdgeScanFeedEdge { get; set; }
    public double EdgeScanFeedTimespan { get; set; }
    public double EdgeScanFeedRespondLatency { get; set; }
    public double EdgeScanFeedDeltaAdjPrice { get; set; }
    public double EdgeScanFeedBuyPrice { get; set; }
    public double EdgeScanFeedSellPrice { get; set; }
    public uint EdgeScanFeedBuyQty { get; set; }
    public uint EdgeScanFeedSellQty { get; set; }
    public DateTime EdgeScanFeedBuyTime { get; set; }
    public DateTime EdgeScanFeedSellTime { get; set; }
}