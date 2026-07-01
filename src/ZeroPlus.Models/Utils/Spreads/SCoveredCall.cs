using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public class SCoveredCall : IStrategy
{
    public BaseStrategy Strategy => BaseStrategy.COVERED_CALL;
    
    public bool TryIdentify(IList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        
        var stockSecurity = legs[0]?.Security;
        var optionSecurity = legs[1]?.Security;
        
        if (stockSecurity?.SecurityType == SecurityType.Stock && 
            optionSecurity is Option option && 
            option.PutCall == PutCall.Call)
        {
            // Verify stock symbol matches option underlying
            bool symbolsMatch = stockSecurity.Symbol == option.Underlying?.Symbol;
            
            // Verify opposite sides (typically buy stock, sell call)
            bool oppositeSides = legs[0].Side != legs[1].Side;
            
            // Verify quantity relationship: stock qty = option qty * multiplier
            bool correctQuantities = legs[0].Quantity == legs[1].Quantity * (option?.Multiplier ?? 100.0);
            
            if (symbolsMatch && oppositeSides && correctQuantities)
            {
                string expirationDate = option!.Expiration.ToString("MMM-dd-yy");
                string symbol = stockSecurity.Symbol ?? "";
                
                // Use existing logic to determine spread side
                Side? spreadSide = StrategyDispatcher.IdentifySpreadSide(Strategy, legs);
                bool isLongCovered = spreadSide == Side.Buy;
                
                string spreadType = $"COVERED {symbol} {expirationDate} {option.Strike} CALL/{symbol}";
                string spreadDescription = $"{(isLongCovered ? "LONG" : "SHORT")} COVERED {symbol} {expirationDate} {option.Strike} CALL/{symbol}";

                details = new StrategyIdentification(
                    BaseType: "COVERED CALL",
                    SpreadType: spreadType,
                    SpreadDescription: spreadDescription
                );

                return true;
            }
        }
        return false;
    }
}