using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for RemoveByWidthPromptView.xaml
    /// </summary>
    public partial class RemoveByWidthPromptView : ThemedWindow
    {
        public RemoveByWidthPromptView()
        {
            InitializeComponent();
        }

        private void PriceSpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void CloseClick(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
