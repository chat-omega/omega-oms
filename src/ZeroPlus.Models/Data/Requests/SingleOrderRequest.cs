using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Requests
{
    public class SingleOrderRequest
    {
        public string? Account { get; set; }
        public string? Route { get; set; }
        public string? Tag { get; set; }
        public string? ClientOrderId { get; set; }
        public string? Locate { get; set; }
        public bool Staged { get; set; }
        public bool ClaimRequire { get; set; }
        public string? Symbol { get; set; }
        public Side Side { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public OrderType OrderType { get; set; }

        public override string ToString()
        {
            return $"Account: {Account}, Route: {Route}, Tag: {Tag}, ClientOrderId: {ClientOrderId}, Locate: {Locate}, Staged: {Staged}, ClaimRequire: {ClaimRequire}, Symbol: {Symbol}, Side: {Side}, Quantity: {Quantity}, Price: {Price}, OrderType: {OrderType}.";
        }
    }
}
