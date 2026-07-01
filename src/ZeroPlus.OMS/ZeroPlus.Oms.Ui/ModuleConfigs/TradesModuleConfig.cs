using System;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class TradesModuleConfig
    {
        public string Symbol { get; set; }
        public bool MLeg { get; set; }
        public bool Unique { get; set; }
        public bool StatProcessingEnabled { get; set; }
        public bool RealTime { get; set; }
        public bool SubscribeToGreeks { get; set; }
        public bool SubscribeToLast { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Settings1 { get; set; }
        public string Settings2 { get; set; }
        public string SingleSettings2 { get; set; }
        public bool UseManualTime { get; set; }
        public string SelectedTime { get; set; }
        public bool AutoRefresh { get; set; }
        public int RefreshInterval { get; set; }
        public bool MaxCountEnabled { get; set; }
        public int MaxCount { get; set; }
        public string FilterString { get; set; }
        public bool AutoBlockEnabled { get; set; } = false;
        public bool AutoSubmit { get; set; } = false;
        public int MaxBlock { get; set; } = 5;
        public List<char> SelectedTradeConditionCodes { get; set; } = new();
        public List<char> SelectedSingleTradeConditionCodes { get; set; } = new();
        public bool NotificationEnabled { get; set; }
        public double NotificationEdge { get; set; }
        public double NotificationTimeSpan { get; set; }
        public bool NotificationSoundEnabled { get; set; }
        public string NotificationSound { get; set; }
        public bool AutoPermEnabled { get; set; }
        public int AutoPermCount { get; set; }
        public double AutoPermEdge { get; set; }
        public double AutoPermTargetEdge { get; set; }
        public bool EdgeFinderEnabled { get; set; }
        public bool EdgeFinderSeparateExchange { get; set; }
        public double EdgeFinderMinPriceRange { get; set; }
        public double EdgeFinderMaxPriceRange { get; set; }
        public int EdgeFinderMinTimeRange { get; set; }
        public int EdgeFinderMaxTimeRange { get; set; }
        public double EdgeFinderMinUnderMoveRange { get; set; }
        public double EdgeFinderMaxUnderMoveRange { get; set; }
        public bool LoadTickType { get; set; }
        public double MinStrikeSpacing { get; set; }
    }
}
