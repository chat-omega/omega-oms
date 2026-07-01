using DevExpress.Xpf.Editors;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DominatorConfigurationView.xaml
    /// </summary>
    public partial class DominatorConfigurationView : UserControl
    {
        public DominatorConfigurationView()
        {
            InitializeComponent();
        }

        private void ToggleEdit(object sender, MouseButtonEventArgs e)
        {
            if (TitleEdit.IsReadOnly)
            {
                TitleEdit.IsReadOnly = false;
                TitleEdit.Focusable = true;
                TitleEdit.Cursor = Cursors.IBeam;
            }
        }

        private void TitleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Reset();
            }
        }

        private void Reset()
        {
            TitleEdit.IsReadOnly = true;
            TitleEdit.Focusable = false;
            TitleEdit.Cursor = Cursors.Arrow;
            TitleEdit.CaretIndex = TitleEdit.Text.Length;
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
