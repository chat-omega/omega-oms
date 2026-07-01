using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DominatorListRollerView.xaml
    /// </summary>
    public partial class DominatorListRollerView : ThemedWindow
    {
        public DominatorListRollerView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is SpinEdit spinEdit)
            {
                spinEdit.SelectAll();
            }
        }
    }
}
