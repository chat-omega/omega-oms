using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallVertical : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_VERTICAL;
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        if (legs[0]!.Security is Option option1 && legs[1]!.Security is Option option2)
        {
            if (option1.PutCall == PutCall.Call && option2.PutCall == PutCall.Call)
            {
                bool sameExpiration = option1.Expiration == option2.Expiration;
                bool differentStrikes = option1.Strike != option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool oppositeSides = legs[0].Side != legs[1].Side;
                if (sameExpiration && sameQuantity && oppositeSides && differentStrikes)
                {
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs); 
                    string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
                    Side side = StrategyDispatcher.IdentifySpreadSide(Strategy, legs) ?? Side.Buy;
                    string spreadType = $"CALL VERTICAL {rootSymbols} {expirationDate} {option1.Strike}/{option2.Strike}";
                    string spreadDescription = (side == Side.Buy ? "BULL" : "BEAR") + spreadType;

                    details = new StrategyIdentification(
                        BaseType: "CALL VERTICAL",
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