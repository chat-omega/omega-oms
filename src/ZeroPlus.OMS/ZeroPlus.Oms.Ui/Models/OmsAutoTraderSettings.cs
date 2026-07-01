using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public class OmsAutoTraderSettings
    {
        public EdgeType EdgeType { get; set; }
        public double EdgeValue { get; set; }
        public Guid ConfigId { get; set; }
        public uint Sequence { get; set; } = default;
        public string Title { get; set; }
        public Venue AutoTraderVenue { get; set; }
        public AutomationConfigModel AutomationConfigModel { get; set; }
        public FishLossConfig FishLossConfig { get; set; }
        public AutoCancelConfig AutoCancelConfig { get; set; }
    }
}
