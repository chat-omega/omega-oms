using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    public abstract class ModuleWindow : ThemedWindow, IDisposable
    {
        protected const string DefaultConfigGroupName = "Admin";
        protected string DefaultConfigGroupTitle => CleanTitle(Title);

        private readonly bool _loadDefault;
        protected bool _disposed;
        private static readonly NLog.ILogger _log = NLog.LogManager.GetCurrentClassLogger();

        public IModuleFactory ModuleFactory { get; }

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public ConfigSave DefaultConfig { get; set; }
        public bool UsingDefaultConfig { get; set; }
        public Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public IModuleViewModel ViewModel { get; private set; }
        public Task ModuleLoadTask { get; private set; } = Task.CompletedTask;

        protected ModuleWindow(Module module, string uid, IModuleFactory moduleFactory, bool loadDefault = true)
        {
            _loadDefault = loadDefault;
            Uid = !string.IsNullOrWhiteSpace(uid) ? uid : Guid.NewGuid().ToString();
            Module = module;
            Name = module.ToString();
            Title = module.ToString().FromCamelCase();
            Closed += Module_Closed;
            Loaded += Module_Loaded;
            ModuleFactory = moduleFactory;
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
        }

        public abstract Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true);
        public abstract string GetConfigAsJson(bool isDefault = false, bool withContent = false);
        public virtual void ClearFiltersClick() { }
        public virtual void ClearSortingClick() { }

        private void Module_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= Module_Loaded;
            Dispatcher.UnhandledException += HandleDispatcherUnhandledException;

            if (DataContext is not IModuleViewModel viewModel)
            {
                return;
            }

            OnModuleLoaded();
            ViewModel = viewModel;
            ViewModel.SetDispatcher(Dispatcher);
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            OmsCore omsCore = ViewModel.OmsCore;
            omsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            ConfigSave = new ConfigSave()
            {
                Title = Title,
                Module = (int)Module,
                Username = omsCore.User.Username,
                Group = omsCore.User.Username,
                OwnerId = omsCore.User.ID,
            };

            LoadAdminConfigs();
            RestoreLayout(_loadDefault);
        }

        protected virtual void RestoreLayout(bool loadDefault)
        {
            if (loadDefault)
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                    ModuleLoadTask = RestoreFromConfigSaveAsync(configSave);
                }
                else
                {
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        ModuleLoadTask = RestoreFromConfigSaveAsync(configSave);
                        DefaultConfig = JsonConvert.DeserializeObject<ConfigSave>(export);
                        UsingDefaultConfig = true;
                    }
                    else
                    {
                        ModuleLoadTask = RestoreFromConfigSaveAsync(ConfigSave);
                    }
                }
            }
            else
            {
                ModuleLoadTask = RestoreFromConfigSaveAsync(ConfigSave);
            }
        }

        private void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _log.Error(e.Exception, $"Dispatcher Unhandled Exception. " +
                                            $"Module: {Module}, " +
                                            $"Id: {Uid}, " +
                                            $"Dispatcher: {Dispatcher.Thread.Name}");
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(HandleDispatcherUnhandledException));
            }
        }

        protected virtual void OnModuleLoaded() { }

        private void Module_Closed(object sender, EventArgs e)
        {
            ViewModel.OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Dispatcher.UnhandledException -= HandleDispatcherUnhandledException;
            Dispose();
            if (!ViewModel.IsDisposed)
            {
                ViewModel.Dispose();
            }
            DataContext = null;
            if (ModuleFactory == null || !ModuleFactory.IsPersistentDispatcher(Dispatcher))
            {
                Dispatcher.InvokeShutdown();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisposeView();
                _disposed = true;
            }
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void DisposeView()
        {

        }

        public void ShareLayout(object sender, EventArgs e)
        {
            ShareLayout();
        }

        public void ShareLayout()
        {
            try
            {
                ShareWithView view = new();
                if (view.DataContext is ShareWithViewModel viewModel)
                {
                    viewModel.Module = Module;
                    viewModel.Config = GetConfigAsJson();
                }
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareLayout));
            }
        }

        public void SaveLayout(object sender, EventArgs e)
        {
            ShowSaveLayoutPrompt();
        }

        public void ShowSaveLayoutPrompt(bool showSaveLocation = false)
        {
            try
            {
                SaveView view = new();
                if (view.DataContext is not SaveViewModel viewModel)
                {
                    return;
                }

                viewModel.LoadGroups(Module);
                viewModel.Id = ConfigSave.Id;
                viewModel.Title = CleanTitle(ConfigSave.Title);
                viewModel.SelectedGroup = ConfigSave.Group;
                viewModel.Config = GetConfigAsJson(!viewModel.SaveLocation);
                viewModel.ShowLocation = showSaveLocation;
                viewModel.SetAsDefault = UsingDefaultConfig;

                view.ShowDialog();

                if (viewModel.Success)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    SaveLayout(viewModel.SetAsDefault, viewModel.SaveLocation);
                }

                if (viewModel.Success && viewModel.AddToFavorites)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    ConfigSave.ConfigJson = GetConfigAsJson();
                    OmsCore.Config.AddFavoriteModule(Name, ConfigSave);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowSaveLayoutPrompt));
            }
        }
        public void LoadLayout(object sender, EventArgs e)
        {
            LoadLayout();
        }

        public void LoadLayout()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                if (windowView.DataContext is ConfigBrowserViewModel viewModel)
                {
                    windowView.Loaded += (_, _) => viewModel.SetModule(Module);
                    viewModel.LoadConfig = configSave => _ = RestoreFromConfigSaveId(configSave.Id);
                }
                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadLayout));
            }
        }

        public void SaveLayout(bool saveDefault, bool saveLocation = false)
        {
            Dispatcher.Invoke(() =>
            {
                ConfigSave.ConfigJson = GetConfigAsJson(saveDefault, true);

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                string export = JsonConvert.SerializeObject(ConfigSave);
                if (!string.IsNullOrWhiteSpace(instanceExportPath))
                {
                    File.WriteAllText(instanceExportPath, export);
                }
                if (saveDefault)
                {
                    ConfigSave.ConfigJson = GetConfigAsJson(isDefault: !saveLocation);
                    export = JsonConvert.SerializeObject(ConfigSave);
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath))
                    {
                        File.WriteAllText(defaultExportPath, export);
                    }
                }

                if (UsingDefaultConfig)
                {
                    if (saveDefault)
                    {
                        UsingDefaultConfig = true;
                        DefaultConfig = ConfigSave;
                    }
                    else
                    {
                        DefaultConfig.Title = CleanTitle(DefaultConfig.Title + " [Local]");
                        export = JsonConvert.SerializeObject(DefaultConfig);
                        string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                        if (!string.IsNullOrWhiteSpace(defaultExportPath))
                        {
                            File.WriteAllText(defaultExportPath, export);
                        }
                    }
                }

                SetTitleFromConfigSave(ConfigSave);
            });
        }

        public async void CloneModule()
        {
            ModuleWindow window = ModuleFactory?.CreateModule(Module, false);
            if (window != null)
            {
                var viewModel = window.ViewModel;
                if (viewModel != null)
                {
                    viewModel.AllowSave = false;
                }
                string configJson = GetConfigAsJson();
                await window.ModuleLoadTask.ContinueWith(_ => window.LoadConfigFromJsonAsync(configJson, offset: true, withContent: true));
            }
        }

        internal async Task RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                ConfigSave configSave = await ViewModel.OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
                await RestoreFromConfigSaveAsync(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal async Task RestoreFromConfigSaveAsync(ConfigSave configSave)
        {
            UsingDefaultConfig = false;

            if (configSave != null)
            {
                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                if (configSave.ConfigJson != null)
                {
                    await LoadConfigFromJsonAsync(configSave.ConfigJson);
                }
                else
                {
                    if (DataContext is ModuleViewModelBase viewModel)
                    {
                        await viewModel.InvokeReady();
                    }
                }
            }
            else
            {
                if (DataContext is ModuleViewModelBase viewModel)
                {
                    await viewModel.InvokeReady();
                }
            }
        }

        protected void SetTitleFromConfigSave(ConfigSave configSave)
        {
            var defaultTitle = Name.FromCamelCase();

            defaultTitle = CleanTitle(defaultTitle);

            if (!string.IsNullOrWhiteSpace(configSave.Title) && configSave.Title != defaultTitle)
            {
                ViewModel.ModuleTitle = configSave.Title + " - " + defaultTitle;
            }
        }

        public static string CleanTitle(string defaultTitle)
        {
            try
            {
                int count = Regex.Count(defaultTitle, Regex.Escape(" [Local]"));
                if (count <= 0)
                {
                    return defaultTitle;
                }

                string module = "";
                if (defaultTitle.Contains(" - "))
                {
                    string[] parts = defaultTitle.Split(" - ");
                    defaultTitle = parts[0];
                    if (parts.Length > 1)
                    {
                        module = parts[1];
                    }
                }

                defaultTitle = defaultTitle.Replace(" [Local]", "");
                defaultTitle += " [Local]";

                if (!string.IsNullOrWhiteSpace(module))
                {
                    defaultTitle += " - " + module;
                }

                return defaultTitle;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CleanTitle));
                return defaultTitle;
            }
        }

        private void OnSaveLayoutRequest()
        {
            try
            {
                SaveLayout(false, true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnSaveLayoutRequest) + " Module: " + Module);
            }
        }

        public void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        protected void LoadWindowSettingsFromJson(string windowSetting, bool offset)
        {
            try
            {
                WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSetting);
                int margin = (offset ? 100 : 0);
                if (Dispatcher.CheckAccess())
                {
                    ApplyWindowSettings(windowSettings, margin);
                }
                else
                {
                    Dispatcher.BeginInvoke(() => ApplyWindowSettings(windowSettings, margin));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadWindowSettingsFromJson));
            }
        }

        private void ApplyWindowSettings(WindowSetting windowSettings, int margin)
        {
            Left = windowSettings.Left + margin;
            Top = windowSettings.Top + margin;
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

        protected void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (dateTime == DateTime.MinValue || dateTime.Date == DateTime.UnixEpoch.Date)
                {
                    e.DisplayText = "";
                }
                else if (Module is Module.CobFeed or Module.ImpliedQuoteFeed)
                {
                    e.DisplayText = dateTime.ToString("MM/dd hh:mm:ss.fff");
                }
                else
                {
                    switch (e.Column.FieldName)
                    {
                        case "UpdateTime" when Module == Module.ExecutionTransaction:
                            e.DisplayText = dateTime.ToString("hh:mm:ss.fff");
                            break;
                        case "LutTimeOnly":
                            e.DisplayText = dateTime.ToString("T");
                            break;
                        case "NearExpiration":
                        case "FarExpiration":
                            e.DisplayText = dateTime.ToString("MMM dd yy");
                            break;
                        default:
                            e.DisplayText = dateTime.ToString(OmsCore.Config.LayoutDefaultDateTimeColumnFormat);
                            break;
                    }
                }
            }
            else if (e.Value is double doubleVal)
            {
                if (double.IsNaN(doubleVal))
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName is "Price" or "AveragePrice")
                {
                    e.DisplayText = doubleVal.ToString("#,##0.00;(#,##0.00)");
                }
                else if (e.Column.FieldName == "Strike")
                {
                    e.DisplayText = doubleVal.ToString(CultureInfo.InvariantCulture);
                }
                else if (e.Column.FieldName == "Delta")
                {
                    e.DisplayText = doubleVal.ToString("n3");
                }
                else
                {
                    e.DisplayText = doubleVal.ToString("n2");
                }
            }
        }

        protected void Grid_ColumnsPopulated(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is GridControl grid)
                {
                    foreach (GridColumn col in grid.Columns.ToList())
                    {
                        try { DependencyPropertyDescriptor.FromProperty(BaseColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(col, ColumnVisibilityChanged); } catch { /* Ignore */ }
                        if (!string.IsNullOrWhiteSpace(col.FieldName))
                        {
                            col.Name = col.FieldName.Replace(".", "").Replace("-", "Remove").Replace(" ", "");
                        }
                        else if (col.Header is string header && !string.IsNullOrWhiteSpace(header))
                        {
                            col.Name = header.Replace(".", "").Replace("-", "Remove").Replace(" ", "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Grid_ColumnsPopulated));
            }
        }

        protected void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            TableView tableView = sender as TableView;
            GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                BarButtonItem removeColumnButton = new()
                {
                    Content = "Hide This Column",
                };
                removeColumnButton.ItemClick += (_, _) => { column.Visible = false; };

                BarButtonItem editColumnButton = new()
                {
                    Content = "Edit Column Header",
                };
                editColumnButton.ItemClick += (_, _) =>
                {
                    UpdateColumnHeaderView view = new();
                    if (view.DataContext is UpdateColumnHeaderViewModel viewModel)
                    {
                        viewModel.Title = column.Header != null ? column.Header.ToString() : column.FieldName;
                        viewModel.TitleUpdatedEvent += title => { column.Header = title; };
                    }
                    view.Show();
                };

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);

                List<IBarManagerControllerAction> buttons = GetHeaderBarButtons(column);
                if (buttons != null)
                {
                    foreach (var button in buttons)
                    {
                        gridMenuEventArgs.Customizations.Add(button);
                    }
                }
            }
            else
            {
                List<IBarManagerControllerAction> buttons = GetRowBarButtons(column);
                if (buttons != null)
                {
                    foreach (var button in buttons)
                    {
                        gridMenuEventArgs.Customizations.Add(button);
                    }
                }
            }
        }

        protected BarButtonItem GetExportTableToExcelButton(TableView tableView)
        {
            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Excel",
            };

            exportToExcelButton.ItemClick += (_, _) => ExportTableToExcel(tableView);
            return exportToExcelButton;
        }

        protected void ExportTableToExcel(TableView tableView)
        {
            try
            {
                if (tableView == null)
                {
                    return;
                }

                ISaveFileDialogService saveFileDialogService = ViewModel.GetService<ISaveFileDialogService>();
                if (saveFileDialogService == null)
                {
                    return;
                }

                saveFileDialogService.DefaultExt = "xlsx";
                saveFileDialogService.DefaultFileName = Module.ToString().FromCamelCase() + $" Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
                saveFileDialogService.Filter = "xlsx|*.xlsx";
                bool dialogResult = saveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    string filePath = saveFileDialogService.GetFullFileName();
                    tableView.ExportToXlsx(filePath);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportTableToExcel));
            }
        }

        public virtual List<IBarManagerControllerAction> GetHeaderBarButtons(GridColumn column)
        {
            return null;
        }

        public virtual List<IBarManagerControllerAction> GetRowBarButtons(GridColumn column)
        {
            return null;
        }

        protected BarButtonItem GetEditGridButton(TableView table)
        {
            BarButtonItem editColumnButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Actions_Settings.svg")),
                Content = "Edit Columns",
            };

            editColumnButton.ItemClick += (_, _) =>
            {
                LayoutSettings(table.Grid);
            };
            return editColumnButton;
        }

        protected void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }

        protected void ColumnVisibilityChanged(object sender, EventArgs e)
        {
            if (sender is GridColumn { Visible: true } column)
            {
                column.VisibleIndex = 1000;
            }
        }

        protected void LoadAdminConfigs()
        {
            if (DataContext is not ModuleViewModelBase viewModel)
                return;

            Task<List<ConfigSave>> loadTask = viewModel.OmsCore.GatewayClient.RequestConfigsAsync((int)Module);

            loadTask.ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    List<ConfigSave> configs = loadTask.Result;
                    if (configs == null)
                    {
                        return;
                    }
                    configs = [.. configs.Where(x => x.Group == DefaultConfigGroupName)];
                    viewModel.AdminConfigs.Clear();
                    if (configs.Count == 0)
                    {
                        return;
                    }

                    foreach (var config in configs)
                        viewModel.AdminConfigs.Add(config);

                    viewModel.SelectedConfig = null;
                });
            });
        }

        public void LoadConfig(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ModuleViewModelBase viewModel || viewModel.SelectedConfig == null)
                return;

            var confirm = viewModel.MessageBoxService?.Show("Are you sure you want to change layouts?",
                                                            "Layout Verification",
                                                            MessageButton.YesNo,
                                                            MessageIcon.Warning,
                                                            MessageResult.Yes) == MessageResult.Yes;
            if (!confirm)
                return;

            Task<List<ConfigSave>> loadTask = viewModel.OmsCore.GatewayClient.RequestConfigsAsync((int)Module);

            loadTask.ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    List<ConfigSave> configs = loadTask.Result;
                    if (configs == null)
                    {
                        viewModel.MessageBoxService.ShowMessage("Failed to load configurations.");
                        return;
                    }
                    var defaultConfig = configs.Where(x => x.Group == DefaultConfigGroupName && x.Title == viewModel.SelectedConfig.Title).FirstOrDefault();
                    if (defaultConfig == null)
                    {
                        viewModel.MessageBoxService.ShowMessage("Default configuration not found.");
                        return;
                    }
                    RestoreFromConfigSaveId(defaultConfig.Id);
                });
            });
        }

        public void ReloadConfigsSelected(object sender, RoutedEventArgs e)
        {
            LoadAdminConfigs();
        }
    }
}
