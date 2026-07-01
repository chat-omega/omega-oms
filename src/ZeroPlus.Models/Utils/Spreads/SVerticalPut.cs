using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPutVertical : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_VERTICAL;
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;

        // Pattern match to ensure both legs are options and cast them safely.
        if (legs[0]?.Security is Option option1 && legs[1]?.Security is Option option2)
        {
            // Verify both legs are Puts.
            if (option1.PutCall == PutCall.Put && option2.PutCall == PutCall.Put)
            {
                // Check the core conditions for a vertical spread.
                bool sameExpiration = option1.Expiration == option2.Expiration;
                bool differentStrikes = option1.Strike != option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool oppositeSides = legs[0].Side != legs[1].Side;

                if (sameExpiration && sameQuantity && oppositeSides && differentStrikes)
                {
                    // Rely on the dispatcher for shared logic.
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
                    string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
                    
                    // Identify the spread's side to determine if it's bullish or bearish.
                    Side side = StrategyDispatcher.IdentifySpreadSide(Strategy, legs) ?? Side.Buy;

                    // For put verticals, convention is to show higher strike first (opposite of calls)
                    string spreadType = $"PUT VERTICAL {rootSymbols} {expirationDate} {option2.Strike}/{option1.Strike}";

                    // For Puts, buying the higher strike is a BEAR spread (a debit).
                    // Selling the higher strike is a BULL spread (a credit).
                    string spreadDescription = (side == Side.Buy ? "BEAR " : "BULL ") + spreadType;

                    details = new StrategyIdentification(
                        BaseType: "PUT VERTICAL",
                        SpreadType: spreadType,
                        SpreadDescription: spreadDescription
                    );

                    return true;
                }
            }
        }
        return false;
    }
}
