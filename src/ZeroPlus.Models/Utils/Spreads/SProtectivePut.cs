using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;


/// <summary>
/// Identifies a Protective Put strategy.
/// A Protective Put is a hedging strategy consisting of a long stock position
/// and a long put option on the same underlying asset.
/// </summary>
public class SProtectivePut : IStrategy
{
    // 1. IStrategy Interface Implementation (with new properties)
    public BaseStrategy Strategy => BaseStrategy.PROTECTIVE_PUT;
    public int LegCount => 2;
    public int OptionLegCount => 1;
    public int StockLegCount => 1;

    /// <summary>
    /// Attempts to identify the Protective Put strategy from a list of trading legs.
    /// The method looks for one long stock leg and one long put option leg with
    /// matching underlying symbols and quantities.
    /// </summary>
    /// <param name="legs">The list of legs in the complex order.</param>
    /// <param name="details">The identified strategy details if successful; otherwise, null.</param>
    /// <returns>True if a Protective Put is successfully identified; otherwise, false.</returns>
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        // 2. Find the specific legs without relying on sort order.
        // This is more robust for mixed-type strategies.
        var stockLeg = legs.FirstOrDefault(l => l.Security?.SecurityType == SecurityType.Stock);
        var optionLeg = legs.FirstOrDefault(l => l.Security is Option);

        // 3. Guard Clause: Ensure one of each type was found.
        if (stockLeg == null || optionLeg == null)
        {
            return false;
        }

        // 4. Pattern match to safely access option properties.
        if (optionLeg.Security is Option option)
        {
            // 5. Check the core conditions for a Protective Put.
            bool isPutOption = option.PutCall == PutCall.Put;
            bool bothLegsAreLong = stockLeg.Side == Side.Buy && optionLeg.Side == Side.Buy;
            
            // Ensure the option is for the correct underlying stock.
            bool underlyingMatches = string.Equals(stockLeg!.Security!.Symbol, option.Underlying?.Symbol, StringComparison.OrdinalIgnoreCase);
            
            // Validate that the quantities align (e.g., 100 shares for 1 option contract).
            bool quantitiesMatch = stockLeg.Quantity == (optionLeg.Quantity * option.Multiplier);

            if (isPutOption && bothLegsAreLong && underlyingMatches && quantitiesMatch)
            {
                // 6. If all conditions match, construct the details.
                string rootSymbol = stockLeg.Security.Symbol;
                string expirationDate = option.Expiration.ToString("MMM-dd-yy");

                // The format matches the one in the original EvaluateLegs method.
                string spreadType = $"PROTECTIVE PUT {rootSymbol} {expirationDate} {option.Strike} PUT/{rootSymbol}";
                Side side = StrategyDispatcher.IdentifySpreadSide(Strategy, legs) ?? Side.Buy;
                string spreadDescription = (side == Side.Buy ? "BEAR " : "BULL ") + spreadType;


                details = new StrategyIdentification(
                    BaseType: "PROTECTIVE PUT",
                    SpreadType: spreadType,
                    SpreadDescription: spreadDescription
                );

                return true;
            }
        }

        return false;
    }
}
