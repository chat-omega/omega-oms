using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models;

public interface IAutoTraderSettings
{
    AutomationConfigModel AutomationConfig { get; set; }
    string Uid { get; }
    // Order submission settings
    int SubmitWithDelayInterval { get; }
    int SubmitWithDelayIntervalEnd { get; }
    int CancelOnAmountOfFillsCount { get; }
    int MaxRestingOrdersCount { get; }
    PxCrossOption PxCrossOption { get; }

    // Timer-based cancellation
    bool CancelWithTimerEnabled { get; }
    double CancelWithTimer { get; }

    // Theoretical price edge cancellation
    bool CancelWithEdgeToTheoEnabled { get; }
    double CancelWithTheoEdge { get; }
    bool CancelWithEdgeToAdjTheoEnabled { get; }
    double CancelWithAdjTheoEdge { get; }

    // Underlying price cancellation
    bool CancelWithUnderlyingPxEnabled { get; }
    double CancelWithUnderlyingPx { get; }
    bool CancelWithUnderlyingDeltaPxEnabled { get; }
    double CancelWithUnderlyingDeltaPx { get; }

    // Mid price and width cancellation
    bool CancelWithEdgeToMidEnabled { get; }
    double CancelWithMidEdge { get; }
    bool CancelWithWidthEnabled { get; }
    double CancelWithWidthThreshold { get; }
    bool MaxWidthCheckEnabled { get; }
    double MaxWidthCheckPx { get; }

    // Minimum edge checks
    bool MinTheoEdgeCheckEnabled { get; }
    double MinTheoEdgeCheckEdge { get; }
    bool MinMidEdgeCheckEnabled { get; }
    double MinMidEdgeCheckEdge { get; }
    bool MinEmaEdgeCheckEnabled { get; }
    double MinEmaEdgeCheckEdge { get; }
    bool MinEdgeToMarketCheckEnabled { get; }
    double MinEdgeToMarketCheckEdge { get; }

    // Bid/Ask related checks
    bool MaxPercentBidCheckEnabled { get; }
    double MaxPercentBidCheckEdge { get; }
    bool MinBidCheckEnabled { get; }
    double MinBidCheckBidValue { get; }
    bool MinBidAskSizeCheckEnabled { get; }
    int MinBidAskSize { get; }

    // EMA width percentage checks
    bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; }
    double MinEmaWidthPercentEdgeToTheoCheckEdge { get; }
}