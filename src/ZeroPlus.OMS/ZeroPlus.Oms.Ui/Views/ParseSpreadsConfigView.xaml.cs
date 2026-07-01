using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ParseSpreadsConfigView.xaml
    /// </summary>
    public partial class ParseSpreadsConfigView : ThemedWindow
    {
        public ParseSpreadsConfigView()
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
