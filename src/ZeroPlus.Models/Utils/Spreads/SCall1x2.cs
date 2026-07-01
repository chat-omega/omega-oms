using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCall1x2 : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_1X2;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // 1x2 requires exactly 2 legs, both call options
        if (legs.Count != 2)
            return false;
            
        if (legs[0]?.Security is not Option option1 || legs[1]?.Security is not Option option2)
            return false;
            
        if (option1.PutCall != PutCall.Call || option2.PutCall != PutCall.Call)
            return false;
            
        // Core 1x2 requirements
        bool sameExpiration = option1.Expiration == option2.Expiration;
        bool differentStrikes = option1.Strike != option2.Strike;
        bool oppositeSides = legs[0].Side != legs[1].Side;
        
        if (!sameExpiration || !differentStrikes || !oppositeSides)
            return false;
            
        // Specific 1x2 quantity validation
        var buyLeg = legs.First(leg => leg.Side == Side.Buy);
        var sellLeg = legs.First(leg => leg.Side == Side.Sell);
        
        bool is1x2Pattern = (buyLeg.Quantity == 1 && sellLeg.Quantity == 2) ||
                           (buyLeg.Quantity == 2 && sellLeg.Quantity == 1);
                           
        if (!is1x2Pattern)
            return false;
            
        // Determine direction: long ratio = more buy quantity
        bool isLongRatio = buyLeg.Quantity > sellLeg.Quantity;
        
        // Legs are already sorted by strike (ascending)
        double lowerStrike = option1.Strike;   // legs[0]
        double higherStrike = option2.Strike;  // legs[1]
        
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        
        string spreadType = $"CALL 1X2 {rootSymbols} {expirationDate} {lowerStrike}/{higherStrike}";
        string spreadDescription = $"{(isLongRatio ? "LONG" : "SHORT")} RATIO CALL SPREAD {spreadType}";

        details = new StrategyIdentification(
            BaseType: "CALL 1X2",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}