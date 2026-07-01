using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SkewedButterflySpreadsGeneratorSettingsModel : BindableBase, ISkewedButterflySpreadsGeneratorSettings
    {


        [JsonProperty]
        [Bindable]
        public partial bool Leg1DeltaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1DeltaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1DeltaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2DeltaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2DeltaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2DeltaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3DeltaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3DeltaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3DeltaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadDeltaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadDeltaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadDeltaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial TheoModel TheoModel { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1TheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1TheoRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1TheoRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2TheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2TheoRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2TheoRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3TheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3TheoRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3TheoRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1VegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2VegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2VegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2VegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3VegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3VegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3VegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1WeightedVegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1WeightedVegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1WeightedVegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2WeightedVegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2WeightedVegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2WeightedVegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3WeightedVegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3WeightedVegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3WeightedVegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadVegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadVegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadVegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool WeightedVegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double WeightedVegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double WeightedVegaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1MarketRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2MarketRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2MarketRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2MarketRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3MarketRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3MarketRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3MarketRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadMarketRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadMarketRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadMarketRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1WidthRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1WidthRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1WidthRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2WidthRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2WidthRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2WidthRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3WidthRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3WidthRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3WidthRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadWidthRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadWidthRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadWidthRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacing1RangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadSpacing1RangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadSpacing1RangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacing2RangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadSpacing2RangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadSpacing2RangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacing1ListEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string SpreadSpacing1List { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacing2ListEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string SpreadSpacing2List { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoAboveMidEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoAboveMid { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoBelowMidEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoBelowMid { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoAbsMidEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoAbsMid { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg1StrikeRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1StrikeRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1StrikeRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg2StrikeRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2StrikeRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg2StrikeRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Leg3StrikeRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3StrikeRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg3StrikeRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadEmaToMidRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadEmaToMidRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadEmaToMidRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoToMidRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoToMidRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoToMidRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool WidthSortingEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool SpreadVolaToHanweckDiffEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double SpreadVolaToHanweckDiff { get; set; }

        public bool DataRequested()
        {
            return Leg1DeltaRangeEnabled ||
                   Leg2DeltaRangeEnabled ||
                   Leg3DeltaRangeEnabled ||
                   SpreadDeltaRangeEnabled ||
                   Leg1TheoRangeEnabled ||
                   Leg2TheoRangeEnabled ||
                   Leg3TheoRangeEnabled ||
                   SpreadTheoRangeEnabled ||
                   Leg1VegaRangeEnabled ||
                   Leg1WeightedVegaRangeEnabled ||
                   Leg2VegaRangeEnabled ||
                   Leg2WeightedVegaRangeEnabled ||
                   Leg3VegaRangeEnabled ||
                   Leg3WeightedVegaRangeEnabled ||
                   SpreadVegaRangeEnabled ||
                   WeightedVegaRangeEnabled ||
                   Leg1MarketRangeEnabled ||
                   Leg1WidthRangeEnabled ||
                   Leg2MarketRangeEnabled ||
                   Leg2WidthRangeEnabled ||
                   Leg3MarketRangeEnabled ||
                   Leg3WidthRangeEnabled ||
                   SpreadMarketRangeEnabled ||
                   SpreadWidthRangeEnabled ||
                   SpreadTheoAboveMidEnabled ||
                   SpreadTheoBelowMidEnabled ||
                   SpreadVolaToHanweckDiffEnabled ||
                   SpreadTheoAbsMidEnabled;
        }

        public SkewedButterflySpreadsGeneratorSettingsModel Clone()
        {
            return new SkewedButterflySpreadsGeneratorSettingsModel()
            {
                Leg1DeltaRangeEnabled = Leg1DeltaRangeEnabled,
                Leg1DeltaRangeFloor = Leg1DeltaRangeFloor,
                Leg1DeltaRangeCeil = Leg1DeltaRangeCeil,
                Leg2DeltaRangeEnabled = Leg2DeltaRangeEnabled,
                Leg2DeltaRangeFloor = Leg2DeltaRangeFloor,
                Leg2DeltaRangeCeil = Leg2DeltaRangeCeil,
                Leg3DeltaRangeEnabled = Leg3DeltaRangeEnabled,
                Leg3DeltaRangeFloor = Leg3DeltaRangeFloor,
                Leg3DeltaRangeCeil = Leg3DeltaRangeCeil,
                SpreadDeltaRangeEnabled = SpreadDeltaRangeEnabled,
                SpreadDeltaRangeFloor = SpreadDeltaRangeFloor,
                SpreadDeltaRangeCeil = SpreadDeltaRangeCeil,
                Leg1TheoRangeEnabled = Leg1TheoRangeEnabled,
                TheoModel = TheoModel,
                Leg1TheoRangeFloor = Leg1TheoRangeFloor,
                Leg1TheoRangeCeil = Leg1TheoRangeCeil,
                Leg2TheoRangeEnabled = Leg2TheoRangeEnabled,
                Leg2TheoRangeFloor = Leg2TheoRangeFloor,
                Leg2TheoRangeCeil = Leg2TheoRangeCeil,
                Leg3TheoRangeEnabled = Leg3TheoRangeEnabled,
                Leg3TheoRangeFloor = Leg3TheoRangeFloor,
                Leg3TheoRangeCeil = Leg3TheoRangeCeil,
                SpreadTheoRangeEnabled = SpreadTheoRangeEnabled,
                SpreadTheoRangeFloor = SpreadTheoRangeFloor,
                SpreadTheoRangeCeil = SpreadTheoRangeCeil,
                Leg1VegaRangeEnabled = Leg1VegaRangeEnabled,
                Leg1VegaRangeFloor = Leg1VegaRangeFloor,
                Leg1VegaRangeCeil = Leg1VegaRangeCeil,
                Leg2VegaRangeEnabled = Leg2VegaRangeEnabled,
                Leg2VegaRangeFloor = Leg2VegaRangeFloor,
                Leg2VegaRangeCeil = Leg2VegaRangeCeil,
                Leg3VegaRangeEnabled = Leg3VegaRangeEnabled,
                Leg3VegaRangeFloor = Leg3VegaRangeFloor,
                Leg3VegaRangeCeil = Leg3VegaRangeCeil,
                Leg1WeightedVegaRangeEnabled = Leg1WeightedVegaRangeEnabled,
                Leg1WeightedVegaRangeFloor = Leg1WeightedVegaRangeFloor,
                Leg1WeightedVegaRangeCeil = Leg1WeightedVegaRangeCeil,
                Leg2WeightedVegaRangeEnabled = Leg2WeightedVegaRangeEnabled,
                Leg2WeightedVegaRangeFloor = Leg2WeightedVegaRangeFloor,
                Leg2WeightedVegaRangeCeil = Leg2WeightedVegaRangeCeil,
                Leg3WeightedVegaRangeEnabled = Leg3WeightedVegaRangeEnabled,
                Leg3WeightedVegaRangeFloor = Leg3WeightedVegaRangeFloor,
                Leg3WeightedVegaRangeCeil = Leg3WeightedVegaRangeCeil,
                SpreadVegaRangeEnabled = SpreadVegaRangeEnabled,
                SpreadVegaRangeFloor = SpreadVegaRangeFloor,
                SpreadVegaRangeCeil = SpreadVegaRangeCeil,
                WeightedVegaRangeEnabled = WeightedVegaRangeEnabled,
                WeightedVegaRangeFloor = WeightedVegaRangeFloor,
                WeightedVegaRangeCeil = WeightedVegaRangeCeil,
                Leg1MarketRangeEnabled = Leg1MarketRangeEnabled,
                Leg1MarketRangeFloor = Leg1MarketRangeFloor,
                Leg1MarketRangeCeil = Leg1MarketRangeCeil,
                Leg2MarketRangeEnabled = Leg2MarketRangeEnabled,
                Leg2MarketRangeFloor = Leg2MarketRangeFloor,
                Leg2MarketRangeCeil = Leg2MarketRangeCeil,
                Leg3MarketRangeEnabled = Leg3MarketRangeEnabled,
                Leg3MarketRangeFloor = Leg3MarketRangeFloor,
                Leg3MarketRangeCeil = Leg3MarketRangeCeil,
                SpreadMarketRangeEnabled = SpreadMarketRangeEnabled,
                SpreadMarketRangeFloor = SpreadMarketRangeFloor,
                SpreadMarketRangeCeil = SpreadMarketRangeCeil,
                Leg1WidthRangeEnabled = Leg1WidthRangeEnabled,
                Leg1WidthRangeFloor = Leg1WidthRangeFloor,
                Leg1WidthRangeCeil = Leg1WidthRangeCeil,
                Leg2WidthRangeEnabled = Leg2WidthRangeEnabled,
                Leg2WidthRangeFloor = Leg2WidthRangeFloor,
                Leg2WidthRangeCeil = Leg2WidthRangeCeil,
                Leg3WidthRangeEnabled = Leg3WidthRangeEnabled,
                Leg3WidthRangeFloor = Leg3WidthRangeFloor,
                Leg3WidthRangeCeil = Leg3WidthRangeCeil,
                SpreadWidthRangeEnabled = SpreadWidthRangeEnabled,
                SpreadWidthRangeFloor = SpreadWidthRangeFloor,
                SpreadWidthRangeCeil = SpreadWidthRangeCeil,
                SpreadSpacing1RangeEnabled = SpreadSpacing1RangeEnabled,
                SpreadSpacing1RangeFloor = SpreadSpacing1RangeFloor,
                SpreadSpacing1RangeCeil = SpreadSpacing1RangeCeil,
                SpreadSpacing2RangeEnabled = SpreadSpacing2RangeEnabled,
                SpreadSpacing2RangeFloor = SpreadSpacing2RangeFloor,
                SpreadSpacing2RangeCeil = SpreadSpacing2RangeCeil,
                SpreadSpacing1ListEnabled = SpreadSpacing1ListEnabled,
                SpreadSpacing1List = SpreadSpacing1List,
                SpreadSpacing2ListEnabled = SpreadSpacing2ListEnabled,
                SpreadSpacing2List = SpreadSpacing2List,
                SpreadTheoAboveMidEnabled = SpreadTheoAboveMidEnabled,
                SpreadTheoAboveMid = SpreadTheoAboveMid,
                SpreadTheoBelowMidEnabled = SpreadTheoBelowMidEnabled,
                SpreadTheoBelowMid = SpreadTheoBelowMid,
                SpreadTheoAbsMidEnabled = SpreadTheoAbsMidEnabled,
                SpreadTheoAbsMid = SpreadTheoAbsMid,
                Leg1StrikeRangeEnabled = Leg1StrikeRangeEnabled,
                Leg1StrikeRangeFloor = Leg1StrikeRangeFloor,
                Leg1StrikeRangeCeil = Leg1StrikeRangeCeil,
                Leg2StrikeRangeEnabled = Leg2StrikeRangeEnabled,
                Leg2StrikeRangeFloor = Leg2StrikeRangeFloor,
                Leg2StrikeRangeCeil = Leg2StrikeRangeCeil,
                Leg3StrikeRangeEnabled = Leg3StrikeRangeEnabled,
                Leg3StrikeRangeFloor = Leg3StrikeRangeFloor,
                Leg3StrikeRangeCeil = Leg3StrikeRangeCeil,
                SpreadVolaToHanweckDiffEnabled = SpreadVolaToHanweckDiffEnabled,
                SpreadVolaToHanweckDiff = SpreadVolaToHanweckDiff,
            };
        }
    }
}
