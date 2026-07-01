using DevExpress.Xpf.Core;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for BasketBlockListConfigView.xaml
    /// </summary>
    public partial class BasketBlockListConfigView : ThemedWindow
    {
        public BasketBlockListConfigView()
        {
            InitializeComponent();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
