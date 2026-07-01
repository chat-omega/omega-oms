using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings.Generic
{
    public class GenericSingleLegSpreadsGeneratorSettings : ISingleLegSpreadsGeneratorSettings
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
        public bool SpreadTheoAboveMidEnabled { get; set; }
        public double SpreadTheoAboveMid { get; set; }
        public bool SpreadTheoBelowMidEnabled { get; set; }
        public double SpreadTheoBelowMid { get; set; }
        public bool SpreadTheoAbsMidEnabled { get; set; }
        public double SpreadTheoAbsMid { get; set; }
        public double Leg1StrikeRangeCeil { get; set; }
        public bool Leg1StrikeRangeEnabled { get; set; }
        public double Leg1StrikeRangeFloor { get; set; }
        public bool ExcludedTradedSymbols { get; set; }
        public bool WidthSortingEnabled { get; set; }
        public bool SpreadVolaToHanweckDiffEnabled { get; set; }
        public double SpreadVolaToHanweckDiff { get; set; }
        public bool DataRequested()
        {
            return true;
        }
    }
}
