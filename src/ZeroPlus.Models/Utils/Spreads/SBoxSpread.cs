using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SBoxSpread : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.BOX;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Box Spread requires exactly 4 legs, all options
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
        
        // Must have exactly 2 different strikes (not 4 like Iron Condor)
        bool twoDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 2;
        
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => l.Quantity == legs[0].Quantity);
        
        if (!sameExpiration || !twoDifferentStrikes || !sameQuantity)
            return false;
            
        // Legs are already sorted by strike, then by PutCall (Put=1, Call=2)
        // So: legs[0]=lower put, legs[1]=lower call, legs[2]=higher put, legs[3]=higher call
        var option0 = (Option)legs[0]!.Security!; // Lower strike put
        var option1 = (Option)legs[1]!.Security!; // Lower strike call  
        var option2 = (Option)legs[2]!.Security!; // Higher strike put
        var option3 = (Option)legs[3]!.Security!; // Higher strike call
        
        // Verify the structure is correct for Box Spread
        bool correctStructure = option0.PutCall == PutCall.Put &&
                               option1.PutCall == PutCall.Call &&
                               option2.PutCall == PutCall.Put &&
                               option3.PutCall == PutCall.Call &&
                               option0.Strike == option1.Strike &&  // Same strike for put/call pairs
                               option2.Strike == option3.Strike &&  // Same strike for put/call pairs
                               option0.Strike < option2.Strike;     // Lower strike < higher strike
                               
        if (!correctStructure)
            return false;
            
        // Box Spread side pattern: specific arbitrage patterns
        // Pattern 1: legs[1] buy, legs[3] sell, legs[2] buy, legs[0] sell
        // Pattern 2: legs[1] sell, legs[3] buy, legs[2] sell, legs[0] buy
        bool validSidePattern1 = legs[1].Side == Side.Buy && 
                                legs[3].Side == Side.Sell && 
                                legs[2].Side == Side.Buy && 
                                legs[0].Side == Side.Sell;
                                
        bool validSidePattern2 = legs[1].Side == Side.Sell && 
                                legs[3].Side == Side.Buy && 
                                legs[2].Side == Side.Sell && 
                                legs[0].Side == Side.Buy;
        
        if (!validSidePattern1 && !validSidePattern2)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongBox = spreadSide == Side.Buy;
        
        string spreadType = $"BOX {rootSymbols} {expirationDate} {option0.Strike}/{option2.Strike}";
        string spreadDescription = $"{(isLongBox ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "BOX",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}