using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public interface ITradeFeedModel
    {
        int Id { get; set; }
        bool IsFirm { get; set; }
        bool IsCopyCat { get; set; }
        int Quantity { get; set; }
        BaseStrategy BaseStrategy { get; set; }
        Side Side { get; set; }
        double Price { get; set; }
        double Bid { get; set; }
        double Ask { get; set; }
        double Delta { get; set; }
        DateTime TradeTime { get; set; }
        string Exchange { get; set; }
        string Description { get; set; }
        string Underlying { get; set; }
    }
}