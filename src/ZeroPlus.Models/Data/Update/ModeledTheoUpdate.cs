using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Update;

public class ModeledTheoUpdate
{
    public uint ModelId { get; set; }
    public double UnderlyingPrice { get; set; }
    public ulong CalcTime { get; set; }
    public List<ModeledTheo> Theos { get; set; } = new();
}