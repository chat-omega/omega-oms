using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketSettings : BindableBase, IEmaConfig, IAutoTraderSettings, IEdgeScanFeedTraderSettings
    {
        public double MaxCancelDelay { get; set; } = 100_000_000;

        public event ResetEmaEventHandler ResetEmaEvent;

        private double _minTimeToPreviousAttemptInterval;
        private double _cancelWithTimer;
        private bool _adjustPriceBeforeSubmit = true;
        private bool _bestOfAdjTheoEnabled;
        private bool _bestOfHwTheoEnabled;
        private bool _bestOfV0TheoEnabled;
        private bool _bestOfMidEnabled;
        private bool _bestOfEmaEnabled;
        private bool _bestOfBidPercentEnabled;
        private bool _bestOfDigBidPercentEnabled;

        public IEnumerable<OrderType> HedgeOrderTypes { get; } = ((OrderType[])Enum.GetValues(typeof(OrderType))).ToList();
        public Dictionary<string, DerivedValueSettings> SymbolToDerivativeSettingsMap { get; }
        public int RequestBestEdgeDays { get; set; }
        public int NagbotIntervalModelConfigId { get; set; }
        public bool EmaEnabled
        {
            get => SubscribeToEma;
            set => SubscribeToEma = value;
        }

        [Bindable]
        public partial string Uid { get; set; }
        [Bindable]
        public partial NagbotIntervalModel NagbotIntervalModel { get; set; }
        [Bindable]
        public partial bool SubscribeToMarketData { get; set; }
        [Bindable]
        public partial bool SubscribeToHanweck { get; set; }
        [Bindable]
        public partial bool SubscribeToDerivatives { get; set; }
        [Bindable]
        public partial bool SubscribeToUnderlying { get; set; }
        [Bindable]
        public partial bool SubscribeToHedgeUnderlying { get; set; }
        [Bindable]
        public partial bool SubscribeToGlobalEdgeToTheo { get; set; }
        [Bindable]
        public partial bool SubscribeToFirmSummary { get; set; }
        [Bindable]
        public partial bool RequestBestEdge { get; set; }
        [Bindable]
        public partial bool SubscribeToDerivedValues { get; set; }
        [Bindable]
        public partial bool SubscribeToInterpolated { get; set; }
        [Bindable]
        public partial bool SubscribeToImplied { get; set; }
        [Bindable]
        public partial bool SubscribeToEma { get; set; }
        [Bindable]
        public partial bool RiskCheckEnabled { get; set; }
        [Bindable]
        public partial bool ActiveUncheckEnabled { get; set; }
        [Bindable]
        public partial string EdgeType { get; set; }
        [Bindable]
        public partial bool UseEdgeToTheo { get; set; }
        [Bindable]
        public partial bool UseEdgeToHistoricBest { get; set; }
        [Bindable]
        public partial bool UseEdgeToAdjTheoWithOverride { get; set; }
        [Bindable]
        public partial bool UseCustomFunctionEdge { get; set; }
        [Bindable]
        public partial bool UseTheoToMarketSpreadPx { get; set; }
        [Bindable]
        public partial bool UseBestOfEdge { get; set; }
        public int BestOfEnabledCount =>
            (BestOfAdjTheoEnabled ? 1 : 0) +
            (BestOfHwTheoEnabled ? 1 : 0) +
            (BestOfV0TheoEnabled ? 1 : 0) +
            (BestOfMidEnabled ? 1 : 0) +
            (BestOfEmaEnabled ? 1 : 0) +
            (BestOfBidPercentEnabled ? 1 : 0) +
            (BestOfDigBidPercentEnabled ? 1 : 0);

        public bool BestOfAdjTheoEnabled
        {
            get => _bestOfAdjTheoEnabled;
            set { SetValue(ref _bestOfAdjTheoEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfAdjTheoEdge { get; set; }
        [Bindable]
        public partial TheoModel BestOfAdjTheoModel { get; set; }
        public bool BestOfHwTheoEnabled
        {
            get => _bestOfHwTheoEnabled;
            set { SetValue(ref _bestOfHwTheoEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfHwTheoEdge { get; set; }
        public bool BestOfV0TheoEnabled
        {
            get => _bestOfV0TheoEnabled;
            set { SetValue(ref _bestOfV0TheoEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfV0TheoEdge { get; set; }
        public bool BestOfMidEnabled
        {
            get => _bestOfMidEnabled;
            set { SetValue(ref _bestOfMidEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfMidEdge { get; set; }
        public bool BestOfEmaEnabled
        {
            get => _bestOfEmaEnabled;
            set { SetValue(ref _bestOfEmaEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfEmaEdge { get; set; }
        public bool BestOfBidPercentEnabled
        {
            get => _bestOfBidPercentEnabled;
            set { SetValue(ref _bestOfBidPercentEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfBidPercentEdge { get; set; }
        public bool BestOfDigBidPercentEnabled
        {
            get => _bestOfDigBidPercentEnabled;
            set { SetValue(ref _bestOfDigBidPercentEnabled, value); RaisePropertyChanged(nameof(BestOfEnabledCount)); }
        }
        [Bindable]
        public partial double BestOfDigBidPercentEdge { get; set; }
        [Bindable]
        public partial bool BestOfAdjTheoPinned { get; set; }
        [Bindable]
        public partial bool BestOfHwTheoPinned { get; set; }
        [Bindable]
        public partial bool BestOfV0TheoPinned { get; set; }
        [Bindable]
        public partial bool BestOfMidPinned { get; set; }
        [Bindable]
        public partial bool BestOfEmaPinned { get; set; }
        [Bindable]
        public partial bool BestOfBidPercentPinned { get; set; }
        [Bindable]
        public partial bool BestOfDigBidPercentPinned { get; set; }
        [Bindable]
        public partial string CustomFunctionEdgeFormula { get; set; }
        [Bindable]
        public partial bool UseDomStyleEdge { get; set; }
        [Bindable]
        public partial ViewModels.DominatorConfigurationViewModel DominatorConfiguration { get; set; }
        [Bindable]
        public partial bool UseEdgeToAdjTheo { get; set; }
        [Bindable]
        public partial EmaModel EmaModel { get; set; }
        [Bindable]
        public partial TheoModel TheoModel { get; set; }
        [Bindable]
        public partial TheoModel FishLossTheoModel { get; set; }
        [Bindable]
        public partial TheoModel AutoCancelTheoModel { get; set; }
        [Bindable]
        public partial bool UseLastFillAdjPx { get; set; }
        [Bindable]
        public partial bool UseEdgeToMid { get; set; }
        [Bindable]
        public partial bool UseEdgeToEma { get; set; }
        [Bindable]
        public partial bool UseEdgeToTheoAndMid { get; set; }
        [Bindable]
        public partial bool UseEdgeToTheoStopMid { get; set; }
        [Bindable]
        public partial bool UseEdgeToEmaStopMid { get; set; }
        [Bindable]
        public partial bool UseEdgeToMidStopEma { get; set; }
        [Bindable]
        public partial bool UseEdgeToBidPercentStopEma { get; set; }
        [Bindable]
        public partial bool UseEdgeToBidPercentStopEmaStopTheo { get; set; }
        [Bindable]
        public partial bool UseEdgeToEmaBidPercentStopEmaStopTheo { get; set; }
        [Bindable]
        public partial bool UseEdgeToDerivedBidPercentStopEmaStopMid { get; set; }
        [Bindable]
        public partial bool UseBidPercent { get; set; }
        [Bindable]
        public partial bool UseTheoBidPercent { get; set; }
        [Bindable]
        public partial bool UseEdgeToEmaBid { get; set; }
        [Bindable]
        public partial bool UseEdgeToBid { get; set; }
        [Bindable]
        public partial bool UsePermAdjPx { get; set; }
        [Bindable]
        public partial double BuyEdge { get; set; }
        [Bindable]
        public partial double SellEdge { get; set; }
        [Bindable]
        public partial double EdgeToTheo { get; set; }
        [Bindable]
        public partial double EdgeToHistoricBest { get; set; }
        [Bindable]
        public partial bool EdgeToAdjTheoWithOverrideUsePercentage { get; set; }
        [Bindable]
        public partial double EdgeToAdjTheoWithOverrideStatic { get; set; }
        [Bindable]
        public partial double EdgeToAdjTheoWithOverridePercent { get; set; }
        [Bindable]
        public partial double EdgeToAdjTheo { get; set; }
        [Bindable]
        public partial double EdgeToTheoToMarketSpread { get; set; }
        [Bindable]
        public partial double LastFillAdjEdge { get; set; }
        [Bindable]
        public partial double EdgeToMid { get; set; }
        [Bindable]
        public partial double EdgeToEma { get; set; }
        [Bindable]
        public partial double EdgeToTheoAndMid { get; set; }
        [Bindable]
        public partial double EdgeToTheoStopMid { get; set; }
        [Bindable]
        public partial double EdgeToEmaStopMid { get; set; }
        [Bindable]
        public partial double EdgeToMidStopEma { get; set; }
        [Bindable]
        public partial double EdgeToBidPercentStopEma { get; set; }
        [Bindable]
        public partial double EdgeToBidPercentStopEmaStopTheo { get; set; }
        [Bindable]
        public partial double EdgeToEmaBidPercentStopEmaStopTheo { get; set; }
        [Bindable]
        public partial double EdgeToDerivedBidPercentStopEmaStopMid { get; set; }
        [Bindable]
        public partial double BidPercent { get; set; }
        [Bindable]
        public partial double TheoBidPercent { get; set; }
        [Bindable]
        public partial double EdgeToEmaBid { get; set; }
        [Bindable]
        public partial double EdgeToBid { get; set; }
        [Bindable]
        public partial double PermAdjEdge { get; set; }
        [Bindable]
        public partial bool FishModeEnabled { get; set; }
        [Bindable]
        public partial AutomationConfigModel AutomationConfig { get; set; }
        [Bindable]
        public partial LegInSettings LegInSettings { get; set; }
        [Bindable]
        public partial bool LegInEnabled { get; set; }
        [Bindable]
        public partial bool CancelWithMaxSizeEnabled { get; set; }
        [Bindable]
        public partial int CancelWithMaxSizeLimit { get; set; }
        [Bindable]
        public partial bool CancelWithEdgeToTheoEnabled { get; set; }
        [Bindable]
        public partial bool CancelWithEdgeToAdjTheoEnabled { get; set; }
        [Bindable]
        public partial double CancelWithTheoEdge { get; set; }
        [Bindable]
        public partial double CancelWithAdjTheoEdge { get; set; }
        [Bindable]
        public partial bool CancelWithOrderPriceEdgeToTheoEnabled { get; set; }
        [Bindable]
        public partial double CancelWithOrderPriceEdgeToTheo { get; set; }
        [Bindable]
        public partial bool CancelWithOrderPriceEdgeToModelTheoEnabled { get; set; }
        [Bindable]
        public partial double CancelWithOrderPriceEdgeToModelTheo { get; set; }
        [Bindable]
        public partial bool CancelWithEdgeToMidEnabled { get; set; }
        [Bindable]
        public partial double CancelWithMidEdge { get; set; }
        [Bindable]
        public partial bool MaxWidthCheckEnabled { get; set; }
        [Bindable]
        public partial double MaxWidthCheckPx { get; set; }
        [Bindable]
        public partial bool MinTheoEdgeCheckEnabled { get; set; }
        [Bindable]
        public partial double MinTheoEdgeCheckEdge { get; set; }
        [Bindable]
        public partial bool MinHwTheoEdgeCheckEnabled { get; set; }
        [Bindable]
        public partial double MinHwTheoEdgeCheckEdge { get; set; }
        [Bindable]
        public partial bool MinV0TheoEdgeCheckEnabled { get; set; }
        [Bindable]
        public partial double MinV0TheoEdgeCheckEdge { get; set; }
        [Bindable]
        public partial double MinEdgeToMarketCheckEdge { get; set; }
        [Bindable]
        public partial double MinEdgeToSkewMarketCheckEdge { get; set; }
        [Bindable]
        public partial double MinEdgeToSkewMarketCrossCheckEdge { get; set; }
        [Bindable]
        public partial bool MinBidCheckEnabled { get; set; }
        [Bindable]
        public partial double MinBidCheckBidValue { get; set; }
        [Bindable]
        public partial bool MinTheoCheckEnabled { get; set; }
        [Bindable]
        public partial double MinTheoCheckTheoValue { get; set; }
        [Bindable]
        public partial bool MinPercentBidCheckEnabled { get; set; }
        [Bindable]
        public partial bool MaxPercentBidCheckEnabled { get; set; }
        [Bindable]
        public partial bool MinBidAskSizeCheckEnabled { get; set; }
        [Bindable]
        public partial int MinBidAskSize { get; set; }
        [Bindable]
        public partial bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; set; }
        [Bindable]
        public partial double MinEmaWidthPercentEdgeToTheoCheckEdge { get; set; }
        [Bindable]
        public partial bool MaxPercentBidCheckUseBestQuote { get; set; }
        [Bindable]
        public partial bool BlockZeroPrice { get; set; }
        [Bindable]
        public partial bool BlockSubmissionOnTheoJump { get; set; }
        [Bindable]
        public partial double MinPercentBidCheckEdge { get; set; }
        [Bindable]
        public partial double MaxPercentBidCheckEdge { get; set; }
        [Bindable]
        public partial bool MaxDigPercentBidCheckEnabled { get; set; }
        [Bindable]
        public partial double MaxDigPercentBidCheckEdge { get; set; }
        [Bindable]
        public partial bool MinEmaEdgeCheckEnabled { get; set; }
        [Bindable]
        public partial bool MinEdgeToMarketCheckEnabled { get; set; }
        [Bindable]
        public partial bool AdjustAfterMinEdgeToSkewMarketCheck { get; set; }
        [Bindable]
        public partial bool IgnoreSkewMktCheckIfBothSidesFail { get; set; }
        [Bindable]
        public partial bool MinEdgeToSkewMarketCheckEnabled { get; set; }
        [Bindable]
        public partial bool AdjustAfterMinEdgeToSkewMarketCrossCheck { get; set; }
        [Bindable]
        public partial bool MinEdgeToSkewMarketCrossCheckEnabled { get; set; }
        [Bindable]
        public partial bool MinEdgeToPreviousAttemptCheckEnabled { get; set; }
        [Bindable]
        public partial bool PreviousAttemptCrossCheckEnabled { get; set; }
        [Bindable]
        public partial bool MinTimeToPreviousAttemptCheckEnabled { get; set; }
        public double MinTimeToPreviousAttemptIntervalSeconds
        {
            get => _minTimeToPreviousAttemptInterval;
            set => SetValue(ref _minTimeToPreviousAttemptInterval, value);
        }
        [Bindable]
        public partial bool MinTimeToPermLoserCheckEnabled { get; set; }
        [Bindable]
        public partial double MinTimeToPermLoserIntervalSeconds { get; set; }
        [Bindable]
        public partial double MinEmaEdgeCheckEdge { get; set; }
        [Bindable]
        public partial bool MinMidEdgeCheckEnabled { get; set; }
        [Bindable]
        public partial double MinMidEdgeCheckEdge { get; set; }
        [Bindable]
        public partial bool CancelWithWidthEnabled { get; set; }
        [Bindable]
        public partial double CancelWithWidthThreshold { get; set; }
        [Bindable]
        public partial bool CancelWithUnderlyingPxEnabled { get; set; }
        [Bindable]
        public partial double CancelWithUnderlyingPx { get; set; }
        [Bindable]
        public partial bool CancelWithUnderlyingDeltaPxEnabled { get; set; }
        [Bindable]
        public partial double CancelWithUnderlyingDeltaPx { get; set; }
        [Bindable]
        public partial bool CancelWithTimerEnabled { get; set; }
        public double CancelWithTimer
        {
            get => _cancelWithTimer;
            set => SetValue(ref _cancelWithTimer, Math.Min(value, MaxCancelDelay));
        }

        [Bindable]
        public partial bool EvaluateAdjustedEdgeOverrides { get; set; }
        [Bindable]
        public partial double AdjustedEdgeOverrideCushionValue { get; set; }
        [Bindable]
        public partial bool ModifyPxWithMktChange { get; set; }
        [Bindable]
        public partial bool DynamicUpdateEdgeOverrides { get; set; }
        [Bindable]
        public partial bool DeltaCapEnabled { get; set; }
        [Bindable]
        public partial double DeltaCapUpperBound { get; set; }
        [Bindable]
        public partial double DeltaCapLowerBound { get; set; }
        [Bindable]
        public partial bool StrikeCapEnabled { get; set; }
        [Bindable]
        public partial double StrikeCapUpperBound { get; set; }
        [Bindable]
        public partial double StrikeCapLowerBound { get; set; }
        [Bindable]
        public partial bool WidthCapEnabled { get; set; }
        [Bindable]
        public partial double WidthCapUpperBound { get; set; }
        [Bindable]
        public partial double WidthCapLowerBound { get; set; }
        [Bindable]
        public partial bool SubmitWithDelayEnabled { get; set; }
        [Bindable]
        public partial bool Randomize { get; set; }
        [Bindable]
        public partial bool Resume { get; set; }
        [Bindable]
        public partial bool DisablePriceRounding { get; set; }
        public bool AdjustPriceBeforeSubmit
        {
            get
            {
                if (!OmsCore.Config.AllowUsingOfStaticPriceInBaskets)
                {
                    return true;
                }
                return _adjustPriceBeforeSubmit;
            }
            set => SetValue(ref _adjustPriceBeforeSubmit, value);
        }

        [Bindable]
        public partial bool StartProcessingFromSelectedRow { get; set; }
        [Bindable(Default = PxCrossOption.Ignore)]
        public partial PxCrossOption PxCrossOption { get; set; }
        [Bindable]
        public partial bool OpenTicketForFills { get; set; }
        [Bindable]
        public partial bool OpenTicketForFailedClose { get; set; }
        [Bindable]
        public partial bool OpenTicketOnEdgeAcquired { get; set; }
        [Bindable]
        public partial bool ResubmitAfterCancel { get; set; }
        [Bindable]
        public partial bool CancelOnClose { get; set; }
        [Bindable]
        public partial bool QueueCancel { get; set; }
        [Bindable]
        public partial bool UseHedgeUnderlyingForAutoCancel { get; set; }
        [Bindable]
        public partial int CancelOnAmountOfFillsCount { get; set; }
        public bool MaxRestingOrdersEnabledLastState { get; set; }
        [Bindable]
        public partial bool MaxRestingOrdersEnabled { get; set; }
        [Bindable]
        public partial bool MaxRestingSet { get; set; }
        public int MaxRestingOrdersCountLastState { get; set; }
        [Bindable]
        public partial int MaxRestingOrdersCount { get; set; }
        [Bindable]
        public partial bool DisableMultipleRestingSizeOrders { get; set; }
        [Bindable]
        public partial bool DerivedValuesEnabled { get; set; }
        [Bindable]
        public partial DerivedValueSettings SpxVsSpy { get; set; }
        [Bindable]
        public partial DerivedValueSettings NdxVsQqq { get; set; }
        [Bindable]
        public partial DerivedValueSettings RutVsIwm { get; set; }
        [Bindable]
        public partial bool HedgeAutoEnabled { get; set; }
        [Bindable]
        public partial OrderType HedgeOrderType { get; set; }
        [Bindable]
        public partial double HedgeLimitEdge { get; set; }
        [Bindable]
        public partial double HedgeLimitIncrement { get; set; }
        [Bindable]
        public partial int HedgeAttempt { get; set; }
        [Bindable]
        public partial bool HedgeOnFailedClose { get; set; }
        [Bindable]
        public partial bool HedgeWithEdge { get; set; }
        [Bindable]
        public partial double HedgeMinEdge { get; set; }
        [Bindable]
        public partial int HedgeInterval { get; set; }
        [Bindable]
        public partial EmaType SelectedEmaType { get; set; }
        [Bindable]
        public partial double PercentVegaThreshold { get; set; }
        [Bindable]
        public partial double EmaSmoothing { get; set; }
        [Bindable]
        public partial double EmaInterval { get; set; }
        [Bindable]
        public partial double EmaPeriods { get; set; }
        [Bindable]
        public partial double MaxBidDeviation { get; set; }
        [Bindable]
        public partial double MaxAskDeviation { get; set; }
        [Bindable]
        public partial int SubmitWithDelayInterval { get; set; }
        [Bindable]
        public partial int SubmitWithDelayIntervalEnd { get; set; }
        [Bindable]
        public partial int NetPos { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial bool NagbotMaintainEdge { get; set; }
        [Bindable]
        public partial double NagbotMaxChangeInUnderlying { get; set; }
        [Bindable]
        public partial double NagBotEdge { get; set; }
        [Bindable]
        public partial double NagbotMaxChangeInVolume { get; set; }
        [Bindable]
        public partial double NagbotMinEdgeForSize { get; set; }
        [Bindable]
        public partial double NagbotMinEdge { get; set; }
        [Bindable]
        public partial bool NagbotMinEdgeForSizeEnabled { get; set; }
        [Bindable]
        public partial bool NagbotMinEdgeEnabled { get; set; }
        [Bindable]
        public partial bool WidthNotificationEnabled { get; set; }
        [Bindable]
        public partial bool MinChangeToEmaNotificationEnabled { get; set; }
        [Bindable]
        public partial bool PercentChangeInEmaNotificationEnabled { get; set; }
        [Bindable]
        public partial bool MaxPercentChangeInUnderlyingEmaEnabled { get; set; }
        [Bindable]
        public partial int TotalWidthTriggered { get; set; }
        [Bindable]
        public partial bool ShowTheoToMidIndicator { get; set; }
        [Bindable]
        public partial bool NotificationEnabled { get; set; }
        [Bindable]
        public partial bool LoggingEnabled { get; set; }
        [Bindable]
        public partial double MinEdgeForLogging { get; set; }
        [Bindable]
        public partial int LoggingTimespan { get; set; }
        [Bindable]
        public partial bool AlertWhenGettingCloseEdge { get; set; }
        [Bindable]
        public partial bool AlertWhenGettingNoFill { get; set; }
        [Bindable]
        public partial int AlertWhenGettingNoFillCount { get; set; }
        [Bindable]
        public partial bool ActivateWindowOnNotificationEnabled { get; set; }
        [Bindable]
        public partial bool SubmitOnTriggerEnabled { get; set; }
        [Bindable]
        public partial bool CancelOnLoss { get; set; }
        [Bindable(Default = true)]
        public partial bool DisableSubmitOnTriggerOnLoss { get; set; }
        [Bindable(Default = true)]
        public partial bool SubmitOnTriggerMaxOpenEnabled { get; set; }
        [Bindable(Default = 1)]
        public partial int SubmitOnTriggerMaxOpenPos { get; set; }
        [Bindable]
        public partial bool AskPriceNotificationEnabled { get; set; }
        [Bindable]
        public partial double AskPriceNotificationTrigger { get; set; }
        [Bindable]
        public partial double WidthNotificationTrigger { get; set; }
        [Bindable]
        public partial bool NotifyOnTheoToMarketSpreadWideningFromEmaEnabled { get; set; }
        [Bindable]
        public partial double MinPercentChangeOnTheoToMarketSpreadWideningFromEma { get; set; }
        [Bindable]
        public partial double MinChangeToEmaNotificationEnabledTrigger { get; set; }
        [Bindable]
        public partial double PercentChangeInEmaNotificationTrigger { get; set; }
        [Bindable]
        public partial double MaxPercentChangeInUnderlyingEma { get; set; }
        [Bindable]
        public partial DataType DataType { get; set; }
        [Bindable]
        public partial bool LockTheos { get; set; }
        [Bindable]
        public partial BasketLoopBlockListModel BasketLoopBlockList { get; set; }
        [Bindable]
        public partial bool StockTiedEnabled { get; set; }
        [Bindable]
        public partial bool StockTiedDeltaNeutral { get; set; }
        [Bindable]
        public partial bool CheapoEnabled { get; set; }
        [Bindable]
        public partial double CheapoLegMaxWidth { get; set; }
        [Bindable]
        public partial int CheaposGeneratedPerOrder { get; set; }
        [Bindable]
        public partial int CheapoDteRangeMin { get; set; }
        [Bindable]
        public partial int CheapoDteRangeMax { get; set; }
        [Bindable]
        public partial double CheapoDeltaRangeMin { get; set; }
        [Bindable]
        public partial double CheapoDeltaRangeMax { get; set; }
        [Bindable]
        public partial double CheapoMarketRangeMin { get; set; }
        [Bindable]
        public partial double CheapoMarketRangeMax { get; set; }
        [Bindable]
        public partial bool CheckForRecentAttempt { get; set; }
        [Bindable]
        public partial double CheckForRecentAttemptTimespan { get; set; }
        [Bindable]
        public partial bool CheckForRecentFill { get; set; }
        [Bindable]
        public partial double CheckForRecentFillTimespan { get; set; }
        [Bindable]
        public partial bool InitQtyEnabled { get; set; }
        [Bindable]
        public partial int InitQty { get; set; }
        [Bindable]
        public partial bool UseMatrixAlgo { get; set; }
        [Bindable]
        public partial MatrixStrategy MatrixStrategy { get; set; }
        [Bindable]
        public partial int MatrixStrategyConfigId { get; set; }
        [Bindable]
        public partial MatrixStrategyConfigModel MatrixStrategyConfigModel { get; set; }
        [Bindable]
        public partial bool MinStrikeSortingEnabled { get; set; }
        [Bindable]
        public partial bool AutoPermEnabled { get; set; }
        [Bindable]
        public partial double AutoPermMinEdge { get; set; }
        [Bindable]
        public partial int AutoPermOrderCount { get; set; }
        [Bindable(Default = 1)]
        public partial int AutoPermMaxGeneration { get; set; }
        [Bindable]
        public partial AutoPermSubmissionStyle AutoPermSubmissionStyle { get; set; }
        [Bindable]
        public partial int AutoPermOrderInitialSize { get; set; }

        public BasketSettings()
        {
            OmsCore.Config.ConfigChangedEvent += Config_configChangedEvent;

            RiskCheckEnabled = true;

            ActiveUncheckEnabled = OmsCore.Config.ActiveUncheckEnabled;
            AutomationConfig ??= new AutomationConfigModel()
            {
                FishEdge = 0.10,
                ContraFishEdge = 0.10,
                FishInterval = 1100,
                FishPriceIncrement = 0.01,
                LoopIncrementType = LoopIncrementType.Static,
                LoopCloseEdgeType = LoopCloseEdgeType.Static,
                LoopIntervalType = LoopIntervalType.Static,
                ContraFishPriceIncrement = 0.01,
            };
            LegInSettings ??= new LegInSettings();
            Uid = "BI" + Guid.NewGuid().ToString().Split('-')[0];

            if (SubmitWithDelayInterval < OmsCore.Config.SubmitWithDelayIntervalMin)
            {
                SubmitWithDelayInterval = OmsCore.Config.SubmitWithDelayIntervalMin;
            }

            if (SubmitWithDelayIntervalEnd < SubmitWithDelayInterval)
            {
                SubmitWithDelayIntervalEnd = SubmitWithDelayInterval;
            }

            CancelOnAmountOfFillsCount = 1;

            SpxVsSpy = new DerivedValueSettings("$SPX", "SPY", 10);
            NdxVsQqq = new DerivedValueSettings("$NDX", "QQQ", 40);
            RutVsIwm = new DerivedValueSettings("$RUT", "IWM", 10);

            SymbolToDerivativeSettingsMap = new Dictionary<string, DerivedValueSettings>()
            {
                {"$SPX", SpxVsSpy },
                {"SPY", SpxVsSpy },
                {"$NDX", NdxVsQqq },
                {"QQQ", NdxVsQqq },
                {"$RUT", RutVsIwm },
                {"IWM", RutVsIwm },
            };

            SelectedEmaType = EmaType.IV;
            EmaSmoothing = 2;
            PercentVegaThreshold = 0.1;
            EmaInterval = 5000;
            EmaPeriods = 5;
        }

        internal void ResetEma()
        {
            ResetEmaEvent?.Invoke();
        }

        private void Config_configChangedEvent(Config.OmsConfig config, bool requiresRestart)
        {
            if (SubmitWithDelayInterval < OmsCore.Config.SubmitWithDelayIntervalMin)
            {
                SubmitWithDelayInterval = OmsCore.Config.SubmitWithDelayIntervalMin;
            }

            if (SubmitWithDelayIntervalEnd < SubmitWithDelayInterval)
            {
                SubmitWithDelayIntervalEnd = SubmitWithDelayInterval;
            }
        }
    }
}
