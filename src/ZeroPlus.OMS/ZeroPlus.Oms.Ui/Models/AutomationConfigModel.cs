using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Automation;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class AutomationConfigModel : BindableBase, ILoopSettings, IAutomationConfig
    {
        public const int MAX_LOOP_COUNT = 40;

        private CloseOrderMode _closeOrderMode;
        private double _contraFishEdge;
        private double _loopMinEdge;
        private int _maxLoopCount;






        private DynamicSizeupModel _dynamicSizeupModel;
        private int _sizeupModelId;
        private bool _lockTraderAutoCloseEnabled;

        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }
        [JsonProperty]
        public bool? CloseOrderMode
        {
            get => CloseOrderModeConverter.ConvertBack(_closeOrderMode, typeof(bool?), null, CultureInfo.InvariantCulture) as bool?;
            set
            {
                var x = (CloseOrderMode)CloseOrderModeConverter.Convert(value, typeof(CloseOrderMode), null, CultureInfo.InvariantCulture)!;
                SetValue(ref _closeOrderMode, x);
                switch (_closeOrderMode)
                {
                    case Oms.Enums.CloseOrderMode.None:
                        GoFishAutoCloseEnabled = false;
                        LoopingEnabled = false;
                        break;
                    case Oms.Enums.CloseOrderMode.AutoClose:
                        GoFishAutoCloseEnabled = true;
                        LoopingEnabled = false;
                        break;
                    case Oms.Enums.CloseOrderMode.Looper:
                        GoFishAutoCloseEnabled = true;
                        LoopingEnabled = true;
                        break;
                }
                SetClosingEdgeMinValue();
            }
        }
        public static IValueConverter CloseOrderModeConverter { get; } = SetupOrderModeConverter();

        [JsonProperty]
        [Bindable]
        public partial bool LockTraderResubmitOnFillEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LockTraderResetQtyOnResubmit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LockTraderResubmitOnFillMaxCount { get; set; }
        [JsonProperty]
        public bool LockTraderAutoCloseEnabled
        {
            get => _lockTraderAutoCloseEnabled;
            set
            {
                SetValue(ref _lockTraderAutoCloseEnabled, value);
                GoFishAutoCloseEnabled = value;
                LoopingEnabled = false;
            }
        }
        [JsonProperty]
        [Bindable]
        public partial bool GoFishAutoCloseEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseResubmit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FishEdge { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial double DynamicEdgeExpansion { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial double DynamicSizeExpansion { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FishPriceIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int FishInterval { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double CloseEdgeMinValue { get; set; }
        [JsonProperty]
        [Bindable]
        public partial ClosingTypes ClosingMode { get; set; }
        [JsonProperty]
        public double ContraFishEdge
        {
            get => _contraFishEdge;
            set
            {
                SetValue(ref _contraFishEdge, value);
                SetClosingEdgeMinValue();
            }
        }
        [JsonProperty]
        [Bindable]
        public partial double ContraFishPriceIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ContraFishInterval { get; set; }

        [JsonProperty]
        [Bindable]
        public partial int ContraFishIntervalMax { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LeaveAutoCloseResting { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopingEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool MaintainLastEdge { get; set; }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool AttemptRegularCloseIn3Way { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseMatchingHwTheosForPricing3WayVerticals { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ThreeWayCloseMaxSpacing { get; set; }
        [JsonProperty]
        [Bindable(Default = 4)]
        public partial int ThreeWayVerticalResubmit { get; set; }
        [JsonProperty]
        [Bindable(Default = 4)]
        public partial int ThreeWayCloseMaxPerms { get; set; }
        [JsonProperty]
        public double LoopMinEdge
        {
            get => _loopMinEdge;
            set
            {
                SetValue(ref _loopMinEdge, value);
                SetClosingEdgeMinValue();
            }
        }
        [JsonProperty]
        [Bindable]
        public partial double LoopMinEdgePercentage { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopMinEdgeUsePercentage { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopInterval { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopIntervalMax { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopResubmit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int AttemptResubmit { get; set; }
        [JsonProperty]
        public int MaxLoopCount
        {
            get => _maxLoopCount;
            set => SetValue(ref _maxLoopCount, Math.Min(value, MAX_LOOP_COUNT));
        }
        [JsonProperty]
        [Bindable]
        public partial int AutomationPartialResubmitCount { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double AutomationRequiredPartialFillPercentage { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LoopMaxLoss { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopFreeLook { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool FreeLookRequireMinFillTime { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FreeLookMinFillTime { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool FreeLookOnLosers { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int FreeLookOnLosersMax { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool FreeLookWhenGettingCloseEdge { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoHedgeOnClose { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoHedgeOnCloseSizeOnly { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoHedgeOnFailure { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoHedgePartial { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoHedgeOpenTicket { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MinHedgeHouseEdge { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoAggressorEnabled { get; set; }
        [JsonProperty]
        [Bindable]
        public partial AutoAggressorMode AutoAggressorMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial AutoAggressorEdgeTightenMode AutoAggressorEdgeTightenMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double AutoAggressorEdgeTightenPercentage { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool ScratchOnLowDeltaSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool IcebergCloserEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int IcebergDisplaySize { get; set; }
        [JsonProperty]
        [Bindable(Default = 10)]
        public partial int IcebergTotalSize { get; set; }

        [JsonProperty]
        [Bindable]
        public partial int IcebergMaxResubmit { get; set; }

        [JsonProperty]
        [Bindable(Default = .05)]
        public partial double ScratchOnLowDeltaMax { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double ScratchOnLowDeltaMaxLoss { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int ScratchOnLowDeltaMinSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopFreeLookOnAll { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopFreeLookOnAllUsingTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FreeLookOnAllIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FreeLookOnAllWalkBackIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FreeLookOnAllIncrementTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double FreeLookOnAllWalkBackIncrementTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopFreeLookOnNickelNames { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LoopFreeLookOnNickelNamesIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LoopFreeLookOnNickelNamesRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LoopFreeLookOnDimeNames { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LoopFreeLookOnDimeNamesIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LoopFreeLookOnDimeNamesRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopSizeupType LoopSizeupType { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SizeUpOnClosingLoop { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SizeUpOnHardSideOnly { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool RequireAdjEdgeForSizeUp { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopIncrementType LoopIncrementType { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopCloseEdgeType LoopCloseEdgeType { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopIntervalType LoopIntervalType { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int DynamicEdgeModelId { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopIncrementConfigModelId { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int AutoPermConfigModelId { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopIncrementConfigModel LoopIncrementConfigModel { get; set; }
        [JsonProperty]
        [Bindable]
        public partial BasketAutoPermModel AutoPermConfigModel { get; set; }
        [JsonProperty]
        [Bindable]
        public partial IDynamicEdgeModel DynamicEdgeModel { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int DynamicIntervalModelId { get; set; }
        [JsonProperty]
        [Bindable]
        public partial IDynamicIntervalModel DynamicIntervalModel { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LooperDynamicRouting { get; set; }
        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool AttemptIncrementUsingDynamicRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool EnableDynamicRouteForOpeningOrders { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool EnableDynamicRouteForClosingOrders { get; set; }
        [JsonProperty]
        [Bindable]
        public partial Dictionary<string, string> ExchToRouteMap { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperOpenRouteSingleLeg { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperCloseRouteSingleLeg { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperOpenRouteSingleLegSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperCloseRouteSingleLegSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseSingleLegSeparateLooperRoutes { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperOpenRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperCloseRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperOpenRouteSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string LooperCloseRouteSize { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string StockTiedOrderRoute { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopSizeupQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopCountBeforeSizeup { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoopPricingMode LoopPricingMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AdjustClosingPriceToMarket { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AdjustClosingPriceToMarketWinnersOnly { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LastEdgeTightenPercent { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxBelowEdgeResubmit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutMaxPercentThroughEma { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutMaxDollarThroughEma { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutFillGuarantee { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutMaxLoss { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutSingleLegIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutSingleLegCancelTime { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double LegOutSpreadCancelTime { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial int SweepTradeEntrySize { get; set; }
        [JsonProperty]
        [Bindable(Default = .15)]
        public partial double SweepTradeEntryLimitPercentage { get; set; }
        [JsonProperty]
        [Bindable(Default = .05)]
        public partial double SweepTradeStopLossPercentage { get; set; }
        [JsonProperty]
        [Bindable(Default = .05)]
        public partial double SweepTradeExitTriggerPercentage { get; set; }
        [JsonProperty]
        [Bindable(Default = .50)]
        public partial double SweepTradeScaledExitPercentage { get; set; }
        [JsonProperty]
        [Bindable]
        public partial DateTime SweepTradeAutoCloseTime { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoLegEnabled { get; set; }
        [JsonProperty]
        [Bindable(Default = .05)]
        public partial double AutoLegMaxWidth { get; set; }
        [JsonProperty]
        [Bindable(Default = .10)]
        public partial double AutoLegCloseEdge { get; set; }
        [JsonProperty]
        [Bindable(Default = .20)]
        public partial double AutoLegMaxLoss { get; set; }
        [JsonProperty]
        [Bindable(Default = .03)]
        public partial double AutoLegCloseIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string AutoLegCloseRoute { get; set; }
        [JsonProperty]
        [Bindable(Default = 500)]
        public partial double AutoLegRestTime { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial double EdgeMultiplier { get; set; }
        [JsonProperty]
        [Bindable(Default = 1)]
        public partial double MaxLossMultiplier { get; set; }
        [JsonProperty]
        public DynamicSizeupModel SizeupConfig
        {
            get => _dynamicSizeupModel;
            set => SetValue(ref _dynamicSizeupModel, value);
        }
        [JsonProperty]
        public int SizeupConfigId
        {
            get => _sizeupModelId;
            set => SetValue(ref _sizeupModelId, value);
        }

        [JsonConstructor]
        public AutomationConfigModel(DynamicIntervalModel dynamicIntervalModel, DynamicEdgeModel dynamicEdgeModel) : this()
        {
            DynamicEdgeModel = dynamicEdgeModel;
            DynamicIntervalModel = dynamicIntervalModel;
        }

        public AutomationConfigModel()
        {
            SweepTradeAutoCloseTime = DateTime.Today + TimeSpan.FromHours(14) + TimeSpan.FromMinutes(50);
            AutoLegCloseRoute = OmsCore.Config?.DefaultSingleLegRoute(OmsCore.Config.InstanceModeV3);
            AdjustClosingPriceToMarket = false;
            MaxBelowEdgeResubmit = 2;
            LegOutMaxPercentThroughEma = .1;
            LegOutMaxDollarThroughEma = .1;
            LegOutFillGuarantee = .00;
            LegOutMaxLoss = .8;
            LegOutSingleLegIncrement = .01;
            LegOutSingleLegCancelTime = 100;
            LegOutSpreadCancelTime = 2000;
        }

        private void SetClosingEdgeMinValue()
        {
            if (LoopingEnabled)
            {
                CloseEdgeMinValue = LoopMinEdge;
                if (ContraFishEdge < LoopMinEdge)
                {
                    ContraFishEdge = LoopMinEdge;
                }
            }
            else if (GoFishAutoCloseEnabled)
            {
                CloseEdgeMinValue = 0;
            }
            else
            {
                CloseEdgeMinValue = 0;
            }
        }

        public static void SaveConfig(IList<AutomationConfigModel> automationConfigModels, ref ObservableCollection<AutomationConfigModel> models)
        {
            string configDirectory = OmsConfig.GetConfigDirectory();

            if (configDirectory != null)
            {
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }
                string automationConfigFile = Path.Combine(configDirectory, $"AutomationConfigs.json");
                string content = JsonConvert.SerializeObject(automationConfigModels);
                File.WriteAllText(automationConfigFile, content);
                models = automationConfigModels.ToObservableCollection();

                OmsCore.Config.OnChange(requiresRestart: false);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is not AutomationConfigModel other)
            {
                return false;
            }

            if (other.Title == this.Title)
            {
                return JsonConvert.SerializeObject(other) == JsonConvert.SerializeObject(this);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return JsonConvert.SerializeObject(this).GetHashCode();
        }

        private static IValueConverter SetupOrderModeConverter()
        {
            return DelegateConverterFactory.CreateValueConverter<bool?, CloseOrderMode>(x => x switch
            {
                true => Oms.Enums.CloseOrderMode.Looper,
                false => Oms.Enums.CloseOrderMode.AutoClose,
                null => Oms.Enums.CloseOrderMode.None,
            }, x => x switch
            {
                Oms.Enums.CloseOrderMode.Looper => true,
                Oms.Enums.CloseOrderMode.AutoClose => false,
                Oms.Enums.CloseOrderMode.None => null,
                _ => null
            });
        }
    }
}
