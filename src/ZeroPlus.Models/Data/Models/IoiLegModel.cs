using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models;

/// <summary>
/// UI model for one IOI leg (enriched from IoiLegRepresentation with computed Symbol).
/// </summary>
public class IoiLegModel
{
    public int LegId { get; set; }
    public string Symbol => $".{UnderlyingSymbol}{Expiration}{Type}{Strike}";
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public IoiSecurityType SecurityType { get; set; }
    public Side Side { get; set; }
    public char Type { get; set; }
    public ushort Ratio { get; set; }
    public uint Expiration { get; set; }
    public double Strike { get; set; }

    public override string ToString()
    {
        return
            $"LegId: {LegId}, " +
            $"Symbol: {Symbol}, " +
            $"Side: {Side}, " +
            $"SecurityType: {SecurityType}, " +
            $"Ratio: {Ratio}";
    }
}
