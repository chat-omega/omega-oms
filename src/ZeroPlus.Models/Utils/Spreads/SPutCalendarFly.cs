using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPutCalendarFly : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_CALENDAR_FLY;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Put Calendar Fly requires exactly 3 legs, all put options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all puts
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Put))
            return false;
            
        // All options must have the SAME strike price
        bool sameStrike = options.All(o => o.Strike == options[0].Strike);
        
        // Must have 3 different expirations
        bool threeDifferentExpirations = options.Select(o => o.Expiration).Distinct().Count() == 3;
        
        if (!sameStrike || !threeDifferentExpirations)
            return false;
            
        // Sort legs by expiration to analyze pattern
        var sortedLegs = legs.OrderBy(l => ((Option)l!.Security!).Expiration).ToList();
        
        var leg0 = sortedLegs[0]; // Nearest expiration
        var leg1 = sortedLegs[1]; // Middle expiration  
        var leg2 = sortedLegs[2]; // Farthest expiration
        
        // Calendar fly quantity pattern: outer legs same qty, middle leg = 2x outer qty (like butterfly)
        bool validQuantityPattern = leg0.Quantity == leg2.Quantity && 
                                   leg1.Quantity == leg0.Quantity * 2;
                                   
        if (!validQuantityPattern)
            return false;
            
        // Calendar fly side pattern: outer legs same side, middle leg opposite side
        bool validSidePattern = leg0.Side == leg2.Side && leg0.Side != leg1.Side;
        
        if (!validSidePattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        
        var option0 = (Option)leg0.Security!;
        var option1 = (Option)leg1.Security!;
        var option2 = (Option)leg2.Security!;
        
        string nearExpiration = option0.Expiration.ToString("MMM-dd-yy");
        string middleExpiration = option1.Expiration.ToString("MMM-dd-yy");
        string farExpiration = option2.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongCalendarFly = spreadSide == Side.Buy;
        
        string spreadType = $"CALENDAR FLY {rootSymbols} {nearExpiration}/{middleExpiration}/{farExpiration} {option0.Strike} PUT";
        string spreadDescription = $"{(isLongCalendarFly ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "PUT CALENDAR FLY",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}