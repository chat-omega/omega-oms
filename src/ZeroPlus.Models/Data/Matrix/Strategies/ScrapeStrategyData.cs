using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public class ScrapeStrategyData : SmartStrategyData
{
    public static int ConfigId { get; } = 115;

    public ScrapeStrategyData()
    {
        Type = 28;
        InstrumentType = InstrumentType.EQUITYOPTION;
    }
}