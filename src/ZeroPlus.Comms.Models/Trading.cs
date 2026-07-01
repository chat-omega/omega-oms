
using System;

namespace ZeroPlus.Comms.Models.Data.Trading
{
    public class OrderUpdate
    {
        public string? OrderId { get; set; }
        public string? Status { get; set; }
        public int FilledQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public double AvgPrice { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
