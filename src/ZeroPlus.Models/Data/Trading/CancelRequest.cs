using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading
{
    public class CancelRequest : IHaveRisk
    {
        public string? LocalId { get; set; }
        public string? PermId { get; set; }
        public string? OrderId { get; set; }
        public string? Account { get; set; }
        public Venue? Venue { get; set; }
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string? RiskCheckMessage { get; set; }
        public override string ToString() => $"Local: {LocalId}, Perm: {PermId}, Order: {OrderId}, Acc: {Account}, Venue: {Venue}";
    }
}
