using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPut1x3x2 : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_1x3x2;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Put 1x3x2 requires exactly 3 legs, all put options
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
            
        // Legs are already sorted by strike (ascending)
        var option1 = options[0]; // Lowest strike
        var option2 = options[1]; // Middle strike  
        var option3 = options[2]; // Highest strike
        
        // 1x3x2 quantity pattern: legs must have quantities 1, 3, 2 in some order
        var quantities = legs.Select(l => l.Quantity).OrderBy(q => q).ToList();
        bool validQuantityPattern = quantities.SequenceEqual(new[] { 1, 2, 3 });
        
        if (!validQuantityPattern)
            return false;
            
        // Sort legs by quantity to analyze pattern
        var sortedByQty = legs.OrderBy(l => l.Quantity).ToList();
        var leg1Qty = sortedByQty[0]; // Quantity 1
        var leg2Qty = sortedByQty[1]; // Quantity 2
        var leg3Qty = sortedByQty[2]; // Quantity 3
        
        // Side pattern: specific asymmetric pattern for 1x3x2
        bool validSidePattern = leg1Qty.Side == leg2Qty.Side && leg3Qty.Side != leg1Qty.Side;
        
        if (!validSidePattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        double spacing1 = Math.Round(Math.Abs(option1.Strike - option2.Strike), 2);
        double spacing2 = Math.Round(Math.Abs(option2.Strike - option3.Strike), 2);
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongStrategy = spreadSide == Side.Buy;
        
        string spreadType = $"ONE THREE TWO {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}/{option3.Strike} PUT[{spacing1}/{spacing2}]";
        string spreadDescription = $"{(isLongStrategy ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "PUT 1X3X2",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}