using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Automation
{
    public interface ILoopSettings
    {
        double LoopMinEdge { get; }
        int LoopInterval { get; }
        int LoopIntervalMax { get; }
        int ContraFishInterval { get; }
        int ContraFishIntervalMax { get; }
        int AttemptResubmit { get; }
        int LoopResubmit { get; }
        double ContraFishEdge { get; }
        double LoopMaxLoss { get; }
        bool LoopingEnabled { get; }
        double ContraFishPriceIncrement { get; }
        bool LoopFreeLookOnAll { get; }
        bool FreeLookWhenGettingCloseEdge { get; }
        bool FreeLookRequireMinFillTime { get; }
        double FreeLookMinFillTime { get; }
        bool LoopFreeLookOnAllUsingTicks { get; }
        double FreeLookOnAllIncrementTicks { get; }
        double FreeLookOnAllIncrement { get; }
        LoopPricingMode LoopPricingMode { get; }
        bool AdjustClosingPriceToMarket { get; set; }
        bool AdjustClosingPriceToMarketWinnersOnly { get; set; }
        double LastEdgeTightenPercent { get; set; }
        bool MaintainLastEdge { get; set; }
        bool AutoHedgeOnClose { get; set; }
        bool AutoHedgeOnCloseSizeOnly { get; set; }
        bool AutoHedgeOnFailure { get; set; }
        bool AutoHedgePartial { get; set; }
        bool AutoHedgeOpenTicket { get; set; }
        double MinHedgeHouseEdge { get; set; }
        bool ScratchOnLowDeltaSize { get; set; }
        double ScratchOnLowDeltaMax { get; set; }
        double ScratchOnLowDeltaMaxLoss { get; set; }
        int ScratchOnLowDeltaMinSize { get; set; }
    }
}