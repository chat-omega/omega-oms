using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SmartRouteEditorView.xaml
    /// </summary>
    public partial class SmartRouteEditorView : ThemedWindow
    {
        public SmartRouteEditorView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit edit)
            {
                edit.SelectAll();
            }
        }
    }
}
