using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LoopSizeupView.xaml
    /// </summary>
    public partial class LoopSizeupView : ThemedWindow
    {
        public LoopSizeupView()
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
