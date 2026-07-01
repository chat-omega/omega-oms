using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.Oms.Update;
using Module = ZeroPlus.Oms.Ui.Models.Module;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class StartupWindowViewModel : ViewModelBase
    {
        private const string APP_CODE = "OMS UI";
        private const string START_UP_NAME = "ZeroPlus OMS.exe";
        private static readonly string MODULE_TITLE = "ZeroPlus OMS";
        private static ILogger _log;
        private static MainView _mainWindow;
        public static MainView MainWindow => _mainWindow;
        private readonly TransactionConsumerModel _transactionConsumerModel;
        private readonly DispatcherStore _dispatcherStore;
        private readonly IModuleFactory _moduleFactory;
        private readonly NotificationManager _notificationManager;
        private readonly ExecutionTransactionsContainer _executionTransactionsContainer;
        private readonly PortfolioManagerModel _portfolioManagerModel;
        private readonly IAbstractFactory<MainView> _mainViewFactory;

        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
        public static OmsCore OmsCore { get; private set; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial bool AuthConnected { get; set; }
        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial string BusyMessage { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial string VersionString { get; set; }
        [Bindable]
        public partial string UpdateStatus { get; set; }
        [Bindable]
        public partial string Username { get; set; }
        [Bindable]
        public partial SecureString SecurePassword { get; set; }
        [Bindable]
        public partial bool SaveUser { get; set; }
        [Bindable]
        public partial User SelectedUser { get; set; }

        public StartupWindowViewModel(TransactionConsumerModel transactionConsumerModel,
                                      PortfolioManagerModel portfolioManagerModel,
                                      NotificationManager notificationManager,
                                      DispatcherStore dispatcherStore,
                                      OmsCore omsCore,
                                      IModuleFactory moduleFactory,
                                      IAbstractFactory<MainView> mainViewFactory,
                                      ExecutionTransactionsContainer executionTransactionsContainer)
        {
            _mainViewFactory = mainViewFactory;
            _executionTransactionsContainer = executionTransactionsContainer;
            _portfolioManagerModel = portfolioManagerModel;
            _dispatcherStore = dispatcherStore;
            _moduleFactory = moduleFactory;
            _transactionConsumerModel = transactionConsumerModel;
            _notificationManager = notificationManager;
            OmsCore = omsCore;

            base.OnInitializeInRuntime();
            ModuleTitle = MODULE_TITLE;

            _log = LogManager.GetCurrentClassLogger();

            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            OmsCore.AppUpdateManager.NewVersionAvailableEvent += OnUpdateManager_NewVersionAvailableEvent;
            OmsCore.GatewayClient.ConnectionStatusChangedEvent += AuthClient_ConnectionStatusChangedEvent;
            OmsCore.GatewayClient.ConfigShareEvent += OnConfigShareEvent;
            AuthClient_ConnectionStatusChangedEvent(OmsCore.GatewayClient.IsConnected);
            VersionString = OmsCore.AppUpdateManager.GetCurrentVersion();
            string selectedUser = OmsCore.GetSavedUser();
            if (selectedUser != null)
            {
                Username = selectedUser;
            }
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public async void Login()
        {
            if (string.IsNullOrEmpty(Username))
            {
                SetMessage("Username required!");
                return;
            }

            if (SecurePassword == null || SecurePassword.Length == 0)
            {
                SetMessage("Password required!");
                return;
            }

            User user = null;
            if (!AuthConnected)
            {
#if DEBUG
                user = new User
                {
                    Username = Username,
                    ID = 0,
                    Modules = Enum.GetValues(typeof(Module)).Cast<int>().ToHashSet()
                };
#else
                SetMessage("Could not connect to auth server.");
                return;
#endif
            }

            IsBusy = true;
            BusyMessage = "Authenticating";
            user ??= await OmsCore.GatewayClient.AuthenticateAsync(Username, SecurePassword, APP_CODE);

            if (user != null)
            {
                BusyMessage = "Initializing";
                SelectedUser = user;
                LoadUser();
            }
            else
            {
                IsBusy = false;
                SetMessage("Authentication failed.");
            }
        }

        [Command]
        public void Settings()
        {
            SettingsView settingsView = new();
            settingsView.Show();
        }

        public async void LoginWithAuthCode(string username, string authCode)
        {
            if (!AuthConnected)
            {
                SetMessage("Could not connect to auth server.");
                return;
            }

            Username = username;

            if (string.IsNullOrEmpty(Username))
            {
                SetMessage("Username required!");
                return;
            }

            if (string.IsNullOrEmpty(authCode))
            {
                SetMessage("Authcode required!");
                return;
            }

            IsBusy = true;
            BusyMessage = "Reauthenticating session";
            User user = await OmsCore.GatewayClient.AuthenticateAsync(Username, authCode);
            if (user != null)
            {
                BusyMessage = "Initializing";
                SelectedUser = user;
                LoadUser();
            }
            else
            {
                IsBusy = false;
                SetMessage("Reauth session failed.");
            }
        }

        private void SetMessage(string message)
        {
            Message = message;
            Task.Delay(7000).ContinueWith(t => Message = "");
        }

        private void AuthClient_ConnectionStatusChangedEvent(bool connected)
        {
            AuthConnected = connected;
        }

        private void OnConfigShareEvent(ConfigShare config)
        {
            switch ((Module)config.Module)
            {
                case Module.Notification:
                    _notificationManager?.AddAlert(config.Message, config.SendTime, "Shared");
                    break;
                default:
                    Task.Run(() => RequestConfigShareLoading(config));
                    break;
            }
        }

        private async Task RequestConfigShareLoading(ConfigShare config)
        {
            MessageResult result = MessageResult.No;
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
                string message = string.IsNullOrEmpty(config.Message) ? "" : "\n" + config.Message + "\n";
                result = MessageBoxService.Show($"{config.Username} shared with you a config for, {moduleName}.\n" +
                                                $"{message}\n" +
                                                $"Would you like to load it now?",
                                                "Config Share ZeroPlus OMS",
                                                MessageButton.YesNo,
                                                MessageIcon.Information,
                                                MessageResult.Yes);
            }));
            if (result == MessageResult.Yes)
            {
                LoadConfig(config);
            }
        }

        private void LoadConfig(ConfigShare config)
        {
            string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
            moduleName = moduleName?.Replace("Layout", "");
            moduleName = moduleName?.Trim();
            string title = string.IsNullOrWhiteSpace(config.Message) ? moduleName : config.Message + " - " + moduleName;
            Thread newWindowThread;
            switch ((Module)config.Module)
            {
                case Module.FishRoutes:
                    List<FishRoute> fishRouteExport = JsonConvert.DeserializeObject<List<FishRoute>>(config.ConfigJson);

                    foreach (FishRoute item in fishRouteExport)
                    {
                        if (OmsCore.Config.FishRoutes.Any(x => x.Routes == item.Routes))
                        {
                            bool ok = false;
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
                                string message = string.IsNullOrEmpty(config.Message) ? "" : "\n" + config.Message + "\n";
                                ok = MessageBoxService.Show($"You already have a Fish route called {item.Routes}.\n" +
                                                            $"Would you like to override it?",
                                                            "Config Share ZeroPlus OMS",
                                                            MessageButton.YesNo,
                                                            MessageIcon.Warning,
                                                            MessageResult.Yes) == MessageResult.Yes;
                            }));
                            if (ok)
                            {
                                OmsCore.Config.FishRoutes.RemoveAll(x => x.Routes == item.Routes);
                                OmsCore.Config.FishRoutes.Add(item);
                            }
                        }
                        else
                        {
                            OmsCore.Config.FishRoutes.Add(item);
                        }
                    }
                    break;
                case Module.SmartRoutes:
                    List<Tuple<string, Dictionary<int, Tuple<string, double>>>> export = JsonConvert.DeserializeObject<List<Tuple<string, Dictionary<int, Tuple<string, double>>>>>(config.ConfigJson);

                    foreach (Tuple<string, Dictionary<int, Tuple<string, double>>> item in export)
                    {
                        if (OmsCore.Config.SmartRoutes.ContainsKey(item.Item1))
                        {
                            bool ok = false;
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
                                string message = string.IsNullOrEmpty(config.Message) ? "" : "\n" + config.Message + "\n";
                                ok = MessageBoxService.Show($"You already have a smart route called {item.Item1}.\n" +
                                                            $"Would you like to override it?",
                                                            "Config Share ZeroPlus OMS",
                                                            MessageButton.YesNo,
                                                            MessageIcon.Warning,
                                                            MessageResult.Yes) == MessageResult.Yes;
                            }));
                            if (ok)
                            {
                                OmsCore.Config.SmartRoutes[item.Item1] = item.Item2;
                            }
                        }
                        else
                        {
                            OmsCore.Config.SmartRoutes[item.Item1] = item.Item2;
                        }
                    }
                    break;
                case Module.SpreadsGenerator:
                case Module.SpreadsGeneratorLayout:
                    _moduleFactory.CreateModule(Module.SpreadsGenerator, null, config.ConfigJson);
                    break;
                case Module.SpreadTemplate:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            SpreadTemplateView window = new();
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
                            viewModel.ModuleTitle = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            window.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.OrderBook:
                    _transactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OrderBookWindowView view = new();
                        OrderBookViewModel viewModel = view.OrderBookView.DataContext as OrderBookViewModel;
                        viewModel.ModuleTitle = title;
                        view.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.CustomOrderBook:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        EmptyOrderBookView view = new();
                        EmptyOrderBookViewModel viewModel = view.DataContext as EmptyOrderBookViewModel;
                        viewModel.ModuleTitle = title;
                        view.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.BasketTrader:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
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
                                viewModel.LoadConfigFromJsonAsync(config.ConfigJson);
                            }
                        }
                    }
                    break;
                case Module.EdgeScanFeed:
                    _moduleFactory.CreateModule(Module.EdgeScanFeed, null, config.ConfigJson);
                    break;
                case Module.Trades:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            TradesView window = new();
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                            viewModel.ModuleTitle = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            window.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.OrderBookLayout:
                    _transactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OrderBookWindowView view = new()
                        {
                            Title = title,
                        };

                        view.Loaded += (s, e) => _ = view.OrderBookView.LoadConfigFromJsonAsync(config.ConfigJson);
                        OrderBookViewModel viewModel = (OrderBookViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.TradeFeed:
                    _transactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TradeFeedView view = new()
                        {
                            Title = title,
                        };

                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                        TradeFeedViewModel viewModel = (TradeFeedViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.ComplexOrderTicketLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            Window window = null;
                            switch (OmsCore.Config.DefaultOrderTicketStyle)
                            {
                                case OrderTicketStyle.Complex:
                                    window = new ComplexOrderTicketView();
                                    window.Loaded += (s, e) => _ = ((ComplexOrderTicketView)window).LoadConfigFromJsonAsync(config.ConfigJson);
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView();
                                    break;
                            }

                            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                            viewModel.Description = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.CombinedOrderTicketLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                    {
                        newWindowThread = new Thread(() =>
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
                                    window.Loaded += (s, e) => _ = ((CombinedOrderTicketView)window).LoadConfigFromJsonAsync(config.ConfigJson);
                                    break;
                            }

                            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                            viewModel.Description = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.CustomOrderBookLayout:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        EmptyOrderBookView view = new();
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                        EmptyOrderBookViewModel viewModel = (EmptyOrderBookViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.BasketTraderLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
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
                                view.LoadConfigFromJsonAsync(config.ConfigJson);
                            }
                        }
                    }
                    break;
                case Module.LockTraderLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.LockTrader))
                    {
                        if (_moduleFactory.CreateModule(Module.LockTrader) is LockTraderView { ViewModel: LockTraderViewModel viewModel } view)
                        {
                            if (viewModel.IsReady)
                            {
                                OnReady(viewModel);
                            }
                            else
                            {
                                viewModel.Ready += OnReady;
                            }

                            void OnReady(IModuleViewModel _)
                            {
                                viewModel.Ready -= OnReady;
                                view.LoadConfigFromJsonAsync(config.ConfigJson);
                            }
                        }
                    }
                    break;
                case Module.TradesLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            TradesView window = new();
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                            viewModel.ModuleTitle = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            window.Loaded += (s, e) => _ = window.LoadConfigFromJsonAsync(config.ConfigJson);

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.SpreadTemplateLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            SpreadTemplateView window = new();
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
                            viewModel.ModuleTitle = title;
                            viewModel.SetDispatcher(window.Dispatcher);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            window.Loaded += (s, e) => window.LoadConfigFromJson(config.ConfigJson);

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.PortfolioLayout:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PortfolioView view = new();
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                        PortfolioViewModel viewModel = (PortfolioViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.DeltaHedgingLayout:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DeltaHedgingView view = new();
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                        DeltaHedgingViewModel viewModel = (DeltaHedgingViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.HedgeHouseLayout:
                    _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
                    {
                        HedgeHouseView view = new();
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                        HedgeHouseViewModel viewModel = (HedgeHouseViewModel)view.DataContext;
                        viewModel.ModuleTitle = title;

                        view.Show();
                    }));
                    break;
                case Module.HeatmapLayout:
                    newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));
                        SpreadHeatmapView window = new();

                        SpreadHeatmapViewModel viewModel = (SpreadHeatmapViewModel)window.DataContext;
                        viewModel.ModuleTitle = title;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => _ = window.LoadConfigFromJsonAsync(config.ConfigJson);

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                    break;
                case Module.ComplexOrderTicket:
                    break;
                case Module.DominatorsManagerLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.DominatorsManagerLayout))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            DominatorsManagerView view = new();
                            view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                            DominatorsManagerViewModel viewModel = (DominatorsManagerViewModel)view.DataContext;
                            viewModel.ModuleTitle = title;

                            view.Show();
                        }));
                    }
                    break;
                case Module.BasketManagerLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketManagerLayout))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BasketManagerView view = new();
                            view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);
                            BasketManagerViewModel viewModel = (BasketManagerViewModel)view.DataContext;
                            viewModel.ModuleTitle = title;

                            view.Show();
                        }));
                    }
                    break;
                case Module.Dashboard:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Dashboard))
                    {
                        if (_moduleFactory.CreateModule(Module.Dashboard) is DashboardView { ViewModel: DashboardViewModel viewModel } view)
                        {
                            if (viewModel.IsReady)
                            {
                                OnReady(viewModel);
                            }
                            else
                            {
                                viewModel.Ready += OnReady;
                            }

                            void OnReady(IModuleViewModel _)
                            {
                                viewModel.Ready -= OnReady;
                                viewModel.LoadConfigFromJsonAsync(config.ConfigJson);
                            }
                        }
                    }
                    break;
                case Module.ScriptTrader:
                    newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));
                        ScriptTraderView window = new(_moduleFactory);
                        window.Loaded += (s, e) => _ = window.LoadConfigFromJsonAsync(config.ConfigJson);
                        window.Show();
                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                    break;
            }
        }

        private void LoadUser()
        {
            if (SelectedUser != null)
            {
                OmsCore.GatewayClient.ConnectionStatusChangedEvent -= AuthClient_ConnectionStatusChangedEvent;
                OmsCore.User = SelectedUser;

                List<AccountConfigModel> savedAccountConfigs = OmsCore.Config.GetSavedAccountConfigs();

                foreach (string account in SelectedUser.Accounts)
                {
                    AccountConfigModel accountConfig = savedAccountConfigs.FirstOrDefault(x => x.Account == account);
                    accountConfig ??= new AccountConfigModel()
                    {
                        Account = account
                    };

                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultBroker))
                    {
                        accountConfig.DefaultRoute = "AUTO";
                    }
                    else if (accountConfig.DefaultBroker.Contains('-'))
                    {
                        accountConfig.DefaultBroker = accountConfig.DefaultBroker.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRoute))
                    {
                        accountConfig.DefaultRoute = "EXCH_ROLL";
                    }
                    else if (accountConfig.DefaultRoute.Contains('-'))
                    {
                        accountConfig.DefaultRoute = accountConfig.DefaultRoute.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultSingleLegRoute))
                    {
                        accountConfig.DefaultSingleLegRoute = "EXCH_ROLL_S";
                    }
                    else if (accountConfig.DefaultSingleLegRoute.Contains('-'))
                    {
                        accountConfig.DefaultSingleLegRoute = accountConfig.DefaultSingleLegRoute.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRouteSpxRutXsp))
                    {
                        accountConfig.DefaultRouteSpxRutXsp = "BCBOE";
                    }
                    else if (accountConfig.DefaultRouteSpxRutXsp.Contains('-'))
                    {
                        accountConfig.DefaultRouteSpxRutXsp = accountConfig.DefaultRouteSpxRutXsp.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRouteNdx))
                    {
                        accountConfig.DefaultRouteNdx = "BPHLX";
                    }
                    else if (accountConfig.DefaultRouteNdx.Contains('-'))
                    {
                        accountConfig.DefaultRouteNdx = accountConfig.DefaultRouteNdx.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultHedgeRouteRegular))
                    {
                        accountConfig.DefaultHedgeRouteRegular = "ISMART";
                    }
                    else if (accountConfig.DefaultHedgeRouteRegular.Contains('-'))
                    {
                        accountConfig.DefaultHedgeRouteRegular = accountConfig.DefaultHedgeRouteRegular.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultCurbSessionRouteRegular))
                    {
                        accountConfig.DefaultCurbSessionRouteRegular = "ISPREAD";
                    }
                    else if (accountConfig.DefaultCurbSessionRouteRegular.Contains('-'))
                    {
                        accountConfig.DefaultCurbSessionRouteRegular = accountConfig.DefaultCurbSessionRouteRegular.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultSweepRouteRegular))
                    {
                        accountConfig.DefaultSweepRouteRegular = "ISMART";
                    }
                    else if (accountConfig.DefaultSweepRouteRegular.Contains('-'))
                    {
                        accountConfig.DefaultSweepRouteRegular = accountConfig.DefaultSweepRouteRegular.Split('-')[1];
                    }

                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRouteAutoTrader))
                    {
                        accountConfig.DefaultRouteAutoTrader = "EXCH_ROLL";
                    }
                    else if (accountConfig.DefaultRouteAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultRouteAutoTrader = accountConfig.DefaultRouteAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultSingleLegRouteAutoTrader))
                    {
                        accountConfig.DefaultSingleLegRouteAutoTrader = "EXCH_ROLL_S";
                    }
                    else if (accountConfig.DefaultSingleLegRouteAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultSingleLegRouteAutoTrader = accountConfig.DefaultSingleLegRouteAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRouteSpxRutXspAutoTrader))
                    {
                        accountConfig.DefaultRouteSpxRutXspAutoTrader = "CBOE";
                    }
                    else if (accountConfig.DefaultRouteSpxRutXspAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultRouteSpxRutXspAutoTrader = accountConfig.DefaultRouteSpxRutXspAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultRouteNdxAutoTrader))
                    {
                        accountConfig.DefaultRouteNdxAutoTrader = "PHLX";
                    }
                    else if (accountConfig.DefaultRouteNdxAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultRouteNdxAutoTrader = accountConfig.DefaultRouteNdxAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultHedgeRouteAutoTrader))
                    {
                        accountConfig.DefaultHedgeRouteAutoTrader = "ISMART";
                    }
                    else if (accountConfig.DefaultHedgeRouteAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultHedgeRouteAutoTrader = accountConfig.DefaultHedgeRouteAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultCurbSessionRouteAutoTrader))
                    {
                        accountConfig.DefaultCurbSessionRouteAutoTrader = "ISPREAD";
                    }
                    else if (accountConfig.DefaultCurbSessionRouteAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultCurbSessionRouteAutoTrader = accountConfig.DefaultCurbSessionRouteAutoTrader.Split('-')[1];
                    }
                    if (string.IsNullOrWhiteSpace(accountConfig.DefaultSweepRouteAutoTrader))
                    {
                        accountConfig.DefaultSweepRouteAutoTrader = "ISMART";
                    }
                    else if (accountConfig.DefaultSweepRouteAutoTrader.Contains('-'))
                    {
                        accountConfig.DefaultSweepRouteAutoTrader = accountConfig.DefaultSweepRouteAutoTrader.Split('-')[1];
                    }

                    OmsCore.Config.AccountConfigs.Add(accountConfig);

                    if (account.Equals(OmsCore.Config.SavedAccount, StringComparison.OrdinalIgnoreCase))
                    {
                        OmsCore.Config.AccountConfig = accountConfig;
                    }
                }

                OmsCore.Config.AccountConfig ??= OmsCore.Config.AccountConfigs.FirstOrDefault(x => x.Account.StartsWith("TBK"));

                if (OmsCore.Config.AccountConfig != null && !string.IsNullOrWhiteSpace(OmsCore.Config.SavedBroker)
                    && string.IsNullOrWhiteSpace(OmsCore.Config.AccountConfig.DefaultBroker))
                {
                    OmsCore.Config.AccountConfig.DefaultBroker = OmsCore.Config.SavedBroker;
                }

                CacheBasketConfigs();

                if (SaveUser)
                {
                    _ = Task.Run(UpdateSelectedUserPing);
                }
                if (OmsCore.Config.EnableBasketManagerClientV2)
                {
                    _ = OmsCore.BasketManagerClient.StartAsync();
                }

                SetupDispatcherThread(Module.BasketTrader);

                Thread mainWindowThread = new Thread(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));

                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.Thread.Name = "Transaction Dispatcher Thread";

                    dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "Transaction Dispatcher Exception");
                        e.Handled = true;
                    };

                    _mainWindow = _mainViewFactory.Create();
                    MainViewModel viewModel = (MainViewModel)_mainWindow.DataContext;
                    viewModel.SetDispatcher(dispatcher);

                    _dispatcherStore.SetModuleCommonDispatcher(Module.OrderBook, dispatcher);
                    _transactionConsumerModel.SetDispatcherAndStart(dispatcher);

                    _mainWindow.Closed += (s, e) => dispatcher.InvokeShutdown();
                    _mainWindow.Loaded += MainWindowLoaded;

                    _mainWindow.Show();
                    Dispatcher.Run();
                });
                mainWindowThread.SetApartmentState(ApartmentState.STA);
                mainWindowThread.Start();
            }
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            _mainWindow.Loaded -= MainWindowLoaded;
            SetupDispatcherThread(Module.Portfolio);
            _portfolioManagerModel.SetDispatcherAndStart(_dispatcherStore.GetDispatcherForModule(Module.Portfolio));
            _executionTransactionsContainer.SetDispatcher(_dispatcherStore.GetDispatcherForModule(Module.OrderBook));
            DispatcherService.BeginInvoke(() => CurrentWindowService.Hide());
            MainViewModel viewModel = (MainViewModel)_mainWindow.DataContext;
            viewModel.LoadInitialWindows();
#if DEBUG
            ShowTestReleaseNotes();
#endif
        }

        private static void ShowTestReleaseNotes()
        {
            string markdown;
            string mdPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ReleaseNotes.md");
            if (File.Exists(mdPath))
            {
                markdown = File.ReadAllText(mdPath);
            }
            else
            {
                markdown = "# Release Notes\n\nCould not find ReleaseNotes.md";
            }

            ReleaseNotesView view = new();
            if (view.DataContext is ReleaseNotesViewModel vm)
            {
                vm.Version = "V-2.15.0";
                vm.ReleaseNotes = markdown;
            }
            view.Show();
        }

        private void SetupDispatcherThread(Module module)
        {
            var waiter = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher.Thread.Name = module + " Dispather Thread";

                dispatcher.UnhandledException += (_, e) =>
                {
                    _log.Error(e.Exception, module + " Dispatcher Exception");
                    e.Handled = true;
                };

                _dispatcherStore.SetModuleCommonDispatcher(module, dispatcher);
                waiter.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            waiter.Wait();
        }

        public static Task CacheBasketConfigs()
        {
            return Task.Run(async () =>
            {
                try
                {
                    foreach (var configSave in OmsCore.Config.SavedBasketQuickAccessLayouts.Select(x => x.Item3).Union(OmsCore.Config.SavedBasketDefaultLayouts.Select(x => x.Item5)))
                    {
                        await CacheConfigSaveContent(configSave);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(CacheBasketConfigs));
                }
            });
        }

        private static async Task CacheConfigSaveContent(ConfigSave configSave)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configSave.ConfigJson))
                {
                    var config = await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                    if (config != null)
                    {
                        configSave.ConfigJson = config.ConfigJson;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CacheConfigSaveContent));
            }
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _log.Error(e.Exception, nameof(Application.Current.DispatcherUnhandledException));
            e.Handled = true;
        }

        private async Task UpdateSelectedUserPing()
        {
            await Task.Run(() =>
            {
                OmsCore.SaveUser();
            });
        }

        private void OnUpdateManager_NewVersionAvailableEvent(Information updateInfo)
        {
            UpdateStatus = "Update available.";
            Task.Run(() => RequestRestartForUpdate(updateInfo));
        }

        private void RequestRestartForUpdate(Information updateInfo)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ReleaseNotesView releaseNotesView = new();

                ReleaseNotesViewModel releaseNotesViewModel = releaseNotesView.DataContext as ReleaseNotesViewModel;
                releaseNotesViewModel.Load(updateInfo);

                releaseNotesView.Show();
            }), DispatcherPriority.Loaded);
        }

        public static async void RestartProgram(string reason)
        {
            try
            {
                await ShutdownPython();

                string curAppLocation = Path.Combine(AppContext.BaseDirectory, START_UP_NAME);

                if (!File.Exists(curAppLocation))
                {
                    throw new SlimException($"Current program not found. {curAppLocation}");
                }

                if (!File.Exists(curAppLocation))
                {
                    throw new SlimException($"Program file not found. {curAppLocation}");
                }

                string args = GetStartUpArgs();

                _ = Process.Start(curAppLocation, args);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, $"{nameof(RestartProgram)} -> Failed to restart program. Reason: {reason}");
                MessageBox.Show($"Failed to restart program for {reason}!\nRestart manually.",
                                "Error - ZeroPlus OMS",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        private static async Task ShutdownPython()
        {
            try
            {
                if (OmsCore.Config.EnablePythonEngine)
                {
                    await Task.Run(() => Python.Runtime.PythonEngine.Shutdown());
                }
            }
            catch (Exception)
            {
            }
        }

        public static string GetStartUpArgs()
        {
            try
            {
                string args = "";
                if (OmsCore != null && !string.IsNullOrEmpty(OmsCore.User.Username) && !string.IsNullOrEmpty(OmsCore.GatewayClient.AuthCode))
                {
                    args = OmsCore.User.Username + " " + OmsCore.GatewayClient.AuthCode;
                }
                return args;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetStartUpArgs));
                return "";
            }
        }
    }
}
