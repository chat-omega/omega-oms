using System;

namespace ZeroPlus.Oms.Ui.ModuleConfigs;

public class UnderlyingLookupKey
{
    public string Symbol { get; set; }
    public double Increment { get; set; }

    public UnderlyingLookupKey()
    {
    }

    public UnderlyingLookupKey(string symbol, double increment)
    {
        Symbol = symbol;
        Increment = increment;
    }

    public override bool Equals(object obj)
    {
        if (obj is UnderlyingLookupKey other)
        {
            return Symbol == other.Symbol && Math.Abs(Increment - other.Increment) < .01;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Tuple.Create(Symbol, Increment).GetHashCode();
    }
}