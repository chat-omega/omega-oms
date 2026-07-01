using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.SpiderRock;

public class SrSpreadLeg
{
    public string? LegSecurity { get; set; }
    public Side? LegSide { get; set; }
    public uint LegRatio { get; set; }
    public PositionEffect LegPositionType { get; set; }
}