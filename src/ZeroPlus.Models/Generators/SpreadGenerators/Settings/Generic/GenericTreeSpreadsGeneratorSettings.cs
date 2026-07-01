using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings.Generic;

public class GenericTreeSpreadsGeneratorSettings : ITreeSpreadsGeneratorSettings
{
    public double Leg1DeltaRangeCeil { get; set; }
    public bool Leg1DeltaRangeEnabled { get; set; }
    public double Leg1DeltaRangeFloor { get; set; }
    public double Leg1TheoRangeCeil { get; set; }
    public TheoModel TheoModel { get; set; }
    public bool Leg1TheoRangeEnabled { get; set; }
    public double Leg1TheoRangeFloor { get; set; }
    public double Leg1VegaRangeCeil { get; set; }
    public bool Leg1VegaRangeEnabled { get; set; }
    public double Leg1VegaRangeFloor { get; set; }
    public double Leg1WeightedVegaRangeCeil { get; set; }
    public bool Leg1WeightedVegaRangeEnabled { get; set; }
    public double Leg1WeightedVegaRangeFloor { get; set; }
    public double Leg1MarketRangeCeil { get; set; }
    public bool Leg1MarketRangeEnabled { get; set; }
    public double Leg1MarketRangeFloor { get; set; }
    public double Leg1WidthRangeCeil { get; set; }
    public bool Leg1WidthRangeEnabled { get; set; }
    public double Leg1WidthRangeFloor { get; set; }
    public double Leg2DeltaRangeCeil { get; set; }
    public bool Leg2DeltaRangeEnabled { get; set; }
    public double Leg2DeltaRangeFloor { get; set; }
    public double Leg2TheoRangeCeil { get; set; }
    public bool Leg2TheoRangeEnabled { get; set; }
    public double Leg2TheoRangeFloor { get; set; }
    public double Leg2VegaRangeCeil { get; set; }
    public bool Leg2VegaRangeEnabled { get; set; }
    public double Leg2VegaRangeFloor { get; set; }
    public double Leg2WeightedVegaRangeCeil { get; set; }
    public bool Leg2WeightedVegaRangeEnabled { get; set; }
    public double Leg2WeightedVegaRangeFloor { get; set; }
    public double Leg2MarketRangeCeil { get; set; }
    public bool Leg2MarketRangeEnabled { get; set; }
    public double Leg2MarketRangeFloor { get; set; }
    public double Leg2WidthRangeCeil { get; set; }
    public bool Leg2WidthRangeEnabled { get; set; }
    public double Leg2WidthRangeFloor { get; set; }
    public double Leg3DeltaRangeCeil { get; set; }
    public bool Leg3DeltaRangeEnabled { get; set; }
    public double Leg3DeltaRangeFloor { get; set; }
    public double Leg3TheoRangeCeil { get; set; }
    public bool Leg3TheoRangeEnabled { get; set; }
    public double Leg3TheoRangeFloor { get; set; }
    public double Leg3VegaRangeCeil { get; set; }
    public bool Leg3VegaRangeEnabled { get; set; }
    public double Leg3VegaRangeFloor { get; set; }
    public double Leg3WeightedVegaRangeCeil { get; set; }
    public bool Leg3WeightedVegaRangeEnabled { get; set; }
    public double Leg3WeightedVegaRangeFloor { get; set; }
    public double Leg3MarketRangeCeil { get; set; }
    public bool Leg3MarketRangeEnabled { get; set; }
    public double Leg3MarketRangeFloor { get; set; }
    public double Leg3WidthRangeCeil { get; set; }
    public bool Leg3WidthRangeEnabled { get; set; }
    public double Leg3WidthRangeFloor { get; set; }
    public double SpreadDeltaRangeCeil { get; set; }
    public bool SpreadDeltaRangeEnabled { get; set; }
    public double SpreadDeltaRangeFloor { get; set; }
    public double SpreadTheoRangeCeil { get; set; }
    public bool SpreadTheoRangeEnabled { get; set; }
    public double SpreadTheoRangeFloor { get; set; }
    public double SpreadVegaRangeCeil { get; set; }
    public bool SpreadVegaRangeEnabled { get; set; }
    public double SpreadVegaRangeFloor { get; set; }
    public double WeightedVegaRangeCeil { get; set; }
    public bool WeightedVegaRangeEnabled { get; set; }
    public double WeightedVegaRangeFloor { get; set; }
    public double SpreadMarketRangeCeil { get; set; }
    public bool SpreadMarketRangeEnabled { get; set; }
    public double SpreadMarketRangeFloor { get; set; }
    public string? SpreadSpacing1List { get; set; }
    public bool SpreadSpacing1ListEnabled { get; set; }
    public double SpreadSpacing1RangeCeil { get; set; }
    public bool SpreadSpacing1RangeEnabled { get; set; }
    public double SpreadSpacing1RangeFloor { get; set; }
    public string? SpreadSpacing2List { get; set; }
    public bool SpreadSpacing2ListEnabled { get; set; }
    public double SpreadSpacing2RangeCeil { get; set; }
    public bool SpreadSpacing2RangeEnabled { get; set; }
    public double SpreadSpacing2RangeFloor { get; set; }
    public double SpreadWidthRangeCeil { get; set; }
    public bool SpreadWidthRangeEnabled { get; set; }
    public double SpreadWidthRangeFloor { get; set; }
    public bool SpreadTheoAboveMidEnabled { get; set; }
    public double SpreadTheoAboveMid { get; set; }
    public bool SpreadTheoBelowMidEnabled { get; set; }
    public double SpreadTheoBelowMid { get; set; }
    public bool SpreadTheoAbsMidEnabled { get; set; }
    public double SpreadTheoAbsMid { get; set; }
    public double Leg1StrikeRangeCeil { get; set; }
    public bool Leg1StrikeRangeEnabled { get; set; }
    public double Leg1StrikeRangeFloor { get; set; }
    public double Leg2StrikeRangeCeil { get; set; }
    public bool Leg2StrikeRangeEnabled { get; set; }
    public double Leg2StrikeRangeFloor { get; set; }
    public double Leg3StrikeRangeCeil { get; set; }
    public bool Leg3StrikeRangeEnabled { get; set; }
    public double Leg3StrikeRangeFloor { get; set; }
    public bool SpreadEmaToMidRangeEnabled { get; set; }
    public double SpreadEmaToMidRangeFloor { get; set; }
    public double SpreadEmaToMidRangeCeil { get; set; }
    public bool SpreadTheoToMidRangeEnabled { get; set; }
    public double SpreadTheoToMidRangeFloor { get; set; }
    public double SpreadTheoToMidRangeCeil { get; set; }
    public bool WidthSortingEnabled { get; set; }
    public bool SpreadVolaToHanweckDiffEnabled { get; set; }
    public double SpreadVolaToHanweckDiff { get; set; }
    public bool DataRequested()
    {
        return false;
    }
}