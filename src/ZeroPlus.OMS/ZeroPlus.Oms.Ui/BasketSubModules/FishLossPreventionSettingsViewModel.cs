using DevExpress.Mvvm;
using System;
using System.Windows.Input;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class FishLossPreventionSettingsViewModel : ViewModelBase
    {
        public PxCrossOption[] PxCrossOptions { get; } = (PxCrossOption[])Enum.GetValues(typeof(PxCrossOption));
        public FishLossConfig FishLossConfig { get; set; } = new();
        private bool isEdgeScanFeedAutoTrader = true;
        public bool IsEdgeScanFeedAutoTrader { get => isEdgeScanFeedAutoTrader; set => SetValue(ref isEdgeScanFeedAutoTrader, value); }
    }
    public class FishLossConfig
    {
        public PxCrossOption PxCrossOption { get; set; } = PxCrossOption.Ignore;
        public bool PreviousAttemptCrossCheckEnabled { get; set; } = false;
        public bool MinEdgeToPreviousAttemptCheckEnabled { get; set; } = true;
        public bool MinTimeToPreviousAttemptCheckEnabled { get; set; } = true;
        public double MinTimeToPreviousAttemptIntervalSeconds { get; set; } = 600;
        public bool MinTimeToPermLoserCheckEnabled { get; set; } = false;
        public double MinTimeToPermLoserIntervalSeconds { get; set; } = 900;

        // Mid price and width cancellation
        public bool CancelWithEdgeToMidEnabled { get; set; } = false;
        public double CancelWithMidEdge { get; set; } = 0.0;
        public bool CancelWithWidthEnabled { get; set; } = false;
        public double CancelWithWidthThreshold { get; set; } = 0.0;
        public bool MaxWidthCheckEnabled { get; set; } = false;
        public double MaxWidthCheckPx { get; set; } = 0.0;

        // Minimum edge checks
        public bool MinTheoEdgeCheckEnabled { get; set; } = false;
        public double MinTheoEdgeCheckEdge { get; set; } = 0.0;
        public bool MinMidEdgeCheckEnabled { get; set; } = false;
        public double MinMidEdgeCheckEdge { get; set; } = 0.0;
        public bool MinEmaEdgeCheckEnabled { get; set; } = false;
        public double MinEmaEdgeCheckEdge { get; set; } = 0.0;
        public bool MinEdgeToMarketCheckEnabled { get; set; } = false;
        public double MinEdgeToMarketCheckEdge { get; set; } = 0.0;

        // Bid/Ask related checks
        public bool MaxPercentBidCheckEnabled { get; set; } = false;
        public double MaxPercentBidCheckEdge { get; set; } = 0.0;
        public bool MinBidCheckEnabled { get; set; } = false;
        public double MinBidCheckBidValue { get; set; } = 0.0;
        public bool MinBidAskSizeCheckEnabled { get; set; } = true;
        public int MinBidAskSize { get; set; } = 1;

        // EMA width percentage checks
        public bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; set; } = false;
        public double MinEmaWidthPercentEdgeToTheoCheckEdge { get; set; } = 0.0;
        public double BlockZeroPrice { get; set; } = 0.0;
        public bool MaxPercentBidCheckUseBestQuote { get; set; } = false;
    }
}