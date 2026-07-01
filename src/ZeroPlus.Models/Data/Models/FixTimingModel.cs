using System;
using Microsoft.Extensions.ObjectPool;

namespace ZeroPlus.Models.Data.Models;

public class FixTimingModel : IResettable
{
    public DateTime Timestamp { get; set; }
    public string? OrderId { get; set; }
    public long Sequence { get; set; }
    public short Category { get; set; }
    public long Nanos { get; set; }

    public bool TryReset()
    {
        Timestamp = default;
        OrderId = default;
        Sequence = default;
        Category = default;
        Nanos = default;
        return true;
    }
}