using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public class AutoTraderConfig : IHaveRisk
    {
        private Dictionary<Tuple<string, double>, AutomationConfig>? _configMapCache;
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string? RiskCheckMessage { get; set; }
        public uint Sequence { get; set; }
        public string ConfigId { get; set; } = string.Empty;
        public string ConfigName { get; set; } = string.Empty;
        public Venue Venue { get; set; }

        public AutomationConfig DefaultAutomationConfig { get; set; } = new();
        public List<AutomationConfig> UnderlyingToAutomationConfigs { get; set; } = new();

        // Edge
        public EdgeType EdgeType { get; set; }
        public double EdgeValue { get; set; }

        public TheoModel TheoModel { get; set; }
        public TheoModel FishLossTheoModel { get; set; }
        public TheoModel AutoCancelTheoModel { get; set; }

        public bool ForMarketCrossPriceUseSweepEnabled { get; set; }
        public string SweepRoute { get; set; } = string.Empty;

        // Auto Cancel & Fish Loss Prevention
        public bool CancelWithMaxSizeEnabled { get; set; }
        public int CancelWithMaxSizeLimit { get; set; }

        public bool CancelWithOrderPriceEdgeToTheoEnabled { get; set; }
        public double CancelWithOrderPriceEdgeToTheo { get; set; }

        public bool CancelWithOrderPriceEdgeToModelTheoEnabled { get; set; }
        public double CancelWithOrderPriceEdgeToModelTheo { get; set; }

        public bool CancelWithTimerEnabled { get; set; }
        public double CancelWithTimer { get; set; }

        public bool CancelWithEdgeToTheoEnabled { get; set; }
        public double CancelWithTheoEdge { get; set; }

        public bool CancelWithEdgeToAdjTheoEnabled { get; set; }
        public double CancelWithAdjTheoEdge { get; set; }

        public bool CancelWithChangeInUnderlyingPxEnabled { get; set; }
        public double CancelWithUnderlyingPxThreshold { get; set; }

        public bool CancelWithChangeInUnderlyingDeltaPxEnabled { get; set; }
        public double CancelWithUnderlyingDeltaPx { get; set; }

        public bool CancelWithEdgeToMidEnabled { get; set; }
        public double CancelWithMidEdge { get; set; }

        public bool CancelWithChangeInWidthEnabled { get; set; }
        public double CancelWithWidthThreshold { get; set; }

        public bool CancelWithMaxWidthEnabled { get; set; }
        public double CancelWithMaxWidthThreshold { get; set; }

        public bool MinEdgeToTheoCheckEnabled { get; set; }
        public double MinEdgeToTheo { get; set; }

        public bool MinEdgeToHwTheoCheckEnabled { get; set; }
        public double MinEdgeToHwTheo { get; set; }

        public bool MinEdgeToV0TheoCheckEnabled { get; set; }
        public double MinEdgeToV0Theo { get; set; }

        public bool MinEdgeToMidCheckEnabled { get; set; }
        public double MinEdgeToMid { get; set; }

        public bool MinEdgeToEmaCheckEnabled { get; set; }
        public double MinEdgeToEma { get; set; }

        public bool MinEdgeToMarketCheckEnabled { get; set; }
        public double MinEdgeToMarket { get; set; }

        public bool MinBidPercentCheckEnabled { get; set; }
        public double MinBidPercent { get; set; }

        public bool MaxBidPercentCheckEnabled { get; set; }
        public double MaxBidPercent { get; set; }

        public bool MaxDigBidPercentCheckEnabled { get; set; }
        public double MaxDigBidPercent { get; set; }

        public bool MinBidAskSizeCheckEnabled { get; set; }
        public int MinBidAskSize { get; set; }

        public bool MinEmaWidthPercentEdgeToTheoCheckEnabled { get; set; }
        public double MinEmaWidthPercentEdgeToTheoCheckEdge { get; set; }

        public bool MinBidCheckEnabled { get; set; }
        public double MinBidCheckBidValue { get; set; }

        public bool MinTheoCheckEnabled { get; set; }
        public double MinTheoCheckTheoValue { get; set; }

        // Smart Routes
        public List<Tuple<string, double>>? OpenRouteSmartMap { get; set; }
        public List<Tuple<string, double>>? CloseRouteSmartMap { get; set; }
        public List<Tuple<string, double>>? OpenRouteSingleLegSmartMap { get; set; }
        public List<Tuple<string, double>>? CloseRouteSingleLegSmartMap { get; set; }

        //Edge override
        public bool EdgeToAdjTheoWithOverrideUsePercentage { get; set; }
        public double EdgeToAdjTheoWithOverrideStatic { get; set; }
        public double EdgeToAdjTheoWithOverridePercent { get; set; }

        public bool CheckForRecentAttempt { get; set; }
        public double CheckForRecentAttemptTimespan { get; set; }
        public bool CheckForRecentFill { get; set; }
        public double CheckForRecentFillTimespan { get; set; }

        public int MinSpxAuction { get; set; }
        public int MinSpxSpreadAuction { get; set; }
        public int MinSingleLegAuction { get; set; }
        public int MinSpreadAuction { get; set; }

        public bool BestOfAdjTheoEnabled { get; set; }
        public double BestOfAdjTheoEdge { get; set; }
        public int BestOfAdjTheoModel { get; set; }
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

        public bool AutoPermEnabled { get; set; }
        public double AutoPermMinEdge { get; set; }
        public int AutoPermOrderCount { get; set; }
        public int AutoPermMaxGeneration { get; set; } = 1;
        public AutoPermSubmissionStyle AutoPermSubmissionStyle { get; set; }
        public int AutoPermOrderInitialSize { get; set; } = 1;

        [JsonIgnore]
        public Dictionary<Tuple<string, double>, AutomationConfig> UnderlyingToAutomationConfigMap => _configMapCache ??= UpdateMap(UnderlyingToAutomationConfigs);

        private static Dictionary<Tuple<string, double>, AutomationConfig> UpdateMap(List<AutomationConfig> configs)
        {
            var dict = new Dictionary<Tuple<string, double>, AutomationConfig>();
            foreach (var config in configs)
            {
                if (config.ConfigKey != null)
                {
                    var key = Tuple.Create(config.ConfigKey.Underlying ?? "", config.ConfigKey.Increment);
                    dict[key] = config;
                }
            }

            return dict;
        }
    }
}
