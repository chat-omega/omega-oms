using DevExpress.Xpf.Core;
using System.Windows;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for FeesEstimateView.xaml
    /// </summary>
    public partial class FeesEstimateView : ThemedWindow
    {
        public FeesEstimateView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
