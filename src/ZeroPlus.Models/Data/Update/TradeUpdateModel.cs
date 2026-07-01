using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public readonly struct TradeUpdateModel
    {
        public readonly string SpreadId;
        public readonly Side Side;
        public readonly int Qty;
        public readonly double Price;
        public readonly double UnderBid;
        public readonly double UnderAsk;

        public TradeUpdateModel(string spreadId, Side side, int quantity, double price, double underBid, double underAsk)
        {
            SpreadId = spreadId;
            Side = side;
            Qty = quantity;
            Price = price;
            UnderBid = underBid;
            UnderAsk = underAsk;
        }

        public override string ToString()
        {
            return Qty + "@" + Math.Round(Price, 2) + " [" + Math.Round(UnderBid, 2) + "X" + Math.Round(UnderAsk, 2) + "]";
        }
    }
}