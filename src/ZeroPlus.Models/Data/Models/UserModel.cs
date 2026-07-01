using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Salt { get; set; }
        public DateTime CreationTime { get; set; }
        public bool CanRequestPnlHistory { get; set; }
        public DateTime PnlHistoryMinDate { get; set; }
        public DateTime PnlHistoryMaxDate { get; set; }
        public bool LimitByStockLegMaxQty { get; set; }
        public int SingleStockMaxQty { get; set; }
        public bool LimitByMaxQty { get; set; }
        public int MaxQty { get; set; }
        public bool LimitBySingleLegMaxQty { get; set; }
        public int SingleLegMaxQty { get; set; }
        public bool LimitByMaxSpreadCount { get; set; }
        public int MaxSpreadCount { get; set; }
        public bool LimitByMaxDelta { get; set; }
        public double MaxDelta { get; set; }
        public bool LimitByMaxRealizedPnl { get; set; }
        public double MaxRealizedPnl { get; set; }
        public bool LimitByMaxUnRealizedPnl { get; set; }
        public double MaxUnRealizedPnl { get; set; }
        public bool LimitByMaxLongNotional { get; set; }
        public double MaxLongNotional { get; set; }
        public bool LimitByMaxShortNotional { get; set; }
        public double MaxShortNotional { get; set; }
        public bool LimitByMaxOpenPositions { get; set; }
        public int MaxOpenPositions { get; set; }
        public bool LimitByMaxSubmissionsPerSecond { get; set; }
        public int MaxSubmissionsPerSecond { get; set; }
        public bool LimitByMaxAutoPermCount { get; set; } = true;
        public int MaxAutoPermCount { get; set; } = 3;
        public bool LimitByMaxAutoPermSlamSize { get; set; } = true;
        public int MaxAutoPermSlamSize { get; set; } = 3;
        public bool LimitByMaxAutoPermGeneration { get; set; } = true;
        public int MaxAutoPermGeneration { get; set; } = 3;

        public IEnumerable<AccountModel>? Accounts { get; set; }
        public IEnumerable<ModuleModel>? Modules { get; set; }
    }
}
