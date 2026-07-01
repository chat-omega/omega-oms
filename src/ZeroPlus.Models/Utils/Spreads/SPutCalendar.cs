using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SPutCalendar : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.PUT_CALENDAR;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        if (legs[0]!.Security is Option option1 && legs[1]!.Security is Option option2)
        {
            if (option1.PutCall == PutCall.Put && option2.PutCall == PutCall.Put)
            {
                bool differentExpirations = option1.Expiration != option2.Expiration;
                bool sameStrike = option1.Strike == option2.Strike;
                bool sameQuantity = legs[0].Quantity == legs[1].Quantity;
                bool oppositeSides = legs[0].Side != legs[1].Side;
                
                if (differentExpirations && sameStrike && sameQuantity && oppositeSides)
                {
                    string rootSymbols = StrategyDispatcher.GetRootSymbols(legs);
                    
                    // Determine which option expires first
                    var nearOption = option1.Expiration < option2.Expiration ? option1 : option2;
                    var farOption = option1.Expiration > option2.Expiration ? option1 : option2;
                    
                    // Use existing logic to determine spread side
                    Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
                    bool isLongCalendar = spreadSide == Side.Buy;
                    
                    string nearExpiration = nearOption.Expiration.ToString("MMM-dd-yy");
                    string farExpiration = farOption.Expiration.ToString("MMM-dd-yy");
                    
                    // Since strike is same for both legs, just use one
                    double strike = option1.Strike;
                    
                    string spreadType = $"PUT CALENDAR {rootSymbols} {nearExpiration}/{farExpiration} {strike}";
                    string spreadDescription = $"{(isLongCalendar ? "LONG" : "SHORT")} {spreadType}";

                    details = new StrategyIdentification(
                        BaseType: "PUT CALENDAR",
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