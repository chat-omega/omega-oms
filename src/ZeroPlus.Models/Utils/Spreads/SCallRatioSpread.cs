using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallRatioSpread : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_CUSTOM_RATIO;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Ratio spread requires exactly 2 legs, both call options
        if (legs.Count != 2)
            return false;
            
        if (legs[0]?.Security is not Option option1 || legs[1]?.Security is not Option option2)
            return false;
            
        if (option1.PutCall != PutCall.Call || option2.PutCall != PutCall.Call)
            return false;
            
        // Core ratio spread requirements
        bool sameExpiration = option1.Expiration == option2.Expiration;
        bool differentStrikes = option1.Strike != option2.Strike;
        bool oppositeSides = legs[0].Side != legs[1].Side;
        bool differentQuantities = legs[0].Quantity != legs[1].Quantity;
        
        if (!sameExpiration || !differentStrikes || !oppositeSides || !differentQuantities)
            return false;
            
        // Determine quantities and direction
        var buyLeg = legs.First(leg => leg.Side == Side.Buy);
        var sellLeg = legs.First(leg => leg.Side == Side.Sell);
        
        int buyQty = buyLeg.Quantity;
        int sellQty = sellLeg.Quantity;
        
        // Determine if it's long or short ratio
        bool isLongRatio = buyQty > sellQty;
        
        // Build ratio string (smaller quantity first)
        int minQty = System.Math.Min(buyQty, sellQty);
        int maxQty = System.Math.Max(buyQty, sellQty);
        string ratioString = $"{minQty}X{maxQty}";
        
        // Legs are already sorted by strike (ascending)
        double lowerStrike = option1.Strike;   // legs[0]
        double higherStrike = option2.Strike;  // legs[1]
        
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
        
        string spreadType = $"{ratioString} RATIO CALL SPREAD {rootSymbols} {ratioString} {expirationDate} {lowerStrike}/{higherStrike}";
        string spreadDescription = $"{(isLongRatio ? "LONG" : "SHORT")} {spreadType}";

        // Determine appropriate baseType based on the ratio
        string baseType = "CALL RATIO";

        details = new StrategyIdentification(
            BaseType: baseType,
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}