using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCoveredPut : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.COVERED_PUT;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        // Covered Put requires exactly 2 legs
        if (legs.Count != 2)
            return false;
            
        // Must have exactly 1 stock and 1 option
        var stockLeg = legs.FirstOrDefault(x => x?.Security?.SecurityType == SecurityType.Stock);
        var optionLeg = legs.FirstOrDefault(x => x?.Security?.SecurityType == SecurityType.Option);
        
        if (stockLeg == null || optionLeg == null)
            return false;
            
        // Option must be a put
        if (optionLeg.Security is not Option option || option.PutCall != PutCall.Put)
            return false;
            
        // Stock symbol must match option's underlying symbol
        bool symbolsMatch = stockLeg.Symbol == option.Underlying?.Symbol;
        
        if (!symbolsMatch)
            return false;
            
        // Sides must be opposite (short stock + long put OR long stock + short put)
        bool oppositeSides = (stockLeg.Side == Side.Buy && optionLeg.Side == Side.Sell) || 
                            (stockLeg.Side == Side.Sell && optionLeg.Side == Side.Buy);
                            
        if (!oppositeSides)
            return false;
            
        // Quantities must match (stock quantity = option quantity * option multiplier)
        bool quantitiesMatch = stockLeg.Quantity == optionLeg.Quantity * option.Multiplier;
        
        if (!quantitiesMatch)
            return false;
            
        // Build the description
        string expirationDate = option.Expiration.ToString("MMM-dd-yy");
        string spreadType = $"COVERED {stockLeg.Symbol} {expirationDate} {option.Strike} PUT/{stockLeg.Symbol}";
        string spreadDescription = $"COVERED {stockLeg.Symbol} {expirationDate} {option.Strike} PUT/{stockLeg.Symbol}";

        details = new StrategyIdentification(
            BaseType: "PUT COVERED",
            SpreadType: spreadType,
            SpreadDescription: spreadDescription
        );

        return true;
    }
}