namespace ZeroPlus.Oms.Data
{
    public class UnderlyingRiskModel
    {
        public static UnderlyingRiskModel Default => new()
        {
            UnderlyingSymbols = "ALL",
            DontTradeThroughBidPercent = false,
            DontTradeThroughMid = false,
            DontTradeThroughEdge = false,
            DontTradeThroughMarketCap = false,
            AutoCancelWhenThroughMid = false,
            AutoCancelWhenThroughEdge = false,
        };

        public string UnderlyingSymbols { get; set; }
        public double EdgeViolationMinLimit { get; set; } = 0.50;
        public bool LastFillCrossThresholdEnabled { get; set; }
        public double LastFillCrossThreshold { get; set; } = .40;
        public int QtyViolationMinLimit { get; set; } = 5;
        public double RiskCheckMarketPercentage { get; set; } = .15;
        public double MaxTheoToAdjTheoOffset { get; set; } = .50;
        public double StaleTheoRiskThreshold { get; set; } = 30000;
        public bool DontTradeThroughBidPercent { get; set; } = true;
        public double DontTradeThroughBidPercentValue { get; set; } = 0.5;
        public bool DontTradeThroughMid { get; set; } = true;
        public bool DontTradeThroughEdge { get; set; } = true;
        public bool DontTradeThroughMarketCap { get; set; } = true;
        public bool AutoCancelWhenThroughMid { get; set; } = true;
        public bool AutoCancelWhenThroughEdge { get; set; } = true;
        public bool OverrideEdgeCheck { get; set; }
    }
}
