using System;

namespace ZeroPlus.Models.Data.SpiderRock;

public interface IOpenSpreadExchOrder
{
    public string? Underlying { get; set; }
    public string? Symbol { get; set; }
    public string? OrderID { get; set; }

    public bool FlipSide { get; set; }

    public int OrigOrderSize { get; set; }
    public int OrderSize { get; set; }
    public double Price { get; set; }
    public DateTime Timestamp { get; set; }

    public SrExch Exch { get; set; }
}