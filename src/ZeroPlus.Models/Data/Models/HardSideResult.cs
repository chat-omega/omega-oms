using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public class HardSideResult
    {
        public HardSideKey HardSideKey { get; set; }
        public Side HardSide { get; set; }
        public DateTime DesignationTime { get; set; }
        public List<double>? Strikes { get; set; }
        public double HardSideBuyGiveUp { get; set; } = double.NaN;
        public double HardSideSellGiveUp { get; set; } = double.NaN;
    }
}
