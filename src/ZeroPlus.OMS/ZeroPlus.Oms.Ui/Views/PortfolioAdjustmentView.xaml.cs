using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for PortfolioAdjustmentView.xaml
    /// </summary>
    public partial class PortfolioAdjustmentView : ThemedWindow
    {
        private PortfolioAdjustmentViewModel _viewModel;

        public PortfolioAdjustmentViewModel ViewModel => _viewModel ??= DataContext as PortfolioAdjustmentViewModel;

        public PortfolioAdjustmentView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel?.SetDispatcher(Dispatcher);
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
