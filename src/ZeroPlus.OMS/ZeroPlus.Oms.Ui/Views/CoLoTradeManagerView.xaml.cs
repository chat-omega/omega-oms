using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for TraderView.xaml
    /// </summary>
    public partial class CoLoTradeManagerView : ThemedWindow
    {
        public CoLoTradeManagerView()
        {
            InitializeComponent();
        }

        public CoLoTradeManagerView(string windowId)
        {
        }

        private void SpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is SpinEdit editBox)
            {
                editBox.SelectAll();
            }
        }
    }
}
