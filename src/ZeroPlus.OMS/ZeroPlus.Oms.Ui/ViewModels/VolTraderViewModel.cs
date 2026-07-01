using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class VolTraderViewModel : CustomizableTableViewModelBase
    {
        private readonly IModuleFactory _moduleFactory;
        private static readonly string MODULE_TITLE = "Vol Trader";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static int _instanceCounter;
        private readonly object _lock;
        private readonly HashSet<BasketTraderViewModel> _baskets;


        private bool? _CloseOrderMode;

        public IEnumerable<string> TypesList { get; } = ((Types[])Enum.GetValues(typeof(Types))).Select(x => x.ToString());
        public ClosingTypes[] ClosingTypes { get; } = (ClosingTypes[])Enum.GetValues(typeof(ClosingTypes));
        public LoopPricingMode[] LoopPricingModes { get; } = (LoopPricingMode[])Enum.GetValues(typeof(LoopPricingMode));
        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Dispatcher Dispatcher { get; set; }

        public string NewType { get; set; }
        public string NewUnderlying { get; set; }
        public double NewMinStrike { get; set; }
        public double NewMaxStrike { get; set; }
        public ExpirationInfoModel ExpirationInfo { get; set; }
        public ObservableCollection<ExpirationInfoModel> ExpirationsList { get; set; }

        [Bindable]
        public partial bool RiskCheckEnabled { get; set; }
        [Bindable]
        public partial bool ActiveUncheckEnabled { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial string Name { get; set; }
        [Bindable]
        public partial int BasketCount { get; set; }
        [Bindable]
        public partial bool ShowSubmitWithDelaySettings { get; set; }
        [Bindable]
        public partial bool ShowRouteSettings { get; set; }
        [Bindable]
        public partial bool ShowAdvancedRouteSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoCloseSettings { get; set; }
        [Bindable]
        public partial bool ShowAutoCancelSettings { get; set; }

        //Submit with delay 
        [Bindable]
        public partial bool SubmitWithDelayEnabled { get; set; }
        [Bindable]
        public partial int SubmitWithDelayInterval { get; set; }
        [Bindable]
        public partial int SubmitWithDelayIntervalEnd { get; set; }
        [Bindable]
        public partial bool OpenTicketForFills { get; set; }
        [Bindable]
        public partial bool OpenTicketForFailedClose { get; set; }
        [Bindable]
        public partial int CancelOnAmountOfFillsCount { get; set; }
        [Bindable]
        public partial bool Randomize { get; set; }
        [Bindable]
        public partial bool Resume { get; set; }
        [Bindable]
        public partial bool DisablePriceRounding { get; set; }
        [Bindable]
        public partial bool AlsoOpenContraTicketEnabled { get; set; }
        [Bindable]
        public partial bool MaxRestingOrdersEnabled { get; set; }
        [Bindable]
        public partial int MaxRestingOrdersCount { get; set; }
        [Bindable]
        public partial bool StartProcessingFromSelectedRow { get; set; }
        [Bindable]
        public partial bool ContraEnabled { get; set; }
        //Submit with delay end

        //Route
        [Bindable]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [Bindable]
        public partial string LooperOpenRoute { get; set; }
        [Bindable]
        public partial string LooperCloseRoute { get; set; }
        [Bindable]
        public partial string LooperOpenRouteSingleLeg { get; set; }
        [Bindable]
        public partial string LooperCloseRouteSingleLeg { get; set; }
        [Bindable]
        public partial bool UseSingleLegSeparateLooperRoutes { get; set; }
        [Bindable]
        public partial string StockTiedOrderRoute { get; set; }

        //Route end

        //Submit with delay
        public bool? CloseOrderMode
        {
            get => _CloseOrderMode;
            set
            {
                SetValue(ref _CloseOrderMode, value);
                if (value == null)
                {
                    GoFishAutoCloseEnabled = false;
                    LoopingEnabled = false;
                }
                else if (value == true)
                {
                    GoFishAutoCloseEnabled = true;
                    LoopingEnabled = true;
                }
                else
                {
                    GoFishAutoCloseEnabled = true;
                    LoopingEnabled = false;
                }

                VolTraderAutomationUpdatedCommand();
            }
        }
        [Bindable]
        public partial bool LoopingEnabled { get; set; }
        [Bindable]
        public partial bool GoFishAutoCloseEnabled { get; set; }
        [Bindable]
        public partial bool UseGlobalCloseEdge { get; set; }
        [Bindable]
        public partial double ContraFishEdge { get; set; }
        [Bindable]
        public partial double CloseEdgeMinValue { get; set; }
        [Bindable]
        public partial double LoopMaxLoss { get; set; }
        [Bindable]
        public partial double LoopMinEdgePercentage { get; set; }
        [Bindable]
        public partial bool LoopMinEdgeUsePercentage { get; set; }
        [Bindable]
        public partial double LoopMinEdge { get; set; }
        [Bindable]
        public partial int MaxLoopCount { get; set; }
        [Bindable]
        public partial double AutomationRequiredPartialFillPercentage { get; set; }
        [Bindable]
        public partial LoopPricingMode LoopPricingMode { get; set; }
        [Bindable]
        public partial int ContraFishInterval { get; set; }
        [Bindable]
        public partial int ContraFishIntervalMax { get; set; }
        [Bindable]
        public partial ClosingTypes ClosingMode { get; set; }
        [Bindable]
        public partial int LoopInterval { get; set; }
        [Bindable]
        public partial int LoopIntervalMax { get; set; }
        [Bindable]
        public partial int AttemptResubmit { get; set; }
        [Bindable]
        public partial bool LoopAutoSizeup { get; set; }
        [Bindable]
        public partial LoopSizeupType LoopSizeupType { get; set; }
        [Bindable]
        public partial int LoopSizeupQty { get; set; }
        [Bindable]
        public partial int AutomationPartialResubmitCount { get; set; }
        [Bindable]
        public partial bool LoopFreeLookOnNickelNames { get; set; }
        [Bindable]
        public partial double LoopFreeLookOnNickelNamesIncrement { get; set; }
        [Bindable]
        public partial string LoopFreeLookOnNickelNamesRoute { get; set; }
        [Bindable]
        public partial bool LoopFreeLookOnDimeNames { get; set; }
        [Bindable]
        public partial double LoopFreeLookOnDimeNamesIncrement { get; set; }
        [Bindable]
        public partial string LoopFreeLookOnDimeNamesRoute { get; set; }
        [Bindable]
        public partial double ContraFishPriceIncrement { get; set; }
        [Bindable]
        public partial bool LeaveAutoCloseResting { get; set; }
        [Bindable]
        public partial int LoopResubmit { get; set; }
        [Bindable]
        public partial int LoopCountBeforeSizeup { get; set; }
        [Bindable]
        public partial bool LooperDynamicRouting { get; set; }
        [Bindable]
        public partial bool AttemptIncrementUsingDynamicRoute { get; set; }
        [Bindable]
        public partial bool EnableDynamicRouteForOpeningOrders { get; set; }
        [Bindable]
        public partial bool EnableDynamicRouteForClosingOrders { get; set; }
        [Bindable]
        public partial bool LoopFreeLook { get; set; }
        [Bindable]
        public partial bool FreeLookWhenGettingCloseEdge { get; set; }
        [Bindable]
        public partial bool LoopFreeLookOnAll { get; set; }
        [Bindable]
        public partial bool LoopFreeLookOnAllUsingTicks { get; set; }
        [Bindable]
        public partial double FreeLookOnAllIncrementTicks { get; set; }
        [Bindable]
        public partial double FreeLookOnAllWalkBackIncrementTicks { get; set; }
        [Bindable]
        public partial double FreeLookOnAllIncrement { get; set; }
        [Bindable]
        public partial double FreeLookOnAllWalkBackIncrement { get; set; }
        //Submit with delay end

        //Auto cancel
        [Bindable]
        public partial bool CancelWithEdgeToTheoEnabled { get; set; }
        [Bindable]
        public partial double CancelWithTheoEdge { get; set; }
        [Bindable]
        public partial bool CancelWithEdgeToAdjTheoEnabled { get; set; }
        [Bindable]
        public partial double CancelWithAdjTheoEdge { get; set; }
        [Bindable]
        public partial bool CancelWithUnderlyingPxEnabled { get; set; }
        [Bindable]
        public partial double CancelWithUnderlyingPx { get; set; }
        [Bindable]
        public partial bool CancelWithUnderlyingDeltaPxEnabled { get; set; }
        [Bindable]
        public partial double CancelWithUnderlyingDeltaPx { get; set; }
        [Bindable]
        public partial bool CancelWithEdgeToMidEnabled { get; set; }
        [Bindable]
        public partial double CancelWithMidEdge { get; set; }
        [Bindable]
        public partial bool CancelWithWidthEnabled { get; set; }
        [Bindable]
        public partial double CancelWithWidthThreshold { get; set; }
        [Bindable]
        public partial bool CancelWithTimerEnabled { get; set; }
        [Bindable]
        public partial double CancelWithTimer { get; set; }
        [Bindable]
        public partial bool ResubmitAfterCancel { get; set; }
        [Bindable]
        public partial bool UseHedgeUnderlyingForAutoCancel { get; set; }
        [Bindable]
        public partial bool CancelOnClose { get; set; }
        //Auto cancel end
        [Bindable]
        public partial ObservableCollection<BasketTraderViewModel> Baskets { get; set; }
        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial bool StrikeRangeEnabled { get; set; }
        [Bindable]
        public partial double MinStrike { get; set; }
        [Bindable]
        public partial double MaxStrike { get; set; }
        [Bindable]
        public partial bool DeltaRangeEnabled { get; set; }
        [Bindable]
        public partial double MinDelta { get; set; }
        [Bindable]
        public partial double MaxDelta { get; set; }
        [Bindable]
        public partial bool IncludeDecimalStrikes { get; set; }
        public VolTradersManager VolTradersManager { get; }

        public VolTraderViewModel(VolTradersManager volTradersManager, IModuleFactory moduleFactory)
        {
            _lock = new object();
            _moduleFactory = moduleFactory;
            _baskets = new HashSet<BasketTraderViewModel>();
            ModuleTitle = MODULE_TITLE;
            Name = ModuleTitle + " - " + ++_instanceCounter;
            Baskets = new ObservableCollection<BasketTraderViewModel>();
            RoutesList = new ObservableCollection<string>();
            VolTradersManager = volTradersManager;
            VolTradersManager.Add(this);

            SubmitWithDelayInterval = 250;
            SubmitWithDelayIntervalEnd = 250;

            UseGlobalCloseEdge = true;

            ShowSubmitWithDelaySettings = true;
            ShowRouteSettings = true;
            ShowAdvancedRouteSettings = false;
            ShowAutoCloseSettings = true;
            ShowAutoCancelSettings = true;

            ExpirationsList = new ObservableCollection<ExpirationInfoModel>();

            RiskCheckEnabled = OmsCore.Config.GlobalBasketRiskControlEnabledV2;
            ActiveUncheckEnabled = OmsCore.Config.ActiveUncheckEnabled;

            _ = RefreshRoutesCommand();
        }

        [Command]
        public void RiskCheckChangedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.Dispatcher.BeginInvoke(() =>
                    {
                        basket.BasketSettings.RiskCheckEnabled = RiskCheckEnabled;
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RiskCheckChangedCommand));
            }
        }

        [Command]
        public void ActiveUncheckChangedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.Dispatcher.BeginInvoke(() =>
                    {
                        basket.BasketSettings.ActiveUncheckEnabled = ActiveUncheckEnabled;
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActiveUncheckChangedCommand));
            }
        }

        [Command]
        public void AddNewBasketCommand()
        {
            try
            {
                AddNewBasketToVolTraderView view = new()
                {
                    DataContext = this
                };
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddNewBasketCommand));
            }
        }

        [Command]
        public async Task SearchUnderlyingCommand()
        {
            try
            {
                ExpirationsList.Clear();
                string queryString = NewUnderlying;
                if (!string.IsNullOrWhiteSpace(queryString) && !queryString.Contains('.'))
                {
                    Task<List<Option>> getOptionsTask = OmsCore.QuoteClient.GetSymbolsAsync(NewUnderlying);
                    List<Option> options = await getOptionsTask;
                    if (options.Count > 0)
                    {
                        HashSet<Tuple<DateTime, string>> alreadyLoadedExpiration = new();
                        foreach (Option option in options.OrderBy(x => x.Expiration))
                        {
                            string rootSymbol = option.RootSymbol;
                            Tuple<DateTime, string> key = Tuple.Create(option.Expiration, rootSymbol);
                            if (!alreadyLoadedExpiration.Contains(key))
                            {
                                alreadyLoadedExpiration.Add(key);
                                ExpirationsList.Add(new ExpirationInfoModel(option.Expiration, rootSymbol));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchUnderlyingCommand));
            }
        }

        [Command]
        public void AddBasketFromSymbolAndExpirationCommand()
        {
            try
            {
                IsBusy = true;

                try
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
                        IsBusy = false;
                    }

                    async void OnReady(IModuleViewModel module)
                    {
                        module.Ready -= OnReady;
                        view.Visibility = Visibility.Hidden;
                        VolTradersManager.AddBasketToVolTrader(viewModel, this);
                        List<BasketTraderItemModel> items = new();

                        List<Option> symbols = await OmsCore.QuoteClient.GetSymbols(NewUnderlying);
                        if (symbols != null)
                        {
                            List<double> strikes = symbols
                                .Where(x => x.Expiration.Date == ExpirationInfo.Expiration.Date &&
                                            x.Strike >= NewMinStrike && x.Strike <= NewMaxStrike)
                                .Select(x => x.Strike).Distinct().ToList();
                            if (!IncludeDecimalStrikes)
                            {
                                strikes = strikes.Where(x => x % 1 == 0).ToList();
                            }

                            foreach (double strike in strikes)
                            {
                                BasketTraderItemModel item = new(viewModel, Dispatcher, OmsCore);
                                items.Add(item);
                                item.LoadSingleLeg(NewUnderlying, NewType, ExpirationInfo.Expiration, strike,
                                    symbols);
                            }
                        }

                        if (DeltaRangeEnabled)
                        {
                            foreach (BasketTraderItemModel item in items.ToList())
                            {
                                await item.WaitForTheoLoadAsync();
                                double totalDelta = Math.Abs(item.TotalDelta);
                                bool valid = totalDelta >= MinDelta && totalDelta <= MaxDelta;
                                if (!valid)
                                {
                                    items.Remove(item);
                                }
                            }
                        }

                        await viewModel.AddMultipleToBasketAsync(items);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(AddBasket));
                    _ = Dispatcher.BeginInvoke(() => MessageBoxService.ShowMessage($"Failed to create basket for {NewUnderlying} {ExpirationInfo.Expiration:MMM dd yy}?", Name, MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddBasketFromSymbolAndExpirationCommand));
            }
        }

        [Command]
        public async Task RefreshRoutesCommand()
        {
            HashSet<string> uniqueRoutes = new(StringComparer.OrdinalIgnoreCase);
            var currentBroker = OmsCore.Config.DefaultBroker;

            var routeLookup = OmsCore.OrderClient?.RouteLookup;
            var ogRoutes = !string.IsNullOrWhiteSpace(currentBroker)
                ? (routeLookup?.GetRoutesForBroker(currentBroker) ?? Array.Empty<string>())
                : (routeLookup?.GetRoutes() ?? Array.Empty<string>());
            foreach (var route in ogRoutes)
            {
                uniqueRoutes.Add(route);
            }

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                RoutesList.Clear();
                RoutesList.Add("");
                foreach (string route in uniqueRoutes.OrderBy(x => x))
                {
                    RoutesList.Add(route);
                }
            }));
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
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
        public void CloneCommand()
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    VolTraderView window = new();
                    VolTraderViewModel viewModel = (VolTraderViewModel)window.DataContext;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (_, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (_, _) =>
                    {
                        viewModel.StrikeRangeEnabled = StrikeRangeEnabled;
                        viewModel.IncludeDecimalStrikes = IncludeDecimalStrikes;
                        viewModel.MinStrike = MinStrike;
                        viewModel.MaxStrike = MaxStrike;

                        foreach (BasketTraderViewModel basketViewModel in Baskets)
                        {
                            if (basketViewModel != null)
                            {
                                viewModel.AddBasket(basketViewModel);
                            }
                        }
                    };

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        [Command]
        public void BrowseLayoutsCommand()
        {
        }

        [Command]
        public async Task ExpirationUpCommand(BasketTraderViewModel selectedBasket)
        {
            try
            {
                if (Baskets.Count == 1 && selectedBasket == null)
                {
                    selectedBasket = Baskets.FirstOrDefault();
                }
                if (BasketCount > 0 && selectedBasket != null)
                {
                    IsBusy = true;
                    await Task.Run(async () =>
                    {
                        IOrderedEnumerable<DateTime> expirations = selectedBasket.GetExpirations();
                        string underlying = selectedBasket.BasketItems.Where(x => x.Underlying != null).Select(x => x.Underlying).FirstOrDefault();
                        List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(underlying);
                        if (options == null || options.Count == 0)
                        {
                            return;
                        }
                        List<DateTime> allExpirations = options.Select(x => x.Expiration).Distinct().ToList();
                        List<Task<BasketTraderViewModel>> loadTasks = new();
                        foreach (DateTime expiration in expirations)
                        {
                            List<DateTime> nextExpirations = allExpirations.Where(x => x > expiration).OrderBy(x => x).ToList();
                            for (int i = 0; i < BasketCount; i++)
                            {
                                if (nextExpirations.Count < i + 1)
                                {
                                    break;
                                }
                                DateTime nextExpiration = nextExpirations[i];
                                Task<BasketTraderViewModel> item = LoadBasket(selectedBasket, nextExpiration);
                                if (item != null)
                                {
                                    loadTasks.Add(item);
                                }
                            }
                        }
                        await Task.WhenAll(loadTasks);
                        List<Task> tasks = new();
                        foreach (BasketTraderViewModel basket in loadTasks.Where(x => x.IsCompletedSuccessfully).Select(x => x.Result))
                        {
                            tasks.Add(basket.LoadRangeAsync(StrikeRangeEnabled, MinStrike, MaxStrike, IncludeDecimalStrikes, DeltaRangeEnabled, MinDelta, MaxDelta));
                        }
                        await Task.WhenAll(tasks);
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public async Task ExpirationDownCommand(BasketTraderViewModel selectedBasket)
        {
            try
            {
                if (Baskets.Count == 1 && selectedBasket == null)
                {
                    selectedBasket = Baskets.FirstOrDefault();
                }
                if (BasketCount > 0 && selectedBasket != null)
                {
                    IsBusy = true;
                    await Task.Run(async () =>
                    {
                        IOrderedEnumerable<DateTime> expirations = selectedBasket.GetExpirations();
                        string underlying = selectedBasket.BasketItems.Where(x => x.Underlying != null).Select(x => x.Underlying).FirstOrDefault();
                        List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(underlying);
                        if (options == null || options.Count == 0)
                        {
                            return;
                        }
                        List<DateTime> allExpirations = options.Select(x => x.Expiration).Distinct().ToList();
                        List<Task<BasketTraderViewModel>> loadTasks = new();
                        foreach (DateTime expiration in expirations)
                        {
                            List<DateTime> nextExpirations = allExpirations.Where(x => x < expiration).OrderByDescending(x => x).ToList();
                            for (int i = 0; i < BasketCount; i++)
                            {
                                if (nextExpirations.Count < i + 1)
                                {
                                    break;
                                }
                                DateTime nextExpiration = nextExpirations[i];
                                loadTasks.Add(LoadBasket(selectedBasket, nextExpiration));
                            }
                        }
                        await Task.WhenAll(loadTasks);
                        List<Task> tasks = new();
                        foreach (BasketTraderViewModel basket in loadTasks.Where(x => x.IsCompletedSuccessfully).Select(x => x.Result))
                        {
                            tasks.Add(basket.LoadRangeAsync(StrikeRangeEnabled, MinStrike, MaxStrike, IncludeDecimalStrikes, DeltaRangeEnabled, MinDelta, MaxDelta));
                        }
                        await Task.WhenAll(tasks);
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public async Task RefreshRangeCommand()
        {
            await Task.Run(async () =>
            {
                List<Task> tasks = new();
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    tasks.Add(basket.LoadRangeAsync(StrikeRangeEnabled, MinStrike, MaxStrike, IncludeDecimalStrikes, DeltaRangeEnabled, MinDelta, MaxDelta));
                }
                await Task.WhenAll(tasks);
            });
        }

        [Command]
        public void CloneAndFlipCallPutCommand()
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    VolTraderView window = new();
                    VolTraderViewModel viewModel = (VolTraderViewModel)window.DataContext;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (_, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (_, _) =>
                    {
                        viewModel.StrikeRangeEnabled = StrikeRangeEnabled;
                        viewModel.IncludeDecimalStrikes = IncludeDecimalStrikes;
                        viewModel.MinStrike = MinStrike;
                        viewModel.MaxStrike = MaxStrike;

                        foreach (BasketTraderViewModel basket in Baskets)
                        {
                            BasketTraderViewModel basketViewModel = basket.Clone("Hide").ViewModel;
                            if (basketViewModel != null)
                            {
                                viewModel.AddBasket(basketViewModel);
                            }
                        }
                    };

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        private Task<BasketTraderViewModel> LoadBasket(BasketTraderViewModel selectedBasket, DateTime nextExpiration)
        {
            Task<BasketTraderViewModel> loadTask = null;
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

                void OnReady(IModuleViewModel module)
                {
                    module.Ready -= OnReady;
                    view.Visibility = Visibility.Hidden;
                    VolTradersManager.AddBasketToVolTrader(viewModel, this);
                    loadTask = !StrikeRangeEnabled
                        ? viewModel.LoadFromTemplatePermAsync(selectedBasket, nextExpiration, DeltaRangeEnabled, MinDelta, MaxDelta)
                        : viewModel.LoadRangeFromTemplatePermAsync(selectedBasket, nextExpiration, MinStrike, MaxStrike, IncludeDecimalStrikes, DeltaRangeEnabled, MinDelta, MaxDelta);

                }
            }
            return loadTask;
        }

        [Command]
        public void CancelAllCommand()
        {
            foreach (BasketTraderViewModel basket in Baskets)
            {
                basket.CancelAllNoCheck();
            }
        }

        [Command]
        public void StopAllCommand()
        {
            foreach (BasketTraderViewModel basket in Baskets)
            {
                basket.StopAllLoops();
                basket.CancelAllNoCheck();
            }
        }

        [Command]
        public void AddBasket(BasketTraderViewModel basketTraderViewModel)
        {
            try
            {
                bool added;
                lock (_lock)
                {
                    added = _baskets.Add(basketTraderViewModel);
                }

                if (Baskets.Count == 0)
                {
                    AutomationConfigModel automationConfig = basketTraderViewModel.GetAutomationConfig();

                    CancelWithEdgeToTheoEnabled = basketTraderViewModel.BasketSettings.CancelWithEdgeToTheoEnabled;
                    CancelWithTheoEdge = basketTraderViewModel.BasketSettings.CancelWithTheoEdge;
                    CancelWithEdgeToAdjTheoEnabled = basketTraderViewModel.BasketSettings.CancelWithEdgeToAdjTheoEnabled;
                    CancelWithAdjTheoEdge = basketTraderViewModel.BasketSettings.CancelWithAdjTheoEdge;
                    CancelWithUnderlyingPxEnabled = basketTraderViewModel.BasketSettings.CancelWithUnderlyingPxEnabled;
                    CancelWithUnderlyingPx = basketTraderViewModel.BasketSettings.CancelWithUnderlyingPx;
                    CancelWithUnderlyingDeltaPxEnabled = basketTraderViewModel.BasketSettings.CancelWithUnderlyingDeltaPxEnabled;
                    CancelWithUnderlyingDeltaPx = basketTraderViewModel.BasketSettings.CancelWithUnderlyingDeltaPx;
                    CancelWithEdgeToMidEnabled = basketTraderViewModel.BasketSettings.CancelWithEdgeToMidEnabled;
                    CancelWithMidEdge = basketTraderViewModel.BasketSettings.CancelWithMidEdge;
                    CancelWithWidthEnabled = basketTraderViewModel.BasketSettings.CancelWithWidthEnabled;
                    CancelWithWidthThreshold = basketTraderViewModel.BasketSettings.CancelWithWidthThreshold;
                    CancelWithTimerEnabled = basketTraderViewModel.BasketSettings.CancelWithTimerEnabled;
                    CancelWithTimer = basketTraderViewModel.BasketSettings.CancelWithTimer;
                    ResubmitAfterCancel = basketTraderViewModel.BasketSettings.ResubmitAfterCancel;
                    UseHedgeUnderlyingForAutoCancel = basketTraderViewModel.BasketSettings.UseHedgeUnderlyingForAutoCancel;
                    CancelOnClose = basketTraderViewModel.BasketSettings.CancelOnClose;
                    SubmitWithDelayEnabled = basketTraderViewModel.BasketSettings.SubmitWithDelayEnabled;
                    SubmitWithDelayInterval = basketTraderViewModel.BasketSettings.SubmitWithDelayInterval;
                    SubmitWithDelayIntervalEnd = basketTraderViewModel.BasketSettings.SubmitWithDelayIntervalEnd;
                    OpenTicketForFills = basketTraderViewModel.BasketSettings.OpenTicketForFills;
                    OpenTicketForFailedClose = basketTraderViewModel.BasketSettings.OpenTicketForFailedClose;
                    CancelOnAmountOfFillsCount = basketTraderViewModel.BasketSettings.CancelOnAmountOfFillsCount == 0 ? 1 : Math.Min(basketTraderViewModel.BasketSettings.CancelOnAmountOfFillsCount, OmsCore.Config.MaxCancelOnLimitV2);
                    Randomize = basketTraderViewModel.BasketSettings.Randomize;
                    Resume = basketTraderViewModel.BasketSettings.Resume;
                    DisablePriceRounding = basketTraderViewModel.BasketSettings.DisablePriceRounding;
                    MaxRestingOrdersEnabled = basketTraderViewModel.BasketSettings.MaxRestingOrdersEnabled;
                    MaxRestingOrdersCount = basketTraderViewModel.BasketSettings.MaxRestingOrdersCount;
                    StartProcessingFromSelectedRow = basketTraderViewModel.BasketSettings.StartProcessingFromSelectedRow;
                    ContraEnabled = basketTraderViewModel.ContraEnabled;
                    AlsoOpenContraTicketEnabled = basketTraderViewModel.AlsoOpenContraTicketEnabled;
                    LooperOpenRoute = automationConfig.LooperOpenRoute;
                    LooperCloseRoute = automationConfig.LooperCloseRoute;
                    UseSingleLegSeparateLooperRoutes = automationConfig.UseSingleLegSeparateLooperRoutes;
                    StockTiedOrderRoute = automationConfig.StockTiedOrderRoute;
                    LooperOpenRouteSingleLeg = automationConfig.LooperOpenRouteSingleLeg;
                    LooperCloseRouteSingleLeg = automationConfig.LooperCloseRouteSingleLeg;
                    CloseOrderMode = automationConfig.CloseOrderMode;
                    if (UseGlobalCloseEdge)
                    {
                        ContraFishEdge = automationConfig.ContraFishEdge;
                        CloseEdgeMinValue = automationConfig.CloseEdgeMinValue;
                    }
                    LoopMaxLoss = automationConfig.LoopMaxLoss;
                    LoopMinEdgePercentage = automationConfig.LoopMinEdgePercentage;
                    LoopMinEdgeUsePercentage = automationConfig.LoopMinEdgeUsePercentage;
                    LoopMinEdge = automationConfig.LoopMinEdge;
                    MaxLoopCount = automationConfig.MaxLoopCount;
                    AutomationRequiredPartialFillPercentage = automationConfig.AutomationRequiredPartialFillPercentage;
                    LoopPricingMode = automationConfig.LoopPricingMode;
                    ContraFishInterval = automationConfig.ContraFishInterval;
                    ContraFishIntervalMax = automationConfig.ContraFishIntervalMax;
                    ClosingMode = automationConfig.ClosingMode;
                    LoopInterval = automationConfig.LoopInterval;
                    LoopIntervalMax = automationConfig.LoopIntervalMax;
                    AttemptResubmit = automationConfig.AttemptResubmit;
                    LoopSizeupType = automationConfig.LoopSizeupType;
                    LoopSizeupQty = automationConfig.LoopSizeupQty;
                    AutomationPartialResubmitCount = automationConfig.AutomationPartialResubmitCount;
                    ContraFishPriceIncrement = automationConfig.ContraFishPriceIncrement;
                    LeaveAutoCloseResting = automationConfig.LeaveAutoCloseResting;
                    LoopResubmit = automationConfig.LoopResubmit;
                    LoopCountBeforeSizeup = automationConfig.LoopCountBeforeSizeup;
                    LooperDynamicRouting = automationConfig.LooperDynamicRouting;
                    AttemptIncrementUsingDynamicRoute = automationConfig.AttemptIncrementUsingDynamicRoute;
                    EnableDynamicRouteForOpeningOrders = automationConfig.EnableDynamicRouteForOpeningOrders;
                    EnableDynamicRouteForClosingOrders = automationConfig.EnableDynamicRouteForClosingOrders;
                    LoopFreeLook = automationConfig.LoopFreeLook;
                    FreeLookWhenGettingCloseEdge = automationConfig.FreeLookWhenGettingCloseEdge;
                    LoopFreeLookOnAll = automationConfig.LoopFreeLookOnAll;
                    LoopFreeLookOnAllUsingTicks = automationConfig.LoopFreeLookOnAllUsingTicks;
                    FreeLookOnAllIncrementTicks = automationConfig.FreeLookOnAllIncrementTicks;
                    FreeLookOnAllWalkBackIncrementTicks = automationConfig.FreeLookOnAllWalkBackIncrementTicks;
                    FreeLookOnAllIncrement = automationConfig.FreeLookOnAllIncrement;
                    FreeLookOnAllWalkBackIncrement = automationConfig.FreeLookOnAllWalkBackIncrement;
                    LoopFreeLookOnNickelNames = automationConfig.LoopFreeLookOnNickelNames;
                    LoopFreeLookOnNickelNamesIncrement = automationConfig.LoopFreeLookOnNickelNamesIncrement;
                    LoopFreeLookOnNickelNamesRoute = automationConfig.LoopFreeLookOnNickelNamesRoute;
                    LoopFreeLookOnDimeNames = automationConfig.LoopFreeLookOnDimeNames;
                    LoopFreeLookOnDimeNamesIncrement = automationConfig.LoopFreeLookOnDimeNamesIncrement;
                    LoopFreeLookOnDimeNamesRoute = automationConfig.LoopFreeLookOnDimeNamesRoute;
                }

                if (added)
                {
                    Dispatcher.BeginInvoke(() => Baskets.Add(basketTraderViewModel));
                }

                if (basketTraderViewModel.BasketItems.Count > 0)
                {
                    double minStrike = basketTraderViewModel.BasketItems.Min(x => x.Legs.Where(legModel => legModel.Strike.Strike != 0).Min(legModel => legModel.Strike.Strike));
                    if (MinStrike == 0 || minStrike < MinStrike)
                    {
                        MinStrike = minStrike;
                    }

                    double maxStrike = basketTraderViewModel.BasketItems.Max(x => x.Legs.Where(legModel => legModel.Strike.Strike != 0).Max(legModel => legModel.Strike.Strike));
                    if (MaxStrike == 0 || maxStrike > MaxStrike)
                    {
                        MaxStrike = maxStrike;
                    }

                    double minDelta = basketTraderViewModel.BasketItems.Min(x => Math.Abs(x.TotalDelta));
                    if (MinDelta == 0 || minDelta < MinDelta)
                    {
                        MinDelta = minDelta;
                    }

                    double maxDelta = basketTraderViewModel.BasketItems.Max(x => Math.Abs(x.TotalDelta));
                    if (MaxDelta == 0 || maxDelta > MaxDelta)
                    {
                        MaxDelta = maxDelta;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddBasket));
            }
        }

        [Command]
        public async Task ResetQtyCommand()
        {
            await Task.Run(() =>
            {
                bool error = false;
                foreach (BasketTraderViewModel basket in Baskets.ToList())
                {
                    try
                    {
                        basket.ClearQty();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ResetQtyCommand));
                        error = true;
                    }
                }
                if (error)
                {
                    _ = Dispatcher.BeginInvoke(() => MessageBoxService.ShowMessage($"An error occurred when trying to reset the quantity.", Name, MessageButton.OK, MessageIcon.Error));
                }
            });
        }

        [Command]
        public void ActivateWindowCommand(BasketTraderViewModel basketModel)
        {
            basketModel.Activate();
        }

        [Command]
        public void HideWindowCommand(BasketTraderViewModel basketModel)
        {
            basketModel.Hide();
        }

        [Command]
        public void CloseWindowCommand(BasketTraderViewModel basketModel)
        {
            basketModel.Close();
        }

        [Command]
        public void ModifyStagedPxQty(BasketTraderViewModel basketModel)
        {
            basketModel.ModifyStagedPxQty();
        }

        [Command]
        public void RemoveBasket(object parameter)
        {
            try
            {
                if (parameter is IEnumerable<object> selectedItems)
                {
                    var basketTraderViewModels = selectedItems.ToList();
                    MessageResult response = Dispatcher.Invoke(() => MessageBoxService.ShowMessage(basketTraderViewModels.Count == 0 ? $"Do you also want to close the Basket?" : $"Do you also want to close the {basketTraderViewModels.Count} Baskets?", Name, MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No));
                    switch (response)
                    {
                        case MessageResult.Cancel:
                            return;
                        case MessageResult.Yes:
                            foreach (BasketTraderViewModel basketTraderViewModel in basketTraderViewModels)
                            {
                                VolTradersManager.RemoveBasketFromVolTraders(basketTraderViewModel);
                                basketTraderViewModel.Close();
                                RemoveBasketNoPrompt(basketTraderViewModel);
                            }
                            break;
                        case MessageResult.No:
                            foreach (BasketTraderViewModel basketTraderViewModel in basketTraderViewModels)
                            {
                                basketTraderViewModel.Activate();
                                RemoveBasketNoPrompt(basketTraderViewModel);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveBasket));
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToTheoCommand()
        {
            try
            {
                CancelWithEdgeToTheoEnabled = !CancelWithEdgeToTheoEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithEdgeToTheoCommand));
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToAdjTheoCommand()
        {
            try
            {
                CancelWithEdgeToAdjTheoEnabled = !CancelWithEdgeToAdjTheoEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithEdgeToAdjTheoCommand));
            }
        }

        [Command]
        public void ToggleCancelWithUnderlyingPxCommand()
        {
            try
            {
                CancelWithUnderlyingPxEnabled = !CancelWithUnderlyingPxEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithUnderlyingPxCommand));
            }
        }

        [Command]
        public void ToggleCancelWithUnderlyingDeltaPxCommand()
        {
            try
            {
                CancelWithUnderlyingDeltaPxEnabled = !CancelWithUnderlyingDeltaPxEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithUnderlyingDeltaPxCommand));
            }
        }

        [Command]
        public void ToggleCancelWithEdgeToMidCommand()
        {
            try
            {
                CancelWithEdgeToMidEnabled = !CancelWithEdgeToMidEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithEdgeToMidCommand));
            }
        }

        [Command]
        public void ToggleCancelWithWidthCommand()
        {
            try
            {
                CancelWithWidthEnabled = !CancelWithWidthEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithWidthCommand));
            }
        }

        [Command]
        public void ToggleCancelWithTimerCommand()
        {
            try
            {
                CancelWithTimerEnabled = !CancelWithTimerEnabled;
                AutoCancelUpdatedCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ToggleCancelWithTimerCommand));
            }
        }

        [Command]
        public void AutoCancelUpdatedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.VolTraderAutoCancelUpdate(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AutoCancelUpdatedCommand));
            }
        }

        [Command]
        public void SubmitWithDelayUpdatedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.VolTraderSubmitWithDelayUpdated(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitWithDelayUpdatedCommand));
            }
        }

        [Command]
        public void VolTraderRouteUpdatedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.VolTraderRouteUpdated(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderRouteUpdatedCommand));
            }
        }

        [Command]
        public void VolTraderAutomationUpdatedCommand()
        {
            try
            {
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.VolTraderAutomationUpdated(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(VolTraderAutomationUpdatedCommand));
            }
        }

        [Command]
        public void ReverseLooperRoutesCommand()
        {
            try
            {
                (LooperCloseRoute, LooperOpenRoute) = (LooperOpenRoute, LooperCloseRoute);
                (LooperCloseRouteSingleLeg, LooperOpenRouteSingleLeg) = (LooperOpenRouteSingleLeg, LooperCloseRouteSingleLeg);
                foreach (BasketTraderViewModel basket in Baskets)
                {
                    basket.VolTraderRouteUpdated(this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseLooperRoutesCommand));
            }
        }

        public void RemoveBasketNoPrompt(BasketTraderViewModel basketTraderViewModel)
        {
            bool removed;
            lock (_lock)
            {
                removed = _baskets.Remove(basketTraderViewModel);
            }
            if (removed)
            {
                Dispatcher.BeginInvoke(() => Baskets.Remove(basketTraderViewModel));
            }
        }

        internal bool Dispose()
        {
            if (Baskets.Count == 0)
            {
                return false;
            }

            MessageResult response = Dispatcher.Invoke(() => MessageBoxService.ShowMessage("Do you want to close all baskets that are currently loaded in Vol Trader?", Name, MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No));
            switch (response)
            {
                case MessageResult.Cancel:
                    return true;
                case MessageResult.Yes:
                    VolTradersManager.Remove(this);
                    foreach (BasketTraderViewModel basket in Baskets)
                    {
                        basket.Close();
                    }
                    break;
                case MessageResult.No:
                    VolTradersManager.Remove(this);
                    foreach (BasketTraderViewModel basket in Baskets)
                    {
                        basket.Activate();
                    }
                    break;
            }
            return false;
        }

        internal string GetViewModelConfigSerialized()
        {
            VolTraderConfig config = new()
            {
                IncludeDecimalStrikes = IncludeDecimalStrikes,
                StrikeRangeEnabled = StrikeRangeEnabled,
                MinStrike = MinStrike,
                MaxStrike = MaxStrike,
                DeltaRangeEnabled = DeltaRangeEnabled,
                MinDelta = MinDelta,
                MaxDelta = MaxDelta,
            };

            foreach (BasketTraderViewModel basketViewModel in Baskets)
            {
                if (basketViewModel != null)
                {
                    config.BasketIdToBasketMap[basketViewModel.Uid] = basketViewModel.GetConfigSerialized(withItems: true);
                }
            }

            return config.Serialize();
        }

        internal void LoadFromConfig(string volTraderConfig, bool isDefault)
        {
            VolTraderConfig config = ModuleConfigBase.Deserialize<VolTraderConfig>(volTraderConfig);
            if (config != null)
            {
                IncludeDecimalStrikes = config.IncludeDecimalStrikes;
                StrikeRangeEnabled = config.StrikeRangeEnabled;
                MinStrike = config.MinStrike;
                MaxStrike = config.MaxStrike;
                DeltaRangeEnabled = config.DeltaRangeEnabled;
                MinDelta = config.MinDelta;
                MaxDelta = config.MaxDelta;

                if (!isDefault && OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    foreach (KeyValuePair<string, string> idConfigPair in config.BasketIdToBasketMap)
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

                            async void OnReady(IModuleViewModel module)
                            {
                                module.Ready -= OnReady;
                                view.Visibility = Visibility.Hidden;
                                VolTradersManager.AddBasketToVolTrader(viewModel, this);
                                await viewModel.DeserializeAndLoadConfig(idConfigPair.Value);
                            }

                        }
                    }
                }
            }
        }
    }
}
