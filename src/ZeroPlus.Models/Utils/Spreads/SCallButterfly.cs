using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallButterfly : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_BUTTERFLY;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call Butterfly requires exactly 3 legs, all call options
        if (legs.Count != 3 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all calls
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Call))
            return false;
            
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have 3 different strikes
        bool threeDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 3;
        
        if (!sameExpiration || !threeDifferentStrikes)
            return false;
            
        // Legs are already sorted by strike (ascending)
        var option1 = options[0]; // Lowest strike
        var option2 = options[1]; // Middle strike  
        var option3 = options[2]; // Highest strike
        
        // Calculate spacing between strikes
        double spacing1 = Math.Round(Math.Abs(option1.Strike - option2.Strike), 2);
        double spacing2 = Math.Round(Math.Abs(option2.Strike - option3.Strike), 2);
        
        // Butterfly requires equal spacing
        bool equalSpacing = Math.Abs(spacing1 - spacing2) < 0.01;
        
        if (!equalSpacing)
            return false;
            
        // Butterfly quantity pattern: outer legs same qty, middle leg = 2x outer qty
        int outerLeg1Qty = legs[0].Quantity;
        int middleLegQty = legs[1].Quantity;
        int outerLeg2Qty = legs[2].Quantity;
        
        bool validQuantityPattern = outerLeg1Qty == outerLeg2Qty && 
                                   middleLegQty == outerLeg1Qty * 2;
                                   
        if (!validQuantityPattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongButterfly = spreadSide == Side.Buy;
        
        string spreadType = $"CALL BUTTERFLY {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}/{option3.Strike} [{spacing1}]";
        string spreadDescription = $"{(isLongButterfly ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL BUTTERFLY",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}