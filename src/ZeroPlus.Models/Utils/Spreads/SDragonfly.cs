using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SDragonfly : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.DRAGONFLY;

    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        // Dragonfly requires exactly 6 legs, all options
        if (legs.Count != 6 || !legs.All(l => l?.Security is Option))
            return false;

        // Must have exactly 3 calls and 3 puts
        var callCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Call);
        var putCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Put);

        if (callCount != 3 || putCount != 3)
            return false;

        // Cast all legs to options for easier access
        var options = legs.Select(l => (Option)l!.Security!).ToList();

        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);

        if (!sameExpiration)
            return false;

        // Must have exactly 5 distinct strikes (dragonfly has middle 2 at same strike)
        var distinctStrikes = options.Select(o => o.Strike).Distinct().OrderBy(s => s).ToList();

        if (distinctStrikes.Count != 5)
            return false;

        // Verify the structure: 3 puts then 3 calls (already sorted)
        // opts[0-2] should be puts, opts[3-5] should be calls
        if (!(legs[0]?.Security is Option opt0) || opt0.PutCall != PutCall.Put)
            return false;
        if (!(legs[1]?.Security is Option opt1) || opt1.PutCall != PutCall.Put)
            return false;
        if (!(legs[2]?.Security is Option opt2) || opt2.PutCall != PutCall.Put)
            return false;
        if (!(legs[3]?.Security is Option opt3) || opt3.PutCall != PutCall.Call)
            return false;
        if (!(legs[4]?.Security is Option opt4) || opt4.PutCall != PutCall.Call)
            return false;
        if (!(legs[5]?.Security is Option opt5) || opt5.PutCall != PutCall.Call)
            return false;

        // Middle legs (legs[2] and legs[3]) should have same strike
        if (opt2.Strike != opt3.Strike)
            return false;

        // Verify side pattern: opts[0] != opts[1], opts[1] == opts[4],
        // opts[0] == opts[2], opts[2] == opts[3], opts[3] == opts[5]
        bool validSidePattern =
            legs[0].Side != legs[1].Side &&
            legs[1].Side == legs[4].Side &&
            legs[0].Side == legs[2].Side &&
            legs[2].Side == legs[3].Side &&
            legs[3].Side == legs[5].Side;

        if (!validSidePattern)
            return false;

        // Verify quantity pattern for dragonfly: wing1Qty=3, wing2Qty=4, bodyQty=1
        // opts[1] and opts[4] should have qty=3 (wing1)
        // opts[0] and opts[5] should have qty=4 (wing2)
        // opts[2] and opts[3] should have qty=1 (body)
        bool validQuantityPattern =
            legs[1].Quantity == 3 &&
            legs[4].Quantity == 3 &&
            legs[0].Quantity == 4 &&
            legs[5].Quantity == 4 &&
            legs[2].Quantity == 1 &&
            legs[3].Quantity == 1;

        if (!validQuantityPattern)
            return false;

        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = opt0.Expiration.ToString("MMM-dd-yy");
        string strikesList = string.Join("/", distinctStrikes.Select(s => s.ToString()));

        string spreadType = $"DRAGON FLY {rootSymbols} {expirationDate} {strikesList}";
        string spreadDescription = spreadType;

        details = new StrategyIdentification(
            BaseType: "DRAGONFLY",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}
