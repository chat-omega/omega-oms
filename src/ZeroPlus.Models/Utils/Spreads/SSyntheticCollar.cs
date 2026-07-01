using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SSyntheticCollar : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.SYNTHETIC_COLLAR;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Synthetic Collar requires exactly 4 legs, all options
        if (legs.Count != 4 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Must have exactly 2 calls and 2 puts
        var callCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Call);
        var putCount = legs.Count(l => ((Option)l!.Security!).PutCall == PutCall.Put);
        
        if (callCount != 2 || putCount != 2)
            return false;
            
        // Cast all legs to options for easier access
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have exactly 3 different strikes (like Iron Butterfly)
        bool threeDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 3;
        
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => l.Quantity == legs[0].Quantity);
        
        if (!sameExpiration || !threeDifferentStrikes || !sameQuantity)
            return false;
            
        // Legs are already sorted by strike, then by PutCall (Put=1, Call=2)
        var option0 = (Option)legs[0]!.Security!; // Lowest strike
        var option1 = (Option)legs[1]!.Security!; // Middle strike (first type)
        var option2 = (Option)legs[2]!.Security!; // Middle strike (second type)  
        var option3 = (Option)legs[3]!.Security!; // Highest strike
        
        // Synthetic Collar structure: middle two legs same strike, different types (like Iron Butterfly)
        bool middleLegsStructure = option1.Strike == option2.Strike && 
                                  option1.PutCall != option2.PutCall;
                                  
        if (!middleLegsStructure)
            return false;
            
        // Option type pattern: outer legs same type, inner legs same type, but outer != inner
        bool correctOptionTypes = option0.PutCall == option1.PutCall &&
                                 option2.PutCall == option3.PutCall &&
                                 option0.PutCall != option3.PutCall;
                                 
        if (!correctOptionTypes)
            return false;
            
        // Synthetic Collar side pattern: outer legs same side, inner legs same side, but outer != inner
        bool outerLegsSameSide = legs[0].Side == legs[1].Side;
        bool innerLegsSameSide = legs[2].Side == legs[3].Side;
        bool outerInnerOpposite = legs[0].Side != legs[2].Side;
        
        if (!outerLegsSameSide || !innerLegsSameSide || !outerInnerOpposite)
            return false;
            
        // Calculate spacing
        double spacing1 = Math.Round(Math.Abs(option0.Strike - option1.Strike), 2);
        double spacing2 = Math.Round(Math.Abs(option2.Strike - option3.Strike), 2);
        string spacingString = spacing1 == spacing2 ? $"[{spacing1}]" : $"[{spacing1}/{spacing2}]";
        
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongSyntheticCollar = spreadSide == Side.Buy;
        
        string spreadType = $"SYNTHETIC COLLAR {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike}/{option3.Strike} {spacingString}";
        string spreadDescription = $"{(isLongSyntheticCollar ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "SYNTHETIC COLLAR",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}