using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCallDiagonal : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.CALL_DIAGONAL;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        if (legs[0]!.Security is Option option1 && legs[1]!.Security is Option option2)
        {
            if (option1.PutCall == PutCall.Call && option2.PutCall == PutCall.Call)
            {
                bool differentExpirations = option1.Expiration != option2.Expiration;
                bool differentStrikes = option1.Strike != option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool oppositeSides = legs[0].Side != legs[1].Side;
                
                if (differentExpirations && differentStrikes && sameQuantity && oppositeSides)
                {
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
                    
                    // Determine which option expires first
                    var nearOption = option1.Expiration < option2.Expiration ? option1 : option2;
                    var farOption = option1.Expiration > option2.Expiration ? option1 : option2;
                    
                    // Calculate strike spacing
                    double spacing = Math.Round(Math.Abs(nearOption.Strike - farOption.Strike), 2);
                    
                    // Use existing logic to determine spread side
                    Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
                    bool isLongDiagonal = spreadSide == Side.Buy;
                    
                    string nearExpiration = nearOption.Expiration.ToString("MMM-dd-yy");
                    string farExpiration = farOption.Expiration.ToString("MMM-dd-yy");
                    
                    string spreadType = $"CALL DIAGONAL {rootSymbols} {nearExpiration}/{farExpiration} {nearOption.Strike}/{farOption.Strike} [{spacing}]";
                    string spreadDescription = $"{(isLongDiagonal ? "LONG" : "SHORT")} {spreadType}";

                    details = new StrategyIdentification(
                        BaseType: "CALL DIAGONAL",
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