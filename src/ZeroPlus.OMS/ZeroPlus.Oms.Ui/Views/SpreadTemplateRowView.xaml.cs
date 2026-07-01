using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SpreadTemplateRowView.xaml
    /// </summary>
    public partial class SpreadTemplateRowView : UserControl
    {
        public SpreadTemplateRowView()
        {
            InitializeComponent();
        }

        private void SpinEdit_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is SpinEdit spinEdit)
            {
                spinEdit.SelectAll();
            }
        }
    }
}
