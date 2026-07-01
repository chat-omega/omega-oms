using DevExpress.Mvvm;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Models;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Services;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Models.Extensions;
using System.Collections.Concurrent;
using System;
using DevExpress.Mvvm.Native;
using System.IO;
using System.Globalization;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Collections;
using DevExpress.Office.Utils;
using System.Windows;
using DevExpress.Xpf.Core.DragDrop.Native;
using System.Threading;
using DevExpress.Xpf.Core;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Ui.Helper;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class NewDominatorManagerViewModel : ModuleViewModelBase
    {
        public NewDominatorManagerViewModel(
            IAbstractFactory<DominatorTraderModel> dominatorTraderF,
            ConfigBrowserViewModel configBrowserViewModel,
            OmsCore omsCore)
            : base(configBrowserViewModel, omsCore)
        {
            _dominatorTraderF = dominatorTraderF;
            Dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        }

        private readonly IAbstractFactory<DominatorTraderModel> _dominatorTraderF;
        #region Services
        public IWindowService SettingsWindowService => GetService<IWindowService>("Settings");
        public IWindowService SpreadQueueWindowService => GetService<IWindowService>("SpreadQueue");
        public IOmsMessageBoxService OmsMessageBoxService => GetService<IOmsMessageBoxService>();
        private IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        private ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public IDispatcherService DispatcherService => GetService<IDispatcherService>();
        private IUIObjectService LoadSpreadsProgressService => GetService<IUIObjectService>("LoadSpreadsProgressService");
        private IUIObjectService TableService => GetService<IUIObjectService>("DominatorsGridService");
        #endregion
        public override Module Module { get; protected set; } = Module.NewDominatorManager;
        public FastObservableCollection<DominatorTraderModel> DominatorTraderModels { get; init; } = new();
        private ConcurrentDictionary<DominatorTraderModel, OmsAutoTraderSettings> autotraderSettings = new();
        public ArrayList SelectedRows { get; set; } = new ArrayList();

        #region Loading Progress Bar
        private int _loadingSpreadCount = 0;
        internal int ProgressBarMax
        {
            get => _loadingSpreadCount;
            set
            {
                _loadingSpreadCount = value;
                DispatcherService.Invoke(() =>
                {
                    LoadSpreadsProgressService.Object.Maximum = _loadingSpreadCount;
                    LoadSpreadsProgressService.Object.Visibility = Visibility.Visible;
                });
            }
        }
        private void DoProgress(int progress)
        {
            DispatcherService.Invoke(() =>
            {
                if (progress == _loadingSpreadCount) LoadSpreadsProgressService.Object.Visibility = Visibility.Collapsed;
                LoadSpreadsProgressService.Object.Value = progress;
            });
        }
        #endregion




        #region ModuleViewModelBase Overrides
        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            var traderConfigs = await Task.Run(() => JsonConvert.DeserializeObject<List<DominatorConfig>>(configJson));

            ConcurrentBag<(string traderTitle, Task<DominatorItem[]> spreads)> spreadLists = new();

            var items = traderConfigs.AsParallel().Select(config =>
            {
                DominatorTraderModel traderModel = _dominatorTraderF.Create();
                traderModel.DominatorConfig = config;
                if (config.SpreadListSaveId is Guid saveId)
                {
                    Task<DominatorItem[]> loadSpreadsTask = LoadSpreadListFromSaveIdAsync(saveId);
                    spreadLists.Add((config.Title, loadSpreadsTask));
                }
                return traderModel;
            }).ToList();

            await DispatcherService.BeginInvoke(() => DominatorTraderModels.AddRange(items));


            Task.WaitAll(spreadLists.Select(async x =>
            {
                DominatorTraderModel trader = DominatorTraderModels.First(d => d.DominatorConfig.Title == x.traderTitle);
                DominatorItem[] spreads = await x.spreads;
                await trader.AddMultipleSpreadsAsync(spreads, dispatcher: Dispatcher);
            }).ToArray());
        }
        public async Task AddDominatorTraderModel(IEnumerable<SpreadGeneratorResults> results, string title)
        {
            try
            {
                var newTrader = _dominatorTraderF.Create();
                newTrader.DominatorConfig.Title = title;
                await Dispatcher.BeginInvoke(() => DominatorTraderModels.Add(newTrader));
                IProgress<int> progress = new Progress<int>(DoProgress);
                ProgressBarMax = results.Sum(r => r.TotalCount);
                await newTrader.LoadFromSpreadResults(results, Dispatcher, progress);
            }
            catch (Exception e)
            {
                _log.Error(e, "Error in AddDominatorTraderModel");
            }
        }

        private async Task<DominatorItem[]> LoadSpreadListFromSaveIdAsync(Guid saveId)
        {
            try
            {
                if (DominatorTraderModels.FirstOrDefault(d => d.DominatorConfig.SpreadListSaveId == saveId) is DominatorTraderModel traderModel)
                {
                    string[] paths = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"*-{saveId}.json");

                    string filePath = paths.Length == 1 ? paths[0] : paths.MaxBy(path =>
                    {
                        string dtstr = path.Split('-').TakeLast(2).First();
                        return DateTime.ParseExact(dtstr, "MM-dd-yyyy hh.mm", CultureInfo.InvariantCulture);
                    });

                    string spreadsJson = await File.ReadAllTextAsync(filePath);
                    IProgress<int> progress = new Progress<int>(DoProgress);
                    var items = await traderModel.ReadAllSpreadsFromFile(spreadsJson, Dispatcher, progress);
                    ProgressBarMax = items.Length;
                    return items;
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "Error while loading spreadList {0}", saveId);
            }

            return Array.Empty<DominatorItem>();
        }

        internal async Task AddTraderFromList(string filePath)
        {
            DominatorTraderModel traderModel = _dominatorTraderF.Create();
            traderModel.DominatorConfig = new DominatorConfig(true);
            await Dispatcher.BeginInvoke(() => DominatorTraderModels.Add(traderModel));
            string spreadsJson = await File.ReadAllTextAsync(filePath);
            IProgress<int> progress = new Progress<int>(DoProgress);
            var spreads = await traderModel.ReadAllSpreadsFromFile(spreadsJson, Dispatcher, progress);
            ProgressBarMax = spreads.Length;
            await traderModel.AddMultipleSpreadsAsync(spreads, dispatcher: Dispatcher);
        }

        private async Task SaveSpreadListAsync(DominatorTraderModel traderModel)
        {
            traderModel.DominatorConfig.SpreadListSaveId = traderModel.DominatorConfig.SpreadListSaveId ?? new Guid();
            var fileName = $"{traderModel.DominatorConfig.Title}-{OmsCore.User.Username}-{nameof(DominatorTraderModel)}-{DateTime.Now:MM-dd-yyyy hh.mm}-{traderModel.DominatorConfig.SpreadListSaveId}";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName + ".json");
            string SpreadListJson = await Task.Run(() => JsonConvert.SerializeObject(traderModel.DominatorItemsSymbols.ToArray()));
            await File.WriteAllTextAsync(filePath, SpreadListJson);
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            var configs = DominatorTraderModels.Select(x => x.DominatorConfig).ToList();
            return JsonConvert.SerializeObject(configs);
        }

        public override void SaveViewModelConfig()
        {
            string configJson = GetConfigSerialized();
            string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
            string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(NewDominatorManagerViewModel)}.json");
            File.WriteAllText(configExportPath, configJson);
        }
        #endregion

        public bool TradersAreSelected() => DominatorTraderModels.Any(d => d.Selected);
        public bool TraderTableHighlighted() => TableService.Object.SelectedItems.Count > 0;

        bool _allowUniqueSpreads;

        public bool AllowUniqueSpreads
        {
            get => _allowUniqueSpreads;
            set
            {
                _allowUniqueSpreads = value;
                if (_allowUniqueSpreads) AllowUniqueSpreadsChanged();
                else DisallowUniqueSpreadsChanged();
            }
        }

        private void AllowUniqueSpreadsChanged()
        {
            DominatorTraderModels.AsParallel().ForAll(trader => trader.AllowUniqueSubmissionsAsync());
        }

        private void DisallowUniqueSpreadsChanged()
        {
            DominatorTraderModels.AsParallel().ForAll(trader => trader.BlockUniqueSubmissionsAsync());
        }


        public bool LoadPriceChain
        {
            get;
            set;
        }

        #region Commands
        [Command(CanExecuteMethodName = nameof(TraderTableHighlighted))]
        public void DeleteDominators()
        {
            int[] handles = TableService.Object.GetSelectedRowHandles();
            DominatorTraderModels.RemoveMultiple(handles);
            DominatorTraderModels.Refresh();
        }
        [Command(CanExecuteMethodName = nameof(TradersAreSelected))]
        public void StartSelected()
        {
            DominatorTraderModels.Where(d => d.Selected).ForEach(d => d.Start());
        }
        [Command]
        public void StartAll() => DominatorTraderModels.ForEach(d => d.Start());

        [Command(CanExecuteMethodName = nameof(TradersAreSelected))]
        public void StopSelected()
        {
            DominatorTraderModels.Where(d => d.Selected).ForEach(d => d.Stop());
        }
        [Command]
        public void StopAll() => DominatorTraderModels.ForEach(d => d.Stop());
        [Command(CanExecuteMethodName = nameof(TradersAreSelected))]
        public void ModifySelected()
        {
            var traders = DominatorTraderModels.Where(d => d.Selected).ToList();
            DominatorConfigurationModuleViewModel bulkConfigWindowVM = new() { TraderModels = traders };
            DominatorConfigurationModuleView bulkConfigWindow = new() { DataContext = bulkConfigWindowVM };
            bulkConfigWindow.Show();
        }
        [Command]
        public void ModifyAll()
        {
            DominatorConfigurationModuleViewModel bulkConfigWindowVM = new() { TraderModels = DominatorTraderModels.ToList() };
            DominatorConfigurationModuleView bulkConfigWindow = new() { DataContext = bulkConfigWindowVM };
            bulkConfigWindow.Show();
        }

        [Command]
        public void StartDom(object parameter)
        {
            if (parameter is DominatorTraderModel traderModel)
            {
                traderModel.Start();
            }
        }

        [Command]
        public void StopDom(object parameter)
        {
            if (parameter is DominatorTraderModel traderModel)
            {
                traderModel.Stop();
            }
        }

        [AsyncCommand]
        public async Task LoadList()
        {
            OpenFileDialogService.Multiselect = false;
            OpenFileDialogService.Filter = "JSON|*.json";
            if (OpenFileDialogService.ShowDialog())
            {
                DominatorTraderModel traderModel = _dominatorTraderF.Create();
                traderModel.DominatorConfig = new DominatorConfig(true)
                {
                    Title = Path.GetFileNameWithoutExtension(OpenFileDialogService.File.Name)
                };
                await DispatcherService.BeginInvoke(() => DominatorTraderModels.Add(traderModel));
                using StreamReader stream = OpenFileDialogService.File.OpenText();
                string spreadsJson = await stream.ReadToEndAsync();
                IProgress<int> progress = new Progress<int>(DoProgress);
                DominatorItem[] spreads = await traderModel.ReadAllSpreadsFromFile(spreadsJson, Dispatcher, progress);
                ProgressBarMax = spreads.Length;
                //traderModel.LoadOptionsTasks;

                await traderModel.AddMultipleSpreadsAsync(spreads, dispatcher: Dispatcher);
                //await Dispatcher.InvokeAsync(() => DominatorTraderModels.Refresh());
            }
        }
        [AsyncCommand]
        public async Task SaveList(object parameter)
        {
            DominatorTraderModel traderModel = parameter as DominatorTraderModel;
            SaveFileDialogService.DefaultFileName = traderModel.Description;
            SaveFileDialogService.DefaultExt = ".json";
            SaveFileDialogService.Filter = "JSON|*.json";
            if (SaveFileDialogService.ShowDialog())
            {
                using FileStream stream = SaveFileDialogService.File.OpenWrite();
                string SpreadListJson = await Task.Run(() => JsonConvert.SerializeObject(traderModel.DominatorItemsSymbols.ToArray()));
                stream.Write(SpreadListJson, System.Text.Encoding.ASCII);
            }
        }
        [Command(CanExecuteMethodName = nameof(TradersAreSelected))]
        public void RemoveHighDeltaSpreadsAndStart()
        {
            throw new NotImplementedException();
        }
        [Command]
        public void LoadEmaCapture()
        {
            throw new NotImplementedException();
        }
        [Command]
        public void DisplayFirmTradeActivity()
        {
            throw new NotImplementedException();
        }
        [Command]
        public void ChangeRoute()
        {
            throw new NotImplementedException();
        }

        [Command]
        public void OpenSpreadQueue(object parameter)
        {
            if (parameter is DominatorTraderModel traderModel)
            {
                NewDominatorSpreadQueueViewModel vm = new(traderModel);
                SpreadQueueWindowService.Title = traderModel.DominatorConfig.Title;
                SpreadQueueWindowService.Show("SpreadQueue", vm, this);
            }

        }
        [Command]
        public void ModifyDomConfig()
        {
            SettingsWindowService.Show("EditDomConfig");
        }

        [Command(CanExecuteMethodName = nameof(TraderTableHighlighted))]
        public void UseExcelCalc()
        {
            OmsCore.DominatorsManager.DominatorTraderModel = null;
            foreach (DominatorTraderModel model in DominatorTraderModels)
                model.UseExcel = false;
            var trader = TableService.Object.SelectedItem as DominatorTraderModel;
            if (OmsCore.DominatorsManager.AnyDominator is Dominator dom)
            {
                trader.UseExcel = true;
                trader.DominatorExcelConnection = dom;
                OmsCore.DominatorsManager.DominatorTraderModel = trader;
            }
        }

        [Command]
        public void OpenRestoreAutoTraderSettings()
        {
            foreach (DominatorTraderModel dominator in DominatorTraderModels)
            {
                autotraderSettings.TryAdd(dominator, new OmsAutoTraderSettings
                {
                    Title = "TEST",
                    ConfigId = new Guid(),
                    AutomationConfigModel = new AutomationConfigModel(),
                    AutoCancelConfig = new AutoCancelConfig(),
                    FishLossConfig = new FishLossConfig(),
                    EdgeType = EdgeType.EdgeToBid,
                    EdgeValue = 0.01,
                    AutoTraderVenue = Venue.TB,
                });
            }
            MessageBoxService.ShowMessage("WARNING: Added blank settings to each selected Dominator for testing,\n Fixed Edge to Bid of 0.01 Set");
        }

        private void OpenAutomationView<T>() where T : UserControl, new()
        {
            var dominator = DominatorTraderModels.First();
            T settingsView = new()
            {
                DataContext = new AutomationSettingsViewModel(nameof(NewDominatorManagerViewModel), Module.NewDominatorManager, OmsCore)
                {
                    AutomationConfig = autotraderSettings[dominator]?.AutomationConfigModel ?? new AutomationConfigModel(),
                }
            };
            var view = new ThemedWindow
            {
                Content = settingsView,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.WidthAndHeight,
            };
            view.Closed += async (s, e) => await SubmitAutoTraderSettingsAsync(dominator);
            view.Show();
        }

        [Command]
        public void OpenLooperSettings()
        {
            try
            {
                OpenAutomationView<LooperSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            }
        }

        [Command]
        public void OpeLegOutSettings()
        {
            try
            {
                OpenAutomationView<LegOutSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpeLegOutSettings));
            }
        }

        [Command]
        public void OpenAutoHedgeSettings()
        {
            try
            {
                OpenAutomationView<AutoHedgeSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenAutoHedgeSettings));
            }
        }

        [Command]
        public void OpenAutoLegSettings()
        {
            try
            {
                OpenAutomationView<AutoLegSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenAutoLegSettings));
            }
        }

        [Command]
        public void OpenSweepSettings()
        {
            try
            {
                OpenAutomationView<SweepTradeSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenSweepSettings));
            }
        }

        [Command]
        public void OpenRouteSettings()
        {
            try
            {
                OpenAutomationView<RouteSettingsView>();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenRouteSettings));
            }
        }

        [Command]
        public void OpenFishLossSettings()
        {
            try
            {
                var dominator = DominatorTraderModels.First();
                FishLossPreventionSettingsView settingsView = new()
                {
                    DataContext = new FishLossPreventionSettingsViewModel()
                    {
                        FishLossConfig = autotraderSettings[dominator].FishLossConfig ?? new FishLossConfig(),
                    }
                };
                var view = new ThemedWindow
                {
                    Content = settingsView,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                view.Closed += async (s, e) => await SubmitAutoTraderSettingsAsync(dominator);
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            }
        }

        [Command]
        public void OpenAutoCancelSettings()
        {
            try
            {
                var dominator = DominatorTraderModels.First();
                AutoCancelSettingsView settingsView = new()
                {
                    DataContext = new AutoCancelSettingsViewModel
                    {
                        AutoCancelConfig = autotraderSettings[dominator].AutoCancelConfig ?? new AutoCancelConfig(),
                    }
                };
                var view = new ThemedWindow
                {
                    Content = settingsView,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    SizeToContent = SizeToContent.WidthAndHeight,
                };
                view.Closed += async (s, e) => await SubmitAutoTraderSettingsAsync(dominator);
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenLooperSettings));
            }
        }

        private Task SubmitAutoTraderSettingsAsync(DominatorTraderModel dominator)
        {
            throw new NotImplementedException();
        }

        [Command]
        public void SaveAutoTraderConfig()
        {
            try
            {
                var dominator = DominatorTraderModels.First();
                SaveView view = new();
                SaveViewModel viewModel = view.DataContext as SaveViewModel;
                viewModel.LoadGroups(Module.Dominator);
                viewModel.ShowDefault = false;
                viewModel.Config = JsonConvert.SerializeObject(autotraderSettings[dominator], Formatting.Indented);
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAutoTraderConfig));
            }
        }

        private DelegateCommand openAutoTraderSettinsgRestoreCommand;
        public ICommand OpenAutoTraderSettinsgRestoreCommand => openAutoTraderSettinsgRestoreCommand ??= new DelegateCommand(OpenAutoTraderSettinsgRestore);

        private void OpenAutoTraderSettinsgRestore()
        {
        }

        private DelegateCommand createAutoTraderSettingsCommand;
        public ICommand CreateAutoTraderSettingsCommand => createAutoTraderSettingsCommand ??= new DelegateCommand(CreateAutoTraderSettings);

        private void CreateAutoTraderSettings()
        {
            var rows = TableService.Object.SelectedItems as ArrayList;
            foreach (var row in rows)
            {
                if (row is DominatorTraderModel dominator)
                {
                    var settings = new OmsAutoTraderSettings
                    {
                        Title = "TEST",
                        ConfigId = new Guid(),
                        AutomationConfigModel = new AutomationConfigModel(),
                        AutoCancelConfig = new AutoCancelConfig(),
                        FishLossConfig = new FishLossConfig(),
                        EdgeType = EdgeType.EdgeToBid,
                        EdgeValue = 0.01,
                        AutoTraderVenue = Venue.TB,
                    };
                    autotraderSettings.TryAdd(dominator, settings);
                }
            }
        }
        #endregion

    }
}
