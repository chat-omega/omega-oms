using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Trading
{
    public class MassCancelRequest
    {
        public Venue Venue { get; set; }
        public Broker Broker { get; set; }
        public MassCancelType CancelType { get; set; }
        public string? Exchange { get; set; }
        public string? Account { get; set; }
        public string? Symbol { get; set; }

        public override string ToString() => $"Venue: {Venue}, Broker: {Broker}, CancelType: {CancelType}, Exchange: {Exchange}, Account: {Account}, Symbol: {Symbol}";
    }
}
