using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.PropertyGrid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DominatorView.xaml
    /// </summary>
    public partial class DominatorView : ThemedWindow
    {
        private const string MODULE_NAME = "Dominator";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public bool GroupExpanded { get; private set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public DominatorView() : this(Guid.NewGuid().ToString())
        {
        }

        public DominatorView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(DominatorView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Closed += ViewClosed;
            Loaded += RestoreLayout;
            Module = Module.Dominator;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
        }

        private void ViewClosed(object sender, EventArgs e)
        {
            DominatorViewModel viewModel = (DominatorViewModel)DataContext;
            viewModel.Dispose();
        }

        private void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date)
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName == "LutTimeOnly")
                {
                    e.DisplayText = dateTime.ToString("T");
                }
                else
                {
                    e.DisplayText = dateTime.ToString(OmsCore.Config.LayoutDefaultDateTimeColumnFormat);
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
                    e.DisplayText = doubleVal.ToString();
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

        private void Grid_ColumnsPopulated(object sender, RoutedEventArgs e)
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

        private void ColumnVisibilityChanged(object sender, EventArgs e)
        {
            if (sender is GridColumn column && column.Visible)
            {
                column.VisibleIndex = 1000;
            }
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;

                BarButtonItem removeColumnButton = new()
                {
                    Content = "Hide This Column",
                };

                removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { column.Visible = false; };

                BarButtonItem editColumnButton = new()
                {
                    Content = "Edit Column Header",
                };

                editColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) =>
                {
                    UpdateColumnHeaderView view = new();
                    UpdateColumnHeaderViewModel viewModel = view.DataContext as UpdateColumnHeaderViewModel;
                    viewModel.Title = column.Header != null ? column.Header.ToString() : column.FieldName;
                    viewModel.TitleUpdatedEvent += (string title) => { column.Header = title; };
                    view.Show();
                };

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);
            }
        }

        public void AddColumn(CustomColumnTemplateModel colTemplate)
        {
            GridColumn column = new()
            {
                FieldName = colTemplate.Header,
                Header = colTemplate.Header,
                Visible = true,
                AllowUnboundExpressionEditor = colTemplate.AllowEquationEvaluator,
                UnboundType = colTemplate.Type,
                AllowEditing = colTemplate.AllowEditing ? DevExpress.Utils.DefaultBoolean.True : DevExpress.Utils.DefaultBoolean.False,
            };

            if (DominatorMainGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                DominatorMainGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                DominatorMainGrid.Columns.Add(column);
            }
        }

        private BarButtonItem GetEditGridButton(TableView table)
        {
            BarButtonItem editColumnButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Actions_Settings.svg")),
                Content = "Edit Columns",
            };

            editColumnButton.ItemClick += (o, i) =>
            {
                LayoutSettings(table.Grid);
            };
            return editColumnButton;
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in DominatorMainGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
            {
                columns.Add(new CustomColumnTemplateModel()
                {
                    Header = column.FieldName,
                    AllowEditing = column.AllowEditing == DefaultBoolean.True,
                    AllowEquationEvaluator = column.AllowUnboundExpressionEditor,
                    Equation = column.UnboundExpression
                });
            }

            return columns;
        }

        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            DominatorMainGrid.FilterCriteria = null;
            DominatorMainGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            DominatorMainGrid.ClearSorting();
        }

        private void ClearGrouoping()
        {
            foreach (GridColumn col in DominatorMainGrid.Columns)
            {
                DominatorMainGrid.UngroupBy(col);
            }
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                DominatorMainGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
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


        private void BrowseLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowConfigBrowser();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayout));
            }
        }
        private void LoadLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowConfigBrowser((ConfigSave configSave) => RestoreFromConfigSaveId(configSave.Id));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadLayout));
            }
        }

        private void ShowConfigBrowser(LoadConfigHandler loadAction = null)
        {
            ConfigBrowserWindowView windowView = new();
            ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;
            windowView.Loaded += (s, a) => viewModel.SetModule(Module);
            if (loadAction != null)
            {
                viewModel.LoadConfig = loadAction;
            }
            windowView.ShowDialog();
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
                _ = LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                if (DataContext is DominatorViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(DominatorMainGrid)] = LayoutHelper.GetLayoutAsString(DominatorMainGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

                if (configDictionary.ContainsKey(nameof(DominatorMainGrid)))
                {
                    LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(DominatorMainGrid)], DominatorMainGrid);
                }

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

                if (configDictionary.ContainsKey(nameof(GridFieldNameToConfigMap)))
                {
                    if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                    {
                        GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                        TableCustomizationViewModel tableCustomizationViewModel = new();
                        tableCustomizationViewModel.Load(DominatorMainGrid, GridFieldNameToConfigMap);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void CloneClick(object sender, RoutedEventArgs e)
        {
            Thread newWindowThread = new(() =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                        Dispatcher.CurrentDispatcher));

                DominatorView window = new();
                DominatorViewModel viewModel = (DominatorViewModel)window.DataContext;
                viewModel.SetDispatcher(window.Dispatcher);

                window.Dispatcher.UnhandledException += (s, e) =>
                {
                    _log.Error(e.Exception, "DispatcherUnhandledException");
                    e.Handled = true;
                };

                window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                window.Show();

                Dispatcher.Run();
            });
            newWindowThread.SetApartmentState(ApartmentState.STA);
            newWindowThread.Start();
        }


        private void TableView_DragRecordOver(object sender, DragRecordOverEventArgs e)
        {
            if (e.IsFromOutside)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private async void TableView_DropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    List<SpreadGeneratorResults> orders = await Task.Run(() => (List<SpreadGeneratorResults>)e.Data.GetData(DataFormats.Serializable));
                    e.Data = null;
                    if (DataContext is DominatorViewModel viewModel)
                    {
                        viewModel.LoadFromSpreadGeneratorResults(orders);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TableView_DropRecord));
            }
        }

        private void DominatorConfigurationEditor_CellValueChanged(object sender, DevExpress.Xpf.PropertyGrid.CellValueChangedEventArgs args)
        {

        }

        private void DominatorConfigurationEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is PropertyGridControl p)
            {
                //p.SelectedObject = DataContext?.SelectedDominator.Config;
            }
        }
    }
}