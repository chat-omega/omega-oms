using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCall1x3x3x1 : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_1X3X3X1;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Call 1x3x3x1 requires exactly 4 legs, all call options
        if (legs.Count != 4 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all calls
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Call))
            return false;
            
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have 4 different strikes
        bool fourDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 4;
        
        if (!sameExpiration || !fourDifferentStrikes)
            return false;
            
        // 1x3x3x1 quantity pattern: legs must have quantities 1, 3, 3, 1
        var quantities = legs.Select(l => l.Quantity).OrderBy(q => q).ToList();
        bool validQuantityPattern = quantities.SequenceEqual(new[] { 1, 1, 3, 3 });
        
        if (!validQuantityPattern)
            return false;
            
        // Legs are already sorted by strike (ascending)
        var option0 = options[0]; // Lowest strike
        var option1 = options[1]; // Second lowest strike
        var option2 = options[2]; // Second highest strike
        var option3 = options[3]; // Highest strike
        
        // Calculate strike spacing
        double spacing1 = Math.Round(Math.Abs(option0.Strike - option1.Strike), 2);
        double spacing2 = Math.Round(Math.Abs(option1.Strike - option2.Strike), 2);
        double spacing3 = Math.Round(Math.Abs(option2.Strike - option3.Strike), 2);
        
        // Equal spacing validation (typically required for 1x3x3x1)
        bool equalSpacing = Math.Abs(spacing1 - spacing2) < 0.01 && 
                           Math.Abs(spacing2 - spacing3) < 0.01;
                           
        if (!equalSpacing)
            return false;
            
        // 1x3x3x1 side pattern: outer legs (1 qty) same side, inner legs (3 qty) opposite side
        var outerLegs = legs.Where(l => l.Quantity == 1).ToList();
        var innerLegs = legs.Where(l => l.Quantity == 3).ToList();
        
        bool validSidePattern = outerLegs.All(l => l.Side == outerLegs[0].Side) &&
                               innerLegs.All(l => l.Side == innerLegs[0].Side) &&
                               outerLegs[0].Side != innerLegs[0].Side;
        
        if (!validSidePattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongStrategy = spreadSide == Side.Buy;
        
        string spreadType = $"1X3X3X1 {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike}/{option2.Strike}/{option3.Strike} CALL[{spacing1}/{spacing2}/{spacing3}]";
        string spreadDescription = $"{(isLongStrategy ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL 1X3X3X1",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}