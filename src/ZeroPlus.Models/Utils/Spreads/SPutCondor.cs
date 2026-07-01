using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPutCondor : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_CONDOR;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Put Condor requires exactly 4 legs, all options
        if (legs.Count != 4 || !legs.All(l => l?.Security is Option))
            return false;
            
        // Cast all legs to options and verify they're all puts
        var options = legs.Select(l => (Option)l!.Security!).ToList();
        
        if (!options.All(o => o.PutCall == PutCall.Put))
            return false;
            
        // All options must have same expiration
        bool sameExpiration = options.All(o => o.Expiration == options[0].Expiration);
        
        // Must have 4 different strikes
        bool fourDifferentStrikes = options.Select(o => o.Strike).Distinct().Count() == 4;
        
        // All legs must have same quantity
        bool sameQuantity = legs.All(l => l.Quantity == legs[0].Quantity);
        
        if (!sameExpiration || !fourDifferentStrikes || !sameQuantity)
            return false;
            
        // Legs are already sorted by strike (ascending)
        var option0 = options[0]; // Lowest strike put
        var option1 = options[1]; // Second lowest strike put
        var option2 = options[2]; // Second highest strike put
        var option3 = options[3]; // Highest strike put
        
        // Put Condor side pattern: Buy-Sell-Sell-Buy OR Sell-Buy-Buy-Sell
        bool longCondorPattern = legs[0].Side == Side.Buy && 
                                legs[1].Side == Side.Sell && 
                                legs[2].Side == Side.Sell && 
                                legs[3].Side == Side.Buy;
                                
        bool shortCondorPattern = legs[0].Side == Side.Sell && 
                                 legs[1].Side == Side.Buy && 
                                 legs[2].Side == Side.Buy && 
                                 legs[3].Side == Side.Sell;
        
        if (!longCondorPattern && !shortCondorPattern)
            return false;
            
        // Build the description
        string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
        string expirationDate = option0.Expiration.ToString("MMM-dd-yy");
        
        // Use existing logic to determine spread side
        Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
        bool isLongPutCondor = spreadSide == Side.Buy;
        
        string spreadType = $"PUT CONDOR {rootSymbols} {expirationDate} {option0.Strike}/{option1.Strike}/{option2.Strike}/{option3.Strike}";
        string spreadDescription = $"{(isLongPutCondor ? "LONG" : "SHORT")} {spreadType}";

        details = new StrategyIdentification(
            BaseType: "PUT CONDOR",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}