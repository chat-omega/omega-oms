using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCall2x3 : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_2X3;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call 2x3 requires exactly 2 legs, all call options
        if (legs.Count != 2 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all calls
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Call))
            return false;
            
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have 2 different strikes
        bool twoDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 2;
        
        if (!sameExpiration || !twoDifferentStrikes)
            return false;
            
        // Legs are already sorted by strike (ascending)
        var option1 = options[0]; // Lower strike
        var option2 = options[1]; // Higher strike
        
        // Must have opposite sides
        bool oppositeSides = legs[0].Side != legs[1].Side;
        
        if (!oppositeSides)
            return false;
            
        // 2x3 ratio pattern: one leg has quantity 2, other leg has quantity 3
        bool validRatioPattern = (legs[0].Quantity == 2 && legs[1].Quantity == 3) ||
                                (legs[0].Quantity == 3 && legs[1].Quantity == 2);
                                
        if (!validRatioPattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        
        // Determine if this is long or short ratio
        var buyLeg = legs.FirstOrDefault(l => l.Side == Side.Buy);
        var sellLeg = legs.FirstOrDefault(l => l.Side == Side.Sell);
        
        bool isLongRatio = buyLeg?.Quantity > sellLeg?.Quantity;
        
        string spreadType = $"CALL 2X3 {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}";
        string spreadDescription = $"{(isLongRatio ? "LONG" : "SHORT")} 2X3 RATIO CALL SPREAD {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}";

        details = new StrategyIdentification(
            BaseType: "CALL 2X3",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}