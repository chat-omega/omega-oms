using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Resources;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Xsl;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Formatting = Newtonsoft.Json.Formatting;
using Side = ZeroPlus.Models.Data.Enums.Side;
using Window = System.Windows.Window;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum FilterType { ALL, OPEN, FILLED, CLOSED, STAGED, UNIQUE, UNIQUE_ORDERS }

    public partial class OrderBookViewModel : CustomizableTableViewModelBase
    {
        private static readonly string MODULE_TITLE = "Order Book";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public static List<BasketAutoPermModel> AutoPermSettings { get; internal set; }

        private readonly TransactionConsumerModel _transactionConsumer;
        private readonly IModuleFactory _moduleFactory;

        private DelegateCommand<object> _filterInNewOrderBookCommand;
        private DelegateCommand<object> _searchInNewTradesModuleCommand;
        private DelegateCommand<object> _chartOrdersCommand;
        private DelegateCommand<object> _blockFromDomCommand;
        private DelegateCommand<object> _sendToDominatorCommand;
        private DelegateCommand<object> _chartSymbolBidAskIvCommand;
        private DelegateCommand<object> _buildSpreadTemplateFromSelectedCommand;
        private DelegateCommand<Tuple<OmsOrderModel, ConfigSave>> _openInBasketTraderCommand;
        private DelegateCommand<Tuple<OmsOrderModel, ConfigSave>> _addToListCommand;
        private DelegateCommand<Tuple<OmsOrderModel, string>> _openInNagbotBasketTraderCommand;
        private DelegateCommand<Tuple<OmsOrderModel, BasketAutoPermModel, ConfigSave>> _openInBasketAndAutoPermCommand;
        private DelegateCommand<object> _sendToNagBotCommand;

        private DispatcherTimer _cancelCountdownTimer;
        private bool _cancelRunning;

        public OmsCore OmsCore { get; }
        public bool IsDisposed { get; set; }
        public string Uid { get; set; }
        public string ParentUid { get; internal set; }
        public Dispatcher Dispatcher { get; set; }
        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        public IUiUpdateService UiUpdateService => GetService<IUiUpdateService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public TextWrapping HeaderTextWrapping => OmsCore.Config.WrapColumnHeaderV2 ? TextWrapping.WrapWithOverflow : TextWrapping.NoWrap;
        public OrderBookConfig OrderBookViewModelConfig { get; set; } = new OrderBookConfig();
        public PortfolioManagerModel PortfolioManager { get; }
        public DominatorsManagerModel DominatorsManagerModel { get; }
        public bool UserScroll { get; set; }
        public bool PortfolioAdjustmentModuleGranted => OmsCore.GatewayClient.GrantedModules.Contains((int)Module.PortfolioAdjustment);
        [Bindable]
        public partial bool IsSubscribedToAllOrOwnAndAll { get; set; }

        public ObservableCollection<ConfigSave> AdminConfigs { get; } = [];
        [Bindable]
        public partial ConfigSave SelectedConfig { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial FilterType FilterType { get; set; }
        [Bindable]
        public partial bool ShowWorkingOrders { get; set; }
        [Bindable]
        public partial ObservableCollection<OmsOrderModel> WorkingOrders { get; set; }
        [Bindable]
        public partial ObservableCollection<object> VisibleWorkingOrders { get; set; }
        [Bindable]
        public partial ObservableCollection<OmsOrderModel> ClosedOrders { get; set; }
        [Bindable]
        public partial bool AutoScroll { get; set; }
        [Bindable]
        public partial string FilterString { get; set; }
        [Bindable]
        public partial OmsOrderModel LatestFilledRow { get; set; }
        [Bindable]
        public partial bool CancelOnTimer { get; set; }
        [Bindable]
        public partial int CancelIntervalSec { get; set; }
        [Bindable]
        public partial TimeSpan CancelCountDown { get; set; }
        [Bindable]
        public partial int CancelDeltaSec { get; set; }

        public ConfigSave ConfigSave { get; set; }
        public BasketGroupManagerModel BasketGroupManagerModel { get; }

        static OrderBookViewModel()
        {
            string path = Path.Combine(OmsCore.Config.GetWorkspaceDirectory(), "BasketAutoPermModels.json");
            if (File.Exists(path))
            {
                Task.Run(() =>
                {
                    string content = File.ReadAllText(path);
                    List<BasketAutoPermModel> models = JsonConvert.DeserializeObject<List<BasketAutoPermModel>>(content);
                    AutoPermSettings = models?.DistinctBy(x => x.Title).ToList();
                });
            }
        }

        public OrderBookViewModel(DominatorsManagerModel dominatorsManagerModel,
                                  TransactionConsumerModel transactionConsumer,
                                  PortfolioManagerModel portfolioManagerModel,
                                  BasketGroupManagerModel basketGroupManagerModel,
                                  IModuleFactory moduleFactory,
                                  OmsCore omsCore)
        {
            OmsCore = omsCore;
            _transactionConsumer = transactionConsumer;
            _moduleFactory = moduleFactory;
            PortfolioManager = portfolioManagerModel;
            BasketGroupManagerModel = basketGroupManagerModel;
            DominatorsManagerModel = dominatorsManagerModel;
            ModuleTitle = MODULE_TITLE;
            _isSubscribedToAllOrOwnAndAll = _transactionConsumer.IsSubscribedToAllOrOwnAndAll;
            VisibleWorkingOrders = new ObservableCollection<object>();
            WorkingOrders = _transactionConsumer.WorkingOrdersCollection;
            ClosedOrders = _isSubscribedToAllOrOwnAndAll ? _transactionConsumer.ClosedOrdersCollection : _transactionConsumer.FilledOrdersCollection;
            _transactionConsumer.ClosedOrderRowAddedEvent += OnClosedOrderRowAddedEvent;
            _transactionConsumer.BlockUiEvent += OnBlockUiEvent;
            _transactionConsumer.UnblockUiEvent += OnUnblockUiEvent;
            OmsCore.HerculesClient.ClientConnected += OnHerculesReconnected;

            CancelIntervalSec = 60;
            _cancelCountdownTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1),
            };
        }

        private void OnHerculesReconnected()
        {
            try
            {
                var dispatcher = Dispatcher ?? _transactionConsumer.Dispatcher;
                dispatcher.BeginInvoke(() =>
                {
                    IsSubscribedToAllOrOwnAndAll = _transactionConsumer.IsSubscribedToAllOrOwnAndAll;
                    ChangeFilter(FilterType.FILLED);
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnHerculesReconnected));
            }
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        #region Commands
        [Command]
        public void ViewLoaded()
        {
        }

        [Command]
        public async Task GetAuditTrail(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is OmsOrderModel orderModel)
                {
                    XmlDocument transactionHistory = await OmsCore.HerculesClient.RequestAuditTrailAsync(orderModel.PermID);
                    if (transactionHistory == null)
                    {
                        return;
                    }

                    Uri templateUri = new("pack://application:,,,/Helper/AuditTrail.xsl");
                    StreamResourceInfo streamReader = Application.GetResourceStream(templateUri);
                    XmlReader xmlReader = XmlReader.Create(streamReader.Stream);
                    XslCompiledTransform compiledTransform = new();
                    compiledTransform.Load(xmlReader);
                    string tempLocation = Path.GetTempFileName() + ".html";
                    XmlTextWriter results = new(tempLocation, null);
                    compiledTransform.Transform(transactionHistory, results);
                    Process.Start(new ProcessStartInfo { FileName = tempLocation, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetAuditTrail));
            }
        }

        [Command]
        public void ShowLegDetailsCommand(object parameter)
        {
            try
            {
                if (parameter is not null and OmsOrderModel orderModel)
                {
                    ComplexOrderLegsView orderDetailsView = new();
                    if (orderDetailsView.DataContext is ComplexOrderLegsViewModel viewModel)
                    {
                        viewModel.Uid = Uid;
                        viewModel.Order = orderModel;
                        viewModel.Orders.Add(orderModel);
                        orderDetailsView.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLegDetailsCommand));
            }
        }

        [Command]
        public void OpenOrderDetailsCommand(object parameter)
        {
            try
            {
                if (parameter is not null and OmsOrderModel orderModel)
                {
                    orderModel.LoadingDetails = true;
                    string url = $"http://orderdetails.corp.zeroplusderivatives.com/?orderId={orderModel.PermID}&user={OmsCore.User.Username}";

                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = uri.ToString(),
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        _log.Warn("Invalid order details URL: {Url}", url);
                    }

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenOrderDetailsCommand));
            }
            finally
            {
                if (parameter is OmsOrderModel orderModel)
                {
                    orderModel.LoadingDetails = false;
                }
            }
        }

        [Command]
        public void TagOrderCommand(object parameter)
        {
            try
            {
                if (parameter is not null and OmsOrderModel orderModel)
                {
                    OrderTaggerView view = new();
                    if (view.DataContext is OrderTaggerViewModel viewModel)
                    {
                        viewModel.OrderId = orderModel.PermID;
                        viewModel.SpreadId = orderModel.SpreadId;
                        viewModel.Tagger = OmsCore.User.Username;
                        view.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TagOrderCommand));
            }
        }

        [Command]
        public void RowDoubleClick(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is OmsOrderModel orderModel)
            {
                OpenInComplexOrderTicket(orderModel);
            }
        }

        [Command]
        public void OpenInPositionAnalyzerCommand(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) &&
                    parameter is OmsOrderModel orderModel)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        PositionAnalyzerView view = new();
                        view.Loaded += (s, e) =>
                        {
                            if (view.DataContext is PositionAnalyzerViewModel viewModel)
                            {
                                viewModel.InputString = orderModel.Symbol;
                                viewModel.BasePrice = orderModel.Price;
                                viewModel.Side = orderModel.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                                viewModel.Quantity = orderModel.Quantity;
                                _ = viewModel.AddCommand();
                            }
                        };
                        view.Show();
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInPositionAnalyzerCommand));
            }
        }

        [Command]
        public void OpenInComplexOrderTicket(OmsOrderModel orderModel)
        {
            try
            {
                bool loadExact = !orderModel.OrderStatus.IsClosed() && (orderModel.OrderSource != OrderSource.OMS || OmsCore.User.Username.Equals(orderModel.Tag, StringComparison.OrdinalIgnoreCase));
                OpenTicket(orderModel, loadExact: loadExact);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        private void OpenTicket(OmsOrderModel orderModel, bool loadExact)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    if (orderModel.Legs is not { Count: > 1 } && OmsCore.Config.UseOrderTicketForSingleLegOrders)
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
                    window.Loaded += (s, e) => viewModel.LoadFromOrderBookAsync(orderModel, loadExact)
                        .ContinueWith(t =>
                        {
                            if (OmsCore.Config.TicketLockEdgeToTheoWhenLoadingFromOrderbook)
                            {
                                viewModel.TemplateEdgeToTheo = orderModel.EdgeToTheo;
                                viewModel.EdgeToTheoLocked = true;
                                _ = viewModel.UseEdgeToTheoAsync(orderModel.EdgeToTheo);
                            }
                            if (OmsCore.Config.OpenSeparateTicketForUnderlying)
                            {
                                double left = 10;
                                double top = 20;
                                double width = 600;
                                double height = 300;
                                window.Dispatcher.Invoke(new Action(() =>
                                {
                                    left = window.Left;
                                    top = window.Top;
                                    width = window.Width;
                                    height = window.Height;
                                }));
                                _ = viewModel.OpenUnderlyingTicket(left, top, width, height);
                            }
                        });
                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();

            }
        }

        [Command]
        public void OpenInBasketTrader(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    if (parameter is OmsOrderModel orderModel)
                    {
                        if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
                            {
                                ViewModel: BasketTraderViewModel viewModel
                            } view)
                        {
                            if (viewModel.IsReady)
                            {
                                Task.Run(() => OnReady(viewModel));
                            }
                            else
                            {
                                viewModel.Ready += OnReady;

                            }

                            void OnReady(IModuleViewModel _)
                            {
                                viewModel.Ready -= OnReady;
                                viewModel.LoadFromOrderModelAsync(orderModel);
                            }
                        }
                    }
                    else if (parameter is Tuple<OmsOrderModel, ConfigSave> config)
                    {
                        if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                        {
                            if (viewModel.IsReady)
                            {
                                Task.Run(() => OnReady(viewModel));
                            }
                            else
                            {
                                viewModel.Ready += OnReady;

                            }

                            async void OnReady(IModuleViewModel _)
                            {
                                viewModel.Ready -= OnReady;
                                ConfigSave configSave = config.Item2;
                                configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                                view.RestoreFromConfigSave(configSave);
                                viewModel.LoadFromOrderModelAsync(config.Item1);
                            }
                        }
                    }
                    else if (parameter is Tuple<OmsOrderModel, string> configPair)
                    {
                        if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                        {
                            if (viewModel.IsReady)
                            {
                                Task.Run(() => OnReady(viewModel));
                            }
                            else
                            {
                                viewModel.Ready += OnReady;

                            }

                            void OnReady(IModuleViewModel _)
                            {
                                viewModel.Ready -= OnReady;
                                viewModel.LoadNagbotFromOrderModelAsync(configPair.Item1);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        [Command]
        public void RemoveFromTodaysOrderbook()
        {
            try
            {
                var orders = ClosedOrders.Where(o => o.RemoveFromTodaysOrderbook && o.SubType == OrderSubType.ManualAdd);

                // Ensure this flag is switched
                foreach (var order in orders)
                    order.AddToTodaysOrderbook = false;

                List<string> permIds = [.. orders.Select(o => o.PermID)];
                if (permIds.Count != 0)
                    OmsCore.HerculesClient.AddRemoveMultipleTrades(add: false, permIds);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveFromTodaysOrderbook));
            }
        }

        private void OpenInBasketAndAutoPerm(Tuple<OmsOrderModel, BasketAutoPermModel, ConfigSave> parameter)
        {
            try
            {
                OmsOrderModel orderModel = parameter.Item1;
                BasketAutoPermModel autoPermModel = parameter.Item2;
                ConfigSave configSave = parameter.Item3;
                if (orderModel == null || autoPermModel == null)
                {
                    _log.Warn(nameof(OpenInBasketAndAutoPerm) + " Invalid input.");
                    return;
                }

                if (!autoPermModel.AutoPermConfigs.Any())
                {
                    _log.Warn(nameof(OpenInBasketAndAutoPerm) + " Invalid input.");
                    MessageBoxService?.ShowMessage($"Invalid perm template. {autoPermModel.Title} for {orderModel.SpreadId}", "Basket Auto Perm");
                    return;
                }
                if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                {
                    if (viewModel.IsReady)
                    {
                        Task.Run(() => OnReady(viewModel));
                    }
                    else
                    {
                        viewModel.Ready += OnReady;
                    }

                    async void OnReady(IModuleViewModel _)
                    {
                        viewModel.Ready -= OnReady;
                        await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id).ContinueWith(async t =>
                            {
                                if (t.IsCompletedSuccessfully)
                                {
                                    var configSave = t.Result;
                                    await viewModel.Dispatcher.BeginInvoke(() => view.RestoreFromConfigSave(configSave, false));
                                    BasketTraderItemModel order = await viewModel.LoadFromOrderModelAsync(orderModel);
                                    double lastEdge = 0;
                                    AutoPermConfigModel selectedConfig = BasketTraderViewModel.SelectAutoPermConfig(order, lastEdge, autoPermModel.AutoPermConfigs, autoPermModel.AutoPermSelectionMode);
                                    if (selectedConfig != null)
                                    {
                                        int gen = order.PermGen + 1;
                                        if (gen <= selectedConfig.MaxGenForPerms)
                                        {
                                            await viewModel.LoadAutoPerms(order, lastEdge, selectedConfig, sendOrders: false);
                                        }
                                        else
                                        {
                                            _log.Info($"{nameof(OpenInBasketAndAutoPerm)} Max Perm Generation reached. {order.SpreadId}, Gen: {gen}");
                                        }
                                    }
                                }
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketAndAutoPerm));
            }
        }

        public ICommand FilterInNewOrderBookCommand
        {
            get
            {
                _filterInNewOrderBookCommand ??= new DelegateCommand<object>(FilterInNewOrderBook);
                return _filterInNewOrderBookCommand;
            }
        }

        public ICommand SearchInNewTradesModuleCommand
        {
            get
            {
                _searchInNewTradesModuleCommand ??= new DelegateCommand<object>(SearchInNewTradesModule);
                return _searchInNewTradesModuleCommand;
            }
        }

        public ICommand ChartOrdersCommand
        {
            get
            {
                _chartOrdersCommand ??= new DelegateCommand<object>(ChartOrders);
                return _chartOrdersCommand;
            }
        }

        public ICommand BlockFromDomCommand
        {
            get
            {
                _blockFromDomCommand ??= new DelegateCommand<object>(BlockFromDom);
                return _blockFromDomCommand;
            }
        }

        public ICommand SendToDominatorCommand
        {
            get
            {
                _sendToDominatorCommand ??= new DelegateCommand<object>(SendToDominator);
                return _sendToDominatorCommand;
            }
        }

        public ICommand SendToNagBotCommand
        {
            get
            {
                _sendToNagBotCommand ??= new DelegateCommand<object>(SendToNagBot);
                return _sendToNagBotCommand;
            }
        }

        public ICommand AddToListCommand
        {
            get
            {
                _addToListCommand ??= new DelegateCommand<Tuple<OmsOrderModel, ConfigSave>>(AddToList);
                return _addToListCommand;
            }
        }

        public ICommand ChartSymbolBidAskIvCommand
        {
            get
            {
                _chartSymbolBidAskIvCommand ??= new DelegateCommand<object>(ChartSymbolBidAskIv);
                return _chartSymbolBidAskIvCommand;
            }
        }

        public ICommand BuildSpreadTemplateFromSelectedCommand
        {
            get
            {
                _buildSpreadTemplateFromSelectedCommand ??= new DelegateCommand<object>(BuildSpreadTemplateFromSelected);
                return _buildSpreadTemplateFromSelectedCommand;
            }
        }

        public ICommand OpenInBasketTraderWithConfigCommand
        {
            get
            {
                _openInBasketTraderCommand ??= new DelegateCommand<Tuple<OmsOrderModel, ConfigSave>>(OpenInBasketTrader);
                return _openInBasketTraderCommand;
            }
        }

        public ICommand OpenInBasketAndAutoPermCommand
        {
            get
            {
                _openInBasketAndAutoPermCommand ??= new DelegateCommand<Tuple<OmsOrderModel, BasketAutoPermModel, ConfigSave>>(OpenInBasketAndAutoPerm);
                return _openInBasketAndAutoPermCommand;
            }
        }

        public ICommand OpenInNagbotBasketTraderCommand
        {
            get
            {
                _openInNagbotBasketTraderCommand ??= new DelegateCommand<Tuple<OmsOrderModel, string>>(OpenInBasketTrader);
                return _openInNagbotBasketTraderCommand;
            }
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

        private void SearchInNewTradesModule(dynamic parameter)
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades) &&
                    parameter != null)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        TradesView window = new();
                        TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        viewModel.Ready += (IModuleViewModel module) =>
                        {
                            viewModel.Symbol = parameter.SearchTerm;
                            viewModel.SelectedTime = "Today";
                            viewModel.LegTypes = parameter.MLeg ? LegTypes.MLeg : LegTypes.Single;

                            if (parameter.ContainsTimeRange && OmsCore.Config.OrderbookIdentifyEdgeScanTrades)
                            {
                                viewModel.Key = parameter.Key;
                                viewModel.Keys = parameter.Keys;
                                viewModel.UseManualTime = true;
                                viewModel.StartTime = parameter.MinTime;
                                viewModel.EndTime = parameter.MaxTime;
                            }

                            viewModel.FilterString = parameter.Filter;
                            viewModel.Refresh();
                        };
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchInNewTradesModule));
            }
        }

        [Command]
        public void CancelOrder(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }

                if (parameter is IEnumerable<object> selectedOrders)
                {
                    var orders = selectedOrders.Select(x => (OmsOrderModel)x).ToList();
                    if (orders.Any(x => !OmsCore.User.Username.Equals(x.Tag, StringComparison.OrdinalIgnoreCase)))
                    {
                        var confirm = MessageBoxService.ShowMessage(
                            "You are about to cancel orders that are placed by other traders.\nAre you sure you want to proceed?", "Cancel", MessageButton.YesNoCancel, MessageIcon.Warning, MessageResult.No);

                        switch (confirm)
                        {
                            case MessageResult.Cancel:
                                return;
                            case MessageResult.No:
                                orders = orders.Where(x => OmsCore.User.Username.Equals(x.Tag, StringComparison.OrdinalIgnoreCase)).ToList();
                                break;
                        }
                    }

                    foreach (OmsOrderModel orderModel in orders)
                    {
                        Cancel(orderModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelOrder));
            }
        }

        [Command]
        public void Clone()
        {
            OrderBookWindowView cloneWindow = new();
            cloneWindow.Loaded += (object s, RoutedEventArgs e) =>
            {
                if (!string.IsNullOrWhiteSpace(ParentUid))
                {
                    cloneWindow.CloneFrom(ParentUid);
                }
                else
                {
                    cloneWindow.CloneFrom(Uid);
                }
            };
            cloneWindow.Show();
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = Module.OrderBook;

                viewModel.Config = GetConfig();

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void SaveConfigOnServer()
        {
            try
            {
                SaveView view = new();

                SaveViewModel viewModel = view.DataContext as SaveViewModel;
                viewModel.LoadGroups(Module.OrderBook);

                viewModel.Config = GetConfig();

                if (ConfigSave != null)
                {
                    viewModel.Id = ConfigSave.Id;
                    viewModel.Title = ConfigSave.Title;
                    viewModel.SelectedGroup = ConfigSave.Group;
                }

                view.ShowDialog();

                if (!string.IsNullOrWhiteSpace(viewModel.Title) && viewModel.Success)
                {
                    ModuleTitle = viewModel.Title;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveConfigOnServer));
            }
        }

        [Command]
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.OrderBookLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void ChangeFilter(FilterType filterType)
        {
            FilterType = filterType;
            switch (filterType)
            {
                case FilterType.ALL:
                    ClosedOrders = _transactionConsumer.ClosedOrdersCollection;
                    break;
                case FilterType.UNIQUE_ORDERS:
                    ClosedOrders = _transactionConsumer.UniqueOrdersCollection;
                    break;
                case FilterType.FILLED:
                    ClosedOrders = _transactionConsumer.FilledOrdersCollection;
                    break;
                case FilterType.UNIQUE:
                    ClosedOrders = _transactionConsumer.UniqueFillsCollection;
                    break;
            }
            LatestFilledRow = null;
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
        public void CancelAllMatrixOrdersCommand()
        {
            OmsCore.AutoTraderClient.CancelOrder(new CancelRequest()
            {
                Account = OmsCore.Config.DefaultAccount,
                Venue = Venue.Matrix,
                LocalId = "<ALL>",
            });
        }

        [Command]
        public void CancelAll()
        {
            CancelAllVisible(checkForRestingTime: false);
        }

        [Command]
        public void CancelAllBuys()
        {
            try
            {
                foreach (object order in VisibleWorkingOrders.ToList())
                {
                    if (order is OmsOrderModel orderUiModel && orderUiModel.Price >= 0)
                    {
                        Cancel(orderUiModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllBuys));
            }
        }

        [Command]
        public void CancelAllSells()
        {
            try
            {
                foreach (object order in VisibleWorkingOrders.ToList())
                {
                    if (order is OmsOrderModel orderUiModel && orderUiModel.Price < 0)
                    {
                        Cancel(orderUiModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllSells));
            }
        }

        [Command]
        public void CancelAllSelected(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                if (parameter is IEnumerable<object> orderModelsSelected)
                {
                    foreach (object item in orderModelsSelected.ToList())
                    {
                        Cancel((OmsOrderModel)item);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllSelected));
            }
        }
        #endregion

        private void StartCancelTimer()
        {
            _cancelCountdownTimer.Stop();
            _cancelCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _cancelCountdownTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            int remaining = CancelIntervalSec;
            CancelCountDown = TimeSpan.FromSeconds(remaining);
            _cancelCountdownTimer.Tick += (_, a) =>
            {
                if (!CancelOnTimer || IsDisposed)
                {
                    _cancelCountdownTimer.Stop();
                    CancelCountDown = TimeSpan.FromSeconds(CancelIntervalSec);
                }
                else if (remaining-- <= 0)
                {
                    _cancelCountdownTimer.Stop();
                    if (!_cancelRunning)
                    {
                        CancelAllVisible(checkForRestingTime: true);
                    }
                }
                else
                {
                    CancelCountDown = TimeSpan.FromSeconds(remaining);
                }
            };

            if (CancelOnTimer)
            {
                _cancelCountdownTimer.Start();
            }
        }

        private void CancelAllVisible(bool checkForRestingTime)
        {
            try
            {
                _cancelRunning = true;
                List<OmsOrderModel> orders = VisibleWorkingOrders.Select(x => (OmsOrderModel)x).ToList();
                if (orders.Any(x => !OmsCore.User.Username.Equals(x.Tag, StringComparison.OrdinalIgnoreCase)))
                {
                    var confirm = MessageBoxService.ShowMessage(
                        "You are about to cancel orders that are placed by other traders.\nAre you sure you want to proceed?", "Cancel", MessageButton.YesNoCancel, MessageIcon.Warning, MessageResult.No);

                    switch (confirm)
                    {
                        case MessageResult.Cancel:
                            return;
                        case MessageResult.No:
                            orders = orders.Where(x => OmsCore.User.Username.Equals(x.Tag, StringComparison.OrdinalIgnoreCase)).ToList();
                            break;
                    }
                }

                foreach (OmsOrderModel orderUiModel in orders)
                {
                    if (!checkForRestingTime || DateTime.Now.ToUniversalTime() - orderUiModel.SubmitTime.ToUniversalTime() > TimeSpan.FromSeconds(CancelDeltaSec))
                    {
                        Cancel(orderUiModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelAllVisible));
            }
            finally
            {
                _cancelRunning = false;
            }
        }

        private void Cancel(OmsOrderModel orderModel)
        {
            if (OmsCore.Config.IsAlgoRoute(route: orderModel.Route))
            {
                _log.Warn("Message: 'Cancel not allowed in algo routes!', Id: '{}', Route: '{}'", orderModel.OrderID, orderModel.Route);
                return;
            }

            double restTime = (DateTime.Now - orderModel.SubmitTime).TotalMicroseconds;
            if (!ValidMinRestPeriod(orderModel, restTime))
            {
                _log.Warn("Message: 'Premature cancel!', Id: '{}', Route: '{}', Time: '{}'", orderModel.OrderID, orderModel.Route, restTime);
                return;
            }

            if (!string.IsNullOrWhiteSpace(orderModel.OrderID))
            {
                CancelRequest cancelRequest = new()
                {
                    LocalId = orderModel.LocalID ?? "",
                    PermId = orderModel.PermID ?? "",
                    OrderId = orderModel.OrderID ?? "",
                    Account = orderModel.AccountAcronym ?? "",
                    Venue = orderModel.Venue,
                };
                if (orderModel.OrderSource == OrderSource.AutoTrader)
                {
                    OmsCore.AutoTraderClient.CancelOrder(cancelRequest);
                }
                else
                {
                    OmsCore.OrderClient.CancelOrder(cancelRequest);
                }
            }
        }

        public bool ValidMinRestPeriod(OmsOrderModel orderModel, double restTime)
        {
            if (orderModel.UnderlyingSymbol == "$SPX")
            {
                if (!orderModel.IsComplexOrder)
                {
                    return OrderTicket.SPX_AUCTION <= restTime;
                }
                else
                {
                    return OrderTicket.SPX_SPREAD_AUCTION <= restTime;
                }
            }
            else
            {
                if (!orderModel.IsComplexOrder)
                {
                    return OrderTicket.SINGLE_LEG_AUCTION <= restTime;
                }
                else
                {
                    return OrderTicket.SPREAD_AUCTION <= restTime;
                }
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

        private void OnClosedOrderRowAddedEvent(OmsOrderModel orderModel)
        {
            if (_autoScroll &&
                (((FilterType == FilterType.FILLED || FilterType == FilterType.UNIQUE) &&
                orderModel.FilledQty > 0) ||
                FilterType == FilterType.ALL || FilterType == FilterType.UNIQUE_ORDERS))
            {
                LatestFilledRow = orderModel;
                UserScroll = false;
            }
        }

        private void BuildSpreadTemplateFromSelected(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    List<ChartValueModel> values = new();
                    List<OmsOrderModel> orders = new();

                    foreach (object tmp in (ObservableCollectionCore<object>)parameter.Orders)
                    {
                        if (tmp is OmsOrderModel order)
                        {
                            orders.Add(order);
                        }
                    }

                    if (orders.Count > 0)
                    {
                        string unders = string.Join(", ", orders.Select(x => x.UnderlyingSymbol).Distinct());
                        List<string> templates = new();

                        foreach (OmsOrderModel order in orders)
                        {
                            SpreadTemplateRowConfig rowConfig = new()
                            {
                                EdgeOverride = order.EdgeOverride,
                                Strategy = (BaseStrategy)order.BaseStrategy
                            };

                            if (order.Legs != null)
                            {
                                if (order.Legs.Count > 0)
                                {
                                    ZeroPlus.Models.Data.Trading.Interfaces.IComplexOrderLeg leg = order.Legs.ElementAt(0);
                                    rowConfig.Side = order.Side ?? Side.Buy;
                                    rowConfig.Leg1Delta = Math.Abs(leg.Delta);
                                    if (leg.Symbol.StartsWith("."))
                                    {
                                        Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                                        rowConfig.Leg1Expiration = option.Expiration;
                                    }
                                }

                                if (order.Legs.Count > 1)
                                {
                                    ZeroPlus.Models.Data.Trading.Interfaces.IComplexOrderLeg leg = order.Legs.ElementAt(1);
                                    rowConfig.Leg2Delta = Math.Abs(leg.Delta);
                                    if (leg.Symbol.StartsWith("."))
                                    {
                                        Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                                        rowConfig.Leg2Expiration = option.Expiration;
                                    }
                                }

                                if (order.Legs.Count > 2)
                                {
                                    ZeroPlus.Models.Data.Trading.Interfaces.IComplexOrderLeg leg = order.Legs.ElementAt(2);
                                    rowConfig.Leg3Delta = Math.Abs(leg.Delta);
                                    if (leg.Symbol.StartsWith("."))
                                    {
                                        Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                                        rowConfig.Leg3Expiration = option.Expiration;
                                    }
                                }

                                if (order.Legs.Count > 3)
                                {
                                    ZeroPlus.Models.Data.Trading.Interfaces.IComplexOrderLeg leg = order.Legs.ElementAt(3);
                                    rowConfig.Leg4Delta = Math.Abs(leg.Delta);
                                    if (leg.Symbol.StartsWith("."))
                                    {
                                        Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                                        rowConfig.Leg4Expiration = option.Expiration;
                                    }
                                }
                            }

                            templates.Add(JsonConvert.SerializeObject(rowConfig));
                        }

                        SpreadTemplateConfig config = new()
                        {
                            UnderlyingQuery = String.Empty, // unders
                            Templates = templates.Distinct().ToList(),
                        };

                        if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
                        {
                            Thread newWindowThread = new(() =>
                            {
                                SynchronizationContext.SetSynchronizationContext(
                                    new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                                SpreadTemplateView window = new();
                                SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
                                viewModel.SetDispatcher(window.Dispatcher);

                                window.Dispatcher.UnhandledException += (s, e) =>
                                {
                                    _log.Error(e.Exception, "DispatcherUnhandledException");
                                    e.Handled = true;
                                };

                                window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                                viewModel.Ready += Load;
                                window.Show();

                                Dispatcher.Run();

                                void Load(IModuleViewModel module)
                                {
                                    viewModel.Ready -= Load;
                                    _ = viewModel.LoadFromConfigAsync(config);
                                }
                            });
                            newWindowThread.SetApartmentState(ApartmentState.STA);
                            newWindowThread.Start();

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BuildSpreadTemplateFromSelected));
            }
        }

        private void ChartOrders(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    List<ChartValueModel> values = new();
                    List<OmsOrderModel> orders = new();

                    foreach (object tmp in (ObservableCollectionCore<object>)parameter.Orders)
                    {
                        if (tmp is OmsOrderModel order)
                        {
                            orders.Add(order);
                        }
                    }

                    switch ((string)parameter.Field)
                    {
                        case nameof(OmsOrderModel.TagEdge):
                            values = orders.Select(x => new ChartValueModel(x.TagEdge, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagEma):
                            values = orders.Select(x => new ChartValueModel(x.TagEma, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagAsk):
                            values = orders.Select(x => new ChartValueModel(x.TagAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagBid):
                            values = orders.Select(x => new ChartValueModel(x.TagBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagMid):
                            values = orders.Select(x => new ChartValueModel(x.TagMid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagTheo):
                            values = orders.Select(x => new ChartValueModel(x.TagTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Fee1):
                            values = orders.Select(x => new ChartValueModel(x.Fee1, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Fee2):
                            values = orders.Select(x => new ChartValueModel(x.Fee2, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Bid):
                            values = orders.Select(x => new ChartValueModel(x.Bid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Ask):
                            values = orders.Select(x => new ChartValueModel(x.Ask, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Mid):
                            values = orders.Select(x => new ChartValueModel(x.Mid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.UnderBid):
                            values = orders.Select(x => new ChartValueModel(x.UnderBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.UnderAsk):
                            values = orders.Select(x => new ChartValueModel(x.UnderAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TV):
                            values = orders.Select(x => new ChartValueModel(x.TV, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Delta):
                            values = orders.Select(x => new ChartValueModel(x.Delta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.ExchangeFee1):
                            values = orders.Select(x => new ChartValueModel(x.ExchangeFee1, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.ExchangeFee2):
                            values = orders.Select(x => new ChartValueModel(x.ExchangeFee2, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.BrokerFee1):
                            values = orders.Select(x => new ChartValueModel(x.BrokerFee1, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.BrokerFee2):
                            values = orders.Select(x => new ChartValueModel(x.BrokerFee2, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.FillTime):
                            values = orders.Select(x => new ChartValueModel(x.FillTime, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.SubmitToNewTime):
                            values = orders.Select(x => new ChartValueModel(x.SubmitToNewTime, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TradeToNewTime):
                            values = orders.Select(x => new ChartValueModel(x.TradeToNewTime, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.NewToCancelTime):
                            values = orders.Select(x => new ChartValueModel(x.NewToCancelTime, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.LastQuantity):
                            values = orders.Select(x => new ChartValueModel(x.LastQuantity, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.FilledQty):
                            values = orders.Select(x => new ChartValueModel(x.FilledQty, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.LeavesQuantity):
                            values = orders.Select(x => new ChartValueModel(x.LeavesQuantity, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CumulativeQuantity):
                            values = orders.Select(x => new ChartValueModel(x.CumulativeQuantity, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Price):
                            values = orders.Select(x => new ChartValueModel(x.Price, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Quantity):
                            values = orders.Select(x => new ChartValueModel(x.Quantity, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.Position):
                            values = orders.Select(x => new ChartValueModel(x.Position, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.RealizedPnL):
                            values = orders.Select(x => new ChartValueModel(x.RealizedPnL, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.AdjustedPnl):
                            values = orders.Select(x => new ChartValueModel(x.AdjustedPnl, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.AveragePrice):
                            values = orders.Select(x => new ChartValueModel(x.AveragePrice, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.BidPercentOfFillPrice):
                            values = orders.Select(x => new ChartValueModel(x.BidPercentOfFillPrice, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseBidPercentOfFillPrice):
                            values = orders.Select(x => new ChartValueModel(x.CloseBidPercentOfFillPrice, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.OmsBidPercentOfFillPrice):
                            values = orders.Select(x => new ChartValueModel(x.OmsBidPercentOfFillPrice, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.OmsBestBidPercent):
                            values = orders.Select(x => new ChartValueModel(x.OmsBestBidPercent, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.LegsCount):
                            values = orders.Select(x => new ChartValueModel(x.LegsCount, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TotalContracts):
                            values = orders.Select(x => new ChartValueModel(x.TotalContracts, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TotalDelta):
                            values = orders.Select(x => new ChartValueModel(x.TotalDelta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalTheo):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalGamma):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalGamma, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalVega):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalVega, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalTheta):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalTheta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalRho):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalRho, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalIV):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalIV, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalUnder):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalUnder, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalUBid):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalUBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalUAsk):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalUAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalBid):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.HanweckTotalAsk):
                            values = orders.Select(x => new ChartValueModel(x.HanweckTotalAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagEdgeToTheo):
                            values = orders.Select(x => new ChartValueModel(x.TagEdgeToTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.TagEdgeToEma):
                            values = orders.Select(x => new ChartValueModel(x.TagEdgeToEma, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.EdgeToTheo):
                            values = orders.Select(x => new ChartValueModel(x.EdgeToTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.InitialEdge):
                            values = orders.Select(x => new ChartValueModel(x.InitialEdge, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.OpenEdge):
                            values = orders.Select(x => new ChartValueModel(x.OpenEdge, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseEdge):
                            values = orders.Select(x => new ChartValueModel(x.CloseEdge, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseTV):
                            values = orders.Select(x => new ChartValueModel(x.CloseTV, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseDelta):
                            values = orders.Select(x => new ChartValueModel(x.CloseDelta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseTotalDelta):
                            values = orders.Select(x => new ChartValueModel(x.CloseTotalDelta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalTheo):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalGamma):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalGamma, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalVega):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalVega, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalTheta):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalTheta, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalRho):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalRho, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalIV):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalIV, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseBid):
                            values = orders.Select(x => new ChartValueModel(x.CloseBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseAsk):
                            values = orders.Select(x => new ChartValueModel(x.CloseAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseUnderBid):
                            values = orders.Select(x => new ChartValueModel(x.CloseUnderBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseUnderAsk):
                            values = orders.Select(x => new ChartValueModel(x.CloseUnderAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalUnder):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalUnder, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalUBid):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalUBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalUAsk):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalUAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalBid):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalBid, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseHanweckTotalAsk):
                            values = orders.Select(x => new ChartValueModel(x.CloseHanweckTotalAsk, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.DeltaAdjustedTheo):
                            values = orders.Select(x => new ChartValueModel(x.DeltaAdjustedTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseDeltaAdjustedTheo):
                            values = orders.Select(x => new ChartValueModel(x.CloseDeltaAdjustedTheo, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.BidSize):
                            values = orders.Select(x => new ChartValueModel(x.BidSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.AskSize):
                            values = orders.Select(x => new ChartValueModel(x.AskSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseBidSize):
                            values = orders.Select(x => new ChartValueModel(x.CloseBidSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseAskSize):
                            values = orders.Select(x => new ChartValueModel(x.CloseAskSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.UnderlyingBidSize):
                            values = orders.Select(x => new ChartValueModel(x.UnderlyingBidSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.UnderlyingAskSize):
                            values = orders.Select(x => new ChartValueModel(x.UnderlyingAskSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseUnderlyingBidSize):
                            values = orders.Select(x => new ChartValueModel(x.CloseUnderlyingBidSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.CloseUnderlyingAskSize):
                            values = orders.Select(x => new ChartValueModel(x.CloseUnderlyingAskSize, x.LastUpdateTime)).ToList();
                            break;
                        case nameof(OmsOrderModel.LoopInitLatency):
                            values = orders.Select(x => new ChartValueModel(x.LoopInitLatency, x.LastUpdateTime)).ToList();
                            break;
                        default:
                            return;
                    }

                    ChartView chartView = new()
                    {
                        Title = "[" + parameter.Field + " X " + nameof(OmsOrderModel.LastUpdateTime) + "] - Chart"
                    };

                    chartView.Loaded += (object s, RoutedEventArgs e) =>
                    {
                        ChartViewModel viewModel = (ChartViewModel)chartView.DataContext;
                        viewModel.Field = parameter.Field;
                        viewModel.LoadChart(values);
                    };

                    chartView.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchInNewTradesModule));
            }
        }

        private void SendToDominator(object parameter)
        {
            try
            {
                if (parameter is Tuple<OmsOrderModel, DominatorModel> tradeDomPair)
                {
                    OmsOrderModel model = tradeDomPair?.Item1;
                    DominatorModel dominator = tradeDomPair?.Item2;

                    if (model != null && dominator != null)
                    {
                        TradeForDom trade = new()
                        {
                            UnderSymbol = model.UnderlyingSymbol,
                            Exchange = model.LastExchange,
                            SpreadType = model.BaseStrategy.ToString(),
                            Quantity = model.Quantity,
                            Symbol = model.Symbol,
                            Bid = model.Bid,
                            Ask = model.Ask,
                            Price = model.Price,
                            UnderPrice = model.HanweckTotalUnder,
                            MidMarket = model.Mid,
                            TradeDelta = model.Delta,
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

        private async void AddToList(Tuple<OmsOrderModel, ConfigSave> pair)
        {
            try
            {
                OmsOrderModel model = pair?.Item1;
                ConfigSave config = pair?.Item2;

                if (model != null && config != null)
                {
                    ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                    if (details != null)
                    {
                        CustomListModel customListModel = JsonConvert.DeserializeObject<CustomListModel>(details.ConfigJson);

                        if (customListModel.SymbolModels.Add(
                                new(model.UnderlyingSymbol, OmsCore.User.Username, DateTime.Now)))
                        {
                            customListModel.LastUpdateTime = DateTime.Now;
                            customListModel.Id = config.Id;
                            customListModel.Load();

                            string configJson = JsonConvert.SerializeObject(config);
                            customListModel.Details = JsonConvert.DeserializeObject<ZeroPlus.Models.Data.Configs.ConfigSave>(configJson);
                            if (customListModel.Details != null)
                            {
                                customListModel.Details.Module = (int)Module.CustomList;
                            }

                            ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(customListModel.Details));
                            configSave.Title = customListModel.Title;
                            configSave.ConfigJson = customListModel.GetAsJson();
                            configSave.SaveTime = DateTime.Now;
                            OmsCore.GatewayClient.SaveConfig(configSave);

                            Dispatcher?.BeginInvoke(() =>
                                MessageBoxService.Show($"{model.UnderlyingSymbol} added to {config.Title}",
                                    "Custom List - ZeroPlus OMS",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information,
                                    MessageBoxResult.OK));
                        }
                        else
                        {
                            Dispatcher?.BeginInvoke(() =>
                                MessageBoxService.Show($"{config.Title} already contains {model.UnderlyingSymbol}",
                                    "Custom List - ZeroPlus OMS",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information,
                                    MessageBoxResult.OK));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToNagBot));
            }
        }

        private void SendToNagBot(object parameter)
        {
            try
            {
                if (parameter is Tuple<OmsOrderModel, BasketTraderViewModel> pair)
                {
                    OmsOrderModel model = pair?.Item1;
                    BasketTraderViewModel basket = pair?.Item2;

                    if (model != null && basket != null)
                    {
                        basket.LoadNagbotFromOrderModelAsync(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToNagBot));
            }
        }

        private void BlockFromDom(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    List<OmsOrderModel> orders = new();
                    foreach (object tmp in (ObservableCollectionCore<object>)parameter.Orders)
                    {
                        if (tmp is OmsOrderModel order)
                        {
                            orders.Add(order);
                        }
                    }
                    if (orders.Count > 0)
                    {
                        BlockSymbolFromDominatorView view = new();
                        if (view.DataContext is BlockSymbolFromDominatorViewModel viewModel)
                        {
                            viewModel.Load(orders);
                            view.Show();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BlockFromDom));
            }
        }

        private void ChartSymbolBidAskIv(object parameter)
        {
            try
            {
                if (parameter is OmsOrderModel model)
                {
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ChartModule) ||
                        OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Heatmap))
                    {
                        Thread newWindowThread = new(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                            ChartModuleView window = new();
                            ChartModuleViewModel viewModel = (ChartModuleViewModel)window.DataContext;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            double midPrice = (model.CloseUnderBid + model.CloseUnderAsk) / 2;
                            viewModel.Ready += (IModuleViewModel module) => viewModel.LoadSnapshotsChart(model.Symbol, midPrice);

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChartSymbolBidAskIv));
            }
        }

        public void SaveViewModelConfig()
        {
            try
            {
                OrderBookViewModelConfig.AutoScroll = AutoScroll;
                OrderBookViewModelConfig.FilterType = FilterType;
                OrderBookViewModelConfig.ShowWorkingOrdersGrid = ShowWorkingOrders;
                OrderBookViewModelConfig.FilterString = FilterString;
                OrderBookViewModelConfig.CancelOnTimer = CancelOnTimer;
                OrderBookViewModelConfig.CancelIntervalSec = CancelIntervalSec;
                OrderBookViewModelConfig.CancelDeltaSec = CancelDeltaSec;

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(OrderBookConfig)}.xml");

                string configJson = JsonConvert.SerializeObject(OrderBookViewModelConfig, Formatting.Indented);
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
            }
        }

        public string GetConfig()
        {
            OrderBookConfig config = new()
            {
                AutoScroll = AutoScroll,
                SplitterHeight = OrderBookViewModelConfig.SplitterHeight,
                FilterType = FilterType,
                ShowWorkingOrdersGrid = ShowWorkingOrders,
                FilterString = FilterString,
                CancelOnTimer = CancelOnTimer,
                CancelIntervalSec = CancelIntervalSec,
                CancelDeltaSec = CancelDeltaSec,
            };

            string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            return configJson;
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return;
            }
            OrderBookViewModelConfig = await Task.Run(() => JsonConvert.DeserializeObject<OrderBookConfig>(configJson));

            AutoScroll = OrderBookViewModelConfig.AutoScroll;
            FilterType = OrderBookViewModelConfig.FilterType;
            ShowWorkingOrders = OrderBookViewModelConfig.ShowWorkingOrdersGrid;
            FilterString = OrderBookViewModelConfig.FilterString;
            OrderBookViewModelConfig.CancelOnTimer = CancelOnTimer;
            OrderBookViewModelConfig.CancelIntervalSec = CancelIntervalSec;
            OrderBookViewModelConfig.CancelDeltaSec = CancelDeltaSec;

            if (!IsSubscribedToAllOrOwnAndAll && FilterType is FilterType.ALL or FilterType.UNIQUE_ORDERS)
            {
                FilterType = FilterType.FILLED;
            }

            ChangeFilter(FilterType);
        }
    }
}