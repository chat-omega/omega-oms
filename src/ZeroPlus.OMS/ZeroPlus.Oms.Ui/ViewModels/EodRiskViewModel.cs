using DevExpress.Mvvm;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class EodRiskViewModel : ModuleViewModelBase
    {
        private DelegateCommand<object> _filterInNewOrderBookCommand;

        public override Module Module { get; protected set; } = Module.EodRisk;

        public ICommand FilterInNewOrderBookCommand
        {
            get
            {
                _filterInNewOrderBookCommand ??= new DelegateCommand<object>(FilterInNewOrderBook);
                return _filterInNewOrderBookCommand;
            }
        }

        public PortfolioManagerModel PortfolioManagerModel { get; }

        public EodRiskViewModel(PortfolioManagerModel portfolioManager, ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            PortfolioManagerModel = portfolioManager;
        }

        private void FilterInNewOrderBook(object parameter)
        {
            try
            {
                if (parameter is string filterString)
                {
                    OrderBookWindowView orderbookWindow = new();
                    OrderBookViewModel viewModel = (OrderBookViewModel)orderbookWindow.DataContext;
                    orderbookWindow.OrderBookView.Ready += () =>
                    {
                        viewModel.ShowWorkingOrders = false;
                        viewModel.FilterString = filterString;
                        orderbookWindow.OrderBookView.HideWorkingOrders();
                    };
                    orderbookWindow.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FilterInNewOrderBook));
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return default;
        }

        public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return Task.CompletedTask;
        }
    }
}
