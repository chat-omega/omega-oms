using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for HedgePositionManagementView.xaml
    /// </summary>
    public partial class HedgePositionManagementView : ThemedWindow
    {
        public HedgePositionManagementView()
        {
            InitializeComponent();
        }

        private void SpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is SpinEdit spinEdit)
            {
                spinEdit.SelectAll();
            }
        }
    }
}
