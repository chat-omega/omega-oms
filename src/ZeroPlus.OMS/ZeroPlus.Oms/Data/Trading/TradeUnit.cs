namespace ZeroPlus.Oms.Data.Trading
{
    internal struct TradeUnit
    {
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double TotalPrice { get; set; }
        public double NetPrice { get; set; }
    }
}
