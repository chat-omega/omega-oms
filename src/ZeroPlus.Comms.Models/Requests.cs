
namespace ZeroPlus.Comms.Models.Data.Requests
{
    public class OrderRequest
    {
        public string? Symbol { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public string? Side { get; set; }
        public string? OrderType { get; set; }
        public string? TimeInForce { get; set; }
        public string? Account { get; set; }
        public string? Route { get; set; }
    }
}
