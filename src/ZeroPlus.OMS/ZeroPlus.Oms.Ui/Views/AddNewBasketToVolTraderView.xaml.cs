using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for AddNewBasketToVolTraderView.xaml
    /// </summary>
    public partial class AddNewBasketToVolTraderView : ThemedWindow
    {
        public AddNewBasketToVolTraderView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
