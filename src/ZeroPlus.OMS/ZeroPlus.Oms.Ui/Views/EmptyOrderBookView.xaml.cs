using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EmptyOrderBookView.xaml
    /// </summary>
    public partial class EmptyOrderBookView : ThemedWindow, IModuleView, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private const string MODULE_NAME = "Custom Orderbook";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public EmptyOrderBookView() : this(Guid.NewGuid().ToString())
        {
        }

        public EmptyOrderBookView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(EmptyOrderBookView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (object s, CancelEventArgs e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Loaded += RestoreLayout;
            Module = Module.CustomOrderBookLayout;
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
                else if (e.Column.FieldName == "DateAdded")
                {
                    e.DisplayText = dateTime.ToString("d");
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
            GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
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

                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);

                BarButtonItem exportToExcelButton = GetExportToExcelButton();
                BarButtonItem exportToDomFormatButton = GetExportToDomFormatButton();

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(editGridButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(exportToExcelButton);
                gridMenuEventArgs.Customizations.Add(exportToDomFormatButton);
            }
            else if (gridMenuEventArgs.MenuType == GridMenuType.RowCell)
            {
                BarButtonItem copySymbolButton = GetCopySymbolButton();
                BarButtonItem copyButton = GetCopyContentButton(column);
                BarButtonItem searchInTradesModule = GetSearchInTradesModuleButton(column);

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());

                gridMenuEventArgs.Customizations.Add(copyButton);
                gridMenuEventArgs.Customizations.Add(copySymbolButton);
                gridMenuEventArgs.Customizations.Add(searchInTradesModule);
            }
        }

        private BarButtonItem GetCopySymbolButton()
        {
            BarButtonItem copyContentButton = new()
            {
                Content = (CustomOrderGrid?.SelectedItems?.Count ?? 0) > 1 ? "Copy Symbols" : "Copy Symbol",
            };

            copyContentButton.ItemClick += CopySymbol;
            void CopySymbol(object sender, ItemClickEventArgs e)
            {
                copyContentButton.ItemClick -= CopySymbol;
                var symbols = "";
                foreach (var item in CustomOrderGrid.SelectedItems)
                {
                    if (item is OmsOrderModel { Symbol: not null } model)
                    {
                        symbols += model.Symbol + "\n";
                    }
                }
                symbols = symbols.TrimEnd();
                Clipboard.SetText(symbols);
            }

            return copyContentButton;
        }

        private BarButtonItem GetCopyContentButton(GridColumn column)
        {
            string selectedCellValue = CustomOrderGrid.GetCellValue(CustomOrderGrid.View.FocusedRowHandle, column)?.ToString();

            BarButtonItem copyContentButton = new()
            {
                Content = "Copy Cell Content",
            };

            copyContentButton.ItemClick += (s, e) => Clipboard.SetText(selectedCellValue);
            return copyContentButton;
        }

        private BarButtonItem GetSearchInTradesModuleButton(GridColumn column)
        {
            EmptyOrderBookViewModel dataContext = (EmptyOrderBookViewModel)DataContext;
            string selectedCellValue = CustomOrderGrid.GetCellValue(CustomOrderGrid.View.FocusedRowHandle, column)?.ToString();
            object timeAndSalesCommandParameter;
            OmsOrderModel selectedItem = (OmsOrderModel)CustomOrderGrid.SelectedItem;
            switch (column.FieldName)
            {
                case nameof(OmsOrderModel.UnderlyingSymbol):
                    timeAndSalesCommandParameter = new { Filter = "", SearchTerm = selectedCellValue, MLeg = selectedItem.IsComplexOrder };
                    break;
                default:
                    DateTime fillTime = selectedItem.LastUpdateTime.ToEastern();
                    DateTime minTime = fillTime.Date;
                    DateTime maxTime = fillTime.Date + TimeSpan.FromDays(1);
                    timeAndSalesCommandParameter = new { Filter = "", SearchTerm = selectedItem.Symbol + "," + OrderBookView.InvertTos(selectedItem.Symbol), MLeg = selectedItem.IsComplexOrder, ContainsTime = true, MinTime = minTime, MaxTime = maxTime };
                    break;
            }
            BarButtonItem searchInTradesModule = new()
            {
                Content = "Time and Sales",
                CommandParameter = timeAndSalesCommandParameter,
                Command = dataContext.SearchInNewTradesModuleCommand,
            };
            return searchInTradesModule;
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

        private BarButtonItem GetExportToExcelButton()
        {
            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Excel",
            };

            exportToExcelButton.ItemClick += ExportToExcel;
            return exportToExcelButton;
        }

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                DataViewBase tableView = CustomOrderGrid.View;
                if (tableView != null)
                {
                    ISaveFileDialogService saveFileDialogService = ((EmptyOrderBookViewModel)DataContext).SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"Order Book Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
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

        private BarButtonItem GetExportToDomFormatButton()
        {
            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Dominator File",
            };

            exportToExcelButton.ItemClick += ExportToDomFormat;
            return exportToExcelButton;
        }

        private void ExportToDomFormat(object sender, ItemClickEventArgs e)
        {
            try
            {
                List<OmsOrderModel> list = new();

                for (int i = 0; i < CustomOrderGrid.VisibleRowCount; i++)
                {
                    int rowHandle = CustomOrderGrid.GetRowHandleByVisibleIndex(i);
                    OmsOrderModel orderModel = (OmsOrderModel)CustomOrderGrid.GetRow(rowHandle);
                    list.Add(orderModel);
                }
                list = list.DistinctBy(x => x.SpreadId).ToList();

                ISaveFileDialogService saveFileDialogService = ((EmptyOrderBookViewModel)DataContext).SaveFileDialogService;
                saveFileDialogService.DefaultExt = "xlsx";
                saveFileDialogService.DefaultFileName = $"{"DOMINATOR SPREADS "} - {DateTime.Now:MM-dd-yyyy hh.mm} - {list.Count} spreads";
                saveFileDialogService.Filter = "Dominator List|*.XLSX";
                bool save = saveFileDialogService.ShowDialog();
                if (save)
                {
                    string filePath = saveFileDialogService.GetFullFileName();

                    Task.Run(() => ExportHelper.WriteSpreadsToFileUsingDominatorFormat(OmsCore.User.Username, filePath, list));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportToDomFormat));
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

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private void TableView_DropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    List<OmsOrder> orders = (List<OmsOrder>)e.Data.GetData(DataFormats.Serializable);
                    List<OmsOrderModel> newRecords = new();
                    foreach (OmsOrder order in orders)
                    {
                        OmsOrderModel orderModel = new();
                        orderModel.Update(order);
                        orderModel.ResetTransactionSpecificProperties();
                        newRecords.Add(orderModel);
                    }
                    DataObject dataObject = new();
                    dataObject.SetData(new RecordDragDropData(newRecords.ToArray()));
                    e.Data = dataObject;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TableView_DropRecord));
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

            if (CustomOrderGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                CustomOrderGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                CustomOrderGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in CustomOrderGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
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
            CustomOrderGrid.FilterCriteria = null;
            CustomOrderGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            CustomOrderGrid.ClearSorting();
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                CustomOrderGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            List<OmsOrder> orders = new();
            string symbol = "";
            foreach (OmsOrderModel orderModel in e.Records)
            {
                orders.Add(orderModel.ToOrder());
                symbol = orderModel.Symbol;
            }
            e.Data.SetData(DataFormats.Serializable, orders);
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
            }
        }

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }

        private void FilterToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is SimpleButton button)
            {
                button.IsChecked = true;
                if (Enum.TryParse((string)button.CommandParameter, out FilterType filterType))
                {
                    switch (filterType)
                    {
                        case FilterType.ALL:
                            UNIQUE_ORDERS.IsChecked = false;
                            FILLED.IsChecked = false;
                            UNIQUE.IsChecked = false;
                            break;
                        case FilterType.FILLED:
                            ALL.IsChecked = false;
                            UNIQUE_ORDERS.IsChecked = false;
                            UNIQUE.IsChecked = false;
                            break;
                        case FilterType.UNIQUE:
                            ALL.IsChecked = false;
                            UNIQUE_ORDERS.IsChecked = false;
                            FILLED.IsChecked = false;
                            break;
                        case FilterType.UNIQUE_ORDERS:
                            ALL.IsChecked = false;
                            FILLED.IsChecked = false;
                            UNIQUE.IsChecked = false;
                            break;
                    }
                }
            }
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            EmptyOrderBookViewModel dataContext = (EmptyOrderBookViewModel)DataContext;
            dataContext.Uid = Uid;
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
                _ = LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                if (DataContext is EmptyOrderBookViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(CustomOrderGrid)] = Helper.LayoutHelper.GetLayoutAsString(CustomOrderGrid),
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

                if (configDictionary.ContainsKey(nameof(CustomOrderGrid)))
                {
                    Helper.LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(CustomOrderGrid)], CustomOrderGrid);
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
                        tableCustomizationViewModel.Load(CustomOrderGrid, GridFieldNameToConfigMap);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();

            if (renderedOnly)
            {
                ScrollViewer scrollViewer = LayoutTreeHelper.GetVisualChildren(CustomOrderTable).OfType<ScrollViewer>().FirstOrDefault();
                if (scrollViewer != null)
                {
                    int bottomIndex = Convert.ToInt32(scrollViewer.ViewportHeight + scrollViewer.VerticalOffset);

                    for (int i = CustomOrderGrid.View.TopRowIndex; i < bottomIndex; i++)
                    {
                        int handle = CustomOrderGrid.GetRowHandleByVisibleIndex(i);
                        if (!CustomOrderGrid.IsValidRowHandle(handle))
                            continue;
                        object item = CustomOrderGrid.GetRow(handle);
                        if (item != null)
                        {
                            list.Add(Tuple.Create(i + 1, item));
                        }
                    }
                }
            }
            else
            {

                for (int i = 0; i < CustomOrderGrid.VisibleRowCount; i++)
                {
                    int rowHandle = CustomOrderGrid.GetRowHandleByVisibleIndex(i);
                    list.Add(Tuple.Create(i + 1, CustomOrderGrid.GetRow(rowHandle)));
                }
                if (startFromSelectedRow && CustomOrderGrid.SelectedItem != null)
                {
                    Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == CustomOrderGrid.SelectedItem);
                    if (selectedRow != null)
                    {
                        list = list.Skip(list.IndexOf(selectedRow)).ToList();
                    }
                }
            }
            return list;
        }

        public bool ItemIsVisible(object item)
        {
            for (int i = 0; i < CustomOrderGrid.VisibleRowCount; i++)
            {
                int rowHandle = CustomOrderGrid.GetRowHandleByVisibleIndex(i);
                object check = CustomOrderGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
        }

        public HashSet<T> GetVisibleItems<T>()
        {
            HashSet<T> list = null;
            for (int i = 0; i < CustomOrderGrid.VisibleRowCount; i++)
            {
                int rowHandle = CustomOrderGrid.GetRowHandleByVisibleIndex(i);
                OmsOrderModel orderModel = (OmsOrderModel)CustomOrderGrid.GetRow(rowHandle);
                if (orderModel is T itemT)
                {
                    list ??= new HashSet<T>();
                    list.Add(itemT);
                }
            }
            return list;
        }
    }
}
