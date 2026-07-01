using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SStock : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.STOCK;

    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        // Verify single leg
        if (legs.Count != 1)
            return false;

        var leg = legs[0];

        // Verify symbol exists
        if (leg?.Symbol == null)
            return false;

        // Verify it's NOT an option (must be stock or index)
        if (leg.Symbol.StartsWith("."))
            return false;

        // Verify it's NOT an index (index symbols start with $)
        if (leg.Symbol.StartsWith("$"))
            return false;

        // Build description matching EvaluateSingleLeg format
        string baseType = "STOCK";
        string spreadType = leg.Symbol;
        string spreadDescription = leg.Side?.ToString().ToUpper() + " " + leg.Symbol;

        details = new StrategyIdentification(
            BaseType: baseType,
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}
