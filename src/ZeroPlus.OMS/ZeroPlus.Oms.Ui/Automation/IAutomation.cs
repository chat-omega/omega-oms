using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation;

public interface IAutomation
{
    double Increment { get; }
    double Interval { get; }
    double SecondaryIncrement { get; }

    void Stop();
    Task ContinueAsync(Walker walker);
    void ShowStatus(OrderUpdateModel execReport, OrderStatus result);
    
    public static string GetTosSymbol(List<TicketLegModel> legs, bool invert = false)
    {
        string tosSymbol = "";
        for (int i = 0; i < legs.Count; i++)
        {
            TicketLegModel leg = legs[i];
            if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                tosSymbol += "-";
            }
            else
            {
                if (i != 0)
                {
                    tosSymbol += "+";
                }
            }

            if (leg.Quantity > 1)
            {
                tosSymbol += leg.Quantity + "*";
            }
            tosSymbol += leg.Symbol;
        }

        SymbolLib.SymbolCodec symbolCodec = new(tosSymbol);

        if (invert)
        {
            symbolCodec.Invert();
        }

        symbolCodec.Normalize();

        return symbolCodec.ToTOS();
    }
}