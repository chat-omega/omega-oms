using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using DevExpress.Mvvm.DataAnnotations;
using System.Dynamic;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DominatorTraderModel : BindableBase, ITraderModel, IDisposable
    {
        internal readonly PortfolioManagerModel _portfolioManagerModel;
        internal readonly NotificationManager _notificationManager;
        internal readonly TransactionConsumerModel _transactionConsumer;
        internal readonly IAbstractFactory<RouteSelectionViewModel> _routeSelectionViewFactory;
        internal readonly IAbstractFactory<ThreeWayCloser> _threeWayCloserFactory;
        internal readonly IAbstractFactory<ComplexOrderTicketViewModel> _ticketFactory;
        public DominatorConfig DominatorConfig { get; set; }

        private readonly OmsCore _omsCore;
        private bool _isRunning = false;
        private long _interval = 50_000_000;

        private readonly List<DominatorItem> _dominatorItems = new();
        public IReadOnlyList<DominatorItem> DominatorItems => _dominatorItems;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public List<string> RoutesList { get; set; } = new();
        public bool IsRunning { get => _isRunning; set => SetValue(ref _isRunning, value); }
        public string Description => DominatorConfig.Title;
        public long Interval { get => _interval; set => SetValue(ref _interval, value); }
        public bool IsDisposed { get; set; }
        public bool Started { get; private set; } = false;
        public bool Selected { get; set; }
        public double AdjustedPnl { get; set; } = 0;
        public bool UseExcel { get; set; } = false;
        public Managers.Dominator DominatorExcelConnection { get; set; }
        private Timer FishTimer { get; set; }

        public DominatorTraderModel(IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
            IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
            IAbstractFactory<RouteSelectionViewModel> routeSelectionViewFactory,
            TransactionConsumerModel transactionConsumer,
            NotificationManager notificationManager,
            PortfolioManagerModel portfolioManagerModel,
            OmsCore omsCore,
            DominatorConfig dominatorConfig = null)
        {
            _omsCore = omsCore;
            _ticketFactory = ticketFactory;
            _threeWayCloserFactory = threeWayCloserFactory;
            _routeSelectionViewFactory = routeSelectionViewFactory;
            _transactionConsumer = transactionConsumer;
            _notificationManager = notificationManager;
            _portfolioManagerModel = portfolioManagerModel;
            DominatorConfig = dominatorConfig ?? new DominatorConfig(true);
        }

        private sealed class FishStatus
        {
            private int index = 0;
            public int UpdateStatus(IList<DominatorItem> dominatorItems)
            {
                for (int i = 0; i < 5; i++)
                {
                    dominatorItems[i].SubscribeToData("UNDERLYING");
                }
                dominatorItems.ElementAtOrDefault(index - 1)?.UnsubscribeData("UNDERLYING");
                return ++index;
            }
        }
        internal virtual void Start()
        {
            var fishState = new FishStatus();
            IsRunning = true;
            Started = true;
            FishTimer = new Timer(
                state: fishState,
                dueTime: new TimeSpan(0),
                period: new TimeSpan(_interval),
                callback: async (state) =>
                {
                    var status = state as FishStatus;
                    var item = _dominatorItems[status.UpdateStatus(_dominatorItems)];
                    if (UseExcel)
                    {
                        // Use connected Excel to calculate Edge and Filtering
                        DominatorExcelConnection.RequestFilterResult(item.Symbol);
                        item.ExcelEdgeReady = false;
                        DominatorExcelConnection.RequestEdgeCalculation(item.Symbol);
                        item.ExcelFilterReady = false;
                        await item.EdgeAndFilterCalculationComplete();
                    }
                    else
                    {
                        // Calculate Filter with OMS
                        if (item.CalculateFilter())
                        {
                            // Calculate Edge with OMS
                            item.Edge = item.CalculateEdge();
                        }
                    }
                    await item.SendOrderAndRegisterFillEvents();
                });
        }

        /*
        private void HandleDomAutoTraderCommand(Dominator dominator, string argumentsJson)
        {
            try
            {
                dynamic argument = Newtonsoft.Json.JsonConvert.DeserializeObject(argumentsJson);
                Guid orderId = argument.Id;
                string symbol = argument.Symbol;
                Side side = argument.Side;
                long price = argument.Price;
                long underPrice = argument.UnderPrice;
                Guid? cancelSendParent = argument.CancelSendParentId;

                // Prevent order and contra order from being sent together
                if (cancelSendParent is Guid domMainOrderId) 
                { // On dominator.OrderClose if fish failed for the main order try to fish the Dom contra
                    string id = domMainOrderId.ToString();
                    void SendContra(object sender, OrderUpdateValues update)
                    {
                        if ((id == update.LocalOrderId || id == update.ParentLocalOrderId)
                        && (update.OrderStatus == OrderStatus.Canceled || update.OrderStatus == OrderStatus.Rejected))
                        {
                            AutoTraderSender(dominator, orderId, symbol, side, price, underPrice, DomContraParentId: domMainOrderId);
                            dominator.OrderClose -= SendContra;
                        }
                    }
                    dominator.OrderClose += SendContra;
                }
                else // send Dom Main to AutoTrader
                { 
                    AutoTraderSender(dominator, orderId, symbol, side, price, underPrice, default);
                }
                _log.Info(argumentsJson);
            }
            catch
            {
                _log.Error("Failed to parse and send autotrader orders");
            }
        }
        private void AutoTraderSender(
            Dominator dominator, 
            Guid orderId,
            string symbol, 
            Side side, 
            long price, 
            long underPrice, 
            Guid DomContraParentId)
        {
            bool isDomContra = DomContraParentId != default;
            SendAutoTraderOrder(orderId, symbol, side, price, underPrice, isDomContra, OmsCore.OrderGatewayClient);
            _log.Info("DOM Autotrader Main: order sent");
        }
         */

        public void ProcessEdgeUpdate(string jsonBody)
        {
            dynamic domCommandArgs = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(jsonBody);
            string symbol = domCommandArgs.Symbol;
            double edge = domCommandArgs.DomEdgeCalc;
            string args = domCommandArgs.DomParams;
            _dominatorItems.Where(item => item.Symbol == symbol).ForEach(item =>
            {
                item.Edge = edge;
                item.ExcelEdgeReady = true;
            });
        }

        public void ProcessFilterUpdate(string jsonBody)
        {
            dynamic domCommandArgs = Newtonsoft.Json.JsonConvert.DeserializeObject<ExpandoObject>(jsonBody);
            string symbol = domCommandArgs.Symbol;
            bool approved = domCommandArgs.Filter;
            string args = domCommandArgs.DomParams;
            _dominatorItems.Where(item => item.Symbol == symbol).ForEach(item =>
            {
                item.FilterApproved = !approved;
                item.ExcelFilterReady = true;
            });
        }

        internal virtual void Stop()
        {

            IsRunning = false;
            FishTimer.Dispose();
        }

        internal void Refresh()
        {
        }

        private static async Task DisposeItems(IEnumerable<OrderTicket> items)
        {
            await Task.Run(() => items.AsParallel().ForAll(item =>
            {
                item.Dispose();
                _log.Info(nameof(DisposeItems) + " Disposing order model for " + item.SpreadId);
            }));
        }
        private Task UpdateRoutesListAsync()
        {
            try
            {
                var routeLookup = _omsCore.OrderClient?.RouteLookup;
                var routes = routeLookup?.GetRoutes() ?? Array.Empty<string>();

                RoutesList.Clear();
                RoutesList.Add("");
                foreach (string route in routes.OrderBy(x => x))
                {
                    RoutesList.Add(route);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateRoutesListAsync));
            }
            return Task.CompletedTask;
        }
        internal List<Task> LoadOptionsTasks = new();

        public async Task<DominatorItem[]> MakeDominatorItemsFromSymbols(Tuple<string, string>[] tuples, System.Windows.Threading.Dispatcher dispatcher, IProgress<int> progressReporter)
        {
            var loadOptionsTasks = new ConcurrentBag<Task>();
            int completed = 0;
            var items = await Task.Run(() => tuples.AsParallel().Select(tuple =>
            {
                DominatorItem model = new(this, DominatorConfig, _omsCore) { Dispatcher = dispatcher };
                LoadOptionsTasks.Add(model.LoadLegsFromTosAsync(tuple.Item1, tuple.Item2?.ToSide()).ContinueWith(task => progressReporter.Report(Interlocked.Increment(ref completed))));
                return model;
            }).ToArray());
            LoadOptionsTasks = loadOptionsTasks.ToList();
            return items;
        }

        private async Task<ConcurrentBag<DominatorItem>> ConvertSymbolsToDominatorItemsAsync(Spread[] spreads, System.Windows.Threading.Dispatcher dispatcher, IProgress<int> progressReporter)
        {
            ConcurrentBag<DominatorItem> dominatorItems = new();
            await Task.Run(() =>
            {
                int completed = 0;
                spreads.AsParallel().WithCancellation(cts.Token)
                .ForAll(spread =>
                {
                    DominatorItem model = new(this, DominatorConfig, _omsCore)
                    {
                        EdgeOverride = spread.EdgeOverride,
                        Dispatcher = dispatcher
                    };
                    dominatorItems.Add(model);
                    model.LoadLegsFromTosAsync(spread.Symbol, spread.Side?.ToSide()).Wait(cts.Token);

                    Interlocked.Increment(ref completed);
                    progressReporter.Report(completed);
                });
            }, cts.Token);
            return dominatorItems;
        }

        public async Task LoadFromSpreadResults(IEnumerable<SpreadGeneratorResults> results, System.Windows.Threading.Dispatcher dispatcher, IProgress<int> progress)
        {

            ConcurrentBag<DominatorItem> models = new();
            List<Task> tasks = new();
            Spread[] spreads = results.SelectMany(x => x.Spreads).ToArray();

            try
            {
                models = await ConvertSymbolsToDominatorItemsAsync(spreads, dispatcher, progress);
                if (cts.Token.IsCancellationRequested) return;
                await AddMultipleSpreadsAsync(models, true, dispatcher: dispatcher);

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFromSpreadResults));
            }

            if (IsDisposed)
            {
                await DisposeItems(models);
                return;
            }
        }

        internal async Task<DominatorItem[]> ReadAllSpreadsFromFile(string spreadsJson, System.Windows.Threading.Dispatcher dispatcher, IProgress<int> progress)
        {
            Tuple<string, string>[] spreadTuples = await Task.Run(() => Newtonsoft.Json.JsonConvert.DeserializeObject<Tuple<string, string>[]>(spreadsJson));
            return await MakeDominatorItemsFromSymbols(spreadTuples, dispatcher, progress);
        }

        internal async Task AddMultipleSpreadsAsync(IEnumerable<DominatorItem> items, bool updateIfExists = false, bool AvoidDuplicates = false, System.Windows.Threading.Dispatcher dispatcher = null)
        {
            ConcurrentBag<DominatorItem> toBeAdded = new();

            try
            {
                if (AvoidDuplicates)
                {
                    var uniqueItems = items.AsParallel().WithCancellation(cts.Token)
                        .GroupBy(item => (item.Description, item.Legs[0].Underlying))
                        .Select(g => g.First()).ToHashSet();

                    if (uniqueItems.Count < items.Count())
                    {
                        await DisposeItems(items.AsParallel().WithCancellation(cts.Token).Except(uniqueItems.AsParallel()));
                    }
                    items = uniqueItems;
                }

                await Task.Run(() => items.AsParallel().WithCancellation(cts.Token).ForAll(item =>
                {
                    toBeAdded.Add(item);
                    item.RegisterEvents(this);
                    //if (dispatcher is not null) item.Dispatcher = dispatcher;
                }), cts.Token);

                if (cts.Token.IsCancellationRequested) return;
                await Task.Run(
                    () => _dominatorItems.AddRange(toBeAdded),
                    cts.Token);
                await dispatcher.InvokeAsync(() => RaisePropertyChanged(nameof(DominatorItems)));

            }
            finally
            {
                if (IsDisposed)
                {
                    await DisposeItems(items);
                }
            }
        }

        public CancellationTokenSource cts = new();
        public async void Dispose()
        {
            FishTimer.Dispose();
            GC.SuppressFinalize(this);
            cts.Cancel();
            cts.Dispose();
            await DisposeItems(_dominatorItems);
        }

        public void OnTrade(OrderTicket order, IOmsOrder trade)
        { }

        public void OnOrderFilledEvent(OrderTicket changedOrder, OrderStatus orderStatus)
        { }

        public void OnOrderClosedEvent(IOmsOrder order, OrderStatus orderStatus, OrderTicket ticket)
        { }

        public IEnumerable<Tuple<string, string>> DominatorItemsSymbols => _dominatorItems.Select(item => new Tuple<string, string>(item.Symbol, (item.Side ?? Side.Buy).ToString()));


        bool _autotraderEnabled;
        public string AutoTraderConfigId { get; internal set; }
        public bool AutoTraderEnabled { get => _autotraderEnabled; set => SetValue(ref _autotraderEnabled, value); }

        [Command]
        private void ChangeRoute() { }

        [Command]
        private void RemoveHighDeltaSpreadsAndStart() { }
        [Command]
        private void EnableLeastDataOption() { }

        internal void AllowUniqueSubmissionsAsync()
        {
            throw new NotImplementedException();
        }

        internal void BlockUniqueSubmissionsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
