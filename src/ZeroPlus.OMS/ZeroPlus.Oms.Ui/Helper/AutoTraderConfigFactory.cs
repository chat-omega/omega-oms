using AutoMapper;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Ui.Models;
using System;
using System.Collections.Generic;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class AutoTraderConfigFactory : IAutoTraderConfigFactory
    {
        private readonly IMapper _mapper;

        public AutoTraderConfigFactory(IMapper mapper)
        {
            _mapper = mapper;
        }
        public AutoTraderConfig CreateFromSettings(OmsAutoTraderSettings settings)
        {
            var builder = new AutoTraderConfigBuilder(_mapper)
            .FromAutomationConfig(settings.AutomationConfigModel)
            .WithEdgeConfiguration(settings.EdgeType, settings.EdgeValue)
            .FromFishLossConfig(settings.FishLossConfig)
            .FromAutoCancelConfig(settings.AutoCancelConfig)
            .WithBasicSettings(
                configId: settings.ConfigId.ToString(),
                configName: settings.Title,
                venue: settings.AutoTraderVenue);

            return builder.Build();
        }

        public AutoTraderConfig CreateFromTraderWithAutomation(
            IAutoTraderSettings automationTrader,
            AutomationConfigModel automationConfig,
            EdgeType edgeType,
            double edgeValue)
            => new AutoTraderConfigBuilder(_mapper)
                .FromAutoTraderSettings(automationTrader)
                .FromAutomationConfig(automationConfig)
                .WithBasicSettings(automationTrader.Uid, automationTrader.Uid, Venue.TB)
                .WithEdgeConfiguration(edgeType, edgeValue)
                .Build();
        
        protected internal class AutoTraderConfigBuilder
        {
            readonly AutoTraderConfig _config;
            private readonly IMapper _mapper;
            public AutoTraderConfig Build() => throw new NotImplementedException();
            public AutoTraderConfigBuilder(IMapper mapper)
            {
                _config = new AutoTraderConfig();
                _mapper = mapper;
            }
            public AutoTraderConfigBuilder FromAutomationConfig(AutomationConfigModel automationConfig)
            {
                _mapper.Map(automationConfig, _config);
                return this;
            }
            public AutoTraderConfigBuilder FromAutoTraderSettings(IAutoTraderSettings automationTrader)
            {
                _mapper.Map(automationTrader, _config);
                return this;
            }
            public AutoTraderConfigBuilder FromFishLossConfig(FishLossConfig fishLossConfig)
            {
                _mapper.Map(fishLossConfig, _config);
                return this;
            }
            public AutoTraderConfigBuilder FromAutoCancelConfig(AutoCancelConfig autoCancelConfig)
            {
                _mapper.Map(autoCancelConfig, _config);
                return this;
            }
            public AutoTraderConfigBuilder WithBasicSettings(string configId, string configName, Venue venue)
            {
                _config.ConfigId = configId;
                _config.ConfigName = configName;
                _config.Venue = venue;
                return this;
            }
            public AutoTraderConfigBuilder WithEdgeConfiguration(EdgeType edgeType, double edge)
            {
                _config.EdgeType = edgeType;
                _config.EdgeValue = edge;
                return this;
            }

            public AutoTraderConfigBuilder WithSmartMaps(
                List<Tuple<string, double>> openRouteMap,
                List<Tuple<string, double>> openRouteSingleLegMap,
                List<Tuple<string, double>> closeRouteMap,
                List<Tuple<string, double>> closeRouteSingleLegMap)
            {
                _config.OpenRouteSmartMap = openRouteMap;
                _config.OpenRouteSingleLegSmartMap = openRouteSingleLegMap;
                _config.CloseRouteSmartMap = closeRouteMap;
                _config.CloseRouteSingleLegSmartMap = closeRouteSingleLegMap;
                return this;
            }
            public AutoTraderConfigBuilder WithCloseEdgeConfiguration(
                SelectionType closeEdgeType,
                double staticCloseEdge,
                double staticMinLoopEdge,
                DynamicEdgeConfigModel dynamicCloseEdge = null)
            {
                _config.DefaultAutomationConfig.CloseEdgeType = closeEdgeType;
                _config.DefaultAutomationConfig.StaticCloseEdge = staticCloseEdge;
                _config.DefaultAutomationConfig.StaticMinLoopEdge = staticMinLoopEdge;
                _config.DefaultAutomationConfig.DynamicCloseEdge = dynamicCloseEdge;
                return this;
            }
            public AutoTraderConfigBuilder WithRouteConfiguration(
                string openRoute = null,
                string closeRoute = null,
                string openRouteSingleLeg = null,
                string closeRouteSingleLeg = null)
            {
                _config.DefaultAutomationConfig.OpenRoute = openRoute ?? OmsCore.Config.DefaultRoute(InstanceMode.AT_TB);
                _config.DefaultAutomationConfig.CloseRoute = closeRoute ?? OmsCore.Config.DefaultRoute(InstanceMode.AT_TB);
                _config.DefaultAutomationConfig.OpenRouteSingleLeg = openRouteSingleLeg ?? OmsCore.Config.DefaultSingleLegRoute(InstanceMode.AT_TB);
                _config.DefaultAutomationConfig.CloseRouteSingleLeg = closeRouteSingleLeg ?? OmsCore.Config.DefaultSingleLegRoute(InstanceMode.AT_TB);
                return this;
            }
            public AutoTraderConfigBuilder EnableLoopConfiguration(int maxNumberOfLoops, LoopPricingMode pricingMode)
            {
                _config.DefaultAutomationConfig.LoopingEnabled = true;  // Explicitly enable looping
                _config.DefaultAutomationConfig.MaxNumberOfLoops = maxNumberOfLoops;
                _config.DefaultAutomationConfig.LoopPricingMode = pricingMode;
                return this;
            }
            public AutoTraderConfigBuilder WithPriceConfiguration(
                PxCrossOption openCrossOption,
                PxCrossOption closeCrossOption)
            {
                _config.DefaultAutomationConfig.PxCrossOption = openCrossOption;
                _config.DefaultAutomationConfig.ClosePxCrossOption = closeCrossOption;
                return this;
            }
            public AutoTraderConfigBuilder WithStaticCloseEdge(
                double staticCloseEdge,
                double staticMinLoopEdge,
                double staticMaxLoss)
            {
                _config.DefaultAutomationConfig.CloseEdgeType = SelectionType.Static;
                _config.DefaultAutomationConfig.StaticCloseEdge = staticCloseEdge;
                _config.DefaultAutomationConfig.StaticMinLoopEdge = staticMinLoopEdge;
                _config.DefaultAutomationConfig.StaticMaxLoss = staticMaxLoss;
                return this;
            }
            public AutoTraderConfigBuilder WithDynamicCloseEdge(DynamicEdgeConfigModel dynamicConfig)
            {
                _config.DefaultAutomationConfig.CloseEdgeType = SelectionType.Dynamic;
                _config.DefaultAutomationConfig.DynamicCloseEdge = dynamicConfig;
                return this;
            }
            public AutoTraderConfigBuilder WithStaticCloseInterval(
                double closeInterval,
                double loopInterval)
            {
                _config.DefaultAutomationConfig.CloseIntervalType = SelectionType.Static;
                _config.DefaultAutomationConfig.StaticCloseInterval = closeInterval;
                _config.DefaultAutomationConfig.StaticLoopInterval = loopInterval;
                return this;
            }
            public AutoTraderConfigBuilder WithDynamicCloseInterval(DynamicIntervalConfigModel dynamicConfig)
            {
                _config.DefaultAutomationConfig.CloseIntervalType = SelectionType.Dynamic;
                _config.DefaultAutomationConfig.DynamicCloseInterval = dynamicConfig;
                return this;
            }

            public AutoTraderConfigBuilder WithStaticIncrement(double increment)
            {
                _config.DefaultAutomationConfig.IncrementType = SelectionType.Static;
                _config.DefaultAutomationConfig.StaticIncrement = increment;
                return this;
            }
            public AutoTraderConfigBuilder WithDynamicIncrement(List<DynamicIncrementModel> dynamicConfig)
            {
                _config.DefaultAutomationConfig.IncrementType = SelectionType.Dynamic;
                _config.DefaultAutomationConfig.DynamicIncrement = dynamicConfig;
                return this;
            }
            public AutoTraderConfigBuilder WithStaticSizeUp(
                int loopCountBeforeSizeup,
                int sizeUp)
            {
                _config.DefaultAutomationConfig.SizeUpType = SelectionType.Static;
                _config.DefaultAutomationConfig.StaticSizeUpLoopCountBeforeSizeup = loopCountBeforeSizeup;
                _config.DefaultAutomationConfig.StaticSizeUp = sizeUp;
                return this;
            }
            public AutoTraderConfigBuilder WithDynamicSizeUp(DynamicSizeUpConfigModel dynamicConfig)
            {
                _config.DefaultAutomationConfig.SizeUpType = SelectionType.Dynamic;
                _config.DefaultAutomationConfig.DynamicSizeUp = dynamicConfig;
                return this;
            }
            public AutoTraderConfigBuilder EnableFreeLook(
                double backupIncrement,
                double walkBackIncrement)
            {
                _config.DefaultAutomationConfig.FreeLookOnAll = true;
                _config.DefaultAutomationConfig.FreeLookBackUpIncrement = backupIncrement;
                _config.DefaultAutomationConfig.FreeLookOnAllWalkBackIncrement = walkBackIncrement;
                return this;
            }
            public AutoTraderConfigBuilder EnableFreeLookAfterLastAttempt()
            {
                _config.DefaultAutomationConfig.FreeLookAfterLastAttempt = true;
                return this;
            }
            public AutoTraderConfigBuilder EnableFreeLookWithTicks(
                double incrementTicks = 1.0,
                double walkBackIncrementTicks = 1.0)
            {
                _config.DefaultAutomationConfig.LoopFreeLookOnAllUsingTicks = true;
                _config.DefaultAutomationConfig.FreeLookOnAllIncrementTicks = incrementTicks;
                _config.DefaultAutomationConfig.FreeLookOnAllWalkBackIncrementTicks = walkBackIncrementTicks;
                return this;
            }
            public AutoTraderConfigBuilder EnableNickelNamesFreeLook(
                double increment,
                string route)
            {
                _config.DefaultAutomationConfig.LoopFreeLookOnNickelNames = true;
                _config.DefaultAutomationConfig.LoopFreeLookOnNickelNamesIncrement = increment;
                _config.DefaultAutomationConfig.LoopFreeLookOnNickelNamesRoute = route;
                return this;
            }
            public AutoTraderConfigBuilder EnableDimeNamesFreeLook(
                double increment,
                string route)
            {
                _config.DefaultAutomationConfig.LoopFreeLookOnDimeNames = true;
                _config.DefaultAutomationConfig.LoopFreeLookOnDimeNamesIncrement = increment;
                _config.DefaultAutomationConfig.LoopFreeLookOnDimeNamesRoute = route;
                return this;
            }
            public AutoTraderConfigBuilder WithResubmitConfiguration(
                int attemptCount,
                int lastFillCount)
            {
                _config.DefaultAutomationConfig.AttemptResubmitCount = attemptCount;
                _config.DefaultAutomationConfig.LastFillResubmitCount = lastFillCount;
                return this;
            }
            public AutoTraderConfigBuilder WithPartialFillConfiguration(
                double percentage,
                int resubmitCount)
            {
                _config.DefaultAutomationConfig.PartialFillPercentage = percentage;
                _config.DefaultAutomationConfig.PartialFillResubmit = resubmitCount;
                return this;
            }
            public AutoTraderConfigBuilder EnableAutoAggressor(
                AutoAggressorMode mode,
                AutoAggressorEdgeTightenMode tightenMode,
                double tightenPercentage)
            {
                _config.DefaultAutomationConfig.AutoAggressorEnabled = true;
                _config.DefaultAutomationConfig.AutoAggressorMode = mode;
                _config.DefaultAutomationConfig.AutoAggressorEdgeTightenMode = tightenMode;
                _config.DefaultAutomationConfig.AutoAggressorEdgeTightenPercentage = tightenPercentage;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithTimer(double timer)
            {
                _config.CancelWithTimerEnabled = true;
                _config.CancelWithTimer = timer;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithTheoEdge(double edge)
            {
                _config.CancelWithEdgeToTheoEnabled = true;
                _config.CancelWithTheoEdge = edge;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithAdjTheoEdge(double edge)
            {
                _config.CancelWithEdgeToAdjTheoEnabled = true;
                _config.CancelWithAdjTheoEdge = edge;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithUnderlyingPriceChange(double threshold)
            {
                _config.CancelWithChangeInUnderlyingPxEnabled = true;
                _config.CancelWithUnderlyingPxThreshold = threshold;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithUnderlyingDeltaPrice(double delta)
            {
                _config.CancelWithChangeInUnderlyingDeltaPxEnabled = true;
                _config.CancelWithUnderlyingDeltaPx = delta;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithMidEdge(double edge)
            {
                _config.CancelWithEdgeToMidEnabled = true;
                _config.CancelWithMidEdge = edge;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithWidthChange(double threshold)
            {
                _config.CancelWithChangeInWidthEnabled = true;
                _config.CancelWithWidthThreshold = threshold;
                return this;
            }
            public AutoTraderConfigBuilder EnableCancelWithMaxWidth(double threshold)
            {
                _config.CancelWithMaxWidthEnabled = true;
                _config.CancelWithMaxWidthThreshold = threshold;
                return this;
            }
            public AutoTraderConfigBuilder EnableMinTheoEdgeCheck(double edge)
            {
                _config.MinEdgeToTheoCheckEnabled = true;
                _config.MinEdgeToTheo = edge;
                return this;
            }
            public AutoTraderConfigBuilder EnableMinMidEdgeCheck(double edge)
            {
                _config.MinEdgeToMidCheckEnabled = true;
                _config.MinEdgeToMid = edge;
                return this;
            }
            public AutoTraderConfigBuilder EnableMinEmaEdgeCheck(double edge)
            {
                _config.MinEdgeToEmaCheckEnabled = true;
                _config.MinEdgeToEma = edge;
                return this;
            }

            public AutoTraderConfigBuilder EnableMinMarketEdgeCheck(double edge)
            {
                _config.MinEdgeToMarketCheckEnabled = true;
                _config.MinEdgeToMarket = edge;
                return this;
            }

            // Bid/Ask checks - each can be enabled independently
            public AutoTraderConfigBuilder EnableMinBidPercentCheck(double percent)
            {
                _config.MinBidPercentCheckEnabled = true;
                _config.MinBidPercent = percent;
                return this;
            }

            public AutoTraderConfigBuilder EnableMaxBidPercentCheck(double percent)
            {
                _config.MaxBidPercentCheckEnabled = true;
                _config.MaxBidPercent = percent;
                return this;
            }

            public AutoTraderConfigBuilder EnableMinBidAskSizeCheck(int size)
            {
                _config.MinBidAskSizeCheckEnabled = true;
                _config.MinBidAskSize = size;
                return this;
            }

            public AutoTraderConfigBuilder EnableMinEmaWidthPercentEdgeToTheoCheck(double edge)
            {
                _config.MinEmaWidthPercentEdgeToTheoCheckEnabled = true;
                _config.MinEmaWidthPercentEdgeToTheoCheckEdge = edge;
                return this;
            }

            public AutoTraderConfigBuilder EnableMinBidCheck(double value)
            {
                _config.MinBidCheckEnabled = true;
                _config.MinBidCheckBidValue = value;
                return this;
            }
            public AutoTraderConfigBuilder EnableScratchOnLowDelta(
                double max,
                double maxLoss,
                int minSize)
            {
                _config.DefaultAutomationConfig.ScratchOnLowDeltaSize = true;
                _config.DefaultAutomationConfig.ScratchOnLowDeltaMax = max;
                _config.DefaultAutomationConfig.ScratchOnLowDeltaMaxLoss = maxLoss;
                _config.DefaultAutomationConfig.ScratchOnLowDeltaMinSize = minSize;
                return this;
            }
            public AutoTraderConfigBuilder EnableAutoHedgeOnClose(
                bool sizeOnly = false,
                double minHedgeHouseEdge = 0.1)
            {
                _config.DefaultAutomationConfig.AutoHedgeOnClose = true;
                _config.DefaultAutomationConfig.AutoHedgeOnCloseSizeOnly = sizeOnly;
                _config.DefaultAutomationConfig.MinHedgeHouseEdge = minHedgeHouseEdge;
                return this;
            }
            public AutoTraderConfigBuilder EnableAutoHedgeOnFailure()
            {
                _config.DefaultAutomationConfig.AutoHedgeOnFailure = true;
                return this;
            }
            public AutoTraderConfigBuilder EnableAutoHedgePartial()
            {
                _config.DefaultAutomationConfig.AutoHedgePartial = true;
                return this;
            }
            public AutoTraderConfigBuilder EnableMaintainLastEdge()
            {
                _config.DefaultAutomationConfig.MaintainLastEdge = true;
                return this;
            }

            public AutoTraderConfigBuilder EnableAdjustClosingPriceToMarketWinnersOnly()
            {
                _config.DefaultAutomationConfig.AdjustClosingPriceToMarketWinnersOnly = true;
                return this;
            }
        }
    }
}
