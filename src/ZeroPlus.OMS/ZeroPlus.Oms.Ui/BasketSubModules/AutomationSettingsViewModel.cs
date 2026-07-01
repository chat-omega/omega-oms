using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;
using AutoTraderConfig = ZeroPlus.Models.Data.Update.AutoTraderConfig;
using ConfigSaveComms = ZeroPlus.Comms.Models.Data.Oms.Config.ConfigSave;
using ConfigSaveSbe = ZeroPlus.Models.Data.Configs.ConfigSave;
using Venue = ZeroPlus.Models.Data.Enums.Venue;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class AutomationSettingsViewModel : ViewModelBase, IAutomationTrader, IDynamicConfigParentModule
    {
        private static readonly ILogger _log = NLog.LogManager.GetCurrentClassLogger();
        protected readonly OmsCore _omsCore;
        public OmsCore OmsCore => _omsCore;
        public readonly string name;
        public static LoopCloseEdgeType[] LoopCloseEdgeTypes { get; } = (LoopCloseEdgeType[])Enum.GetValues(typeof(LoopCloseEdgeType));
        public static LoopIntervalType[] LoopIntervalTypes { get; } = (LoopIntervalType[])Enum.GetValues(typeof(LoopIntervalType));
        public static LoopSizeupType[] LoopSizeupTypes { get; } = (LoopSizeupType[])Enum.GetValues(typeof(LoopSizeupType));
        public static LoopIncrementType[] LoopIncrementTypes { get; } = (LoopIncrementType[])Enum.GetValues(typeof(LoopIncrementType));
        public static LoopPricingMode[] LoopPricingModes { get; } = (LoopPricingMode[])Enum.GetValues(typeof(LoopPricingMode));
        public ClosingTypes[] ClosingTypes { get; } = (ClosingTypes[])Enum.GetValues(typeof(ClosingTypes));

        private readonly Module _module;

        string ConfigId { get; set; } = new Guid().ToString();
        public IList<string> RoutesList { get; init; }
        public IList<string> DmaRoutesList { get; init; }
        public IList<string> SorRoutesList { get; init; }

        public AutomationSettingsViewModel(string moduleName, Module module, OmsCore omsCore)
        {
            name = moduleName;
            _module = module;
            _omsCore = omsCore;
        }
        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public IWindowService AdvancedWindowService => GetService<IWindowService>("Advanced");
        [Bindable(Default = Venue.TB)]
        public partial Venue AutoTraderVenue { get; set; }
        [Bindable]
        public partial bool ShowAdvancedRouteSettings { get; set; }

        #region IAutomationTrader
        [Bindable]
        public partial ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> UnderlyingToAutomationConfigModelLookup { get; set; }
        [Bindable]
        public partial ObservableCollection<AutomationConfigModel> AutomationConfigModels { get; set; }
        [Bindable]
        public partial AutomationConfigModel AutomationConfig { get; set; }
        public AutomationConfigModel GetAutomationConfig(string underlying = null, double increment = 0)
        {
            increment = Math.Abs(Math.Round(increment, 2));
            Tuple<string, double> key = new(underlying, increment);
            if (!string.IsNullOrWhiteSpace(underlying) &&
                UnderlyingToAutomationConfigModelLookup.TryGetValue(key, out AutomationConfigModel config) &&
                config is AutomationConfigModel automationConfigModel) return automationConfigModel;
            return _automationConfig;
        }

        #endregion

        #region Commands
        private DelegateCommand showLoopCloseEdgeConfigPanelCommand;
        public ICommand ShowLoopCloseEdgeConfigPanelCommand => showLoopCloseEdgeConfigPanelCommand ??= new DelegateCommand(ShowLoopCloseEdgeConfigPanel);
        private void ShowLoopCloseEdgeConfigPanel()
        {
            try
            {
                Views.DynamicEdgeManagementView view = new();
                if (view.DataContext is DynamicEdgeManagementViewModel viewModel)
                {
                    viewModel.ParentBasket = this;
                    view.Closed += (s, e) => ReloadDynamicEdgeConfig();
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopCloseEdgeConfigPanelCommand));
            }
        }
        private async void ReloadDynamicEdgeConfig(AutomationConfigModel automationConfig = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig is not null
                    && automationConfig.DynamicEdgeModelId > 0
                    && await _omsCore.GatewayClient.RequestConfigDataAsync(automationConfig.DynamicEdgeModelId) is ConfigSaveComms details)
                {
                    DynamicEdgeModel model = LoadDynamicEdgeModel(details);
                    automationConfig.DynamicEdgeModel = model;
                    automationConfig.DynamicEdgeModel.Id = automationConfig.DynamicEdgeModelId;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicEdgeConfig));
            }
        }
        private static DynamicEdgeModel LoadDynamicEdgeModel(ConfigSaveComms details)
        {
            DynamicEdgeModel model = JsonConvert.DeserializeObject<DynamicEdgeModel>(details.ConfigJson);
            model.Details = ConvertConfigSaveCommsToConfigSaveModel(details);
            model.Load();
            return model;
        }

        private static ConfigSaveSbe ConvertConfigSaveCommsToConfigSaveModel(ConfigSaveComms configSaveComms)
        {
            string detailsJson = JsonConvert.SerializeObject(configSaveComms);
            return JsonConvert.DeserializeObject<ConfigSaveSbe>(detailsJson);
        }


        private DelegateCommand configAutomationConfigCommand;
        public ICommand ConfigAutomationConfigCommand => configAutomationConfigCommand ??= new DelegateCommand(ConfigAutomationConfig);
        private void ConfigAutomationConfig()
        {
            try
            {
                List<AutomationConfigModel> automationConfigModels = AutomationConfigModels.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
                List<AutomationConfigMap> list = GetCurrentMappings();

                AutomationConfigMappingView view = new();
                if (view.DataContext is AutomationConfigMappingViewModel viewModel)
                {
                    viewModel.Configs.AddRange(list);
                    viewModel.AutomationConfigs.AddRange(automationConfigModels);
                    viewModel.ApplyConfigHandler = (ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> config) =>
                    {
                        if (config != null) UnderlyingToAutomationConfigModelLookup = config;
                    };
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ConfigAutomationConfigCommand));
            }
        }
        private List<AutomationConfigMap> GetCurrentMappings()
        {
            List<AutomationConfigMap> list = new();
            if (UnderlyingToAutomationConfigModelLookup != null)
            {
                foreach (var ((underlying, inc), automationConfig) in UnderlyingToAutomationConfigModelLookup)
                {
                    if (!string.IsNullOrWhiteSpace(underlying) && automationConfig != null)
                    {
                        AutomationConfigMap automationConfigMap = new()
                        {
                            Underlyings = underlying,
                            Increment = inc,
                            AutomationConfig = automationConfig,
                        };
                        list.Add(automationConfigMap);
                    }
                }
            }
            return list;
        }

        private DelegateCommand showLoopAdvancedConfigsCommand;
        public ICommand ShowLoopAdvancedConfigsCommand => showLoopAdvancedConfigsCommand ??= new DelegateCommand(ShowLoopAdvancedConfigs);

        private void ShowLoopAdvancedConfigs()
        {
            try
            {
                AdvancedWindowService.Show(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopAdvancedConfigsCommand));
            }
        }

        private DelegateCommand saveAutomationConfigCommand;
        public ICommand SaveAutomationConfigCommand => saveAutomationConfigCommand ??= new DelegateCommand(SaveAutomationConfig);

        private void SaveAutomationConfig()
        {
            SaveView view = new();
            if (view.DataContext is SaveViewModel viewModel)
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                viewModel.ShowDefault = true;
                viewModel.ShowGroup = false;
                viewModel.ShowLocation = false;
                viewModel.Title = automationConfig.Title ?? new Guid().ToString();
                viewModel.SetDispatcher(view.Dispatcher);

                view.ShowDialog();

                if (viewModel.Success)
                {
                    automationConfig.Title = viewModel.Title;
                    if (AutomationConfigModels.Select(x => x.Title).Contains(automationConfig.Title)) MessageBoxService.ShowMessage("Invalid Automation Config Title");
                    else AutomationConfigModels.Add(automationConfig);
                    AutomationConfigModel.SaveConfig(AutomationConfigModels, ref _automationConfigModels);
                }
            }
        }


        private DelegateCommand showDynamicIntervalConfigPanelCommand;
        public ICommand ShowDynamicIntervalConfigPanelCommand => showDynamicIntervalConfigPanelCommand ??= new DelegateCommand(ShowDynamicIntervalConfigPanel);

        private void ShowDynamicIntervalConfigPanel()
        {
            try
            {
                DynamicIntervalManagementView view = new();
                if (view.DataContext is DynamicIntervalManagementViewModel viewModel)
                {
                    viewModel.AutomationTrader = this;
                    view.Closed += (s, e) => ReloadDynamicIntervalConfig();
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowDynamicIntervalConfigPanelCommand));
            }
        }

        private async void ReloadDynamicIntervalConfig(AutomationConfigModel automationConfig = null)
        {
            try
            {
                automationConfig ??= GetAutomationConfig();
                if (automationConfig != null && automationConfig.DynamicIntervalModelId > 0)
                {
                    var details = await OmsCore.GatewayClient.RequestConfigDataAsync(automationConfig.DynamicIntervalModelId);
                    if (details != null)
                    {
                        DynamicIntervalModel model = JsonConvert.DeserializeObject<DynamicIntervalModel>(details.ConfigJson);
                        model.Id = automationConfig.DynamicIntervalModelId;
                        model.Details = details;
                        automationConfig.DynamicIntervalModel = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReloadDynamicIntervalConfig));
            }
        }

        private DelegateCommand sizeUpConfigChangedCommand;
        public ICommand SizeUpConfigChangedCommand => sizeUpConfigChangedCommand ??= new DelegateCommand(SizeUpConfigChanged);

        private void SizeUpConfigChanged()
        {
            if (OmsCore.Config.WarnAgainstLargeSizeUpConfigV2)
            {
                var automationConfig = GetAutomationConfig();
                if (automationConfig != null)
                {
                    if (automationConfig.LoopSizeupType == LoopSizeupType.Dynamic &&
                        automationConfig.SizeupConfig != null &&
                        automationConfig.SizeupConfig.SizeUpConfigs.Any() &&
                        automationConfig.SizeupConfig.SizeUpConfigs.Max(x => x.Size) >= OmsCore.Config.WarnAgainstLargeSizeUpQty)
                    {

                        bool proceed = MessageBoxService?.Show($"Are you sure you want to use dynamic size up '{SizeupConfig?.Title}'?", " - ZeroPlus OMS", MessageButton.YesNo, MessageIcon.Exclamation, MessageResult.No) == MessageResult.Yes;
                        if (!proceed)
                        {
                            automationConfig.LoopSizeupType = LoopSizeupType.Off;
                        }
                    }
                    else if (automationConfig.LoopSizeupType == LoopSizeupType.Static &&
                             automationConfig.LoopSizeupQty >= OmsCore.Config.WarnAgainstLargeSizeUpQty)
                    {
                        bool proceed = MessageBoxService?.Show($"Are you sure you want to use static size up of qty {automationConfig.LoopSizeupQty}?", $" - ZeroPlus OMS", MessageButton.YesNo, MessageIcon.Exclamation, MessageResult.No) == MessageResult.Yes;
                        if (!proceed)
                        {
                            automationConfig.LoopSizeupType = LoopSizeupType.Off;
                        }
                    }
                }
            }
        }

        private DelegateCommand showSizeupConfigPanelCommand;
        public ICommand ShowSizeupConfigPanelCommand => showSizeupConfigPanelCommand ??= new DelegateCommand(ShowSizeupConfigPanel);

        private void ShowSizeupConfigPanel()
        {
            try
            {
                LoopSizeupManagementView view = new();
                if (view.DataContext is LoopSizeupManagementViewModel viewModel)
                {
                    viewModel.AutomationTrader = this;
                    view.Show();
                }
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowSizeupConfigPanelCommand));
            }
        }


        private DelegateCommand showLoopIncrementConfigPanelCommand;
        public ICommand ShowLoopIncrementConfigPanelCommand => showLoopIncrementConfigPanelCommand ??= new DelegateCommand(ShowLoopIncrementConfigPanel);

        private void ShowLoopIncrementConfigPanel()
        {
            try
            {
                var automationConfig = GetAutomationConfig();
                if (automationConfig.LoopIncrementConfigModel != null &&
                    automationConfig.LoopIncrementConfigModelId == 0 &&
                    string.IsNullOrWhiteSpace(automationConfig.LoopIncrementConfigModel.Title))
                {
                    EditDynamicIncrementConfig(automationConfig.LoopIncrementConfigModel);
                }
                else
                {
                    DynamicConfigManagementView view = new();
                    if (view.DataContext is DynamicConfigManagementViewModel viewModel)
                    {
                        viewModel.Parent = this;
                        viewModel.ConfigModule = Module.DynamicIncrementConfigs;
                        view.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowLoopIncrementConfigPanelCommand));
            }
        }
        private void EditDynamicIncrementConfig(IDynamicConfigModel selectedModel)
        {
            try
            {
                LoopDynamicIncrementConfigView view = new();
                LoopDynamicIncrementConfigViewModel viewModel = view.DataContext as LoopDynamicIncrementConfigViewModel;

                viewModel.SetModel((LoopIncrementConfigModel)selectedModel);

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditDynamicIncrementConfig));
            }
        }

        private DelegateCommand reverseLooperRoutesCommand;
        public ICommand ReverseLooperRoutesCommand => reverseLooperRoutesCommand ??= new DelegateCommand(ReverseLooperRoutes);

        private void ReverseLooperRoutes()
        {
            try
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                (automationConfig.LooperCloseRoute, automationConfig.LooperOpenRoute) = (automationConfig.LooperOpenRoute, automationConfig.LooperCloseRoute);
                (automationConfig.LooperCloseRouteSize, automationConfig.LooperOpenRouteSize) = (automationConfig.LooperOpenRouteSize, automationConfig.LooperCloseRouteSize);
                (automationConfig.LooperCloseRouteSingleLeg, automationConfig.LooperOpenRouteSingleLeg) = (automationConfig.LooperOpenRouteSingleLeg, automationConfig.LooperCloseRouteSingleLeg);
                (automationConfig.LooperCloseRouteSingleLegSize, automationConfig.LooperOpenRouteSingleLegSize) = (automationConfig.LooperOpenRouteSingleLegSize, automationConfig.LooperCloseRouteSingleLegSize);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReverseLooperRoutesCommand));
            }
        }

        private DelegateCommand clearAutomationConfigCommand;
        public ICommand ClearAutomationConfigCommand => clearAutomationConfigCommand ??= new DelegateCommand(ClearAutomationConfig);

        private void ClearAutomationConfig()
        {
            UnderlyingToAutomationConfigModelLookup = new();
        }
        #endregion

        #region IDynamicConfigParentModule
        public DynamicSizeupModel SizeupConfig { get; set; }
        public int GetCurrentConfigId(Module configModule)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    AutomationConfigModel automationConfigModel = GetAutomationConfig();
                    return automationConfigModel.DynamicEdgeModelId;
                case Module.DynamicIncrementConfigs:
                    automationConfigModel = GetAutomationConfig();
                    return automationConfigModel.LoopIncrementConfigModelId;
            }
            return 0;
        }
        public IDynamicConfigModel GetDynamicConfig(Module configModule, string configJson = null)
        {
            return configModule switch
            {
                Module.DynamicEdgeConfigs => GetDynamicEdgeConfigModel(configJson),
                Module.AutoPermConfig => GetAutoPermModel(configJson),
                Module.DynamicIncrementConfigs => GetDynamicIncrementConfig(configJson),
                _ => null,
            };
        }
        private DynamicEdgeConfigModel GetDynamicEdgeConfigModel(string configJson)
        {
            DynamicEdgeConfigModel config = null;
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                config = JsonConvert.DeserializeObject<DynamicEdgeConfigModel>(configJson);
            }

            config ??= new DynamicEdgeConfigModel()
            {
                Creator = OmsCore.User.Username,
                LastUpdateTime = DateTime.Now,
            };
            return config;
        }
        private BasketAutoPermModel GetAutoPermModel(string configJson)
        {
            BasketAutoPermModel autoPermConfig = null;
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                autoPermConfig = JsonConvert.DeserializeObject<BasketAutoPermModel>(configJson);
            }
            if (autoPermConfig == null)
            {
                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                BasketAutoPermModel autoPermConfigModel = automationConfigModel.AutoPermConfigModel;
                autoPermConfig = new()
                {
                    Creator = OmsCore.User.Username,
                    LastUpdateTime = DateTime.Now,
                    ShowAutoPermOthers = true,
                    AutoPermOtherInstances = autoPermConfigModel?.AutoPermOtherInstances ?? new(),
                    SubmitAutoPerms = autoPermConfigModel?.SubmitAutoPerms ?? true,
                    WaitForPrevious = autoPermConfigModel?.WaitForPrevious ?? true,
                    AutoPermOthers = autoPermConfigModel?.AutoPermOthers ?? false,
                };
            }
            return autoPermConfig;
        }
        private LoopIncrementConfigModel GetDynamicIncrementConfig(string configJson)
        {
            LoopIncrementConfigModel model = null;
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                model = JsonConvert.DeserializeObject<LoopIncrementConfigModel>(configJson);
            }

            if (model == null)
            {
                model ??= new LoopIncrementConfigModel()
                {
                    Creator = OmsCore.User.Username,
                    LastUpdateTime = DateTime.Now,
                    DynamicIncrementConfigs = new List<DynamicIncrementConfigModel>(),
                };
            }
            return model;
        }
        public void LoadDynamicConfig(Module configModule, IDynamicConfigModel currentModel)
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    automationConfigModel.DynamicEdgeModel = currentModel as IDynamicEdgeModel;
                    automationConfigModel.DynamicEdgeModelId = currentModel?.Id ?? 0;
                    break;
                case Module.AutoPermConfig:
                    automationConfigModel.AutoPermConfigModel = currentModel as BasketAutoPermModel;
                    automationConfigModel.AutoPermConfigModelId = currentModel?.Id ?? 0;
                    break;
                case Module.DynamicIncrementConfigs:
                    automationConfigModel.LoopIncrementConfigModel = currentModel as LoopIncrementConfigModel;
                    automationConfigModel.LoopIncrementConfigModelId = currentModel?.Id ?? 0;
                    break;
            }
        }
        public void EditDynamicConfig(Module configModule, IDynamicConfigModel selectedModel)
        {
            switch (configModule)
            {
                case Module.DynamicEdgeConfigs:
                    DynamicEdgeConfigView view = new();
                    if (view.DataContext is DynamicEdgeConfigViewModel viewModel)
                    {
                        viewModel.Model = (DynamicEdgeModel)selectedModel;
                        viewModel.Loader = (config) => LoadDynamicConfig(configModule, selectedModel);
                    }
                    view.ShowDialog();
                    break;
                case Module.DynamicIncrementConfigs:
                    EditDynamicIncrementConfig(selectedModel);
                    break;
            }
        }
        #endregion


    }
}