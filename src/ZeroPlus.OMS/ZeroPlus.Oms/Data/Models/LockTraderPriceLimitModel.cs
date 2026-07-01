using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Data.Models;

public class LockTraderPriceLimitModel
{
    public BaseStrategy? Strategy { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
}