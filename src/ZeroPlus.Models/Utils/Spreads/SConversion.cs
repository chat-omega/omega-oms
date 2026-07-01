using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SConversion : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CONVERSION;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Conversion requires exactly 3 legs
        if (legs.Count != 3)
            return false;
            
        // Must have exactly 1 stock and 2 options
        var stockLeg = legs.FirstOrDefault(x => x?.Security?.SecurityType == SecurityType.Stock);
        var optionLegs = legs.Where(x => x?.Security?.SecurityType == SecurityType.Option).ToList();
        
        if (stockLeg == null || optionLegs.Count != 2)
            return false;
            
        // Must have exactly 1 call and 1 put
        var callLeg = optionLegs.FirstOrDefault(x => ((Option)x!.Security!).PutCall == PutCall.Call);
        var putLeg = optionLegs.FirstOrDefault(x => ((Option)x!.Security!).PutCall == PutCall.Put);
        
        if (callLeg == null || putLeg == null)
            return false;
            
        var callOption = (Option)callLeg.Security!;
        var putOption = (Option)putLeg.Security!;
        
        // Options must have same expiration
        bool sameExpiration = callOption.Expiration == putOption.Expiration;
        
        // Options must have same strike
        bool sameStrike = callOption.Strike == putOption.Strike;
        
        if (!sameExpiration || !sameStrike)
            return false;
            
        // Stock symbol must match option underlying symbol
        bool symbolsMatch = stockLeg.Symbol == callOption.Underlying?.Symbol &&
                           stockLeg.Symbol == putOption.Underlying?.Symbol;
                           
        if (!symbolsMatch)
            return false;
            
        // Quantities must match (stock quantity = option quantity * multiplier)
        bool quantitiesMatch = stockLeg.Quantity == callLeg.Quantity * callOption.Multiplier &&
                              stockLeg.Quantity == putLeg.Quantity * putOption.Multiplier &&
                              callLeg.Quantity == putLeg.Quantity;
                              
        if (!quantitiesMatch)
            return false;
            
        // Conversion side pattern: call sell, put buy, stock buy
        bool validSidePattern = callLeg.Side == Side.Sell &&
                               putLeg.Side == Side.Buy &&
                               stockLeg.Side == Side.Buy;
                               
        if (!validSidePattern)
            return false;
            
        // Build the description
        string expirationDate = callOption.Expiration.ToString("MMM-dd-yy");
        string spreadType = $"CONVERSION {stockLeg.Symbol} {expirationDate} {callOption.Strike}";
        string spreadDescription = spreadType;

        details = new StrategyIdentification(
            BaseType: "CONVERSION",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}