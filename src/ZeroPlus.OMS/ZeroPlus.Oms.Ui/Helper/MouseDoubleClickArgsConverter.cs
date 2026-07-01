using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Charts;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MouseDoubleClickArgsConverter : EventArgsConverterBase<MouseButtonEventArgs>
    {
        protected override object Convert(object sender, MouseButtonEventArgs args)
        {
            if (args.ClickCount == 2 && sender is ChartControl chart && args != null)
            {
                ChartHitInfo info = chart.CalcHitInfo(args.GetPosition(chart), 3);
                if (info.Pane.Name == "defaultPane")
                {
                    DiagramCoordinates diagramCoordinates = ((XYDiagram2D)chart.Diagram).PointToDiagram(args.GetPosition(chart));
                    return diagramCoordinates;
                }
            }
            return null;
        }
    }
}
