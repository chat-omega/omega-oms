using System;
using System.Collections.Generic;
using System.ComponentModel;
using ZeroPlus.Oms.Ui.Enums;
using System.Linq;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class DominatorConfig : ModuleConfigBase
    {
        public DominatorConfig(bool useDefaults)
        {
            if (useDefaults)
            {
                InitialFishQuantity = 1;
                RestartWhenDone = true;
                EdgeExpansionPerDelta = 0.01;
                MinEdgePerContract = 0.05;
                MaxSpreadDelta = 5.0;
            }
        }
        [Category("Main")]
        public DomSecurityType SecurityType { get; set; } = DomSecurityType.Index;
        public Guid? SpreadListSaveId { get; set; } = null;
        public bool LeastDataOption { get; set; } = false;
        public bool LoadPriceChain { get; set; } = true;
        [Category("Main")]
        public string Title { get; set; } = new Guid().ToString();
        public int InitialFishQuantity { get; set; } = 1;
        [Category("Fisher")]
        public bool RestartWhenDone { get; set; } = true;
        [Category("Multiplier")]
        public double EdgeExpansionPerDelta { get; set; } = 1;
        [Category("Filter")]
        public double MinEdgePerContract { get; set; } = 0;
        [Category("Filter")]
        public double MaxSpreadDelta { get; set; } = double.NaN;
        [Category("Filter")]
        public int MaxDTE { get; set; } = int.MaxValue;
        [Category("Filter")]
        public int MinDTE { get; set; } = 0;
        [Category("Filter")]
        public bool AllowIfDayTraded { get; set; } = true;
        [Category("Filter")]
        public bool UseBestQuoteForBidPercent { get; set; } = true;
        [Category("Multiplier")]
        public IList<UnderlyingMultiplier> UnderlyingMultiplier { get; set; } = new UnderlyingMultipliers();
        [Category("Multiplier")]
        public IList<DTEMultiplier> DTEMultiplier { get; set; } = new DTEMultipliers();

        [Category("Multiplier")]
        public IDictionary<Strategy, double> SpreadTypeMultiplier { get; set; } = Enum.GetValues<Strategy>().ToDictionary(x => x, _ => 1.0);
        [Category("Multiplier")]
        public IList<ExpirationGapMultiplier> ExpirationGapMultipliers { get; set; } = new List<ExpirationGapMultiplier> { new ExpirationGapMultiplier() };
        [Category("Multiplier")]
        public IList<LegDeltaMultiplier> LegDeltaMultiplier { get; set; } = new LegDeltaMultipliers();
    }
    public class UnderlyingMultipliers : List<UnderlyingMultiplier> { }
    public class UnderlyingMultiplier
    {
        public string Symbol { get; set; } = "";
        public double Multiplier { get; set; } = 1;
    }
    public class DTEMultipliers : List<DTEMultiplier> { }
    public class DTEMultiplier
    {
        public int Dte { get; set; } = 0;
        public double Multiplier { get; set; } = 1;
    }
    public class LegDeltaMultipliers : List<LegDeltaMultiplier> { }
    public class LegDeltaMultiplier
    {
        public double LegDelta { get; set; } = 0;
        public double Multiplier { get; set; } = 1;
    }
    public class ExpirationGapMultipliers : List<ExpirationGapMultiplier> { }
    public class ExpirationGapMultiplier
    {
        public int ExpirationGap { get; set; } = 0;
        public double Multiplier { get; set; } = 1;
    }
}