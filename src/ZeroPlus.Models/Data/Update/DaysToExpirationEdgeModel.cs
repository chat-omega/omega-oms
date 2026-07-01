namespace ZeroPlus.Models.Data.Update
{
    public class DaysToExpirationEdgeModel
    {
        public bool Active { get; set; }
        public int DaysToExpiration { get; set; }
        public int MinBidAskSize { get; set; }
        public double MinIncrement { get; set; }
        public double MinWidth { get; set; }
        public double MinSpacingForVertical { get; set; }
        public double MinSpacingForFlys { get; set; }
        public double MinSpacingForVerticalPercentage { get; set; }
        public double MinSpacingForFlysPercentage { get; set; }
        public double BaseEdge { get; set; }
        public double CloseEdge { get; set; } = double.NaN;
        public double LoopMinEdge { get; set; }
        public double AutoPermMinEdge { get; set; }
        public int VerticalQty { get; set; }
        public int Qty { get; set; }
        public double MaxPercentBid { get; set; } = double.NaN;
        public double LoopMaxLoss { get; set; }
        public double AdditionalEdgePerContract { get; set; }
        public double AdditionalEdgePerWeightedVega { get; set; }
        public double MaxAllowedAboveEma { get; set; }
        public double MaxAllowedAboveTheo { get; set; }
        public double MaxAllowedAboveVola { get; set; }
        public double MinMarketWidth { get; set; }
        public double MaxThroughTradePx { get; set; }
        public double MinMarketCross { get; set; } = double.NaN;
        public double DynamicBaseEdge { get; set; }
        public double DynamicBaseEdgeAddition { get; set; }
        public double AdditionalEdgePerWidth { get; set; }
        public double DynamicCloseEdge { get; set; }
        public double DynamicCloseEdgeAddition { get; set; }
        public double AdditionalCloseEdgePerWidth { get; set; }
        public double DynamicAutoPermMinEdge { get; set; }
        public double DynamicAutoPermMinEdgeAddition { get; set; }
        public double DynamicLoopMinEdge { get; set; }
        public double DynamicLoopMinEdgeAddition { get; set; }
        public double DynamicLoopMaxLoss { get; set; }
        public double DynamicLoopMaxLossAddition { get; set; }
        public double DynamicAdditionalEdgePerContract { get; set; }
        public double DynamicAdditionalEdgePerContractAddition { get; set; }
        public double DynamicAdditionalEdgePerWeightedVega { get; set; }
        public double DynamicAdditionalEdgePerWeightedVegaAddition { get; set; }
        public double DynamicMaxAllowedPercentBid { get; set; }
        public double DynamicMaxAllowedPercentBidAddition { get; set; }
        public double DynamicMaxAllowedAboveEma { get; set; }
        public double DynamicMaxAllowedAboveEmaAddition { get; set; }
        public double DynamicMaxAllowedAboveTheo { get; set; }
        public double DynamicMaxAllowedAboveTheoAddition { get; set; }
        public double DynamicMaxAllowedAboveVola { get; set; }
        public double DynamicMaxAllowedAboveVolaAddition { get; set; }
        public double DynamicMinMarketWidth { get; set; }
        public double DynamicMinMarketWidthAddition { get; set; }
    }
}