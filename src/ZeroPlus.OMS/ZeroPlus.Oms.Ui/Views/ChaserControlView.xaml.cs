using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ChaserControlView.xaml
    /// </summary>
    public partial class ChaserControlView : UserControl
    {
        public ChaserControlView()
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
