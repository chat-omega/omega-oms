using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

/// <summary>
/// Represents a Gut Iron Condor or Gut Skewed Iron Condor strategy.
/// <para>
/// A Gut Iron Condor is a 4-leg, 4-strike strategy where the short options are ITM (Gut):
/// Leg 0: Long Put (Lowest Strike)
/// Leg 1: Short Call (Second Strike)
/// Leg 2: Short Put (Third Strike)
/// Leg 3: Long Call (Highest Strike)
/// </para>
/// <para>
/// The "body" width is the distance between the two middle strikes (short positions).
/// If the wings (distance from outer to inner strikes) are equal, it is a "GUT IRON CONDOR".
/// If the wings are unequal, it is a "GUT SKEWED IRON CONDOR".
/// </para>
/// </summary>
public class SGutIronCondor : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.GUT_IRON_CONDOR;

    /// <inheritdoc/>
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        if (legs.Count != 4)
            return false;

        // Legs are already sorted by strike in StrategyDispatcher
        // Expected Order for Gut Iron Condor (4 strikes):
        // 0: Long Put (Lowest Strike)
        // 1: Short Call (Second Strike)
        // 2: Short Put (Third Strike)
        // 3: Long Call (Highest Strike)

        var leg0 = legs[0];
        var leg1 = legs[1];
        var leg2 = legs[2];
        var leg3 = legs[3];

        if (leg0.Security is not Option opt0 ||
            leg1.Security is not Option opt1 ||
            leg2.Security is not Option opt2 ||
            leg3.Security is not Option opt3)
            return false;

        // Check Types: Put, Call, Put, Call
        if (opt0.PutCall != PutCall.Put ||
            opt1.PutCall != PutCall.Call ||
            opt2.PutCall != PutCall.Put ||
            opt3.PutCall != PutCall.Call)
            return false;

        // Check Expirations (all must match)
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

        // Check Quantities: 1:1:1:1 ratio
        if (leg0.Quantity != leg1.Quantity ||
            leg1.Quantity != leg2.Quantity ||
            leg2.Quantity != leg3.Quantity)
            return false;

        // Check Strikes Order: leg0 < leg1 < leg2 < leg3 (4 different strikes)
        if (!(opt0.Strike < opt1.Strike &&
              opt1.Strike < opt2.Strike &&
              opt2.Strike < opt3.Strike))
            return false;

        // Wings Calculation: distance from outer to inner strikes
        double wing1 = opt1.Strike - opt0.Strike;  // Left wing: Short Call - Long Put
        double wing2 = opt3.Strike - opt2.Strike;  // Right wing: Long Call - Short Put

        string spreadTypeStr;
        string baseTypeStr;

        // Tolerance for floating point comparison
        if (Math.Abs(wing1 - wing2) < 0.001)
        {
            spreadTypeStr = "GUT IRON CONDOR";
            baseTypeStr = "GUT_IRON_CONDOR";
        }
        else
        {
            spreadTypeStr = "GUT SKEWED IRON CONDOR";
            baseTypeStr = "GUT_SKEWED_IRON_CONDOR";
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
