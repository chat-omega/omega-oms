using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
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
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for TradeFeedView.xaml
    /// </summary>
    public partial class TradeFeedView : ThemedWindow
    {
        private const string MODULE_NAME = "Live Trade Feed";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public TradeFeedView() : this(Guid.NewGuid().ToString())
        {
        }

        public TradeFeedView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(TradeFeedView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Closed += ViewClosed;
            Loaded += RestoreLayout;
            Module = Module.TradeFeed;
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
            TradeFeedViewModel dataContext = (TradeFeedViewModel)DataContext;
            dataContext.Dispose();
        }

        private void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                switch (e.Column.FieldName)
                {
                    case "LutTimeOnly":
                        e.DisplayText = dateTime.ToString("T");
                        break;
                    case "DateAdded":
                        e.DisplayText = dateTime.ToString("d");
                        break;
                    case "NearExpiration":
                    case "FarExpiration":
                        if (dateTime == DateTime.MinValue)
                        {
                            e.DisplayText = "";
                        }
                        else
                        {
                            e.DisplayText = dateTime.ToString("MMM dd yy");
                        }
                        break;
                    default:
                        if (e.Column.FieldName.StartsWith("Expiration"))
                        {
                            if (dateTime == DateTime.MinValue)
                            {
                                e.DisplayText = "";
                            }
                            else
                            {
                                e.DisplayText = dateTime.ToString("MMM dd yy");
                            }
                        }
                        else
                        {
                            e.DisplayText = dateTime.ToString("hh:mm:ss.ffff tt");
                        }

                        break;
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
                else if (e.Column.FieldName == "Delta")
                {
                    e.DisplayText = doubleVal.ToString("n3");
                }
                else
                {
                    e.DisplayText = doubleVal.ToString("n2");
                }
            }
            else if (e.Value is TimeSpan timeSpan)
            {
                e.DisplayText = timeSpan.TotalMilliseconds.ToString("n0");
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
            if (gridMenuEventArgs.MenuType is GridMenuType.Column or GridMenuType.Band)
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

                BarButtonItem exportToExcelButton = new()
                {
                    Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                    Content = "Export to Excel",
                };

                exportToExcelButton.ItemClick += ExportToExcel;

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(exportToExcelButton);
            }
            else if (gridMenuEventArgs.MenuType == GridMenuType.RowCell)
            {
                TradeFeedViewModel viewModel = (TradeFeedViewModel)DataContext;
                GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;

                string selectedCellValue = FeedGrid.GetCellValue(FeedGrid.View.FocusedRowHandle, column)?.ToString();
                string filterString = "";
                string searchTerm = "";
                if (column.FieldName == "Symbol")
                {
                    searchTerm = selectedCellValue;
                    filterString = $"([Symbol] == '{selectedCellValue}')";
                }
                else if (column.FieldName == "UnderSymbol")
                {
                    searchTerm = selectedCellValue;
                    filterString = $"([UnderlyingSymbol] == '{selectedCellValue}')";
                }
                else
                {
                    TradeFeedModel item = (TradeFeedModel)FeedGrid.SelectedItem;
                    selectedCellValue = item.Description;
                    filterString = $"([SpreadId] == '{selectedCellValue}')";
                }

                BarButtonItem filterInNewOrderBookButton = new()
                {
                    Content = "Filter in new Order Book",
                    CommandParameter = filterString,
                    Command = viewModel.FilterInNewOrderBookCommand,
                };

                BarButtonItem filterInTradesButton = new()
                {
                    Content = "Time and Sales",
                    CommandParameter = searchTerm,
                    Command = viewModel.FilterInNewTradesModuleCommand,
                };

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(filterInNewOrderBookButton);
                gridMenuEventArgs.Customizations.Add(filterInTradesButton);
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

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                TableView tableView = TradesTableView;
                if (tableView != null)
                {
                    ISaveFileDialogService saveFileDialogService = ((TradeFeedViewModel)DataContext).SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"Trades Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
                    saveFileDialogService.Filter = "xlsx|*.xlsx";
                    bool dialogResult = saveFileDialogService.ShowDialog();
                    if (dialogResult)
                    {
                        string filePath = saveFileDialogService.GetFullFileName();
                        tableView.ExportToXlsx(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportToExcel));
            }
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void TableView_DragRecordOver(object sender, DragRecordOverEventArgs e)
        {
            if (e.IsFromOutside)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            string symbol = "";
            foreach (TradeFeedModel trade in e.Records)
            {
                symbol = trade.Symbol;
            }
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
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

            if (FeedGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                FeedGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                FeedGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in FeedGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
            {
                columns.Add(new CustomColumnTemplateModel()
                {
                    Header = column.FieldName,
                    AllowEditing = column.AllowEditing == DevExpress.Utils.DefaultBoolean.True,
                    AllowEquationEvaluator = column.AllowUnboundExpressionEditor,
                    Equation = column.UnboundExpression
                });
            }

            return columns;
        }

        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            FeedGrid.FilterCriteria = null;
            FeedGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            FeedGrid.ClearSorting();
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                FeedGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();
            for (int i = 0; i < FeedGrid.VisibleRowCount; i++)
            {
                int rowHandle = FeedGrid.GetRowHandleByVisibleIndex(i);
                list.Add(Tuple.Create(i + 1, FeedGrid.GetRow(rowHandle)));
            }
            if (startFromSelectedRow && FeedGrid.SelectedItem != null)
            {
                Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == FeedGrid.SelectedItem);
                if (selectedRow != null)
                {
                    list = list.Skip(list.IndexOf(selectedRow)).ToList();
                }
            }
            return list;
        }

        public HashSet<T> GetVisibleItems<T>()
        {
            return null;
        }

        public bool ItemIsVisible(object item)
        {
            for (int i = 0; i < FeedGrid.VisibleRowCount; i++)
            {
                int rowHandle = FeedGrid.GetRowHandleByVisibleIndex(i);
                object check = FeedGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
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
                ConfigSave configSave = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId));
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
                if (DataContext is TradeFeedViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            TradeFeedViewModel datacontext = (TradeFeedViewModel)DataContext;
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(FeedGrid)] = LayoutHelper.GetLayoutAsString(FeedGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
                [nameof(TradeFeedViewModelConfig)] = datacontext.GetConfigJson(),
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

                LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(FeedGrid)], FeedGrid);
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
                if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(FeedGrid, GridFieldNameToConfigMap);
                }
                if (configDictionary.TryGetValue(nameof(TradeFeedViewModelConfig), out string viewModelConfog))
                {
                    if (DataContext is TradeFeedViewModel viewModel)
                    {
                        TradeFeedViewModelConfig config = await Task.Run(() => JsonConvert.DeserializeObject<TradeFeedViewModelConfig>(viewModelConfog));
                        viewModel.LoadConfig(config);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private void Clone(object sender, RoutedEventArgs e)
        {
            try
            {
                string config = GetConfigAsJson();
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    TradeFeedView window = new();
                    TradeFeedViewModel viewModel = (TradeFeedViewModel)window.DataContext;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (s, e) => _ = window.LoadConfigFromJsonAsync(config);

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }
    }
}
