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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for TradesView.xaml
    /// </summary>
    public partial class TradesView : ThemedWindow, IModuleView, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private const string MODULE_NAME = "Trades";
        protected const string DefaultConfigGroupName = "Admin";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly Regex _legSplitRegex = new(@"(?=[-+])", RegexOptions.Compiled);

        private bool _layoutRestored;
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public TradesView() : this(Guid.NewGuid().ToString())
        {
        }

        public TradesView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(TradesView);
            Closed += TradesView_Closed;
            Loaded += RestoreLayout;
            Module = Module.TradesLayout;
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
        }

        private void TradesView_Closed(object sender, EventArgs e)
        {
            TradesViewModel dataContext = (TradesViewModel)DataContext;
            dataContext.Dispose();
        }

        private void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (e.Column.FieldName == "LutTimeOnly")
                {
                    e.DisplayText = dateTime.ToString("T");
                }
                else if (e.Column.FieldName == "DateAdded")
                {
                    e.DisplayText = dateTime.ToString("d");
                }
                else if (e.Column.FieldName.StartsWith("Expiration"))
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
                    e.DisplayText = dateTime.ToString("dd-MMM-yy hh:mm:ss.ffff tt");
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
                            col.Name = col.FieldName.Replace(".", "").Replace("-", "Remove").Replace(" ", "").Replace("%", "Percent");
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
                TradesViewModel viewModel = (TradesViewModel)DataContext;
                GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;

                if (OmsCore.Config.CustomPermCombinations.Count > 0)
                {
                    BarSubItem customPermsButton = GetCustomPermsButton();
                    gridMenuEventArgs.Customizations.Add(customPermsButton);
                }
                var selectedItem = (OpraDatabaseTradeModel)TradeGrid.SelectedItem;

                string selectedCellValue = TradeGrid.GetCellValue(TradeGrid.View.FocusedRowHandle, column).ToString();
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

                    searchTerm = selectedItem.Symbol;
                    selectedCellValue = selectedItem.SpreadId;
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

#if RELEASE
                if (selectedItem.Condition.Contains("Stock"))
#endif
                {
                    BarButtonItem stockTiedButton = new()
                    {
                        Content = "Open in Order Ticket (w/ stock leg)",
                        CommandParameter = selectedItem,
                        Command = viewModel.OpenInStockTiedTicketCommand,
                    };
                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(stockTiedButton);
                }

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(filterInNewOrderBookButton);
                gridMenuEventArgs.Customizations.Add(filterInTradesButton);

                DominatorsManagerModel dominatorsManager = (DataContext as TradesViewModel).DominatorsManagerModel;
                if (dominatorsManager.Dominators.Count > 0)
                {
                    BarSubItem sendToDomButton = new()
                    {
                        Content = "Send To Dominator"
                    };

                    foreach (IGrouping<string, DominatorModel> dominator in dominatorsManager.Dominators.GroupBy(x => x.Host))
                    {
                        BarSubItem domSubMenu = new()
                        {
                            Content = dominator.Key
                        };
                        foreach (DominatorModel instance in dominator.ToList())
                        {
                            BarButtonItem instanceButton = new()
                            {
                                Content = instance.Instance,
                                CommandParameter = Tuple.Create((OpraDatabaseTradeModel)TradeGrid.SelectedItem, instance),
                                Command = viewModel.SendToDominatorCommand,
                            };
                            domSubMenu.Items.Add(instanceButton);
                        }
                        sendToDomButton.Items.Add(domSubMenu);
                    }
                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(sendToDomButton);
                }
            }
        }

        private BarSubItem GetCustomPermsButton()
        {
            TradesViewModel dataContext = (TradesViewModel)DataContext;
            BarSubItem sendToDomButton = new()
            {
                Content = "Open Basket And Load Perms"
            };

            foreach (string customPerm in OmsCore.Config.CustomPermCombinations.Keys)
            {
                BarButtonItem permButton = new()
                {
                    Content = customPerm,
                    CommandParameter = customPerm,
                    Command = dataContext.OpenInBasketAndLoadCustomPermCommand,
                };
                sendToDomButton.Items.Add(permButton);
            }

            return sendToDomButton;
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
                    ISaveFileDialogService saveFileDialogService = ((TradesViewModel)DataContext).SaveFileDialogService;
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
            List<OmsOrder> orders = new();
            string symbol = "";
            foreach (OpraDatabaseTradeModel trade in e.Records)
            {
                orders.Add(ToOrder(trade));
                symbol = trade.Symbol;
            }
            e.Data.SetData(DataFormats.Serializable, orders);
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
            }
        }

        public OmsOrder ToOrder(OpraDatabaseTradeModel trade)
        {
            var legs = ParseLegsFromTos(trade.Symbol);
            int legCount = legs.Count;

            int divisor = 1;
            if (legCount > 0)
            {
                var quantities = legs.Select(x => x.Quantity).ToList();
                Comms.Models.Math.Helper.GetLCDAdjustedList(quantities, out divisor);
            }

            return new OmsOrder
            {
                Symbol = trade.UnderSymbol,
                UnderlyingSymbol = trade.UnderSymbol,
                Price = trade.Price,
                Legs = legs,
                Quantity = divisor,
                MultiLeg = legCount > 1
            };
        }

        private List<OmsOrderLeg> ParseLegsFromTos(string symbol)
        {
            string[] legSymbols = _legSplitRegex.Split(symbol);
            var legs = new List<OmsOrderLeg>(legSymbols.Length);
            int actualIndex = 0;

            for (int i = 0; i < legSymbols.Length; i++)
            {
                string rawLeg = legSymbols[i];
                if (string.IsNullOrWhiteSpace(rawLeg)) continue;

                Side side = rawLeg[0] == '-' ? Side.Sell : Side.Buy;

                ReadOnlySpan<char> legSpan = rawLeg.AsSpan();
                if (legSpan[0] == '+' || legSpan[0] == '-')
                {
                    legSpan = legSpan.Slice(1);
                }

                int qty = 1;
                string finalSymbol;

                int asteriskIndex = legSpan.IndexOf('*');
                if (asteriskIndex != -1)
                {
                    qty = Math.Abs(int.Parse(legSpan.Slice(0, asteriskIndex)));
                    finalSymbol = legSpan.Slice(asteriskIndex + 1).ToString();
                }
                else
                {
                    finalSymbol = legSpan.ToString();
                }

                legs.Add(new OmsOrderLeg
                {
                    LegID = $"leg{actualIndex++}",
                    Symbol = finalSymbol,
                    Quantity = qty,
                    Ratio = qty,
                    Side = side,
                });
            }

            return legs;
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

            if (TradeGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                TradeGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                TradeGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in TradeGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
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
            TradeGrid.FilterCriteria = null;
            TradeGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            TradeGrid.ClearSorting();
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                TradeGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();
            for (int i = 0; i < TradeGrid.VisibleRowCount; i++)
            {
                int rowHandle = TradeGrid.GetRowHandleByVisibleIndex(i);
                list.Add(Tuple.Create(i + 1, TradeGrid.GetRow(rowHandle)));
            }
            if (startFromSelectedRow && TradeGrid.SelectedItem != null)
            {
                Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == TradeGrid.SelectedItem);
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
            for (int i = 0; i < TradeGrid.VisibleRowCount; i++)
            {
                int rowHandle = TradeGrid.GetRowHandleByVisibleIndex(i);
                object check = TradeGrid.GetRow(rowHandle);
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
            TradesViewModel dataContext = (TradesViewModel)DataContext;
            dataContext.Uid = Uid;
            OmsCore omsCore = dataContext.OmsCore;
            omsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => omsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = omsCore.User.Username,
                Group = omsCore.User.Username,
                OwnerId = omsCore.User.ID,
            };
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
                dataContext.LoadViewModelConfig(Uid);
            }
            LoadQuickAccessConfigs();
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

        public void BrowseLayouts(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;
                void loader(object sender, RoutedEventArgs args)
                {
                    windowView.Loaded -= loader;
                    viewModel?.SetModule(Module);
                }
                windowView.Loaded += loader;
                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        private async void RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                ConfigSave configSave = await (DataContext as TradesViewModel).OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
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
                if (DataContext is TradesViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            TradesViewModel datacontext = (TradesViewModel)DataContext;
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(TradeGrid)] = LayoutHelper.GetLayoutAsString(TradeGrid),
                [nameof(TradesModuleConfig)] = datacontext.GetConfigJson(),
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
                TradesViewModel dataContext = (TradesViewModel)DataContext;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    await dataContext.InvokeReady();
                    return;
                }
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));
                _ = dataContext.LoadConfigFromJsonAsync(configDictionary[nameof(TradesModuleConfig)]);

                LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(TradeGrid)], TradeGrid);
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
                    tableCustomizationViewModel.Load(TradeGrid, GridFieldNameToConfigMap);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private void TableView_DropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                TradesViewModel viewModel = (TradesViewModel)DataContext;
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    List<OmsOrder> orders = (List<OmsOrder>)e.Data.GetData(DataFormats.Serializable);
                    List<BasketTraderItemModel> newRecords = new();
                    OmsOrder order = orders.FirstOrDefault();
                    if (order != null)
                    {
                        viewModel.Symbol = order.Symbol;
                        viewModel.SelectedTime = "Today";
                        viewModel.LegTypes = order.Legs.Count > 1 ? LegTypes.MLeg : LegTypes.Single;
                        viewModel.Refresh();
                    }
                    e.Data = null;
                }
                else if (e.Data.GetDataPresent(DataFormats.CommaSeparatedValue))
                {
                    string dragItem = e.Data.GetData(DataFormats.CommaSeparatedValue, false).ToString();
                    viewModel.Symbol = dragItem;
                    viewModel.Refresh();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TableView_DropRecord));
            }
        }

        private void LoadQuickAccessConfigs()
        {
            if (DataContext is not TradesViewModel viewModel)
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
            if (DataContext is not TradesViewModel viewModel || viewModel.SelectedConfig == null)
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

        private void ReloadConfigsSelected(object sender, RoutedEventArgs e)
        {
            LoadQuickAccessConfigs();
        }
    }
}
