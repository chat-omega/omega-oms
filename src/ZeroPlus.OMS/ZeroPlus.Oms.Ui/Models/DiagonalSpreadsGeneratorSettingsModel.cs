using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DiagonalSpreadsGeneratorSettingsModel : BindableBase, IDiagonalSpreadsGeneratorSettings
    {


        public bool _Leg1DeltaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1DeltaRangeEnabled { get; set; }

        public double _Leg1DeltaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1DeltaRangeFloor { get; set; }

        public double _Leg1DeltaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1DeltaRangeCeil { get; set; }

        public bool _Leg2DeltaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2DeltaRangeEnabled { get; set; }

        public double _Leg2DeltaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2DeltaRangeFloor { get; set; }

        public double _Leg2DeltaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2DeltaRangeCeil { get; set; }

        public bool _SpreadDeltaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadDeltaRangeEnabled { get; set; }

        public double _SpreadDeltaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadDeltaRangeFloor { get; set; }

        public double _SpreadDeltaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadDeltaRangeCeil { get; set; }

        [JsonProperty]
        [Bindable]
        public partial TheoModel TheoModel { get; set; }

        public bool _Leg1TheoRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1TheoRangeEnabled { get; set; }

        public double _Leg1TheoRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1TheoRangeFloor { get; set; }

        public double _Leg1TheoRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1TheoRangeCeil { get; set; }

        public bool _Leg2TheoRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2TheoRangeEnabled { get; set; }

        public double _Leg2TheoRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2TheoRangeFloor { get; set; }

        public double _Leg2TheoRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2TheoRangeCeil { get; set; }

        public bool _SpreadTheoRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoRangeEnabled { get; set; }

        public double _SpreadTheoRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoRangeFloor { get; set; }

        public double _SpreadTheoRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoRangeCeil { get; set; }

        public bool _Leg1VegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1VegaRangeEnabled { get; set; }

        public double _Leg1VegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeFloor { get; set; }

        public double _Leg1VegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1VegaRangeCeil { get; set; }

        public bool _Leg2VegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2VegaRangeEnabled { get; set; }

        public double _Leg2VegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2VegaRangeFloor { get; set; }

        public double _Leg2VegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2VegaRangeCeil { get; set; }

        public bool _Leg1WeightedVegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1WeightedVegaRangeEnabled { get; set; }

        public double _Leg1WeightedVegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1WeightedVegaRangeFloor { get; set; }

        public double _Leg1WeightedVegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1WeightedVegaRangeCeil { get; set; }

        public bool _Leg2WeightedVegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2WeightedVegaRangeEnabled { get; set; }

        public double _Leg2WeightedVegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2WeightedVegaRangeFloor { get; set; }

        public double _Leg2WeightedVegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2WeightedVegaRangeCeil { get; set; }

        public bool _SpreadVegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadVegaRangeEnabled { get; set; }

        public double _SpreadVegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadVegaRangeFloor { get; set; }

        public double _SpreadVegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadVegaRangeCeil { get; set; }

        public bool _WeightedVegaRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool WeightedVegaRangeEnabled { get; set; }

        public double _WeightedVegaRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double WeightedVegaRangeFloor { get; set; }

        public double _WeightedVegaRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double WeightedVegaRangeCeil { get; set; }

        public bool _Leg1MarketRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1MarketRangeEnabled { get; set; }

        public double _Leg1MarketRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeFloor { get; set; }

        public double _Leg1MarketRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1MarketRangeCeil { get; set; }

        public bool _Leg2MarketRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2MarketRangeEnabled { get; set; }

        public double _Leg2MarketRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2MarketRangeFloor { get; set; }

        public double _Leg2MarketRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2MarketRangeCeil { get; set; }

        public bool _SpreadMarketRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadMarketRangeEnabled { get; set; }

        public double _SpreadMarketRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadMarketRangeFloor { get; set; }

        public double _SpreadMarketRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadMarketRangeCeil { get; set; }

        public bool _Leg1WidthRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1WidthRangeEnabled { get; set; }

        public double _Leg1WidthRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1WidthRangeFloor { get; set; }

        public double _Leg1WidthRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1WidthRangeCeil { get; set; }

        public bool _Leg2WidthRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2WidthRangeEnabled { get; set; }

        public double _Leg2WidthRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2WidthRangeFloor { get; set; }

        public double _Leg2WidthRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2WidthRangeCeil { get; set; }

        public bool _SpreadWidthRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadWidthRangeEnabled { get; set; }

        public double _SpreadWidthRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadWidthRangeFloor { get; set; }

        public double _SpreadWidthRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadWidthRangeCeil { get; set; }

        public bool _SpreadSpacingRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacingRangeEnabled { get; set; }

        public int _SpreadSpacingRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial int SpreadSpacingRangeFloor { get; set; }

        public int _SpreadSpacingRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial int SpreadSpacingRangeCeil { get; set; }

        public bool _SpreadSpacingListEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadSpacingListEnabled { get; set; }

        public string _SpreadSpacingList;
        [JsonProperty]
        [Bindable]
        public partial string SpreadSpacingList { get; set; }

        public bool _SpreadExpirationGapRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadExpirationGapRangeEnabled { get; set; }

        public int _SpreadExpirationGapRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial int SpreadExpirationGapRangeFloor { get; set; }

        public int _SpreadExpirationGapRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial int SpreadExpirationGapRangeCeil { get; set; }

        public bool _SpreadExpirationGapListEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadExpirationGapListEnabled { get; set; }

        public string _SpreadExpirationGapList;
        [JsonProperty]
        [Bindable]
        public partial string SpreadExpirationGapList { get; set; }

        public bool _SpreadStrikeSpacingRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadStrikeSpacingRangeEnabled { get; set; }

        public double _SpreadStrikeSpacingRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double SpreadStrikeSpacingRangeFloor { get; set; }

        public double _SpreadStrikeSpacingRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double SpreadStrikeSpacingRangeCeil { get; set; }

        public bool _SpreadStrikeSpacingListEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadStrikeSpacingListEnabled { get; set; }

        public string _SpreadStrikeSpacingList;
        [JsonProperty]
        [Bindable]
        public partial string SpreadStrikeSpacingList { get; set; }

        public bool _SpreadTheoAboveMidEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoAboveMidEnabled { get; set; }

        public double _SpreadTheoAboveMid;
        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoAboveMid { get; set; }

        public bool _SpreadTheoBelowMidEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoBelowMidEnabled { get; set; }

        public double _SpreadTheoBelowMid;
        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoBelowMid { get; set; }

        public bool _SpreadTheoAbsMidEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool SpreadTheoAbsMidEnabled { get; set; }

        public double _SpreadTheoAbsMid;
        [JsonProperty]
        [Bindable]
        public partial double SpreadTheoAbsMid { get; set; }

        public bool _Leg1StrikeRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg1StrikeRangeEnabled { get; set; }

        public double _Leg1StrikeRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg1StrikeRangeFloor { get; set; }

        public double _Leg1StrikeRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg1StrikeRangeCeil { get; set; }

        public bool _Leg2StrikeRangeEnabled;
        [JsonProperty]
        [Bindable]
        public partial bool Leg2StrikeRangeEnabled { get; set; }

        public double _Leg2StrikeRangeFloor;
        [JsonProperty]
        [Bindable]
        public partial double Leg2StrikeRangeFloor { get; set; }

        public double _Leg2StrikeRangeCeil;
        [JsonProperty]
        [Bindable]
        public partial double Leg2StrikeRangeCeil { get; set; }

        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int Leg1Ratio { get; set; }

        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int Leg2Ratio { get; set; }

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
                   SpreadDeltaRangeEnabled ||
                   Leg1TheoRangeEnabled ||
                   Leg2TheoRangeEnabled ||
                   SpreadTheoRangeEnabled ||
                   Leg1VegaRangeEnabled ||
                   Leg1WeightedVegaRangeEnabled ||
                   Leg2VegaRangeEnabled ||
                   Leg2WeightedVegaRangeEnabled ||
                   SpreadVegaRangeEnabled ||
                   WeightedVegaRangeEnabled ||
                   Leg1MarketRangeEnabled ||
                   Leg1WidthRangeEnabled ||
                   Leg2MarketRangeEnabled ||
                   Leg2WidthRangeEnabled ||
                   SpreadMarketRangeEnabled ||
                   SpreadWidthRangeEnabled ||
                   SpreadTheoAboveMidEnabled ||
                   SpreadTheoBelowMidEnabled ||
                   SpreadVolaToHanweckDiffEnabled ||
                   SpreadTheoAbsMidEnabled;
        }

        public DiagonalSpreadsGeneratorSettingsModel Clone()
        {
            return new DiagonalSpreadsGeneratorSettingsModel()
            {
                Leg1DeltaRangeEnabled = Leg1DeltaRangeEnabled,
                Leg1DeltaRangeFloor = Leg1DeltaRangeFloor,
                Leg1DeltaRangeCeil = Leg1DeltaRangeCeil,
                Leg2DeltaRangeEnabled = Leg2DeltaRangeEnabled,
                Leg2DeltaRangeFloor = Leg2DeltaRangeFloor,
                Leg2DeltaRangeCeil = Leg2DeltaRangeCeil,
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
                SpreadTheoRangeEnabled = SpreadTheoRangeEnabled,
                SpreadTheoRangeFloor = SpreadTheoRangeFloor,
                SpreadTheoRangeCeil = SpreadTheoRangeCeil,
                Leg1VegaRangeEnabled = Leg1VegaRangeEnabled,
                Leg1VegaRangeFloor = Leg1VegaRangeFloor,
                Leg1VegaRangeCeil = Leg1VegaRangeCeil,
                Leg2VegaRangeEnabled = Leg2VegaRangeEnabled,
                Leg2VegaRangeFloor = Leg2VegaRangeFloor,
                Leg2VegaRangeCeil = Leg2VegaRangeCeil,
                Leg1WeightedVegaRangeEnabled = Leg1WeightedVegaRangeEnabled,
                Leg1WeightedVegaRangeFloor = Leg1WeightedVegaRangeFloor,
                Leg1WeightedVegaRangeCeil = Leg1WeightedVegaRangeCeil,
                Leg2WeightedVegaRangeEnabled = Leg2WeightedVegaRangeEnabled,
                Leg2WeightedVegaRangeFloor = Leg2WeightedVegaRangeFloor,
                Leg2WeightedVegaRangeCeil = Leg2WeightedVegaRangeCeil,
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
                SpreadMarketRangeEnabled = SpreadMarketRangeEnabled,
                SpreadMarketRangeFloor = SpreadMarketRangeFloor,
                SpreadMarketRangeCeil = SpreadMarketRangeCeil,
                Leg1WidthRangeEnabled = Leg1WidthRangeEnabled,
                Leg1WidthRangeFloor = Leg1WidthRangeFloor,
                Leg1WidthRangeCeil = Leg1WidthRangeCeil,
                Leg2WidthRangeEnabled = Leg2WidthRangeEnabled,
                Leg2WidthRangeFloor = Leg2WidthRangeFloor,
                Leg2WidthRangeCeil = Leg2WidthRangeCeil,
                SpreadWidthRangeEnabled = SpreadWidthRangeEnabled,
                SpreadWidthRangeFloor = SpreadWidthRangeFloor,
                SpreadWidthRangeCeil = SpreadWidthRangeCeil,
                SpreadSpacingRangeEnabled = SpreadSpacingRangeEnabled,
                SpreadSpacingRangeFloor = SpreadSpacingRangeFloor,
                SpreadSpacingRangeCeil = SpreadSpacingRangeCeil,
                SpreadSpacingListEnabled = SpreadSpacingListEnabled,
                SpreadSpacingList = SpreadSpacingList,
                SpreadExpirationGapRangeEnabled = SpreadExpirationGapRangeEnabled,
                SpreadExpirationGapRangeFloor = SpreadExpirationGapRangeFloor,
                SpreadExpirationGapRangeCeil = SpreadExpirationGapRangeCeil,
                SpreadExpirationGapListEnabled = SpreadExpirationGapListEnabled,
                SpreadExpirationGapList = SpreadExpirationGapList,
                SpreadStrikeSpacingRangeEnabled = SpreadStrikeSpacingRangeEnabled,
                SpreadStrikeSpacingRangeFloor = SpreadStrikeSpacingRangeFloor,
                SpreadStrikeSpacingRangeCeil = SpreadStrikeSpacingRangeCeil,
                SpreadStrikeSpacingListEnabled = SpreadStrikeSpacingListEnabled,
                SpreadStrikeSpacingList = SpreadStrikeSpacingList,
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
                Leg1Ratio = Leg1Ratio,
                Leg2Ratio = Leg2Ratio,
                SpreadVolaToHanweckDiffEnabled = SpreadVolaToHanweckDiffEnabled,
                SpreadVolaToHanweckDiff = SpreadVolaToHanweckDiff,
            };
        }
    }
}
