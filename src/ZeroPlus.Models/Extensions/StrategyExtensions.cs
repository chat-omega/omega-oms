using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators;

namespace ZeroPlus.Models.Extensions
{
    public static class StrategyExtensions
    {
        public static bool IsRegularFly(this BaseStrategy baseStrategy)
        {
            return baseStrategy == BaseStrategy.CALL_BUTTERFLY ||
                   baseStrategy == BaseStrategy.PUT_BUTTERFLY;
        }

        public static bool IsSkewedFly(this BaseStrategy baseStrategy)
        {
            return baseStrategy == BaseStrategy.CALL_SKEWED_BUTTERFLY ||
                   baseStrategy == BaseStrategy.PUT_SKEWED_BUTTERFLY;
        }

        public static bool IsCalendar(this BaseStrategy baseStrategy)
        {
            return baseStrategy == BaseStrategy.CALL_CALENDAR ||
                   baseStrategy == BaseStrategy.PUT_CALENDAR;
        }

        public static bool IsAnyFly(this BaseStrategy baseStrategy)
        {
            return baseStrategy == BaseStrategy.CALL_BUTTERFLY ||
                   baseStrategy == BaseStrategy.PUT_BUTTERFLY ||
                   baseStrategy == BaseStrategy.CALL_SKEWED_BUTTERFLY ||
                   baseStrategy == BaseStrategy.PUT_SKEWED_BUTTERFLY;
        }

        public static Strategy ToStrategy(this BaseStrategy baseStrategy)
        {
            return baseStrategy switch
            {
                BaseStrategy.CALL or BaseStrategy.PUT => Strategy.SingleLeg,
                BaseStrategy.CALL_VERTICAL or BaseStrategy.PUT_VERTICAL => Strategy.Vertical,
                BaseStrategy.CALL_CALENDAR or BaseStrategy.PUT_CALENDAR => Strategy.Calendar,
                BaseStrategy.CALL_DIAGONAL or BaseStrategy.PUT_DIAGONAL => Strategy.Diagonal,
                BaseStrategy.CALL_BUTTERFLY or BaseStrategy.PUT_BUTTERFLY => Strategy.Butterfly,
                BaseStrategy.CALL_SKEWED_BUTTERFLY or BaseStrategy.PUT_SKEWED_BUTTERFLY => Strategy.SkewedButterfly,
                BaseStrategy.CALL_TREE or BaseStrategy.PUT_TREE=> Strategy.Tree,
                BaseStrategy.CALL_CALENDAR_FLY or BaseStrategy.PUT_CALENDAR_FLY => Strategy.CalendarButterfly,
                BaseStrategy.CALL_1X2 or BaseStrategy.PUT_1X2 => Strategy.Ratio1X2,
                BaseStrategy.CALL_1X3 or BaseStrategy.PUT_1X3 => Strategy.Ratio1X3,
                BaseStrategy.CALL_2X3 or BaseStrategy.PUT_2X3 => Strategy.RatioCustom,
                BaseStrategy.CALL_CONDOR or BaseStrategy.PUT_CONDOR => Strategy.Condor,
                BaseStrategy.CALL_1X3X3X1 or BaseStrategy.PUT_1X3X3X1 => Strategy.OneThreeThreeOne,
                BaseStrategy.CALL_1x3x2 or BaseStrategy.PUT_1x3x2 => Strategy.OneThreeTwo,
                BaseStrategy.CALL_2x3x1 or BaseStrategy.PUT_2x3x1 => Strategy.TwoThreeOne,
                BaseStrategy.IRON_CONDOR => Strategy.IronCondor,
                BaseStrategy.IRON_BUTTERFLY => Strategy.IronButterfly,
                _ => Strategy.Custom,
            };
        }

    }
}
