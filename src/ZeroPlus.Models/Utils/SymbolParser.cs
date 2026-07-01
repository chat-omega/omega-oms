using SymbolLib;
using System;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Utils
{
    public class SymbolParser
    {
        private const int CO = 99983932;

        public static Security? GetSecurityFromSymbol(string symbol, out string? underlying)
        {
            underlying = default;
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return default;
            }

            if (symbol.Contains('\\'))
            {
                symbol = symbol.Split('\\')[symbol.StartsWith('\\') ? 1 : 0];
            }

            SymbolCodec codec = new SymbolCodec(symbol);
            if (codec.LegCount == 0)
            {
                return default;
            }

            Instrument instrument = codec.GetLeg(0);

            if (instrument.instrumentType == InstrumentType.Option)
            {
                underlying = instrument.underlyingSymbol;
                Option option = new Option()
                {
                    ID = CO + Math.Abs(symbol.ToUpper().GetHashCode()),
                    Symbol = instrument.symbol,
                    RootSymbol = instrument.rootSymbol,
                    Expiration = instrument.expiration,
                    PutCall = instrument.callPut ? Data.Enums.PutCall.Put : Data.Enums.PutCall.Call,
                    Strike = instrument.strike,
                };
                return option;
            }

            underlying = instrument.symbol;
            Security security = new Security()
            {
                ID = CO + Math.Abs(symbol.ToUpper().GetHashCode()),
                Multiplier = 1,
                Symbol = instrument.symbol,
                SecurityType = (Data.Enums.SecurityType)instrument.instrumentType,
            };

            return security;
        }

        public static string GetUnderlyingSymbolFromSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return symbol;
            }

            if (symbol.Contains('\\'))
            {
                symbol = symbol.Split('\\')[symbol.StartsWith('\\') ? 1 : 0];
            }

            SymbolCodec codec = new SymbolCodec(symbol);
            if (codec.LegCount == 0)
            {
                return symbol;
            }

            Instrument instrument = codec.GetLeg(0);

            if (instrument.instrumentType == InstrumentType.Option)
            {
                return instrument.underlyingSymbol;
            }
            else
            {
                return instrument.symbol;
            }
        }
    }
}
