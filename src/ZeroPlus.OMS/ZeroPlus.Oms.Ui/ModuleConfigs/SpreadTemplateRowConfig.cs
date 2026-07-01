using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class SpreadTemplateRowConfig
    {
        public BaseStrategy Strategy { get; set; }
        public Side Side { get; set; }
        public double Leg1Delta { get; set; }
        public double Leg2Delta { get; set; }
        public double Leg3Delta { get; set; }
        public double Leg4Delta { get; set; }
        public double EdgeOverride { get; set; }
        public bool EdgeOverrideEnabled { get; set; }
        public DateTime Leg1Expiration { get; set; }
        public DateTime Leg2Expiration { get; set; }
        public DateTime Leg3Expiration { get; set; }
        public DateTime Leg4Expiration { get; set; }
    }
}
