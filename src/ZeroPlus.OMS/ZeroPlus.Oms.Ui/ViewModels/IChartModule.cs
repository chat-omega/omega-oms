using DevExpress.Xpf.Charts;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public interface IChartModule
    {
        SeriesAggregateFunction AggregateFunction { get; set; }
        Dispatcher Dispatcher { get; set; }
    }
}