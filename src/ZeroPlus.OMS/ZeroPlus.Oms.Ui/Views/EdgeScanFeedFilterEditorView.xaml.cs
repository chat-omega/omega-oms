using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Editors;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedFilterEditorView.xaml
    /// </summary>
    public partial class EdgeScanFeedFilterEditorView : UserControl
    {
        public EdgeScanFeedFilterEditorView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
