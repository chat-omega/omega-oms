using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPutTree : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_TREE;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Put Tree requires exactly 3 legs, all put options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all puts
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Put))
            return false;
            
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have 3 different strikes
        bool threeDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 3;
        
        if (!sameExpiration || !threeDifferentStrikes)
            return false;
            
        // Tree requires all quantities = 1
        bool allQuantitiesOne = legs.All(l => l.Quantity == 1);
        
        if (!allQuantitiesOne)
            return false;
            
        // Tree side pattern: one leg has different side from the other two
        // Either leg[0] different from leg[1] and leg[2], OR leg[1] different from leg[0] and leg[2]
        bool validSidePattern = (legs[0].Side != legs[1].Side && legs[0].Side != legs[2].Side) ||
                               (legs[0].Side != legs[2].Side && legs[1].Side != legs[2].Side);
                               
        if (!validSidePattern)
            return false;
            
        // Legs are already sorted by strike (ascending)
        var option1 = options[0]; // Lowest strike
        var option2 = options[1]; // Middle strike  
        var option3 = options[2]; // Highest strike
        
        // Calculate spacing (original shows spacing1, but tree may not need equal spacing)
        double spacing1 = Math.Round(Math.Abs(option1.Strike - option2.Strike), 2);
        
        // Build the description with side information for consistency
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongTree = spreadSide == Side.Buy;
        
        string spreadType = $"TREE {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}/{option3.Strike} PUT[{spacing1}]";
        string spreadDescription = $"{(isLongTree ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "PUT TREE",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}