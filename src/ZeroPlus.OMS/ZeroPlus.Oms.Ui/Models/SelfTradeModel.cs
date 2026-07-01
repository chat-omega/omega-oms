using System;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models
{
    public class SelfTradeModel : ISelfTradeModel
    {
        public string Symbol { get; set; }
        public DateTime TradeTime { get; set; }
        public int Qty { get; set; }
    }
}