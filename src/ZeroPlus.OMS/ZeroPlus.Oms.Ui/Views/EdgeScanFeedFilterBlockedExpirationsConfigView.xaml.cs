using DevExpress.Xpf.Core;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedFilterBlockedExpirationsConfigView.xaml
    /// </summary>
    public partial class EdgeScanFeedFilterBlockedExpirationsConfigView : ThemedWindow
    {
        public EdgeScanFeedFilterBlockedExpirationsConfigView()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
