using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Profiling;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.Oms.Update;
using ConfigSave = ZeroPlus.Comms.Models.Data.Oms.Config.ConfigSave;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class MainViewModel : ViewModelBase, IOmsDataSubscriber, IDynamicConfigParentModule
    {
        private readonly int STALE_SERVER_TIME_THRESHOLD = 5;
        private readonly int DELAY_SERVER_TIME_THRESHOLD = 1500;
        private readonly int WARN_DELAY_SERVER_TIME_THRESHOLD = 2500;
        private readonly int ALERT_CREEP_THRESHOLD = 5;
        private readonly int WATCHER_INTERVAL = 1000;

        private static readonly string MODULE_TITLE = "ZeroPlus OMS";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private static readonly ConcurrentDictionary<string, Window> _ticketIdToTicketWindowMap = new();
        private static readonly ConcurrentDictionary<string, BasketTraderView> _basketIdToBasketMap = new();

        private string _lastServerTime = "";
        private int _lastServerTimeSame = 0;
        private bool _connectionWatcherRunning;
        private DateTime _lastUpdate;
        private bool _alertRunning;
        private Information _updateInfo;

        private readonly TransactionConsumerModel _transactionConsumerModel;
        private readonly NotificationManager _notificationManager;
        private readonly DispatcherStore _dispatcherStore;


        public OmsCore OmsCore { get; private set; }

        public IWindowService WindowService => GetService<IWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public IDispatcherService DispatcherService => GetService<IDispatcherService>();
        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        public IEnumerable<InstanceMode> InstanceModes { get; } = ((InstanceMode[])Enum.GetValues(typeof(InstanceMode))).ToList();
        public IModuleFactory ModuleFactory { get; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial ObservableCollection<FavoriteModuleGroupModel> FavoriteModules { get; set; }

        [Bindable]
        public partial string Username { get; set; }

        [Bindable]
        public partial bool UpdateAvailable { get; set; }

        [Bindable]
        public partial bool ShowQuoteAlert { get; set; }

        [Bindable]
        public partial bool QuoteConnected { get; set; }

        [Bindable]
        public partial bool TradeRequestConnected { get; set; }

        [Bindable]
        public partial bool HanweckConnected { get; set; }

        [Bindable]
        public partial bool OrderGatewayConnected { get; set; }

        [Bindable]
        public partial bool AutoTraderDirectConnected { get; set; }

        [Bindable]
        public partial bool RaptorConnected { get; set; }

        [Bindable]
        public partial bool EdgeScannerConnected { get; set; }

        [Bindable]
        public partial bool EdgeScanFeedRunnerConnected { get; set; }

        [Bindable]
        public partial bool SymbolMapConnected { get; set; }

        [Bindable]
        public partial bool TelemetryConnected { get; set; }

        [Bindable]
        public partial bool FullEmaConnected { get; set; }

        private bool emaServerConnected;
        public bool EmaServerConnected
        {
            get => emaServerConnected;
            set => SetValue(ref emaServerConnected, value);
        }

        [Bindable]
        public partial bool InterpolatorConnected { get; set; }

        [Bindable]
        public partial bool TheosConnected { get; set; }

        [Bindable]
        public partial bool HubTronConnected { get; set; }

        [Bindable]
        public partial bool IbGatewayConnected { get; set; }

        [Bindable]
        public partial bool DatabentoConnected { get; set; }

        [Bindable]
        public partial bool CobConnected { get; set; }

        [Bindable]
        public partial bool PricingConnected { get; set; }

        [Bindable]
        public partial bool HerculesConnected { get; set; }

        [Bindable]
        public partial bool OrderConnected { get; set; }

        [Bindable]
        public partial bool PositionConnected { get; set; }

        [Bindable]
        public partial bool LiveVolDataConnected { get; set; }

        public bool ComplexOrderTicketModuleGranted => OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) && OmsCore.Config.DefaultOrderTicketStyle == OrderTicketStyle.Complex;
        public bool CombinedOrderTicketModuleGranted => OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket) && OmsCore.Config.DefaultOrderTicketStyle == OrderTicketStyle.Combined;
        public bool CoLoTraderModuleGranted => true;

        //Todo
        public bool PnlReportModuleGranted => true;

        public double ServerCreep { get; private set; }
        public bool IsDisposed { get; set; }

        public MainViewModel(IModuleFactory moduleFactory,
                             TransactionConsumerModel transactionConsumer,
                             NotificationManager notificationManager,
                             DispatcherStore dispatcherStore,
                             OmsCore omsCore)
        {
            _notificationManager = notificationManager;
            _transactionConsumerModel = transactionConsumer;
            _dispatcherStore = dispatcherStore;
            OmsCore = omsCore;
            ModuleFactory = moduleFactory;
            SetDisconnectTrigger();
            OmsCore.GatewayClient.ConnectionStatusChangedEvent += OnGatewayConnectionStatusChanged;
            if (OmsCore.Config != null)
            {
                OmsCore.Config.PropertyChanged += OnConfigPropertyChanged;
            }
        }

        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Toggling RouteOpsOrdersToAutoTraderDirect changes whether OPS-mode tickets
            // pull routes from the OrderClient or the AutoTraderClient (and whether the
            // broker prefix is applied on the wire). Refresh every visible ticket and
            // basket so the dropdowns stay in sync with the new wire format.
            if (e.PropertyName == nameof(OmsConfig.RouteOpsOrdersToAutoTraderDirect))
            {
                ApplyConfigChangeToTickets();
                ApplyConfigChangeToBaskets();
            }
        }

        private static void ApplyConfigChangeToTickets()
        {
            IEnumerable<Window> tickets = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<ComplexOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<CombinedOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<OrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<DualOrderTicketView>())));
            foreach (Window ticketView in tickets)
            {
                try
                {
                    ticketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (ticketView.DataContext is ComplexOrderTicketViewModel { IsLowLatencyHangManager: false } ticketViewModel)
                        {
                            ticketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyConfigChangeToTickets));
                }
            }
        }

        private static void ApplyConfigChangeToBaskets()
        {
            foreach (Window basketView in StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>())
            {
                try
                {
                    basketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (basketView.DataContext is BasketTraderViewModel basketViewModel)
                        {
                            basketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyConfigChangeToBaskets));
                }
            }
        }

        private void SetDisconnectTrigger()
        {
            var delay = (int)(DateTime.Today + TimeSpan.FromDays(1) + TimeSpan.FromHours(5) - DateTime.Now).TotalMilliseconds;
            Task.Delay(delay).ContinueWith(t =>
            {
                DisconnectClients();
                SetDisconnectTrigger();
            });

            var killMdTime = (int)(TimeSpan.FromHours(17) - DateTime.Now.TimeOfDay).TotalMilliseconds;
            if (killMdTime > 0)
            {
                Task.Delay(killMdTime).ContinueWith(t => OmsCore.QuoteClient.StopAsync());
            }
        }

        private void OnGatewayConnectionStatusChanged(bool connected)
        {
            if (connected)
            {
                return;
            }
            _log.Info("Disconnected from gateway server!");
            if (QuoteConnected)
            {
                _ = OmsCore.QuoteClient.StopAsync();
            }
        }

        [Command]
        public void InstanceAccountChangingCommand(EditValueChangingEventArgs eventArgs)
        {
            if (eventArgs.OldValue != null)
            {
                MessageBoxService?.ShowMessage($"Instance Account Changed.\nOld Account: {eventArgs.OldValue}\nNew Account: {eventArgs.NewValue}!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
            }
        }

        [Command]
        public void InstanceBrokerChangingCommand(EditValueChangingEventArgs eventArgs)
        {
            if (eventArgs?.OldValue != null && !Equals(eventArgs.OldValue, eventArgs.NewValue))
            {
                MessageBoxService?.ShowMessage($"Instance Broker Changed.\nOld Broker: {eventArgs.OldValue}\nNew Broker: {eventArgs.NewValue}!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
            }
        }

        [Command]
        public void InstanceBrokerChangedCommand()
        {
            try
            {
                OmsCore.Config?.SaveAccountConfigs();
                ApplyBrokerChangeToTickets();
                ApplyBrokerChangeToBaskets();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(InstanceBrokerChangedCommand));
            }
        }

        private static void ApplyBrokerChangeToTickets()
        {
            IEnumerable<Window> tickets = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<ComplexOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<CombinedOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<OrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<DualOrderTicketView>())));
            foreach (Window ticketView in tickets)
            {
                try
                {
                    ticketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (ticketView.DataContext is ComplexOrderTicketViewModel { IsLowLatencyHangManager: false } ticketViewModel &&
                            string.IsNullOrEmpty(ticketViewModel.BrokerOverride))
                        {
                            ticketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyBrokerChangeToTickets));
                }
            }
        }

        private static void ApplyBrokerChangeToBaskets()
        {
            foreach (Window basketView in StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>())
            {
                try
                {
                    basketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (basketView.DataContext is BasketTraderViewModel basketViewModel &&
                            string.IsNullOrEmpty(basketViewModel.BrokerOverride))
                        {
                            basketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyBrokerChangeToBaskets));
                }
            }
        }

        [Command]
        public void ApplyInstanceModeChangesCommand()
        {
            try
            {
                ApplyInstanceModeChangeToTickets();
                ApplyInstanceModeChangeToBaskets();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ApplyInstanceModeChangesCommand));
            }
        }

        private static void ApplyInstanceModeChangeToTickets()
        {
            IEnumerable<Window> tickets = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<ComplexOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<CombinedOrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<OrderTicketView>()
                                                   .Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<DualOrderTicketView>())));
            foreach (Window ticketView in tickets)
            {
                try
                {
                    ticketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (ticketView.DataContext is ComplexOrderTicketViewModel { IsLowLatencyHangManager: false } ticketViewModel)
                        {
                            ticketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyInstanceModeChangeToTickets));
                }
            }
        }

        private static void ApplyInstanceModeChangeToBaskets()
        {
            foreach (Window basketView in StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>())
            {
                try
                {
                    basketView?.Dispatcher?.BeginInvoke(() =>
                    {
                        if (basketView.DataContext is BasketTraderViewModel basketViewModel)
                        {
                            basketViewModel.ApplyInstanceModeChangesCommand();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(ApplyInstanceModeChangeToBaskets));
                }
            }
        }

        private void OnAlertMessage(ZeroPlus.Models.Data.Update.AlertMessageModel alertMessage, string source)
        {
            try
            {
                if (OmsCore.Config.ShowOrderRateAlertV2)
                {
                    _log.Info("Alert. Id: {}, Time: {}, Msg: {}", alertMessage.AlertId, alertMessage.Time, alertMessage.Message);
                    _notificationManager?.AddAlert(alertMessage.Message, alertMessage.Time, source, "Alert:" + alertMessage.AlertId);
                }
            }
            catch (Exception) { }
        }

        [Command]
        public void ClosingCommand(CancelEventArgs arg)
        {
            MessageResult? result = MessageBoxService?.ShowMessage($"Do you want to save your workspace before quitting?", "ZeroPlus OMS", MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No);
            switch (result)
            {
                case MessageResult.Cancel:
                    arg.Cancel = true;
                    return;
                case MessageResult.Yes:
                    OmsCore.RequestSaveWorkspace();
                    break;
            }
            _ = ShutdownPython();
            Environment.Exit(0);
        }

        [Command]
        public async Task ReconnectCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return;
            }

            switch (args)
            {
                case "Order" when OmsCore.Config.OrderClientEnabled:
                    await OmsCore.OrderClient.RestartAsync();
                    break;
                case "Position" when OmsCore.Config.PositionClientEnabled:
                    await OmsCore.OrderClient.RestartPositionsAsync();
                    break;
                case "Quote" when OmsCore.Config.QuoteClientEnabled:
                    await OmsCore.QuoteClient.StopAsync();
                    await VerifyUserEntitlement().ContinueWith(async t =>
                    {
                        if (t.Result)
                        {
                            await OmsCore.QuoteClient.StartAsync();
                        }
                    });
                    break;
                case "Trades" when OmsCore.Config.RequestClientEnabled:
                    await OmsCore.TradesClient.RestartAsync();
                    break;
                case "Hanweck" when OmsCore.Config.HanweckClientEnabled:
                    await OmsCore.GreekClient.RestartAsync();
                    break;
                case "Hercules" when OmsCore.Config.TransactionClientEnabled:
                    await Task.Run(() => OmsCore.HerculesClient.Reconnect());
                    break;
                case "EdgeScanner" when OmsCore.Config.EdgeScannerClientEnabled:
                    await OmsCore.EdgeScannerClient.RestartAsync();
                    break;
                case "EdgeScanFeedRunner" when OmsCore.Config.EdgeScanFeedRunnerClientEnabled:
                    await OmsCore.EdgeScanFeedRunnerClient.RestartAsync();
                    break;
                case "SymbolMap" when OmsCore.Config.SymbolMapClientEnabled:
                    await OmsCore.SymbolMapClient.RestartAsync();
                    break;
                case "Telemetry" when OmsCore.Config.TelemetryClientEnabled:
                    await OmsCore.TelemetryClient.RestartAsync();
                    break;
                case "Ema" when OmsCore.Config.EmaClientEnabled:
                case "FullEma" when OmsCore.Config.EmaClientEnabled:
                    await OmsCore.FullEmaClient.RestartAsync();
                    break;
                case "EMAServer" when OmsCore.Config.DaEmaClientEnabled:
                    OmsCore.EmaServerClientModel.Restart();
                    break;
                case "Interpolator" when OmsCore.Config.InterpolatorClientEnabled:
                    await OmsCore.InterpolatorClient.RestartAsync();
                    break;
                case "Theos" when OmsCore.Config.TheosClientEnabled:
                    await OmsCore.TheosClient.RestartAsync();
                    break;
                case "HubTron" when OmsCore.Config.HubTronClientEnabled:
                    await OmsCore.HubTronClient.RestartAsync();
                    break;
                case "IbGateway" when OmsCore.Config.IbGatewayClientEnabled:
                    await OmsCore.IbGatewayClient.RestartAsync();
                    break;
                case "Databento" when OmsCore.Config.DatabentoClientEnabled:
                    await OmsCore.DatabentoClient.RestartAsync();
                    break;
                case "OrderGateway" when OmsCore.Config.AutoTraderClientEnabled:
                    await OmsCore.AutoTraderClient.RestartAsync();
                    break;
                case "Raptor" when OmsCore.Config.DerivativesClientEnabled:
                    await OmsCore.UpdateManager.RestartAsync();
                    break;
                case "Cob" when OmsCore.Config.CobClientEnabled:
                    await OmsCore.CobClient.RestartAsync();
                    break;
                case "Pricing" when OmsCore.Config.PricingClientEnabledV2:
                    await OmsCore.PricingClient.RestartAsync();
                    break;
                case "AutoTraderDirect" when OmsCore.Config.AutoTraderDirectClientEnabled:
                    await OmsCore.AutoTraderDirectClient.RestartAsync();
                    break;
                case "LiveVolData" when OmsCore.Config.LiveVolDataClientEnabled:
                    await OmsCore.LiveVolDataClient.RestartAsync();
                    break;
            }
        }

        private static async Task ShutdownPython()
        {
            try
            {
                if (OmsCore.Config.EnablePythonEngine)
                {
                    await Task.Run(Python.Runtime.PythonEngine.Shutdown);
                }
            }
            catch (Exception)
            {
            }
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            Initialize();
            ReloadFavorites();
        }

        protected override void OnInitializeInRuntime()
        {
            base.OnInitializeInRuntime();
            FavoriteModules = new ObservableCollection<FavoriteModuleGroupModel>();
        }

        public void Initialize()
        {
            try
            {
                ModuleTitle = MODULE_TITLE;

                OmsCore.Config.ConfigChangedEvent += Config_ConfigChangedEvent;

                OmsCore.SetupOrderClients();
                OmsCore.AppUpdateManager.NewVersionAvailableEvent += (updateInfo) =>
                {
                    _updateInfo = updateInfo;
                    UpdateAvailable = true;
                };
                OmsCore.OrderClient.ConnectionStatusChangedEvent += connected => { OrderConnected = connected; Username = OmsCore.User.Username; };
                OmsCore.OrderClient.PositionConnectionStatusChangedEvent += connected => PositionConnected = connected;
                OmsCore.QuoteClient.ConnectionStatusChangedEvent += connected => { QuoteConnected = connected; SubscribeCreep(connected); };
                OmsCore.TradesClient.ConnectionStatusChangedEvent += connected => TradeRequestConnected = connected;
                OmsCore.GreekClient.ConnectionStatusChangedEvent += connected => HanweckConnected = connected;
                OmsCore.AutoTraderClient.ConnectionStatusChangedEvent += connected => OrderGatewayConnected = connected;
                OmsCore.AutoTraderDirectClient.ConnectionStatusChangedEvent += connected => AutoTraderDirectConnected = connected;
                OmsCore.UpdateManager.ConnectionStatusChangedEvent += connected => RaptorConnected = connected;
                OmsCore.EdgeScannerClient.ConnectionStatusChangedEvent += connected => EdgeScannerConnected = connected;
                OmsCore.EdgeScanFeedRunnerClient.ConnectionStatusChangedEvent += connected => EdgeScanFeedRunnerConnected = connected;
                OmsCore.SymbolMapClient.ConnectionStatusChangedEvent += connected => SymbolMapConnected = connected;
                OmsCore.TelemetryClient.ConnectionStatusChangedEvent += connected => TelemetryConnected = connected;
                OmsCore.FullEmaClient.ConnectionStatusChangedEvent += connected => FullEmaConnected = connected;
                OmsCore.EmaServerClientModel.ConnectionStatusChangedEvent += connected => EmaServerConnected = connected;
                OmsCore.InterpolatorClient.ConnectionStatusChangedEvent += connected => InterpolatorConnected = connected;
                OmsCore.TheosClient.ConnectionStatusChangedEvent += connected => TheosConnected = connected;
                OmsCore.HubTronClient.ConnectionStatusChangedEvent += connected => HubTronConnected = connected;
                OmsCore.IbGatewayClient.ConnectionStatusChangedEvent += connected => IbGatewayConnected = connected;
                OmsCore.DatabentoClient.ConnectionStatusChangedEvent += connected => DatabentoConnected = connected;
                OmsCore.CobClient.ConnectionStatusChangedEvent += connected => CobConnected = connected;
                OmsCore.PricingClient.ConnectionStatusChangedEvent += connected => PricingConnected = connected;
                OmsCore.LiveVolDataClient.ConnectionStatusChangedEvent += connected => LiveVolDataConnected = connected;
                OmsCore.HerculesClient.ClientConnected += () => HerculesConnected = true;
                OmsCore.HerculesClient.ClientDisconnected += () => HerculesConnected = false;

                NotificationsView notificationsView = new(OmsCore.Config);
                notificationsView.Show();
                NotificationsViewModel notificationsViewModel = (NotificationsViewModel)notificationsView.DataContext;
                notificationsViewModel.Config = OmsCore.Config;
                _notificationManager.Subscribe(notificationsViewModel);

                if (OmsCore.Config.ConnectClientsOnStartupV2)
                {
                    Task.Run(ConnectClients);
                }

                if (OmsCore.Config.DominatorsManagerListenerEnabled)
                {
                    Task.Run(() => OmsCore.DominatorsManager.StartServerAsync());
                    OmsCore.DominatorsManager.DominatorPlaySoundRequestEvent += OnDominatorPlaySoundRequestEvent;
                    OmsCore.DominatorsManager.OpenTicketRequestEvent += OnOpenTicketRequestEvent;
                    OmsCore.DominatorsManager.CloseTicketRequestEvent += OnCloseTicketRequestEvent;
                    OmsCore.DominatorsManager.OpenBasketRequestEvent += OnOpenBasketRequestEvent;
                    OmsCore.DominatorsManager.OpenChartRequestEvent += OnOpenChartRequestEvent;
                }

                if (OmsCore.Config.BasketManagerListenerEnabledV2)
                {
                    Task.Run(() => OmsCore.BasketManager.StartServerAsync());
                    OmsCore.BasketManager.OpenTicketRequestEvent += OnOpenTicketRequestEvent;
                }

                if (OmsCore.User.Modules.Contains((int)Module.OrderRateAlerts))
                {
                    OmsCore.HerculesClient.AlertMessage += message => OnAlertMessage(message, "Transaction");
                }
                OmsCore.EdgeScannerClient.ScannerClient.AlertMessage += message => OnAlertMessage(message, "Edge Scan Feed");
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Crash Report.\n{ex.Message}\nPlease restart program.", "Crash Report - ZeroPlus OMS", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }
        }

        private void Config_ConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            ReloadFavorites();
        }

        private void ReloadFavorites()
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    FavoriteModules.Clear();
                    foreach (FavoriteModuleGroupModel favoriteModule in OmsCore.Config.FavoriteModules.ToList())
                    {
                        FavoriteModules.Add(favoriteModule);
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadFavorites));
            }
        }

        private void SubscribeCreep(bool connected = true)
        {
            if (connected)
            {
                OmsCore.QuoteClient.Subscribe(String.Empty, SubscriptionFieldType.ServerClockUpdate, this);

                if (!_connectionWatcherRunning)
                {
                    _lastUpdate = DateTime.Now;
                    _connectionWatcherRunning = true;
                    Task.Run(() => RunConnectionWatchAsync());
                }
            }
            else
            {
                _connectionWatcherRunning = false;
                StopAlert();
            }
        }

        private async Task RunConnectionWatchAsync()
        {
            while (_connectionWatcherRunning)
            {
                await Task.Delay(WATCHER_INTERVAL);

                double creepSec = OmsCore.QuoteClient.ServerCreepMs / 1000.0;
                if (creepSec > ALERT_CREEP_THRESHOLD)
                {
                    _log.Info("Alert Creep Threshold Passed.");
                }

                if ((DateTime.Now - _lastUpdate).TotalMilliseconds >= WARN_DELAY_SERVER_TIME_THRESHOLD)
                {
                    ServerCreep = 999;
                    ShowAlert();
                    _log.Info("Stale Server Time Threshold Passed. Last update: " + _lastUpdate);
                }
                else if ((DateTime.Now - _lastUpdate).TotalMilliseconds >= DELAY_SERVER_TIME_THRESHOLD)
                {
                    ServerCreep = 999;
                    SubscribeCreep();
                    _log.Info("Stale Server Time Threshold Passed. Last update: " + _lastUpdate);
                }
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.ServerClockUpdate && value is DateTime clockUpdate)
            {
                _lastUpdate = DateTime.Now;
                string clockUpdateString = clockUpdate.ToString("hh:mm:ss.fff");
                if (_lastServerTime == clockUpdateString)
                {
                    _lastServerTimeSame++;
                    if (_lastServerTimeSame > STALE_SERVER_TIME_THRESHOLD)
                    {
                        ServerCreep = 999;
                        ShowAlert();
                        _log.Info("Stale Server Time Threshold Passed. Update: " + clockUpdateString + ", Count: " + _lastServerTimeSame);
                    }
                }
                else
                {
                    _lastServerTime = clockUpdateString;
                    _lastServerTimeSame = 0;
                    StopAlert();
                }
            }
        }

        public void LoadInitialWindows()
        {
            foreach ((string windowName, string windowGuid) in WindowHelper.NameAndUidPairsForStartupModules)
            {
                switch (windowName)
                {
                    case nameof(ComplexOrderTicketView):
                        ComplexOrderTicket(windowGuid, OmsCore, ModuleFactory);
                        break;
                    case nameof(CombinedOrderTicketView):
                        CombinedOrderTicket(windowGuid, OmsCore);
                        break;
                    case nameof(OrderTicketView):
                        OrderTicket(windowGuid);
                        break;
                    case nameof(DualOrderTicketView):
                        DualOrderTicket(windowGuid);
                        break;
                    case nameof(Module.BasketTrader):
                    case nameof(Module.BasketTraderLayout):
                    case nameof(BasketTraderView):
                        ModuleFactory.CreateModule(Module.BasketTrader, windowGuid);
                        break;
                    case nameof(VolTraderView):
                        VolTrader(windowGuid, OmsCore);
                        break;
                    case nameof(EmptyOrderBookView):
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            EmptyOrderBookView customWindow = new(windowGuid);
                            customWindow.Show();
                        });
                        break;
                    case nameof(OrderBookWindowView):
                        OrderBookWindowView orderbookWindow = new(windowGuid);
                        orderbookWindow.Show();
                        break;
                    case nameof(TradeFeedView):
                        TradeFeedView tradeFeedView = new(windowGuid);
                        tradeFeedView.Show();
                        break;
                    case nameof(PortfolioView):
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            PortfolioView portfolioWindow = new(windowGuid);
                            portfolioWindow.Show();
                        });
                        break;
                    case nameof(PositionAnalyzerView):
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            PositionAnalyzerView portfolioWindow = new(windowGuid);
                            portfolioWindow.Show();
                        });
                        break;
                    case nameof(DeltaHedgingView):
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            DeltaHedgingView deltaHedgingView = new(windowGuid);
                            deltaHedgingView.Show();
                        });
                        break;
                    case nameof(HedgeHouseView):
                        _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
                        {
                            HedgeHouseView deltaHedgingView = new(windowGuid);
                            deltaHedgingView.Show();
                        }));
                        break;
                    case nameof(GammaScalpingModuleView):
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            GammaScalpingModuleView gammaScalpView = new(windowGuid);
                            gammaScalpView.Show();
                        });
                        break;
                    case nameof(DominatorView):
                        Dominator(windowGuid);
                        break;
                    case nameof(OptionChainView):
                        OptionChain(windowGuid, OmsCore);
                        break;
                    case nameof(SpreadsGeneratorView):
                        ModuleFactory.CreateModule(Module.SpreadsGenerator, windowGuid);
                        break;
                    case nameof(SpreadTemplateView):
                        SpreadTemplate(windowGuid, OmsCore);
                        break;
                    case nameof(SpreadHeatmapView):
                        SpreadsHeatmap(windowGuid, OmsCore);
                        break;
                    case nameof(TradesView):
                        Trades(windowGuid, OmsCore);
                        break;
                    case nameof(PnlReportView):
                        PnlReport();
                        break;
                    case nameof(Module.EdgeScanFeed):
                    case nameof(EdgeScanFeedView):
                        ModuleFactory.CreateModule(Module.EdgeScanFeed, windowGuid);
                        break;
                    case nameof(Module.ScriptTrader):
                        ModuleFactory.CreateModule(Module.ScriptTrader, windowGuid);
                        break;
                    case nameof(Module.LowLatencyManager):
                        ModuleFactory.CreateModule(Module.LowLatencyManager, windowGuid);
                        break;
                    case nameof(Module.LowLatencyOrderBook):
                        ModuleFactory.CreateModule(Module.LowLatencyOrderBook, windowGuid);
                        break;
                    case nameof(Module.BulletinBoard):
                        ModuleFactory.CreateModule(Module.BulletinBoard, windowGuid);
                        break;
                    case nameof(Module.MarketMovers):
                        ModuleFactory.CreateModule(Module.MarketMovers, windowGuid);
                        break;
                    case nameof(Module.BasketGroup):
                        ModuleFactory.CreateModule(Module.BasketGroup, windowGuid);
                        break;
                    case nameof(Module.Dashboard):
                        ModuleFactory.CreateModule(Module.Dashboard, windowGuid);
                        break;
                    case nameof(Module.EodRisk):
                        ModuleFactory.CreateModule(Module.EodRisk, windowGuid);
                        break;
                    case nameof(Module.UserPosition):
                        ModuleFactory.CreateModule(Module.UserPosition, windowGuid);
                        break;
                    case nameof(Module.WinningTradesMonitor):
                        ModuleFactory.CreateModule(Module.WinningTradesMonitor, windowGuid);
                        break;
                    case nameof(Module.CloseSubsMonitor):
                        ModuleFactory.CreateModule(Module.CloseSubsMonitor, windowGuid);
                        break;
                    case nameof(Module.ExecutionTransaction):
                        ModuleFactory.CreateModule(Module.ExecutionTransaction, windowGuid);
                        break;
                    case nameof(DominatorsManagerView):
                        if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.DominatorsManager))
                        {
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                DominatorsManagerView dominatorsManagerView = new(windowGuid);
                                dominatorsManagerView.Show();
                            });
                        }
                        break;
                    case nameof(BasketManagerView):
                        if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketManager))
                        {
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                BasketManagerView basketManagerView = new(windowGuid);
                                basketManagerView.Show();
                            });
                        }
                        break;
                    case nameof(Module.QuotesAndGreeksBoard):
                        ModuleFactory.CreateModule(Module.QuotesAndGreeksBoard, windowGuid);
                        break;
                }
            }
        }

        #region Commands
        [Command]
        public void OpenFavoriteModuleCommand(FavoriteModuleModel favoriteModuleModel)
        {
            if (favoriteModuleModel != null && favoriteModuleModel.ConfigSave != null)
            {
                LoadFromConfig(favoriteModuleModel.ConfigSave);
            }
        }

        [Command]
        public void RemoveFavoriteModuleCommand(FavoriteModuleModel favoriteModuleModel)
        {
            if (favoriteModuleModel != null)
            {
                OmsCore.Config.RemoveFavoriteModule(favoriteModuleModel);
            }
        }

        [Command]
        public void ReportBug()
        {
            ReportBugView reportBugView = new();
            reportBugView.Show();
        }

        [Command]
        public void UpdateVersion() => OmsCore.AppUpdateManager.DoUpdateAvailable(_updateInfo);

        [Command]
        public void OpenLinkCommand(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLinkCommand));
            }
        }

        [Command]
        public void StartModuleCommand(Module module)
        {
            try
            {
                _log.Info($"Starting Module. Module: {module}");
                ModuleFactory.CreateModule(module);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error starting Module. Module: {module}");
            }
        }

        [Command]
        public void StartBlankModuleCommand(Module module)
        {
            try
            {
                _log.Info($"Starting Blank Module. Module: {module}");
                ModuleFactory.CreateModule(module, loadDefault: false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error starting blank Module. Module: {module}");
            }
        }

        [Command]
        public void PortfolioAdjustmentCommand()
        {
            _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
            {
                PortfolioAdjustmentView view = new();
                view.Show();
            }));
        }

        [Command]
        public void OptionChain()
        {
            OptionChain(string.Empty, OmsCore);
        }

        [Command]
        public void Dominator()
        {
            Dominator(string.Empty);
        }

        [Command]
        public void EmptyOrderBook()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                EmptyOrderBookView view = new();
                view.Show();
            });
        }

        [Command]
        public void SymbolListManagerCommand()
        {
            DynamicConfigManagementView view = new();
            if (view.DataContext is DynamicConfigManagementViewModel viewModel)
            {
                viewModel.ConfigModule = Module.CustomList;
                viewModel.Parent = this;
                viewModel.ShowLoadButton = false;
                view.Show();
            }
        }

        [Command]
        public void OrderBook()
        {
            Dispatcher.BeginInvoke(() =>
            {
                OrderBookWindowView view = new();
                view.Show();
            });
        }

        [Command]
        public void TradeFeed()
        {
            Dispatcher.BeginInvoke(() =>
            {
                TradeFeedView view = new();
                view.Show();
            });
        }

        [Command]
        public void ComplexOrderTicketCommand()
        {
            ComplexOrderTicket(string.Empty, OmsCore, ModuleFactory);
        }

        [Command]
        public void CombinedOrderTicketCommand()
        {
            CombinedOrderTicket(string.Empty, OmsCore);
        }

        [Command]
        public void OrderTicketCommand()
        {
            OrderTicket(string.Empty);
        }

        [Command]
        public void DualOrderTicketCommand()
        {
            DualOrderTicket(string.Empty);
        }

        [Command]
        public void VolTrader()
        {
            VolTrader(string.Empty, OmsCore);
        }

        [Command]
        public void LockTrader()
        {
            LockTrader(string.Empty);
        }

        [Command]
        public void CoLoTradeManagerCommand()
        {
            CoLoTradeManager(string.Empty);
        }

        [Command]
        public void Trades()
        {
            Trades(string.Empty, OmsCore);
        }

        [Command]
        public void Chart()
        {
            Chart(string.Empty, OmsCore);
        }

        [Command]
        public void LiveChartCommand()
        {
            LiveChart(string.Empty);
        }

        [Command]
        public void EmaChartCommand()
        {
            EmaChart(string.Empty);
        }

        [Command]
        public void SpreadsGenerator()
        {
            ModuleFactory.CreateModule(Module.SpreadsGenerator, string.Empty);
        }

        [Command]
        public void SpreadTemplate()
        {
            SpreadTemplate(string.Empty, OmsCore);
        }

        [Command]
        public void SpreadsHeatmap()
        {
            SpreadsHeatmap(string.Empty, OmsCore);
        }

        [Command]
        public void RequestFishedSymbolsCommand()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                FishedSymbolsRequestView view = new();
                view.Show();
            });
        }

        [Command]
        public void PnlReport()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PnlReportView view = new();
                view.Show();
            });
        }

        [Command]
        public void DominatorsManager()
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.DominatorsManager))
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    DominatorsManagerView view = new();
                    view.Show();
                });
            }
        }

        [Command]
        public void BasketManager()
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketManager))
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    BasketManagerView view = new();
                    view.Show();
                });
            }
        }

        [Command]
        public void DominatorListRollerCommand()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                DominatorListRollerView view = new();
                view.Show();
            });
        }

        [Command]
        public void Portfolio()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PortfolioView view = new();
                view.Show();
            });
        }

        [Command]
        public void PositionAnalyzer()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PositionAnalyzerView view = new();
                view.Show();
            });
        }

        [Command]
        public void DeltaHedgingCommand()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                DeltaHedgingView view = new();
                view.Show();
            });
        }

        [Command]
        public void HedgeHouseCommand()
        {
            _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
            {
                HedgeHouseView view = new();
                view.Show();
            }));
        }

        [Command]
        public void GammaScalpingCommand()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                GammaScalpingModuleView view = new();
                view.Show();
            });
        }

        [Command]
        public void ComboTraderCommand()
        {
            ComboTrader(string.Empty);
        }

        [Command]
        public void PairTraderCommand()
        {
            PairTrader(string.Empty);
        }

        [Command]
        public void ModelTraderCommand()
        {
            ModelTrader(string.Empty);
        }

        [Command]
        public void MlTradersControlCommand()
        {
            MlTradersControl(string.Empty);
        }

        [Command]
        public void PairTradersControllerCommand()
        {
            PairTradersController(string.Empty);
        }

        [Command]
        public void Exit()
        {
            MessageResult? result = MessageResult.No;
            Dispatcher.Invoke(new Action(() => result = MessageBoxService?.ShowMessage($"Do you want to save your workspace before quitting?", "ZeroPlus OMS", MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No)));

            switch (result)
            {
                case MessageResult.Cancel:
                    return;
                case MessageResult.Yes:
                    OmsCore.RequestSaveWorkspace();
                    break;
            }

            _ = ShutdownPython();
            Environment.Exit(0);
        }

        [Command]
        public void SaveWorkspace()
        {
            try
            {
                SaveView view = new();

                SaveViewModel viewModel = view.DataContext as SaveViewModel;
                viewModel.ShowGroup = false;
                viewModel.ShowDefault = false;
                viewModel.Workspace = true;

                viewModel.SetDispatcher(view.Dispatcher);

                if (OmsCore.Config.WorkspaceTitle != OmsConfig.DEFAULT_WORKSPACE)
                {
                    viewModel.Title = OmsCore.Config.WorkspaceTitle;
                }

                view.ShowDialog();

                if (viewModel.Success)
                {
                    OmsCore.RequestSaveWorkspace();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SaveWorkspace)} -> Exception saving workspace.");
            }
        }

        [Command]
        public void LoadWorkspace()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.Workspace);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(LoadWorkspace)} -> Exception loading workspace.");
            }
        }

        [Command]
        public void SaveWorkspaceAndRestart()
        {
            try
            {
                SaveWorkspace();
                StartupWindowViewModel.RestartProgram("Save Workspace And Restart");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SaveWorkspaceAndRestart)} -> Exception saving workspace.");
            }
        }

        [Command]
        public void ExportSettings()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "zip";
                SaveFileDialogService.DefaultFileName = $"ZeroPlus OMS {OmsCore.User.Username} Layout";
                SaveFileDialogService.Filter = "Zip|*.zip";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    string configPath = OmsConfig.GetConfigDirectory();
                    string filePath = SaveFileDialogService.GetFullFileName();
                    ZipFile.CreateFromDirectory(configPath, filePath);
                    Dispatcher.BeginInvoke(new Action(() => MessageBoxService.ShowMessage($"Layout exported to\n{filePath}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Information)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportSettings));
                Dispatcher.BeginInvoke(new Action(() => MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
        }

        [Command]
        public async void ImportSettings()
        {
            try
            {
                OpenFileDialogService.Filter = "Zip|*.zip";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    string filePath = file.GetFullName();
                    string configPath = OmsConfig.GetConfigDirectory();

                    if (Directory.Exists(configPath))
                    {
                        string backup = configPath + "old";
                        if (Directory.Exists(backup))
                        {
                            Directory.Delete(backup, true);
                        }
                        Directory.Move(configPath, backup);
                    }

                    ZipFile.ExtractToDirectory(filePath, configPath);

                    MessageResult messageResult = MessageResult.None;
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        messageResult = MessageBoxService.ShowMessage("Layout imported successfully. Restart required.\n"
                                                                      + "Would you like to restart now?",
                                                                        "ZeroPlus OMS",
                                                                         MessageButton.YesNo,
                                                                         MessageIcon.Question,
                                                                         MessageResult.Yes);
                    }));
                    switch (messageResult)
                    {
                        case MessageResult.Yes:
                            StartupWindowViewModel.RestartProgram("Settings Import");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ImportSettings));
                _ = Dispatcher.BeginInvoke(new Action(() => MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
        }

        [Command]
        public void Settings()
        {
            SettingsView settingsView = new();
            settingsView.Show();
        }

        [Command]
        public void ConnectClients()
        {
            if (!OrderConnected && OmsCore.Config.OrderClientEnabled)
            {
                _ = OmsCore.OrderClient.StartAsync();
            }

            if (!PositionConnected && OmsCore.Config.PositionClientEnabled)
            {
                _ = OmsCore.OrderClient.StartPositionsAsync();
            }

            if (!QuoteConnected && OmsCore.Config.QuoteClientEnabled)
            {
                _ = VerifyUserEntitlement().ContinueWith(t =>
                {
                    if (t.Result)
                    {
                        _ = OmsCore.QuoteClient.StartAsync();
                    }
                });
            }

            if (!TradeRequestConnected && OmsCore.Config.RequestClientEnabled)
            {
                _ = OmsCore.TradesClient.StartAsync();
            }

            if (!HanweckConnected && OmsCore.Config.HanweckClientEnabled)
            {
                _ = OmsCore.GreekClient.StartAsync();
            }

            if (!RaptorConnected && OmsCore.Config.DerivativesClientEnabled)
            {
                _ = OmsCore.UpdateManager.StartAsync();
            }

            if (!EdgeScannerConnected && OmsCore.Config.EdgeScannerClientEnabled)
            {
                _ = OmsCore.EdgeScannerClient.StartAsync();
            }

            if (!EdgeScanFeedRunnerConnected && OmsCore.Config.EdgeScanFeedRunnerClientEnabled)
            {
                _ = OmsCore.EdgeScanFeedRunnerClient.StartAsync();
            }

            if (!SymbolMapConnected && OmsCore.Config.SymbolMapClientEnabled)
            {
                _ = OmsCore.SymbolMapClient.StartAsync();
            }

            if (!TelemetryConnected && OmsCore.Config.TelemetryClientEnabled)
            {
                _ = OmsCore.TelemetryClient.StartAsync();
            }

            if (!FullEmaConnected && OmsCore.Config.EmaClientEnabled)
            {
                _ = OmsCore.FullEmaClient.StartAsync();
            }

            if (!EmaServerConnected && OmsCore.Config.DaEmaClientEnabled)
            {
                _ = Task.Run(() => OmsCore.EmaServerClientModel.Start());
            }

            if (!InterpolatorConnected && OmsCore.Config.InterpolatorClientEnabled)
            {
                _ = OmsCore.InterpolatorClient.StartAsync();
            }

            if (!TheosConnected && OmsCore.Config.TheosClientEnabled)
            {
                _ = OmsCore.TheosClient.StartAsync();
            }

            if (!HubTronConnected && OmsCore.Config.HubTronClientEnabled)
            {
                _ = OmsCore.HubTronClient.StartAsync();
            }

            if (!IbGatewayConnected && OmsCore.Config.IbGatewayClientEnabled)
            {
                _ = OmsCore.IbGatewayClient.StartAsync();
            }

            if (!DatabentoConnected && OmsCore.Config.DatabentoClientEnabled)
            {
                _ = OmsCore.DatabentoClient.StartAsync();
            }

            if (!CobConnected && OmsCore.Config.CobClientEnabled)
            {
                _ = OmsCore.CobClient.StartAsync();
            }

            if (!PricingConnected && OmsCore.Config.PricingClientEnabledV2)
            {
                _ = OmsCore.PricingClient.StartAsync();
            }

            if (!OrderGatewayConnected && OmsCore.Config.AutoTraderClientEnabled)
            {
                _ = OmsCore.AutoTraderClient.StartAsync();
            }

            if (!AutoTraderDirectConnected && OmsCore.Config.AutoTraderDirectClientEnabled)
            {
                _ = OmsCore.AutoTraderDirectClient.StartAsync();
            }

            if (!HerculesConnected && OmsCore.Config.TransactionClientEnabled)
            {
                bool portfolioDispatcherSet = _dispatcherStore.GetDispatcherForModule(Module.Portfolio) != null;
                if (portfolioDispatcherSet)
                {
                    _ = OmsCore.HerculesClient.ConnectAndStart();
                }
            }

            if (!LiveVolDataConnected && OmsCore.Config.LiveVolDataClientEnabled)
            {
                _ = OmsCore.LiveVolDataClient.StartAsync();
            }
        }

        private async Task<bool> VerifyUserEntitlement()
        {
            if (!OmsCore.GatewayClient.IsConnected)
            {
                Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Not Entitled to Market Data.\nConnection to Auth Server Down!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Information));
                return false;
            }

            if (OmsCore.User.MaxDuplicateSessions == 0)
            {
                Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Not Entitled to Market Data.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Information));
                return false;
            }

            var users = await OmsCore.GatewayClient.GetUsersAsync();
            var currentUserOnline = users?.FirstOrDefault(x => x.ID == OmsCore.User.ID);
            if (currentUserOnline == null)
            {
                Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Not Entitled to Market Data.\nCurrent User Lookup Failed!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error));
                return false;
            }

            var verifyUserEntitlement = OmsCore.User.MaxDuplicateSessions > currentUserOnline.MaxDuplicateSessions - 1;
            if (!verifyUserEntitlement)
            {
                Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage($"Not Entitled to Market Data.\nMax number of active sessions reached.\nMax: {OmsCore.User.MaxDuplicateSessions}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning));
            }
            return verifyUserEntitlement;
        }

        [Command]
        public void DisconnectClients()
        {
            _ = OmsCore.OrderClient.StopAsync();
            _ = OmsCore.OrderClient.StopPositionsAsync();
            _ = OmsCore.QuoteClient.StopAsync();
            _ = OmsCore.TradesClient.StopAsync();
            _ = OmsCore.GreekClient.StopAsync();
            _ = OmsCore.UpdateManager.StopAsync();
            _ = OmsCore.EdgeScannerClient.StopAsync();
            _ = OmsCore.EdgeScanFeedRunnerClient.StopAsync();
            _ = OmsCore.SymbolMapClient.StopAsync();
            _ = OmsCore.TelemetryClient.StopAsync();
            _ = OmsCore.FullEmaClient.StopAsync();
            _ = OmsCore.InterpolatorClient.StopAsync();
            _ = OmsCore.TheosClient.StopAsync();
            _ = OmsCore.HubTronClient.StopAsync();
            _ = OmsCore.IbGatewayClient.StopAsync();
            _ = OmsCore.DatabentoClient.StopAsync();
            _ = OmsCore.CobClient.StopAsync();
            _ = OmsCore.PricingClient.StopAsync();
            _ = OmsCore.AutoTraderClient.StopAsync();
            _ = OmsCore.AutoTraderDirectClient.StopAsync();
            _ = OmsCore.HerculesClient.DisconnectAndStop();
            _ = OmsCore.LiveVolDataClient.StopAsync();
            OmsCore.EmaServerClientModel.Stop();
        }

        [AsyncCommand]
        public async Task CheckForUpdate()
        {
            try
            {
                await OmsCore.AppUpdateManager.CheckForUpdateAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(CheckForUpdate)} -> Exception checking for update.");
            }
        }

        [Command]
        public void ChangeLog()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ChangeLogView view = new();
                    view.Show();
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChangeLog));
            }
        }

        [Command]
        public async Task GatherUsageStatsCommand()
        {
            try
            {
                var duration = 90_000;
                var proceed = GetConfirmation(duration);
                if (proceed)
                {
                    Profiler profiler = new Profiler();
                    await profiler.StartTracing(duration);
                    MessageBoxService.Show("Trace saved to " + profiler.OutputFilePath, "Gather Usage Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBoxService.Show("Trace cancelled!", "Gather Usage Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GatherUsageStatsCommand));
            }
        }

        [Command]
        public void GenerateMemoryDumpCommand()
        {
            try
            {
                var duration = 45_000;
                var proceed = GetConfirmation(duration);
                if (proceed)
                {
                    Profiler profiler = new Profiler();
                    profiler.GenerateMemoryDump();
                    MessageBoxService.Show("Trace saved to " + profiler.OutputFilePath, "Gather Usage Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBoxService.Show("Trace cancelled!", "Gather Usage Statistics", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateMemoryDumpCommand));
            }
        }

        private bool GetConfirmation(int duration)
        {
            var result = MessageBoxService.ShowMessage(
                $"Initiating this diagnostic tracer will significantly impact application performance for the next {(duration / 1000D):N0}seconds. Before proceeding, you must ensure all automation and trading from this instance are stopped.\n\nDo you wish to continue?",
                "Gather Usage Statistics", MessageButton.YesNoCancel, MessageIcon.Warning, MessageResult.Cancel);
            var proceed = result == MessageResult.Yes;
            return proceed;
        }

        [Command]
        public void CloseAllTickets()
        {
            try
            {
                StartupWindowViewModel.MainWindow.WindowHelper.CloseAll<ComplexOrderTicketView>();
                StartupWindowViewModel.MainWindow.WindowHelper.CloseAll<CombinedOrderTicketView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseAllTickets));
            }
        }

        [Command]
        public void EmergencyStopCommand()
        {
            try
            {
                _log.Info("EMERGENCY STOP Requested");
                StopAllBaskets();
                StopAllTickets();
                _log.Info("EMERGENCY STOP Complete");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmergencyStopCommand));
            }
        }

        private static void StopAllBaskets()
        {
            try
            {
                foreach (Window basketView in StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>())
                {
                    try
                    {
                        basketView?.Dispatcher.Invoke(() =>
                        {
                            if (basketView.DataContext is BasketTraderViewModel basketViewModel)
                            {
                                AutomationConfigModel automationConfig = basketViewModel.GetAutomationConfig();
                                automationConfig.CloseOrderMode = null;
                                automationConfig.LoopingEnabled = false;
                                automationConfig.GoFishAutoCloseEnabled = false;
                                basketViewModel.CancelAll();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(EmergencyStopCommand));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmergencyStopCommand));
            }
        }

        private static void StopAllTickets()
        {
            try
            {
                foreach (Window ticketView in StartupWindowViewModel.MainWindow.WindowHelper.GetAll<ComplexOrderTicketView>().Union(StartupWindowViewModel.MainWindow.WindowHelper.GetAll<CombinedOrderTicketView>()))
                {
                    try
                    {
                        ticketView?.Dispatcher.Invoke(() =>
                        {
                            if (ticketView.DataContext is ComplexOrderTicketViewModel viewModel)
                            {
                                viewModel.CancelSpeedTrader();
                                viewModel.SpeedTraderClosingType = SpeedTraderClosingType.Off;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(EmergencyStopCommand));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EmergencyStopCommand));
            }
        }
        #endregion

        private void ShowAlert()
        {
            if (!_alertRunning)
            {
                _alertRunning = true;
                Dispatcher.BeginInvoke(() => ShowQuoteAlert = true);
            }
        }

        private void StopAlert()
        {
            if (_alertRunning)
            {
                _alertRunning = false;
                Dispatcher.BeginInvoke(() => ShowQuoteAlert = false);
            }
        }

        private static void ComplexOrderTicket(string windowId, OmsCore omsCore, IModuleFactory moduleFactory)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
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
                            window = !string.IsNullOrEmpty(windowId) ? new ComplexOrderTicketView(moduleFactory, windowId) : new ComplexOrderTicketView();
                            break;
                        case OrderTicketStyle.Combined:
                            window = !string.IsNullOrEmpty(windowId) ? new CombinedOrderTicketView(windowId) : new CombinedOrderTicketView();
                            break;
                        default:
                            return;
                    }

                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
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
        }

        private static void CombinedOrderTicket(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    CombinedOrderTicketView window = !string.IsNullOrEmpty(windowId) ? new CombinedOrderTicketView(windowId) : new CombinedOrderTicketView();
                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
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
        }

        private static void OrderTicket(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                OrderTicketView window = !string.IsNullOrEmpty(windowId) ? new OrderTicketView(windowId) : new OrderTicketView();
                ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
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

        private static void DualOrderTicket(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                DualOrderTicketView window = !string.IsNullOrEmpty(windowId) ? new DualOrderTicketView(windowId) : new DualOrderTicketView();
                ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
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

        private static void VolTrader(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    VolTraderView window = !string.IsNullOrEmpty(windowId) ? new VolTraderView(windowId) : new VolTraderView();
                    VolTraderViewModel viewModel = (VolTraderViewModel)window.DataContext;
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
        }

        private void LockTrader(string windowId)
        {
            ModuleFactory.CreateModule(Module.LockTrader, windowId);
        }

        private static void CoLoTradeManager(string windowId)
        {
            //if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.CoLoTradeManager))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    CoLoTradeManagerView window = !string.IsNullOrEmpty(windowId) ? new CoLoTradeManagerView(windowId) : new CoLoTradeManagerView();
                    CoLoTradeManagerViewModel viewModel = (CoLoTradeManagerViewModel)window.DataContext;
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
        }

        private static void OptionChain(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.LockTrader))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    OptionChainView window = !string.IsNullOrEmpty(windowId) ? new OptionChainView(windowId) : new OptionChainView();
                    OptionChainViewModel viewModel = (OptionChainViewModel)window.DataContext;
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
        }

        private static void Dominator(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                DominatorView window = !string.IsNullOrEmpty(windowId) ? new DominatorView(windowId) : new DominatorView();
                DominatorViewModel viewModel = (DominatorViewModel)window.DataContext;
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

        private static void SpreadTemplate(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    SpreadTemplateView window = !string.IsNullOrEmpty(windowId) ? new SpreadTemplateView(windowId) : new SpreadTemplateView();
                    SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
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
        }

        private static void SpreadsHeatmap(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.Heatmap))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    SpreadHeatmapView window = !string.IsNullOrEmpty(windowId) ? new SpreadHeatmapView(windowId) : new SpreadHeatmapView();
                    SpreadHeatmapViewModel viewModel = (SpreadHeatmapViewModel)window.DataContext;
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
        }

        private static void ComboTrader(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                ComboTraderView window = !string.IsNullOrEmpty(windowId) ? new ComboTraderView(windowId) : new ComboTraderView();
                ComboTraderViewModel viewModel = (ComboTraderViewModel)window.DataContext;
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

        private static void PairTradersController(string windowId)
        {
            PairTradersControllerView window = !string.IsNullOrEmpty(windowId) ? new PairTradersControllerView(windowId) : new PairTradersControllerView();
            window.Show();
        }

        private static void PairTrader(string windowId)
        {
            PairTraderView window = !string.IsNullOrEmpty(windowId) ? new PairTraderView(windowId) : new PairTraderView();
            window.Show();
        }

        private static void ModelTrader(string windowId)
        {
            ModelTraderView window = !string.IsNullOrEmpty(windowId) ? new ModelTraderView(windowId) : new ModelTraderView();
            ModelTraderViewModel viewModel = (ModelTraderViewModel)window.DataContext;
            viewModel.SetDispatcher(window.Dispatcher);
            window.Show();
        }

        private static void MlTradersControl(string windowId)
        {
            MlTradersControlView window = !string.IsNullOrEmpty(windowId) ? new MlTradersControlView(windowId) : new MlTradersControlView();
            window.Show();
        }

        private static void Trades(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    TradesView window = !string.IsNullOrEmpty(windowId) ? new TradesView(windowId) : new TradesView();
                    TradesViewModel viewModel = (TradesViewModel)window.DataContext;
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
        }

        private static void Chart(string windowId, OmsCore omsCore)
        {
            if (omsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    ChartModuleView window = !string.IsNullOrEmpty(windowId) ? new ChartModuleView(windowId) : new ChartModuleView();
                    ChartModuleViewModel viewModel = (ChartModuleViewModel)window.DataContext;
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
        }

        private static void LiveChart(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                LiveChartView window = !string.IsNullOrEmpty(windowId) ? new LiveChartView(windowId) : new LiveChartView();
                LiveChartViewModel viewModel = (LiveChartViewModel)window.DataContext;
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

        private void EmaChart(string windowId)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                EmaChartView window = !string.IsNullOrEmpty(windowId) ? new EmaChartView(ModuleFactory, windowId) : new EmaChartView(ModuleFactory);
                EmaChartViewModel viewModel = (EmaChartViewModel)window.DataContext;
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

        private void OnDominatorPlaySoundRequestEvent(int id, string name)
        {
            if (id > 0 && string.IsNullOrWhiteSpace(name))
            {
                SoundManager.Play(id);
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                SoundManager.Play(name);
            }
        }

        private void OnOpenChartRequestEvent(OpenChartRequest openChartRequest)
        {
            try
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
                        viewModel.Ready += (IModuleViewModel module) => viewModel.LoadSymbol(openChartRequest.Symbol,
                                                                      openChartRequest.Days,
                                                                      openChartRequest.Mins,
                                                                      openChartRequest.UnderPrice,
                                                                      (Oms.Enums.UnderPriceSource)openChartRequest.UnderPriceSource,
                                                                      (IvChartType)openChartRequest.ChartType);

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnOpenChartRequestEvent));
            }
        }

        private void OnOpenTicketRequestEvent(OpenTicketRequest openTicketRequest, Dominator dominator)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                if (!_ticketIdToTicketWindowMap.TryGetValue(openTicketRequest.Symbol, out Window window))
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
                                if (openTicketRequest.Left >= 0 || openTicketRequest.Top >= 0)
                                {
                                    window = new ComplexOrderTicketView
                                    {
                                        Manual = true,
                                    };
                                }
                                else
                                {
                                    window = new ComplexOrderTicketView
                                    {
                                        Manual = false
                                    };
                                }
                                break;
                            case OrderTicketStyle.Combined:
                                if (openTicketRequest.Left >= 0 || openTicketRequest.Top >= 0)
                                {
                                    window = new CombinedOrderTicketView
                                    {
                                        Manual = true
                                    };
                                }
                                else
                                {
                                    window = new CombinedOrderTicketView
                                    {
                                        Manual = false
                                    };
                                }
                                break;
                        }

                        ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        _ticketIdToTicketWindowMap[openTicketRequest.Symbol] = window;

                        window.Closed += (s, e) =>
                        {
                            _ticketIdToTicketWindowMap.TryRemove(openTicketRequest.Symbol, out _);
                            window.Dispatcher.InvokeShutdown();
                        };
                        window.Loaded += (s, e) =>
                        {
                            if (openTicketRequest.Left > 0)
                            {
                                window.Left = openTicketRequest.Left;
                            }
                            if (openTicketRequest.Top > 0)
                            {
                                window.Top = openTicketRequest.Top;
                            }
                            object[] windowParameters = new object[]
                            {
                                window.Width,
                                window.Height,
                                window.Left,
                                window.Top
                            };
                            viewModel.ShowAutoHedge = openTicketRequest.Hedge;
                            _ = viewModel.LoadFromOpenTicketRequestAsync(openTicketRequest, windowParameters, dominator);
                        };
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
                else
                {
                    window.Dispatcher.Invoke(new Action(() =>
                    {
                        ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        if (openTicketRequest.Left >= 0)
                        {
                            window.Left = openTicketRequest.Left;
                        }
                        if (openTicketRequest.Top >= 0)
                        {
                            window.Top = openTicketRequest.Top;
                        }

                        viewModel.ShowAutoHedge = openTicketRequest.Hedge;

                        if (openTicketRequest.TicketType == TicketType.ThreeWay)
                        {
                            var windowParameters = new object[]
                            {
                                window.Width,
                                window.Height,
                                window.Left,
                                window.Top
                            };
                            _ = viewModel.ThreeWayFromRequestAsync(openTicketRequest, windowParameters, dominator);
                        }
                        window.Activate();
                    }));
                }
            }
        }

        private void OnCloseTicketRequestEvent(CloseTicketRequest closeTicketRequest)
        {
            try
            {
                if (!closeTicketRequest.IsBasket && _ticketIdToTicketWindowMap.TryGetValue(closeTicketRequest.Symbol, out Window window))
                {
                    window.Dispatcher.Invoke(new Action(() =>
                    {
                        if (window.DataContext is ComplexOrderTicketViewModel viewModel)
                        {
                            viewModel.EdgeProjector?.CloseAll();
                        }
                        window.Close();
                    }));
                }
                else if (closeTicketRequest.IsBasket && _basketIdToBasketMap.TryGetValue(closeTicketRequest.Id, out BasketTraderView view))
                {
                    view.Dispatcher.Invoke(new Action(() => view.Close()));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseAllTickets));
            }
        }

        private void OnOpenBasketRequestEvent(OpenBasketRequest openBasketRequest)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
            {
                if (!string.IsNullOrEmpty(openBasketRequest.Id) && _basketIdToBasketMap.TryGetValue(openBasketRequest.Id, out BasketTraderView window) && window != null)
                {
                    window.Dispatcher.BeginInvoke(() =>
                    {
                        BasketTraderViewModel viewModel = window.DataContext as BasketTraderViewModel;
                        switch (openBasketRequest.BasketMode)
                        {
                            case BasketMode.Block:
                                _ = viewModel.LoadBlockFromOpenBasketRequest(openBasketRequest);
                                break;
                            case BasketMode.Normal:
                                viewModel.LoadFromOpenBasketRequest(openBasketRequest);
                                break;
                            case BasketMode.PermBasket:
                                viewModel.LoadFromOpenPermBasketRequest(openBasketRequest);
                                break;
                        }
                    });
                }
                else
                {
                    if (ModuleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
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
                            switch (openBasketRequest.BasketMode)
                            {
                                case BasketMode.Block:
                                    viewModel.LoadBlockFromOpenBasketRequest(openBasketRequest);
                                    break;
                                case BasketMode.Normal:
                                    viewModel.LoadFromOpenBasketRequest(openBasketRequest);
                                    break;
                                case BasketMode.PermBasket:
                                    viewModel.LoadFromOpenPermBasketRequest(openBasketRequest);
                                    break;
                            }

                        }
                    }
                }
            }
        }

        public void LoadFromConfig(ConfigSave config)
        {
            string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
            moduleName = moduleName?.Replace("Layout", "");
            moduleName = moduleName?.Trim();
            string title = moduleName;
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
                            Dispatcher.Invoke(new Action(() =>
                            {
                                string moduleName = Regex.Replace(((Module)config.Module).ToString(), "(\\B[A-Z])", " $1");
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
                            Dispatcher.Invoke(new Action(() =>
                            {
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
                    ModuleFactory.CreateModule(Module.SpreadsGenerator, null, config.ConfigJson);
                    break;
                case Module.SpreadTemplate:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            SpreadTemplateView window = new()
                            {
                                Title = title,
                            };
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
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
                        OrderBookWindowView view = new()
                        {
                            Title = title,
                        };
                        OrderBookViewModel viewModel = view.OrderBookView.DataContext as OrderBookViewModel;
                        view.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

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
                        view.Loaded += async (s, e) => await view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.CustomOrderBook:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        EmptyOrderBookView view = new()
                        {
                            Title = title,
                        };
                        EmptyOrderBookViewModel viewModel = view.DataContext as EmptyOrderBookViewModel;
                        view.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.BasketTrader:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                    {
                        if (ModuleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
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
                                viewModel.LoadConfigFromJsonAsync(config.ConfigJson);
                            }
                        }
                    }

                    break;
                case Module.Trades:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            TradesView window = new()
                            {
                                Title = title,
                            };
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
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
                                    window = new ComplexOrderTicketView
                                    {
                                        Title = title,
                                    };
                                    window.Loaded += (s, e) => _ = ((ComplexOrderTicketView)window).LoadConfigFromJsonAsync(config.ConfigJson);
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView
                                    {
                                        Title = title,
                                    };
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
                                    window = new ComplexOrderTicketView
                                    {
                                        Title = title,
                                    };
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView
                                    {
                                        Title = title,
                                    };
                                    window.Loaded += (s, e) => _ = ((CombinedOrderTicketView)window).LoadConfigFromJsonAsync(config.ConfigJson);
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
                        EmptyOrderBookView view = new()
                        {
                            Title = title,
                        };
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.BasketTraderLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                    {
                        if (ModuleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
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
                        if (ModuleFactory.CreateModule(Module.LockTrader) is LockTraderView { ViewModel: LockTraderViewModel viewModel } view)
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

                            TradesView window = new()
                            {
                                Title = title,
                            };
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
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

                            SpreadTemplateView window = new()
                            {
                                Title = title,
                            };
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
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
                        PortfolioView view = new()
                        {
                            Title = title,
                        };
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.PositionAnalyzer:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PositionAnalyzerView view = new()
                        {
                            Title = title,
                        };
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.DeltaHedgingLayout:
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DeltaHedgingView view = new()
                        {
                            Title = title,
                        };
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.HedgeHouseLayout:
                    _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
                    {
                        HedgeHouseView view = new()
                        {
                            Title = title,
                        };
                        view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                        view.Show();
                    }));
                    break;
                case Module.HeatmapLayout:
                    newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));
                        SpreadHeatmapView window = new()
                        {
                            Title = title,
                        };

                        SpreadHeatmapViewModel viewModel = (SpreadHeatmapViewModel)window.DataContext;
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
                            DominatorsManagerView view = new()
                            {
                                Title = title,
                            };
                            view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                            view.Show();
                        }));
                    }
                    break;
                case Module.BasketManagerLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketManagerLayout))
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BasketManagerView view = new()
                            {
                                Title = title,
                            };
                            view.Loaded += (s, e) => _ = view.LoadConfigFromJsonAsync(config.ConfigJson);

                            view.Show();
                        }));
                    }
                    break;
                case Module.DashboardLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Dashboard))
                    {
                        if (ModuleFactory.CreateModule(Module.Dashboard) is { ViewModel: ModuleViewModelBase viewModel } view)
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
            }
        }

        public async void LoadConfigLocal(ConfigSave configSave)
        {
            if ((Module)configSave.Module == Module.Workspace)
            {
                OmsCore.Config.WorkspaceTitle = configSave.Title;
                StartupWindowViewModel.RestartProgram("Save Workspace And Restart");
                return;
            }

            configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
            string moduleName = ConfigBrowserViewModel.GetModuleName(((Module)configSave.Module).ToString());
            string title = string.IsNullOrWhiteSpace(configSave.Title) ? moduleName : configSave.Title + " - " + moduleName;
            Thread newWindowThread;
            switch ((Module)configSave.Module)
            {
                case Module.SpreadsGenerator:
                case Module.SpreadsGeneratorLayout:
                    ModuleFactory.CreateModule(Module.SpreadsGenerator, null, configSave);
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
                            window.Loaded += (s, e) => window.RestoreFromConfigSave(configSave);
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
                            viewModel.ConfigSave = configSave;
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
                case Module.OrderBook:
                    _ = _transactionConsumerModel.Dispatcher.BeginInvoke(() =>
                    {
                        OrderBookWindowView view = new()
                        {
                            Title = configSave.Title
                        };
                        view.Loaded += async (s, e) => await view.OrderBookView.RestoreFromConfigSave(configSave);
                        OrderBookViewModel viewModel = view.OrderBookView.DataContext as OrderBookViewModel;
                        viewModel.ConfigSave = configSave;

                        view.Show();
                    });
                    break;
                case Module.TradeFeed:
                    _ = _transactionConsumerModel.Dispatcher.BeginInvoke(() =>
                    {
                        TradeFeedView view = new()
                        {
                            Title = configSave.Title
                        };
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);
                        TradeFeedViewModel viewModel = view.DataContext as TradeFeedViewModel;
                        viewModel.ConfigSave = configSave;

                        view.Show();
                    });
                    break;
                case Module.ComplexOrderTicket:
                    break;
                case Module.CustomOrderBook:
                    _ = Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        EmptyOrderBookView view = new();
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);
                        EmptyOrderBookViewModel viewModel = view.DataContext as EmptyOrderBookViewModel;
                        viewModel.ConfigSave = configSave;

                        view.Show();
                    });
                    break;
                case Module.BasketTrader:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                    {
                        ModuleFactory.CreateModule(Module.BasketTrader, null, configSave);
                    }
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
                            window.Loaded += (s, e) => window.RestoreFromConfigSave(configSave);
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                            viewModel.ConfigSave = configSave;
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
                case Module.OrderBookLayout:
                    _ = _transactionConsumerModel.Dispatcher.BeginInvoke(() =>
                    {
                        OrderBookWindowView view = new();

                        view.Loaded += async (s, e) => await view.OrderBookView.RestoreFromConfigSave(configSave);

                        view.Show();
                    });
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
                                    ComplexOrderTicketView cot = new();
                                    cot.Loaded += async (s, e) => await cot.RestoreFromConfigSaveAsync(configSave);
                                    window = cot;
                                    break;
                                case OrderTicketStyle.Combined:
                                    CombinedOrderTicketView cmt = new();
                                    cmt.Loaded += async (s, e) => await cmt.RestoreFromConfigSaveAsync(configSave);
                                    window = cmt;
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

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.OrderTicketLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            Window window = null;

                            OrderTicketView cot = new();
                            cot.Loaded += async (s, e) => await cot.RestoreFromConfigSaveAsync(configSave);
                            window = cot;

                            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
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
                                    ComplexOrderTicketView cot = new();
                                    cot.Loaded += (s, e) => _ = cot.RestoreFromConfigSaveAsync(configSave);
                                    window = cot;
                                    break;
                                case OrderTicketStyle.Combined:
                                    CombinedOrderTicketView cmt = new();
                                    cmt.Loaded += (s, e) => _ = cmt.RestoreFromConfigSaveAsync(configSave);
                                    window = cmt;
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

                            window.Show();

                            Dispatcher.Run();
                        });
                        newWindowThread.SetApartmentState(ApartmentState.STA);
                        newWindowThread.Start();
                    }
                    break;
                case Module.CustomOrderBookLayout:
                    _ = Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        EmptyOrderBookView view = new();
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);
                        view.Show();
                    });
                    break;
                case Module.BasketTraderLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                    {
                        ModuleFactory.CreateModule(Module.BasketTrader, null, configSave);
                    }
                    break;
                case Module.LockTraderLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.LockTrader))
                    {
                        ModuleFactory.CreateModule(Module.LockTrader, null, configSave);
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
                            window.Loaded += (s, e) => window.RestoreFromConfigSave(configSave);
                            TradesViewModel viewModel = (TradesViewModel)window.DataContext;
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
                case Module.SpreadTemplateLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.SpreadTemplate))
                    {
                        newWindowThread = new Thread(() =>
                        {
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(
                                    Dispatcher.CurrentDispatcher));

                            SpreadTemplateView window = new();
                            window.Loaded += (s, e) => window.RestoreFromConfigSave(configSave);
                            SpreadTemplateViewModel viewModel = (SpreadTemplateViewModel)window.DataContext;
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
                case Module.PortfolioLayout:
                    _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PortfolioView view = new();
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);

                        view.Show();
                    }));
                    break;
                case Module.DeltaHedgingLayout:
                    _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DeltaHedgingView view = new();
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);

                        view.Show();
                    }));
                    break;
                case Module.HedgeHouseLayout:
                    _dispatcherStore.GetDispatcherForModule(Module.Portfolio)?.BeginInvoke(new Action(() =>
                    {
                        HedgeHouseView view = new();
                        view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);

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
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => window.RestoreFromConfigSave(configSave);

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                    break;
                case Module.DominatorsManagerLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.DominatorsManager))
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            DominatorsManagerView view = new();
                            view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);

                            view.Show();
                        }));
                    }
                    break;
                case Module.BasketManagerLayout:
                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketManager))
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BasketManagerView view = new();
                            view.Loaded += (s, e) => view.RestoreFromConfigSave(configSave);

                            view.Show();
                        }));
                    }
                    break;
                case Module.Dashboard:
                case Module.DashboardLayout:
                    StartModuleCommand(Module.Dashboard);
                    break;
                case Module.EodRisk:
                    StartModuleCommand(Module.EodRisk);
                    break;
            }
        }

        public void EditDynamicConfig(Module configModule, IDynamicConfigModel selectedModel)
        {
            switch (configModule)
            {
                case Module.CustomList when selectedModel is CustomListModel model:
                    CustomListEditorView view = new();
                    if (view.DataContext is CustomListEditorViewModel viewModel)
                    {
                        viewModel.Init(model);
                        view.Show();
                    }
                    break;
            }
        }

        public IDynamicConfigModel GetDynamicConfig(Module configModule, string configJson = null)
        {
            switch (configModule)
            {
                case Module.CustomList:
                    CustomListModel model = null;
                    if (!string.IsNullOrWhiteSpace(configJson))
                    {
                        model = JsonConvert.DeserializeObject<CustomListModel>(configJson);
                    }

                    model ??= new CustomListModel
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };

                    return model;
                default:
                    return null;
            }
        }

        public void LoadDynamicConfig(Module configModule, IDynamicConfigModel currentModel)
        {
        }

        public int GetCurrentConfigId(Module configModule)
        {
            return 0;
        }
    }
}
