using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public sealed record PermLegResult
    {
        public string Symbol { get; init; } = string.Empty;
        public Side Side { get; init; }
        public int Ratio { get; init; }
        public double Strike { get; init; }
        public DateTime Expiration { get; init; }
        public PutCall PutCall { get; init; }
    }
}
