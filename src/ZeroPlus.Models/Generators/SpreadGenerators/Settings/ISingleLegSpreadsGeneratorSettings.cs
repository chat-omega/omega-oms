using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings
{
    public interface ISingleLegSpreadsGeneratorSettings : ISpreadsGeneratorSettings
    {
        double Leg1DeltaRangeCeil { get; set; }
        bool Leg1DeltaRangeEnabled { get; set; }
        double Leg1DeltaRangeFloor { get; set; }
        double Leg1TheoRangeCeil { get; set; }
        TheoModel TheoModel { get; set; }
        bool Leg1TheoRangeEnabled { get; set; }
        double Leg1TheoRangeFloor { get; set; }
        double Leg1VegaRangeCeil { get; set; }
        bool Leg1VegaRangeEnabled { get; set; }
        double Leg1VegaRangeFloor { get; set; }
        double Leg1WeightedVegaRangeCeil { get; set; }
        bool Leg1WeightedVegaRangeEnabled { get; set; }
        double Leg1WeightedVegaRangeFloor { get; set; }
        double Leg1MarketRangeCeil { get; set; }
        bool Leg1MarketRangeEnabled { get; set; }
        double Leg1MarketRangeFloor { get; set; }
        double Leg1WidthRangeCeil { get; set; }
        bool Leg1WidthRangeEnabled { get; set; }
        double Leg1WidthRangeFloor { get; set; }
        bool SpreadTheoAboveMidEnabled { get; set; }
        double SpreadTheoAboveMid { get; set; }
        bool SpreadTheoBelowMidEnabled { get; set; }
        double SpreadTheoBelowMid { get; set; }
        bool SpreadTheoAbsMidEnabled { get; set; }
        double SpreadTheoAbsMid { get; set; }
        double Leg1StrikeRangeCeil { get; set; }
        bool Leg1StrikeRangeEnabled { get; set; }
        double Leg1StrikeRangeFloor { get; set; }
        bool ExcludedTradedSymbols { get; set; }
        bool SpreadVolaToHanweckDiffEnabled { get; set; }
        double SpreadVolaToHanweckDiff { get; set; }
    }
}