using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PortfolioViewModel : CustomizableTableViewModelBase
    {
        private static readonly string MODULE_TITLE = "Portfolio";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();


        public IUiUpdateService UiUpdateService => GetService<IUiUpdateService>();
        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        public bool ShowPositionManager => OmsCore.GatewayClient.GrantedModules.Contains((int)Module.PositionManager);

        public OmsCore OmsCore { get; }
        public PositionUpdateConsumer PositionUpdateConsumer { get; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial TheoPriceInfoModel TheoPriceInfoModel { get; set; }
        [Bindable]
        public partial ObservableCollection<string> Accounts { get; set; }
        [Bindable]
        public partial string SelectedAccount { get; set; }
        [Bindable]
        public partial ObservableCollection<PositionModel> Positions { get; set; }

        public PortfolioViewModel(OmsCore omsCore, PositionUpdateConsumer positionUpdateConsumer)
        {
            OmsCore = omsCore;
            PositionUpdateConsumer = positionUpdateConsumer;
            positionUpdateConsumer.OpenedPortfolioWindowsCount++;
            if (PositionUpdateConsumer.OpenedPortfolioWindowsCount == 1)
            {
                OmsCore.OrderClient.FirstPortfolioWindowOpened();
            }

            ModuleTitle = MODULE_TITLE;
            Accounts = PositionUpdateConsumer.Accounts;
            SelectedAccount = Accounts.FirstOrDefault();
            AccountChanged();

            PositionUpdateConsumer.BlockUiEvent += OnBlockUiEvent;
            PositionUpdateConsumer.UnblockUiEvent += OnUnblockUiEvent;

            TheoPriceInfoModel = new TheoPriceInfoModel();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void MarketDataSubscriptionModeChangedCommand()
        {
            PositionUpdateConsumer.UpdateSubscriptions(PositionUpdateConsumer.MarketDataSubscriptionMode);
        }

        [Command]
        public void AddColumn()
        {
            AddColumnView addColumnView = new();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent += OnAddColumnEvent;
            addColumnView.ShowDialog();
            ((AddColumnViewModel)addColumnView.DataContext).AddColumnEvent -= OnAddColumnEvent;
        }

        private void OnAddColumnEvent(CustomColumnTemplateModel colTemplate)
        {
            LoadCustomColumnService.AddCustomColumn(colTemplate);
        }

        [Command]
        public void AccountChanged()
        {
            if (PositionUpdateConsumer.GetPositionsCollection(SelectedAccount, out ObservableCollection<PositionModel> positionsCollection))
            {
                Positions = positionsCollection;
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage("Collection not found.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning)
                ));
            }
        }

        [Command]
        public void OpenInComplexOrderTicket(object positionsIn)
        {
            try
            {
                if (positionsIn == null)
                {
                    return;
                }

                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) &&
                    positionsIn is IEnumerable<object> positionHolder)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        if (positionHolder.Count() <= 1 && OmsCore.Config.UseOrderTicketForSingleLegOrders)
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
                        window.Loaded += (s, e) => _ = viewModel.LoadFromPositionAsync(positionHolder.Select(x => (PositionModel)x).ToList());
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        [Command]
        public void OpenInGammaScalperCommand(object positionsIn)
        {
            try
            {
                if (positionsIn == null)
                {
                    return;
                }

                if (positionsIn is IEnumerable<object> positionHolder)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        List<PositionModel> positions = positionHolder.Select(x => (PositionModel)x).ToList();
                        GammaScalpingModuleView view = new();
                        if (view.DataContext is GammaScalpingModuleViewModel viewModel)
                        {
                            viewModel.Account = OmsCore.Config.DefaultAccount;
                            _ = viewModel.OrderTicket.LoadFromPositionAsync(positions);

                            view.Show();
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInGammaScalperCommand));
            }
        }

        [Command]
        public void OpenPositionManagerCommand(ObservableCollectionCore<object> positionHolder)
        {
            try
            {
                if (positionHolder == null)
                {
                    return;
                }

                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.PositionManager))
                {
                    foreach (PositionModel position in positionHolder)
                    {
                        PositionManagerView view = new();
                        PositionManagerViewModel viewModel = (PositionManagerViewModel)view.DataContext;
                        viewModel.LoadFromModel(position);
                        view.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenPositionManagerCommand));
            }
        }

        [Command]
        public void Clone()
        {
            try
            {
                PortfolioView portfolioWindow = new();
                portfolioWindow.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (_, _) =>
                {
                    viewModel?.SetModule(Module.PortfolioLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        internal void Dispose()
        {
            foreach (PositionModel position in Positions)
            {
                position.Dispose();
            }
            PositionUpdateConsumer.BlockUiEvent -= OnBlockUiEvent;
            PositionUpdateConsumer.UnblockUiEvent -= OnUnblockUiEvent;

            PositionUpdateConsumer.OpenedPortfolioWindowsCount--;
            if (PositionUpdateConsumer.OpenedPortfolioWindowsCount == 0)
            {
                OmsCore.OrderClient.AllPortfolioWindowsClosed();
            }
        }

        private void OnBlockUiEvent()
        {
            UiUpdateService?.BeginUpdate();
        }

        private void OnUnblockUiEvent()
        {
            UiUpdateService?.EndUpdate();
        }
    }
}
