using ZeroPlus.Models.Data.Enums.Matrix;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public class SeekerSpreadStrategyData : SmartStrategyData
{
    public static int ConfigId { get; } = 117;

    public SeekerSpreadStrategyData()
    {
        Type = 37;
        InstrumentType = InstrumentType.SPREAD;
    }
}