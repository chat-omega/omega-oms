using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for AvgTimeCloseConfigView.xaml
    /// </summary>
    public partial class AvgTimeCloseConfigView : ThemedWindow
    {
        public AvgTimeCloseConfigView()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SelectAll(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
