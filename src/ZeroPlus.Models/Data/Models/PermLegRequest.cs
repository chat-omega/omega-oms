using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public sealed record PermLegRequest
    {
        public string Symbol { get; init; } = string.Empty;
        public Side Side { get; init; }
        public int Ratio { get; init; }
    }
}
