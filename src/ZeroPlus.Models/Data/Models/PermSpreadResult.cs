using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Models
{
    public sealed record PermSpreadResult
    {
        public IReadOnlyList<PermLegResult> Legs { get; init; } = new List<PermLegResult>();
    }
}
