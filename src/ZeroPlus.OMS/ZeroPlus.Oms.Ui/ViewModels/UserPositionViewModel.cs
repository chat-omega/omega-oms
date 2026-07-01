using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class UserPositionViewModel : ModuleViewModelBase
    {
        private DelegateCommand<object> _sendToDominatorCommand;

        public override Module Module { get; protected set; } = Module.UserPosition;
        public DominatorsManagerModel DominatorsManagerModel { get; }
        public PortfolioManagerModel PortfolioManagerModel { get; }

        public UserPositionViewModel(PortfolioManagerModel portfolioManagerModel, DominatorsManagerModel dominatorsManagerModel, ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            DominatorsManagerModel = dominatorsManagerModel;
            PortfolioManagerModel = portfolioManagerModel;
        }

        [Command]
        public void RefreshCommand()
        {
            PortfolioManagerModel.Refresh();
        }

        [Command]
        public void ConfigureAlertsCommand()
        {
            PositionAlertConfigurationView view = new();
            view.ShowDialog();
        }

        [Command]
        public void OpenInComplexOrderTicketCommand(UserSpreadPositionModel model)
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        if (OmsCore.Config.UseOrderTicketForSingleLegOrders)
                        {
                            window = new OrderTicketView();
                        }
                        else
                        {
                            switch (OmsCore.Config.DefaultOrderTicketStyle)
                            {
                                case OrderTicketStyle.Complex:
                                    window = new ComplexOrderTicketView();
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView();
                                    break;
                            }
                        }

                        ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => _ = viewModel.LoadLegsFromTosAsync(model.Symbol, model.NetQty < 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, true);
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicketCommand));
            }
        }

        [Command]
        public void RemoveModelCommand(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                if (parameter is IEnumerable<object> selected)
                {
                    foreach (var item in selected)
                    {
                        if (item is UserSpreadPositionModel model)
                        {
                            PortfolioManagerModel.RemoveUserSpreadPosition(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveModelCommand));
            }
        }

        [Command]
        public void ResetHiddenModelsCommand()
        {
            PortfolioManagerModel.ResetHiddenModels();
        }

        public ICommand SendToDominatorCommand
        {
            get
            {
                _sendToDominatorCommand ??= new DelegateCommand<object>(SendToDominator);
                return _sendToDominatorCommand;
            }
        }

        private void SendToDominator(object parameter)
        {
            try
            {
                if (parameter is Tuple<object, DominatorModel> tradeDomPair)
                {
                    PositionSModel model = (PositionSModel)(tradeDomPair?.Item1);
                    DominatorModel dominator = tradeDomPair?.Item2;

                    if (model != null && dominator != null)
                    {
                        TradeForDom trade = new()
                        {
                            UnderSymbol = model.Underlying,
                            Quantity = model.NetQty,
                            Symbol = model.Symbol,
                            Price = model.OpenPositionAveragePrice,
                            UnderPrice = model.OpenPositionFillUnderPrice,
                        };
                        dominator.SendTradeToDominator(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToDominator));
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
