using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
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
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Formatting = Newtonsoft.Json.Formatting;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EmptyOrderBookViewModel : CustomizableTableViewModelBase, IOmsDataSubscriber
    {
        private const string MODULE_TITLE = "Custom Order Book";
        private const int UPDATE_LIMIT = 10_000;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsCore _omsCore;
        private bool _unsavedChanges;
        private Dictionary<string, List<OmsOrderModel>> _underlyingMap;

        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
        public IDialogService DialogService => GetService<IDialogService>();
        protected ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public IUiUpdateService UiUpdateService => GetService<IUiUpdateService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public List<string> SupportedFormats { get; set; } = new List<string> { "Dominator List" };
        public bool PortfolioAdjustmentModuleGranted => OmsCore.GatewayClient.GrantedModules.Contains((int)Module.PortfolioAdjustment);

        [Bindable(Default = true)]
        public partial bool ShowAllTransactionsTabs { get; set; }

        public string Uid { get; internal set; }
        public Dispatcher Dispatcher { get; set; }

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial bool CrateSeparateExportForUnderlyings { get; set; }
        [Bindable]
        public partial bool RandomizeExport { get; set; }
        [Bindable]
        public partial string ExportFormat { get; set; }
        [Bindable]
        public partial bool GeneratePerms { get; set; }
        [Bindable]
        public partial int GeneratePermsCount { get; set; }
        public string _ModuleTitle;
        [Bindable]
        public partial string ModuleTitle { get; set; }

        public bool _ShowFilters;
        [Bindable]
        public partial bool ShowFilters { get; set; }

        public bool _EnableDeltaAdjusting;
        [Bindable]
        public partial bool EnableDeltaAdjusting { get; set; }

        public FastObservableCollection<OmsOrderModel> _Orders;
        [Bindable]
        public partial FastObservableCollection<OmsOrderModel> Orders { get; set; }

        public FastObservableCollection<OmsOrderModel> _ClosedOrdersCollection;
        [Bindable]
        public partial FastObservableCollection<OmsOrderModel> ClosedOrdersCollection { get; set; }

        public FastObservableCollection<OmsOrderModel> _UniqueOrdersCollection;
        [Bindable]
        public partial FastObservableCollection<OmsOrderModel> UniqueOrdersCollection { get; set; }

        public FastObservableCollection<OmsOrderModel> _FilledOrdersCollection;
        [Bindable]
        public partial FastObservableCollection<OmsOrderModel> FilledOrdersCollection { get; set; }

        public FastObservableCollection<OmsOrderModel> _UniqueFillsCollection;
        [Bindable]
        public partial FastObservableCollection<OmsOrderModel> UniqueFillsCollection { get; set; }

        public FilterType _FilterType;
        [Bindable]
        public partial FilterType FilterType { get; set; }

        public string _FilePath;
        [Bindable]
        public partial string FilePath { get; set; }

        public string _FileName;
        public string FileName
        {
            get => _FileName;
            set
            {
                SetValue(ref _FileName, value);
                ModuleTitle = string.IsNullOrWhiteSpace(value) ? MODULE_TITLE : value + " - " + MODULE_TITLE;
            }
        }

        public bool _AutoSave;
        [Bindable]
        public partial bool AutoSave { get; set; }

        public bool _Loaded;
        private DelegateCommand<object> _searchInNewTradesModuleCommand;
        private readonly List<ContraPartyReportModel> _contraPartyReports = [];
        private object _contraPartyReportsLock = new();

        [Bindable]
        public partial bool Loaded { get; set; }

        public ConfigSave ConfigSave { get; set; }
        public bool IsDisposed { get; set; }
        public ICommand SearchInNewTradesModuleCommand
        {
            get
            {
                _searchInNewTradesModuleCommand ??= new DelegateCommand<object>(SearchInNewTradesModule);
                return _searchInNewTradesModuleCommand;
            }
        }


        public EmptyOrderBookViewModel(OmsCore omsCore, TransactionConsumerModel transactionConsumerModel)
        {
            _omsCore = omsCore;
            transactionConsumerModel.ContrapartyReportsAdded += OnContrapartyReportsAdded;

            ModuleTitle = MODULE_TITLE;

            ClosedOrdersCollection = new FastObservableCollection<OmsOrderModel>();
            UniqueOrdersCollection = new FastObservableCollection<OmsOrderModel>();
            FilledOrdersCollection = new FastObservableCollection<OmsOrderModel>();
            UniqueFillsCollection = new FastObservableCollection<OmsOrderModel>();

            ClosedOrdersCollection.CollectionChanged += (_, _) => CheckForSubscriptionCommand();
            ClosedOrdersCollection.CollectionChanged += Orders_CollectionChanged;
            UniqueOrdersCollection.CollectionChanged += Orders_CollectionChanged;
            FilledOrdersCollection.CollectionChanged += Orders_CollectionChanged;
            UniqueFillsCollection.CollectionChanged += Orders_CollectionChanged;

            Orders = ClosedOrdersCollection;
            FilterType = FilterType.ALL;

            FileName = "Custom Order Book";
            SetTitle();
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;

            ExportFormat = SupportedFormats.FirstOrDefault();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        public void SetTitle()
        {
            FileName = $"{OmsCore.User.Username} Order Book";
        }

        [Command]
        public void RequestSymbolEdgeMapCommand()
        {
            if (ClosedOrdersCollection.Count > 0)
            {
                IEnumerable<IGrouping<string, OmsOrderModel>> grouped = ClosedOrdersCollection.Where(x => !x.IsComplexOrder).GroupBy(x => x.SpreadId);
                if (grouped.Any())
                {
                    foreach (IGrouping<string, OmsOrderModel> group in grouped)
                    {
                        OmsOrderModel first = group.First();
                        Instrument inst = new(first.Symbol);
                        string symbol = inst.ToTOS();
                        OmsCore.HerculesClient.RequestSymbolEdgeMapAsync(symbol, DateTime.Today - TimeSpan.FromDays(7)).ContinueWith(t =>
                        {
                            if (t.Result != null && t.Result.Any())
                            {
                                IEnumerable<ZeroPlus.Models.Data.Edge.SymbolEdgeMap> edges = t.Result;
                                double sampleUnder = edges.OrderByDescending(x => x.Date).FirstOrDefault().BestBuyPriceUnderlying;

                                double bestBuyPrice = double.NaN;
                                double bestBuyPriceUnder = double.NaN;

                                double bestSellPrice = double.NaN;
                                double bestSellPriceUnder = double.NaN;

                                Side? openingSide = null;
                                Side? hardSide = null;

                                foreach (ZeroPlus.Models.Data.Edge.SymbolEdgeMap edge in edges)
                                {
                                    double adjBuy = ((sampleUnder - edge.BestBuyPriceUnderlying) * edge.BestBuyPriceDelta) + edge.BestBuyPrice;
                                    double adjSell = ((sampleUnder - edge.BestSellPriceUnderlying) * edge.BestSellPriceDelta) + edge.BestSellPrice;

                                    if ((double.IsNaN(bestBuyPrice) || adjBuy < bestBuyPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        bestBuyPrice = edge.BestBuyPrice;
                                        bestBuyPriceUnder = edge.BestBuyPriceUnderlying;
                                    }
                                    if ((double.IsNaN(bestSellPrice) || adjSell > bestSellPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                                    {
                                        bestSellPrice = edge.BestSellPrice;
                                        bestSellPriceUnder = edge.BestSellPriceUnderlying;
                                    }
                                    break;
                                }

                                if (edges.Select(x => x.OpeningSide).Distinct().Count() == 1)
                                {
                                    openingSide = edges.FirstOrDefault()?.OpeningSide;
                                }
                                if (edges.Select(x => x.HardSide).Distinct().Count() == 1)
                                {
                                    hardSide = edges.FirstOrDefault()?.HardSide;
                                }

                                foreach (OmsOrderModel item in group)
                                {
                                    item.BestBuyPrice = bestBuyPrice;
                                    item.BestBuyPriceUnderMid = bestBuyPriceUnder;
                                    item.BestSellPrice = bestSellPrice;
                                    item.BestSellPriceUnderMid = bestSellPriceUnder;
                                }
                            }
                        });
                    }
                    EnableDeltaAdjusting = true;
                    CheckForSubscriptionCommand();
                }
            }
        }

        [Command]
        public void CheckForSubscriptionCommand()
        {
            if (ClosedOrdersCollection.Count > 0)
            {
                if (EnableDeltaAdjusting)
                {
                    _underlyingMap = ClosedOrdersCollection.Where(x => x.CumulativeQuantity > 0).GroupBy(x => x.UnderlyingSymbol).ToDictionary(x => x.Key, x => x.ToList());
                    foreach (string underlying in _underlyingMap.Keys)
                    {
                        if (!string.IsNullOrWhiteSpace(underlying))
                        {
                            OmsCore.QuoteClient.Subscribe(underlying, SubscriptionFieldType.MidPoint, this);
                        }
                    }
                }
                else
                {
                    OmsCore.QuoteClient.UnsubscribeAllAsync(this);
                    foreach (OmsOrderModel item in ClosedOrdersCollection)
                    {
                        item.ResetDeltaAdjustPrices();
                    }
                }
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

                            if (parameter.ContainsTime)
                            {
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

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                string symbol = key.Symbol;
                SubscriptionFieldType type = key.Type;
                if (type == SubscriptionFieldType.MidPoint &&
                    !string.IsNullOrWhiteSpace(symbol) &&
                    value is double midUpdate &&
                    _underlyingMap.TryGetValue(symbol, out List<OmsOrderModel> list))
                {
                    foreach (OmsOrderModel item in list)
                    {
                        item.DeltaAdjustPrices(midUpdate);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        [Command]
        public void Clone()
        {
            try
            {
                EmptyOrderBookView customWindow = new();
                EmptyOrderBookViewModel viewModel = (EmptyOrderBookViewModel)customWindow.DataContext;
                customWindow.Loaded += (s, e) => _ = viewModel.LoadConfigFromJsonAsync(OrdersAsJson());
                customWindow.Show();
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

                viewModel.Module = Module.CustomOrderBook;

                viewModel.Config = OrdersAsJson();

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
                viewModel.LoadGroups(Module.CustomOrderBook);

                viewModel.Config = OrdersAsJson();

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
        public void BrowseConfigs()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.CustomOrderBook);
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
                    viewModel.SetModule(Module.CustomOrderBookLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void Closing()
        {
            OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
            OmsCore.QuoteClient.UnsubscribeAllAsync(this);
            if (_unsavedChanges)
            {
                bool result = false;

                Application.Current.Dispatcher.Invoke(new Action(() => result = MessageBoxService.ShowMessage($"Do you want to save changes to your order book?", "ZeroPlus OMS", MessageButton.YesNo) == MessageResult.Yes));

                if (result)
                {
                    SaveOrderBook();
                }
            }
        }

        [Command]
        public void Clear()
        {
            ClosedOrdersCollection.Clear();
            UniqueOrdersCollection.Clear();
            FilledOrdersCollection.Clear();
            UniqueFillsCollection.Clear();
            OmsCore.QuoteClient.UnsubscribeAllAsync(this);
        }

        [Command]
        public async Task Clean()
        {
            HashSet<string> loadedSpreads = new();
            HashSet<OmsOrderModel> itemsToRemove = new();

            foreach (OmsOrderModel item in ClosedOrdersCollection)
            {
                if (loadedSpreads.Contains(item.SpreadId))
                {
                    itemsToRemove.Add(item);
                }
                else
                {
                    loadedSpreads.Add(item.SpreadId);
                }
            }

            foreach (OmsOrderModel item in itemsToRemove)
            {
                ClosedOrdersCollection.Remove(item);
            }

            bool undo = false;
            await Application.Current.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (itemsToRemove.Count > 0)
                {
                    undo = MessageBoxService?.Show($"{itemsToRemove.Count} items removed.\nTo Undo this change click on Cancel",
                                                     "Confirm",
                                                     MessageButton.OKCancel,
                                                     MessageIcon.Exclamation,
                                                     MessageResult.OK) == MessageResult.Cancel;
                }
                else
                {
                    MessageBoxService?.ShowMessage("No item removed.",
                                                   "ZeroPlus OMS",
                                                   MessageButton.OK,
                                                   MessageIcon.Information);
                }
            }));
            bool blocked = false;
            try
            {
                if (itemsToRemove.Count > UPDATE_LIMIT)
                {
                    UiUpdateService.BeginUpdate();
                    blocked = true;
                }
                if (undo)
                {
                    ClosedOrdersCollection.AddRange(itemsToRemove.ToList());
                }
                else
                {
                    foreach (OmsOrderModel item in itemsToRemove)
                    {
                        UniqueOrdersCollection.Remove(item);
                        FilledOrdersCollection.Remove(item);
                        UniqueFillsCollection.Remove(item);
                    }
                }
            }
            finally
            {
                if (blocked)
                {
                    UiUpdateService.EndUpdate();
                }
            }
        }

        [Command]
        public void SaveOrderBook()
        {
            try
            {
                if (Loaded)
                {
                    WriteOrderBookToFile();
                }
                else
                {
                    SaveAsOrderBook();
                }
                _unsavedChanges = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveOrderBook));
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public void SaveAsOrderBook()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "json";
                SaveFileDialogService.DefaultFileName = $"{OmsCore.User.Username} Order Book - {DateTime.Now:MM-dd-yyyy hh.mm}";
                SaveFileDialogService.Filter = "Json|*.json";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    FileName = SaveFileDialogService.SafeFileName();
                    FilePath = SaveFileDialogService.GetFullFileName();
                    WriteOrderBookToFile();
                }
                _unsavedChanges = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAsOrderBook));

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
        }

        [Command]
        public void LoadOrderBook()
        {
            try
            {
                ShowFilters = false;
                Orders = ClosedOrdersCollection;
                OpenFileDialogService.Filter = "Json|*.json";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    FileName = file.Name;
                    FilePath = file.GetFullName();
                    _ = LoadFromFileAsync();
                }
                _unsavedChanges = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadOrderBook));
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
        }

        [Command]
        public void LoadOrderBookFromArchive()
        {
            try
            {
                ShowFilters = true;
                lock (_contraPartyReportsLock)
                    _contraPartyReports.Clear();
                ArchiveRequestView view = new();
                ArchiveRequestViewModel viewModel = (ArchiveRequestViewModel)view.DataContext;
                viewModel.UiUpdateService = UiUpdateService;
                viewModel.ClosedOrdersCollection = ClosedOrdersCollection;
                viewModel.UniqueOrdersCollection = UniqueOrdersCollection;
                viewModel.FilledOrdersCollection = FilledOrdersCollection;
                viewModel.UniqueFillsCollection = UniqueFillsCollection;
                viewModel.Loaded += () =>
                {
                    ShowAllTransactionsTabs = !viewModel.FillsOnly;
                    if (viewModel.FillsOnly)
                    {
                        Orders = FilledOrdersCollection;
                        FilterType = FilterType.FILLED;
                    }
                    else
                    {
                        Orders = ClosedOrdersCollection;
                        FilterType = FilterType.ALL;
                    }
                    LoadContraPartyReport();
                };
                view.Show();
                _unsavedChanges = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadOrderBookFromArchive));
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)));
            }
        }

        private void OnContrapartyReportsAdded(DateTime targetDate, List<ContraPartyReportModel> reports)
        {
            lock (_contraPartyReportsLock)
            {
                _contraPartyReports.AddRange(reports);

                _log.Info("Received {count} contraparty reports for {targetDate}. {} total reports loaded", reports.Count, targetDate.Date, _contraPartyReports.Count);
                LoadContraPartyReport();
            }
        }

        private void LoadContraPartyReport()
        {
            var collection = ClosedOrdersCollection.Count > 0 ? ClosedOrdersCollection : FilledOrdersCollection;

            if (collection.Count == 0 || _contraPartyReports == null || _contraPartyReports.Count == 0)
            {
                return;
            }

            Dictionary<string, ContraPartyReportModel> reportByClOrdId = [];
            foreach (var report in _contraPartyReports)
            {
                reportByClOrdId[report.ClOrdID] = report;
            }

            for (var i = 0; i < collection.Count; i++)
            {
                var order = collection[i];
                if (reportByClOrdId.TryGetValue(order.PermID, out var report) && report != null)
                {
                    order.AddContraPartyReport(report);
                }
            }

        }

        [Command]
        public void ChangeFilter(FilterType filterType)
        {
            FilterType = filterType;
            switch (filterType)
            {
                case FilterType.ALL:
                    Orders = ClosedOrdersCollection;
                    break;
                case FilterType.UNIQUE_ORDERS:
                    Orders = UniqueOrdersCollection;
                    break;
                case FilterType.FILLED:
                    Orders = FilledOrdersCollection;
                    break;
                case FilterType.UNIQUE:
                    Orders = UniqueFillsCollection;
                    break;
            }
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
                _log.Error(ex, nameof(OpenInComplexOrderTicket));
            }
        }

        [Command]
        public void OpenOrderDetailsCommand(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is OmsOrderModel orderModel)
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
        public void RowDoubleClick(RowClickArgs args)
        {
            try
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
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RowDoubleClick));
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
                    parameter is OmsOrderModel orderModel)
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
                        window.Loaded += (s, e) => _ = viewModel.LoadFromOrderBookAsync(orderModel);
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
        public void RemoveOrder(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is OmsOrderModel orderModel)
                {
                    ClosedOrdersCollection.Remove(orderModel);
                    UniqueOrdersCollection.Remove(orderModel);
                    FilledOrdersCollection.Remove(orderModel);
                    UniqueFillsCollection.Remove(orderModel);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveOrder));
            }
        }

        [Command]
        public void RemoveSelected(object parameter)
        {
            try
            {
                if (parameter == null)
                {
                    return;
                }

                if (parameter is IEnumerable<object> ordersSelected)
                {
                    foreach (object order in ordersSelected.ToList())
                    {
                        RemoveOrder((OmsOrderModel)order);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveSelected));
            }
        }

        [Command]
        public void CustomUnboundColumnData(UnboundColumnRowArgs args)
        {
            OmsOrderModel row = (OmsOrderModel)args.Item;
            if (args.IsGetData)
            {
                if (row.UnboundDataColumnToValueMap.TryGetValue(args.FieldName, out string value))
                {
                    args.Value = value;
                }
            }
            else if (args.IsSetData)
            {
                row.UnboundDataColumnToValueMap[args.FieldName] = (string)args.Value;
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
        public void ExportCommand()
        {
            ExportSpreadsToFileView view = new ExportSpreadsToFileView
            {
                DataContext = this,
                GeneratePermsCheckBox =
                {
                    Visibility = Visibility.Visible
                },
                GeneratePermsSpinEdit =
                {
                    Visibility = Visibility.Visible
                }
            };
            view.ShowDialog();
        }

        [Command]
        public async void WriteToFileCommand()
        {
            var items = GetItemsByVisualOrderService.GetVisibleItems<OmsOrderModel>();
            if (items != null)
            {
                List<SymbolCodec> symbolCodecs = items.Select(spread => new SymbolCodec(spread.Symbol)).ToList();
                if (GeneratePerms && GeneratePermsCount > 0)
                {
                    foreach (SymbolCodec symbolCodec in symbolCodecs.ToList())
                    {
                        var prevSymbolCodecUp = symbolCodec;
                        var prevSymbolCodecDown = symbolCodec;

                        for (int j = 0; j < GeneratePermsCount; j++)
                        {
                            SymbolCodec strikeUp = new SymbolCodec();
                            for (int i = 0; i < prevSymbolCodecUp.LegCount; i++)
                            {
                                try
                                {
                                    Instrument leg = prevSymbolCodecUp.GetLeg(i);
                                    Option option = OptionsHelper.GetOptionFromSymbol(leg.symbol);

                                    if (option.Expiration.Date < DateTime.Today)
                                    {
                                        break;
                                    }

                                    var nextStrikeOption = await OmsCore.QuoteClient.GetNextStrikeOption(option, PermutationDirection.Up);
                                    Instrument strikeUpOption = new Instrument(nextStrikeOption.OptionSymbol)
                                    {
                                        buySell = leg.buySell,
                                        ratio = leg.ratio
                                    };

                                    strikeUp.AddLeg(strikeUpOption);
                                    symbolCodecs.Add(strikeUp);
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex, nameof(WriteToFileCommand));
                                    break;
                                }
                            }
                            prevSymbolCodecUp = strikeUp;

                            SymbolCodec strikeDown = new SymbolCodec();
                            for (int i = 0; i < prevSymbolCodecDown.LegCount; i++)
                            {
                                try
                                {
                                    Instrument leg = prevSymbolCodecDown.GetLeg(i);
                                    Option option = OptionsHelper.GetOptionFromSymbol(leg.symbol);

                                    if (option.Expiration.Date < DateTime.Today)
                                    {
                                        break;
                                    }

                                    var nextStrikeOption = await OmsCore.QuoteClient.GetNextStrikeOption(option, PermutationDirection.Down);
                                    Instrument strikeDownOption = new Instrument(nextStrikeOption.OptionSymbol)
                                    {
                                        buySell = leg.buySell,
                                        ratio = leg.ratio
                                    };

                                    strikeDown.AddLeg(strikeDownOption);
                                    symbolCodecs.Add(strikeDown);
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex, nameof(WriteToFileCommand));
                                    break;
                                }
                            }
                            prevSymbolCodecDown = strikeDown;
                        }
                    }
                }

                symbolCodecs = symbolCodecs.DistinctBy(x => x.ToTOS()).ToList();

                ISaveFileDialogService saveFileDialogService = SaveFileDialogService;
                saveFileDialogService.DefaultExt = "xlsx";
                saveFileDialogService.DefaultFileName = $"{"DOMINATOR SPREADS "} - {DateTime.Now:MM-dd-yyyy hh.mm} - {symbolCodecs.Count} spreads";
                saveFileDialogService.Filter = "Dominator List|*.XLSX";
                bool save = saveFileDialogService.ShowDialog();

                if (save)
                {
                    string filePath = saveFileDialogService.GetFullFileName();
                    Task.Run(() => ExportHelper.WriteSpreadsToFileUsingDominatorFormat(OmsCore.User.Username, filePath, symbolCodecs, null, RandomizeExport));
                }
            }
        }

        [Command]
        public void AddToTodaysOrderbook()
        {
            try
            {
                var orders = Orders.Where(o => o.AddToTodaysOrderbook);

                // Ensure this flag is switched
                foreach (var order in orders)
                    order.RemoveFromTodaysOrderbook = false;

                List<string> permIds = [.. orders.Select(o => o.PermID)];
                if (permIds.Count != 0)
                    _omsCore.HerculesClient.AddRemoveMultipleTrades(add: true, permIds);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddToTodaysOrderbook));
            }
        }

        private async Task LoadFromFileAsync()
        {
            bool blocked = false;
            try
            {
                if (FilePath != null)
                {
                    Loaded = true;
                    ClosedOrdersCollection.Clear();
                    List<OmsOrderModel> orderModels = new();

                    await Task.Run(() =>
                    {
                        string fileContent = File.ReadAllText(FilePath);
                        List<OmsOrder> orders = JsonConvert.DeserializeObject<List<OmsOrder>>(fileContent);

                        foreach (OmsOrder order in orders)
                        {
                            OmsOrderModel orderModel = new();
                            orderModel.Update(order);
                            orderModels.Add(orderModel);
                        }
                    });
                    if (orderModels.Count > UPDATE_LIMIT)
                    {
                        UiUpdateService.BeginUpdate();
                        blocked = true;
                    }
                    ClosedOrdersCollection.AddRange(orderModels);
                }
            }
            finally
            {
                if (blocked)
                {
                    UiUpdateService.EndUpdate();
                }
            }
        }

        private void WriteOrderBookToFile()
        {
            Loaded = true;
            string jsonString = OrdersAsJson();
            File.WriteAllText(FilePath, jsonString);
            _unsavedChanges = false;
        }

        private string OrdersAsJson()
        {
            List<OmsOrder> orders = ClosedOrdersCollection.Select(x => x.ToOrder()).ToList();

            string jsonString = JsonConvert.SerializeObject(orders);
            return jsonString;
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            ClosedOrdersCollection.Clear();
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return;
            }
            List<OmsOrder> orders = await Task.Run(() => JsonConvert.DeserializeObject<List<OmsOrder>>(configJson));

            foreach (OmsOrder order in orders)
            {
                OmsOrderModel orderModel = new();
                orderModel.Update(order);
                ClosedOrdersCollection.AddItem(orderModel);
            }
        }

        private void Orders_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _unsavedChanges = true;

            if (AutoSave && Loaded)
            {
                WriteOrderBookToFile();
            }
        }

        internal async Task LoadViewModelConfigAsync(string uid)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(EmptyOrderBookConfig)}.xml");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = File.ReadAllText(configExportPath);
                    EmptyOrderBookConfig config = await Task.Run(() => JsonConvert.DeserializeObject<EmptyOrderBookConfig>(myFileStream));

                    FileName = config.FileName;
                    FilePath = config.FilePath;
                    EnableDeltaAdjusting = config.EnableDeltaAdjusting;

                    await LoadFromFileAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadViewModelConfigAsync));
                _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        public void SaveViewModelConfig()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePath))
                {
                    if (ClosedOrdersCollection.Count == 0)
                    {
                        return;
                    }
                    else
                    {
                        FileName = $"{OmsCore.User.Username} Order Book - {DateTime.Now:MM-dd-yyyy hh.mm} {new Random().NextDouble()}";
                        FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FileName + ".json");
                    }
                }
                ValidatePath();
                EmptyOrderBookConfig config = new()
                {
                    FilePath = FilePath,
                    FileName = FileName,
                    EnableDeltaAdjusting = EnableDeltaAdjusting,
                };
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(EmptyOrderBookConfig)}.xml");
                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private void ValidatePath()
        {
            string path = Path.GetDirectoryName(FilePath);

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception)
                {
                    FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FileName + ".json");
                }
            }
        }
    }
}
