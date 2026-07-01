using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for HunterControlView.xaml
    /// </summary>
    public partial class HunterControlView : UserControl
    {
        public HunterControlView()
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
