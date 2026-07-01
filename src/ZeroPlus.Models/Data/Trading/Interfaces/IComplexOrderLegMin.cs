using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;

namespace ZeroPlus.Models.Data.Trading.Interfaces
{
    public interface IComplexOrderLegMin
    {
        int Quantity { get; }
        Side? Side { get; }
        Security? Security { get; }
        string? Symbol { get; }
        int Ratio { get; }
    }
}