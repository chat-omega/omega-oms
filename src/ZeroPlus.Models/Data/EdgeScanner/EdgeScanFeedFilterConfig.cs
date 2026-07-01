using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class EdgeScanFeedFilterConfig : EdgeScanFeedRunnerFilterConfig
    {
        public bool AutoScrollEdgeFeed { get; set; } = true;
        public bool EnableLogMode { get; set; }
        public bool ConfirmWithIbCob { get; set; }
        public bool AutoTraderCheckForVisualFilters { get; set; }
        public string? AutoTraderConfig { get; set; }
        public DateTime SaveTime { get; set; } = DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(12);
        public bool AutoSave { get; set; }
        public bool AudioAlertEnabled { get; set; }
        public string? AudioAlertSound { get; set; }
        public bool BasketOpen { get; set; }

        public int BlockedSymbolModelId { get; set; }
        public EdgeScanFeedRunnerOption EdgeScanFeedRunnerOption { get; set; }
        public bool LoadWithStockTiedLeg { get; set; }
    }
}
