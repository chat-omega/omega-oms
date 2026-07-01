using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

/// <summary>
/// Represents a Gut Iron Fly or Gut Skewed Iron Fly strategy.
/// <para>
/// A Gut Iron Fly is a 4-leg, 3-strike strategy where the short options are ITM (Gut):
/// Leg 0: Long Call (Lowest Strike)
/// Leg 1: Short Put (Middle Strike)
/// Leg 2: Short Call (Middle Strike - same as Leg 1)
/// Leg 3: Long Put (Highest Strike)
/// </para>
/// <para>
/// If the wings (distance from outer strikes to middle strike) are equal, it is a "GUT IRON FLY".
/// If the wings are unequal, it is a "GUT SKEWED IRON FLY".
/// </para>
/// </summary>
public class SGutIronFly : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.GUT_IRON_FLY;

    /// <inheritdoc/>
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        if (legs.Count != 4)
            return false;

        // Legs are already sorted by strike in StrategyDispatcher
        // Expected Order for Gut Iron Fly (3 strikes):
        // 0: Long Call (Lowest Strike)
        // 1: Short Put (Middle Strike)
        // 2: Short Call (Middle Strike - same as Leg 1)
        // 3: Long Put (Highest Strike)

        var leg0 = legs[0];
        var leg1 = legs[1];
        var leg2 = legs[2];
        var leg3 = legs[3];

        if (leg0.Security is not Option opt0 ||
            leg1.Security is not Option opt1 ||
            leg2.Security is not Option opt2 ||
            leg3.Security is not Option opt3)
            return false;

        // Check Types: Call, Put, Call, Put
        if (opt0.PutCall != PutCall.Call ||
            opt1.PutCall != PutCall.Put ||
            opt2.PutCall != PutCall.Call ||
            opt3.PutCall != PutCall.Put)
            return false;

        // Check Expirations (assuming all need to match for a standard Iron Fly/Gut Fly)
        if (opt0.Expiration != opt1.Expiration ||
            opt1.Expiration != opt2.Expiration ||
            opt2.Expiration != opt3.Expiration)
            return false;

        // Check Sides: Outer legs same, inner legs same, outer != inner
        // Supports both Long and Short variants
        if (leg0.Side != leg3.Side ||
            leg1.Side != leg2.Side ||
            leg0.Side == leg1.Side)
            return false;

        // Check Quantities: 1:1:1:1 usually, but code just ensures structure mostly.
        // If we want equal quantities:
        if (leg0.Quantity != leg1.Quantity ||
            leg1.Quantity != leg2.Quantity ||
            leg2.Quantity != leg3.Quantity)
            return false;

        // Check Strikes Order: leg0 < leg1 == leg2 < leg3 (3 strikes total)
        // Middle two legs must have the same strike (this defines it as a "Fly")
        if (!(opt0.Strike < opt1.Strike &&
              opt1.Strike == opt2.Strike &&
              opt2.Strike < opt3.Strike))
            return false;

        // Wings Calculation
        double wing1 = opt1.Strike - opt0.Strike;
        double wing2 = opt3.Strike - opt2.Strike;

        string spreadTypeStr;
        string baseTypeStr;

        if (Math.Abs(wing1 - wing2) < 0.001)
        {
            spreadTypeStr = "GUT IRON FLY";
            baseTypeStr = "GUT_IRON_FLY";
        }
        else
        {
            spreadTypeStr = "GUT SKEWED IRON FLY";
            baseTypeStr = "GUT_SKEWED_IRON_FLY";
        }

        string typeStr = $"{spreadTypeStr} {opt0.Expiration:MMM-dd-yy} {opt0.Strike}/{opt1.Strike}/{opt2.Strike}/{opt3.Strike}";

        var side = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        string description = $"{(side == Side.Buy ? "LONG" : "SHORT")} {typeStr}";

        details = new StrategyIdentification(
            BaseType: baseTypeStr,
            SpreadType: typeStr,
            SpreadDescription: description
        );

        return true;
    }
}
