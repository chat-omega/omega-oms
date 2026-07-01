using System.Windows;
using System.Windows.Controls;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Views
{
    public partial class LatencyIndicatorView : UserControl
    {
        public LatencyIndicatorView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            (DataContext as LatencyIndicatorViewModel)?.Dispose();
        }
    }
}
