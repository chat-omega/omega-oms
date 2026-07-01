using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SpreadTemplateViewModel : ModuleViewModelBase, IModuleViewModel
    {
        public const string MODULE_TITLE = "Spread Template";

        private CancellationTokenSource _cancellationTokenSource = new();
        private List<SpreadGeneratorResults> _latestSpreadGeneratorResults = new();
        private readonly ConcurrentDictionary<DateTime, TemplateExpirationModel> _expirationToTemplateModelMap = new();
        private readonly DominatorsManagerModel _dominatorsManagerModel;
        private readonly IModuleFactory _moduleFactory;


        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public override Module Module { get; protected set; } = Module.SpreadTemplate;
        public List<string> SupportedFormats { get; set; } = new List<string> { "Dominator List", "CSV" };

        [Bindable]
        public partial string ExportFormat { get; set; }

        [Bindable]
        public partial bool RandomizeExport { get; set; }

        [Bindable]
        public partial bool CanExport { get; set; }

        [Bindable]
        public partial string FilePath { get; set; }

        [Bindable]
        public partial bool SaveWhenDone { get; set; }

        [Bindable]
        public partial bool ShowProgressBar { get; set; }

        [Bindable]
        public partial string ProgressStatus { get; set; }

        [Bindable]
        public partial bool ExportToFile { get; set; }

        [Bindable]
        public partial bool OpenInBasket { get; set; }

        [Bindable]
        public partial bool AllowGenerating { get; set; }

        [Bindable]
        public partial bool Generating { get; set; }

        [Bindable]
        public partial string UnderlyingQuery { get; set; }

        [Bindable]
        public partial bool MinStrikeEnabled { get; set; }

        [Bindable]
        public partial double MinStrike { get; set; }

        [Bindable]
        public partial bool MaxStrikeEnabled { get; set; }

        [Bindable]
        public partial double MaxStrike { get; set; }

        [Bindable]
        public partial bool MinOpenInterestEnabled { get; set; }

        [Bindable]
        public partial double MinOpenInterest { get; set; }

        [Bindable]
        public partial bool MaxOpenInterestEnabled { get; set; }

        [Bindable]
        public partial double MaxOpenInterest { get; set; }

        [Bindable]
        public partial ObservableCollection<OptionChainModel> OptionChains { get; set; }

        [Bindable]
        public partial ObservableCollection<SpreadTemplateRowViewModel> Templates { get; set; }

        [Bindable]
        public partial ObservableCollection<TemplateExpirationModel> Expirations { get; set; }

        public SpreadTemplateViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, DominatorsManagerModel dominatorsManagerModel, IModuleFactory moduleFactory) :
            base(configBrowserViewModel, omsCore)
        {
            _dominatorsManagerModel = dominatorsManagerModel;
            _moduleFactory = moduleFactory;
            ModuleTitle = MODULE_TITLE;
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
            ConfigBrowserViewModel = configBrowserViewModel;
            ConfigBrowserViewModel.Module = Module.SpreadTemplate.ToString();

            ExportFormat = SupportedFormats.FirstOrDefault();
            RandomizeExport = true;
            OptionChains = new ObservableCollection<OptionChainModel>();
            Templates = new ObservableCollection<SpreadTemplateRowViewModel>();
            Expirations = new ObservableCollection<TemplateExpirationModel>();
        }

        protected override async void OnInitializeInRuntime()
        {

            try
            {
                List<Option> symbols = await ServiceLocator.GetService<OmsCore>().QuoteClient.GetOptionsAsync("$SPX");
                if (symbols != null)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        foreach (DateTime expiration in symbols.Select(x => x.Expiration).Distinct().OrderBy(x => x))
                        {
                            GetExpiration(expiration);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to OnInitialize SpreadTemplateViewModel");
            }
        }

        public TemplateExpirationModel GetExpiration(DateTime expiration)
        {
            if (!_expirationToTemplateModelMap.TryGetValue(expiration, out TemplateExpirationModel expirationModel) || (expirationModel == default && expiration != default))
            {
                expirationModel = new TemplateExpirationModel(expiration);
                _expirationToTemplateModelMap[expiration] = expirationModel;
                Expirations.Add(expirationModel);
            }

            return expirationModel;
        }

        [Command]
        public void AddTemplate()
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    SpreadTemplateRowViewModel template = new(this);
                    Templates.Add(template);
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddTemplate));
            }
        }

        [Command]
        public void RemoveTemplateCommand(SpreadTemplateRowViewModel rowViewModel)
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    Templates.Remove(rowViewModel);
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveTemplateCommand));
            }
        }

        [Command]
        public void ReverseSides()
        {
            try
            {
                foreach (SpreadTemplateRowViewModel template in Templates)
                {
                    template.Reverse();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseSides));
            }
        }

        [Command]
        public void RowUpCommand(SpreadTemplateRowViewModel rowViewModel)
        {
            try
            {
                int index = Templates.IndexOf(rowViewModel);
                if (index > 0)
                {
                    Templates.Move(index, index - 1);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RowUpCommand));
            }
        }

        [Command]
        public void RowDownCommand(SpreadTemplateRowViewModel rowViewModel)
        {
            try
            {
                int index = Templates.IndexOf(rowViewModel);
                if (index < Templates.Count - 1)
                {
                    Templates.Move(index, index + 1);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RowDownCommand));
            }
        }

        [Command]
        public void DuplicateTemplateCommand(SpreadTemplateRowViewModel templateBase)
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    SpreadTemplateRowViewModel template = new(this)
                    {
                        Strategy = templateBase.Strategy,
                        Side = templateBase.Side,
                        Leg1Delta = templateBase.Leg1Delta,
                        Leg2Delta = templateBase.Leg2Delta,
                        Leg3Delta = templateBase.Leg3Delta,
                        Leg4Delta = templateBase.Leg4Delta,
                        Leg1Expiration = templateBase.Leg1Expiration,
                        Leg2Expiration = templateBase.Leg2Expiration,
                        IsLeg3Visible = templateBase.IsLeg3Visible,
                        IsLeg4Visible = templateBase.IsLeg4Visible,
                        IsLeg2ExpirationVisible = templateBase.IsLeg2ExpirationVisible,
                    };
                    Templates.Add(template);
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveTemplateCommand));
            }
        }

        [Command]
        public void Clone()
        {
            try
            {
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
                        _ = viewModel.LoadFromConfigAsync(GetConfig());

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
        public void ClearSymbols()
        {
            try
            {
                OptionChains.Clear();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearSymbols));
            }
        }

        [Command]
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        [Command]
        public void ConfigRowDoubleClick(NodeClickArgs args)
        {
            if (args != null && args.Item != null)
            {
                if (args.Item is ConfigSave configSave)
                {
                    LoadSavedConfig(configSave);
                }
            }
        }

        [Command]
        public void LoadSavedConfig(ConfigSave configSave)
        {
            try
            {
                if (configSave == null)
                {
                    return;
                }
                OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id)
                    .ContinueWith(t => Dispatcher?.Invoke(() => LoadConfigFromJsonAsync(t.Result.ConfigJson)));
                ModuleTitle = configSave.Title + " - " + MODULE_TITLE;
            }
            catch (AggregateException ae)
            {
                foreach (Exception ex in ae.InnerExceptions)
                {
                    _log.Error(ex, nameof(LoadSavedConfig));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadSavedConfig));
            }
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = Module.SpreadTemplate;
                SpreadTemplateConfig config = GetConfig();
                viewModel.Config = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void SaveConfig()
        {
            try
            {
                SaveView view = new();

                SaveViewModel viewModel = view.DataContext as SaveViewModel;
                viewModel.LoadGroups(Module.SpreadTemplate);
                viewModel.ShowDefault = false;
                SpreadTemplateConfig config = GetConfig();
                viewModel.Config = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                if (ConfigSave != null)
                {
                    viewModel.Id = ConfigSave.Id;
                    viewModel.Title = ConfigSave.Title;
                    viewModel.SelectedGroup = ConfigSave.Group;
                }

                view.ShowDialog();

                if (!string.IsNullOrWhiteSpace(viewModel.Title) && viewModel.Success)
                {
                    ModuleTitle = viewModel.Title + " - " + MODULE_TITLE;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveConfig));
            }
        }

        [Command]
        public void PasteFromClipboard()
        {
            UnderlyingQuery = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        [Command]
        public async Task SearchUnderlying()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(UnderlyingQuery))
                {
                    AllowGenerating = false;
                    IEnumerable<string> symbols = UnderlyingQuery.Replace(",", ";")
                                                 .Split(';')
                                                 .Where(x => !string.IsNullOrWhiteSpace(x))
                                                 .Select(x => x.Trim().ToUpper())
                                                 .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                                                 .Distinct();

                    List<Task> getOptionsTasks = new();
                    ConcurrentBag<OptionChainModel> results = new();
                    foreach (string symbol in symbols)
                    {
                        Task task = OmsCore.QuoteClient.GetSymbolsAsync(symbol)
                                                      .ContinueWith(t => results.Add(new OptionChainModel(OmsCore.SecurityBook, t.Result)));
                        getOptionsTasks.Add(task);
                    }

                    await Task.Run(() => Task.WhenAll(getOptionsTasks));
                    AddMultipleOptionChains(results.ToList());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchUnderlying));
            }
        }

        [Command]
        public void RemoveOptionChain(OptionChainModel optionChain)
        {
            if (OptionChains.Any(x => x.Symbol == optionChain.Symbol))
            {
                DispatcherService?.BeginInvoke(() =>
                {
                    OptionChains.Remove(optionChain);
                }).ContinueWith(x => CheckIfGeneratingIsAllowed());
            }
        }

        [Command]
        public void SaveToFileCommand()
        {
            try
            {
                ExportSpreadsToFileView exportToFileView = new()
                {
                    DataContext = this
                };
                exportToFileView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveToFileCommand));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async Task WriteToFile()
        {
            try
            {
                string titleString = GetTitle();
                if (titleString.Length > 230)
                {
                    titleString = titleString.Substring(0, 230);
                }
                bool save = false;
                switch (ExportFormat.ToUpper())
                {
                    case "DOMINATOR LIST":
                        titleString = "DOMINATOR SPREADS " + titleString;
                        if (titleString.Length > 230)
                        {
                            titleString = titleString.Substring(0, 230);
                        }
                        SaveFileDialogService.DefaultExt = "xlsx";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {_latestSpreadGeneratorResults.Sum(x => x.Spreads.Count)} spreads";
                        SaveFileDialogService.Filter = "Dominator List|*.XLSX";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string FilePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => WriteSpreadsToFileUsingDominatorFormat(FilePath));
                            }
                            else
                            {
                                SaveWhenDone = true;
                            }
                        }
                        break;
                    case "CSV":
                        SaveFileDialogService.DefaultExt = "csv";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {_latestSpreadGeneratorResults.Sum(x => x.Spreads.Count)} spreads";
                        SaveFileDialogService.Filter = "Comma Separated Values|*.CSV";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string FilePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => WriteSpreadsToFile(FilePath));
                            }
                            else
                            {
                                SaveWhenDone = true;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(WriteToFile));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async Task OpenInBasketTrader()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    MessageResult result = MessageResult.No;
                    await Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        result = MessageBoxService.Show($"Would you like to load {_latestSpreadGeneratorResults.Sum(x => x.Spreads.Count)} spreads in basket?",
                                                        "Spread Template",
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
                                viewModel.LoadFromSpreadResultsAsync(_latestSpreadGeneratorResults);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async void GenerateSpreadsCommand()
        {
            try
            {
                Generating = true;
                AllowGenerating = false;
                CanExport = false;
                ShowProgressBar = true;
                ProgressStatus = "Generating " + GetTitle();
                _latestSpreadGeneratorResults = new List<SpreadGeneratorResults>();
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;

                Stopwatch stopwatch = Stopwatch.StartNew();
                HashSet<string> errors = new();
                List<Task<SpreadGeneratorResults>> tasks = new();
                foreach (string symbol in OptionChains.Select(x => x.Symbol).Distinct())
                {
                    var options = await OmsCore.QuoteClient.GetOptionsAsync(symbol);

                    if (MinStrikeEnabled)
                    {
                        options = options.Where(x => x.Strike >= MinStrike).ToList();
                    }

                    if (MaxStrikeEnabled)
                    {
                        options = options.Where(x => x.Strike <= MaxStrike).ToList();
                    }

                    if (MinOpenInterestEnabled || MaxOpenInterestEnabled)
                    {
                        DataStore openInterestStore = new(token, OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        openInterestStore.GetQuoteDataFor(options, SubscriptionFieldType.OpenInterest);
                        List<Option> selectedOptions = new();
                        foreach (Option option in options)
                        {
                            double openInterest = Math.Abs(await openInterestStore.GetDataAsync(option.Symbol));
                            if (double.IsNaN(openInterest))
                            {
                                errors.Add("Open Interest not found for " + option.Symbol);
                                continue;
                            }
                            else if ((MinOpenInterestEnabled && openInterest < MinOpenInterest) ||
                                     (MaxOpenInterestEnabled && openInterest > MaxOpenInterest))
                            {
                                continue;
                            }
                            selectedOptions.Add(option);
                        }
                        options = selectedOptions;
                    }

                    foreach (SpreadTemplateRowViewModel template in Templates)
                    {
                        SpreadTemplateGenerator item = new();
                        tasks.Add(Task.Run(async () => await item.GenerateFromTemplateAsync(symbol, options, template, token), token));
                    }
                }

                await Task.Run(() => Task.WhenAll(tasks), token);

                List<SpreadGeneratorResults> spreadGeneratorResults = tasks.Select(x => x.Result).ToList();
                errors = errors.Concat(spreadGeneratorResults.SelectMany(x => x.Errors)).ToHashSet();

                int totalCount = spreadGeneratorResults.Sum(x => x.Spreads.Count);

                stopwatch.Stop();
                ShowProgressBar = false;
                ProgressStatus = $"Done! {totalCount:N0} spreads generated, in {stopwatch.ElapsedMilliseconds}ms.";

                if (errors.Count > 0)
                {
                    MessageResult result = MessageResult.No;
                    await Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        result = MessageBoxService.Show($"Spreads generator finished with {errors.Count} errors.\n" +
                                                        "Would you like view the errors?",
                                                        "Spread Template",
                                                        MessageButton.YesNo,
                                                        MessageIcon.Question,
                                                        MessageResult.No);
                    }));
                    if (result == MessageResult.Yes)
                    {
                        await Dispatcher?.BeginInvoke(new Action(() =>
                            MessageBoxService.ShowMessage($"{String.Join("\n", errors)}",
                                                           "Spread Template Errors",
                                                           MessageButton.OK,
                                                           MessageIcon.Error)
                        ));
                    }
                }

                if (totalCount > 0)
                {
                    _latestSpreadGeneratorResults = spreadGeneratorResults;
                    CanExport = true;
                    if (SaveWhenDone)
                    {
                        _ = WriteToFileWhenDone();
                    }
                }
                else
                {
                    CanExport = false;
                }
            }
            catch (OperationCanceledException ex)
            {
                ShowProgressBar = false;
                ProgressStatus = $"{ex.Message}";
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateSpreadsCommand));

                ShowProgressBar = false;
                ProgressStatus = $"Error! {ex.Message}.";

                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
            finally
            {
                Generating = false;
                AllowGenerating = true;
            }
        }

        private async void WriteSpreadsToFile(string filePath)
        {
            SaveWhenDone = false;
            List<string> spreads = (await Task.Run(() => ProcessResults(_latestSpreadGeneratorResults))).ToList();
            if (RandomizeExport)
            {
                ListHelper.Shuffle(spreads);
            }
            FileStream file = new(filePath, FileMode.Create);
            StreamWriter streamWriter = new(file, Encoding.Default);

            StringBuilder sb = new();

            foreach (string spread in spreads)
            {
                streamWriter.WriteLine(spread);
            }
            streamWriter.Close();
            file.Close();
        }

        private string[] ProcessResults(List<SpreadGeneratorResults> spreadsResults)
        {
            int count = spreadsResults.Sum(x => x.Spreads.Count);
            string[] spreads = new string[count];
            int index = 0;
            foreach (SpreadGeneratorResults spreadsResult in spreadsResults)
            {
                foreach (Spread spread in spreadsResult.Spreads)
                {
                    spreads[index++] = spread.Symbol;
                }
            }

            return spreads;
        }

        private string GetTitle()
        {
            string symbols = String.Join(", ", OptionChains.Select(x => x.Symbol));
            string types = Templates.Any(x => x.Strategy.ToString().Contains("CALL")) ? "- C" : "";
            types += Templates.Any(x => x.Strategy.ToString().Contains("PUT")) ? "- P" : "";
            string selectedStrategies = string.Join(", ", Templates.Select(x => x.Strategy.ToString().Replace("_", " ").Replace("CALL", "").Replace("PUT", "").Trim()).Distinct());
            return $"{symbols} {types.Trim()} - {selectedStrategies} Spreads";
        }

        private void AddMultipleOptionChains(List<OptionChainModel> optionChains)
        {
            DispatcherService?.BeginInvoke(() =>
            {
                foreach (OptionChainModel optionChain in optionChains)
                {
                    if (!OptionChains.Any(x => x.Symbol == optionChain.Symbol))
                    {
                        OptionChains.Add(optionChain);
                    }
                }
            }).ContinueWith(x => CheckIfGeneratingIsAllowed());
        }

        private void CheckIfGeneratingIsAllowed()
        {
            AllowGenerating = OptionChains.Count > 0;
        }

        private async Task WriteToFileWhenDone()
        {
            if (SaveWhenDone)
            {
                SaveWhenDone = false;
                switch (ExportFormat.ToUpper())
                {
                    case "DOMINATOR LIST":
                        await Task.Run(() => WriteSpreadsToFileUsingDominatorFormat(FilePath));
                        break;
                    case "CSV":
                        await Task.Run(() => WriteSpreadsToFile(FilePath));
                        break;
                }
            }
        }

        private void WriteSpreadsToFileUsingDominatorFormat(string filePath)
        {
            try
            {
                SaveWhenDone = false;

                using Workbook workbook = new();
                Worksheet worksheet = workbook.Worksheets[0];
                workbook.BeginUpdate();
                try
                {
                    int rowsCount = Math.Max(2, _latestSpreadGeneratorResults.Sum(x => x.Spreads.Count));
                    object[,] values = new object[rowsCount, 24];

                    values[0, 21] = DateTime.Today.ToString("M.d.yy");
                    values[0, 22] = _latestSpreadGeneratorResults.FirstOrDefault().Strategy.ToString();
                    values[1, 21] = OmsCore.User.Username;

                    Dictionary<string, double> underlyingSymbolToLastPriceMap = new();

                    int index = -1;
                    for (int i = 0; i < _latestSpreadGeneratorResults.Count; i++)
                    {
                        SpreadGeneratorResults spreadResult = _latestSpreadGeneratorResults[i];
                        List<Spread> spreads = spreadResult.Spreads.Where(x => x != null).ToList();
                        foreach (Spread spread in spreads)
                        {
                            index++;

                            string underlying = spread.Legs[0].Option?.Underlying?.Symbol;
                            if (!underlyingSymbolToLastPriceMap.ContainsKey(underlying))
                            {
                                double lastPrice = OmsCore.QuoteClient.GetSnapshotAsync(underlying, SubscriptionFieldType.LastPrice).Result;
                                underlyingSymbolToLastPriceMap[underlying] = lastPrice;
                            }

                            if (TimeHelper.IsThirdFridayOfTheMonth(spread.Legs[0].Option.Expiration) &&
                                spread.Legs[0].Option?.Underlying?.Symbol?.Replace("$", "") == spread.Legs[0].Option.RootSymbol)
                            {
                                values[index, 0] = spread.Legs[0].Option.Expiration.ToString("MMM yy").ToUpper();
                            }
                            else
                            {
                                values[index, 0] = spread.Legs[0].Option.Expiration.ToString("dd_MMM_yy").ToUpper();
                            }

                            switch (spreadResult.Strategy)
                            {
                                case Strategy.Vertical:
                                case Strategy.Ratio1X2:
                                case Strategy.Ratio1X3:
                                case Strategy.RatioCustom:
                                case Strategy.Butterfly:
                                case Strategy.SkewedButterfly:
                                case Strategy.CalendarButterfly:
                                case Strategy.IronButterfly:
                                case Strategy.Condor:
                                case Strategy.IronCondor:
                                case Strategy.OneThreeThreeOne:
                                    values[index, 1] = spread.Legs[0].Option.Strike;
                                    values[index, 2] = spread.Legs[1].Option.Strike;
                                    if (spread.Legs[2].Option != null)
                                    {
                                        values[index, 12] = spread.Legs[2].Option.Strike;
                                    }
                                    break;
                                case Strategy.Calendar:
                                case Strategy.Diagonal:
                                    if (TimeHelper.IsThirdFridayOfTheMonth(spread.Legs[1].Option.Expiration) &&
                                        spread.Legs[1].Option?.Underlying?.Symbol?.Replace("$", "") == spread.Legs[1].Option.RootSymbol)
                                    {
                                        values[index, 1] = spread.Legs[1].Option.Expiration.ToString("MMM yy").ToUpper();
                                    }
                                    else
                                    {
                                        values[index, 1] = spread.Legs[1].Option.Expiration.ToString("dd_MMM_yy").ToUpper();
                                    }

                                    values[index, 2] = spread.Legs[0].Option.Strike;
                                    values[index, 12] = spread.Legs[1].Option.Strike;
                                    break;
                            }

                            switch (spreadResult.Strategy)
                            {
                                case Strategy.Vertical:
                                    values[index, 4] = "1X1";
                                    break;
                                case Strategy.Ratio1X2:
                                    values[index, 4] = "1X2";
                                    break;
                                case Strategy.Ratio1X3:
                                    values[index, 4] = "1X3";
                                    break;
                                case Strategy.Butterfly:
                                case Strategy.SkewedButterfly:
                                case Strategy.CalendarButterfly:
                                case Strategy.IronButterfly:
                                    values[index, 4] = "FLY";
                                    break;
                            }

                            values[index, 3] = spreadResult.Type == PutCall.Call ? "Call" : "Put";
                            values[index, 7] = spread.Symbol;
                            values[index, 14] = spread.Legs[0].Option?.Symbol;
                            values[index, 15] = spread.Legs[1].Option?.Symbol;
                            values[index, 16] = spread.Legs[2].Option?.Symbol;
                            values[index, 17] = spread.Legs[3].Option?.Symbol;
                            values[index, 18] = spreadResult.Underlying.Replace("$", "");
                            values[index, 23] = 1;
                        }
                    }

                    values[0, 20] = string.Join(";", underlyingSymbolToLastPriceMap.Keys.Select(x => x.Replace("$", "")));
                    values[1, 22] = string.Join(";", underlyingSymbolToLastPriceMap.Select(x => x.Key + "," + x.Value));

                    if (RandomizeExport)
                    {
                        Random random = new();
                        int row = values.GetLength(0);
                        int columns = values.GetLength(1);

                        // Dont change metadata columns
                        if (columns > 4)
                        {
                            columns -= 4;
                        }

                        while (row > 1)
                        {
                            int swapRow = random.Next(row--);
                            for (int col = 0; col < columns; col++)
                            {
                                object temp = values[row, col];
                                values[row, col] = values[swapRow, col];
                                values[swapRow, col] = temp;
                            }
                        }
                    }

                    for (int row = 0; row < values.GetLength(0); row++)
                    {
                        for (int col = 0; col < values.GetLength(1); col++)
                        {
                            object value = values[row, col];
                            worksheet.Cells[row, col].SetValue(value);
                        }
                    }
                }
                finally
                {
                    workbook.EndUpdate();
                }

                workbook.SaveDocument(filePath, DocumentFormat.Xlsx);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(WriteSpreadsToFileUsingDominatorFormat));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private SpreadTemplateConfig GetConfig()
        {
            List<string> underlyings = OptionChains.Select(x => x.Symbol).ToList();

            if (!string.IsNullOrWhiteSpace(UnderlyingQuery))
            {
                List<string> symbols = UnderlyingQuery.Replace(",", ";")
                                             .Split(';')
                                             .Where(x => !string.IsNullOrWhiteSpace(x))
                                             .Select(x => x.Trim().ToUpper())
                                             .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                                             .Distinct()
                                             .ToList();
                underlyings.AddRange(symbols);
            }

            return new SpreadTemplateConfig()
            {
                UnderlyingQuery = string.Join(",", underlyings.Distinct()),
                Templates = Templates.Select(x => x.GetConfigAsJson()).ToList(),
                MinStrikeEnabled = MinStrikeEnabled,
                MinStrike = MinStrike,
                MaxStrikeEnabled = MaxStrikeEnabled,
                MaxStrike = MaxStrike,
                MinOpenInterestEnabled = MinOpenInterestEnabled,
                MinOpenInterest = MinOpenInterest,
                MaxOpenInterestEnabled = MaxOpenInterestEnabled,
                MaxOpenInterest = MaxOpenInterest,
            };
        }

        public async Task LoadFromConfigAsync(SpreadTemplateConfig config)
        {
            OptionChains.Clear();
            Templates.Clear();

            UnderlyingQuery = config.UnderlyingQuery;
            if (config.Templates != null)
            {
                foreach (string json in config.Templates)
                {
                    SpreadTemplateRowViewModel row = new(this);
                    await row.LoadConfigFromJsonAsync(json);
                    Templates.Add(row);
                }
            }
            MinStrikeEnabled = config.MinStrikeEnabled;
            MinStrike = config.MinStrike;
            MaxStrikeEnabled = config.MaxStrikeEnabled;
            MaxStrike = config.MaxStrike;
            MinOpenInterestEnabled = config.MinOpenInterestEnabled;
            MinOpenInterest = config.MinOpenInterest;
            MaxOpenInterestEnabled = config.MaxOpenInterestEnabled;
            MaxOpenInterest = config.MaxOpenInterest;
            _ = InvokeReady();
        }

        internal string GetConfigJson()
        {
            return JsonConvert.SerializeObject(GetConfig(), Newtonsoft.Json.Formatting.Indented);
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }
                SpreadTemplateConfig config = await Task.Run(() => JsonConvert.DeserializeObject<SpreadTemplateConfig>(configJson));
                await LoadFromConfigAsync(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        internal async Task LoadViewModelConfigAsync(string uid)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(SpreadTemplateConfig)}.json");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = await Task.Run(() => File.ReadAllText(configExportPath));
                    SpreadTemplateConfig config = await Task.Run(() => JsonConvert.DeserializeObject<SpreadTemplateConfig>(myFileStream));
                    await LoadFromConfigAsync(config);
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

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            SpreadTemplateConfig config = GetConfig();
            string configJson = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
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
                SpreadTemplateConfig config = GetConfig();

                string configJson = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(SpreadTemplateConfig)}.json");
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
    }
}
