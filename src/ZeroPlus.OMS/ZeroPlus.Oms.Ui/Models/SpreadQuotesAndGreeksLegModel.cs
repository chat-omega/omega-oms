using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models;

public class SpreadQuotesAndGreeksLegModel : QuotesAndGreeksModel
{
    public int Ratio { get; set; }
    public Side Side { get; set; }

    public SpreadQuotesAndGreeksLegModel(OmsCore omsCore) : base(omsCore)
    {
    }
}