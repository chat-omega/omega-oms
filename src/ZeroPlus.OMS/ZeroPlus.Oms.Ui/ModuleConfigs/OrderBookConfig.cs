using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class OrderBookConfig
    {
        public bool ShowWorkingOrdersGrid { get; set; }
        public FilterType FilterType { get; set; }
        public bool AutoScroll { get; set; }
        public string SplitterHeight { get; set; }
        public string FilterString { get; set; }
        public bool CancelOnTimer { get; set; }
        public int CancelIntervalSec { get; set; } = 60;
        public int CancelDeltaSec { get; set; } = 0;
    }
}