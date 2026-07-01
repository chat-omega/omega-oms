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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Data;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using GridLengthConverter = System.Windows.GridLengthConverter;
using GroupBox = DevExpress.Xpf.LayoutControl.GroupBox;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for BasketTraderContentView.xaml
    /// </summary>
    public partial class BasketTraderContentView : UserControl, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private const string MODULE_NAME = "Basket Trader";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly SolidColorBrush _borderHighlightColor = new((Color)ColorConverter.ConvertFromString("#3675a2")!);
        private readonly SolidColorBrush _borderRegularColor = new((Color)ColorConverter.ConvertFromString("#3f3f46")!);
        private bool _layoutRestored;
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public Dictionary<string, ColumnConfigModel> BasketTradersGridFieldNameToConfigMap { get; set; }
        public BasketTraderViewModel ViewModel => DataContext as BasketTraderViewModel;
        public bool BasketExpanded { get; private set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public BasketTraderContentView() : this(Guid.NewGuid().ToString())
        {
        }

        public BasketTraderContentView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Reset();
            Name = nameof(BasketTraderView);
            Module = Module.BasketTraderLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            BasketTradersGridFieldNameToConfigMap = new();

            GridLengthConverter glc = new();
            GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0")!;
            ExpandCollapseGridButton.Content = 4;
        }

        private void EdgeBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.Key == Key.Up || e.Key == Key.Down) && ViewModel != null && ViewModel.SubmitAllRunning)
            {
                e.Handled = true;
                return;
            }
        }

        private void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date)
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName is "LutTimeOnly" or "FirmLastTradeTime")
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
                if (OmsCore.Config.CustomPermCombinations.Any())
                {
                    BarSubItem customPermsButton = GetCustomPermsButton();
                    BarSubItem newBasketPermsButton = GetNewCustomPermsButton();

                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(customPermsButton);
                    gridMenuEventArgs.Customizations.Add(newBasketPermsButton);
                }

                DominatorsManagerModel dominatorsManager = ViewModel.DominatorsManagerModel;
                if (dominatorsManager.Dominators.Any())
                {
                    BarSubItem sendToDomButton = GetSendToDominatorButton();

                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(sendToDomButton);
                }
            }
        }

        private BarSubItem GetSendToDominatorButton()
        {
            BarSubItem sendToDomButton = new()
            {
                Content = "Send To Dominator"
            };
            DominatorsManagerModel dominatorsManager = ViewModel.DominatorsManagerModel;
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
                        CommandParameter = Tuple.Create((BasketTraderItemModel)BasketGrid.SelectedItem, instance),
                        Command = ViewModel.SendToDominatorCommand,
                    };
                    domSubMenu.Items.Add(instanceButton);
                }
                sendToDomButton.Items.Add(domSubMenu);
            }

            return sendToDomButton;
        }

        private BarSubItem GetCustomPermsButton()
        {
            BarSubItem button = new()
            {
                Content = "Load Custom Perms"
            };

            foreach (string customPerm in OmsCore.Config.CustomPermCombinations.Keys)
            {
                BarButtonItem permButton = new()
                {
                    Content = customPerm,
                    CommandParameter = customPerm,
                    Command = ViewModel.LoadCustomPermCommand,
                };
                button.Items.Add(permButton);
            }

            return button;
        }

        private BarSubItem GetNewCustomPermsButton()
        {
            BarSubItem button = new()
            {
                Content = "Load Custom Perms in New Basket"
            };

            foreach (string customPerm in OmsCore.Config.CustomPermCombinations.Keys)
            {
                BarButtonItem permButton = new()
                {
                    Content = customPerm,
                    CommandParameter = customPerm,
                    Command = ViewModel.LoadCustomPermNewBasketCommand,
                };
                button.Items.Add(permButton);
            }

            return button;
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
                TableView tableView = BasketTable;
                if (tableView != null)
                {
                    ISaveFileDialogService saveFileDialogService = (ViewModel).SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"Basket Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
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

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            TableViewHitInfo hitInfo = BasketTable.CalcHitInfo(Mouse.GetPosition(BasketTable));
            if (hitInfo.Column == null ||
                hitInfo.Column?.FieldName == "CloseAllPositions" ||
                hitInfo.Column?.FieldName == "Submit" ||
                hitInfo.Column?.FieldName == "Close" ||
                hitInfo.Column?.FieldName == "ContraSubmit" ||
                hitInfo.Column?.FieldName == "ContraCancel" ||
                hitInfo.Column?.FieldName == "Auto" ||
                hitInfo.Column?.FieldName == "Remove" ||
                hitInfo.Column?.FieldName == "Cancel")
            {
                e.AllowDrag = false;
                e.Handled = true;
                e.AllowedEffects = DragDropEffects.None;
                return;
            }
            else
            {
                List<OmsOrder> orders = new();
                string symbol = "";
                foreach (BasketTraderItemModel basketItem in e.Records)
                {
                    orders.Add(basketItem.ToOrder());
                    symbol = basketItem.Symbol;
                }
                e.Data.SetData(DataFormats.Serializable, orders);
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
                }
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

        private async void TableView_DropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    var data = e.Data.GetData(DataFormats.Serializable);
                    var loaded = await LoadAsOrderModel(data);
                    if (!loaded)
                    {
                        loaded = LoadAsTos(data);
                    }
                    if (loaded)
                    {
                        e.Data = null;
                        return;
                    }
                }
                if (e.Data.GetDataPresent(DataFormats.CommaSeparatedValue))
                {
                    string dragItem = e.Data.GetData(DataFormats.CommaSeparatedValue, false).ToString();
                    string[] items = dragItem.Split('\n');
                    List<string> uniqueSpreads = new();
                    for (int i = 0; i < items.Length; i++)
                    {
                        string spreadId = items[i];
                        if (!string.IsNullOrWhiteSpace(spreadId))
                        {
                            spreadId = spreadId.Trim();
                            uniqueSpreads.Add(spreadId);
                        }
                    }
                    _ = ViewModel.LoadFromSpreadIdsAsync(uniqueSpreads.Distinct().Select(x => Tuple.Create(x, double.NaN)).ToList());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TableView_DropRecord));
            }
        }

        private async Task<bool> LoadAsOrderModel(object data)
        {
            try
            {
                List<OmsOrder> orders = (List<OmsOrder>)data;
                if (orders != null)
                {
                    List<BasketTraderItemModel> newRecords = new();
                    bool loadEdgeOverride = false;

                    if (OmsCore.Config.PromptToLoadEdgeOverrideWhenDraggingToBasket)
                    {
                        if (orders.Any(x => !double.IsNaN(x.EdgeOverride)))
                        {
                            loadEdgeOverride = await ViewModel.GetVerificationAsync("Would you like to apply Edge Override settings to incoming spreads?", "Basket Trader - ZeroPlus OMS");
                        }
                    }

                    var loadTasks = new List<Task>(orders.Count);
                    foreach (OmsOrder order in orders)
                    {
                        BasketTraderItemModel basketItem = new(ViewModel, Dispatcher, OmsCore);
                        var task = basketItem.LoadFromOrder(order);
                        loadTasks.Add(task);
                        basketItem.EdgeOverride = loadEdgeOverride ? Math.Abs(order.EdgeOverride) : double.NaN;
                        newRecords.Add(basketItem);
                    }

                    await Task.WhenAll(loadTasks);
                    _ = ViewModel.AddMultipleToBasketAsync(newRecords);
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool LoadAsTos(object data)
        {
            try
            {
                List<string> orders = (List<string>)data;
                if (orders != null)
                {
                    _ = ViewModel.LoadFromSpreadIdsAsync(orders.Distinct().Select(x => Tuple.Create(x, double.NaN)).ToList());
                }

                return true;
            }
            catch (Exception)
            {
                return false;
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
                AllowEditing = colTemplate.AllowEditing ? DefaultBoolean.True : DefaultBoolean.False,
            };

            if (BasketGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                BasketGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                BasketGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in BasketGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
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

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();
            for (int i = 0; i < BasketGrid.VisibleRowCount; i++)
            {
                int rowHandle = BasketGrid.GetRowHandleByVisibleIndex(i);
                list.Add(Tuple.Create(i + 1, BasketGrid.GetRow(rowHandle)));
            }
            if (startFromSelectedRow && BasketGrid.SelectedItem != null)
            {
                Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == BasketGrid.SelectedItem);
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
            for (int i = 0; i < BasketGrid.VisibleRowCount; i++)
            {
                int rowHandle = BasketGrid.GetRowHandleByVisibleIndex(i);
                object check = BasketGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            BasketGrid.FilterCriteria = null;
            BasketGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            BasketGrid.ClearSorting();
        }

        private void BasketGrid_ExpandCollapse(object sender, RoutedEventArgs e)
        {
            if (BasketExpanded)
            {
                BasketGrid.CollapseAllGroups();
                for (int i = 0; i < BasketGrid.VisibleRowCount; i++)
                {
                    int rowHandle = BasketGrid.GetRowHandleByVisibleIndex(i);
                    BasketGrid.CollapseMasterRow(rowHandle);
                }
            }
            else
            {
                BasketGrid.ExpandAllGroups();
                for (int i = 0; i < BasketGrid.VisibleRowCount; i++)
                {
                    if (i > 20)
                    {
                        break;
                    }
                    int rowHandle = BasketGrid.GetRowHandleByVisibleIndex(i);
                    BasketGrid.ExpandMasterRow(rowHandle);
                }
            }
            BasketExpanded = !BasketExpanded;
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                BasketGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
                ExpandCollapseGridButton.Content = 4;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("750")!;
                ExpandCollapseGridButton.Content = 3;
            }
        }

        private void CutMorphSearchBox(object sender, ItemClickEventArgs e)
        {
            MorphSearchBox.Cut();
        }

        private void CopyMorphSearchBox(object sender, ItemClickEventArgs e)
        {
            MorphSearchBox.Copy();
        }

        private void PasteMorphSearchBox(object sender, ItemClickEventArgs e)
        {
            MorphSearchBox.EditValue = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        private void MorphDisplayText(object sender, CustomDisplayTextEventArgs e) => e.DisplayText = ViewModel?.MorphSummary;

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, BasketTradersGridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
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
                viewModel.ShowLocation = true;

                if (Parent is Border { Parent: BasketTraderView basketTrader })
                {
                    viewModel.SetAsDefault = basketTrader.UsingDefaultConfig;
                }

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

        public void SaveLayout(bool saveDefault, bool saveLocation = false, bool withItems = false)
        {
            Dispatcher.Invoke(() =>
            {
                ConfigSave.ConfigJson = GetConfigAsJson(!saveLocation, withItems);

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

                if (Parent is Border { Parent: BasketTraderView { UsingDefaultConfig: true } view })
                {
                    if (saveDefault)
                    {
                        view.UsingDefaultConfig = true;
                        view.DefaultConfig = ConfigSave;
                    }
                    else
                    {
                        view.DefaultConfig.Title = view.DefaultConfig.Title + " [Local]";
                        export = JsonConvert.SerializeObject(view.DefaultConfig);
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
                if (Parent is Border { Parent: BasketTraderView basketTraderView })
                {
                    basketTraderView.RestoreFromConfigSave(configSave);
                    return;
                }

                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                LoadConfigFromJson(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            var defaultTitle = configSave.Title;
            if (!string.IsNullOrWhiteSpace(defaultTitle))
            {
                if (DataContext is BasketTraderViewModel viewModel)
                {
                    defaultTitle = ModuleWindow.CleanTitle(defaultTitle);

                    viewModel.ModuleTitle = defaultTitle + (defaultTitle != MODULE_NAME ? " - " + MODULE_NAME : "");
                }
            }
        }

        public string GetConfigAsJson(bool isDefault = false, bool withItems = false)
        {
            if (Parent is Border { Parent: BasketTraderView basketTraderView })
            {
                return basketTraderView.GetConfigAsJson(isDefault, withItems);
            }

            if (DataContext is not BasketTraderViewModel dataContext)
            {
                return "";
            }

            GridLengthConverter glc = new();
            string splitterHeight = glc.ConvertToString(GridSplitterCol.Width)?.Replace("*", "");

            Dictionary<string, int> subModuleOrderMap = new();

            System.Collections.IList list = SubmodulePanel.Children;
            for (int i = 0; i < list.Count; i++)
            {
                GroupBox item = (GroupBox)list[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                {
                    subModuleOrderMap[item.Name] = i;
                }
            }

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(BasketTraderConfig)] = dataContext.GetConfigSerialized(withItems: withItems, onlyLayout: true),
                [nameof(BasketGrid)] = Helper.LayoutHelper.GetLayoutAsString(BasketGrid),
                [nameof(GridSplitterCol)] = splitterHeight,
                [nameof(BasketTradersGridFieldNameToConfigMap)] = JsonConvert.SerializeObject(BasketTradersGridFieldNameToConfigMap),
                [nameof(SubmodulePanel)] = JsonConvert.SerializeObject(subModuleOrderMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public void LoadDefault()
        {
            SetQuickAccessWidth();
            string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
            string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");

            if (!_layoutRestored)
            {
                _layoutRestored = true;

                if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                {
                    string export = File.ReadAllText(defaultExportPath);
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                    RestoreFromConfigSave(configSave);
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (Parent is Border { Parent: BasketTraderView view })
                        {
                            view.DefaultConfig = JsonConvert.DeserializeObject<ConfigSave>(export);
                            view.UsingDefaultConfig = true;
                        }
                    });
                }
                else
                {
                    RestoreFromConfigSave(ConfigSave);
                }
            }
        }

        internal void LoadConfigFromJson(string configJson, bool loadViewModelConfig = true)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }

                if (Parent is Border { Parent: BasketTraderView basketTraderView })
                {
                    basketTraderView.LoadConfigFromJsonAsync(configJson);
                    return;
                }

                Dictionary<string, string> configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson);

                Dictionary<string, ColumnConfigModel> configMap = null;
                if (configDictionary.ContainsKey(nameof(BasketTradersGridFieldNameToConfigMap)))
                {
                    if (configDictionary.TryGetValue(nameof(BasketTradersGridFieldNameToConfigMap), out string fieldMapConfig))
                    {
                        configMap = JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig);
                    }
                }

                Dictionary<string, int> subModuleMap = null;
                if (configDictionary.TryGetValue(nameof(SubmodulePanel), out string subModuleDictionary))
                {
                    subModuleMap = JsonConvert.DeserializeObject<Dictionary<string, int>>(subModuleDictionary);
                }

                Dispatcher?.BeginInvoke(() =>
                {
                    if (loadViewModelConfig)
                    {
                        ModuleViewModelBase dataContext = (ModuleViewModelBase)DataContext;

                        if (dataContext != null && configDictionary.ContainsKey(nameof(BasketTraderConfig)))
                        {
                            configDictionary.TryGetValue(nameof(BasketTraderConfig), out string basketTraderConfig);
                            dataContext.LoadConfigFromJsonAsync(basketTraderConfig);
                        }
                    }

                    GridLengthConverter glc = new();

                    if (configDictionary.TryGetValue(nameof(GridSplitterCol), out var splitterSettings))
                    {
                        GridLength? length = null;
                        try
                        {
                            length = (GridLength)glc.ConvertFromString(splitterSettings)!;
                        }
                        catch
                        { /* ignored */}

                        length ??= (GridLength)glc.ConvertFromString("0")!;

                        GridSplitterCol.Width = length.Value;
                    }

                    if (configDictionary.TryGetValue(nameof(BasketGrid), out var gridSettings))
                    {
                        gridSettings = Helper.LayoutHelper.RemoveHiddenAndDuplicateColumns(gridSettings, Helper.LayoutHelper.BasketDeadColumns);
                        Helper.LayoutHelper.RestoreLayoutFromString(gridSettings, BasketGrid);
                    }

                    if (configMap != null)
                    {
                        BasketTradersGridFieldNameToConfigMap = configMap;
                        TableCustomizationViewModel tableCustomizationViewModel = new();
                        tableCustomizationViewModel.Load(BasketGrid, BasketTradersGridFieldNameToConfigMap);
                    }

                    if (subModuleMap != null)
                    {
                        System.Collections.IList list = SubmodulePanel.Children;
                        List<GroupBox> copy = new();
                        foreach (var t in list)
                        {
                            GroupBox item = (GroupBox)t;
                            if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                            {
                                copy.Add(item);
                            }
                        }
                        SubmodulePanel.Children.Clear();
                        List<GroupBox> sorted = copy.Where(x => subModuleMap.TryGetValue(x.Name, out _)).OrderBy(x => subModuleMap[x.Name]).ToList();
                        foreach (GroupBox item in sorted)
                        {
                            SubmodulePanel.Children.Add(item);
                        }
                        foreach (GroupBox item in copy)
                        {
                            if (!sorted.Contains(item))
                            {
                                SubmodulePanel.Children.Add(item);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJson));
            }
        }

        private void ShowColumnChooser(object sender, RoutedEventArgs e)
        {
            BasketTable.ShowColumnChooser();
        }

        private void ToggleEdit(object sender, MouseButtonEventArgs e)
        {
            if (NameEdit.IsReadOnly)
            {
                NameEdit.IsReadOnly = false;
                NameEdit.Focusable = true;
                NameEdit.Cursor = Cursors.IBeam;
            }
        }

        private void NameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Reset();
            }
        }

        private void Reset()
        {
            NameEdit.IsReadOnly = true;
            NameEdit.Focusable = false;
            NameEdit.Cursor = Cursors.Arrow;
            NameEdit.CaretIndex = NameEdit.Text.Length;
            NameEditButton.IsChecked = false;
        }

        private void EditName(object sender, RoutedEventArgs e)
        {
            NameEdit.IsReadOnly = false;
            NameEdit.Focusable = true;
            NameEdit.Cursor = Cursors.IBeam;
            NameEdit.SelectAll();
            NameEdit.Focus();
        }

        private void SetName(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void SubModuleDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }

        private void SubModuleDragLeave(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (sender is GroupBox sourceBox)
            {
                sourceBox.BorderBrush = _borderRegularColor;
                sourceBox.BorderThickness = new Thickness(1);
            }
        }

        private void SubModuleDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(GroupBox)) &&
                e.Data.GetData(typeof(GroupBox)) is GroupBox dragged &&
                sender is GroupBox sourceBox)
            {
                int draggedIndex = SubmodulePanel.Children.IndexOf(dragged);
                int sourceTextBlockIndex = SubmodulePanel.Children.IndexOf(sourceBox);
                sourceBox.BorderBrush = _borderHighlightColor;
                if (draggedIndex < sourceTextBlockIndex)
                {
                    sourceBox.BorderThickness = new Thickness(1, 1, 5, 1);
                }
                else if (draggedIndex > sourceTextBlockIndex)
                {
                    sourceBox.BorderThickness = new Thickness(5, 1, 1, 1);
                }
                else
                {
                    sourceBox.BorderThickness = new Thickness(1, 1, 1, 1);
                }
            }
            e.Handled = true;
        }

        private void SubModuleMouseDown(object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    if (sender is GroupBox block)
                    {
                        DragDrop.DoDragDrop(block, block, DragDropEffects.Move);
                    }
                    break;
            }
        }

        private void SubModulePanelDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(GroupBox)))
                {
                    GroupBox dragged = (GroupBox)e.Data.GetData(typeof(GroupBox));
                    if (dragged != null)
                    {
                        int draggedIndex = SubmodulePanel.Children.IndexOf(dragged);
                        object parent = e.Source;
                        if (parent is not GroupBox)
                        {
                            parent = ((UIElement)parent).FindElementByTypeInParents<GroupBox>(SubmodulePanel);
                        }
                        if (parent is GroupBox sourceBox)
                        {
                            sourceBox.BorderBrush = _borderRegularColor;
                            sourceBox.BorderThickness = new Thickness(1);
                            dragged.BorderBrush = _borderRegularColor;
                            dragged.BorderThickness = new Thickness(1);
                            int sourceTextBlockIndex = SubmodulePanel.Children.IndexOf(sourceBox);

                            if (sourceTextBlockIndex >= 0)
                            {
                                if (draggedIndex < sourceTextBlockIndex)
                                {
                                    SubmodulePanel.Children.RemoveAt(draggedIndex);
                                    for (int i = draggedIndex + 1; i <= sourceTextBlockIndex; i++)
                                    {
                                        UIElement item = SubmodulePanel.Children[i];
                                        SubmodulePanel.Children.RemoveAt(i);
                                        SubmodulePanel.Children.Insert(i - 1, item);
                                    }

                                    SubmodulePanel.Children.Insert(sourceTextBlockIndex, dragged);
                                }
                                else
                                {
                                    SubmodulePanel.Children.RemoveAt(draggedIndex);
                                    for (int i = draggedIndex - 1; i >= sourceTextBlockIndex; i--)
                                    {
                                        UIElement item = SubmodulePanel.Children[i];
                                        SubmodulePanel.Children.RemoveAt(i);
                                        SubmodulePanel.Children.Insert(i + 1, item);
                                    }

                                    SubmodulePanel.Children.Insert(sourceTextBlockIndex, dragged);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void EdgeOverrideValueChanged(object sender, EditValueChangedEventArgs eventArgs)
        {
            System.Collections.IList selected = BasketGrid.SelectedItems;
            SpinEdit spinEdit = sender as SpinEdit;
            if ((Keyboard.IsKeyDown(Key.LeftAlt) ||
                 Keyboard.IsKeyDown(Key.RightAlt)) &&
                selected.Count > 1 &&
                spinEdit.IsMouseOver)
            {
                bool useDynamic = false;
                double change = 0.0;

                if (ViewModel.BasketSettings.DynamicUpdateEdgeOverrides)
                {
                    if (eventArgs.NewValue != null && eventArgs.OldValue != null)
                    {
                        useDynamic = true;
                        double value = (double)eventArgs.NewValue - (double)eventArgs.OldValue;
                        change = Math.Round(value, 2);
                    }
                }

                foreach (object item in selected)
                {
                    if (item is BasketTraderItemModel basketItem && eventArgs.NewValue is double newEdgeOverride)
                    {
                        if (useDynamic)
                        {
                            basketItem.EdgeOverride += change;
                        }
                        else
                        {
                            basketItem.EdgeOverride = newEdgeOverride;
                        }
                    }
                }
            }
        }

        private void QtyValueChanged(object sender, EditValueChangedEventArgs eventArgs)
        {
            System.Collections.IList selected = BasketGrid.SelectedItems;
            SpinEdit spinEdit = sender as SpinEdit;
            if ((Keyboard.IsKeyDown(Key.LeftAlt) ||
                 Keyboard.IsKeyDown(Key.RightAlt)) &&
                 selected.Count > 1 &&
                 spinEdit.IsMouseOver)
            {
                foreach (object item in selected)
                {
                    if (item is BasketTraderItemModel basketItem && eventArgs.NewValue is int newQty)
                    {
                        basketItem.UpdateQty(newQty);
                    }
                }
            }
        }

        private void AdjustedEdgeOverrideValueChanged(object sender, EditValueChangedEventArgs eventArgs)
        {
            System.Collections.IList selected = BasketGrid.SelectedItems;
            SpinEdit spinEdit = sender as SpinEdit;
            if ((Keyboard.IsKeyDown(Key.LeftAlt) ||
                 Keyboard.IsKeyDown(Key.RightAlt)) &&
                selected.Count > 1 &&
                spinEdit.IsMouseOver)
            {
                bool useDynamic = false;
                double change = 0.0;

                if (ViewModel.BasketSettings.DynamicUpdateEdgeOverrides)
                {
                    if (eventArgs.NewValue != null && eventArgs.OldValue != null)
                    {
                        useDynamic = true;
                        double value = (double)eventArgs.NewValue - (double)eventArgs.OldValue;
                        change = Math.Round(value, 2);
                    }
                }

                foreach (object item in selected)
                {
                    if (item is BasketTraderItemModel basketItem && eventArgs.NewValue is double newEdgeOverride)
                    {
                        if (useDynamic)
                        {
                            basketItem.AdjustedEdgeOverride += change;
                        }
                        else
                        {
                            basketItem.AdjustedEdgeOverride = newEdgeOverride;
                        }
                    }
                }
            }
        }

        private void EdgeCurveAdjustmentValueChanged(object sender, EditValueChangedEventArgs eventArgs)
        {
            System.Collections.IList selected = BasketGrid.SelectedItems;
            SpinEdit spinEdit = sender as SpinEdit;
            if ((Keyboard.IsKeyDown(Key.LeftAlt) ||
                 Keyboard.IsKeyDown(Key.RightAlt)) &&
                selected.Count > 1 &&
                spinEdit.IsMouseOver)
            {
                bool useDynamic = false;
                double change = 0.0;

                if (ViewModel.BasketSettings.DynamicUpdateEdgeOverrides)
                {
                    if (eventArgs.NewValue != null && eventArgs.OldValue != null)
                    {
                        useDynamic = true;
                        double value = (double)eventArgs.NewValue - (double)eventArgs.OldValue;
                        change = Math.Round(value, 2);
                    }
                }

                foreach (object item in selected)
                {
                    if (item is BasketTraderItemModel basketItem && eventArgs.NewValue is double newEdgeOverride)
                    {
                        if (useDynamic)
                        {
                            basketItem.EdgeCurveAdjustment += change;
                        }
                        else
                        {
                            basketItem.EdgeCurveAdjustment = newEdgeOverride;
                        }
                    }
                }
            }
        }

        private void ResetEdgeOverrideSpinEdit(object sender, ItemClickEventArgs e)
        {
            foreach (object selectedRow in BasketGrid.SelectedItems)
            {
                if (selectedRow is BasketTraderItemModel basketItem)
                {
                    basketItem.EdgeOverride = double.NaN;
                }
            }
        }

        private void ResetAdjustedEdgeOverrideSpinEdit(object sender, ItemClickEventArgs e)
        {
            foreach (object selectedRow in BasketGrid.SelectedItems)
            {
                if (selectedRow is BasketTraderItemModel basketItem)
                {
                    basketItem.AdjustedEdgeOverride = double.NaN;
                }
            }
        }

        private void ResetEdgeCurveAdjustmentSpinEdit(object sender, ItemClickEventArgs e)
        {
            foreach (object selectedRow in BasketGrid.SelectedItems)
            {
                if (selectedRow is BasketTraderItemModel basketItem)
                {
                    basketItem.EdgeCurveAdjustment = double.NaN;
                }
            }
        }

        private void ApplyQtyChange(object sender, RoutedEventArgs e)
        {
            if (sender is SimpleButton applyQtyButton)
            {
                if (applyQtyButton.Parent is DockPanel container)
                {
                    foreach (object child in container.Children)
                    {
                        if (child is SpinEdit qtySpinEdit)
                        {
                            BindingExpression bindingExpression = qtySpinEdit.GetBindingExpression(BaseEdit.EditValueProperty);
                            bindingExpression?.UpdateSource();
                        }
                    }
                }
            }
        }

        private void CheckForBasketSettingUpdate(object sender, MouseEventArgs e)
        {
            try
            {
                if (ViewModel.GetInstanceMode().IsAutoTraderInstance())
                {
                    ViewModel.AutoConfigUpdated = false;
                }
            }
            catch (Exception)
            {
                //ignored
            }
        }

        private void Clone(object sender, RoutedEventArgs e)
        {
            var config = GetConfigAsJson();
            ViewModel.Clone(config);
        }

        private void BasketButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            SimpleButton button = (SimpleButton)sender;
            button.ReleaseMouseCapture();
            if (e.LeftButton == MouseButtonState.Pressed && !ViewModel.IsEdgeScanFeedAutoTrader)
            {
                DataObject dataObject = new(DataFormats.Serializable, ViewModel.Uid);
                DragDrop.DoDragDrop(button, dataObject, DragDropEffects.Move);
            }
        }

        private void BasketButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SimpleButton button = sender as SimpleButton;
            button?.ReleaseMouseCapture();
        }

        private void BasketButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var basketDragItem = new BasketDragItem
            {
                Dispatcher = Dispatcher,
                ConfigAsJson = GetConfigAsJson(),
                ViewModel = ViewModel,
            };

            if (Parent is Border { Parent: BasketTraderView basketTraderView })
            {
                basketDragItem.Window = basketTraderView;
            }

            BasketGroupViewModel.BasketIdToBasketDragMap[ViewModel.Uid] = basketDragItem;
        }

        private void MinStrikeSortingChecked(object sender, RoutedEventArgs e)
        {
            BasketGrid.ClearSorting();
            MinStrikeColumn.Visible = true;
            MinStrikeColumn.SortOrder = ColumnSortOrder.Ascending;
        }

        private void MinStrikeSortingUnchecked(object sender, RoutedEventArgs e)
        {
            BasketGrid.ClearSorting();
            MinStrikeColumn.Visible = false;
        }
    }
}
