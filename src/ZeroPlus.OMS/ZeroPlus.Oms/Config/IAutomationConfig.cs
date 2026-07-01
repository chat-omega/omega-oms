using System.Collections.Generic;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Config
{
    public interface IAutomationConfig
    {
        string Title { get; set; }
        int AttemptResubmit { get; set; }
        int AutomationPartialResubmitCount { get; set; }
        double AutomationRequiredPartialFillPercentage { get; set; }
        double CloseEdgeMinValue { get; set; }
        bool? CloseOrderMode { get; set; }
        ClosingTypes ClosingMode { get; set; }
        double ContraFishEdge { get; set; }
        int ContraFishInterval { get; set; }
        int ContraFishIntervalMax { get; set; }
        double ContraFishPriceIncrement { get; set; }
        double FishEdge { get; set; }
        int FishInterval { get; set; }
        double FishPriceIncrement { get; set; }
        double FreeLookOnAllIncrement { get; set; }
        bool GoFishAutoCloseEnabled { get; set; }
        bool LeaveAutoCloseResting { get; set; }
        int LoopCountBeforeSizeup { get; set; }
        string LooperCloseRoute { get; set; }
        bool LooperDynamicRouting { get; set; }
        bool AttemptIncrementUsingDynamicRoute { get; set; }
        bool EnableDynamicRouteForOpeningOrders { get; set; }
        bool EnableDynamicRouteForClosingOrders { get; set; }
        Dictionary<string, string> ExchToRouteMap { get; set; }
        string LooperOpenRoute { get; set; }
        string LooperOpenRouteSingleLeg { get; set; }
        string LooperCloseRouteSingleLeg { get; set; }
        bool UseSingleLegSeparateLooperRoutes { get; set; }
        bool LoopFreeLook { get; set; }
        bool AdjustClosingPriceToMarket { get; set; }
        bool LoopFreeLookOnAll { get; set; }
        bool LoopingEnabled { get; set; }
        int LoopInterval { get; set; }
        int LoopIntervalMax { get; set; }
        double LoopMaxLoss { get; set; }
        double LoopMinEdge { get; set; }
        int LoopResubmit { get; set; }
        int LoopSizeupQty { get; set; }
        int MaxLoopCount { get; set; }
        bool UseResubmit { get; set; }
        bool SizeUpOnClosingLoop { get; set; }
        bool SizeUpOnHardSideOnly { get; set; }
        LoopSizeupType LoopSizeupType { get; set; }
        LoopIncrementType LoopIncrementType { get; set; }
        LoopIntervalType LoopIntervalType { get; set; }
        LoopCloseEdgeType LoopCloseEdgeType { get; set; }
        double LastEdgeTightenPercent { get; set; }
        IDynamicEdgeModel DynamicEdgeModel { get; set; }
        IDynamicIntervalModel DynamicIntervalModel { get; set; }
    }
}