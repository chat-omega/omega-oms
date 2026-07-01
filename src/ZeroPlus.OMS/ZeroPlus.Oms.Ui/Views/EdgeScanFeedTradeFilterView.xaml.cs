using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using NLog;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedTradeFilterView.xaml
    /// </summary>
    public partial class EdgeScanFeedTradeFilterView : ThemedWindow
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public EdgeScanFeedTradeFilterView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit editBase)
            {
                editBase.SelectAll();
            }
        }
    }
}
