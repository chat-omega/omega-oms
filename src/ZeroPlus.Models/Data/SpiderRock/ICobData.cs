using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock;

public interface ICobData
{
    internal CobDataType DataType { get; }

    public bool FromCache { get; set; }
    public string? Underlying { get; set; }
    public string? Symbol { get; set; }
    public BaseStrategy BaseStrategy { get; set; }
    public string? SpreadId { get; set; }
    public string? SpreadDescription { get; set; }
}