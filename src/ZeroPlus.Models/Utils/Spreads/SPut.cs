using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPut : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT;

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

        // Verify it's an option
        if (leg.Security is not Option option)
            return false;

        // Verify it's a put
        if (option.PutCall != PutCall.Put)
            return false;

        // Build description matching EvaluateSingleLeg format
        string strategy = option.PutCall.ToString().ToUpper() + " " +
                         option.Underlying!.Symbol + " " +
                         option.Expiration.ToString("MMM-dd-yy") + " " +
                         option.Strike;
        string baseType = option.PutCall.ToString().ToUpper();
        string spreadType = strategy;
        string spreadDescription = leg.Side?.ToString().ToUpper() + " " + strategy;

        details = new StrategyIdentification(
            BaseType: baseType,
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}
