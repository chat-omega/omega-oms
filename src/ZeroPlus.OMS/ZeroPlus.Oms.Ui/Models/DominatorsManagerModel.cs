using DevExpress.Mvvm;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Excel;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DominatorsManagerModel : BindableBase, IOmsDataSubscriber
    {
        private readonly int STALE_SERVER_TIME_THRESHOLD = 10;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly PortfolioManagerModel _portfolioManagerModel;
        private readonly ConcurrentDictionary<string, DominatorModel> _dominatorIdToDominatorsMap = new();
        private string _lastServerTime = "";
        private int _lastServerTimeSame = 0;
        private IAutoTraderConfigFactory _autoTraderConfigFactory;
        private readonly IModuleFactory _moduleFactory;


        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool IsDisposed { get; set; }

        public ObservableCollection<DominatorModel> Dominators { get; set; } = new ObservableCollection<DominatorModel>();
        public ObservableCollection<ExcelManager> ExcelManagers { get; set; } = new ObservableCollection<ExcelManager>();
        public Dictionary<string, Dominator> DomSourceToDominatorMap { get; private set; } = new Dictionary<string, Dominator>();
        public double ServerCreep => Math.Round(OmsCore.QuoteClient.ServerCreepMs / 1000.0, 3, MidpointRounding.AwayFromZero);

        [Bindable]
        public partial bool AlertCreepEnabled { get; set; }
        [Bindable]
        public partial bool StopCreepEnabled { get; set; }
        [Bindable]
        public partial double AlertCreepThreshold { get; set; }
        [Bindable]
        public partial double StopCreepThreshold { get; set; }
        [Bindable]
        public partial bool AllowUniqueSpreads { get; set; }

        public DominatorsManagerModel(PortfolioManagerModel portfolioManagerModel, IAutoTraderConfigFactory autoTraderConfigFactory, IModuleFactory moduleFactory)
        {
            _moduleFactory = moduleFactory;
            _portfolioManagerModel = portfolioManagerModel;
            _autoTraderConfigFactory = autoTraderConfigFactory;
            AlertCreepThreshold = 5;
            StopCreepThreshold = 10;
            AllowUniqueSpreads = true;
            if (OmsCore.Config.DominatorsManagerListenerEnabled)
            {
                OmsCore.QuoteClient.Subscribe(String.Empty, SubscriptionFieldType.ServerClockUpdate, this);
                LoadDominatorSettings();
                OmsCore.DominatorsManager.DominatorUpdatedEvent += DominatorsManager_DominatorUpdatedEvent;
                OmsCore.DominatorsManager.DominatorDisconnectedEvent += DominatorsManager_DominatorDisconnectedEvent;
                OmsCore.DominatorsManager.ServerStatusChangedEvent += DominatorsManager_ServerStatusChangedEvent;
                OmsCore.DominatorsManager.ExcelManagerConnectedEvent += DominatorsManager_ExcelManagerConnectedEvent;
                OmsCore.DominatorsManager.ExcelManagerDisconnectedEvent += DominatorsManager_ExcelManagerDisconnectedEvent;
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.ServerClockUpdate &&
                     value is DateTime clockUpdate)
            {
                string clockUpdateString = clockUpdate.ToString("hh:mm:ss.fff");
                if (_lastServerTime == clockUpdateString)
                {
                    _lastServerTimeSame++;
                    if (_lastServerTimeSame > STALE_SERVER_TIME_THRESHOLD)
                    {
                        StopAllDominators();
                        OmsCore.DominatorsManager.DispatchMessage("Creep Alert Threshold Passed", "ZeroPlus OMS");
                        _log.Info("Stale Server Time Threshold Passed. All doms stopped.");
                    }
                }
                else
                {
                    _lastServerTime = clockUpdateString;
                    _lastServerTimeSame = 0;
                }

                if (StopCreepEnabled && ServerCreep > StopCreepThreshold)
                {
                    StopAllDominators();
                    _log.Info("Stop Creep Threshold Passed. All doms stopped.");
                }

                if (AlertCreepEnabled && ServerCreep > AlertCreepThreshold)
                {
                    OmsCore.DominatorsManager.DispatchMessage("Creep Alert Threshold Passed", "ZeroPlus OMS");
                    _log.Info("Alert Creep Threshold Passed.");
                }
            }
        }

        private void DominatorsManager_ExcelManagerConnectedEvent(ExcelManager excelManager)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ExcelManagers.Add(excelManager);
            }));
        }

        private void DominatorsManager_ExcelManagerDisconnectedEvent(ExcelManager excelManager)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ExcelManagers.Remove(excelManager);
            }));
        }

        private void DominatorsManager_ServerStatusChangedEvent(bool serverUp)
        {
            _dominatorIdToDominatorsMap.Clear();
        }

        #region AutoTraderConfig Builder


        #endregion

        internal async Task SubmitAutoTraderSettings(DominatorModel dominatorModel)
        {
            var settings = dominatorModel.OmsAutoTraderSettings;
            AutoTraderConfig autoTraderConfig = new()
            {
                UserId = (uint)OmsCore.User.ID
            };
            autoTraderConfig.ApplyDefaultAutomationConfig(settings.AutomationConfigModel);
            autoTraderConfig.ApplyFishLossConfig(settings.FishLossConfig);
            autoTraderConfig.ApplyAutoCancelConfig(settings.AutoCancelConfig);

            autoTraderConfig.OpenRouteSmartMap = OpenRouteSmartMap(settings.AutomationConfigModel);
            autoTraderConfig.OpenRouteSingleLegSmartMap = OpenRouteSingleLegSmartMap(settings.AutomationConfigModel);
            autoTraderConfig.CloseRouteSmartMap = CloseRouteSmartMap(settings.AutomationConfigModel);
            autoTraderConfig.CloseRouteSingleLegSmartMap = CloseRouteSingleLegSmartMap(settings.AutomationConfigModel);

            autoTraderConfig.ConfigId = settings.ConfigId.ToString();
            autoTraderConfig.ConfigName = settings.Title;
            autoTraderConfig.Venue = settings.AutoTraderVenue;
            dominatorModel.Dominator.AutoTraderConfigId = autoTraderConfig.ConfigId;

            autoTraderConfig.EdgeType = settings.EdgeType; // = EdgeType.LastFillAdjEdge;
            autoTraderConfig.EdgeValue = settings.EdgeValue; // = 0;
            autoTraderConfig.Sequence = settings.Sequence++;

            await Task.Run(() => OmsCore.AutoTraderClient.SendAutoTraderConfig(autoTraderConfig));
#if DEBUG
            OmsCore.DominatorsManager.SendTestOrder(dominatorModel.Dominator);
#endif
        }

        #region Routes

        static List<Tuple<string, double>> OpenRouteSmartMap(AutomationConfigModel configModel)
        {
            if (configModel.LooperOpenRoute is not null
                && OmsCore.Config.SmartRoutes.TryGetValue(configModel.LooperOpenRoute, out var openRouteMap))
            {
                return openRouteMap.Select(x => x.Value).ToList();
            }
            return [];
        }

        static List<Tuple<string, double>> CloseRouteSmartMap(AutomationConfigModel configModel)
        {

            if (configModel.LooperCloseRoute is not null
                && OmsCore.Config.SmartRoutes.TryGetValue(configModel.LooperCloseRoute, out var closeRouteMap))
            {
                return closeRouteMap.Select(x => x.Value).ToList();
            }
            return [];
        }

        static List<Tuple<string, double>> OpenRouteSingleLegSmartMap(AutomationConfigModel configModel)
        {
            if (configModel.LooperOpenRouteSingleLeg is not null
                && OmsCore.Config.SmartRoutes.TryGetValue(configModel.LooperOpenRouteSingleLeg, out var openRouteSingleLegMap))
            {
                return openRouteSingleLegMap.Select(x => x.Value).ToList();
            }
            return [];
        }

        static List<Tuple<string, double>> CloseRouteSingleLegSmartMap(AutomationConfigModel configModel)
        {
            if (configModel.LooperCloseRouteSingleLeg is not null
                && OmsCore.Config.SmartRoutes.TryGetValue(configModel.LooperCloseRouteSingleLeg, out var closeRouteSingleLegMap))
            {
                return closeRouteSingleLegMap.Select(x => x.Value).ToList();
            }
            return [];
        }
        #endregion
        private Dictionary<string, OmsAutoTraderSettings> _dominatorIdToAutoTraderSettingsMap = new();
#if DEBUG
        public void ZZZ_ADD_Dominator()
        {
            var d = new Dominator(OmsCore);
            var dominatorModel = new DominatorModel(d, _portfolioManagerModel, _moduleFactory);
            _dominatorIdToDominatorsMap[d.Id] = dominatorModel;
            Application.Current.Dispatcher.Invoke(() => Dominators.Add(dominatorModel));
        }
#endif
        private void DominatorsManager_DominatorUpdatedEvent(Dominator dominator)
        {
            if (!_dominatorIdToDominatorsMap.TryGetValue(dominator.Id, out DominatorModel dominatorModel))
            {
                dominatorModel = new DominatorModel(dominator, _portfolioManagerModel, _moduleFactory);
                _dominatorIdToDominatorsMap[dominator.Id] = dominatorModel;
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Dominators.Add(dominatorModel);
                }));
                if (!AllowUniqueSpreads)
                {
                    dominatorModel.BlockUniqueSubmissionsAsync();
                }
            }
            dominatorModel.Update(dominator);
            if (DomSourceToDominatorMap.TryGetValue(dominatorModel.Source, out Dominator saved))
            {
                if (string.IsNullOrEmpty(dominatorModel.Setup) &&
                    !string.IsNullOrEmpty(saved.Setup))
                {
                    dominator.LoadDominatorSetup(saved.Setup);
                }
                else if (string.IsNullOrEmpty(dominatorModel.Configs) &&
                         !string.IsNullOrEmpty(dominatorModel.Setup) &&
                         !string.IsNullOrEmpty(saved.FullName) &&
                         !string.IsNullOrEmpty(saved.SubName))
                {
                    dominator.LoadList(saved.FullName, saved.SubName);

                    string updateCommand = "";
                    updateCommand += "EdgeMultiplier:" + saved.EdgeMultiplier + ";";
                    updateCommand += "DeltaMax:" + saved.DeltaMax + ";";
                    updateCommand += "LoopSize:" + saved.LoopSize + ";";
                    dominator.UpdateSettings(updateCommand);
                }
            }
        }

        private void DominatorsManager_DominatorDisconnectedEvent(Dominator dominator)
        {
            if (_dominatorIdToDominatorsMap.TryGetValue(dominator.Id, out DominatorModel dominatorModel))
            {
                _dominatorIdToDominatorsMap.TryRemove(dominator.Id, out _);
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Dominators.Remove(dominatorModel);
                }));
                dominatorModel.Dispose();
            }
        }

        private void StopAllDominators()
        {
            try
            {
                Parallel.ForEach(Dominators.ToList(), dominator =>
                {
                    if (dominator.IsRunning)
                    {
                        dominator.StopAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopAllDominators));
            }
        }

        internal async void LoadDominatorSettings()
        {
            try
            {
                string configDir = OmsConfig.GetConfigDirectory();
                string configExportPath = Path.Combine(configDir, $"{nameof(Dominator)}Settings.json");
                if (File.Exists(configExportPath))
                {
                    string configJson = File.ReadAllText(configExportPath);
                    DomSourceToDominatorMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, Dominator>>(configJson));
                }

                DomSourceToDominatorMap ??= new Dictionary<string, Dominator>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadDominatorSettings));
            }
        }

        internal void SaveDominatorSettings()
        {
            try
            {
                DomSourceToDominatorMap = Dominators.Where(x => x.Active).ToDictionary(x => x.Instance, x => x.Dominator);
                string configJson = JsonConvert.SerializeObject(DomSourceToDominatorMap, Formatting.Indented);
                string configDir = OmsConfig.GetConfigDirectory();
                string configExportPath = Path.Combine(configDir, $"{nameof(Dominator)}Settings.json");
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveDominatorSettings));
            }
        }
    }
    static class AutoTraderConfigExt
    {
        public static void ApplyFishLossConfig(this AutoTraderConfig autoTraderConfig, FishLossConfig fishLossConfig)
        {
            // Mid price and width cancellation (exact matches)
            autoTraderConfig.CancelWithEdgeToMidEnabled = fishLossConfig.CancelWithEdgeToMidEnabled;
            autoTraderConfig.CancelWithMidEdge = fishLossConfig.CancelWithMidEdge;
            autoTraderConfig.CancelWithWidthThreshold = fishLossConfig.CancelWithWidthThreshold;

            // Max width checks (property name mapping)
            autoTraderConfig.CancelWithMaxWidthEnabled = fishLossConfig.MaxWidthCheckEnabled;
            autoTraderConfig.CancelWithMaxWidthThreshold = fishLossConfig.MaxWidthCheckPx;

            // Minimum edge checks (property name mappings)
            autoTraderConfig.MinEdgeToTheoCheckEnabled = fishLossConfig.MinTheoEdgeCheckEnabled;
            autoTraderConfig.MinEdgeToTheo = fishLossConfig.MinTheoEdgeCheckEdge;
            autoTraderConfig.MinEdgeToMidCheckEnabled = fishLossConfig.MinMidEdgeCheckEnabled;
            autoTraderConfig.MinEdgeToMid = fishLossConfig.MinMidEdgeCheckEdge;
            autoTraderConfig.MinEdgeToEmaCheckEnabled = fishLossConfig.MinEmaEdgeCheckEnabled;
            autoTraderConfig.MinEdgeToEma = fishLossConfig.MinEmaEdgeCheckEdge;
            autoTraderConfig.MinEdgeToMarketCheckEnabled = fishLossConfig.MinEdgeToMarketCheckEnabled;
            autoTraderConfig.MinEdgeToMarket = fishLossConfig.MinEdgeToMarketCheckEdge;

            // Bid/Ask related checks (exact matches and mappings)
            autoTraderConfig.MaxBidPercentCheckEnabled = fishLossConfig.MaxPercentBidCheckEnabled;
            autoTraderConfig.MaxBidPercent = fishLossConfig.MaxPercentBidCheckEdge;
            autoTraderConfig.MinBidCheckEnabled = fishLossConfig.MinBidCheckEnabled;
            autoTraderConfig.MinBidCheckBidValue = fishLossConfig.MinBidCheckBidValue;
            autoTraderConfig.MinBidAskSizeCheckEnabled = fishLossConfig.MinBidAskSizeCheckEnabled;
            autoTraderConfig.MinBidAskSize = fishLossConfig.MinBidAskSize;

            // EMA width percentage checks (exact matches)
            autoTraderConfig.MinEmaWidthPercentEdgeToTheoCheckEnabled = fishLossConfig.MinEmaWidthPercentEdgeToTheoCheckEnabled;
            autoTraderConfig.MinEmaWidthPercentEdgeToTheoCheckEdge = fishLossConfig.MinEmaWidthPercentEdgeToTheoCheckEdge;
        }
        public static void ApplyAutoCancelConfig(this AutoTraderConfig autoTraderConfig, AutoCancelConfig autoCancelConfig)
        {
            // Timer-based cancellation
            autoTraderConfig.CancelWithTimerEnabled = autoCancelConfig.CancelWithTimerEnabled;
            autoTraderConfig.CancelWithTimer = autoCancelConfig.CancelWithTimer;

            // Theoretical price edge cancellation
            autoTraderConfig.CancelWithEdgeToTheoEnabled = autoCancelConfig.CancelWithEdgeToTheoEnabled;
            autoTraderConfig.CancelWithTheoEdge = autoCancelConfig.CancelWithTheoEdge;
            autoTraderConfig.CancelWithEdgeToAdjTheoEnabled = autoCancelConfig.CancelWithEdgeToAdjTheoEnabled;
            autoTraderConfig.CancelWithAdjTheoEdge = autoCancelConfig.CancelWithAdjTheoEdge;

            // Underlying price cancellation
            autoTraderConfig.CancelWithChangeInUnderlyingPxEnabled = autoCancelConfig.CancelWithUnderlyingPxEnabled;
            autoTraderConfig.CancelWithUnderlyingPxThreshold = autoCancelConfig.CancelWithUnderlyingPx;
            autoTraderConfig.CancelWithChangeInUnderlyingDeltaPxEnabled = autoCancelConfig.CancelWithUnderlyingDeltaPxEnabled;
            autoTraderConfig.CancelWithUnderlyingDeltaPx = autoCancelConfig.CancelWithUnderlyingDeltaPx;

            // Mid price and width cancellation
            autoTraderConfig.CancelWithEdgeToMidEnabled = autoCancelConfig.CancelWithEdgeToMidEnabled;
            autoTraderConfig.CancelWithMidEdge = autoCancelConfig.CancelWithMidEdge;
            autoTraderConfig.CancelWithChangeInWidthEnabled = autoCancelConfig.CancelWithWidthEnabled;
            autoTraderConfig.CancelWithWidthThreshold = autoCancelConfig.CancelWithWidthThreshold;
        }
        public static void ApplyDefaultAutomationConfig(this AutoTraderConfig autoTraderConfig, AutomationConfigModel automationConfigModel)
        {
            autoTraderConfig.DefaultAutomationConfig = MapAutomationConfig(automationConfigModel);
        }
        static AutomationConfig MapAutomationConfig(AutomationConfigModel automationConfigModel)
        {
            AutomationConfig automationConfig = new();
            automationConfig.LoopingEnabled = automationConfigModel.LoopingEnabled;
            automationConfig.OpenRoute = automationConfigModel.LooperOpenRoute;
            automationConfig.CloseRoute = automationConfigModel.LooperCloseRoute;
            automationConfig.OpenRouteSingleLeg = automationConfigModel.LooperOpenRouteSingleLeg;
            automationConfig.CloseRouteSingleLeg = automationConfigModel.LooperCloseRouteSingleLeg;
            automationConfig.OpenRouteSize = automationConfigModel.LooperOpenRouteSize;
            automationConfig.CloseRouteSize = automationConfigModel.LooperCloseRouteSize;
            automationConfig.OpenRouteSingleLegSize = automationConfigModel.LooperOpenRouteSingleLegSize;
            automationConfig.CloseRouteSingleLegSize = automationConfigModel.LooperCloseRouteSingleLegSize;
            automationConfig.CloseEdgeType = automationConfigModel.LoopCloseEdgeType == Oms.Enums.LoopCloseEdgeType.Static ? SelectionType.Static : SelectionType.Dynamic;
            automationConfig.StaticCloseEdge = automationConfigModel.ContraFishEdge;
            automationConfig.StaticMinLoopEdge = automationConfigModel.LoopMinEdgeUsePercentage ? Math.Max(automationConfigModel.LoopMinEdgePercentage * automationConfigModel.ContraFishEdge, 0) : automationConfigModel.LoopMinEdge;
            automationConfig.StaticMaxLoss = automationConfigModel.LoopMaxLoss;
            automationConfig.DynamicCloseEdge = automationConfigModel.DynamicEdgeModel?.GetConfig();

            automationConfig.LooperDynamicRouting = automationConfigModel.LooperDynamicRouting;
            automationConfig.AttemptIncrementUsingDynamicRoute = automationConfigModel.AttemptIncrementUsingDynamicRoute;
            automationConfig.EnableDynamicRouteForOpeningOrders = automationConfigModel.EnableDynamicRouteForOpeningOrders;
            automationConfig.EnableDynamicRouteForClosingOrders = automationConfigModel.EnableDynamicRouteForClosingOrders;
            automationConfig.ExchToRouteList = automationConfigModel.ExchToRouteMap?.Select(x => Tuple.Create(x.Key, x.Value)).ToList() ?? new();

            automationConfig.CloseIntervalType = automationConfigModel.LoopIntervalType == Oms.Enums.LoopIntervalType.Static ? SelectionType.Static : SelectionType.Dynamic;
            automationConfig.StaticCloseInterval = automationConfigModel.ContraFishInterval;
            automationConfig.StaticCloseIntervalMax = automationConfigModel.ContraFishIntervalMax;
            automationConfig.StaticLoopInterval = automationConfigModel.LoopInterval;
            automationConfig.StaticLoopIntervalMax = automationConfigModel.LoopIntervalMax;
            automationConfig.DynamicCloseInterval = automationConfigModel.DynamicIntervalModel?.GetConfig();

            automationConfig.IncrementType = automationConfigModel.LoopIncrementType == Oms.Enums.LoopIncrementType.Static ? SelectionType.Static : SelectionType.Dynamic;
            automationConfig.StaticIncrement = automationConfigModel.ContraFishPriceIncrement;
            automationConfig.DynamicIncrement = automationConfigModel.LoopIncrementConfigModel?.DynamicIncrementConfigs?.Select(x => x.GetConfig()).ToList();

            automationConfig.SizeUpType = automationConfigModel.LoopSizeupType switch
            {
                Oms.Enums.LoopSizeupType.Static => SelectionType.Static,
                Oms.Enums.LoopSizeupType.Dynamic => SelectionType.Dynamic,
                _ => SelectionType.Off,
            };

            automationConfig.StaticSizeUpLoopCountBeforeSizeup = automationConfigModel.LoopCountBeforeSizeup;
            automationConfig.StaticSizeUp = automationConfigModel.LoopSizeupQty;
            automationConfig.DynamicSizeUp = automationConfigModel.SizeupConfig?.GetConfig();

            automationConfig.AutoAggressorEnabled = automationConfigModel.AutoAggressorEnabled;
            automationConfig.AutoAggressorMode = automationConfigModel.AutoAggressorMode;
            automationConfig.AutoAggressorEdgeTightenMode = automationConfigModel.AutoAggressorEdgeTightenMode;
            automationConfig.AutoAggressorEdgeTightenPercentage = automationConfigModel.AutoAggressorEdgeTightenPercentage;

            automationConfig.ScratchOnLowDeltaSize = automationConfigModel.ScratchOnLowDeltaSize;
            automationConfig.ScratchOnLowDeltaMax = automationConfigModel.ScratchOnLowDeltaMax;
            automationConfig.ScratchOnLowDeltaMaxLoss = automationConfigModel.ScratchOnLowDeltaMaxLoss;
            automationConfig.ScratchOnLowDeltaMinSize = automationConfigModel.ScratchOnLowDeltaMinSize;

            automationConfig.FreeLookRequireMinFillTime = automationConfigModel.FreeLookRequireMinFillTime;
            automationConfig.FreeLookMinFillTime = automationConfigModel.FreeLookMinFillTime;

            automationConfig.FreeLookOnLosers = automationConfigModel.FreeLookOnLosers;
            automationConfig.FreeLookOnLosersMax = automationConfigModel.FreeLookOnLosersMax;

            automationConfig.FreeLookOnAll = automationConfigModel.LoopFreeLookOnAll;
            automationConfig.FreeLookAfterLastAttempt = automationConfigModel.LoopFreeLook;
            automationConfig.FreeWhenGettingCloseEdge = automationConfigModel.FreeLookWhenGettingCloseEdge;
            automationConfig.FreeLookBackUpIncrement = automationConfigModel.FreeLookOnAllIncrement;
            automationConfig.FreeLookOnAllWalkBackIncrement = automationConfigModel.FreeLookOnAllWalkBackIncrement;

            automationConfig.LoopFreeLookOnAllUsingTicks = automationConfigModel.LoopFreeLookOnAllUsingTicks;
            automationConfig.FreeLookOnAllIncrementTicks = automationConfigModel.FreeLookOnAllIncrementTicks;
            automationConfig.FreeLookOnAllWalkBackIncrementTicks = automationConfigModel.FreeLookOnAllWalkBackIncrementTicks;

            automationConfig.LoopFreeLookOnNickelNames = automationConfigModel.LoopFreeLookOnNickelNames;
            automationConfig.LoopFreeLookOnNickelNamesIncrement = automationConfigModel.LoopFreeLookOnNickelNamesIncrement;
            automationConfig.LoopFreeLookOnNickelNamesRoute = automationConfigModel.LoopFreeLookOnNickelNamesRoute;
            automationConfig.LoopFreeLookOnDimeNames = automationConfigModel.LoopFreeLookOnDimeNames;
            automationConfig.LoopFreeLookOnDimeNamesIncrement = automationConfigModel.LoopFreeLookOnDimeNamesIncrement;
            automationConfig.LoopFreeLookOnDimeNamesRoute = automationConfigModel.LoopFreeLookOnDimeNamesRoute;

            automationConfig.MaintainLastEdge = automationConfigModel.MaintainLastEdge;
            automationConfig.AttemptResubmitCount = automationConfigModel.AttemptResubmit;
            automationConfig.LastFillResubmitCount = automationConfigModel.LoopResubmit;
            automationConfig.MaxNumberOfLoops = automationConfigModel.MaxLoopCount;
            automationConfig.PartialFillPercentage = automationConfigModel.AutomationRequiredPartialFillPercentage;
            automationConfig.PartialFillResubmit = automationConfigModel.AutomationPartialResubmitCount;
            automationConfig.LoopPricingMode = automationConfigModel.LoopPricingMode;
            automationConfig.ClosePxCrossOption = automationConfigModel.AdjustClosingPriceToMarket ? PxCrossOption.SmartAdjust : PxCrossOption.Ignore;
            automationConfig.AdjustClosingPriceToMarketWinnersOnly = automationConfigModel.AdjustClosingPriceToMarketWinnersOnly;

            automationConfig.AutoHedgeOnClose = automationConfigModel.AutoHedgeOnClose;
            automationConfig.AutoHedgeOnCloseSizeOnly = automationConfigModel.AutoHedgeOnCloseSizeOnly;
            automationConfig.MinHedgeHouseEdge = automationConfigModel.MinHedgeHouseEdge;
            automationConfig.AutoHedgeOnFailure = automationConfigModel.AutoHedgeOnFailure;
            automationConfig.AutoHedgePartial = automationConfigModel.AutoHedgePartial;

            automationConfig.AutoLegEnabled = automationConfigModel.AutoLegEnabled;
            automationConfig.AutoLegMaxWidth = automationConfigModel.AutoLegMaxWidth;
            automationConfig.AutoLegCloseEdge = automationConfigModel.AutoLegCloseEdge;
            automationConfig.AutoLegMaxLoss = automationConfigModel.AutoLegMaxLoss;
            automationConfig.AutoLegCloseIncrement = automationConfigModel.AutoLegCloseIncrement;
            automationConfig.AutoLegCloseRoute = automationConfigModel.AutoLegCloseRoute;
            automationConfig.AutoLegRestTime = automationConfigModel.AutoLegRestTime;
            return automationConfig;
        }
    }
}