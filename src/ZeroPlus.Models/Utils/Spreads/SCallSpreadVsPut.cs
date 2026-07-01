using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallSpreadVsPut : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_SPREAD_VS_PUT;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call Spread vs Put requires exactly 3 legs, all options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Must have exactly 2 calls and 1 put
        var callCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Call);
        var putCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Put);
        
        if (callCount != 2 || putCount != 1)
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
                            
        var leg0 = sortedLegs[0];
        var leg1 = sortedLegs[1]; 
        var leg2 = sortedLegs[2];
        
        var option0 = (Option)leg0.Security!;
        var option1 = (Option)leg1.Security!;
        var option2 = (Option)leg2.Security!;
        
        // Must have 3 different strikes
        bool threeDifferentStrikes = option0.Strike != option1.Strike && 
                                    option1.Strike != option2.Strike && 
                                    option0.Strike != option2.Strike;
                                    
        if (!threeDifferentStrikes)
            return false;
            
        // Strikes must be in ascending order (guaranteed by sorting, but let's be explicit)
        bool ascendingStrikes = option0.Strike < option1.Strike && option1.Strike < option2.Strike;
        
        if (!ascendingStrikes)
            return false;
            
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => Math.Abs(l.Quantity) == Math.Abs(legs[0].Quantity));
        
        if (!sameQuantity)
            return false;
            
        // Side pattern: outer legs (0 and 2) same side, middle leg (1) opposite side
        bool validSidePattern = leg0.Side == leg2.Side && leg1.Side != leg0.Side;
        
        if (!validSidePattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        double spacing = Math.Round(Math.Abs(option2.Strike - option0.Strike), 2);
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongStrategy = spreadSide == Side.Buy;
        
        string spreadType = $"CALL SPREAD VS PUT {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike} [{spacing}]";
        string spreadDescription = $"{(isLongStrategy ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL SPREAD VS PUT",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}