using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DaysToExpirationEdgeModel : BindableBase
    {
        private bool _active = true;
        private int _daysToExpiration;
        private int _minBidAskSize;
        private double _minIncrement;
        private double _minWidth;
        private int _qty = 1;
        private int _verticalQty = 1;
        private double _baseEdge = .10;
        private double _closeEdge = double.NaN;
        private double _loopMinEdge = .10;
        private double _autoPermMinEdge = .10;

        private double _loopMaxLoss = .10;
        private double _additionalEdgePerContract;
        private double _additionalEdgePerWeightedVega;

        private double _minSpacingForVertical = 0;
        private double _minSpacingForFlys = 0;

        private double _minSpacingForVerticalPercentage = .10;
        private double _minSpacingForFlysPercentage = .10;

        private double _maxAllowedPercentBid;
        private double _maxAllowedAboveEma;
        private double _maxAllowedAboveTheo;
        private double _maxAllowedAboveVola;
        private double _maxThroughTradePx;
        private double _minMarketWidth;
        private double _minMarketCross;

        private double _dynamicBaseEdge = .10;
        private double _dynamicBaseEdgeAddition;
        private double _additionalEdgePerWidth;
        private double _dynamicCloseEdge = double.NaN;
        private double _dynamicCloseEdgeAddition;
        private double _additionalCloseEdgePerWidth;
        private double _dynamicLoopMinEdge = .10;
        private double _dynamicLoopMinEdgeAddition;
        private double _dynamicAutoPermMinEdge = .10;
        private double _dynamicAutoPermMinEdgeAddition;

        private double _dynamicLoopMaxLoss = .10;
        private double _dynamicLoopMaxLossAddition;
        private double _dynamicAdditionalEdgePerContract;
        private double _dynamicAdditionalEdgePerContractAddition;
        private double _dynamicAdditionalEdgePerWeightedVega;
        private double _dynamicAdditionalEdgePerWeightedVegaAddition;

        private double _dynamicMaxAllowedPercentBid;
        private double _dynamicMaxAllowedPercentBidAddition;
        private double _dynamicMaxAllowedAboveEma;
        private double _dynamicMaxAllowedAboveEmaAddition;
        private double _dynamicMaxAllowedAboveTheo;
        private double _dynamicMaxAllowedAboveTheoAddition;
        private double _dynamicMaxAllowedAboveVola;
        private double _dynamicMaxAllowedAboveVolaAddition;
        private double _dynamicMinMarketWidth;
        private double _dynamicMinMarketWidthAddition;


        [JsonProperty]
        public bool Active { get => _active; set => SetValue(ref _active, value); }

        [JsonProperty]
        public int DaysToExpiration { get => _daysToExpiration; set => SetValue(ref _daysToExpiration, value); }

        [JsonProperty]
        public int MinBidAskSize { get => _minBidAskSize; set => SetValue(ref _minBidAskSize, value); }

        [JsonProperty]
        public double MinIncrement { get => _minIncrement; set => SetValue(ref _minIncrement, value); }

        [JsonProperty]
        public double MinWidth { get => _minWidth; set => SetValue(ref _minWidth, value); }

        [JsonProperty]
        public double MinSpacingForVertical { get => _minSpacingForVertical; set => SetValue(ref _minSpacingForVertical, value); }

        [JsonProperty]
        public double MinSpacingForFlys { get => _minSpacingForFlys; set => SetValue(ref _minSpacingForFlys, value); }

        [JsonProperty]
        public double MinSpacingForVerticalPercentage { get => _minSpacingForVerticalPercentage; set => SetValue(ref _minSpacingForVerticalPercentage, value); }

        [JsonProperty]
        public double MinSpacingForFlysPercentage { get => _minSpacingForFlysPercentage; set => SetValue(ref _minSpacingForFlysPercentage, value); }

        [JsonProperty]
        public double BaseEdge { get => _baseEdge; set => SetValue(ref _baseEdge, value); }

        [JsonProperty]
        public double CloseEdge { get => _closeEdge; set => SetValue(ref _closeEdge, value); }

        [JsonProperty]
        public double LoopMinEdge { get => _loopMinEdge; set => SetValue(ref _loopMinEdge, value); }

        [JsonProperty]
        public double AutoPermMinEdge { get => _autoPermMinEdge; set => SetValue(ref _autoPermMinEdge, value); }

        [JsonProperty]
        public double LoopMaxLoss { get => _loopMaxLoss; set => SetValue(ref _loopMaxLoss, value); }

        [JsonProperty]
        public double AdditionalEdgePerContract { get => _additionalEdgePerContract; set => SetValue(ref _additionalEdgePerContract, value); }

        [JsonProperty]
        public double AdditionalEdgePerWeightedVega { get => _additionalEdgePerWeightedVega; set => SetValue(ref _additionalEdgePerWeightedVega, value); }

        [JsonProperty]
        public int VerticalQty { get => _verticalQty; set => SetValue(ref _verticalQty, value); }

        [JsonProperty]
        public int Qty { get => _qty; set => SetValue(ref _qty, value); }

        [JsonProperty]
        public double MaxAllowedPercentBid { get => _maxAllowedPercentBid; set => SetValue(ref _maxAllowedPercentBid, value); }

        [JsonProperty]
        public double MaxAllowedAboveEma { get => _maxAllowedAboveEma; set => SetValue(ref _maxAllowedAboveEma, value); }

        [JsonProperty]
        public double MaxAllowedAboveTheo { get => _maxAllowedAboveTheo; set => SetValue(ref _maxAllowedAboveTheo, value); }

        [JsonProperty]
        public double MaxAllowedAboveVola { get => _maxAllowedAboveVola; set => SetValue(ref _maxAllowedAboveVola, value); }

        [JsonProperty]
        public double MinMarketWidth { get => _minMarketWidth; set => SetValue(ref _minMarketWidth, value); }

        [JsonProperty]
        public double MaxThroughTradePx { get => _maxThroughTradePx; set => SetValue(ref _maxThroughTradePx, value); }

        [JsonProperty]
        public double MinMarketCross { get => _minMarketCross; set => SetValue(ref _minMarketCross, value); }

        [JsonProperty]
        public double DynamicBaseEdge { get => _dynamicBaseEdge; set => SetValue(ref _dynamicBaseEdge, value); }

        [JsonProperty]
        public double DynamicBaseEdgeAddition { get => _dynamicBaseEdgeAddition; set => SetValue(ref _dynamicBaseEdgeAddition, value); }

        [JsonProperty]
        public double AdditionalEdgePerWidth { get => _additionalEdgePerWidth; set => SetValue(ref _additionalEdgePerWidth, value); }

        [JsonProperty]
        public double DynamicCloseEdge { get => _dynamicCloseEdge; set => SetValue(ref _dynamicCloseEdge, value); }

        [JsonProperty]
        public double DynamicCloseEdgeAddition { get => _dynamicCloseEdgeAddition; set => SetValue(ref _dynamicCloseEdgeAddition, value); }

        [JsonProperty]
        public double AdditionalCloseEdgePerWidth { get => _additionalCloseEdgePerWidth; set => SetValue(ref _additionalCloseEdgePerWidth, value); }

        [JsonProperty]
        public double DynamicAutoPermMinEdge { get => _dynamicAutoPermMinEdge; set => SetValue(ref _dynamicAutoPermMinEdge, value); }

        [JsonProperty]
        public double DynamicAutoPermMinEdgeAddition { get => _dynamicAutoPermMinEdgeAddition; set => SetValue(ref _dynamicAutoPermMinEdgeAddition, value); }

        [JsonProperty]
        public double DynamicLoopMinEdge { get => _dynamicLoopMinEdge; set => SetValue(ref _dynamicLoopMinEdge, value); }

        [JsonProperty]
        public double DynamicLoopMinEdgeAddition { get => _dynamicLoopMinEdgeAddition; set => SetValue(ref _dynamicLoopMinEdgeAddition, value); }

        [JsonProperty]
        public double DynamicLoopMaxLoss { get => _dynamicLoopMaxLoss; set => SetValue(ref _dynamicLoopMaxLoss, value); }

        [JsonProperty]
        public double DynamicLoopMaxLossAddition { get => _dynamicLoopMaxLossAddition; set => SetValue(ref _dynamicLoopMaxLossAddition, value); }

        [JsonProperty]
        public double DynamicAdditionalEdgePerContract { get => _dynamicAdditionalEdgePerContract; set => SetValue(ref _dynamicAdditionalEdgePerContract, value); }

        [JsonProperty]
        public double DynamicAdditionalEdgePerContractAddition { get => _dynamicAdditionalEdgePerContractAddition; set => SetValue(ref _dynamicAdditionalEdgePerContractAddition, value); }

        [JsonProperty]
        public double DynamicAdditionalEdgePerWeightedVega { get => _dynamicAdditionalEdgePerWeightedVega; set => SetValue(ref _dynamicAdditionalEdgePerWeightedVega, value); }

        [JsonProperty]
        public double DynamicAdditionalEdgePerWeightedVegaAddition { get => _dynamicAdditionalEdgePerWeightedVegaAddition; set => SetValue(ref _dynamicAdditionalEdgePerWeightedVegaAddition, value); }

        [JsonProperty]
        public double DynamicMaxAllowedPercentBid { get => _dynamicMaxAllowedPercentBid; set => SetValue(ref _dynamicMaxAllowedPercentBid, value); }

        [JsonProperty]
        public double DynamicMaxAllowedPercentBidAddition { get => _dynamicMaxAllowedPercentBidAddition; set => SetValue(ref _dynamicMaxAllowedPercentBidAddition, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveEma { get => _dynamicMaxAllowedAboveEma; set => SetValue(ref _dynamicMaxAllowedAboveEma, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveEmaAddition { get => _dynamicMaxAllowedAboveEmaAddition; set => SetValue(ref _dynamicMaxAllowedAboveEmaAddition, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveTheo { get => _dynamicMaxAllowedAboveTheo; set => SetValue(ref _dynamicMaxAllowedAboveTheo, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveTheoAddition { get => _dynamicMaxAllowedAboveTheoAddition; set => SetValue(ref _dynamicMaxAllowedAboveTheoAddition, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveVola { get => _dynamicMaxAllowedAboveVola; set => SetValue(ref _dynamicMaxAllowedAboveVola, value); }

        [JsonProperty]
        public double DynamicMaxAllowedAboveVolaAddition { get => _dynamicMaxAllowedAboveVolaAddition; set => SetValue(ref _dynamicMaxAllowedAboveVolaAddition, value); }

        [JsonProperty]
        public double DynamicMinMarketWidth { get => _dynamicMinMarketWidth; set => SetValue(ref _dynamicMinMarketWidth, value); }

        [JsonProperty]
        public double DynamicMinMarketWidthAddition { get => _dynamicMinMarketWidthAddition; set => SetValue(ref _dynamicMinMarketWidthAddition, value); }

        internal DaysToExpirationEdgeModel Clone()
        {
            return new DaysToExpirationEdgeModel()
            {
                Active = Active,
                BaseEdge = BaseEdge,
                CloseEdge = CloseEdge,
                AutoPermMinEdge = AutoPermMinEdge,
                LoopMinEdge = LoopMinEdge,
                LoopMaxLoss = LoopMaxLoss,
                Qty = Qty,
                VerticalQty = VerticalQty,
                DaysToExpiration = DaysToExpiration,
                MinBidAskSize = MinBidAskSize,
                MinIncrement = MinIncrement,
                MinWidth = MinWidth,
                MinMarketWidth = MinMarketWidth,
                MinMarketCross = MinMarketCross,
                MaxThroughTradePx = MaxThroughTradePx,
                MinSpacingForFlys = MinSpacingForFlys,
                MinSpacingForVertical = MinSpacingForVertical,
                MinSpacingForFlysPercentage = MinSpacingForFlysPercentage,
                MinSpacingForVerticalPercentage = MinSpacingForVerticalPercentage,
                MaxAllowedPercentBid = MaxAllowedPercentBid,
                MaxAllowedAboveEma = MaxAllowedAboveEma,
                MaxAllowedAboveTheo = MaxAllowedAboveTheo,
                MaxAllowedAboveVola = MaxAllowedAboveVola,
                AdditionalEdgePerContract = AdditionalEdgePerContract,
                AdditionalEdgePerWeightedVega = AdditionalEdgePerWeightedVega,

                DynamicBaseEdge = DynamicBaseEdge,
                DynamicBaseEdgeAddition = DynamicBaseEdgeAddition,
                AdditionalEdgePerWidth = AdditionalEdgePerWidth,
                DynamicCloseEdge = DynamicCloseEdge,
                DynamicCloseEdgeAddition = DynamicCloseEdgeAddition,
                AdditionalCloseEdgePerWidth = AdditionalCloseEdgePerWidth,
                DynamicAutoPermMinEdge = DynamicAutoPermMinEdge,
                DynamicAutoPermMinEdgeAddition = DynamicAutoPermMinEdgeAddition,
                DynamicLoopMinEdge = DynamicLoopMinEdge,
                DynamicLoopMinEdgeAddition = DynamicLoopMinEdgeAddition,
                DynamicLoopMaxLoss = DynamicLoopMaxLoss,
                DynamicLoopMaxLossAddition = DynamicLoopMaxLossAddition,
                DynamicAdditionalEdgePerContract = DynamicAdditionalEdgePerContract,
                DynamicAdditionalEdgePerContractAddition = DynamicAdditionalEdgePerContractAddition,
                DynamicAdditionalEdgePerWeightedVega = DynamicAdditionalEdgePerWeightedVega,
                DynamicAdditionalEdgePerWeightedVegaAddition = DynamicAdditionalEdgePerWeightedVegaAddition,
                DynamicMaxAllowedPercentBid = DynamicMaxAllowedPercentBid,
                DynamicMaxAllowedPercentBidAddition = DynamicMaxAllowedPercentBidAddition,
                DynamicMaxAllowedAboveEma = DynamicMaxAllowedAboveEma,
                DynamicMaxAllowedAboveEmaAddition = DynamicMaxAllowedAboveEmaAddition,
                DynamicMaxAllowedAboveTheo = DynamicMaxAllowedAboveTheo,
                DynamicMaxAllowedAboveVola = DynamicMaxAllowedAboveVola,
                DynamicMaxAllowedAboveTheoAddition = DynamicMaxAllowedAboveTheoAddition,
                DynamicMinMarketWidth = DynamicMinMarketWidth,
                DynamicMinMarketWidthAddition = DynamicMinMarketWidthAddition,
            };
        }

        internal ZeroPlus.Models.Data.Update.DaysToExpirationEdgeModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.DaysToExpirationEdgeModel()
            {
                Active = Active,
                BaseEdge = BaseEdge,
                CloseEdge = CloseEdge,
                AutoPermMinEdge = AutoPermMinEdge,
                LoopMinEdge = LoopMinEdge,
                LoopMaxLoss = LoopMaxLoss,
                Qty = Qty,
                VerticalQty = VerticalQty,
                DaysToExpiration = DaysToExpiration,
                MinBidAskSize = MinBidAskSize,
                MinIncrement = MinIncrement,
                MinWidth = MinWidth,
                MinMarketWidth = MinMarketWidth,
                MinMarketCross = MinMarketCross,
                MaxThroughTradePx = MaxThroughTradePx,
                MinSpacingForFlys = MinSpacingForFlys,
                MinSpacingForVertical = MinSpacingForVertical,
                MinSpacingForFlysPercentage = MinSpacingForFlysPercentage,
                MinSpacingForVerticalPercentage = MinSpacingForVerticalPercentage,
                MaxPercentBid = MaxAllowedPercentBid,
                MaxAllowedAboveEma = MaxAllowedAboveEma,
                MaxAllowedAboveTheo = MaxAllowedAboveTheo,
                MaxAllowedAboveVola = MaxAllowedAboveVola,
                AdditionalEdgePerContract = AdditionalEdgePerContract,
                AdditionalEdgePerWeightedVega = AdditionalEdgePerWeightedVega,

                DynamicBaseEdge = DynamicBaseEdge,
                DynamicBaseEdgeAddition = DynamicBaseEdgeAddition,
                AdditionalEdgePerWidth = AdditionalEdgePerWidth,
                DynamicCloseEdge = DynamicCloseEdge,
                DynamicCloseEdgeAddition = DynamicCloseEdgeAddition,
                AdditionalCloseEdgePerWidth = AdditionalCloseEdgePerWidth,
                DynamicAutoPermMinEdge = DynamicAutoPermMinEdge,
                DynamicAutoPermMinEdgeAddition = DynamicAutoPermMinEdgeAddition,
                DynamicLoopMinEdge = DynamicLoopMinEdge,
                DynamicLoopMinEdgeAddition = DynamicLoopMinEdgeAddition,
                DynamicLoopMaxLoss = DynamicLoopMaxLoss,
                DynamicLoopMaxLossAddition = DynamicLoopMaxLossAddition,
                DynamicAdditionalEdgePerContract = DynamicAdditionalEdgePerContract,
                DynamicAdditionalEdgePerContractAddition = DynamicAdditionalEdgePerContractAddition,
                DynamicAdditionalEdgePerWeightedVega = DynamicAdditionalEdgePerWeightedVega,
                DynamicAdditionalEdgePerWeightedVegaAddition = DynamicAdditionalEdgePerWeightedVegaAddition,
                DynamicMaxAllowedPercentBid = DynamicMaxAllowedPercentBid,
                DynamicMaxAllowedPercentBidAddition = DynamicMaxAllowedPercentBidAddition,
                DynamicMaxAllowedAboveEma = DynamicMaxAllowedAboveEma,
                DynamicMaxAllowedAboveEmaAddition = DynamicMaxAllowedAboveEmaAddition,
                DynamicMaxAllowedAboveTheo = DynamicMaxAllowedAboveTheo,
                DynamicMaxAllowedAboveTheoAddition = DynamicMaxAllowedAboveTheoAddition,
                DynamicMaxAllowedAboveVola = DynamicMaxAllowedAboveVola,
                DynamicMaxAllowedAboveVolaAddition = DynamicMaxAllowedAboveVolaAddition,
                DynamicMinMarketWidth = DynamicMinMarketWidth,
                DynamicMinMarketWidthAddition = DynamicMinMarketWidthAddition,
            };
        }
    }
}
