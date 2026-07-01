using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using NLog;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;
using PositionType = ZeroPlus.Models.Data.Enums.PositionType;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PositionsViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial PositionType PositionType { get; set; }

        [Bindable]
        public partial PortfolioModel PortfolioModel { get; set; }

        [Bindable]
        public partial ObservableCollection<IPosition> Positions { get; set; }

        [Command]
        public void PositionDoubleClickCommand(RowClickArgs args)
        {
            if (args == null || args.Item == null || PositionType != PositionType.Symbol)
            {
                return;
            }
            if (args.Item is IPosition position)
            {
                CreateComplexOrderTicket(position);
            }
        }

        internal void LoadPositions(PortfolioModel portfolioModel, PositionType positionType)
        {
            if (portfolioModel != null)
            {
                PortfolioModel = portfolioModel;
                PositionType = positionType;
                switch (positionType)
                {
                    case PositionType.Spread:
                        Positions = portfolioModel.SpreadPositions;
                        ModuleTitle = PortfolioModel.Name + " Spread Details";
                        break;
                    case PositionType.Underlying:
                        Positions = portfolioModel.UnderlyingPositions;
                        ModuleTitle = PortfolioModel.Name + " Underlying Details";
                        break;
                    case PositionType.BaseStrategy:
                        Positions = portfolioModel.SpreadTypePositions;
                        ModuleTitle = PortfolioModel.Name + " Strategy Details";
                        break;
                    case PositionType.Route:
                        Positions = portfolioModel.RoutePositions;
                        ModuleTitle = PortfolioModel.Name + " Route Details";
                        break;
                    case PositionType.Exchange:
                        Positions = portfolioModel.ExchangePositions;
                        ModuleTitle = PortfolioModel.Name + " Exchange Details";
                        break;
                    case PositionType.Symbol:
                        Positions = portfolioModel.SymbolPositions;
                        ModuleTitle = PortfolioModel.Name + " Symbol Details";
                        break;
                }
            }
        }

        private void CreateComplexOrderTicket(IPosition position)
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
                    window.Loaded += (s, e) =>
                    {
                        string spread = position.NetQty == -1 ? position.Name : (position.NetQty * -1) + "*" + position.Name;
                        _ = viewModel.LoadLegsFromTosAsync(spread);
                    };

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }
    }
}
