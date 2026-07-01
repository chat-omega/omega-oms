using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SMarriedStraddle : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.MARRIED_STRADDLE;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Married Straddle requires exactly 3 legs, all options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Must have exactly 2 puts and 1 call
        var putCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Put);
        var callCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Call);
        
        if (putCount != 2 || callCount != 1)
            return false;
            
        // Cast all legs to options for easier access
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        if (!sameExpiration)
            return false;
            
        // Sort legs by strike, then by option type (puts before calls at same strike)
        var sortedLegs = legs.OrderBy(l => ((Option)l!.Security!).Strike)
                            .ThenBy(l => ((Option)l!.Security!).PutCall)
                            .ToList();
                            
        var leg0 = sortedLegs[0]; // Lower strike put
        var leg1 = sortedLegs[1]; // Higher strike put
        var leg2 = sortedLegs[2]; // Higher strike call (same as higher put)
        
        var option0 = (Option)leg0.Security!;
        var option1 = (Option)leg1.Security!;
        var option2 = (Option)leg2.Security!;
        
        // Married straddle structure: put at lower strike + straddle at higher strike
        bool straddleStrike = option1.Strike == option2.Strike;
        bool lowerPutStrike = option0.Strike < option1.Strike;
        
        if (!straddleStrike || !lowerPutStrike)
            return false;
            
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => Math.Abs(l.Quantity) == Math.Abs(legs[0].Quantity));
        
        if (!sameQuantity)
            return false;
            
        // Side pattern: straddle legs same side, lower put opposite side
        bool validSidePattern = leg1.Side == leg2.Side && leg0.Side != leg1.Side;
        
        if (!validSidePattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        double spacing = Math.Round(Math.Abs(option1.Strike - option0.Strike), 2);
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongStrategy = spreadSide == Side.Buy;
        
        string spreadType = $"MARRIED STRADDLE {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike} [{spacing}]";
        string spreadDescription = $"{(isLongStrategy ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "MARRIED STRADDLE",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}