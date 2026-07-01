using System;

namespace ZeroPlus.Oms.Ui.Models;

public class CloseSubsModel
{
    public string Underlying { get; set; }
    public string Trader { get; set; }
    public string Symbol { get; set; }
    public string SpreadId { get; set; }
    public double CloseSubs { get; set; } = double.NaN;
    public double AdjustedPnl { get; set; } = double.NaN;
    public DateTime Time { get; set; }
}