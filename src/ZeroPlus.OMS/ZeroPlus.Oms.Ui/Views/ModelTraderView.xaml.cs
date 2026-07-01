using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ModelTraderView.xaml
    /// </summary>
    public partial class ModelTraderView : ThemedWindow
    {
        private const string MODULE_NAME = "Model Trader";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public ModelTraderViewModel ViewModel { get; }

        public ModelTraderView() : this(Guid.NewGuid().ToString())
        {
        }

        public ModelTraderView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(ModelTraderView);
            Closing += View_Closing;
            Loaded += RestoreLayout;
            Module = Module.ModelTrader;
            GridFieldNameToConfigMap = new();
            ViewModel = DataContext as ModelTraderViewModel;
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            ModelTraderViewModel viewModel = (ModelTraderViewModel)DataContext;
            bool cancel = viewModel.Dispose();
            e.Cancel = cancel;
            if (!cancel)
            {
                (DataContext as ModelTraderViewModel).OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            }
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(saveDefault: false);
        }

        private async void RestoreLayout(object sender, RoutedEventArgs e)
        {
            await RestoreLayoutAsync();
        }

        public async Task RestoreLayoutAsync()
        {
            Loaded -= RestoreLayout;
            if (ShowInTaskbar)
            {
                StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            }
            ModelTraderViewModel dataContext = (ModelTraderViewModel)DataContext;
            OmsCore omsCore = dataContext.OmsCore;
            dataContext.Uid = Uid;
            omsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = omsCore.User.Username,
                Group = omsCore.User.Username,
                OwnerId = omsCore.User.ID,
            };
            dataContext.Name = "Model Trader " + Uid.Split('-').LastOrDefault();
            string path = $"{Uid}-{Module}-layout.json";
            string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
            string instanceExportPath = Path.Combine(layoutDir, path);
            string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");

            if (!_layoutRestored)
            {
                _layoutRestored = true;
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = await Task.Run(() => JsonConvert.DeserializeObject<ConfigSave>(export));
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = await Task.Run(() => JsonConvert.DeserializeObject<ConfigSave>(export));
                        RestoreFromConfigSave(configSave);
                    }
                    else
                    {
                        RestoreFromConfigSave(ConfigSave);
                    }
                }
            }
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void RemoveButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is SimpleButton baseEdit)
            {
                baseEdit.Opacity = 1;
            }
        }

        private void RemoveButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is SimpleButton baseEdit)
            {
                baseEdit.Opacity = 0.5;
            }
        }

        private void RestoreFromConfigSave(ConfigSave configSave)
        {
            if (configSave != null)
            {
                ConfigSave = configSave;
                _ = LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private async Task LoadConfigFromJsonAsync(string configJson)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

                if (configDictionary.ContainsKey(nameof(WindowSetting)))
                {
                    if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                    {
                        WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                        Left = windowSettings.Left;
                        Top = windowSettings.Top;
                        if (windowSettings.Width > 0)
                        {
                            Width = windowSettings.Width;
                        }
                        if (windowSettings.Height > 0)
                        {
                            Height = windowSettings.Height;
                        }
                        WindowState = windowSettings.WindowState;
                    }
                }

                if (configDictionary.TryGetValue(nameof(QuoteGrid), out string gridLayout))
                {
                    LayoutHelper.RestoreLayoutFromString(gridLayout, QuoteGrid);
                }

                if (configDictionary.TryGetValue(nameof(OrdersGrid), out gridLayout))
                {
                    LayoutHelper.RestoreLayoutFromString(gridLayout, OrdersGrid);
                }

                if (configDictionary.TryGetValue(nameof(HedgeOrdersGrid), out gridLayout))
                {
                    LayoutHelper.RestoreLayoutFromString(gridLayout, HedgeOrdersGrid);
                }

                if (configDictionary.TryGetValue(nameof(ModelTraderConfig), out gridLayout))
                {
                    ModelTraderConfig modelTraderConfig = await Task.Run(() => JsonConvert.DeserializeObject<ModelTraderConfig>(gridLayout));
                    if (DataContext is ModelTraderViewModel viewModel)
                    {
                        viewModel.LoadConfig(modelTraderConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        public void SaveLayout(bool saveDefault)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ConfigSave.ConfigJson = GetConfigAsJson();

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                string export = JsonConvert.SerializeObject(ConfigSave);
                if (!string.IsNullOrWhiteSpace(instanceExportPath))
                {
                    File.WriteAllText(instanceExportPath, export);
                }
                if (saveDefault)
                {
                    ConfigSave.ConfigJson = GetConfigAsJson(isDefault: true);
                    export = JsonConvert.SerializeObject(ConfigSave);
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath))
                    {
                        File.WriteAllText(defaultExportPath, export);
                    }
                }
            }));
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(QuoteGrid)] = LayoutHelper.GetLayoutAsString(QuoteGrid),
                [nameof(OrdersGrid)] = LayoutHelper.GetLayoutAsString(OrdersGrid),
                [nameof(HedgeOrdersGrid)] = LayoutHelper.GetLayoutAsString(HedgeOrdersGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(ModelTraderConfig)] = JsonConvert.SerializeObject((DataContext as ModelTraderViewModel)?.GetConfig()),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            OrdersGrid.ClearSorting();
            HedgeOrdersGrid.ClearSorting();
            QuoteGrid.ClearSorting();
        }

        private void CloneLayout(object sender, RoutedEventArgs e)
        {
            ModelTraderView window = new();
            ModelTraderViewModel viewModel = (ModelTraderViewModel)window.DataContext;
            viewModel.SetDispatcher(window.Dispatcher);
            window.Loaded += (s, e) => _ = window.LoadConfigFromJsonAsync(GetConfigAsJson());
            window.Show();
        }

        private void ShareLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ShareWithView view = new();
                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = Module;
                viewModel.Config = GetConfigAsJson();

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareLayout));
            }
        }

        private void SaveLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveView view = new();
                SaveViewModel viewModel = view.DataContext as SaveViewModel;

                viewModel.LoadGroups(Module);
                viewModel.Id = ConfigSave.Id;
                viewModel.Title = ConfigSave.Title;
                viewModel.SelectedGroup = ConfigSave.Group;
                viewModel.Config = GetConfigAsJson();

                view.ShowDialog();

                if (viewModel.Success)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    SaveLayout(viewModel.SetAsDefault);
                }

                if (viewModel.Success && viewModel.AddToFavorites)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    ConfigSave.ConfigJson = GetConfigAsJson();
                    OmsCore.Config.AddFavoriteModule(MODULE_NAME, ConfigSave);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLayout));
            }
        }

        private void LoadLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (s, a) => viewModel.SetModule(Module);
                viewModel.LoadConfig = (ConfigSave configSave) => RestoreFromConfigSaveId(configSave.Id);

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadLayout));
            }
        }

        private async void RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                OmsCore omsCore = (DataContext as ModelTraderViewModel).OmsCore;
                ConfigSave configSave = await Task.Run(() => omsCore.GatewayClient.RequestConfigDataAsync(configSaveId));
                RestoreFromConfigSave(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }
    }
}
