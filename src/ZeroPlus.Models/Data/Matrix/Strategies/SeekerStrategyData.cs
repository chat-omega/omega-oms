using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public class SeekerStrategyData : SmartStrategyData
{
    public static int ConfigId { get; } = 116;

    public SeekerStrategyData()
    {
        Type = 36;
        InstrumentType = InstrumentType.EQUITYOPTION;
    }
}