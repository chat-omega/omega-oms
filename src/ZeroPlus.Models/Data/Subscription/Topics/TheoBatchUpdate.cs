using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class TheoBatchUpdate
{
    public int UnderIndex { get; set; }
    public IReadOnlyList<IDeltaAdjustedOption> Updates { get; set; } = null!;
    public bool BaseFitUpdated { get; set; }
    public bool[] AdjustedFlags { get; set; } = null!;
}
