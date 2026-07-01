using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models;

public interface IQuoteDisplay
{
    public bool Active { get; set; }
    public Side? Side { get; set; }
    public string Symbol { get; set; }
    public int Ratio { get; set; }
    public int Quantity { get; set; }
    public int ActualQty { get; set; }
    public double ManualAvgCost { get; set; }
    public double ManualRealPnl { get; set; }
    public double ManualUnrealPnl { get; set; }
    public ExpirationInfoModel ExpirationInfo { get; set; }
    public StrikeInfoModel Strike { get; set; }
    public string Type { get; set; }
    public string Position { get; set; }
    public double Bid { get; set; }
    public double Mid { get; set; }
    public double Ask { get; set; }
    public double Ema { get; set; }
    public double Delta { get; set; }
    public double GammaAdjustedDelta { get; set; }
    public double DeltaModeled { get; set; }
    public double ThetaModeled { get; set; }
    public double GammaModeled { get; set; }
    public double NetDelta { get; set; }
    public double Gamma { get; set; }
    public double Vega { get; set; }
    public double Theta { get; set; }
    public double NetGamma { get; set; }
    public double NetTheta { get; set; }
    public double Rho { get; set; }
    public double Implied { get; set; }
}