using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ModelTraderConfig
    {
        public string Underlying { get; set; }
        public double StopLoss { get; set; }
        public int Quantity { get; set; }
        public LiquidityType LiquidityType { get; set; }
        public double AddLiquidityRestPeriod { get; set; }
        public double Interval { get; set; }
        public double BarInterval { get; set; }
        public double CacheInterval { get; set; }
        public double AutoCloseInterval { get; set; }
        public double DownAutoCloseInterval { get; set; }
        public double Beta { get; set; }
        public ZeroPlus.Models.Data.Enums.OrderType OrderType { get; set; }
        public ModelType ModelType { get; set; }
        public bool SimulationEnabled { get; set; }
        public double CandlePeriod { get; set; }
    }
}
