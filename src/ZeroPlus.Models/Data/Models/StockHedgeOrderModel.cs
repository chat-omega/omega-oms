using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public record StockHedgeOrderModel(
        int Qty,
        Side side,
        string Module);
}
