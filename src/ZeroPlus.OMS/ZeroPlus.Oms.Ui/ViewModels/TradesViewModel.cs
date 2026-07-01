using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.Databento;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using MinimumTickStyle = ZeroPlus.Comms.Models.Data.MarketData.MinimumTickStyle;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TradesViewModel : ModuleViewModelBase, ITradesSubscriber, IOmsDataSubscriber
    {
        private const int BATCH_UPDATE = 10_000;

        private static readonly string MODULE_TITLE = "Trades";
        private readonly System.Timers.Timer _refreshTimer;
        private DelegateCommand<object> _filterInNewOrderBookCommand;
        private DelegateCommand<object> _searchInNewTradesModuleCommand;
        private DelegateCommand<OpraDatabaseTradeModel> _openInStockTiedTicketCommand;
        private DelegateCommand<object> _sendToDominatorCommand;
        private DelegateCommand<string> _openInBasketAndLoadCustomPermCommand;

        private readonly TransactionConsumerModel _transactionConsumerModel;
        private ConcurrentQueue<List<OpraDatabaseTradeModel>> _queuedTrades = new();
        private readonly ConcurrentDictionary<string, List<OpraDatabaseTradeModel>> _spreadIdToTradesMap = [];
        private readonly PortfolioManagerModel _portfolioManager;
        private readonly IModuleFactory _moduleFactory;
        public ObservableCollection<object> _SelectedItems;
        public string _Symbol;
        public LegTypes _LegTypes;
        public bool _Unique;
        public bool _StatProcessingEnabled;
        public bool _RealTime;
        public DateTime _StartTime;
        public DateTime _EndTime;
        public string _Settings1;
        public string _Settings2;
        public string _SingleSettings2;
        public bool _InProgress;
        public bool _UseManualTime;
        public string _SelectedTime;
        public bool _AutoRefresh;
        public bool _SubscribeToGreeks;
        public bool _SubscribeToLast;
        public int _RefreshInterval;
        public bool _MaxCountEnabled;
        public double _MinStrikeSpacing;
        public int _MaxCount;
        public FastObservableCollection<OpraDatabaseTradeModel> _Trades;
        public OpraDatabaseTradeModel _CurrentItem;
        public MbpTradeModel _CurrentDatabentoItem;
        public string _FilterString;
        public List<object> _TradeConditionCodes;
        public List<object> _SingleLegTradeConditionCodes;
        public List<object> _SelectedTradeConditionCodes;
        public List<object> _SelectedSingleTradeConditionCodes;
        public bool _NotificationEnabled;
        public double _NotificationEdge;
        public double _NotificationTimeSpan;
        public bool _NotificationSoundEnabled;
        public string _NotificationSound;
        public bool _AutoPermEnabled;
        public int _AutoPermCount;
        public double _AutoPermEdge;
        public double _AutoPermTargetEdge;
        public bool _EdgeFinderEnabled;
        public bool _EdgeFinderSeparateExchange;
        public double _EdgeFinderMinPriceRange;
        public double _EdgeFinderMaxPriceRange;
        public int _EdgeFinderMinTimeRange;
        public int _EdgeFinderMaxTimeRange;
        public double _EdgeFinderMinUnderMoveRange;
        public double _EdgeFinderMaxUnderMoveRange;
        private int _queuedCount;
        private readonly object _lock = new();
        private readonly HashSet<string> _blockedSpreadIds = new();
        private readonly System.Timers.Timer _filterRefreshTimer;

        public override Module Module { get; protected set; } = Module.Trades;

        public CancellationTokenSource CancellationTokenSource { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public int RequestId { get; set; }
        public DominatorsManagerModel DominatorsManagerModel { get; }
        public NotificationManager NotificationManager { get; }

        protected ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public IUiUpdateService UiUpdateService => GetService<IUiUpdateService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public IEnumerable<LegTypes> SourceLegTypes { get; } = ((LegTypes[])Enum.GetValues(typeof(LegTypes))).ToList();

        public Tuple<int, double, DateTime, bool> Key { get; set; }
        public List<Tuple<int, double, DateTime, bool>> Keys { get; set; }

        [Bindable]
        public partial ObservableCollection<object> SelectedDatabentoItems { get; set; }
        [Bindable]
        public partial ObservableCollection<object> SelectedItems { get; set; }
        public string Symbol
        {
            get => _Symbol;
            set => SetValue(ref _Symbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }
        [Bindable]
        public partial LegTypes LegTypes { get; set; }

        [Bindable]
        public partial bool LoadTickType { get; set; }

        [Bindable]
        public partial bool Unique { get; set; }
        [Bindable]
        public partial bool StatProcessingEnabled { get; set; }
        public bool RealTime
        {
            get => _RealTime;
            set
            {
                SetValue(ref _RealTime, value);
                RealTimeChanged();
            }
        }
        [Bindable]
        public partial bool UseDatabento { get; set; }
        [Bindable]
        public partial DateTime StartTime { get; set; }
        [Bindable]
        public partial DateTime EndTime { get; set; }
        [Bindable]
        public partial string Settings1 { get; set; }
        [Bindable]
        public partial string Settings2 { get; set; }
        [Bindable]
        public partial string SingleSettings2 { get; set; }
        [Bindable]
        public partial bool InProgress { get; set; }
        [Bindable]
        public partial bool UseManualTime { get; set; }
        [Bindable]
        public partial string SelectedTime { get; set; }
        [Bindable]
        public partial bool AutoRefresh { get; set; }
        public bool SubscribeToGreeks
        {
            get => _SubscribeToGreeks;
            set
            {
                SetValue(ref _SubscribeToGreeks, value);
                SubscribeToGreeksChanged();
            }
        }
        public bool SubscribeToLast
        {
            get => _SubscribeToLast;
            set
            {
                SetValue(ref _SubscribeToLast, value);
                SubscribeToLastChanged();
            }
        }
        [Bindable]
        public partial int RefreshInterval { get; set; }
        [Bindable]
        public partial bool MaxCountEnabled { get; set; }
        [Bindable]
        public partial double MinStrikeSpacing { get; set; }
        [Bindable]
        public partial int MaxCount { get; set; }


        [Bindable(Default = 60)]
        public partial int DeltaAdjEdgeIntervalSeconds { get; set; }
        [Bindable]
        public partial bool MatchIoiTrades { get; set; }

        [Bindable]
        public partial FastObservableCollection<MbpTradeModel> DatabentoTrades { get; set; }
        [Bindable]
        public partial FastObservableCollection<OpraDatabaseTradeModel> Trades { get; set; }
        [Bindable]
        public partial OpraDatabaseTradeModel CurrentItem { get; set; }
        [Bindable]
        public partial MbpTradeModel CurrentDatabentoItem { get; set; }
        [Bindable]
        public partial string FilterString { get; set; }

        partial void OnFilterStringChanged(string value) => FilterChanged();
        [Bindable]
        public partial ObservableCollection<FilterModel> Filters { get; set; }
        [Bindable]
        public partial FilterModel SelectedFilter { get; set; }
        [Bindable]
        public partial bool AutoBlockEnabled { get; set; }
        [Bindable]
        public partial bool AutoSubmit { get; set; }
        [Bindable]
        public partial int MaxBlock { get; set; }
        [Bindable]
        public partial List<object> TradeConditionCodes { get; set; }
        [Bindable]
        public partial List<object> SingleLegTradeConditionCodes { get; set; }
        public List<object> SelectedTradeConditionCodes
        {
            get => _SelectedTradeConditionCodes;
            set => SetValue(ref _SelectedTradeConditionCodes, value.Where(x => (char)x != '*').ToList());
        }
        public List<object> SelectedSingleTradeConditionCodes
        {
            get => _SelectedSingleTradeConditionCodes;
            set => SetValue(ref _SelectedSingleTradeConditionCodes, value.Where(x => (char)x != '*').ToList());
        }
        [Bindable]
        public partial bool NotificationEnabled { get; set; }
        [Bindable]
        public partial double NotificationEdge { get; set; }
        [Bindable]
        public partial double NotificationTimeSpan { get; set; }
        [Bindable]
        public partial bool NotificationSoundEnabled { get; set; }
        [Bindable]
        public partial string NotificationSound { get; set; }
        [Bindable]
        public partial bool AutoPermEnabled { get; set; }
        [Bindable]
        public partial int AutoPermCount { get; set; }
        [Bindable]
        public partial double AutoPermEdge { get; set; }
        [Bindable]
        public partial double AutoPermTargetEdge { get; set; }
        [Bindable]
        public partial bool EdgeFinderEnabled { get; set; }
        [Bindable]
        public partial bool EdgeFinderSeparateExchange { get; set; }
        [Bindable]
        public partial double EdgeFinderMinPriceRange { get; set; }
        [Bindable]
        public partial double EdgeFinderMaxPriceRange { get; set; }
        [Bindable]
        public partial int EdgeFinderMinTimeRange { get; set; }
        [Bindable]
        public partial int EdgeFinderMaxTimeRange { get; set; }
        [Bindable]
        public partial double EdgeFinderMinUnderMoveRange { get; set; }
        [Bindable]
        public partial double EdgeFinderMaxUnderMoveRange { get; set; }

        public TradesViewModel(ConfigBrowserViewModel configBrowserViewModel,
                               OmsCore omsCore,
                               TransactionConsumerModel transactionConsumerModel,
                               PortfolioManagerModel portfolioManagerModel,
                               DominatorsManagerModel dominatorsManagerModel,
                               NotificationManager notificationManager,
                               IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
        {
            DominatorsManagerModel = dominatorsManagerModel;
            _portfolioManager = portfolioManagerModel;
            _moduleFactory = moduleFactory;
            _transactionConsumerModel = transactionConsumerModel;
            NotificationManager = notificationManager;
            ModuleTitle = MODULE_TITLE;
            Trades = new FastObservableCollection<OpraDatabaseTradeModel>();
            DatabentoTrades = new FastObservableCollection<MbpTradeModel>();
            SelectedItems = new ObservableCollection<object>();
            SelectedDatabentoItems = new ObservableCollection<object>();
            SelectedTradeConditionCodes = new List<object>();
            SelectedSingleTradeConditionCodes = new List<object>();
            Filters = new();
            LegTypes = LegTypes.MLeg;
            SelectedTime = "10 Min";
            _refreshTimer = new System.Timers.Timer
            {
                AutoReset = false
            };
            _filterRefreshTimer = new System.Timers.Timer(250)
            {
                AutoReset = false
            };
            _filterRefreshTimer.Elapsed += (_, _) => UiUpdateService?.ReapplyFilter(nameof(OpraDatabaseTradeModel.AdjustedPnl), nameof(OpraDatabaseTradeModel.Position));
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
            OmsCore.Config.PropertyChanged += ConfigOnPropertyChanged;
            _refreshTimer.Elapsed += RefreshTimer_Elapsed;
            SetTimeRange();
            LoadTradeConditionCodes();
        }

        private void ConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OmsCore.Config.TradeFilters))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    Filters.Clear();
                    foreach (var filter in OmsCore.Config.TradeFilters)
                    {
                        Filters.Add(filter);
                    }
                });
            }
        }

        [Command]
        public void ApplyFilterCommand()
        {
            try
            {
                if (SelectedFilter != null)
                {
                    FilterString = SelectedFilter.Filter;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ApplyFilterCommand));
            }
        }

        [Command]
        public void RemoveFilterCommand(FilterModel filter)
        {
            try
            {
                if (filter != null)
                {
                    if (filter.Equals(SelectedFilter))
                    {
                        SelectedFilter = null;
                    }

                    OmsCore.Config.RemoveTradesFilter(filter);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveFilterCommand));
            }
        }

        [Command]
        public void SaveFilterCommand()
        {
            try
            {
                SaveView view = new();
                if (view.DataContext is SaveViewModel viewModel)
                {
                    viewModel.ShowDefault = false;
                    viewModel.ShowGroup = false;
                    viewModel.ShowLocation = false;
                    viewModel.SetDispatcher(view.Dispatcher);

                    view.ShowDialog();

                    if (viewModel.Success)
                    {
                        var filter = new FilterModel()
                        {
                            Name = viewModel.Title,
                            Filter = FilterString
                        };

                        OmsCore.Config.AddTradesFilter(filter);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveFilterCommand));
            }
        }

        private void FilterChanged()
        {
            if (SelectedFilter != null && FilterString != SelectedFilter.Filter)
            {
                SelectedFilter = null;
            }
        }

        [Command]
        public void Clone()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
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
                        viewModel.Ready += (IModuleViewModel module) => _ = viewModel.LoadConfigFromJsonAsync(GetConfigJson(), true, false);

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = Module.Trades;

                viewModel.Config = GetConfigJson();

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void SetTimeRange()
        {
            int min;
            switch (SelectedTime)
            {
                case "1 Min":
                    min = 1;
                    break;
                case "2 Min":
                    min = 2;
                    break;
                case "3 Min":
                    min = 3;
                    break;
                case "5 Min":
                    min = 5;
                    break;
                case "10 Min":
                    min = 10;
                    break;
                case "15 Min":
                    min = 15;
                    break;
                case "20 Min":
                    min = 20;
                    break;
                case "30 Min":
                    min = 30;
                    break;
                case "1 Hour":
                    min = 60;
                    break;
                case "Today":
                    StartTime = EasternTimeNow().Date + TimeSpan.FromHours(5);
                    EndTime = StartTime + TimeSpan.FromHours(13);
                    return;
                default:
                    return;
            }

            EndTime = EasternTimeNow();
            StartTime = EndTime - TimeSpan.FromMinutes(min);
        }

        [Command]
        public void Refresh()
        {
            InProgress = true;

            CancellationTokenSource?.Cancel();
            CancellationTokenSource = new CancellationTokenSource();
            CancellationToken = CancellationTokenSource.Token;
            Clear();

            if (!UseManualTime)
            {
                SetTimeRange();
            }

            if (!UseDatabento)
            {
                Task.Run(RequestTrades, CancellationToken);
            }
            else
            {
                Task.Run(RequestDatabentoTrades, CancellationToken);
            }
        }

        [Command]
        public void CancelRefresh()
        {
            InProgress = false;
            CancellationTokenSource?.Cancel();
            Task.Run(() => StopTrades());
        }

        [Command]
        public async Task SaveTradesCommand()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "json";
                SaveFileDialogService.DefaultFileName = $"{Symbol} Trades - {DateTime.Now:MM-dd-yyyy hh.mm}".Trim();
                SaveFileDialogService.Filter = "Json|*.json";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    string filePath = SaveFileDialogService.GetFullFileName();
                    await Task.Run(() => SaveTradesToFile(filePath));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveTradesCommand));
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
            }
        }

        private void SaveTradesToFile(string filePath)
        {
            string jsonString = JsonConvert.SerializeObject(Trades.ToList());
            File.WriteAllText(filePath, jsonString);
        }

        [Command]
        public async Task LoadTradesCommand()
        {
            try
            {
                OpenFileDialogService.Filter = "Json|*.json";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    string filePath = file.GetFullName();
                    await LoadFromFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadTradesCommand));
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
            }
        }

        private async Task LoadFromFileAsync(string filePath)
        {
            bool blocked = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    Trades.Clear();
                    InProgress = true;
                    List<OpraDatabaseTradeModel> orderModels = null;

                    await Task.Run(() =>
                    {
                        string fileContent = File.ReadAllText(filePath);
                        orderModels = JsonConvert.DeserializeObject<List<OpraDatabaseTradeModel>>(fileContent);
                    });

                    if (orderModels != null)
                    {
                        if (orderModels.Count > BATCH_UPDATE)
                        {
                            UiUpdateService.BeginUpdate();
                            blocked = true;
                        }
                        Trades.AddRange(orderModels);
                    }
                }
            }
            finally
            {
                if (blocked)
                {
                    UiUpdateService.EndUpdate();
                }

                InProgress = false;
            }
        }

        private void OpenItemInStockTiedTicket(OpraDatabaseTradeModel trade)
        {
            if (trade is null)
            {
                return;
            }

            LoadTicket(trade, true);
        }

        [Command]
        public void OpenInComplexOrderTicket(object parameter)
        {
            try
            {
                if (parameter is OpraDatabaseTradeModel trade)
                {
                    LoadTicket(trade);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        private static void LoadTicket(OpraDatabaseTradeModel trade, bool withStockLeg = false)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));
                Window window = null;

                if (trade.LegCount > 1 && OmsCore.Config.UseOrderTicketForSingleLegOrders)
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
                    _ = viewModel.LoadFromTradeAsync(trade, withStockLeg);

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
                    if (parameter is OpraDatabaseTradeModel trade)
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
                                viewModel.LoadFromTradeAsync(trade);

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
            }
        }

        [Command]
        public void BlockInBasketTrader(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is OpraDatabaseTradeModel trade)
                {
                    StartBlockingBasket(trade);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
            }
        }

        private void StartBlockingBasket(OpraDatabaseTradeModel trade, bool autoSubmit = false)
        {
            if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
            {
                if (viewModel.IsReady)
                {
                    Task.Run(() => OnReady(viewModel), CancellationToken);
                }
                else
                {
                    viewModel.Ready += OnReady;
                }

                async void OnReady(IModuleViewModel _)
                {
                    viewModel.Ready -= OnReady;
                    await viewModel.LoadBlockFromTradeAsync(trade);
                    if (autoSubmit)
                    {
                        viewModel.Dispatcher.BeginInvoke(new Action(() => viewModel.SubmitAllNoCheck()));
                    }
                }
            }
        }

        [Command]
        public void RowDoubleClick(RowClickArgs args)
        {
            if (args == null || args.Item == null)
            {
                return;
            }
            if (args.Item is OpraDatabaseTradeModel trade)
            {
                OpenInComplexOrderTicket(trade);
            }
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
        public async void OpenInBasket()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    List<OpraDatabaseTradeModel> trades = GetVisibleTrades();
                    MessageResult result = MessageResult.No;
                    await Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        result = MessageBoxService.Show($"Would you like to load {trades.Count} spreads in basket?",
                                                        "Spreads Generator",
                                                        MessageButton.YesNo,
                                                        MessageIcon.Question,
                                                        MessageResult.No);
                    }));
                    if (result == MessageResult.Yes)
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
                                viewModel.LoadFromSpreadIdsAsync(trades.Select(x => Tuple.Create(x.Symbol, double.NaN)).ToList());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasket));
                ShowMessageBox(ex.Message);
            }
        }

        [Command]
        public void SearchTimeAndSalesOnLegsCommand(OpraDatabaseTradeModel model)
        {
            try
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

                    window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                    viewModel.Ready += _ =>
                    {
                        string symbols = model.Symbol;
                        SymbolCodec codec = new SymbolCodec(model.Symbol);
                        if (codec.LegCount > 1)
                        {
                            for (int i = 0; i < codec.LegCount; i++)
                            {
                                Instrument leg = codec.GetLeg(i);
                                symbols += "," + leg.symbol.Replace("+", "").Replace("-", "");
                            }
                        }
                        viewModel.Symbol = symbols;
                        viewModel.UseManualTime = true;
                        viewModel.StartTime = model.TradeTime - TimeSpan.FromMinutes(1);
                        viewModel.EndTime = model.TradeTime + TimeSpan.FromMinutes(1);
                        viewModel.LegTypes = LegTypes.All;

                        viewModel.Refresh();
                    };
                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchTimeAndSalesOnLegsCommand));
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

        public ICommand FilterInNewTradesModuleCommand
        {
            get
            {
                _searchInNewTradesModuleCommand ??= new DelegateCommand<object>(SearchInNewTradesModule);

                return _searchInNewTradesModuleCommand;
            }
        }

        public ICommand OpenInStockTiedTicketCommand
        {
            get
            {
                _openInStockTiedTicketCommand ??= new DelegateCommand<OpraDatabaseTradeModel>(OpenItemInStockTiedTicket);
                return _openInStockTiedTicketCommand;
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

        public ICommand OpenInBasketAndLoadCustomPermCommand
        {
            get
            {
                _openInBasketAndLoadCustomPermCommand ??= new DelegateCommand<string>(OpenInBasketAndLoadCustomPerm);

                return _openInBasketAndLoadCustomPermCommand;
            }
        }

        private void OpenInBasketAndLoadCustomPerm(string title)
        {
            try
            {
                if (!UseDatabento)
                {
                    List<OpraDatabaseTradeModel> trades = new();

                    foreach (object model in SelectedItems)
                    {
                        if (model is OpraDatabaseTradeModel trade)
                        {
                            trades.Add(trade);
                        }
                    }

                    if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                    {
                        if (trades.Count > 0)
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
                                    viewModel.LoadFromTradesAsync(trades, title);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketAndLoadCustomPerm));
            }
        }

        private void FilterInNewOrderBook(object parameter)
        {
            try
            {
                if (parameter is string filterString)
                {
                    _transactionConsumerModel.Dispatcher.BeginInvoke(new Action(() =>
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

        private void SendToDominator(object parameter)
        {
            try
            {
                if (parameter is Tuple<OpraDatabaseTradeModel, DominatorModel> tradeDomPair)
                {
                    OpraDatabaseTradeModel tradeUiModel = tradeDomPair.Item1;
                    DominatorModel dominator = tradeDomPair.Item2;

                    if (tradeUiModel != null && dominator != null)
                    {
                        TradeForDom trade = new()
                        {
                            UnderSymbol = tradeUiModel.UnderSymbol,
                            Exchange = tradeUiModel.Exchange,
                            SpreadType = tradeUiModel.SpreadType,
                            Quantity = tradeUiModel.Quantity,
                            Symbol = tradeUiModel.Symbol,
                            Bid = tradeUiModel.Bid,
                            Ask = tradeUiModel.Ask,
                            Price = tradeUiModel.Price,
                            UnderPrice = tradeUiModel.UnderPrice,
                            MidMarket = tradeUiModel.MidMarket,
                            TradeDelta = tradeUiModel.TradeDelta,
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
                        viewModel.Ready += (IModuleViewModel module) =>
                        {
                            TradesModuleConfig config = new()
                            {
                                Symbol = searchTerm,
                                MLeg = LegTypes == LegTypes.MLeg,
                                Unique = Unique,
                                StatProcessingEnabled = StatProcessingEnabled,
                                RealTime = RealTime,
                                SubscribeToGreeks = SubscribeToGreeks,
                                SubscribeToLast = SubscribeToLast,
                                StartTime = StartTime,
                                EndTime = EndTime,
                                Settings1 = Settings1,
                                Settings2 = Settings2,
                                SingleSettings2 = SingleSettings2,
                                UseManualTime = UseManualTime,
                                SelectedTime = SelectedTime,
                                AutoRefresh = AutoRefresh,
                                RefreshInterval = RefreshInterval,
                                MaxCountEnabled = MaxCountEnabled,
                                MaxCount = MaxCount,
                                FilterString = FilterString,
                                SelectedTradeConditionCodes = SelectedTradeConditionCodes.Select(x => (char)x).ToList(),
                                SelectedSingleTradeConditionCodes = SelectedSingleTradeConditionCodes.Select(x => (char)x).ToList(),
                                NotificationEnabled = NotificationEnabled,
                                NotificationEdge = NotificationEdge,
                                NotificationTimeSpan = NotificationTimeSpan,
                                NotificationSoundEnabled = NotificationSoundEnabled,
                                NotificationSound = NotificationSound,
                                AutoPermEnabled = AutoPermEnabled,
                                AutoPermCount = AutoPermCount,
                                AutoPermEdge = AutoPermEdge,
                                AutoBlockEnabled = AutoBlockEnabled,
                                MaxBlock = MaxBlock,
                                AutoPermTargetEdge = AutoPermTargetEdge,
                                EdgeFinderEnabled = EdgeFinderEnabled,
                                EdgeFinderSeparateExchange = EdgeFinderSeparateExchange,
                                EdgeFinderMinPriceRange = EdgeFinderMinPriceRange,
                                EdgeFinderMaxPriceRange = EdgeFinderMaxPriceRange,
                                EdgeFinderMinTimeRange = EdgeFinderMinTimeRange,
                                EdgeFinderMaxTimeRange = EdgeFinderMaxTimeRange,
                                EdgeFinderMinUnderMoveRange = EdgeFinderMinUnderMoveRange,
                                EdgeFinderMaxUnderMoveRange = EdgeFinderMaxUnderMoveRange,
                            };
                            string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                            viewModel.LoadConfigFromJsonAsync(configJson, false, false).ContinueWith(t => viewModel.Refresh(), CancellationToken);
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

        private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (AutoRefresh)
            {
                Refresh();
            }
        }

        private void RealTimeChanged()
        {
            CancelRefresh();
            if (RealTime)
            {
                Refresh();
            }
        }

        private void SubscribeToGreeksChanged()
        {
            if (SubscribeToGreeks)
            {
                foreach (var item in Trades)
                {
                    //item.SubscribeToGreeks(); //todo if req
                }
            }
            else
            {
                foreach (var item in Trades)
                {
                    //item.UnsubscribeGreeks();
                }
            }
        }

        private void SubscribeToLastChanged()
        {
            if (SubscribeToLast)
            {
                foreach (var item in Trades)
                {
                    //item.SubscribeToLast();
                }
            }
            else
            {
                foreach (var item in Trades)
                {
                    //item.UnsubscribeLast();
                }
            }
        }

        private void Clear()
        {
            try
            {
                Dispatcher?.BeginInvoke(() => Trades.Clear());
                _spreadIdToTradesMap.Clear();
                _portfolioManager.UnsubscribeAll(SubscriptionFieldType.FirmSpreadPosition, this);
                Reset();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clear));
            }
        }

        private void Reset()
        {
            _queuedTrades = new();
            _queuedCount = 0;
        }

        private static DateTime EasternTimeNow()
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);
        }

        private void RequestTrades()
        {
            try
            {
                Reset();
                GetSelectedTradeConditions();
                OmsCore.TradesClient.RequestTrades(Symbol, LegTypes is LegTypes.MLeg or LegTypes.All, RealTime, StartTime, EndTime, Settings1, Settings2, DeltaAdjEdgeIntervalSeconds, this, MatchIoiTrades);
                if (LegTypes == LegTypes.All)
                {
                    OmsCore.TradesClient.RequestTrades(Symbol, false, RealTime, StartTime, EndTime, Settings1, SingleSettings2, DeltaAdjEdgeIntervalSeconds, this, MatchIoiTrades);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestTrades));
                ShowMessageBox(ex.Message);
            }
        }

        private async void RequestDatabentoTrades()
        {
            try
            {
                Reset();
                List<MbpTradeModel> trades = await OmsCore.DatabentoClient.RequestTradesAsync(Symbol, StartTime, EndTime);
                Dispatcher?.BeginInvoke(() =>
                {
                    DatabentoTrades.Clear();
                    DatabentoTrades.AddRange(trades);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestTrades));
                ShowMessageBox(ex.Message);
            }
        }

        private void GetSelectedTradeConditions()
        {
            var conditionCodes = "";
            foreach (char code in TradeConditionCodes.Select(x => ((TradeConditionCodeModel)x).Code).Where(x => x != '*'))
            {
                if (SelectedTradeConditionCodes.Contains(code))
                {
                    conditionCodes += "=" + code + ",";
                }
            }
            if (conditionCodes.Length > 0 && conditionCodes[^1] == ',')
            {
                conditionCodes = conditionCodes[..^1];
            }

            Settings2 = conditionCodes;
        }

        private void GetSelectedSingleTradeConditions()
        {
            var conditionCodes = "";
            foreach (char code in SingleLegTradeConditionCodes.Select(x => ((TradeConditionCodeModel)x).Code).Where(x => x != '*'))
            {
                if (SelectedSingleTradeConditionCodes.Contains(code))
                {
                    conditionCodes += "=" + code + ",";
                }
            }
            if (conditionCodes.Length > 0 && conditionCodes[^1] == ',')
            {
                conditionCodes = conditionCodes[..^1];
            }

            SingleSettings2 = conditionCodes;
        }

        private void StopTrades()
        {
            try
            {
                Reset();
                if (RealTime)
                {
                    OmsCore.TradesClient.StopTrades(Symbol, LegTypes is LegTypes.MLeg or LegTypes.All, RealTime, StartTime, EndTime, Settings1, Settings2, DeltaAdjEdgeIntervalSeconds, this);
                    if (LegTypes == LegTypes.All)
                    {
                        OmsCore.TradesClient.StopTrades(Symbol, false, RealTime, StartTime, EndTime, Settings1, SingleSettings2, DeltaAdjEdgeIntervalSeconds, this);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopTrades));
                ShowMessageBox(ex.Message);
            }
        }

        public void QueueTrades(List<OpraDatabaseTradeModel> trades)
        {
            CancellationToken token = CancellationToken;
            _queuedTrades.Enqueue(trades);
            _queuedCount += trades.Count;
            if (RealTime)
            {
                ProcessQueuedTrades();
            }
        }

        public async void ProcessQueuedTrades()
        {
            try
            {
                CancellationToken token = CancellationToken;
                await Task.Run(() =>
                {
                    var trades = _queuedTrades.SelectMany(x => x).ToList();
                    Reset();

                    if (!token.IsCancellationRequested)
                    {
                        HandleTrades(trades, !RealTime);
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                Reset();
            }
        }

        public async void HandleTrades(List<OpraDatabaseTradeModel> trades, bool lastMessage)
        {
            try
            {
                Dictionary<string, int> spreadIdToCountMap = trades.Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
                                                                   .GroupBy(x => x.Symbol)
                                                                   .ToDictionary(g => g.Key, g => g.Count());

                Dictionary<string, double> underlyingSymbolToLastPriceMap = trades.GroupBy(x => x.UnderSymbol)
                    .ToDictionary(g => g.Key ?? "", g => GetUnderlyingMid(g.OrderByDescending(x => x.TradeTime).FirstOrDefault()));
                var symbolToTickStyleMap = await GetTickStyleMap(underlyingSymbolToLastPriceMap);

                if (Unique)
                {
                    trades = trades.GroupBy(x => x.Symbol)
                                           .Select(x => x.First())
                                           .ToList();
                }

                Dictionary<string, TradesLowHighEdgeModel> spreadIdToLowHighModelMap = new();


                foreach (var trade in trades)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!spreadIdToLowHighModelMap.TryGetValue(trade.Symbol, out TradesLowHighEdgeModel tradesLowHighEdgeModel))
                    {
                        tradesLowHighEdgeModel = new();
                        spreadIdToLowHighModelMap[trade.Symbol] = tradesLowHighEdgeModel;
                    }

                    EvaluateSpreadDescription(trade);
                    spreadIdToCountMap.TryGetValue(trade.Symbol, out var count);
                    trade.Count = count;
                    trade.TradesLowHighEdgeModel = tradesLowHighEdgeModel;
                    trade.AdjustedPnl = double.NaN;
                    if (underlyingSymbolToLastPriceMap.TryGetValue(trade.UnderSymbol, out double lastUnder) && !double.IsNaN(lastUnder))
                    {
                        trade.DeltaAdjustedPrice = Math.Round((lastUnder - (trade.UnderBid + trade.UnderAsk) / 2) * trade.TradeDelta + trade.Price, 2);
                    }

                    SetTickStyle(trade, symbolToTickStyleMap);
                    CheckForIndicators(trade);
                }

                AddStats(trades);
                trades = ApplyLimitFilters(trades);
                AddToTable(trades);
                SubscribeToPosition(trades);

                if (lastMessage)
                {
                    InProgress = false;

                    if (AutoRefresh && RefreshInterval > 0)
                    {
                        _refreshTimer.Interval = RefreshInterval;
                        _refreshTimer.Start();
                    }
                }


                CheckForAutoBlock(trades);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleTrades));
                ShowMessageBox(ex.Message);
            }
        }

        private void SubscribeToPosition(List<OpraDatabaseTradeModel> trades)
        {
            var grouped = trades.GroupBy(x => x.SpreadId);
            foreach (var group in grouped)
            {
                var spreadId = group.Key;
                if (!string.IsNullOrWhiteSpace(spreadId))
                {
                    if (!_spreadIdToTradesMap.TryGetValue(spreadId, out var map))
                    {
                        map = group.ToList();
                        _spreadIdToTradesMap[spreadId] = map;
                        _portfolioManager?.Subscribe(spreadId, SubscriptionFieldType.FirmSpreadPosition, this);
                    }
                    else
                    {
                        OpraDatabaseTradeModel sample = map.FirstOrDefault();
                        map.AddRange(group);
                        if (sample != null)
                        {
                            HandleUpdate(group, sample.AdjustedPnl, sample.Position);
                        }
                    }
                }
            }
        }

        private void HandleUpdate(IEnumerable<OpraDatabaseTradeModel> group, double adjustedPnl, int position)
        {
            foreach (var trade in group)
            {
                trade.AdjustedPnl = adjustedPnl;
                trade.Position = position;
            }
        }

        private void CheckForIndicators(OpraDatabaseTradeModel trade)
        {
            if (Keys is { Count: > 0 })
            {
                foreach (Tuple<int, double, DateTime, bool> key in Keys)
                {
                    if (key.Item1 == trade.Quantity &&
                        Math.Round(key.Item2, 2) == Math.Round(trade.Price, 2) &&
                        trade.TradeTime.Hour == key.Item3.Hour &&
                        trade.TradeTime.Minute == key.Item3.Minute &&
                        trade.TradeTime.Second == key.Item3.Second &&
                        trade.TradeTime.Millisecond == key.Item3.Millisecond)
                    {
                        trade.ShowIndicator = true;
                        trade.BuyIndicator = key.Item4;
                    }
                }
            }

            if (Key != null)
            {
                if (Key.Item1 == trade.Quantity &&
                    Math.Round(Key.Item2, 2) == Math.Round(trade.Price, 2) &&
                    trade.TradeTime.Hour == Key.Item3.Hour &&
                    trade.TradeTime.Minute == Key.Item3.Minute &&
                    trade.TradeTime.Second == Key.Item3.Second &&
                    trade.TradeTime.Millisecond == Key.Item3.Millisecond)
                {
                    trade.IsFirm = true;
                    trade.BuyIndicator = Key.Item4;
                }
            }
        }

        private void AddToTable(List<OpraDatabaseTradeModel> trades)
        {
            bool uiLocked = false;
            if (trades.Count > BATCH_UPDATE)
            {
                UiUpdateService?.BeginUpdate();
                uiLocked = true;
            }
            Dispatcher.BeginInvoke(() =>
            {
                Trades.AddRange(trades);

                if (uiLocked)
                {
                    UiUpdateService?.EndUpdate();
                }
            });
        }

        private void AddStats(List<OpraDatabaseTradeModel> trades)
        {
            if (StatProcessingEnabled)
            {
                Dictionary<string, Tuple<double, double>> priceRangeMap = new();
                foreach (var tradeModel in trades)
                {
                    if (!priceRangeMap.TryGetValue(tradeModel.SpreadId, out var priceRange))
                    {
                        var spreadTradeModels = trades.Where(x => x.SpreadId == tradeModel.SpreadId).ToList();
                        double min = spreadTradeModels.Min(x => x.Price);
                        double max = spreadTradeModels.Max(x => x.Price);
                        double adjMin = spreadTradeModels.Min(x => x.DeltaAdjustedPrice);
                        double adjMax = spreadTradeModels.Max(x => x.DeltaAdjustedPrice);
                        priceRange = Tuple.Create(Math.Abs(max - min), Math.Abs(adjMax - adjMin));
                        priceRangeMap[tradeModel.SpreadId] = priceRange;
                    }
                    tradeModel.PriceRange = priceRange.Item1;
                    tradeModel.AdjPriceRange = priceRange.Item2;
                }
            }
        }

        private List<OpraDatabaseTradeModel> ApplyLimitFilters(List<OpraDatabaseTradeModel> trades)
        {
            if (MaxCountEnabled && trades.Count >= MaxCount)
            {
                trades = trades.Take(MaxCount).ToList();
            }

            if (MinStrikeSpacing > 0)
            {
                trades = trades.Where(x => x.ExpSpacing != 0 || x.StrikeSpacing >= MinStrikeSpacing).ToList();
            }

            return trades;
        }

        private async Task<Dictionary<string, MinimumTickStyle>> GetTickStyleMap(Dictionary<string, double> underlyingSymbolToLastPriceMap)
        {
            Dictionary<string, MinimumTickStyle> symbolToTickStyleMap = new();

            if (LegTypes == LegTypes.Single && LoadTickType)
            {
                List<string> underlyings = underlyingSymbolToLastPriceMap.Keys.ToList();
                if (underlyings.Count > 0)
                {
                    foreach (var under in underlyings)
                    {
                        List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbols(under);
                        foreach (var symbol in symbols)
                        {
                            symbolToTickStyleMap[symbol.OptionSymbol] = symbol.TickType;
                        }
                    }
                }
            }

            return symbolToTickStyleMap;
        }

        private void SetTickStyle(OpraDatabaseTradeModel trade, Dictionary<string, MinimumTickStyle> symbolToTickStyleMap)
        {
            if (LoadTickType)
            {
                if (trade.LegCount <= 1)
                {
                    if (symbolToTickStyleMap.TryGetValue(trade.Symbol, out var style))
                    {
                        switch (style)
                        {
                            case MinimumTickStyle.None:
                                trade.TickStyle = "";
                                break;
                            case MinimumTickStyle.AllPenny:
                                trade.TickStyle = "Penny";
                                break;
                            case MinimumTickStyle.Pennies:
                                trade.TickStyle = trade.Price < 3 ? "Penny" : "Nickel";
                                break;
                            case MinimumTickStyle.Nickels:
                                trade.TickStyle = trade.Price < 3 ? "Nickel" : "Dime";
                                break;
                            case MinimumTickStyle.Dimes:
                                trade.TickStyle = "Dime";
                                break;
                        }
                    }
                }
                else
                {
                    trade.TickStyle = trade.UnderSymbol == "$SPX" ? "Nickel" : "Penny";
                }
            }
        }

        private void CheckForAutoBlock(List<OpraDatabaseTradeModel> trades)
        {
            if (AutoBlockEnabled)
            {
                List<string> newSpreadIds = trades.Select(x => x.Symbol).Distinct().ToList();
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    List<OpraDatabaseTradeModel> visibleTrades = GetVisibleTrades().Where(x => newSpreadIds.Contains(x.Symbol)).ToList();
                    for (int i = 0; i < visibleTrades.Count; i++)
                    {
                        if (CancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        OpraDatabaseTradeModel trade = visibleTrades[i];
                        if (i + 1 > MaxBlock)
                        {
                            break;
                        }
                        bool isNew;
                        lock (_lock)
                        {
                            isNew = _blockedSpreadIds.Add(trade.SpreadId);
                        }
                        if (isNew)
                        {
                            StartBlockingBasket(trade, AutoSubmit);
                        }
                    }
                }));
            }
        }

        public void EvaluateSpreadDescription(OpraDatabaseTradeModel trade)
        {
            OptionStrategy.TryIdentify(trade.Symbol, out string baseStrategy, out string spreadType, out var description);
            LoadIndicators(trade);
            trade.SpreadTypeOms = baseStrategy;
            trade.SpreadId = spreadType;
            trade.Description = description;
        }

        private void LoadIndicators(OpraDatabaseTradeModel trade)
        {
            var codec = new SymbolCodec(trade.Symbol);
            var legs = new List<Instrument>(codec.LegCount);
            for (int i = 0; i < codec.LegCount; i++)
            {
                var leg = codec.GetLeg(i);
                legs.Add(leg);
            }

            if (legs.Count == 0)
            {
                var security = OptionsHelper.GetOptionFromSymbol(trade.Symbol.Replace("+", "").Replace("-", ""));
                trade.ExpirationOne = security.Expiration;
                trade.DaysToExp = (security.Expiration.Date - DateTime.Today).TotalDays;
                trade.Strike = security.Strike;
            }
            else
            {
                List<DateTime> expirations = legs.Select(x => x.expiration).Distinct().OrderBy(x => x).ToList();
                trade.Strike = legs.Select(x => x.strike).OrderBy(x => x).FirstOrDefault();
                switch (expirations.Count)
                {
                    case > 2:
                        trade.ExpirationOne = expirations[0];
                        trade.ExpirationTwo = expirations[1];
                        trade.ExpirationThree = expirations[2];
                        trade.DaysToExp = (expirations[0].Date - DateTime.Today).TotalDays;
                        trade.ExpSpacing = Math.Min(Math.Abs((trade.ExpirationOne - trade.ExpirationTwo).TotalDays), Math.Abs((trade.ExpirationTwo - trade.ExpirationThree).TotalDays));
                        break;
                    case 2:
                        trade.ExpirationOne = expirations[0];
                        trade.ExpirationTwo = expirations[1];
                        trade.DaysToExp = (expirations[0].Date - DateTime.Today).TotalDays;
                        trade.ExpSpacing = Math.Abs((trade.ExpirationOne - trade.ExpirationTwo).TotalDays);
                        break;
                    case 1:
                        trade.DaysToExp = (expirations[0].Date - DateTime.Today).TotalDays;
                        trade.ExpirationOne = expirations[0];
                        trade.ExpSpacing = 0;
                        break;
                }

                List<double> strikes = legs.Select(x => x.strike).Distinct().OrderBy(x => x).ToList();
                switch (strikes.Count)
                {
                    case > 3:
                        trade.SpacingOne = strikes[1] - strikes[0];
                        trade.SpacingTwo = strikes[2] - strikes[1];
                        trade.SpacingThree = strikes[3] - strikes[2];
                        trade.StrikeSpacing = Math.Min(Math.Min(trade.SpacingOne, trade.SpacingTwo), trade.SpacingThree);
                        break;
                    case > 2:
                        trade.SpacingOne = strikes[1] - strikes[0];
                        trade.SpacingTwo = strikes[2] - strikes[1];
                        trade.StrikeSpacing = Math.Min(trade.SpacingOne, trade.SpacingTwo);
                        break;
                    case > 1:
                        trade.SpacingOne = strikes[1] - strikes[0];
                        trade.StrikeSpacing = trade.SpacingOne;
                        break;
                    default:
                        trade.SpacingOne = 0;
                        trade.StrikeSpacing = 0;
                        break;
                }
            }
        }
        private double GetUnderlyingMid(OpraDatabaseTradeModel trade)
        {
            try
            {
                if (trade == null)
                {
                    return double.NaN;
                }
                else
                {
                    return (trade.UnderBid + trade.UnderAsk) / 2;
                }
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private List<OpraDatabaseTradeModel> GetVisibleTrades()
        {
            return GetItemsByVisualOrderService.GetItemsByVisualOrder().Select(x => (OpraDatabaseTradeModel)x.Item2).ToList();
        }

        internal new void Dispose()
        {
            base.Dispose();
            _filterRefreshTimer.Stop();
            _filterRefreshTimer.Dispose();
            OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
            OmsCore.Config.PropertyChanged -= ConfigOnPropertyChanged;
            Task.Run(Clear);
        }

        internal void LoadViewModelConfig(string uid)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(TradesModuleConfig)}.json");
                string defaultConfigExportPath = Path.Combine(layoutDir, $"Default-{nameof(TradesModuleConfig)}.json");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = File.ReadAllText(configExportPath);
                    _ = LoadConfigFromJsonAsync(myFileStream);
                }
                else if (File.Exists(defaultConfigExportPath))
                {
                    string myFileStream = File.ReadAllText(defaultConfigExportPath);
                    _ = LoadConfigFromJsonAsync(myFileStream, reset: true);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadViewModelConfig));
                ShowMessageBox(ex.Message);
            }
        }

        internal async Task LoadConfigFromJsonAsync(string configJson, bool reset = false, bool readyNotify = true)
        {
            TradesModuleConfig config = await Task.Run(() => JsonConvert.DeserializeObject<TradesModuleConfig>(configJson));
            Symbol = config.Symbol;
            LegTypes = config.MLeg ? LegTypes.MLeg : LegTypes.Single;
            Unique = config.Unique;
            StatProcessingEnabled = config.StatProcessingEnabled;
            RealTime = config.RealTime;
            SubscribeToGreeks = config.SubscribeToGreeks;
            SubscribeToLast = config.SubscribeToLast;
            StartTime = config.StartTime;
            EndTime = config.EndTime;
            Settings1 = config.Settings1;
            Settings2 = config.Settings2;
            SingleSettings2 = config.SingleSettings2;
            UseManualTime = config.UseManualTime;
            SelectedTime = config.SelectedTime;
            AutoRefresh = config.AutoRefresh;
            RefreshInterval = config.RefreshInterval;
            MaxCountEnabled = config.MaxCountEnabled;
            MaxCount = config.MaxCount;
            FilterString = config.FilterString;
            SelectedTradeConditionCodes = config.SelectedTradeConditionCodes.Select(x => (object)x).ToList();
            SelectedSingleTradeConditionCodes = config.SelectedSingleTradeConditionCodes.Select(x => (object)x).ToList();
            NotificationEnabled = config.NotificationEnabled;
            NotificationEdge = config.NotificationEdge;
            NotificationTimeSpan = config.NotificationTimeSpan;
            NotificationSoundEnabled = config.NotificationSoundEnabled;
            NotificationSound = config.NotificationSound;
            AutoBlockEnabled = config.AutoBlockEnabled;
            MaxBlock = config.MaxBlock;
            AutoPermEnabled = config.AutoPermEnabled;
            AutoPermCount = config.AutoPermCount;
            AutoPermEdge = config.AutoPermEdge;
            AutoPermTargetEdge = config.AutoPermTargetEdge;
            EdgeFinderEnabled = config.EdgeFinderEnabled;
            EdgeFinderSeparateExchange = config.EdgeFinderSeparateExchange;
            EdgeFinderMinPriceRange = config.EdgeFinderMinPriceRange;
            EdgeFinderMaxPriceRange = config.EdgeFinderMaxPriceRange;
            EdgeFinderMinTimeRange = config.EdgeFinderMinTimeRange;
            EdgeFinderMaxTimeRange = config.EdgeFinderMaxTimeRange;
            EdgeFinderMinUnderMoveRange = config.EdgeFinderMinUnderMoveRange;
            EdgeFinderMaxUnderMoveRange = config.EdgeFinderMaxUnderMoveRange;
            LoadTickType = config.LoadTickType;
            MinStrikeSpacing = config.MinStrikeSpacing;

            if (readyNotify)
            {
                _ = InvokeReady();
            }
        }

        private void ShowMessageBox(string message)
        {
            try
            {
                Dispatcher?.BeginInvoke(new Action(() => MessageBoxService?.ShowMessage($"Something went wrong.\n{message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowMessageBox));
            }
        }

        public string GetConfigJson()
        {
            TradesModuleConfig config = new()
            {
                Symbol = Symbol,
                MLeg = LegTypes is LegTypes.MLeg or LegTypes.All,
                Unique = Unique,
                StatProcessingEnabled = StatProcessingEnabled,
                RealTime = RealTime,
                SubscribeToGreeks = SubscribeToGreeks,
                SubscribeToLast = SubscribeToLast,
                StartTime = StartTime,
                EndTime = EndTime,
                Settings1 = Settings1,
                Settings2 = Settings2,
                SingleSettings2 = SingleSettings2,
                UseManualTime = UseManualTime,
                SelectedTime = SelectedTime,
                AutoBlockEnabled = AutoBlockEnabled,
                MaxBlock = MaxBlock,
                AutoRefresh = AutoRefresh,
                RefreshInterval = RefreshInterval,
                MaxCountEnabled = MaxCountEnabled,
                MaxCount = MaxCount,
                FilterString = FilterString,
                SelectedTradeConditionCodes = SelectedTradeConditionCodes.Select(x => (char)x).ToList(),
                SelectedSingleTradeConditionCodes = SelectedSingleTradeConditionCodes.Select(x => (char)x).ToList(),
                NotificationEnabled = NotificationEnabled,
                NotificationEdge = NotificationEdge,
                NotificationTimeSpan = NotificationTimeSpan,
                NotificationSoundEnabled = NotificationSoundEnabled,
                NotificationSound = NotificationSound,
                AutoPermEnabled = AutoPermEnabled,
                AutoPermCount = AutoPermCount,
                AutoPermEdge = AutoPermEdge,
                AutoPermTargetEdge = AutoPermTargetEdge,
                EdgeFinderEnabled = EdgeFinderEnabled,
                EdgeFinderSeparateExchange = EdgeFinderSeparateExchange,
                EdgeFinderMinPriceRange = EdgeFinderMinPriceRange,
                EdgeFinderMaxPriceRange = EdgeFinderMaxPriceRange,
                EdgeFinderMinTimeRange = EdgeFinderMinTimeRange,
                EdgeFinderMaxTimeRange = EdgeFinderMaxTimeRange,
                EdgeFinderMinUnderMoveRange = EdgeFinderMinUnderMoveRange,
                EdgeFinderMaxUnderMoveRange = EdgeFinderMaxUnderMoveRange,
                LoadTickType = LoadTickType,
                MinStrikeSpacing = MinStrikeSpacing,
            };

            string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            return configJson;
        }

        private void LoadTradeConditionCodes()
        {
            TradeConditionCodes =
            [
                new TradeConditionCodeModel('*', " -- Single Leg -- "),
                new TradeConditionCodeModel('a', "a\tSLAN\tSingle Leg Auction Non ISO"),
                new TradeConditionCodeModel('b', "b\tSLAI\tSingle Leg Auction ISO"),
                new TradeConditionCodeModel('c', "c\tSLCN\tSingle Leg Cross Non ISO"),
                new TradeConditionCodeModel('d', "d\tSCLI\tSingle Leg Cross ISO"),
                new TradeConditionCodeModel('e', "e\tSLFT\tSingle Leg Floor Trade"),
                new TradeConditionCodeModel('I', "I\tAUTO\tAuto-electronic Exec"),
                new TradeConditionCodeModel('S', "S\tISOI\tSingle Leg Cross ISO"),

                new TradeConditionCodeModel('*', " -- Multi Leg -- "),
                new TradeConditionCodeModel('f', "f\tMLET\tMulti Leg auto-electronic trade"),
                new TradeConditionCodeModel('g', "g\tMLAT\tMulti Leg Auction"),
                new TradeConditionCodeModel('h', "h\tMLCT\tMulti Leg Cross"),
                new TradeConditionCodeModel('i', "i\tMLFT\tMulti Leg floor trade"),
                new TradeConditionCodeModel('k', "k\tTLAT\tStock Options Auction"),
                new TradeConditionCodeModel('m', "m\tMFSL\tMulti Leg floor trade against single leg(s)"),
                new TradeConditionCodeModel('n', "n\tTLET\tStock Options auto-electronic trade"),
                new TradeConditionCodeModel('o', "o\tTLCT\tStock Options Cross"),
                new TradeConditionCodeModel('p', "p\tTLFT\tStock Options floor trade"),
                new TradeConditionCodeModel('t', "t\tCBMO\tMulti Leg Floor Trade of Proprietary Products"),

                new TradeConditionCodeModel('*', " -- Multi Leg Against Single Leg(s) -- "),
                new TradeConditionCodeModel('j', "j\tMESL\tMulti Leg auto-electronic trade against single leg(s)"),
                new TradeConditionCodeModel('l', "l\tMASL\tMulti Leg Auction against single leg(s)"),
                new TradeConditionCodeModel('q', "q\tTESL\tStock Options auto-electronic trade against single leg(s)"),
                new TradeConditionCodeModel('r', "r\tTASL\tStock Options Auction against single leg(s)"),
                new TradeConditionCodeModel('s', "s\tTFSL\tStock Options floor trade against single leg(s)"),
                new TradeConditionCodeModel('u', "u\tMCTP\tMultilateral Compression Trade of Proprietary Products"),

                new TradeConditionCodeModel('*', " -- Others -- "),
                new TradeConditionCodeModel('A', "A\tCANC\tNow busted"),
                new TradeConditionCodeModel('B', "B\tOSEQ\tOut of sequence"),
                new TradeConditionCodeModel('C', "C\tCNCL\tTransaction is the last reported but now canceled"),
                new TradeConditionCodeModel('D', "D\tLATE\tTransaction is being reported late"),
                new TradeConditionCodeModel('E', "E\tCNCO\tOpen report, now busted"),
                new TradeConditionCodeModel('F', "F\tOPEN\tOpen but late report"),
                new TradeConditionCodeModel('G', "G\tCNOL\tOnly report but now busted"),
                new TradeConditionCodeModel('H', "H\tOPNL\tOpening report, late"),
                new TradeConditionCodeModel('J', "J\tREOP\tReopening report"),
                new TradeConditionCodeModel('v', "v\tEXHT\tExtended Hours Trade")
            ];

            SingleLegTradeConditionCodes = new List<object>
            {
                new TradeConditionCodeModel('*', " -- Single Leg -- "),
                new TradeConditionCodeModel('a', "a\tSLAN\tSingle Leg Auction Non ISO"),
                new TradeConditionCodeModel('b', "b\tSLAI\tSingle Leg Auction ISO"),
                new TradeConditionCodeModel('c', "c\tSLCN\tSingle Leg Cross Non ISO"),
                new TradeConditionCodeModel('d', "d\tSCLI\tSingle Leg Cross ISO"),
                new TradeConditionCodeModel('e', "e\tSLFT\tSingle Leg Floor Trade"),
                new TradeConditionCodeModel('I', "I\tAUTO\tAuto-electronic Exec"),
                new TradeConditionCodeModel('S', "S\tISOI\tSingle Leg Cross ISO"),
            };
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            string configJson = GetConfigJson();
            return configJson;
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            await LoadConfigFromJsonAsync(configJson);
        }

        public override void SaveViewModelConfig()
        {
            try
            {
                string configJson = GetConfigJson();
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(TradesModuleConfig)}.json");
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
                ShowMessageBox(ex.Message);
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
        {
            switch (key.Type)
            {
                case SubscriptionFieldType.FirmSpreadPosition:
                    if (value is IPosition positionUpdate)
                    {
                        if (_spreadIdToTradesMap.TryGetValue(positionUpdate.Name, out var models))
                        {
                            HandleUpdate(models, positionUpdate.AdjustedPnl, positionUpdate.NetQty);
                            _filterRefreshTimer.Stop();
                            _filterRefreshTimer.Start();
                        }
                    }
                    break;
            }
        }

    }
}
