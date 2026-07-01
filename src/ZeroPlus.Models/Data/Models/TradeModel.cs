namespace ZeroPlus.Models.Data.Models
{
    public class TradeModel
    {
        public string? OrderId { get; set; }
        public string? Username { get; set; }
        public string? Account { get; set; }
        public string? SecurityType { get; set; }
        public string? Symbol { get; set; }
        public string? Underlying { get; set; }
        public string? Expiration { get; set; }
        public string? Strike { get; set; }
        public string? PutCall { get; set; }
        public string? Currency { get; set; }
        public int Multiplier { get; set; }
        public string? Route { get; set; }
        public string? Side { get; set; }
        public double Price { get; set; }
        public double Commission { get; set; }
        public int Quantity { get; set; }
        public string? Exchange { get; set; }
        public string? LiquidityFlag { get; set; }
        public string? PositionEffect { get; set; }
        public string? RoutingSession { get; set; }
        public string? ContraBroker { get; set; }
        public string? ClearingFirm { get; set; }
        public string? ClearingAccount { get; set; }
        public string? Tag { get; set; }
        public string? TradeDateTime { get; set; }
    }
}
