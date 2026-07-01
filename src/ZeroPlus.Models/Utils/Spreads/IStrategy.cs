using System;
using System.Collections;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public record struct SpreadLeg(Security Security, string Symbol, Side? Side, int Quantity, int Ratio = 1) : IComplexOrderLegMin;
public record StrategyIdentification(string BaseType, string SpreadType, string SpreadDescription);
public interface IStrategy
{
    BaseStrategy Strategy { get; }
    bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details);
}