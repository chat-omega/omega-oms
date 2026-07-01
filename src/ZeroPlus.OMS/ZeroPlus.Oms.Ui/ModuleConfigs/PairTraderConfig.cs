using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class PairTraderConfig
    {
        public string Leg1Symbol { get; set; }
        public Side Leg1Side { get; set; }
        public int Leg1Qty { get; set; }
        public string Leg2Symbol { get; set; }
        public Side Leg2Side { get; set; }
        public int Leg2Qty { get; set; }
        public int SpreadQty { get; set; }
        public DataType DataType { get; set; }
        public double Rounding { get; set; }
        public double EmaSmoothing { get; set; }
        public double EmaPeriods { get; set; }
        public double EmaInterval { get; set; }
        public bool AutoTriggerMethod { get; set; }
        public TriggerMethod BuyTriggerMethod { get; set; }
        public InitSide BuyInitialSide { get; set; }
        public double SpreadTriggerValue { get; set; }
        public ExecutionStyle BuyExecutionStyle { get; set; }
        public int BuyManualQty { get; set; }
        public PairsType TriggerMethod { get; set; }
        public TriggerMethod SellTriggerMethod { get; set; }
        public InitSide SellInitialSide { get; set; }
        public double SpreadSellTriggerValue { get; set; }
        public ExecutionStyle SellExecutionStyle { get; set; }
        public int SellManualQty { get; set; }
        public PairTriggerType PairTriggerType { get; set; }
        public double TriggerTimer { get; set; } = 15000;
        public ExecutionStyle BuyAutoExecutionStyle { get; set; }
        public double TriggerProximity { get; set; }
        public int AutoCancel { get; set; }
        public double CancelTrigger { get; set; }
        public double StopLoss { get; set; } = .75;
        public ExecutionStyle SellAutoExecutionStyle { get; set; }
        public bool CloseOrders { get; set; }
        public bool RestOrders { get; set; }
        public PairTriggerType PairProfitType { get; set; } = PairTriggerType.Static;
        public CancelMode PairCancelMode { get; set; } = CancelMode.All;
        public bool CloseByAvgCloseTime { get; set; }
        public bool BlockReentryAfterAvgTimeClose { get; set; }
        public bool BlockReentryAfterStoploss { get; set; }
        public double AvgCloseTimeLookbackSeconds { get; set; }
        public double AvgCloseTimeMultiplier { get; set; }
        public double MinCloseTimesec { get; set; } = 120;
        public bool LockedEma { get; set; }
        public double LockedEmaValue { get; set; }
        public int BuyTiersCount { get; set; } = 3;
        public double BuyTiersSpacing { get; set; } = .05;
        public double BuyTiersProfitSpacing { get; set; } = .05;
        public double BuyProfitStart { get; set; } = .05;
        public double SellProfitStart { get; set; } = .05;
        public double BuyTiersStart { get; set; } = .05;
        public double SellTiersStart { get; set; } = .05;
        public int SellTiersCount { get; set; } = 3;
        public double SellTiersSpacing { get; set; } = .05;
        public double SellTiersProfitSpacing { get; set; } = .05;

    }
}
