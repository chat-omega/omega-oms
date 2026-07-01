using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SIronCondor : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.IRON_CONDOR;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Iron Condor requires exactly 4 legs, all options
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
        
        // Must have 4 different strikes
        bool fourDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 4;
        
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => l.Quantity == legs[0].Quantity);
        
        if (!sameExpiration || !fourDifferentStrikes || !sameQuantity)
            return false;
            
        // Legs are already sorted by strike, then by PutCall (Put=1, Call=2)
        // So: legs[0]=lower put, legs[1]=higher put, legs[2]=lower call, legs[3]=higher call
        var option0 = (Option)legs[0]!.Security!; // Lower strike put
        var option1 = (Option)legs[1]!.Security!; // Higher strike put  
        var option2 = (Option)legs[2]!.Security!; // Lower strike call
        var option3 = (Option)legs[3]!.Security!; // Higher strike call
        
        // Verify the strike ordering and option types are correct for Iron Condor
        bool correctStructure = option0.PutCall == PutCall.Put &&
                               option1.PutCall == PutCall.Put &&
                               option2.PutCall == PutCall.Call &&
                               option3.PutCall == PutCall.Call &&
                               option0.Strike < option1.Strike &&
                               option1.Strike < option2.Strike &&
                               option2.Strike < option3.Strike;
                               
        if (!correctStructure)
            return false;
            
        // Iron Condor side pattern: outer legs same side, inner legs same side, but opposite
        bool outerLegsSameSide = legs[0].Side == legs[3].Side;
        bool innerLegsSameSide = legs[1].Side == legs[2].Side;
        bool outerInnerOpposite = legs[0].Side != legs[1].Side;
        
        if (!outerLegsSameSide || !innerLegsSameSide || !outerInnerOpposite)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongIronCondor = spreadSide == Side.Buy;
        
        string spreadType = $"IRON CONDOR {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike}/{option2.Strike}/{option3.Strike} PUT/CALL";
        string spreadDescription = $"{(isLongIronCondor ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "IRON CONDOR",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}