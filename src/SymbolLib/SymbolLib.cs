namespace SymbolLib;

/// <summary>Option or stock type</summary>
public enum InstrumentType
{
    Option,
    Stock,
    Future,
    Index,
    Unknown
}

/// <summary>Parsed instrument from a symbol string</summary>
public class Instrument
{
    public string symbol;
    public string underlyingSymbol;
    public string rootSymbol;
    public string expiration;
    public string callPut;
    public double strike;
    public InstrumentType instrumentType;

    public Instrument(string symbol)
    {
        this.symbol = symbol;
        // Basic stub parsing for common patterns
        if (symbol.Length > 4)
        {
            // Try to identify options vs stock
            // Option format: SPY 100320C00500000 or similar
            underlyingSymbol = symbol[..symbol.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }, 2)];
            if (underlyingSymbol.Length < symbol.Length - 6)
            {
                instrumentType = InstrumentType.Option;
                var rest = symbol[underlyingSymbol.Length..];
                if (rest.Length >= 6)
                {
                    callPut = rest.Contains('C') ? "C" : "P";
                    var parts = callPut == "C" ? rest.Split('C') : rest.Split('P');
                    if (parts.Length > 1) {
                        expiration = parts[0];
                        double.TryParse(parts[1], out strike);
                        strike /= 1000;
                    }
                }
            }
        }
        if (instrumentType == InstrumentType.Unknown)
        {
            instrumentType = InstrumentType.Stock;
            underlyingSymbol = symbol;
            rootSymbol = symbol;
        }
        rootSymbol ??= underlyingSymbol;
    }
}

/// <summary>Decodes a symbol string into its component legs</summary>
public class SymbolCodec
{
    private readonly string _symbol;
    private readonly List<Instrument> _legs = new();

    public int LegCount => _legs.Count;

    public SymbolCodec(string symbol)
    {
        _symbol = symbol;
        // Parse multi-leg strategies (e.g. "SPY 100320C00500000+SPY 100320P00500000")
        var parts = symbol.Split('+');
        foreach (var p in parts)
        {
            _legs.Add(new Instrument(p.Trim()));
        }
    }

    public Instrument GetLeg(int index)
    {
        return index < _legs.Count ? _legs[index] : new Instrument(_symbol);
    }
}