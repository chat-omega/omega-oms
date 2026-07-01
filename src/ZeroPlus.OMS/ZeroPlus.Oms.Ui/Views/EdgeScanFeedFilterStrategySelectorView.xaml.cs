using DevExpress.Xpf.Core;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedFilterStrategySelectorView.xaml
    /// </summary>
    public partial class EdgeScanFeedFilterStrategySelectorView : ThemedWindow
    {
        public EdgeScanFeedFilterStrategySelectorView()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
