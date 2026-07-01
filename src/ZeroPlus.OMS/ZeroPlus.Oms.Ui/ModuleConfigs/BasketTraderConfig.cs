using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class BasketTraderConfig
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool FishModeEnabled { get; set; }
        public bool UseResubmit { get; set; }
        public double FishEdge { get; set; }
        public double FishPriceIncrement { get; set; }
        public int FishInterval { get; set; } = 1100;
        public double ContraFishEdge { get; set; } = 0.3;
        public double ContraFishPriceIncrement { get; set; } = 0.05;
        public LoopIncrementType LoopIncrementType { get; set; } = LoopIncrementType.Static;
        public LoopIntervalType LoopIntervalType { get; set; } = LoopIntervalType.Static;
        public LoopCloseEdgeType LoopCloseEdgeType { get; set; } = LoopCloseEdgeType.Static;
        public int ContraFishIntervalV2 { get; set; }
        public int ContraFishIntervalMaxV2 { get; set; }
        public bool SizeUpOnClosingLoop { get; set; }
        public bool SizeUpOnHardSideOnly { get; set; }
        public bool RequireAdjEdgeForSizeUp { get; set; }
        public bool LayoutLocked { get; set; } = true;
        public bool ShowBasketSettings { get; set; } = true;
        public bool ShowEdgeSettings { get; set; } = true;
        public bool ShowDerivativeValuesSettings { get; set; } = true;
        public bool ShowMarketMakerSettings { get; set; } = true;
        public bool ShowHedgeSettings { get; set; } = true;
        public bool ShowEmaSettings { get; set; } = true;
        public bool ShowPermSettings { get; set; } = true;
        public bool ShowAdvancedPermSettings { get; set; } = false;
        public bool ShowMorphSettings { get; set; } = true;
        public bool ShowContraSettings { get; set; } = true;
        public bool ShowSubmitWithDelaySettings { get; set; } = true;
        public bool ShowRouteSettings { get; set; } = true;
        public bool ShowAdvancedRouteSettings { get; set; } = false;
        public bool ShowEdgeToTheoModelSettings { get; set; } = true;
        public bool ShowNagbotSettings { get; set; } = true;
        public bool ShowLoggingSettings { get; set; } = false;
        public bool ShowAlerts { get; set; } = false;
        public bool ShowMatrixAlgos { get; set; } = false;
        public bool ShowAutoLegSettings { get; set; } = true;
        public bool ShowNotificationSettings { get; set; } = true;
        public bool ShowFishSettings { get; set; } = true;
        public bool ShowHedgeHouseSettings { get; set; } = true;
        public bool ShowFishLossSettings { get; set; } = true;
        public bool ShowLegOutSettings { get; set; } = true;
        public bool ShowLegInSettings { get; set; } = false;
        public bool ShowSweepTradeSettings { get; set; } = false;
        public bool ShowAutoCloseSettings { get; set; } = true;
        public bool ShowAutoCancelSettings { get; set; } = true;
        public bool ShowAutoPermSettings { get; set; } = true;
        public bool AutoPermEnabled { get; set; }
        public double AutoPermMinEdge { get; set; }
        public int AutoPermOrderCount { get; set; }
        public int AutoPermMaxGeneration { get; set; } = 1;
        public AutoPermSubmissionStyle AutoPermSubmissionStyle { get; set; }
        public int AutoPermOrderInitialSize { get; set; } = 1;
        public bool ShowSubscriptionManager { get; set; } = true;
        public bool ShowBasketStats { get; set; } = true;
        public bool ShowStockTiedSettings { get; set; } = true;
        public bool ShowCheapoSettings { get; set; } = true;
        public bool ShowBlockListSettings { get; set; } = true;
        public bool RecalculatePriceOnInterval { get; set; } = false;
        public bool AutoPermOnFill { get; set; } = false;
        public bool AutoPermOthers { get; set; } = false;
        public List<string> AutoPermOthersList { get; set; } = new List<string>();
        public bool SubmitAutoPerms { get; set; } = false;
        public bool WaitForPrevious { get; set; } = true;
        public AutoPermSelectionMode AutoPermSelectionMode { get; set; }
        public PermOperationModel AutoPermTemplate { get; set; }
        public double CancelWithTheoEdge { get; set; }
        public bool CancelWithOrderPriceEdgeToTheoEnabled { get; set; }
        public double CancelWithOrderPriceEdgeToTheo { get; set; }
        public bool CancelWithOrderPriceEdgeToModelTheoEnabled { get; set; }
        public double CancelWithOrderPriceEdgeToModelTheo { get; set; }
        public bool CancelWithEdgeToTheoEnabled { get; set; }
        public double CancelWithAdjTheoEdge { get; set; }
        public bool CancelWithEdgeToAdjTheoEnabled { get; set; }
        public double CancelWithMidEdge { get; set; }
        public double CancelWithWidthThreshold { get; set; }
        public bool CancelWithEdgeToMidEnabled { get; set; }
        public bool CancelWithWidthEnabled { get; set; }
        public bool DerivedValuesEnabled { get; set; } = false;
        public bool DynamicUpdateEdgeOverrides { get; set; } = false;
        public bool EvaluateAdjustedEdgeOverrides { get; set; } = false;
        public double AdjustedEdgeOverrideCushionValue { get; set; }
        public bool DeltaCapEnabled { get; set; }
        public double DeltaCapUpperBound { get; set; }
        public double DeltaCapLowerBound { get; set; }
        public bool ModifyPxWithMktChange { get; set; }
        public bool StrikeCapEnabled { get; set; }
        public double StrikeCapUpperBound { get; set; }
        public double StrikeCapLowerBound { get; set; }
        public bool WidthCapEnabled { get; set; }
        public double WidthCapUpperBound { get; set; }
        public double WidthCapLowerBound { get; set; }
        public bool PermSelf { get; set; }
        public bool AvoidInvalidItems { get; set; } = true;
        public int CancelOnAmountOfFillsCount { get; set; }
        public bool MaxRestingOrdersEnabledV3 { get; set; }
        public int MaxRestingOrdersCount { get; set; } = 1;
        public bool DisableMultipleRestingSizeOrders { get; set; }
        public bool UseEdgeToTheo { get; set; }
        public bool UseEdgeToHistoricBest { get; set; }
        public bool UseEdgeToAdjTheo { get; set; }
        public bool UseLastFillAdjPx { get; set; }
        public bool UseEdgeToMid { get; set; }
        public bool UseEdgeToEma { get; set; }
        public bool UseEdgeToTheoAndMid { get; set; }
        public bool UseEdgeToTheoStopMid { get; set; }
        public bool UseEdgeToEmaStopMid { get; set; }
        public bool UseEdgeToMidStopEma { get; set; }
        public bool UseEdgeToBidPercentStopEma { get; set; }
        public bool UseEdgeToBidPercentStopEmaStopTheo { get; set; }
        public bool UseEdgeToEmaBidPercentStopEmaStopTheo { get; set; }
        public bool UseEdgeToDerivedBidPercentStopEmaStopMid { get; set; }
        public bool UseTheoBidPercent { get; set; }
        public bool UseBidPercent { get; set; }
        public bool UseEdgeToEmaBid { get; set; }
        public bool UseEdgeToBid { get; set; }
        public bool UseCustomFunctionEdge { get; set; }
        public bool UseDomStyleEdge { get; set; }
        public bool UseEdgeToAdjTheoWithOverride { get; set; }
        public bool UseBestOfEdge { get; set; }
        public bool BestOfAdjTheoEnabled { get; set; }
        public double BestOfAdjTheoEdge { get; set; }
        public TheoModel BestOfAdjTheoModel { get; set; }
        public bool BestOfHwTheoEnabled { get; set; }
        public double BestOfHwTheoEdge { get; set; }
        public bool BestOfV0TheoEnabled { get; set; }
        public double BestOfV0TheoEdge { get; set; }
        public bool BestOfMidEnabled { get; set; }
        public double BestOfMidEdge { get; set; }
        public bool BestOfEmaEnabled { get; set; }
        public double BestOfEmaEdge { get; set; }
        public bool BestOfBidPercentEnabled { get; set; }
        public double BestOfBidPercentEdge { get; set; }
        public bool BestOfDigBidPercentEnabled { get; set; }
        public double BestOfDigBidPercentEdge { get; set; }
        public bool ShowBestOfEdgeExpanded { get; set; } = true;
        public bool BestOfEdgeLocked { get; set; }
        public bool BestOfAdjTheoPinned { get; set; }
        public bool BestOfHwTheoPinned { get; set; }
        public bool BestOfV0TheoPinned { get; set; }
        public bool BestOfMidPinned { get; set; }
        public bool BestOfEmaPinned { get; set; }
        public bool BestOfBidPercentPinned { get; set; }
        public bool BestOfDigBidPercentPinned { get; set; }
        public double EdgeToTheo { get; set; }
        public double EdgeToHistoricBest { get; set; }
        public double EdgeToAdjTheo { get; set; }
        public EmaModel EmaModel { get; set; }
        public TheoModel TheoModel { get; set; }
        public TheoModel FishLossTheoModel { get; set; }
        public TheoModel AutoCancelTheoModel { get; set; }
        public string CustomFunctionEdgeFormula { get; set; }
        public ViewModels.DominatorConfigurationViewModel DominatorConfiguration { get; set; }
        public double LastFillAdjEdge { get; set; }
        public double EdgeToMid { get; set; }
        public double EdgeToEma { get; set; }
        public double EdgeToTheoAndMid { get; set; }
        public double EdgeToTheoStopMid { get; set; }
        public double EdgeToEmaStopMid { get; set; }
        public double EdgeToMidStopEma { get; set; }
        public double EdgeToBidPercentStopEma { get; set; }
        public double EdgeToBidPercentStopEmaStopTheo { get; set; }
        public double EdgeToEmaBidPercentStopEmaStopTheo { get; set; }
        public double TheoBidPercent { get; set; }
        public double BidPercent { get; set; }
        public double EdgeToEmaBid { get; set; }
        public double EdgeToBid { get; set; }
        public bool EdgeToAdjTheoWithOverrideUsePercentage { get; set; }
        public double EdgeToAdjTheoWithOverrideStatic { get; set; }
        public double EdgeToAdjTheoWithOverridePercent { get; set; } = 1;
        public int Count { get; set; }
        public PermSide PermSide { get; set; } = PermSide.All;
        public PermType PermType { get; set; } = PermType.Strike;
        public bool MaintainBaseStrategyOnPerm { get; set; }
        public bool ContraEnabled { get; set; }
        public bool AlsoOpenContraTicketEnabled { get; set; }
        public bool AutoSave { get; set; }
        public bool CancelWithUnderlyingPxEnabled { get; set; }
        public double CancelWithUnderlyingPx { get; set; }
        public bool CancelWithUnderlyingDeltaPxEnabled { get; set; }
        public double CancelWithUnderlyingDeltaPx { get; set; }
        public bool CancelWithTimerEnabled { get; set; } = true;
        public double CancelWithTimer { get; set; } = 1100;
        public bool CancelWithMaxSizeEnabled { get; set; }
        public int CancelWithMaxSizeLimit { get; set; } = 10;
        public bool SubmitWithDelayEnabled { get; set; }
        public bool OpenTicketForFills { get; set; }
        public bool OpenTicketForFailedClose { get; set; }
        public bool OpenTicketOnEdgeAcquired { get; set; }
        public int SubmitWithDelayIntervalMin { get; set; } = 250;
        public int SubmitWithDelayIntervalEnd { get; set; }
        public bool AutoClean { get; set; }
        public string BasketItems { get; set; }
        public bool ResubmitOnTimer { get; set; }
        public bool ActivateWindowOnResubmitFill { get; set; }
        public int ResubmitIntervalSec { get; set; } = 60;
        public int ResubmitIntervalCount { get; set; } = 99;
        public bool ModifyOnTimer { get; set; }
        public double ModifyIntervalSec { get; set; } = 60;
        public bool Randomize { get; set; }
        public bool Resume { get; set; }
        public bool DisablePriceRounding { get; set; }
        public bool StartProcessingFromSelectedRow { get; set; }
        public double LoopMinEdge { get; set; } = 0.1;
        public int LoopInterval { get; set; } = 0;
        public int LoopIntervalMax { get; set; } = 0;
        public bool MaintainLastEdge { get; set; }
        public int LoopResubmit { get; set; } = 1;
        public int AttemptResubmit { get; set; } = 0;
        public double LoopMaxLoss { get; set; } = 0.3;
        public double LoopMinEdgePercentage { get; set; } = 0.5;
        public bool LoopMinEdgeUsePercentage { get; set; }
        public bool LoopFreeLook { get; set; }
        public bool FreeLookRequireMinFillTime { get; set; }
        public double FreeLookMinFillTime { get; set; }
        public bool FreeLookOnLosers { get; set; }
        public int FreeLookOnLosersMax { get; set; }
        public bool FreeLookWhenGettingCloseEdge { get; set; }
        public bool LoopFreeLookOnAll { get; set; }
        public bool LoopFreeLookOnAllUsingTicks { get; set; }
        public double FreeLookOnAllIncrementTicks { get; set; } = 1;
        public double FreeLookOnAllWalkBackIncrementTicks { get; set; } = 1;
        public bool LoopFreeLookOnNickelNames { get; set; }
        public double LoopFreeLookOnNickelNamesIncrement { get; set; }
        public string LoopFreeLookOnNickelNamesRoute { get; set; }
        public bool LoopFreeLookOnDimeNames { get; set; }
        public double LoopFreeLookOnDimeNamesIncrement { get; set; }
        public string LoopFreeLookOnDimeNamesRoute { get; set; }
        public double FreeLookOnAllIncrement { get; set; } = .01;
        public double FreeLookOnAllWalkBackIncrement { get; set; } = .00;
        public int MaxLoopCount { get; set; } = 2;
        public int AutomationPartialResubmitCountV2 { get; set; }
        public double AutomationRequiredPartialFillPercentageV2 { get; set; }
        public LoopSizeupType LoopSizeupType { get; set; } = LoopSizeupType.Off;
        public bool AdjustClosingPriceToMarketV2 { get; set; } = true;
        public bool AdjustClosingPriceToMarketWinnersOnly { get; set; }
        public bool LooperDynamicRouting { get; set; }
        public bool AttemptIncrementUsingDynamicRoute { get; set; } = true;
        public bool EnableDynamicRouteForOpeningOrders { get; set; } = true;
        public bool EnableDynamicRouteForClosingOrders { get; set; } = true;
        public bool MinWidthFishLossVisible { get; set; } = true;
        public bool MaxWidthFishLossVisible { get; set; } = true;
        public bool TheoEdgeFishLossVisible { get; set; } = true;
        public bool HwTheoEdgeFishLossVisible { get; set; } = true;
        public bool V0TheoEdgeFishLossVisible { get; set; } = true;
        public bool MinTheoFishLossVisible { get; set; } = true;
        public bool MinEdgeFishLossVisible { get; set; } = true;
        public bool EmaEdgeFishLossVisible { get; set; } = true;
        public bool MktEdgeFishLossVisible { get; set; } = true;
        public bool SkewMktEdgeFishLossVisible { get; set; } = true;
        public bool SkewCrossEdgeFishLossVisible { get; set; } = true;
        public bool MinPercentBidFishLossVisible { get; set; } = true;
        public bool MaxPercentBidFishLossVisible { get; set; } = true;
        public bool MaxDigPercentBidFishLossVisible { get; set; } = true;
        public bool MinBidFishLossVisible { get; set; } = true;
        public bool MinBidAskSizeFishLossVisible { get; set; } = true;
        public bool WidthPercentE2TFishLossVisible { get; set; } = true;
        public bool FirmAttemptFishLossVisible { get; set; } = true;
        public bool FirmTradeFishLossVisible { get; set; } = true;
        public bool PermTimeFishLossVisible { get; set; } = true;
        public bool PermLoserFishLossVisible { get; set; } = true;
        public bool RecentAttemptFishLossVisible { get; set; } = true;
        public bool PxCrossMktFishLossVisible { get; set; } = true;

        public List<Tuple<string, string>> ExchToRouteMapV5 { get; set; } = new()
        {
#if DEBUG
            Tuple.Create("SLX", "CBOE"),
#endif
            // AMEX
            Tuple.Create("1", "AMEX"),
            Tuple.Create("AMXO", "AMEX"),
            Tuple.Create("XAMEX", "AMEX"),

            // ARCA
            Tuple.Create("ARCO", "ARCA"),
            Tuple.Create("ARCX", "ARCA"),
            Tuple.Create("N", "ARCA"),
            Tuple.Create("PCXO", "ARCA"),
            Tuple.Create("PSE", "ARCA"),

            // BATS
            Tuple.Create("84", "BATS"),
            Tuple.Create("BATO", "BATS"),

            // BOX
            Tuple.Create("B", "BOX"),
            Tuple.Create("XBOS", "BOX"),
            Tuple.Create("XBOX", "BOX"),

            // C2
            Tuple.Create("C2OX", "C2"),

            // CBOE
            Tuple.Create("W", "CBOE"),
            Tuple.Create("XCBO", "CBOE"),

            // EDGX
            Tuple.Create("EDGA", "EDGX"),
            Tuple.Create("EDGO", "EDGX"),

            // EMLD
            Tuple.Create("EMLD", "EMLD"),

            // GMNI
            Tuple.Create("GMNI", "GMNI"),

            // ISE
            Tuple.Create("XISX", "ISE"),
            Tuple.Create("Y", "ISE"),

            // MCRY
            Tuple.Create("MCRY", "MCRY"),

            // MEMX
            Tuple.Create("MXOP", "MEMX"),

            // MIAX
            Tuple.Create("XMIO", "MIAX"),

            // NASDAQ
            Tuple.Create("64", "NASDAQ"),
            Tuple.Create("XNAS", "NASDAQ"),
            Tuple.Create("XNDQ", "NASDAQ"),
            Tuple.Create("XPSX", "NASDAQ"),

            // NQBX
            Tuple.Create("XBXO", "NQBX"),

            // PEARL
            Tuple.Create("EPRL", "PEARL"),
            Tuple.Create("MPRL", "PEARL"),

            // PHLX
            Tuple.Create("X", "PHLX"),
            Tuple.Create("XPHO", "PHLX"),

            // SPHR
            Tuple.Create("SPHR", "SPHR")
        };

        public string LooperOpenRoute { get; set; }
        public string LooperCloseRoute { get; set; }
        public string LooperOpenRouteSize { get; set; }
        public string LooperCloseRouteSize { get; set; }
        public string StockTiedOrderRoute { get; set; }
        public string LooperOpenRouteSingleLeg { get; set; }
        public string LooperCloseRouteSingleLeg { get; set; }
        public string LooperOpenRouteSingleLegSize { get; set; }
        public string LooperCloseRouteSingleLegSize { get; set; }
        public bool UseSingleLegSeparateLooperRoutes { get; set; }
        public int LoopSizeupQty { get; set; } = 2;
        public int LoopCountBeforeSizeup { get; set; } = 2;
        public int ResubmitCount { get; set; } = 3;
        public LoopPricingMode LoopPricingMode { get; set; } = LoopPricingMode.PriceIncrement;
        public bool LoopPriceBackupEnabled { get; set; }

        public bool LockTraderAutoCloseEnabled { get; set; }
        public bool LockTraderResubmitOnFillEnabled { get; set; }
        public bool LockTraderResetQtyOnResubmit { get; set; } = true;
        public int LockTraderResubmitOnFillMaxCount { get; set; } = 1;

        public bool AutoAggressorEnabled { get; set; }
        public AutoAggressorMode AutoAggressorMode { get; set; }
        public AutoAggressorEdgeTightenMode AutoAggressorEdgeTightenMode { get; set; }
        public double AutoAggressorEdgeTightenPercentage { get; set; } = .10;

        public bool ScratchOnLowDeltaSize { get; set; } = false;
        public double ScratchOnLowDeltaMax { get; set; } = .05;
        public double ScratchOnLowDeltaMaxLoss { get; set; } = .00;
        public int ScratchOnLowDeltaMinSize { get; set; } = 5;
        public bool IcebergCloserEnabled { get; set; }
        public int IcebergDisplaySize { get; set; } = 1;
        public int IcebergTotalSize { get; set; } = 10;
        public int IcebergMaxResubmit { get; set; }
        public double LastEdgeTightenPercentV2 { get; set; }
        public int MaxBelowEdgeResubmitV2 { get; set; } = 0;
        public double DynamicEdgeExpansionV2 { get; set; } = 1;
        public double DynamicSizeExpansionV2 { get; set; } = 1;
        public bool? CloseOrderMode { get; set; } = null;
        public string MorphSymbolsQuery { get; set; }
        public PxCrossOption CrossOption { get; set; } = PxCrossOption.Ignore;
        public bool CancelOnClose { get; set; } = true;
        public bool QueueCancel { get; set; }
        public bool UseHedgeUnderlyingForAutoCancel { get; set; } = false;
        public bool HedgeAutoEnabled { get; set; } = false;
        public OrderType HedgeOrderType { get; set; } = OrderType.Limit;
        public double HedgeLimitEdge { get; set; } = .20;
        public double HedgeLimitIncrement { get; set; } = .05;
        public int HedgeAttempt { get; set; } = 2;
        public int HedgeInterval { get; set; } = 1000;
        public bool HedgeOnFailedClose { get; set; } = true;
        public bool HedgeWithEdge { get; set; } = false;
        public double HedgeMinEdge { get; set; } = .05;
        public bool MarketMakerEnabled { get; set; } = false;
        public bool MarketMakerUseBidEmaOnly { get; set; } = false;
        public bool MarketMakerUseAskEmaOnly { get; set; } = false;
        public bool MarketMakerSubmitBuysEnabled { get; set; } = true;
        public bool MarketMakerSubmitSellsEnabled { get; set; }
        public bool MarketMakerBuildClosingSpreadsAutomatically { get; set; }
        public bool MarketMakerBuildClosingSpreads { get; set; } = false;
        public double MarketMakerEdgeToBid { get; set; } = 0.2;
        public double MarketMakerEdgeToAsk { get; set; } = 0.2;
        public MarketMakerOffsetType MarketMakerOffsetType { get; set; } = MarketMakerOffsetType.PxOffset;
        public double MarketMakerPosOffset { get; set; } = 0.02;
        public double MarketMakerPosPercentageOffset { get; set; } = 0.2;
        public string MarketMakerBuyRoute { get; set; }
        public string MarketMakerSellRoute { get; set; }
        public EmaType SavedEmaType { get; set; } = EmaType.Off;
        public double EmaPercentVegaThreshold { get; set; } = 0.1;
        public double EmaSmoothing { get; set; } = 2.0;
        public double EmaPeriods { get; set; } = 5000;
        public double EmaInterval { get; set; } = 20;
        public double MaxBidDeviation { get; set; } = 0.03;
        public double MaxAskDeviation { get; set; } = 0.03;
        public bool LeaveAutoCloseResting { get; set; }
        public double MarketMakerChangePriceWithUnderlyingChangeThreshold { get; set; } = 0.1;
        public double MarketMakerMinBidDistance { get; set; } = 0.0;
        public double MarketMakerMinAskDistance { get; set; } = 0.0;
        public bool SubscribeToMarketData { get; set; } = true;
        public bool SubscribeToHanweck { get; set; } = true;
        public bool SubscribeToDerivatives { get; set; } = true;
        public bool SubscribeToUnderlying { get; set; } = true;
        public bool SubscribeToHedgeUnderlying { get; set; } = false;
        public bool SubscribeToGlobalEdgeToTheo { get; set; } = false;
        public bool SubscribeToFirmSummary { get; set; } = false;
        public bool RequestBestEdge { get; set; } = false;
        public int RequestBestEdgeDays { get; set; } = 10;
        public bool SubscribeToEma { get; set; } = false;
        public bool ResetVolumeChange { get; set; } = true;
        public double BuyEdge { get; set; }
        public double SellEdge { get; set; }
        public bool MaxWidthCheckEnabled { get; set; }
        public double MaxWidthCheckPx { get; set; }
        public bool MinTheoEdgeCheckEnabled { get; set; }
        public bool MinHwTheoEdgeCheckEnabled { get; set; }
        public bool MinV0TheoEdgeCheckEnabled { get; set; }
        public bool MinEdgeToMarketCheckEnabled { get; set; }
        public bool MinEdgeToSkewMarketCheckEnabled { get; set; }
        public bool IgnoreSkewMktCheckIfBothSidesFail { get; set; }
        public bool AdjustAfterMinEdgeToSkewMarketCheck { get; set; }
        public double MinEdgeToSkewMarketCheckEdge { get; set; }
        public bool MinEdgeToSkewMarketCrossCheckEnabled { get; set; }
        public double MinEdgeToSkewMarketCrossCheckEdge { get; set; }
        public bool AdjustAfterMinEdgeToSkewMarketCrossCheck { get; set; }
        public bool BlockZeroPrice { get; set; }
        public bool BlockSubmissionOnTheoJump { get; set; }
        public bool MaxPercentBidCheckUseBestQuote { get; set; }
        public bool MaxPercentBidCheckEnabled { get; set; }
        public bool MinPercentBidCheckEnabled { get; set; }
        public double MinTheoEdgeCheckEdge { get; set; }
        public double MinHwTheoEdgeCheckEdge { get; set; }
        public double MinV0TheoEdgeCheckEdge { get; set; }
        public bool MinBidCheckEnabled { get; set; }
        public double MinBidCheckBidValue { get; set; }
        public bool MinTheoCheckEnabled { get; set; }
        public double MinTheoCheckTheoValue { get; set; }
        public bool MinBidAskSizeCheckEnabled { get; set; }
        public int MinBidAskSize { get; set; }
        public bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; set; }
        public double MinEmaWidthPercentEdgeToTheoCheckEdge { get; set; }
        public bool PreviousAttemptCrossCheckEnabled { get; set; }
        public bool MinEdgeToPreviousAttemptCheckEnabled { get; set; }
        public bool MinTimeToPreviousAttemptCheckEnabled { get; set; }
        public double MinTimeToPreviousAttemptIntervalSeconds { get; set; } = 600;
        public bool MinTimeToPermLoserCheckEnabled { get; set; }
        public double MinTimeToPermLoserIntervalSeconds { get; set; } = 900;
        public double MinEdgeToMarketCheckEdge { get; set; }
        public double MaxPercentBidCheckEdge { get; set; }
        public bool MaxDigPercentBidCheckEnabled { get; set; }
        public double MaxDigPercentBidCheckEdge { get; set; }
        public double MinPercentBidCheckEdge { get; set; }
        public bool MinMidEdgeCheckEnabled { get; set; }
        public double MinMidEdgeCheckEdge { get; set; }
        public bool MinEmaEdgeCheckEnabled { get; set; }
        public double MinEmaEdgeCheckEdge { get; set; }
        public bool SubscribeToImplied { get; set; }
        public bool SubscribeToInterpolatedValues { get; set; }
        public bool SubscribeToDerivedValues { get; set; }
        public int SizeupConfigId { get; set; }
        public int NagbotIntervalModelConfigId { get; set; }
        public int DynamicEdgeConfigId { get; set; }
        public int DynamicIntervalModelId { get; set; }
        public int AutoPermConfigModelId { get; set; }
        public int LoopIncrementConfigModelId { get; set; }
        public List<BasketLoopBlockListModel> BasketLoopBlockModels { get; set; }
        public BasketLoopBlockListModel BasketLoopBlockList { get; set; }
        public List<KeyValuePair<UnderlyingLookupKey, string>> UnderlyingMappingConfigs { get; set; }
        public bool AutoHedgeOnClose { get; set; }
        public bool AutoHedgeOnCloseSizeOnly { get; set; }
        public double MinHedgeHouseEdge { get; set; } = .10;
        public bool AutoHedgeOnFailure { get; set; }
        public bool AutoHedgePartial { get; set; }

        public Venue AutoTraderVenue { get; set; } = Venue.TB;
        public double NagBotEdge { get; set; }
        public bool NagbotEnabled { get; set; }
        public bool NagbotMaintainEdge { get; set; }
        public double NagbotMaxChangeInUnderlying { get; set; }
        public double NagbotMaxChangeInVolume { get; set; } = 10;
        public double NagbotMinEdgeForSize { get; set; }
        public double NagbotMinEdge { get; set; }
        public bool NagbotMinEdgeForSizeEnabled { get; set; }
        public bool NagbotMinEdgeEnabled { get; set; }

        public double EdgeMultiplier { get; set; } = 1;
        public double MaxLossMultiplier { get; set; } = 1;

        public bool WidthNotificationEnabled { get; set; }
        public bool MinChangeToEmaNotificationEnabled { get; set; }
        public bool PercentChangeInEmaNotificationEnabled { get; set; }
        public bool MaxPercentChangeInUnderlyingEmaEnabled { get; set; }
        public bool NotificationEnabled { get; set; } = true;
        public bool LoggingEnabled { get; set; } = false;
        public double MinEdgeForLogging { get; set; } = .10;
        public int LoggingTimespan { get; set; } = 300_000;
        public double WidthNotificationTrigger { get; set; }
        public double MaxPercentChangeInUnderlyingEma { get; set; }
        public double PercentChangeInEmaNotificationTrigger { get; set; }
        public double MinChangeToEmaNotificationEnabledTrigger { get; set; }
        public bool ActivateWindowOnNotificationEnabled { get; set; }
        public bool SubmitOnTriggerEnabled { get; set; }
        public bool CancelOnLoss { get; set; }
        public bool DisableSubmitOnWidthTriggerOnLoss { get; set; } = true;
        public bool ShowTheoToMidIndicator { get; set; }
        public bool SubmitOnWidthTriggerMaxOpenEnabled { get; set; } = true;
        public bool NotifyOnTheoToMarketSpreadWideningFromEmaEnabled { get; set; }
        public double MinPercentChangeOnTheoToMarketSpreadWideningFromEma { get; set; } = .01;
        public bool StockTiedEnabled { get; set; } = false;
        public bool StockTiedDeltaNeutral { get; set; } = true;
        public bool CheapoEnabled { get; set; }
        public int CheaposGeneratedPerOrder { get; set; } = 1;
        public double CheapoLegMaxWidth { get; set; } = .03;
        public int CheapoDteRangeMin { get; set; }
        public int CheapoDteRangeMax { get; set; }
        public double CheapoDeltaRangeMin { get; set; } = 0;
        public double CheapoDeltaRangeMax { get; set; } = 1;
        public double CheapoMarketRangeMin { get; set; } = 0;
        public double CheapoMarketRangeMax { get; set; } = .05;
        public bool AlertWhenGettingNoFill { get; set; }
        public int AlertWhenGettingNoFillCount { get; set; } = 50;
        public int SubmitOnWidthTriggerMaxOpenPos { get; set; } = 1;
        public List<DynamicIncrementConfigModel> DynamicIncrementConfigs { get; set; }
        public List<AutoPermConfigModel> AutoPermConfigs { get; set; }
        public bool CheckForRecentAttempt { get; set; }
        public double CheckForRecentAttemptTimespan { get; set; }
        public bool CheckForRecentFill { get; set; }
        public double CheckForRecentFillTimespan { get; set; }
        public bool InitQtyEnabled { get; set; }
        public int InitQty { get; set; } = 1;
        public bool UseMatrixAlgo { get; set; }
        public MatrixStrategy MatrixStrategy { get; set; }
        public bool MinStrikeSortingEnabled { get; set; }
        public double AskPriceNotificationTrigger { get; set; } = 3.00;
        public bool AskPriceNotificationEnabled { get; set; }
    }
}
