using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using DevExpress.Mvvm.Native;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.LowLatency;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LowLatencyManagerViewModel : ModuleViewModelBase
    {
        private readonly OmsCore _omsCore;
        private readonly IAbstractFactory<LowLatencyModel> _lowLatencyModelFactory;
        private readonly HashSet<int> _loadedConfigs = new();
        private readonly Comms.Models.Data.Oms.Config.ConfigSave _allGenerator;

        private bool _configLoaded;
        private DispatcherTimer _updateTimer;
        private DelegateCommand<Tuple<ILowLatencyInstance, LowLatencyOrderModel>> _openHangCommand;

        public override Module Module { get; protected set; } = Module.LowLatencyManager;
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();

        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial string IsBusyMessage { get; set; }
        [Bindable]
        public partial int TotalShortPos { get; set; }
        [Bindable]
        public partial int TotalLongPos { get; set; }
        [Bindable]
        public partial int TotalWorkingShortPos { get; set; }
        [Bindable]
        public partial int TotalWorkingLongPos { get; set; }
        [Bindable]
        public partial double TotalRealPnl { get; set; }
        [Bindable]
        public partial double TotalUnrealPnl { get; set; }
        [Bindable]
        public partial double TotalNetPnl { get; set; }
        [Bindable]
        public partial ObservableCollection<LowLatencyHostModel> Hosts { get; set; }
        [Bindable]
        public partial ObservableCollection<LowLatencyModel> LowLatencyModels { get; set; }
        [Bindable]
        public partial ObservableCollection<InitiatorModel> Initiators { get; set; }
        [Bindable]
        public partial ObservableCollection<LoopModel> Loops { get; set; }
        [Bindable]
        public partial ObservableCollection<LiquidatorModel> Liquidators { get; set; }
        [Bindable]
        public partial ObservableCollection<SignalModel> Signals { get; set; }
        [Bindable]
        public partial ObservableCollection<LowLatencyRiskModel> RiskModels { get; set; }
        [Bindable]
        public partial ObservableCollection<Comms.Models.Data.Oms.Config.ConfigSave> WatchlistGenerators { get; set; }
        [Bindable]
        public partial int StartMinRank { get; set; }
        [Bindable]
        public partial int StopMaxRank { get; set; }
        [Bindable]
        public partial LowLatencyTransactionsProcessor LowLatencyTransactionsProcessor { get; set; }
        public ICommand OpenHangCommand
        {
            get
            {
                _openHangCommand ??= new DelegateCommand<Tuple<ILowLatencyInstance, LowLatencyOrderModel>>(OpenHang);
                return _openHangCommand;
            }
        }

        public LowLatencyManagerViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, IAbstractFactory<LowLatencyModel> lowLatencyModelFactory, LowLatencyTransactionsProcessor latencyTransactionsProcessor) : base(configBrowserViewModel, omsCore)
        {
            _omsCore = omsCore;
            _lowLatencyModelFactory = lowLatencyModelFactory;
            _allGenerator = new Comms.Models.Data.Oms.Config.ConfigSave() { Title = "All", Id = 0 };
            LowLatencyTransactionsProcessor = latencyTransactionsProcessor;
            Hosts = new();
            LowLatencyModels = new();
            Initiators = new();
            Loops = new();
            Liquidators = new();
            Signals = new();
            RiskModels = new();
            WatchlistGenerators = new() { _allGenerator };
        }

        public override void OnSetDispatcher()
        {
            _updateTimer = new DispatcherTimer(DispatcherPriority.ContextIdle, Dispatcher);
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += (_, _) => UpdateStats();
            _updateTimer.Start();
        }

        public override void OnDispose()
        {
            _updateTimer.Stop();
        }

        private void UpdateStats()
        {
           
            var totalRealPnL = 0.0;
            var totalUnrealPnL = 0.0;

            for (var index = LowLatencyModels.Count - 1; index >= 0; index--)
            {
                var model = LowLatencyModels[index];
                totalRealPnL += model.RealPnl;
                totalUnrealPnL += model.UnrealPnl;
            }

            TotalRealPnl = totalRealPnL;
            TotalUnrealPnl = totalUnrealPnL;
        }

        protected override async Task OnReadyAsync()
        {
            await base.OnReadyAsync();
            if (_configLoaded)
            {
                return;
            }
            _configLoaded = true;
            IsBusy = true;
            IsBusyMessage = "Loading Saved Configs...";
            await ReloadSavedConfigsCommand();
            IsBusyMessage = "Setting up instance...";
            Setup();
            IsBusy = false;
        }

        private void Setup()
        {
            Hosts.Add(new LowLatencyHostModel()
            {
                Name = "TIPPY",
                Address = "10.11.91.82"
            });
            Hosts.Add(new LowLatencyHostModel()
            {
                Name = "TRIPPY",
                Address = "10.11.91.83"
            });
            Hosts.Add(new LowLatencyHostModel()
            {
                Name = "ZIPPY",
                Address = "10.11.91.81"
            });
        }

        [Command]
        public void OpenManualAdjustmentCommand()
        {
            LowLatencyManualAdjustmentRequestView view = new LowLatencyManualAdjustmentRequestView();
            if (view.DataContext is LowLatencyManualAdjustmentRequestViewModel viewModel)
            {
                viewModel.Modules = LowLatencyModels.ToList();
                viewModel.Usernames = LowLatencyModels.Select(x => x.Username).Distinct().ToObservableCollection();
                view.Show();
            }
        }

        [Command]
        public void AddTemplateCommand(string type)
        {
            Module module;

            switch (type)
            {
                case "LoLaInitiator":
                    module = Module.LoLaInitiator;
                    break;
                case "LoLaLoop":
                    module = Module.LoLaLoop;
                    break;
                case "LoLaLiquidator":
                    module = Module.LoLaLiquidator;
                    break;
                case "LoLaSignal":
                    module = Module.LoLaSignal;
                    break;
                case "LoLaRisk":
                    module = Module.LoLaRisk;
                    break;
                default:
                    return;
            }

            IDynamicConfigModel model = GetModel(module);
            if (model != null)
            {
                model.Creator = _omsCore.User.Username;
                model.LastUpdateTime = DateTime.Now;
                model.Load();
                model.Id = 0;
                model.Details = null;
                LowLatencyConfigEditorView view = GetLoLaConfigEditor(module, model);
                view.ShowDialog();
                if (model.Details != null)
                {
                    _ = AddModelSafe(module, model);
                }
            }
        }

        [Command]
        public async Task ReloadSavedConfigsCommand()
        {
            var loadLoLaInitiator = LoadSavedConfigs(Module.LoLaInitiator);
            var loadLoLaLoop = LoadSavedConfigs(Module.LoLaLoop);
            var loadLoLaLiquidator = LoadSavedConfigs(Module.LoLaLiquidator);
            var loadLoLaSignal = LoadSavedConfigs(Module.LoLaSignal);
            var loadLoLaRisk = LoadSavedConfigs(Module.LoLaRisk);
            var loadGenerator = LoadWatchlistGeneratorConfigs();

            await Task.WhenAll(loadLoLaInitiator, loadLoLaLoop, loadLoLaLiquidator, loadLoLaSignal, loadLoLaRisk, loadGenerator);
        }

        private async Task LoadWatchlistGeneratorConfigs()
        {
            var items = await _omsCore.GatewayClient.RequestConfigsAsync((int)Module.SpreadsGenerator);
            if (items != null)
            {
                var group = Module.ToString().FromCamelCase();
                items = items.Where(x => x.Group == group).ToList();
                if (items.Any())
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        WatchlistGenerators.Clear();
                        WatchlistGenerators.Add(_allGenerator);
                        foreach (var config in items)
                        {
                            WatchlistGenerators.Add(config);
                        }
                    });
                }
            }
        }

        private async Task LoadSavedConfigs(Module moduleType)
        {
            var items = await _omsCore.GatewayClient.RequestConfigsAsync((int)moduleType);
            if (items != null && items.Any())
            {
                await LoadConfigs(moduleType, items);
            }
        }

        private async Task LoadConfigs(Module moduleType, List<Comms.Models.Data.Oms.Config.ConfigSave> items)
        {
            try
            {
                List<IDynamicConfigModel> models = new();
                foreach (var id in items.Select(x => x.Id).Where(x => !_loadedConfigs.Contains(x)))
                {
                    try
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave item = await OmsCore.GatewayClient.RequestConfigDataAsync(id);
                        if (item != null)
                        {
                            _loadedConfigs.Add(item.Id);
                            IDynamicConfigModel model = GetModel(moduleType);
                            if (model != null)
                            {
                                model.Id = item.Id;
                                model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(item));
                                model.Title = item.Title;
                                model.Creator = item.Username;
                                model.LastUpdateTime = item.SaveTime;
                                model.Load();
                                models.Add(model);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(LoadSavedConfigs) + $" Type: {moduleType}, Id: {id}");
                    }
                }
                await AddMultipleModelsSafe(moduleType, models);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadSavedConfigs) + $" Type: {moduleType}");
            }
        }

        private IDynamicConfigModel GetClone(IDynamicConfigModel template, Module module, bool exactCopy = false)
        {
            template.Save();
            IDynamicConfigModel model = GetModel(module);
            CopyFromTemplate(model, template, exactCopy);
            return model;
        }

        private void CopyFromTemplate(IDynamicConfigModel model, IDynamicConfigModel template, bool exactCopy)
        {
            if (model != null && template != null)
            {
                model.Id = template.Id;
                model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(template.Details));
                model.Title = template.Title;
                model.Creator = template.Creator;
                model.LastUpdateTime = template.LastUpdateTime;
                model.Load();
                if (!exactCopy)
                {
                    model.Id = 0;
                    model.Details = null;
                    model.Title += " Clone";
                    model.Creator = _omsCore.User.Username;
                    model.LastUpdateTime = DateTime.Now;
                }
            }
        }

        private IDynamicConfigModel GetModel(Module moduleType)
        {
            var currentBroker = OmsCore.Config.DefaultBroker;
            var routeLookup = _omsCore.OrderClient?.RouteLookup;
            var routes = !string.IsNullOrWhiteSpace(currentBroker)
                ? (routeLookup?.GetRoutesForBroker(currentBroker) ?? Array.Empty<string>())
                : (routeLookup?.GetRoutes() ?? Array.Empty<string>());
            switch (moduleType)
            {
                case Module.LoLaInitiator:
                    return new InitiatorModel(routes);
                case Module.LoLaLoop:
                    return new LoopModel();
                case Module.LoLaLiquidator:
                    return new LiquidatorModel(routes);
                case Module.LoLaRisk:
                    return new LowLatencyRiskModel();
                case Module.LoLaSignal:
                    return new SignalModel();
            }

            return null;
        }

        private async Task AddMultipleModelsSafe(Module moduleType, List<IDynamicConfigModel> models)
        {
            if (models.Any())
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    foreach (var dynamicConfigModel in models)
                    {
                        AddModel(moduleType, dynamicConfigModel);
                    }
                });
            }
        }

        private async Task AddModelSafe(Module moduleType, IDynamicConfigModel dynamicConfigModel)
        {
            await Dispatcher.BeginInvoke(() => AddModel(moduleType, dynamicConfigModel));
        }

        private void AddModel(Module moduleType, IDynamicConfigModel dynamicConfigModel)
        {
            switch (moduleType)
            {
                case Module.LoLaInitiator:
                    InitiatorModel initiatorModel = (InitiatorModel)dynamicConfigModel;
                    Initiators.Add(initiatorModel);
                    foreach (var instance in LowLatencyModels)
                    {
                        var copy = GetClone(initiatorModel, moduleType, exactCopy: true);
                        instance.Initiators.Add((InitiatorModel)copy);
                    }
                    break;
                case Module.LoLaLoop:
                    LoopModel loopModel = (LoopModel)dynamicConfigModel;
                    Loops.Add(loopModel);
                    foreach (var instance in LowLatencyModels)
                    {
                        var copy = GetClone(loopModel, moduleType, exactCopy: true);
                        instance.Loops.Add((LoopModel)copy);
                    }
                    break;
                case Module.LoLaLiquidator:
                    LiquidatorModel liquidatorModel = (LiquidatorModel)dynamicConfigModel;
                    Liquidators.Add(liquidatorModel);
                    foreach (var instance in LowLatencyModels)
                    {
                        var copy = GetClone(liquidatorModel, moduleType, exactCopy: true);
                        instance.Liquidators.Add((LiquidatorModel)copy);
                    }
                    break;
                case Module.LoLaRisk:
                    LowLatencyRiskModel loqLatencyRiskModel = (LowLatencyRiskModel)dynamicConfigModel;
                    RiskModels.Add(loqLatencyRiskModel);
                    foreach (var instance in LowLatencyModels)
                    {
                        var copy = GetClone(loqLatencyRiskModel, moduleType, exactCopy: true);
                        instance.RiskModels.Add((LowLatencyRiskModel)copy);
                    }
                    break;
                case Module.LoLaSignal:
                    SignalModel signalModel = (SignalModel)dynamicConfigModel;
                    Signals.Add(signalModel);
                    foreach (var instance in LowLatencyModels)
                    {
                        var copy = GetClone(signalModel, moduleType, exactCopy: true);
                        instance.Signals.Add((SignalModel)copy);
                    }
                    break;
            }
        }

        [Command]
        public void AddCommand()
        {
            LowLatencyModel lowLatencyModel = _lowLatencyModelFactory?.Create();
            if (lowLatencyModel != null)
            {
                CopyTemplates(lowLatencyModel);
                LowLatencyModels.Add(lowLatencyModel);
            }
        }

        [AsyncCommand(UseCommandManager = false)]
        public async Task RefreshAllCommand()
        {
            await Task.WhenAll(LowLatencyModels.Select(x => x.RefreshAsync()));
        }

        private void CopyTemplates(LowLatencyModel lowLatencyModel)
        {
            foreach (var template in Initiators)
            {
                var model = GetClone(template, Module.LoLaInitiator, exactCopy: true);
                lowLatencyModel.Initiators.Add((InitiatorModel)model);
            }

            foreach (var template in Loops)
            {
                var model = GetClone(template, Module.LoLaLoop, exactCopy: true);
                lowLatencyModel.Loops.Add((LoopModel)model);
            }

            foreach (var template in Liquidators)
            {
                var model = GetClone(template, Module.LoLaLiquidator, exactCopy: true);
                lowLatencyModel.Liquidators.Add((LiquidatorModel)model);
            }

            foreach (var template in Signals)
            {
                var model = GetClone(template, Module.LoLaSignal, exactCopy: true);
                lowLatencyModel.Signals.Add((SignalModel)model);
            }

            foreach (var template in RiskModels)
            {
                var model = GetClone(template, Module.LoLaRisk, exactCopy: true);
                lowLatencyModel.RiskModels.Add((LowLatencyRiskModel)model);
            }
        }

        [Command]
        public void StopAllCommand()
        {
            List<LowLatencyModel> instances = LowLatencyModels.ToList();
            StopMultipleInstances(instances);
        }

        [Command]
        public void KillAllCommand()
        {
            List<LowLatencyModel> instances = LowLatencyModels.ToList();
            StopMultipleInstances(instances, killAll: true);
        }

        [Command]
        public void KillByHostCommand(LowLatencyHostModel host)
        {
            List<LowLatencyModel> instances = LowLatencyModels.Where(x => x.Host == host.Name || x.Host == host.Address).ToList();
            StopMultipleInstances(instances);
        }

        [Command]
        public void CloneCommand(LowLatencyModel model)
        {
            if (model is { LatencyInstance: not null })
            {
                var instanceConfig = model.GetConfig();
                LowLatencyModel lowLatencyModel = _lowLatencyModelFactory?.Create();
                if (lowLatencyModel != null)
                {
                    lowLatencyModel.Name = instanceConfig.Name;
                    lowLatencyModel.Rank = instanceConfig.Rank;

                    CopyTemplates(lowLatencyModel);

                    lowLatencyModel.Initiator = lowLatencyModel.Initiators.FirstOrDefault(x => x.Id == instanceConfig.InitiatorId);
                    lowLatencyModel.Loop = lowLatencyModel.Loops.FirstOrDefault(x => x.Id == instanceConfig.LoopId);
                    lowLatencyModel.Liquidator = lowLatencyModel.Liquidators.FirstOrDefault(x => x.Id == instanceConfig.LiquidatorId);
                    lowLatencyModel.Signal = lowLatencyModel.Signals.FirstOrDefault(x => x.Id == instanceConfig.SignalId);
                    lowLatencyModel.Risk = lowLatencyModel.RiskModels.FirstOrDefault(x => x.Id == instanceConfig.RiskId);
                    lowLatencyModel.WatchlistGenerator = WatchlistGenerators.FirstOrDefault(x => x.Id == instanceConfig.WatchlistGenerator);

                    lowLatencyModel.SetUsername();

                    LowLatencyModels.Add(lowLatencyModel);
                }
            }
        }

        [Command]
        public void ClearNotificationCommand(LowLatencyModel model)
        {
            if (model != null)
            {
                model.ShowNotification = false;
            }
        }

        [Command]
        public void OpenHangsCommand(LowLatencyModel model)
        {
            if (model is { LatencyInstance: not null })
            {
                LowLatencyTransactionsProcessor.OpenHangsForInstance(model.LatencyInstance);
            }
        }

        [Command]
        public void RunFromFileCommand(LowLatencyModel model)
        {
            OpenFileDialogService.Filter = "Log file|*.LOG";
            var dialogResult = OpenFileDialogService.ShowDialog();
            if (!dialogResult)
            {
                return;
            }
            IFileInfo file = OpenFileDialogService.Files.First();
            var filePath = file.GetFullName();
            model.LoadFromFile(filePath);
        }

        [Command]
        public void StartByRankCommand()
        {
            var startMinRank = 5 - StartMinRank;
            StartMinRank = 0;
            HashSet<string> runningNames = LowLatencyModels.Where(x => x.IsRunning).Select(x => x.Name).ToHashSet();
            List<LowLatencyModel> instances = LowLatencyModels.Where(x => x.CheckForCanStart(out _) && x.Rank >= startMinRank && !runningNames.Contains(x.Name)).DistinctBy(x => x.Name).ToList();
            if (instances.Any())
            {
                var res = MessageBoxService.ShowMessage($"Are you sure you want to start all instances with Rank {startMinRank} and up? ({instances.Count}) instances", "Low Latency Manager", MessageButton.YesNo, MessageIcon.Question, MessageResult.No);
                if (res == MessageResult.Yes)
                {
                    foreach (var item in instances)
                    {
                        if (!item.IsRunning)
                        {
                            StartCommand(item);
                        }
                    }
                }
            }
            else
            {
                MessageBoxService.ShowMessage($"No instances match criteria min Rank {startMinRank}!", "Low Latency Manager", MessageButton.OK, MessageIcon.Information, MessageResult.OK);
            }
        }

        [Command]
        public void StopByRankCommand()
        {
            var stopRank = StopMaxRank;
            StopMaxRank = 0;
            List<LowLatencyModel> instances = LowLatencyModels.Where(x => x.Rank <= stopRank).ToList();
            StopMultipleInstances(instances);
        }

        [Command]
        public void FlipMuteLoopsCommand()
        {
            LowLatencyTransactionsProcessor.AudioMuted = !LowLatencyTransactionsProcessor.AudioMuted;
        }

        [Command]
        public void FlipOpenTicketCommand()
        {
            LowLatencyTransactionsProcessor.DisableOpenTicket = !LowLatencyTransactionsProcessor.DisableOpenTicket;
        }

        [Command]
        public async Task RefreshCommand(LowLatencyModel model)
        {
            await model.RefreshAsync();
        }

        [Command]
        public void StartCommand(LowLatencyModel model)
        {
            if (!model.CheckForCanStart(out var message))
            {
                MessageBoxService.ShowMessage($"Instance can not start {model.Name}\n{message}", "Low Latency Manager", MessageButton.OK, MessageIcon.Warning);
                return;
            }

            if (model.Signal.Signals.Any(x => x.InitialOrderQty > 1))
            {
                var res = MessageBoxService.ShowMessage($"You are about to start an instance with an initial qty of {model.Signal.Signals.Max(x => x.InitialOrderQty)}.\nAre you sure you want to proceed?", $"{model.Name} - Low Latency Manager", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
                if (res != MessageResult.Yes)
                {
                    return;
                }
            }

            if (model.RunInTestMode)
            {
                var res = MessageBoxService.ShowMessage($"You are about to start an instance in TEST MODE.\nAre you sure you want to proceed?", $"{model.Name} - Low Latency Manager", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
                if (res != MessageResult.Yes)
                {
                    return;
                }
            }

            LowLatencyModel other = LowLatencyModels.FirstOrDefault(x => x.IsRunning && x != model && x.Name == model.Name);
            if (other != null)
            {
                MessageBoxService.ShowMessage($"Another instance '{other.Username}' is already running with the same underlying '{model.Name}'!", $"{model.Name} - Low Latency Manager", MessageButton.OK, MessageIcon.Error, MessageResult.OK);
                return;
            }

            model.Start();
        }

        [Command]
        public void StopCommand(LowLatencyModel model)
        {
            model.Stop(killAll: false);
        }

        [Command]
        public void KillAllForInstanceCommand(LowLatencyModel model)
        {
            model.Stop(killAll: false);
        }

        [Command]
        public void RemoveInstanceCommand(LowLatencyModel model)
        {
            var res = MessageBoxService.ShowMessage($"Are you sure you want to remove{(string.IsNullOrWhiteSpace(model.Name) ? "" : " " + model.Name)} instance?", "Low Latency Manager", MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
            if (res == MessageResult.Yes)
            {
                model.Stop(killAll: false);
                LowLatencyModels.Remove(model);
            }
        }

        [Command]
        public void InitiatorTemplateChangedCommand(LowLatencyModel model)
        {
            model?.InitiatorChanged();
        }

        [Command]
        public void LiquidatorTemplateChangedCommand(LowLatencyModel model)
        {
            model?.LiquidatorChanged();
        }

        [Command]
        public void SignalTemplateChangedCommand(LowLatencyModel model)
        {
            model?.SignalChanged();
        }

        [Command]
        public void RiskTemplateChangedCommand(LowLatencyModel model)
        {
            model?.RiskChanged();
        }

        [Command]
        public void EditInitiatorCommand(InitiatorModel model)
        {
            EditTemplate(Module.LoLaInitiator, model);
        }

        [Command]
        public void CloneInitiatorCommand(InitiatorModel model)
        {
            CloneTemplate(Module.LoLaInitiator, model);
        }

        [Command]
        public void EditLoopCommand(LoopModel model)
        {
            EditTemplate(Module.LoLaLoop, model);
        }

        [Command]
        public void CloneLoopCommand(LoopModel model)
        {
            CloneTemplate(Module.LoLaLoop, model);
        }

        [Command]
        public void EditRiskCommand(LowLatencyRiskModel model)
        {
            EditTemplate(Module.LoLaRisk, model);
        }

        [Command]
        public void CloneRiskCommand(LowLatencyRiskModel model)
        {
            CloneTemplate(Module.LoLaRisk, model);
        }

        [Command]
        public void EditLiquidatorCommand(LiquidatorModel model)
        {
            EditTemplate(Module.LoLaLiquidator, model);
        }

        [Command]
        public void CloneLiquidatorCommand(LiquidatorModel model)
        {
            CloneTemplate(Module.LoLaLiquidator, model);
        }

        [Command]
        public void EditSignalCommand(SignalModel model)
        {
            EditTemplate(Module.LoLaSignal, model);
        }

        [Command]
        public void CloneSignalCommand(SignalModel model)
        {
            CloneTemplate(Module.LoLaSignal, model);
        }

        public void OpenHang(Tuple<ILowLatencyInstance, LowLatencyOrderModel> par)
        {
            ILowLatencyInstance instance = par.Item1;
            LowLatencyOrderModel model = par.Item2;
            LowLatencyTransactionsProcessor.OpenInComplexTicket(model, instance);
        }

        private void EditTemplate(Module module, IDynamicConfigModel template)
        {
            if (TryGetBaseModel(module, template.Id, out var baseTemplate))
            {
                template = baseTemplate;
            }

            LowLatencyConfigEditorView view = GetLoLaConfigEditor(module, template);
            view.ShowDialog();
        }

        private void CloneTemplate(Module module, IDynamicConfigModel template)
        {
            if (TryGetBaseModel(module, template.Id, out var baseTemplate))
            {
                template = baseTemplate;
            }
            var model = GetClone(template, module);
            if (model == null)
            {
                return;
            }
            LowLatencyConfigEditorView view = GetLoLaConfigEditor(module, model);
            view.ShowDialog();
        }

        private bool TryGetBaseModel(Module moduleType, int id, out IDynamicConfigModel dynamicConfigModel)
        {
            if (id <= 0)
            {
                dynamicConfigModel = null;
                return false;
            }

            switch (moduleType)
            {
                case Module.LoLaInitiator:
                    dynamicConfigModel = Initiators.FirstOrDefault(x => x.Id == id);
                    break;
                case Module.LoLaLoop:
                    dynamicConfigModel = Loops.FirstOrDefault(x => x.Id == id);
                    break;
                case Module.LoLaLiquidator:
                    dynamicConfigModel = Liquidators.FirstOrDefault(x => x.Id == id);
                    break;
                case Module.LoLaRisk:
                    dynamicConfigModel = RiskModels.FirstOrDefault(x => x.Id == id);
                    break;
                case Module.LoLaSignal:
                    dynamicConfigModel = Signals.FirstOrDefault(x => x.Id == id);
                    break;
                default:
                    dynamicConfigModel = null;
                    break;
            }

            return dynamicConfigModel != null;
        }

        private static void StopMultipleInstances(List<LowLatencyModel> instances, bool killAll = false)
        {
            foreach (var item in instances.Where(item => item.IsRunning))
            {
                item.Stop(killAll);
            }
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            try
            {
                LowLatencyManagerConfig config = await ModuleConfigBase.DeserializeAsync<LowLatencyManagerConfig>(configJson);
                if (config != null)
                {
                    var models = new List<LowLatencyModel>();
                    foreach (var instanceConfig in config.LowLatencyModelConfigs)
                    {
                        LowLatencyModel lowLatencyModel = _lowLatencyModelFactory?.Create();
                        if (lowLatencyModel != null)
                        {
                            lowLatencyModel.Name = instanceConfig.Name;
                            lowLatencyModel.Rank = instanceConfig.Rank;

                            lowLatencyModel.InstanceId = instanceConfig.InstanceId;
                            lowLatencyModel.Username = instanceConfig.Username;

                            CopyTemplates(lowLatencyModel);

                            lowLatencyModel.Initiator = lowLatencyModel.Initiators.FirstOrDefault(x => x.Id == instanceConfig.InitiatorId);
                            lowLatencyModel.Loop = lowLatencyModel.Loops.FirstOrDefault(x => x.Id == instanceConfig.LoopId);
                            lowLatencyModel.Liquidator = lowLatencyModel.Liquidators.FirstOrDefault(x => x.Id == instanceConfig.LiquidatorId);
                            lowLatencyModel.Signal = lowLatencyModel.Signals.FirstOrDefault(x => x.Id == instanceConfig.SignalId);
                            lowLatencyModel.Risk = lowLatencyModel.RiskModels.FirstOrDefault(x => x.Id == instanceConfig.RiskId);
                            lowLatencyModel.WatchlistGenerator = WatchlistGenerators.FirstOrDefault(x => x.Id == instanceConfig.WatchlistGenerator);

                            models.Add(lowLatencyModel);
                        }
                    }

                    if (models.Any())
                    {
                        var maxId = models.Max(x => x.InstanceId);
                        LowLatencyInstance.UpdateNextInstanceId(maxId);

                        foreach (var model in models)
                        {
                            model.VerifyUsername();
                        }

                        await Dispatcher.BeginInvoke(() =>
                        {
                            LowLatencyModels.Clear();
                            foreach (var instance in models)
                            {
                                LowLatencyModels.Add(instance);
                            }
                        });
                    }

                    LowLatencyTransactionsProcessor.AudioMuted = config.AudioMuted;
                    LowLatencyTransactionsProcessor.DisableOpenTicket = config.DisableOpenTicket;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeserializeAndLoadConfig));
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            LowLatencyManagerConfig config = new()
            {
                AudioMuted = LowLatencyTransactionsProcessor.AudioMuted,
                DisableOpenTicket = LowLatencyTransactionsProcessor.DisableOpenTicket,
                LowLatencyModelConfigs = LowLatencyModels.Select(x => x.GetConfig()).ToList()
            };
            return config.Serialize();
        }

        private LowLatencyConfigEditorView GetLoLaConfigEditor(Module module, IDynamicConfigModel model)
        {
            LowLatencyConfigEditorView view = new();
            LowLatencyConfigEditorViewModel viewModel = view.ViewModel;
            IDynamicConfigModel copy = GetClone(model, module, exactCopy: true);
            if (viewModel != null)
            {
                viewModel.Model = copy;
                viewModel.OriginalTitle = copy.Title;
                viewModel.UsedByMultiple = IsBeingUsedByMultipleInstances(module, copy);

                view.SetupConfigView(module);
                viewModel.Module = module;
                viewModel.ConfigBrowserViewModel.SetDispatcher(view.Dispatcher);
                viewModel.ConfigBrowserViewModel.SetModule(module);
                viewModel.ConfigSavedEvent += OnConfigUpdate;
                viewModel.ConfigDeletedEvent += OnConfigDelete;
            }
            return view;
        }

        private void OnConfigUpdate(Module module, IDynamicConfigModel model, bool isNew)
        {
            try
            {
                if (isNew)
                {
                    _ = AddModelSafe(module, model);
                }
                else
                {
                    List<IDynamicConfigModel> instances;
                    switch (module)
                    {
                        case Module.LoLaInitiator:
                            instances = LowLatencyModels.SelectMany(x => x.Initiators).Where(x => x != null && x.Id == model.Id)
                                .Union(Initiators.Where(x => x.Id == model.Id)).Cast<IDynamicConfigModel>().ToList();
                            break;
                        case Module.LoLaLoop:
                            instances = LowLatencyModels.SelectMany(x => x.Loops).Where(x => x != null && x.Id == model.Id)
                                .Union(Loops.Where(x => x.Id == model.Id)).Cast<IDynamicConfigModel>().ToList();
                            break;
                        case Module.LoLaLiquidator:
                            instances = LowLatencyModels.SelectMany(x => x.Liquidators).Where(x => x != null && x.Id == model.Id)
                                .Union(Liquidators.Where(x => x.Id == model.Id)).Cast<IDynamicConfigModel>().ToList();
                            break;
                        case Module.LoLaSignal:
                            instances = LowLatencyModels.SelectMany(x => x.Signals).Where(x => x != null && x.Id == model.Id)
                                .Union(Signals.Where(x => x.Id == model.Id)).Cast<IDynamicConfigModel>().ToList();
                            break;
                        case Module.LoLaRisk:
                            instances = LowLatencyModels.SelectMany(x => x.RiskModels).Where(x => x != null && x.Id == model.Id)
                                .Union(RiskModels.Where(x => x.Id == model.Id)).Cast<IDynamicConfigModel>().ToList();
                            break;
                        default:
                            return;
                    }

                    foreach (var instance in instances)
                    {
                        CopyFromTemplate(instance, model, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnConfigUpdate));
            }
        }

        private void OnConfigDelete(Module module, Comms.Models.Data.Oms.Config.ConfigSave config)
        {
            try
            {
                Dispatcher?.BeginInvoke(() => FindAndRemove(module, config.Id));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnConfigUpdate));
            }
        }

        private void FindAndRemove(Module module, int id)
        {
            foreach (var instModel in LowLatencyModels)
            {
                switch (module)
                {
                    case Module.LoLaInitiator:
                        FindAndRemoveInitator(id, instModel);
                        break;
                    case Module.LoLaLoop:
                        FindAndRemoveLoop(id, instModel);
                        break;
                    case Module.LoLaLiquidator:
                        FindAndRemoveLiquidator(id, instModel);
                        break;
                    case Module.LoLaSignal:
                        FindAndRemoveSignal(id, instModel);
                        break;
                    case Module.LoLaRisk:
                        FindAndRemoveRisk(id, instModel);
                        break;
                }
            }
        }

        private void FindAndRemoveInitator(int id, LowLatencyModel instModel)
        {
            foreach (InitiatorModel item in instModel.Initiators.Where(x => x != null && x.Id == id).ToList())
            {
                if (item == instModel.Initiator)
                {
                    if (instModel.IsRunning)
                    {
                        instModel.Stop(false);
                    }
                    instModel.Initiator = null;
                }
                instModel.Initiators.Remove(item);
            }

            foreach (InitiatorModel item in Initiators.Where(x => x != null && x.Id == id).ToList())
            {
                Initiators.Remove(item);
            }
        }

        private void FindAndRemoveLoop(int id, LowLatencyModel instModel)
        {
            foreach (LoopModel item in instModel.Loops.Where(x => x != null && x.Id == id).ToList())
            {
                if (item == instModel.Loop)
                {
                    if (instModel.IsRunning)
                    {
                        instModel.Stop(false);
                    }
                    instModel.Loop = null;
                }
                instModel.Loops.Remove(item);
            }

            foreach (LoopModel item in Loops.Where(x => x != null && x.Id == id).ToList())
            {
                Loops.Remove(item);
            }
        }

        private void FindAndRemoveLiquidator(int id, LowLatencyModel instModel)
        {
            foreach (LiquidatorModel item in instModel.Liquidators.Where(x => x != null && x.Id == id).ToList())
            {
                if (item == instModel.Liquidator)
                {
                    if (instModel.IsRunning)
                    {
                        instModel.Stop(false);
                    }
                    instModel.Liquidator = null;
                }
                instModel.Liquidators.Remove(item);
            }

            foreach (LiquidatorModel item in Liquidators.Where(x => x != null && x.Id == id).ToList())
            {
                Liquidators.Remove(item);
            }
        }

        private void FindAndRemoveSignal(int id, LowLatencyModel instModel)
        {
            foreach (SignalModel item in instModel.Signals.Where(x => x != null && x.Id == id).ToList())
            {
                if (item == instModel.Signal)
                {
                    if (instModel.IsRunning)
                    {
                        instModel.Stop(false);
                    }
                    instModel.Signal = null;
                }
                instModel.Signals.Remove(item);
            }

            foreach (SignalModel item in Signals.Where(x => x != null && x.Id == id).ToList())
            {
                Signals.Remove(item);
            }
        }

        private void FindAndRemoveRisk(int id, LowLatencyModel instModel)
        {
            foreach (LowLatencyRiskModel item in instModel.RiskModels.Where(x => x != null && x.Id == id).ToList())
            {
                if (item == instModel.Risk)
                {
                    if (instModel.IsRunning)
                    {
                        instModel.Stop(false);
                    }
                    instModel.Risk = null;
                }
                instModel.RiskModels.Remove(item);
            }

            foreach (LowLatencyRiskModel item in RiskModels.Where(x => x != null && x.Id == id).ToList())
            {
                RiskModels.Remove(item);
            }
        }

        private bool IsBeingUsedByMultipleInstances(Module module, IDynamicConfigModel model)
        {
            try
            {
                switch (module)
                {
                    case Module.LoLaInitiator:
                        return LowLatencyModels.Count(x => x.Initiator != null && x.Initiator.Id == model.Id) > 1;
                    case Module.LoLaLoop:
                        return LowLatencyModels.Count(x => x.Loop != null && x.Loop.Id == model.Id) > 1;
                    case Module.LoLaLiquidator:
                        return LowLatencyModels.Count(x => x.Liquidator != null && x.Liquidator.Id == model.Id) > 1;
                    case Module.LoLaSignal:
                        return LowLatencyModels.Count(x => x.Signal != null && x.Signal.Id == model.Id) > 1;
                    case Module.LoLaRisk:
                        return LowLatencyModels.Count(x => x.Risk != null && x.Risk.Id == model.Id) > 1;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(IsBeingUsedByMultipleInstances));
                return false;
            }
        }
    }
}
