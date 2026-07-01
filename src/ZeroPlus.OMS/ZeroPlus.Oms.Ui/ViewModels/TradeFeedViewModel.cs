using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using SymbolLib;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TradeFeedViewModel : CustomizableTableViewModelBase, IOmsDataSubscriber
    {
        public bool IsDisposed { get; set; }


        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private DelegateCommand<object> _filterInNewOrderBookCommand;
        private DelegateCommand<object> _searchInNewTradesModuleCommand;

        private readonly NotificationManager _notificationManager;
        private readonly IModuleFactory _moduleFactory;


        private DateTime _startTime;
        private DispatcherTimer _upTimeUpdateTimer;


        public OmsCore OmsCore { get; }
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();

        public DominatorsManagerModel DominatorsManagerModel { get; }
        public TransactionConsumerModel TransactionConsumerModel { get; }
        public Dispatcher Dispatcher { get; set; }
        public ConfigSave ConfigSave { get; set; }

        [Bindable]
        public partial string UpTime { get; set; }

        [Bindable(Default = true)]
        public partial bool AutoScroll { get; set; }

        [Bindable(Default = false)]
        public partial bool LoadAllTradesEnabled { get; set; }

        [Bindable]
        public partial TradeFeedModel LatestFeed { get; set; }

        [Bindable(Default = "Live Trade Feed")]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial ObservableCollection<object> SelectedItems { get; set; }

        public ICommand FilterInNewOrderBookCommand
        {
            get
            {
                _filterInNewOrderBookCommand ??= new DelegateCommand<object>(FilterInNewOrderBook);
                return _filterInNewOrderBookCommand;
            }
        }

        public ICommand FilterInNewTradesModuleCommand
        {
            get
            {
                _searchInNewTradesModuleCommand ??= new DelegateCommand<object>(SearchInNewTradesModule);
                return _searchInNewTradesModuleCommand;
            }
        }

        public TradeFeedViewModel(TransactionConsumerModel transactionConsumerModel,
                                  DominatorsManagerModel dominatorsManagerModel,
                                  NotificationManager notificationManager,
                                  OmsCore omsCore,
                                  IModuleFactory moduleFactory)
        {
            _notificationManager = notificationManager;
            _moduleFactory = moduleFactory;
            DominatorsManagerModel = dominatorsManagerModel;
            TransactionConsumerModel = transactionConsumerModel;
            TransactionConsumerModel.Subscribe("*", SubscriptionFieldType.TradeFeed, this);
            TransactionConsumerModel.TradeFeedReceivedEvent += OnTradeFeedReceived;
            OmsCore = omsCore;
            StartUpTimeTimer();
        }

        internal void Dispose()
        {
            IsDisposed = true;
            TransactionConsumerModel.TradeFeedReceivedEvent -= OnTradeFeedReceived;
            TransactionConsumerModel.Unsubscribe("*", SubscriptionFieldType.TradeFeed, this);
        }

        private void OnTradeFeedReceived(TradeFeedModel tradeFeed)
        {
            if (_autoScroll)
            {
                LatestFeed = tradeFeed;
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {

        }

        private void StartUpTimeTimer()
        {
            _startTime = DateTime.Now;
            _upTimeUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1_000),
            };
            _upTimeUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _upTimeUpdateTimer.Tick += UpTimeTimerTick;
            _upTimeUpdateTimer.Start();
        }

        private void UpTimeTimerTick(object sender, EventArgs e)
        {
            TimeSpan delta = DateTime.Now - _startTime;
            UpTime = Math.Truncate(delta.TotalHours).ToString("00") + ":" + delta.Minutes.ToString("00") + ":" + delta.Seconds.ToString("00");
        }

        [Command]
        public void LoadAllTrades()
        {
            try
            {
                TransactionConsumerModel.UnsubscribeAll(this);
                if (LoadAllTradesEnabled)
                {
                    TransactionConsumerModel.Subscribe("ALL", SubscriptionFieldType.TradeFeed, this);
                }
                else
                {
                    TransactionConsumerModel.Subscribe("*", SubscriptionFieldType.TradeFeed, this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadAllTrades));
            }
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;
                viewModel.Config = GetConfigJson();
                viewModel.Module = Module.TradeFeed;

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void BrowseConfigs()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.TradeFeed);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseConfigs));
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
                    viewModel.SetModule(Module.TradeFeed);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void RowDoubleClick(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is TradeFeedModel trade)
            {
                OpenInComplexOrderTicket(trade);
            }
        }

        [Command]
        public void OpenInComplexOrderTicket(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) &&
                    parameter is TradeFeedModel trade)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;

                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView();
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView();
                                break;
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
                            _ = viewModel.LoadLegsFromTosAsync(trade.Symbol, trade.Side, true);

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
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
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
                if (parameter is TradeFeedModel trade)
                {
                    if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
                        {
                            ViewModel: BasketTraderViewModel viewModel
                        })
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
                            viewModel.LoadFromSymbol(trade.Symbol);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
            }
        }

        private void FilterInNewOrderBook(object parameter)
        {
            try
            {
                if (parameter is string filterString)
                {
                    TransactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OrderBookWindowView orderbookWindow = new();

                        orderbookWindow.OrderBookView.Ready += () =>
                        {
                            orderbookWindow.CloneFrom("FilterFromTrade");
                            orderbookWindow.OrderBookView.HideWorkingOrders();
                            ((OrderBookViewModel)orderbookWindow.DataContext).FilterString = filterString;
                        };

                        orderbookWindow.Show();
                    }));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FilterInNewOrderBook));
            }
        }

        private void SearchInNewTradesModule(object parameter)
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades) &&
                    parameter is string searchTerm)
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
                        SymbolCodec codec = new(searchTerm);
                        viewModel.Ready += (IModuleViewModel module) =>
                        {
                            viewModel.Symbol = searchTerm;
                            viewModel.LegTypes = codec.LegCount > 1 ? LegTypes.MLeg : LegTypes.Single;
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

        internal string GetConfigJson()
        {
            TradeFeedViewModelConfig config = GetConfig();
            string json = JsonConvert.SerializeObject(config);
            return json;
        }

        private TradeFeedViewModelConfig GetConfig()
        {
            TradeFeedViewModelConfig config = new();
            return config;
        }

        internal void LoadConfigJson(string json)
        {
            try
            {
                TradeFeedViewModelConfig config = JsonConvert.DeserializeObject<TradeFeedViewModelConfig>(json);
                LoadConfig(config);
            }
            catch (Exception)
            {
            }
        }

        internal void LoadConfig(TradeFeedViewModelConfig config)
        {

        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }
    }
}
