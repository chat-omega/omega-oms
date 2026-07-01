using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class VolTraderConfig : ModuleConfigBase
    {
        public bool StrikeRangeEnabled { get; set; }
        public double MinStrike { get; set; }
        public double MaxStrike { get; set; }
        public bool DeltaRangeEnabled { get; set; }
        public double MinDelta { get; set; }
        public double MaxDelta { get; set; }
        public bool IncludeDecimalStrikes { get; set; }
        public Dictionary<string, string> BasketIdToBasketMap { get; set; } = new Dictionary<string, string>();
    }
}
