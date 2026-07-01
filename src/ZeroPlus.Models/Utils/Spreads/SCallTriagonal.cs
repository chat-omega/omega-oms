using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallTriagonal : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_TRIAGONAL;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call Triagonal requires exactly 3 legs, all call options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all calls
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Call))
            return false;
            
        // Must have exactly 2 different expirations
        bool twoExpirations = options.Select(o => o.Expiration).Distinct().Count() == 2;
        
        // Must have exactly 3 different strikes
        bool threeDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 3;
        
        if (!twoExpirations || !threeDifferentStrikes)
            return false;
            
        // Sort legs by strike, then by quantity to analyze pattern
        var sortedLegs = legs.OrderBy(l => ((Option)l!.Security!).Strike)
                            .ThenBy(l => l.Quantity)
                            .ToList();
                            
        var leg0 = sortedLegs[0];
        var leg1 = sortedLegs[1]; 
        var leg2 = sortedLegs[2];
        
        var option0 = (Option)leg0.Security!;
        var option1 = (Option)leg1.Security!;
        var option2 = (Option)leg2.Security!;
        
        // Triagonal quantity pattern: first two legs same qty, third leg = sum of first two
        bool validQuantityPattern = leg0.Quantity == leg1.Quantity && 
                                   leg0.Quantity + leg1.Quantity == leg2.Quantity;
                                   
        if (!validQuantityPattern)
            return false;
            
        // Triagonal side pattern: first two legs same side, third leg opposite side
        bool validSidePattern = leg0.Side == leg1.Side && leg1.Side != leg2.Side;
        
        if (!validSidePattern)
            return false;
            
        // Triagonal expiration pattern: first two legs same expiration, third leg different
        bool validExpirationPattern = option0.Expiration == option1.Expiration && 
                                     option1.Expiration != option2.Expiration;
                                     
        if (!validExpirationPattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string nearExpiration = option0.Expiration.ToString("MMM-dd-yy");
        string farExpiration = option2.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongTriagonal = spreadSide == Side.Buy;
        
        string spreadType = $"TRIAGONAL {rootSymbols} {nearExpiration}/{farExpiration} {option0.Strike}/{option1.Strike}/{option2.Strike} CALL";
        string spreadDescription = $"{(isLongTriagonal ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL TRIAGONAL",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}