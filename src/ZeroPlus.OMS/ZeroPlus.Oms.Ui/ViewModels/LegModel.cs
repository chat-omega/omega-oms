namespace ZeroPlus.Oms.Ui.ViewModels
{
    internal class LegModel
    {
        public double BaseLine { get; set; } = double.NaN;
        public double TestAdjTheo { get; set; } = double.NaN;
        public double AdjTheo { get; set; } = double.NaN;
        public double AdjEma { get; set; } = double.NaN;
        public double HanweckTheo { get; set; } = double.NaN;

        public int Ratio { get; set; }

        public uint TestAdjTheoSeq { get; internal set; }
        public uint AdjTheoSeq { get; internal set; }
        public ulong EmaSeq { get; internal set; }
        public bool TheoJumpDetected { get; internal set; }

        public double TradeIntBid { get; internal set; } = double.NaN;
        public double TradeIntAsk { get; internal set; } = double.NaN;
        public double TradeIntBidBase { get; internal set; } = double.NaN;
        public double TradeIntAskBase { get; internal set; } = double.NaN;
        public double TradeIntBidUnderlying { get; internal set; } = double.NaN;
        public double TradeIntAskUnderlying { get; internal set; } = double.NaN;
        public double BestBid { get; internal set; } = double.NaN;
        public double BestAsk { get; internal set; } = double.NaN;
        public double BestBidBase { get; internal set; } = double.NaN;
        public double BestAskBase { get; internal set; } = double.NaN;
        public double BestBidUnderlying { get; internal set; } = double.NaN;
        public double BestAskUnderlying { get; internal set; } = double.NaN;
    }
}
