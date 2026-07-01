using System;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Oms.Helper;

public static class ComplexOrderSlimExtensions
{
    public static void AddLegs(this IComplexOrderSlim orderSlim, ISecurityBook book, out string underlyingSymbol)
    {
        SymbolLib.SymbolCodec codec = new(orderSlim.Symbol);

        ComplexOrderLeg[] legs = new ComplexOrderLeg[codec.LegCount];
        for (int index = 0; index < legs.Length; index++)
        {
            var instrument = codec.GetLeg(index);
            legs[index] = new ComplexOrderLeg(book)
            {
                LegID = $"{(instrument.symbol.Length >= 6 ? instrument.symbol[..6] : instrument.symbol)}-leg{index}",
                Symbol = instrument.symbol,
                Quantity = Math.Abs(instrument.ratio),
                Ratio = instrument.ratio,
                Side = instrument.buySell ? Models.Data.Enums.Side.Buy : Models.Data.Enums.Side.Sell,
            };
        }

        orderSlim.Legs = [.. legs];
        underlyingSymbol = codec.UnderlyingSymbol();
    }
}