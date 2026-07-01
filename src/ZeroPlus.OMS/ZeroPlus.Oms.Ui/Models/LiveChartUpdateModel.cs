using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public class LiveChartUpdateModel
    {
        public DateTime Timestamp { get; internal set; }
        public double AdjTheo { get; internal set; } = double.NaN;
        public double AdjEma { get; internal set; } = double.NaN;
        public double HwTheo { get; internal set; } = double.NaN;
        public double Underlying { get; internal set; } = double.NaN;
        public double TestValue { get; internal set; } = double.NaN;
        public double BaseLine { get; internal set; } = double.NaN;
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
