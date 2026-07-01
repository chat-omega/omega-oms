using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DrifterControlView.xaml
    /// </summary>
    public partial class DrifterControlView : UserControl
    {
        public DrifterControlView()
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
