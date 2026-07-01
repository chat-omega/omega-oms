using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class GammaScalpingModuleViewModel : CustomizableTableViewModelBase, IDeltaHedgeManagerModel
    {
        private static readonly string MODULE_TITLE = "Gamma Scalp";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, UnderlyingPositionModel> _underlyingSymbolToUnderlyingPositionModelMap;
        private DispatcherTimer _updateTimer;
        private bool _roundDeltaForHedge;
        private bool _isHedging;
        private ObservableCollection<string> _routesList;
        private string _account;
        private ObservableCollection<string> _accounts;
        private OrderType _orderType;
        private LimitHandling _limitHandling;
        private DispatcherTimer _chartUpdateTimer;
        private readonly YAxisViewModel _underAxes;
        private readonly YAxisViewModel _pnlAxes;

        private readonly ConcurrentDictionary<string, LiveChartSeriesModel> _symbolToChartSeriesModelMap;

        private readonly LiveChartSeriesModel _midDataPoints;
        private readonly LiveChartSeriesModel _emaDataPoints;
        private readonly LiveChartSeriesModel _pnlDataPoints;
        private readonly LiveChartSeriesModel _pnlEmaDataPoints;
        private readonly LiveChartSeriesModel _vegaPnlDataPoints;

        public ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        public static List<PositionEntryType> PositionEntryTypes { get; } = ((PositionEntryType[])Enum.GetValues(typeof(PositionEntryType))).ToList();
        public static List<OrderType> OrderTypes { get; } = ((OrderType[])Enum.GetValues(typeof(OrderType))).ToList();
        public static List<LimitHandling> LimitHandlingOptions { get; } = ((LimitHandling[])Enum.GetValues(typeof(LimitHandling))).ToList();
        public static List<GammaScalpTriggerMode> GammaScalpTriggerModes { get; } = ((GammaScalpTriggerMode[])Enum.GetValues(typeof(GammaScalpTriggerMode))).ToList();
        public ObservableCollection<LiveChartSeriesModel> ChartSeries { get; set; }
        public ObservableCollection<YAxisViewModel> ChartYAxes { get; set; }
        public ObservableCollection<UnderlyingPositionModel> ScalpedPositions { get; set; }

        public OmsCore OmsCore { get; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial UnderlyingPositionModel UnderlyingPositionModel { get; set; }

        [Bindable]
        public partial bool UnderlyingLoaded { get; set; }

        private string _Underlying;
        public string Underlying
        {
            get => _Underlying;
            set
            {
                SetValue(ref _Underlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
                HedgeUnderlying = value;
            }
        }

        private string _HedgeUnderlying;
        public string HedgeUnderlying
        {
            get => _HedgeUnderlying;
            set => SetValue(ref _HedgeUnderlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial double HedgeMultiplier { get; set; }

        [Bindable]
        public partial double AutoHedgeLimitDiff { get; set; }

        [Bindable]
        public partial double InitialHedgeLimitDiff { get; set; }

        public bool IsHedging
        {
            get => _isHedging;
            set
            {
                _isHedging = value;
                NotifyPropertyChanged();
            }
        }

        public bool RoundDeltaForHedge
        {
            get => _roundDeltaForHedge;
            set
            {
                _roundDeltaForHedge = value;
                NotifyPropertyChanged();
            }
        }

        public ObservableCollection<string> RoutesList
        {
            get => _routesList;
            set
            {
                _routesList = value;
                NotifyPropertyChanged();
            }
        }

        public ObservableCollection<string> Accounts
        {
            get => _accounts;
            set
            {
                _accounts = value;
                NotifyPropertyChanged();
            }
        }

        public string Account
        {
            get => _account;
            set
            {
                _account = value;
                NotifyPropertyChanged();
            }
        }

        public OrderType OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                NotifyPropertyChanged();
            }
        }

        public LimitHandling LimitHandling
        {
            get => _limitHandling;
            set
            {
                _limitHandling = value;
                NotifyPropertyChanged();
            }
        }

        public bool GammaScalper { get; set; }
        public ComplexOrderTicketViewModel OrderTicket { get; }

        public GammaScalpingModuleViewModel(ComplexOrderTicketViewModel complexOrderTicketViewModel, OmsCore omsCore)
        {
            _underlyingSymbolToUnderlyingPositionModelMap = new ConcurrentDictionary<string, UnderlyingPositionModel>();
            HedgeMultiplier = 1;
            GammaScalper = true;
            OmsCore = omsCore;
            ModuleTitle = MODULE_TITLE;
            OrderType = OrderType.Limit;
            Accounts = new ObservableCollection<string>();
            RoutesList = new ObservableCollection<string>();
            ScalpedPositions = new ObservableCollection<UnderlyingPositionModel>();

            _underAxes = new YAxisViewModel("Under", null);
            _pnlAxes = new YAxisViewModel("PnL", null, DevExpress.Xpf.Charts.AxisAlignment.Far);

            ChartYAxes = new ObservableCollection<YAxisViewModel>
            {
                _underAxes,
                _pnlAxes,
            };

            _midDataPoints = new LiveChartSeriesModel("Under Mid", _underAxes);
            _emaDataPoints = new LiveChartSeriesModel("Under EMA", _underAxes);

            _pnlDataPoints = new LiveChartSeriesModel("Net PnL", _pnlAxes);
            _pnlEmaDataPoints = new LiveChartSeriesModel("PnL EMA", _pnlAxes);
            _vegaPnlDataPoints = new LiveChartSeriesModel("Vega PnL", _pnlAxes);

            _symbolToChartSeriesModelMap = new ConcurrentDictionary<string, LiveChartSeriesModel>();
            ChartSeries = new ObservableCollection<LiveChartSeriesModel>()
            {
                _midDataPoints,
                _emaDataPoints,
                _pnlDataPoints,
                _pnlEmaDataPoints,
                _vegaPnlDataPoints,
            };

            StartUpdateTimer();
            OrderTicket = complexOrderTicketViewModel;
            OrderTicket.IsGammaScalpTicket = true;
        }

        [Command]
        public async void SearchUnderlyingCommand()
        {
            try
            {
                if (!UnderlyingLoaded)
                {
                    UnderlyingLoaded = true;
                    List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                    MDUnderlying details = OmsCore.QuoteClient.GetUnderlyingDetails(Underlying);
                    if (symbols.Count > 0 && details != null)
                    {
                        AddSymbol(Underlying, HedgeUnderlying, HedgeMultiplier, details);
                    }
                    else
                    {
                        UnderlyingLoaded = false;
                        MessageBoxService.ShowMessage("Symbol not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchUnderlyingCommand));
            }
        }

        [Command]
        public async Task SubmitAsyncCommand()
        {
            await OrderTicket.SubmitAsync();
        }

        [Command]
        public async Task SubmitContraAsyncCommand()
        {
            await OrderTicket.SubmitContraAsync();
        }

        internal void LoadHedgeRoutes()
        {
            try
            {
                if (RoutesList.Count == 0)
                {
                    Task.Run(() =>
                    {
                        List<Comms.Models.Data.ZPAccount> accounts = OmsCore.OrderClient.GetAccountAndRoutes("AAPL");
                        var routeLookup = OmsCore.OrderClient?.RouteLookup;
                        var routes = routeLookup?.GetRoutes() ?? Array.Empty<string>();
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (accounts != null)
                            {
                                Accounts = accounts.Select(x => x.Acronym).ToObservableCollection();
                                Account = OmsCore.Config.DefaultAccount;
                                if (!Accounts.Contains(Account))
                                {
                                    Accounts.Add(Account);
                                }
                            }
                            RoutesList = routes.ToObservableCollection();
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadHedgeRoutes));
            }
        }

        private void StartUpdateTimer()
        {
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _updateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
            _updateTimer.Tick += (_, _) =>
            {
                RunHedgeDeltaUpdate();
            };
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            OrderTicket.SetDispatcher(dispatcher);
            LoadHedgeRoutes();
            _updateTimer.Start();
            _chartUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _chartUpdateTimer.Tick += UpdateChart;
            _chartUpdateTimer.Start();
        }

        private void UpdateChart(object sender, EventArgs e)
        {
            if (UnderlyingPositionModel != null && !double.IsNaN(UnderlyingPositionModel.Mid))
            {
                DateTime time = DateTime.Now;
                _midDataPoints.ChartPoints.Add(new ChartValueModel(UnderlyingPositionModel.Mid, time));
                _emaDataPoints.ChartPoints.Add(new ChartValueModel(UnderlyingPositionModel.Ema, time));
                _pnlDataPoints.ChartPoints.Add(new ChartValueModel(UnderlyingPositionModel.NetPnl, time));
                _pnlEmaDataPoints.ChartPoints.Add(new ChartValueModel(UnderlyingPositionModel.PnlEma, time));
                _vegaPnlDataPoints.ChartPoints.Add(new ChartValueModel(UnderlyingPositionModel.IvVegaPnl, time));

                if (OrderTicket != null)
                {
                    foreach (TicketLegModel leg in OrderTicket.Legs)
                    {
                        if (!string.IsNullOrWhiteSpace(leg.Symbol) && leg.ActualQty != 0)
                        {
                            if (!_symbolToChartSeriesModelMap.TryGetValue(leg.Symbol, out LiveChartSeriesModel model))
                            {
                                model = new LiveChartSeriesModel(leg.Symbol, _pnlAxes);
                                ChartSeries.Add(model);
                                _symbolToChartSeriesModelMap[leg.Symbol] = model;
                            }
                            model.ChartPoints.Add(new ChartValueModel(leg.IvVegaPnl, time));
                        }
                    }
                }
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
        public void ResetPositionPnlCommand()
        {
            UnderlyingPositionModel.PositionUnrealPnl = 0;
            UnderlyingPositionModel.PositionRealPnl = 0;

            UnderlyingPositionModel.NetPnl = UnderlyingPositionModel.ScalpPnl + UnderlyingPositionModel.ScalpUnrealPnl + UnderlyingPositionModel.PositionUnrealPnl + UnderlyingPositionModel.PositionRealPnl;
        }

        internal UnderlyingPositionModel AddSymbol(string underlyingSymbol, string hedgeUnderlying, double hedgeMultiplier, MDUnderlying details = null)
        {
            bool isNew = !_underlyingSymbolToUnderlyingPositionModelMap.TryGetValue(underlyingSymbol, out UnderlyingPositionModel underlyingPositionModel);
            if (isNew)
            {
                UnderlyingLoaded = true;
                Underlying = underlyingSymbol;
                HedgeUnderlying = hedgeUnderlying;
                HedgeMultiplier = hedgeMultiplier;

                if (OrderTicket.Underlying != underlyingSymbol)
                {
                    OrderTicket.Underlying = underlyingSymbol;
                    _ = OrderTicket.SearchUnderlying();
                }

                UnderlyingPositionModel = new UnderlyingPositionModel(this, underlyingSymbol, hedgeUnderlying, hedgeMultiplier, details)
                {
                    Active = true
                };
                UnderlyingPositionModel.SetOrderTicket(OrderTicket);
                _underlyingSymbolToUnderlyingPositionModelMap[underlyingSymbol] = UnderlyingPositionModel;
                Dispatcher.BeginInvoke(() =>
                {
                    ScalpedPositions.Add(UnderlyingPositionModel);
                });
            }
            else
            {
                UnderlyingPositionModel = underlyingPositionModel;
            }
            return UnderlyingPositionModel;
        }

        [Command]
        public void OpenInPositionManagerCommand(UnderlyingPositionModel hedgePositionModel)
        {
            HedgePositionManagementView managementView = new();
            if (managementView.DataContext is HedgePositionManagementViewModel positionManagementViewModel)
            {
                positionManagementViewModel.UnderlyingPositionModel = hedgePositionModel;
                managementView.Show();
            }
            else
            {
                _log.Error(nameof(OpenInPositionManagerCommand) + " position manager load failed.");
            }
        }

        [Command]
        public void ActivateAllCommand()
        {
            try
            {
                if (UnderlyingPositionModel != null)
                {
                    UnderlyingPositionModel.Active = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void DeactivateAllCommand()
        {
            try
            {
                if (UnderlyingPositionModel != null)
                {
                    UnderlyingPositionModel.Active = false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ActivateAllCommand));
            }
        }

        [Command]
        public void StartStopCommand()
        {
            IsHedging = !IsHedging;
        }

        [Command]
        public void Clone()
        {
            try
            {
                GammaScalpingModuleView view = new();
                view.Show();
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

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.DeltaHedgingLayout);
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
            _chartUpdateTimer?.Stop();
            _updateTimer?.Stop();
            IsHedging = false;
            UnderlyingPositionModel?.Dispose();
            OrderTicket?.Dispose();
        }

        private void RunHedgeDeltaUpdate()
        {
            try
            {
                UnderlyingPositionModel?.Update(RoundDeltaForHedge);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RunHedgeDeltaUpdate));
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            RaisePropertyChanged(propertyName);
        }
    }
}
