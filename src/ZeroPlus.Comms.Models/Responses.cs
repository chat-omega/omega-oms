
namespace ZeroPlus.Comms.Models.Data.Responses
{
    public class OrderResponse
    {
        public string? OrderId { get; set; }
        public string? Status { get; set; }
        public int FilledQuantity { get; set; }
        public double AvgPrice { get; set; }
        public string? Message { get; set; }
    }
}
