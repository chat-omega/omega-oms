using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SStraddle : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.STRADDLE;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        if (legs[0]!.Security is Option option1 && legs[1]!.Security is Option option2)
        {
            // Straddle requirements: one call, one put
            bool isCallAndPut = (option1.PutCall == PutCall.Put && option2.PutCall == PutCall.Call) ||
                               (option1.PutCall == PutCall.Call && option2.PutCall == PutCall.Put);
            
            if (isCallAndPut)
            {
                bool sameExpiration = option1.Expiration == option2.Expiration;
                bool sameStrike = option1.Strike == option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool sameSides = legs[0].Side == legs[1].Side;
                
                if (sameExpiration && sameQuantity && sameSides && sameStrike)
                {
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
                    string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
                    
                    // Use existing logic to determine if long or short
                    Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
                    bool isLongStraddle = spreadSide == Side.Buy;
                    
                    // Since legs are sorted and strikes are same, just use first option's strike
                    double strike = option1.Strike;
                    
                    string spreadType = $"STRADDLE {rootSymbols} {expirationDate} {strike}";
                    string spreadDescription = $"{(isLongStraddle ? "LONG" : "SHORT")} STRADDLE {rootSymbols} {expirationDate} {strike}";

                    details = new StrategyIdentification(
                        BaseType: "STRADDLE",
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