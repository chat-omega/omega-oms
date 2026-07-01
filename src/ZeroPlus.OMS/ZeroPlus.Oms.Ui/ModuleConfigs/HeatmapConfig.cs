using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class HeatmapConfig
    {
        public string UnderlyingQuery { get; set; }
        public HeatMapMode HeatMapMode { get; set; }
        public bool EnableGlobalHeatmap { get; set; }
        public Operator Operator { get; set; }
        public IvChartType ChartType { get; set; }
        public UnderPriceSource UnderPriceSource { get; set; }
        public double Delta { get; set; }
        public int Days { get; set; }
        public int Mins { get; set; }
        public Output Output { get; set; } = Output.Heatmap;
        public string OrderTallySymbol { get; set; }
        public SpreadHeatmapAlert SpreadHeatmapAlert { get; set; } = new();
    }
}
