using DevExpress.Xpf.Charts;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ChartModuleConfig
    {
        public string Symbol { get; set; }
        public bool SnapshotMode { get; set; }
        public int Interval { get; set; }
        public ChartField SelectedChartField { get; set; }
        public OptionType OptionType { get; set; }
        public int RequestDays { get; set; }
        public UnderPriceSource UnderPriceSource { get; set; }
        public double UnderLastPrice { get; set; }
        public double UnderPriceOffset { get; set; }
        public double DeltaOffset { get; set; }
        public SeriesAggregateFunction AggregateFunction { get; set; }
    }
}
