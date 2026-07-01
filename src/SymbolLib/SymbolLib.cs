namespace SymbolLib;

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
    public string underlyingSymbol = "";
    public string rootSymbol = "";
    public DateTime expiration;
    public bool callPut;  // true = Put, false = Call
    public double strike;
    public InstrumentType instrumentType = InstrumentType.Unknown;
    public bool valid = true;

    public Instrument(string symbol)
    {
        this.symbol = symbol;
        ParseSymbol(symbol);
    }

    private void ParseSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) { valid = false; return; }

        // Try to identify options vs stock
        // Common formats: SPY100320C00500000, SPY  100320C00500000
        var clean = symbol.Trim();
        if (clean.Length < 5) { instrumentType = InstrumentType.Stock; underlyingSymbol = clean; rootSymbol = clean; return; }

        // Find where the date digits start (after the ticker)
        int digitStart = -1;
        for (int i = 1; i < clean.Length && i < 10; i++)
        {
            if (char.IsDigit(clean[i]) && !char.IsDigit(clean[i - 1]))
            {
                digitStart = i;
                break;
            }
        }

        if (digitStart > 0 && clean.Length - digitStart >= 8)
        {
            instrumentType = InstrumentType.Option;
            underlyingSymbol = clean[..digitStart].Trim();
            var rest = clean[digitStart..];
            var putCallIdx = rest.IndexOfAny(new[] { 'C', 'P' });
            if (putCallIdx >= 0)
            {
                callPut = rest[putCallIdx] == 'P';
                var dateStr = rest[..putCallIdx];
                var strikeStr = rest[(putCallIdx + 1)..];
                if (dateStr.Length >= 6)
                {
                    if (DateTime.TryParseExact(dateStr, ["MMddyy", "yyyyMMdd", "yyMMdd"],
                        null, System.Globalization.DateTimeStyles.None, out var dt))
                        expiration = dt;
                }
                double.TryParse(strikeStr, out strike);
                strike /= 1000;
            }
        }
        else
        {
            instrumentType = InstrumentType.Stock;
            underlyingSymbol = clean;
        }
        rootSymbol = underlyingSymbol;
    }

    public DateTime Date => expiration;
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
        var parts = symbol.Split('+');
        foreach (var p in parts)
        {
            _legs.Add(new Instrument(p.Trim()));
        }
        if (_legs.Count == 0) _legs.Add(new Instrument(symbol));
    }

    public Instrument GetLeg(int index) =>
        index >= 0 && index < _legs.Count ? _legs[index] : new Instrument(_symbol);
}