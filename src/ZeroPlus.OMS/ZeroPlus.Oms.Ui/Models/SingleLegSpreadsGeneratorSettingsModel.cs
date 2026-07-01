using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SingleLegSpreadsGeneratorSettingsModel : BindableBase, ISingleLegSpreadsGeneratorSettings
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
        public partial bool Leg1VegaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeCeil { get; set; }

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
        public partial bool Leg1MarketRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeFloor { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeCeil { get; set; }

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
        public partial bool ExcludedTradedSymbols { get; set; }

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
                   Leg1TheoRangeEnabled ||
                   Leg1VegaRangeEnabled ||
                   Leg1WeightedVegaRangeEnabled ||
                   Leg1MarketRangeEnabled ||
                   Leg1WidthRangeEnabled ||
                   SpreadTheoAboveMidEnabled ||
                   SpreadTheoBelowMidEnabled ||
                   SpreadVolaToHanweckDiffEnabled ||
                   SpreadTheoAbsMidEnabled;
        }

        public SingleLegSpreadsGeneratorSettingsModel Clone()
        {
            return new SingleLegSpreadsGeneratorSettingsModel()
            {
                Leg1DeltaRangeEnabled = Leg1DeltaRangeEnabled,
                Leg1DeltaRangeFloor = Leg1DeltaRangeFloor,
                Leg1DeltaRangeCeil = Leg1DeltaRangeCeil,
                Leg1TheoRangeEnabled = Leg1TheoRangeEnabled,
                TheoModel = TheoModel,
                Leg1TheoRangeFloor = Leg1TheoRangeFloor,
                Leg1TheoRangeCeil = Leg1TheoRangeCeil,
                Leg1VegaRangeEnabled = Leg1VegaRangeEnabled,
                Leg1VegaRangeFloor = Leg1VegaRangeFloor,
                Leg1VegaRangeCeil = Leg1VegaRangeCeil,
                Leg1WeightedVegaRangeEnabled = Leg1WeightedVegaRangeEnabled,
                Leg1WeightedVegaRangeFloor = Leg1WeightedVegaRangeFloor,
                Leg1WeightedVegaRangeCeil = Leg1WeightedVegaRangeCeil,
                Leg1MarketRangeEnabled = Leg1MarketRangeEnabled,
                Leg1MarketRangeFloor = Leg1MarketRangeFloor,
                Leg1MarketRangeCeil = Leg1MarketRangeCeil,
                Leg1WidthRangeEnabled = Leg1WidthRangeEnabled,
                Leg1WidthRangeFloor = Leg1WidthRangeFloor,
                Leg1WidthRangeCeil = Leg1WidthRangeCeil,
                SpreadTheoAboveMidEnabled = SpreadTheoAboveMidEnabled,
                SpreadTheoAboveMid = SpreadTheoAboveMid,
                SpreadTheoBelowMidEnabled = SpreadTheoBelowMidEnabled,
                SpreadTheoBelowMid = SpreadTheoBelowMid,
                SpreadTheoAbsMidEnabled = SpreadTheoAbsMidEnabled,
                SpreadTheoAbsMid = SpreadTheoAbsMid,
                Leg1StrikeRangeEnabled = Leg1StrikeRangeEnabled,
                Leg1StrikeRangeFloor = Leg1StrikeRangeFloor,
                Leg1StrikeRangeCeil = Leg1StrikeRangeCeil,
                SpreadVolaToHanweckDiffEnabled = SpreadVolaToHanweckDiffEnabled,
                SpreadVolaToHanweckDiff = SpreadVolaToHanweckDiff,
            };
        }
    }
}
