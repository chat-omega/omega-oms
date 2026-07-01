using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallRatioDiagonal : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_CUSTOM_RATIO;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call Ratio Diagonal requires exactly 4 legs, all call options
        if (legs.Count != 4 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all calls
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Call))
            return false;
            
        // Must have exactly 2 different expirations (time spread characteristic)
        bool twoExpirations = options.Select(o => o.Expiration).Distinct().Count() == 2;
        
        // Must have exactly 3 different strikes (ratio characteristic)
        bool threeDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 3;
        
        if (!twoExpirations || !threeDifferentStrikes)
            return false;
            
        // Must have exactly 2 different quantities (ratio characteristic)
        var quantities = legs.Select(l => l.Quantity).Distinct().ToList();
        bool twoQuantities = quantities.Count == 2;
        
        if (!twoQuantities)
            return false;
            
        // Must have exactly 2 different sides (spread characteristic)
        bool twoSides = legs.Select(l => l.Side).Distinct().Count() == 2;
        
        if (!twoSides)
            return false;
            
        // Sort by expiration to identify near/far legs
        var sortedByExp = legs.OrderBy(l => ((Option)l!.Security!).Expiration).ToList();
        var nearExpiration = ((Option)sortedByExp.First()!.Security!).Expiration;
        var farExpiration = ((Option)sortedByExp.Last()!.Security!).Expiration;
        var nearExpLegs = legs.Where(l => ((Option)l!.Security!).Expiration == nearExpiration).ToList();
        var farExpLegs = legs.Where(l => ((Option)l!.Security!).Expiration != nearExpiration).ToList();
        
        // Validate structure: should have legs in both expirations
        bool validExpirationDistribution = nearExpLegs.Count >= 1 && farExpLegs.Count >= 1 && 
                                          nearExpLegs.Count + farExpLegs.Count == 4;
                                          
        if (!validExpirationDistribution)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        
        // Get all strikes for description
        var strikes = options.Select(o => o.Strike).Distinct().OrderBy(s => s).ToList();
        string strikesList = string.Join("/", strikes.Select(s => s.ToString()));
        
        // Build ratio pattern description
        var ratios = quantities.OrderBy(q => q).ToList();
        string ratioPattern = string.Join(":", ratios);
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongStrategy = spreadSide == Side.Buy;
        
        string spreadType = $"RATIO DIAGONAL {rootSymbols} {nearExpiration.ToString("MMM-dd-yy")}/{farExpiration.ToString("MMM-dd-yy")} {strikesList} CALL RATIO[{ratioPattern}]";
        string spreadDescription = $"{(isLongStrategy ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL RATIO DIAGONAL",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}