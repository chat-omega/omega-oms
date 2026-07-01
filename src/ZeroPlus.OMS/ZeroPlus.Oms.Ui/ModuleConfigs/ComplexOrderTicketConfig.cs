using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ComplexOrderTicketConfig
    {
        public OmsOrder Order { get; set; }
        public bool SubmitWithDelayPercentBidEnabled { get; set; } = true;
        public double SubmitWithDelayPercentBid { get; set; } = .05;
        public bool SubmitWithDelayTheoReferenceEnabled { get; set; } = true;
        public double SubmitWithDelayEdgeToTheo { get; set; } = .2;
        public bool SubmitWithDelayDeltaAdjustEnabled { get; set; } = true;
        public Side SubmitWithDelaySide { get; set; } = Side.Buy;
        public double SubmitWithDelayDeltaAdjLevel { get; set; }
        public bool SubmitWithDelayBidRangeEnabled { get; set; }
        public double SubmitWithDelayMinBid { get; set; }
        public double SubmitWithDelayMaxBid { get; set; }
        public bool SubmitWithDelayAskRangeEnabled { get; set; }
        public double SubmitWithDelayMinAsk { get; set; }
        public double SubmitWithDelayMaxAsk { get; set; }
        public bool SubmitWithDelayCancelOnUserPositionChangeEnabled { get; set; } = true;
        public bool SubmitWithDelayCancelOnLegVolumeChangeEnabled { get; set; } = true;
        public int SubmitWithDelayCancelOnVolumeChange { get; set; } = 10;
        public bool SubmitWithDelayPlayPreSubmitNotification { get; set; } = true;
        public int SubmitWithDelayPreSubmitNotificationSeconds { get; set; } = 10;
        public bool EnableControlPxKey { get; set; }
    }
}