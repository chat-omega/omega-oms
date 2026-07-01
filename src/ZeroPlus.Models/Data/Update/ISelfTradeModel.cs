using System;

namespace ZeroPlus.Models.Data.Update
{
    public interface ISelfTradeModel
    {
        string Symbol { get; set; }
        DateTime TradeTime { get; set; }
        int Qty { get; set; }
    }
}