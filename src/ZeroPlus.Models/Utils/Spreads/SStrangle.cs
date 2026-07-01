using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SStrangle : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.STRANGLE;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        if (legs[0]!.Security is Option option1 && legs[1]!.Security is Option option2)
        {
            // Strangle requirements: one call, one put
            bool isCallAndPut = (option1.PutCall == PutCall.Put && option2.PutCall == PutCall.Call) ||
                               (option1.PutCall == PutCall.Call && option2.PutCall == PutCall.Put);
            
            if (isCallAndPut)
            {
                bool sameExpiration = option1.Expiration == option2.Expiration;
                bool differentStrikes = option1.Strike != option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool sameSides = legs[0].Side == legs[1].Side;
                
                // For a valid strangle, call strike must be higher than put strike
                var putOption = option1.PutCall == PutCall.Put ? option1 : option2;
                var callOption = option1.PutCall == PutCall.Call ? option1 : option2;
                bool callStrikeHigher = callOption.Strike > putOption.Strike;
                
                if (sameExpiration && sameQuantity && sameSides && differentStrikes && callStrikeHigher)
                {
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
                    string expirationDate = option1.Expiration.ToString("MMM-dd-yy");
                    
                    // Use existing logic to determine if long or short
                    Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
                    bool isLongStrangle = spreadSide == Side.Buy;
                    
                    // Legs are sorted by strike, so legs[0] should be the put (lower strike)
                    double putStrike = putOption.Strike;
                    double callStrike = callOption.Strike;
                    
                    string spreadType = $"STRANGLE {rootSymbols} {expirationDate} {putStrike}/{callStrike} PUT/CALL";
                    string spreadDescription = $"{(isLongStrangle ? "LONG" : "SHORT")} STRANGLE {rootSymbols} {expirationDate} {putStrike}/{callStrike} PUT/CALL";

                    details = new StrategyIdentification(
                        BaseType: "STRANGLE",
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