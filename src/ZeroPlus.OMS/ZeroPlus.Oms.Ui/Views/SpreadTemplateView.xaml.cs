using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using GridLengthConverter = System.Windows.GridLengthConverter;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SpreadTemplateView.xaml
    /// </summary>
    public partial class SpreadTemplateView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "Spread Template";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public SpreadTemplateView() : this(Guid.NewGuid().ToString())
        {
        }

        public SpreadTemplateView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(SpreadTemplateView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Loaded += RestoreLayout;
            Closed += SpreadTemplateView_Closed;
            Module = Module.SpreadTemplateLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
        }

        private void SpreadTemplateView_Closed(object sender, EventArgs e)
        {
            SpreadTemplateViewModel dataContext = (SpreadTemplateViewModel)DataContext;
            dataContext.Dispose();
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs e)
        {
            if (e.MenuType == GridMenuType.Column)
            {
                GridColumn col = (GridColumn)e.MenuInfo.Column;

                BarButtonItem removeColumnButton = new()
                {
                    Content = "Hide This Column",
                };

                removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { col.Visible = false; };

                e.Customizations.Add(removeColumnButton);
            }
        }

        private void SelectAll(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SpinEdit spinedit = sender as SpinEdit;
            spinedit?.SelectAll();
        }

        private void GridSplitter_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetQuickAccessWidth();
        }

        private void ExpandCollapseGrid_Click(object sender, RoutedEventArgs e)
        {
            SetQuickAccessWidth();
        }

        private void SetQuickAccessWidth()
        {
            GridLengthConverter glc = new();
            if (GridSplitterCol.Width.Value > 0)
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0")!;
                ExpandCollapseGridBtn.Content = 4;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("750")!;
                ExpandCollapseGridBtn.Content = 3;
            }
        }

        private void CutSearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.Cut();
        }

        private void CopySearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.Copy();
        }

        private void PasteSearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.EditValue = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            SpreadTemplateViewModel dataContext = (SpreadTemplateViewModel)DataContext;
            dataContext.Uid = Uid;
            SetQuickAccessWidth();
            if (!_layoutRestored)
            {
                _layoutRestored = true;
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                    RestoreFromConfigSave(configSave);
                }
                else
                {
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        RestoreFromConfigSave(configSave);
                    }
                    else
                    {
                        RestoreFromConfigSave(ConfigSave);
                    }
                }
                _ = dataContext.LoadViewModelConfigAsync(Uid);
            }
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
                SetTitleFromConfigSave(ConfigSave);
            }));
        }

        private async void RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                ConfigSave configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
                RestoreFromConfigSave(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal void RestoreFromConfigSave(ConfigSave configSave)
        {
            if (configSave != null)
            {
                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                LoadConfigFromJson(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                if (DataContext is SpreadTemplateViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            SpreadTemplateViewModel dataContext = (SpreadTemplateViewModel)DataContext;
            GridLengthConverter glc = new();
            string splitterHeight = glc.ConvertToString(GridSplitterCol.Width).Replace("*", "");

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(GridSplitterCol)] = splitterHeight,
                [nameof(SpreadTemplateConfig)] = dataContext.GetConfigJson(),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async void LoadConfigFromJson(string configJson)
        {
            try
            {
                _layoutRestored = true;
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

                SpreadTemplateViewModel dataContext = (SpreadTemplateViewModel)DataContext;
                await dataContext.LoadConfigFromJsonAsync(configDictionary[nameof(SpreadTemplateConfig)]);

                GridLengthConverter glc = new();
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString(configDictionary[nameof(GridSplitterCol)]);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJson));
            }
        }
    }
}
