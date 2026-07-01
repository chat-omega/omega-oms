using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LoopDynamicIncrementConfigView.xaml
    /// </summary>
    public partial class LoopDynamicIncrementConfigView : ThemedWindow
    {
        public LoopDynamicIncrementConfigView()
        {
            InitializeComponent();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void CloseWindow(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
