using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class OptionChainViewModel : CustomizableTableViewModelBase
    {
        private readonly IModuleFactory _moduleFactory;
        private static readonly string MODULE_TITLE = "Option Chain";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private List<Option> _optionChain;
        private DelegateCommand<object> _loadStrategyCommand;
        private DispatcherTimer _uiUpdateTimer;

        internal ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        internal IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public string Uid { get; internal set; }
        public Dispatcher Dispatcher { get; set; }
        public List<string> GroupingOptions { get; } = new List<string> { "None", "Expiration", "Strike" };
        private INotifyCollectionChanged _visibleItems;
        private string _Underlying;

        private PortfolioManagerModel PortfolioManagerModel { get; }

        public INotifyCollectionChanged VisibleItems
        {
            get => _visibleItems;
            set
            {
                if (_visibleItems != null)
                {
                    _visibleItems.CollectionChanged -= OnVisibleItemsCollectionChanged;
                }

                _visibleItems = value;
                if (_visibleItems != null)
                {
                    _visibleItems.CollectionChanged += OnVisibleItemsCollectionChanged;
                }
            }
        }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        public string Underlying
        {
            get => _Underlying;
            set => SetValue(ref _Underlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial string SelectedAccount { get; set; }

        [Bindable]
        public partial ObservableCollection<string> AccountsList { get; set; }

        [Bindable]
        public partial ObservableCollection<QuoteSubscriberModel> UnderlyingInformation { get; set; }

        [Bindable]
        public partial QuoteSubscriberModel UnderlyingQuoteSubscriber { get; set; }

        [Bindable]
        public partial ObservableCollection<OptionChainItemModel> Options { get; set; }

        [Bindable]
        public partial string VisibleStrikes { get; set; }

        [Bindable]
        public partial int BestPriceLookback { get; set; }

        [Bindable]
        public partial double Last { get; set; }

        [Bindable]
        public partial double NetChange { get; set; }

        [Bindable]
        public partial double PercentChange { get; set; }

        public ObservableCollection<string> VisibleStrikesList => new()
        {
            "5",
            "7",
            "10",
            "15",
            "20",
            "25",
            "30",
            "35",
            "40",
            "45",
            "50",
            "60",
            "70",
            "80",
            "90",
            "100",
            "125",
            "150",
            "175",
            "200",
            "ALL",
        };

        public OptionChainViewModel(PortfolioManagerModel portfolioManager, IModuleFactory moduleFactory)
        {
            _moduleFactory = moduleFactory;
            PortfolioManagerModel = portfolioManager;
            ModuleTitle = MODULE_TITLE;
            VisibleStrikes = VisibleStrikesList.LastOrDefault();
            AccountsList = new ObservableCollection<string>();
            Options = new ObservableCollection<OptionChainItemModel>();
            UnderlyingInformation = new ObservableCollection<QuoteSubscriberModel>();
            UnderlyingQuoteSubscriber = new QuoteSubscriberModel();
            UnderlyingInformation.Add(UnderlyingQuoteSubscriber);
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            StartUiUpdateTimer();
        }

        [Command]
        public void Clone()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.LockTrader))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        OptionChainView window = new();
                        OptionChainViewModel viewModel = (OptionChainViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };
                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => viewModel.LoadFromConfig(GetConfig());

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
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();

                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (sender, args) =>
                {
                    viewModel.SetModule(Module.OptionChainLayout);
                };

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public async Task LoadOptionChain()
        {
            try
            {
                Clear();
                ObservableCollection<OptionChainItemModel> models = await Task.Run(() => GetModels());
                if (models.Count > 0)
                {
                    Dispatcher?.BeginInvoke(() => Options = models);
                    _uiUpdateTimer?.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadOptionChain));
            }
        }

        private void OnVisibleItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateSubscription();
        }

        public void UpdateSubscription()
        {
            try
            {
                HashSet<OptionChainItemModel> visible = GetItemsByVisualOrderService.GetVisibleItems<OptionChainItemModel>();
                if (visible != null && visible.Count > 0)
                {
                    foreach (OptionChainItemModel model in Options)
                    {
                        if (visible.Contains(model))
                        {
                            model.SubscribeDataAsync(BestPriceLookback);
                        }
                        else
                        {
                            model.UnsubscribeDataAsync();
                        }
                    }
                }
                else
                {
                    foreach (OptionChainItemModel model in Options)
                    {
                        model.UnsubscribeDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnVisibleItemsCollectionChanged));
            }
        }

        private async Task<ObservableCollection<OptionChainItemModel>> GetModels()
        {
            ObservableCollection<OptionChainItemModel> models = new();
            Task<List<Option>> getOptionsTask = OmsCore.QuoteClient.GetSymbolsAsync(Underlying);

            List<Option> options = await getOptionsTask;
            if (options.Count > 0)
            {
                _ = InitOptionChainAsync();

                _optionChain = options.ToList();
                IEnumerable<List<Option>> optionsGroup = _optionChain.GroupBy(x => (x.RootSymbol, x.Expiration)).Select(g => g.ToList());
                bool useAllStrikes = int.TryParse(VisibleStrikes, out int result);
                if (useAllStrikes)
                {
                    foreach (List<Option> option in optionsGroup)
                    {
                        IEnumerable<IGrouping<double, Option>> byStrike = option.OrderByDescending(x => x.Strike).GroupBy(x => x.Strike).Select(g => g).Take(result);
                        foreach (IGrouping<double, Option> strikes in byStrike)
                        {
                            OptionChainItemModel item = new(strikes, PortfolioManagerModel);
                            models.Add(item);
                        }
                    }
                }
                else
                {
                    foreach (List<Option> option in optionsGroup)
                    {
                        IEnumerable<IGrouping<double, Option>> byStrike = option.OrderBy(x => x.Strike).GroupBy(x => x.Strike).Select(g => g);
                        foreach (IGrouping<double, Option> strikes in byStrike)
                        {
                            OptionChainItemModel item = new(strikes, PortfolioManagerModel);
                            models.Add(item);
                        }
                    }
                }
            }

            return models;
        }

        private void StartUiUpdateTimer()
        {
            _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(OmsCore.Config.BasketUiUpdateInterval),
            };
            _uiUpdateTimer.Tick += (_, _) => UpdateUiProperties();
        }

        private void UpdateUiProperties()
        {
            try
            {
                double last = UnderlyingQuoteSubscriber.Last;
                foreach (OptionChainItemModel option in Options)
                {
                    option.UpdateChanges(last);
                }
                UnderlyingQuoteSubscriber.UpdateChanges();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateUiProperties));
            }
        }

        [Command]
        public void CustomGroupDisplayText(GroupDisplayTextArgs args)
        {
            if (args.FieldName == "Expiration")
            {
                DateTime dateTime = Convert.ToDateTime(args.Value);
                int totalDays = (int)(dateTime - DateTime.Today).TotalDays;
                OptionChainItemModel item = (OptionChainItemModel)args.Item;
                args.DisplayText = $"{dateTime:MMM-dd-yy} ({totalDays}) ({item.CallOption.RootSymbol})";
            }
        }

        public ICommand LoadStrategyCommand
        {
            get
            {
                _loadStrategyCommand ??= new DelegateCommand<object>(LoadStrategyAsync);

                return _loadStrategyCommand;
            }
        }


        public void Dispose()
        {
            _uiUpdateTimer?.Stop();
            OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
            Task.Run(() =>
            {
                UnderlyingQuoteSubscriber.Dispose();
                foreach (OptionChainItemModel option in Options)
                {
                    option.Dispose();
                }
            });
        }

        public void Clear()
        {
            _uiUpdateTimer?.Stop();
            foreach (OptionChainItemModel option in Options)
            {
                option.Dispose();
            }
            Dispatcher?.BeginInvoke(() =>
            {
                Options.Clear();
            });
        }

        internal Dictionary<string, bool> GetValidStrategies(Option option)
        {
            Dictionary<string, bool> validStrategies = new()
            {
                {"Vertical", IsStrategyValid("Vertical", option) },
                {"Back/Ratio", IsStrategyValid("Back/Ratio", option)},
                {"Diagonal", IsStrategyValid("Diagonal", option)},
                {"Calendar", IsStrategyValid("Calendar", option)},
                {"Straddle", IsStrategyValid("Straddle", option)},
                {"Strangle", IsStrategyValid("Strangle", option)},
                {"Covered Stock", IsStrategyValid("Covered Stock", option)},
                {"Butterfly", IsStrategyValid("Butterfly", option)},
                {"Condor", IsStrategyValid("Condor", option)},
                {"Iron Condor", IsStrategyValid("Iron Condor", option)},
                {"Reversal", IsStrategyValid("Reversal", option)},
                {"Conversion", IsStrategyValid("Conversion", option)},
            };

            return validStrategies;
        }

        private bool IsStrategyValid(string strategy, Option option)
        {
            try
            {
                if (_optionChain == null)
                {
                    return false;
                }

                int ComplexStrategyMinStrikeWidth = 0;
                switch (strategy)
                {
                    case "Back/Ratio":
                        return option.Type == OptionType.CALL ? _optionChain.FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null : _optionChain.OrderByDescending<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null;
                    case "Butterfly":
                        IEnumerable<Option> collection = _optionChain.OrderBy<Option, double>(x => x.Strike).Where<Option>(x => x.Type == option.Type && x.Expiration == option.Expiration);
                        return collection.Any<Option>(x => x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && collection.FirstOrDefault<Option>(y => y.Strike < option.Strike && Math.Abs(y.Strike - option.Strike) == Math.Abs(x.Strike - option.Strike)) != null);
                    case "Calendar":
                        return _optionChain.FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike == option.Strike && (x.Expiration - option.Expiration).TotalDays >= 1.0) != null;
                    case "Condor":
                        if (_optionChain.OrderByDescending<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) == null)
                        {
                            return false;
                        }

                        Option condor3 = _optionChain.OrderBy<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration);
                        return condor3 != null && _optionChain.OrderBy<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike > condor3.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null;
                    case "Diagonal":
                        return option.Type == OptionType.CALL ? _optionChain.FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && (x.Expiration - option.Expiration).TotalDays >= 1.0) != null : _optionChain.OrderByDescending<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && (x.Expiration - option.Expiration).TotalDays >= 1.0) != null;
                    case "Iron Condor":
                        return option.Type == OptionType.CALL ? _optionChain.OrderBy<Option, double>(x => x.Strike).Any<Option>(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) && _optionChain.OrderByDescending<Option, double>(x => x.Strike).Count<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) >= 2 : _optionChain.OrderByDescending<Option, double>(x => x.Strike).Any<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) && _optionChain.Count<Option>(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) >= 2;
                    case "Straddle":
                        return _optionChain.FirstOrDefault<Option>(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike == option.Strike && x.Expiration == option.Expiration) != null;
                    case "Strangle":
                        return option.Type == OptionType.CALL ? _optionChain.OrderByDescending<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null : _optionChain.FirstOrDefault<Option>(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null;
                    case "Vertical":
                        return option.Type == OptionType.CALL ? _optionChain.FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike > option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null : _optionChain.OrderByDescending<Option, double>(x => x.Strike).FirstOrDefault<Option>(x => x.Type == option.Type && x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) >= ComplexStrategyMinStrikeWidth && x.Expiration == option.Expiration) != null;
                    case "Covered Stock":
                    //return option.Class.Settlement == null ? this.Underlying is Stock : false;
                    case "Conversion":
                    //return option.Underlying.SecurityType == SecurityType.Stock;
                    case "Reversal":
                    //return option.Underlying.SecurityType == SecurityType.Stock;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(IsStrategyValid)}, Strategy: {strategy}");
                return false;
            }
        }

        private async Task InitOptionChainAsync()
        {
            UnderlyingQuoteSubscriber.SetUnderlying(Underlying);
            List<Comms.Models.Data.ZPAccount> accounts = await OmsCore.OrderClient.AccountsLookup.GetAccountsAsync(AccountsLookup.AccountsType.All);
            if (accounts != null)
            {
                ObservableCollection<string> uniqueAccounts = accounts.Select(x => x.Acronym).Distinct().ToObservableCollection();
                await Dispatcher?.BeginInvoke(new Action(() => AccountsList = uniqueAccounts));
                SelectedAccount = !string.IsNullOrWhiteSpace(OmsCore.Config.DefaultAccount) ? OmsCore.Config.DefaultAccount : AccountsList.FirstOrDefault();
            }
        }

        private async void LoadStrategyAsync(object parameter)
        {
            if (parameter is not Tuple<string, string, Option> input)
            {
                return;
            }

            string strategy = input.Item1;
            string side = input.Item2;
            Option option = input.Item3;

            List<TicketLegModel> legs = await Task.Run(() => LoadStrategy(strategy, side, option));


            if (strategy == "BasketTrader")
            {
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
                            viewModel.LoadFromLegsAsync(legs);
                        }
                    }
                }
            }
            else
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        if (legs.Count <= 1 && OmsCore.Config.UseOrderTicketForSingleLegOrders)
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
                        window.Loaded += (s, e) => _ = viewModel.LoadFromLegsAsync(legs);
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
        }

        private List<TicketLegModel> LoadStrategy(string strategy, string side, Option option)
        {
            List<TicketLegModel> legs = new();
            switch (strategy)
            {
                case "Back/Ratio":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    if (option.Type == OptionType.CALL)
                    {
                        Option ratioLeg2C = _optionChain.FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = ratioLeg2C?.OptionSymbol,
                            Type = ratioLeg2C?.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                            Ratio = 2,
                            Quantity = 2,
                        });
                        break;
                    }

                    Option ratioLeg2P = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike < option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = ratioLeg2P?.OptionSymbol,
                        Type = ratioLeg2P?.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        Ratio = 2,
                        Quantity = 2,
                    });
                    break;
                case "Butterfly":
                    IEnumerable<Option> collection = _optionChain.OrderBy(x => x.Strike).Where<Option>(x => x.Type == option.Type && x.Expiration == option.Expiration);
                    Option bflyLeg3 = collection.FirstOrDefault(x => x.Strike > option.Strike && collection.FirstOrDefault(y => y.Strike < option.Strike && Math.Abs(y.Strike - option.Strike) == Math.Abs(x.Strike - option.Strike)) != null);
                    Option bflayLeg1 = collection.LastOrDefault<Option>(x => x.Strike < option.Strike && Math.Abs(x.Strike - option.Strike) == Math.Abs(bflyLeg3.Strike - option.Strike));
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = bflayLeg1.OptionSymbol,
                        Type = bflayLeg1.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                        Ratio = 2,
                        Quantity = 2,
                    });
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = bflyLeg3.OptionSymbol,
                        Type = bflyLeg3.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Calendar":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option calLeg2 = _optionChain.FirstOrDefault(x => x.Type == option.Type && x.Strike == option.Strike && (x.Expiration - option.Expiration).TotalDays >= 1.0);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = calLeg2.OptionSymbol,
                        Type = calLeg2.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Condor":
                    Option condorLeg1 = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike < option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = condorLeg1.OptionSymbol,
                        Type = condorLeg1.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option condorLeg3 = _optionChain.OrderBy(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = condorLeg3.OptionSymbol,
                        Type = condorLeg3.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option condorLeg4 = _optionChain.OrderBy(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike > legs[legs.Count - 1].Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = condorLeg4.OptionSymbol,
                        Type = condorLeg4.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Conversion":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = Underlying,
                        Side = ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option conversionLeg2 = _optionChain.FirstOrDefault(x => x.Type == OptionType.CALL && x.Strike == option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = conversionLeg2.OptionSymbol,
                        Type = conversionLeg2.Type.ToString(),
                        Side = ZeroPlus.Models.Data.Enums.Side.Sell
                    });
                    Option conversionLeg3 = _optionChain.FirstOrDefault(x => x.Type == OptionType.PUT && x.Strike == option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = conversionLeg3.OptionSymbol,
                        Type = conversionLeg3.Type.ToString(),
                        Side = ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    break;
                case "Covered Stock":
                case "Delta Neutral":
                    if (option.Type == OptionType.CALL)
                    {
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = Underlying,
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        });
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = option.OptionSymbol,
                            Type = option.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                        break;
                    }
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = Underlying,
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Diagonal":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    if (option.Type == OptionType.CALL)
                    {
                        Option diagonalLeg2C = _optionChain.FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike && (x.Expiration - option.Expiration).TotalDays >= 1.0);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = diagonalLeg2C.OptionSymbol,
                            Type = diagonalLeg2C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                        break;
                    }

                    Option diagonalLeg2P = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike < option.Strike && (x.Expiration - option.Expiration).TotalDays >= 1.0);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = diagonalLeg2P.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    break;
                case "Iron Condor":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    if (option.Type == OptionType.CALL)
                    {
                        Option ironCondorLeg2C = _optionChain.OrderBy(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike && x.Expiration == option.Expiration);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = ironCondorLeg2C.OptionSymbol,
                            Type = ironCondorLeg2C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                        Option ironCondorLeg3C = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == (option.Type == OptionType.PUT ? OptionType.CALL : OptionType.PUT) && x.Strike < legs[0].Strike && x.Expiration == option.Expiration);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = ironCondorLeg3C.OptionSymbol,
                            Type = ironCondorLeg3C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        });
                        Option ironCondorLeg4C = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == (option.Type == OptionType.PUT ? OptionType.CALL : OptionType.PUT) && x.Strike < legs[2].Strike && x.Expiration == option.Expiration);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = ironCondorLeg4C.OptionSymbol,
                            Type = ironCondorLeg4C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                        break;
                    }

                    Option ironCondorLeg2P = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike < option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = ironCondorLeg2P.OptionSymbol,
                        Type = ironCondorLeg2P.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option ironCondorLeg3P = _optionChain.FirstOrDefault(x => x.Type == (option.Type == OptionType.PUT ? OptionType.CALL : OptionType.PUT) && x.Strike > option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = ironCondorLeg3P.OptionSymbol,
                        Type = ironCondorLeg3P.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    Option ironCondorLeg4P = _optionChain.FirstOrDefault(x => x.Type == (option.Type == OptionType.PUT ? OptionType.CALL : OptionType.PUT) && x.Strike > legs[2].Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = ironCondorLeg4P.OptionSymbol,
                        Type = ironCondorLeg4P.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    break;
                case "Reversal":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = Underlying,
                        Side = ZeroPlus.Models.Data.Enums.Side.Sell
                    });
                    Option reversalLeg2 = _optionChain.FirstOrDefault(x => x.Type == OptionType.CALL && x.Strike == option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = reversalLeg2.OptionSymbol,
                        Type = reversalLeg2.Type.ToString(),
                        Side = ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    Option reversalLeg3 = _optionChain.FirstOrDefault(x => x.Type == OptionType.PUT && x.Strike == option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = reversalLeg3.OptionSymbol,
                        Type = reversalLeg3.Type.ToString(),
                        Side = ZeroPlus.Models.Data.Enums.Side.Sell
                    });
                    break;
                case "Straddle":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    Option straddleLeg2 = _optionChain.FirstOrDefault(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike == option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = straddleLeg2?.OptionSymbol,
                        Type = straddleLeg2?.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Strangle":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    if (option.Type == OptionType.CALL)
                    {
                        Option strangleLeg2C = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike < option.Strike && x.Expiration == option.Expiration);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = strangleLeg2C.OptionSymbol,
                            Type = strangleLeg2C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        });
                        break;
                    }

                    Option strangleLeg2P = _optionChain.FirstOrDefault(x => x.Type == (option.Type == OptionType.CALL ? OptionType.PUT : OptionType.CALL) && x.Strike > option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = strangleLeg2P.OptionSymbol,
                        Type = strangleLeg2P.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
                case "Vertical":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    if (option.Type == OptionType.CALL)
                    {
                        Option verticalLeg2C = _optionChain.FirstOrDefault(x => x.Type == option.Type && x.Strike > option.Strike && x.Expiration == option.Expiration);
                        legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                        {
                            Symbol = verticalLeg2C.OptionSymbol,
                            Type = verticalLeg2C.Type.ToString(),
                            Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                        break;
                    }

                    Option verticalLeg2P = _optionChain.OrderByDescending(x => x.Strike).FirstOrDefault(x => x.Type == option.Type && x.Strike < option.Strike && x.Expiration == option.Expiration);
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = verticalLeg2P.OptionSymbol,
                        Type = verticalLeg2P.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                    break;
                case "ComplexOrderTicket":
                case "BasketTrader":
                    legs.Add(new TicketLegModel(OmsCore, Underlying, SelectedAccount, null, PortfolioManagerModel)
                    {
                        Symbol = option.OptionSymbol,
                        Type = option.Type.ToString(),
                        Side = side == "BUY" ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                    });
                    break;
            }

            return legs;
        }

        public void SaveViewModelConfig()
        {
            try
            {
                OptionChainConfig config = GetConfig();

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(OptionChainConfig)}.xml");

                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
                Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private OptionChainConfig GetConfig()
        {
            return new()
            {
                Underlying = Underlying,
                Account = SelectedAccount,
                VisibleStrikes = VisibleStrikes,
                BestPriceLookback = BestPriceLookback,
            };
        }

        internal async Task LoadViewModelConfigAsync(string uid)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(OptionChainConfig)}.xml");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = File.ReadAllText(configExportPath);
                    OptionChainConfig config = await Task.Run(() => JsonConvert.DeserializeObject<OptionChainConfig>(myFileStream));
                    LoadFromConfig(config);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadViewModelConfigAsync));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private void LoadFromConfig(OptionChainConfig config)
        {
            Underlying = config.Underlying;
            SelectedAccount = config.Account;
            VisibleStrikes = config.VisibleStrikes;
            BestPriceLookback = config.BestPriceLookback;

            _ = LoadOptionChain();
        }
    }
}
