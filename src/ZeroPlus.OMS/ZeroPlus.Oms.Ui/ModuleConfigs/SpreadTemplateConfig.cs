using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class SpreadTemplateConfig
    {
        public string UnderlyingQuery { get; set; }
        public List<string> Templates { get; set; }
        public bool MinStrikeEnabled { get; set; }
        public double MinStrike { get; set; }
        public bool MaxStrikeEnabled { get; set; }
        public double MaxStrike { get; set; }
        public bool MinOpenInterestEnabled { get; set; }
        public double MinOpenInterest { get; set; }
        public bool MaxOpenInterestEnabled { get; set; }
        public double MaxOpenInterest { get; set; }
    }
}
