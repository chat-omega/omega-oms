using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using FilterType = ZeroPlus.Oms.Ui.ViewModels.FilterType;
using GridLengthConverter = System.Windows.GridLengthConverter;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for OrderBookView.xaml
    /// </summary>
    public delegate void WindowReadyEventHandler();
    public partial class OrderBookView : UserControl, ISupportCustomColumn
    {
        private const string MODULE_NAME = "Orderbook";
        private const double SCROLL_EDGE_OFFSET = 5.0;
        protected const string DefaultConfigGroupName = "Admin";

        public event WindowReadyEventHandler Ready;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;
        private bool _autoScroll;
        private readonly Dictionary<string, string> _headerOverride = new()
        {
            {"BrokerFee1", "Introducing Broker Fee" },
            {"BrokerFee2", "Executing Broker Fee" },
            {"ExchangeFee1", "Exchange Fee" },
            {"ExchangeFee2", "ORF Fee" },
            {"Fee1", "Regulatory Fees" },
        };

        public Module Module { get; private set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        private Dictionary<string, ColumnConfigModel> FilledOrdersGridFieldNameToConfigMap { get; set; }

        [Obsolete]
        public OrderBookView() : this(Guid.NewGuid().ToString())
        {
        }

        [Obsolete]
        public OrderBookView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(OrderBookView);
            Loaded += RestoreLayout;
            OmsCore.Config.ConfigChangedEvent += Config_ConfigChangedEvent;
            Unloaded += (s, e) => OmsCore.Config.ConfigChangedEvent -= Config_ConfigChangedEvent;
            Module = Module.OrderBookLayout;
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
            FilledOrdersGridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
            HideWorkingOrders();
        }

        private void Config_ConfigChangedEvent(Config.OmsConfig config, bool requiresRestart)
        {
            try
            {
                Dispatcher?.Invoke(() =>
                {
                    UpdateFonts(config);
                });
            }
            catch (Exception)
            {
            }
        }

        private void UpdateFonts(OmsConfig config)
        {
            if (OpenOrderGrid.FontSize != config.OrderBookFontSize)
            {
                OpenOrderGrid.FontSize = config.OrderBookFontSize;
            }

            if (FilledOrderGrid.FontSize != config.OrderBookFontSize)
            {
                FilledOrderGrid.FontSize = config.OrderBookFontSize;
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
                else if (e.Column.FieldName == "LutTimeOnly")
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString("hh:mm:ss.fff");
                }
                else if (e.Column.FieldName.StartsWith("HanweckTimestamp"))
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString("hh:mm:ss.fff");
                }
                else
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString(OmsCore.Config.LayoutDefaultDateTimeColumnFormat);
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

                        if (!string.IsNullOrEmpty(col.FieldName) && _headerOverride.TryGetValue(col.FieldName, out string newHeader))
                        {
                            col.Header = newHeader;
                        }

                        col.AddHandler(DXSerializer.CustomGetSerializablePropertiesEvent,
                            new CustomGetSerializablePropertiesEventHandler(Column_CustomGetSerializableProperties));
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

        private void Column_CustomGetSerializableProperties(object sender, CustomGetSerializablePropertiesEventArgs e)
        {
            e.SetPropertySerializable(ColumnBase.CellStyleProperty, new DXSerializable());
            e.SetPropertySerializable(ColumnBase.ActualCellStyleProperty, new DXSerializable());
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            TableView tableView = sender as TableView;
            GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                BarButtonItem removeColumnButton = GetRemoveColumnButton(column);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editColumnButton = GetEditColumnButton(column);
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                BarButtonItem addColumnButton = GetAddColumnButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);
                gridMenuEventArgs.Customizations.Add(addColumnButton);

                if (tableView != null && tableView == FilledOrdersTableView)
                {
                    BarButtonItem exportToExcelButton = GetExportToExcelButton();
                    BarButtonItem exportToDomFormatButton = GetExportToDomFormatButton();

                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(exportToExcelButton);
                    gridMenuEventArgs.Customizations.Add(exportToDomFormatButton);
                }
            }
            else if (gridMenuEventArgs.MenuType == GridMenuType.RowCell && tableView == FilledOrdersTableView)
            {
                BarButtonItem filterInNewOrderBookButton = GetFilterInOrderBookButton(column);
                BarButtonItem copySymbolButton = GetCopySymbolButton();
                BarButtonItem copyDebugButton = GetDebugInformationButton();
                BarButtonItem copyButton = GetCopyContentButton(column);
                BarButtonItem filterPermsInNewOrderBookButton = GetFilterPermsInOrderBookButton(column);
                BarButtonItem searchInTradesModule = GetSearchInTradesModuleButton(column);
                BarButtonItem chartOptionIvs = GetChartOptionIvsButton();

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());

                gridMenuEventArgs.Customizations.Add(copyButton);
                gridMenuEventArgs.Customizations.Add(copySymbolButton);
                gridMenuEventArgs.Customizations.Add(copyDebugButton);
                gridMenuEventArgs.Customizations.Add(filterInNewOrderBookButton);
                gridMenuEventArgs.Customizations.Add(filterPermsInNewOrderBookButton);
                gridMenuEventArgs.Customizations.Add(searchInTradesModule);
                gridMenuEventArgs.Customizations.Add(chartOptionIvs);

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem generateTemplateButton = GetTemplateButton(column);
                gridMenuEventArgs.Customizations.Add(generateTemplateButton);

                if (FilledOrderGrid.SelectedItems.Count > 1 && column.FieldName != nameof(OmsOrderModel.LastUpdateTime))
                {
                    BarButtonItem chartOrdersButton = GetChartButton(column);
                    gridMenuEventArgs.Customizations.Add(chartOrdersButton);
                }

                OrderBookViewModel viewModel = (OrderBookViewModel)DataContext;
                DominatorsManagerModel dominatorsManager = viewModel.DominatorsManagerModel;
                if (dominatorsManager.Dominators.Count > 0)
                {
                    BarSubItem sendToDomButton = GetSendToDominatorButton();
                    BarButtonItem blockFromDomButton = GetBlockFromDomButton();

                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(sendToDomButton);
                    gridMenuEventArgs.Customizations.Add(blockFromDomButton);
                }

                if (OmsCore.Config.ShowNagBotButtonInOrderbookV2)
                {
                    BarSubItem sendToNagbotButton = GetSendToNagBotButtonAsync();
                    if (sendToNagbotButton != null)
                    {
                        gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                        gridMenuEventArgs.Customizations.Add(sendToNagbotButton);
                    }
                }

                BarSubItem mainButton = new()
                {
                    Content = "Add To List"
                };
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(mainButton);

                OmsCore omsCore = viewModel.OmsCore;
                if (OmsCore.Config.ShowBasketConfigsInOrderbook)
                {
                    omsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTraderLayout)
                        .ContinueWith(t =>
                        {
                            if (t.Result != null)
                            {
                                List<ConfigSave> userConfigs = t.Result.Where(x => string.Equals(omsCore.User.Username, x.Username, StringComparison.OrdinalIgnoreCase)).ToList();

                                Dispatcher.BeginInvoke(() =>
                                {
                                    OpenInBasketSubButton.Items.Clear();
                                    foreach (ConfigSave config in userConfigs)
                                    {
                                        BarButtonItem instanceButton = new()
                                        {
                                            Content = config.Title,
                                            CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, config),
                                            Command = ((OrderBookViewModel)DataContext).OpenInBasketTraderWithConfigCommand,
                                        };
                                        OpenInBasketSubButton.Items.Add(instanceButton);
                                    }
                                    AddAutoPermButton(gridMenuEventArgs, userConfigs);
                                });
                            }
                        });
                }

                omsCore.GatewayClient.RequestConfigsAsync((int)Module.CustomList)
                    .ContinueWith(t =>
                    {
                        if (t.Result != null)
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var config in t.Result)
                                {
                                    var subButton = new BarButtonItem
                                    {
                                        Content = config.Title,
                                        CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, config),
                                        Command = viewModel.AddToListCommand,
                                    };
                                    mainButton.Items.Add(subButton);
                                }

                            });
                        }
                    });
            }
        }

        private BarButtonItem GetChartOptionIvsButton()
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            OmsOrderModel selectedItem = (OmsOrderModel)FilledOrderGrid.SelectedItem;
            BarButtonItem searchInTradesModule = new()
            {
                Content = "Chart Symbol Bid/Ask IV",
                CommandParameter = selectedItem,
                Command = dataContext.ChartSymbolBidAskIvCommand,
            };
            return searchInTradesModule;
        }

        private BarButtonItem GetBlockFromDomButton()
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            return new()
            {
                Content = "Block From Dominator",
                CommandParameter = new { Orders = FilledOrderGrid.SelectedItems },
                Command = dataContext.BlockFromDomCommand,
            };
        }

        private void AddAutoPermButton(GridMenuEventArgs gridMenuEventArgs, List<ConfigSave> userConfigs)
        {
            try
            {
                if (OrderBookViewModel.AutoPermSettings != null)
                {
                    OpenInAutoPermBasketButton.Items.Clear();
                    foreach (BasketAutoPermModel model in OrderBookViewModel.AutoPermSettings.DistinctBy(x => x.Title))
                    {
                        BarSubItem subButton = new()
                        {
                            Content = model.Title,
                        };
                        OpenInAutoPermBasketButton.Items.Add(subButton);
                        foreach (ConfigSave config in userConfigs)
                        {
                            BarButtonItem instanceButton = new()
                            {
                                Content = config.Title,
                                CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, model, config),
                                Command = ((OrderBookViewModel)DataContext).OpenInBasketAndAutoPermCommand,
                            };
                            subButton.Items.Add(instanceButton);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddAutoPermButton));
            }
        }

        private BarSubItem GetSendToDominatorButton()
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            BarSubItem sendToDomButton = new()
            {
                Content = "Send To Dominator"
            };
            DominatorsManagerModel dominatorsManager = (DataContext as OrderBookViewModel)?.DominatorsManagerModel;
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
                        CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, instance),
                        Command = dataContext.SendToDominatorCommand,
                    };
                    domSubMenu.Items.Add(instanceButton);
                }
                sendToDomButton.Items.Add(domSubMenu);
            }

            return sendToDomButton;
        }

        private BarSubItem GetSendToNagBotButtonAsync()
        {
            try
            {
                OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
                List<Tuple<string, BasketTraderViewModel>> baskets = dataContext.BasketGroupManagerModel.AllBaskets;
                BarSubItem sendToNagbotButton = new()
                {
                    Content = "Send To NagBot"
                };

                BarButtonItem nagBotSubMenu = new()
                {
                    Content = "New Basket Trader",
                    CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, "nagbot"),
                    Command = dataContext.OpenInNagbotBasketTraderCommand,
                };
                sendToNagbotButton.Items.Add(nagBotSubMenu);
                sendToNagbotButton.Items.Add(new BarItemSeparator());

                foreach (Tuple<string, BasketTraderViewModel> basket in baskets)
                {
                    string title = basket.Item1;

                    nagBotSubMenu = new BarButtonItem
                    {
                        Content = title,
                        CommandParameter = Tuple.Create((OmsOrderModel)FilledOrderGrid.SelectedItem, basket.Item2),
                        Command = dataContext.SendToNagBotCommand,
                    };
                    sendToNagbotButton.Items.Add(nagBotSubMenu);
                }

                return sendToNagbotButton;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetSendToNagBotButtonAsync));
                return null;
            }
        }

        private BarButtonItem GetChartButton(GridColumn column)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            return new()
            {
                Content = "Chart Orders by [" + column.FieldName + " X " + nameof(OmsOrderModel.LastUpdateTime) + "]",
                CommandParameter = new { Field = column.FieldName, Orders = FilledOrderGrid.SelectedItems },
                Command = dataContext.ChartOrdersCommand,
            };
        }

        private BarButtonItem GetTemplateButton(GridColumn column)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            return new()
            {
                Content = "Build Spread Template",
                CommandParameter = new { Orders = FilledOrderGrid.SelectedItems },
                Command = dataContext.BuildSpreadTemplateFromSelectedCommand,
            };
        }

        private BarButtonItem GetSearchInTradesModuleButton(GridColumn column)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            string selectedCellValue = FilledOrderGrid.GetCellValue(FilledOrderGrid.View.FocusedRowHandle, column)?.ToString();
            object timeAndSalesCommandParameter;
            OmsOrderModel selectedItem = (OmsOrderModel)FilledOrderGrid.SelectedItem;
            switch (column.FieldName)
            {
                case nameof(OmsOrderModel.UnderlyingSymbol):
                    timeAndSalesCommandParameter = new { Filter = "", SearchTerm = selectedCellValue, MLeg = selectedItem.IsComplexOrder };
                    break;
                default:
                    bool containsTimeRange = selectedItem.SubType == OrderSubType.EdgeScanFeed;
                    if (containsTimeRange)
                    {
                        DateTime fillTime = selectedItem.LastUpdateTime.ToEastern();
                        Tuple<int, double, DateTime, bool> key = Tuple.Create(selectedItem.CumulativeQuantity, selectedItem.AveragePrice, fillTime, selectedItem.Side == ZeroPlus.Models.Data.Enums.Side.Buy);
                        List<Tuple<int, double, DateTime, bool>> keys = new()
                        {
                            key,
                            Tuple.Create(selectedItem.EdgeScanFeedBuyQty, selectedItem.EdgeScanFeedBuyPrice, selectedItem.EdgeScanFeedBuyTime, true),
                            Tuple.Create(selectedItem.EdgeScanFeedSellQty, selectedItem.EdgeScanFeedSellPrice, selectedItem.EdgeScanFeedSellTime, false),
                        };
                        DateTime minTime = fillTime - TimeSpan.FromMinutes(15);
                        DateTime maxTime = fillTime + TimeSpan.FromMinutes(15);
                        timeAndSalesCommandParameter = new { Filter = "", SearchTerm = selectedItem.Symbol + "," + InvertTos(selectedItem.Symbol), MLeg = selectedItem.IsComplexOrder, ContainsTimeRange = true, Keys = keys, MinTime = minTime, MaxTime = maxTime, Key = key };
                    }
                    else
                    {
                        timeAndSalesCommandParameter = new { Filter = "", SearchTerm = selectedItem.Symbol + "," + InvertTos(selectedItem.Symbol), MLeg = selectedItem.IsComplexOrder, ContainsTimeRange = false };
                    }
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

        private BarButtonItem GetFilterInOrderBookButton(GridColumn column)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            string selectedCellValue = FilledOrderGrid.GetCellValue(FilledOrderGrid.View.FocusedRowHandle, column)?.ToString();
            string filterString = $"([{column.FieldName}] == '{selectedCellValue}')";

            BarButtonItem filterInNewOrderBookButton = new()
            {
                Content = "Filter in new Order Book",
                CommandParameter = filterString,
                Command = dataContext.FilterInNewOrderBookCommand,
            };
            return filterInNewOrderBookButton;
        }

        private BarButtonItem GetCopyContentButton(GridColumn column)
        {
            string selectedCellValue = FilledOrderGrid.GetCellValue(FilledOrderGrid.View.FocusedRowHandle, column)?.ToString();

            BarButtonItem copyContentButton = new()
            {
                Content = "Copy Cell Content",
            };

            copyContentButton.ItemClick += (s, e) => Clipboard.SetText(selectedCellValue);
            return copyContentButton;
        }

        private BarButtonItem GetCopySymbolButton()
        {
            BarButtonItem copyContentButton = new()
            {
                Content = (FilledOrderGrid?.SelectedItems?.Count ?? 0) > 1 ? "Copy Symbols" : "Copy Symbol",
            };

            copyContentButton.ItemClick += CopySymbol;
            void CopySymbol(object sender, ItemClickEventArgs e)
            {
                copyContentButton.ItemClick -= CopySymbol;
                var symbols = "";
                foreach (var item in FilledOrderGrid.SelectedItems)
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

        private BarButtonItem GetDebugInformationButton()
        {
            BarButtonItem copyContentButton = new()
            {
                Content = "Copy Debug Info",
            };

            copyContentButton.ItemClick += CopySymbol;
            void CopySymbol(object sender, ItemClickEventArgs e)
            {
                copyContentButton.ItemClick -= CopySymbol;
                var sb = new StringBuilder();
                sb.AppendLine("PermID\tOrderID\tOrderSource\tSubtype\tSpreadId\tSymbol");
                foreach (var item in FilledOrderGrid.SelectedItems)
                {
                    if (item is OmsOrderModel { Symbol: not null } model)
                    {
                        sb.AppendLine($"{model.PermID}\t{model.OrderID}\t{model.OrderSource.ToString()}\t{model.SubType?.ToString().FromCamelCase()}\t{model.SpreadId}\t{model.Symbol}");
                    }
                }

                var path = Path.Join(Path.GetTempPath(), Path.GetRandomFileName()[..^4] + ".tsv");
                File.WriteAllText(path, sb.ToString());
                Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
            }

            return copyContentButton;
        }

        private BarButtonItem GetFilterPermsInOrderBookButton(GridColumn column)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            OmsOrderModel selectedCellValue = (OmsOrderModel)FilledOrderGrid.GetRow(FilledOrderGrid.View.FocusedRowHandle);
            BarButtonItem filterInNewOrderBookButton = new()
            {
                Content = "Filter Auto Perms in Order Book",
                Command = dataContext.FilterInNewOrderBookCommand,
            };

            if (selectedCellValue != null)
            {
                filterInNewOrderBookButton.CommandParameter = $"([SpreadHash] == '{selectedCellValue.SpreadId.Md5Hash()}')";
            }
            return filterInNewOrderBookButton;
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

        private static BarButtonItem GetEditColumnButton(GridColumn column)
        {
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
            return editColumnButton;
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

        private BarButtonItem GetAddColumnButton(TableView table)
        {
            BarButtonItem editColumnButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Outlook Inspired/AddColumn.svg")),
                Content = "Add Column",
            };

            editColumnButton.ItemClick += (o, i) =>
            {
                OrderBookViewModel viewModel = DataContext as OrderBookViewModel;
                viewModel.AddColumn();
            };
            return editColumnButton;
        }

        private static BarButtonItem GetRemoveColumnButton(GridColumn column)
        {
            BarButtonItem removeColumnButton = new()
            {
                Content = "Hide This Column",
            };

            removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { column.Visible = false; };
            return removeColumnButton;
        }

        public static string InvertTos(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return symbol;
            }
            else
            {
                string reversed = symbol.Replace("-", "_").Replace("+", "-").Replace("_", "+");
                if (reversed.StartsWith("+"))
                {
                    reversed = reversed.Substring(1);
                }
                else
                {
                    reversed = "-" + reversed;
                }
                return reversed;
            }
        }

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                TableView tableView = FilledOrdersTableView;
                if (tableView != null)
                {
                    ISaveFileDialogService saveFileDialogService = ((OrderBookViewModel)DataContext).SaveFileDialogService;
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

        private void ExportToDomFormat(object sender, ItemClickEventArgs e)
        {
            try
            {
                List<OmsOrderModel> list = new();
                for (int i = 0; i < FilledOrderGrid.VisibleRowCount; i++)
                {
                    int rowHandle = FilledOrderGrid.GetRowHandleByVisibleIndex(i);
                    OmsOrderModel orderModel = (OmsOrderModel)FilledOrderGrid.GetRow(rowHandle);
                    list.Add(orderModel);
                }

                list = list.DistinctBy(x => x.SpreadId).ToList();
                var viewModel = (OrderBookViewModel)DataContext;
                ISaveFileDialogService saveFileDialogService = viewModel.SaveFileDialogService;
                saveFileDialogService.DefaultExt = "xlsx";
                saveFileDialogService.DefaultFileName = $"DOMINATOR SPREADS - {DateTime.Now:MM-dd-yyyy hh.mm} - {list.Count} spreads";
                saveFileDialogService.Filter = "Dominator List|*.XLSX";
                bool save = saveFileDialogService.ShowDialog();
                if (save)
                {
                    string username = viewModel.OmsCore.User.Username;
                    string filePath = saveFileDialogService.GetFullFileName();
                    Task.Run(() => ExportHelper.WriteSpreadsToFileUsingDominatorFormat(username, filePath, list));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportToDomFormat));
            }
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            List<OmsOrder> orders = new();
            var symbol = "";
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

            if (FilledOrderGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                FilledOrderGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                FilledOrderGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in FilledOrderGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
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

        private void ClearFiltersClick(object sender, System.Windows.RoutedEventArgs e)
        {
            OpenOrderGrid.FilterCriteria = null;
            OpenOrderGrid.FilterString = "";
            FilledOrderGrid.FilterCriteria = null;
            FilledOrderGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, System.Windows.RoutedEventArgs e)
        {
            OpenOrderGrid.ClearSorting();
            FilledOrderGrid.ClearSorting();
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

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if (sender is GridControl gridControl)
                {
                    if (gridControl == OpenOrderGrid)
                    {
                        OpenOrderGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
                    }
                    if (gridControl == FilledOrderGrid)
                    {
                        FilledOrderGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
                    }
                }
            }
        }

        private void ShowWorkingOrders(object sender, RoutedEventArgs e)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            if (dataContext.OrderBookViewModelConfig != null && !string.IsNullOrWhiteSpace(dataContext.OrderBookViewModelConfig.SplitterHeight))
            {
                GridLengthConverter glc = new();
                GridSplitterRow.Height = (GridLength)glc.ConvertFromString(dataContext.OrderBookViewModelConfig.SplitterHeight);
            }
        }

        private void HideWorkingOrders(object sender, RoutedEventArgs e)
        {
            HideWorkingOrders();
        }

        public void HideWorkingOrders()
        {
            GridLengthConverter glc = new();
            GridSplitterRow.Height = (GridLength)glc.ConvertFromString("0");
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is SpinEdit spinEdit)
            {
                spinEdit.SelectAll();
            }
        }

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            Dictionary<string, ColumnConfigModel> configMap = grid == OpenOrderGrid ? GridFieldNameToConfigMap : FilledOrdersGridFieldNameToConfigMap;
            viewModel.Customize(grid, configMap);

            tableCustomizationView.ShowDialog();
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            OrderBookViewModel viewModel = (OrderBookViewModel)DataContext;
            viewModel.SetDispatcher(Dispatcher);
            OmsCore omsCore = viewModel.OmsCore;
            omsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Unloaded += OnUnloaded;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = omsCore.User.Username,
                Group = omsCore.User.Username,
                OwnerId = omsCore.User.ID,
            };
            RestoreLayout(Uid);
            LoadQuickAccessConfigs();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            OrderBookViewModel viewModel = (OrderBookViewModel)DataContext;
            if (viewModel != null)
            {
                OmsCore omsCore = viewModel.OmsCore;
                omsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderBookViewModel.FilterType) && sender is OrderBookViewModel vm)
            {
                SyncFilterButtons(vm.FilterType);
            }
        }

        private void SyncFilterButtons(FilterType filterType)
        {
            ALL.IsChecked = filterType == FilterType.ALL;
            UNIQUE_ORDERS.IsChecked = filterType == FilterType.UNIQUE_ORDERS;
            FILLED.IsChecked = filterType == FilterType.FILLED;
            UNIQUE.IsChecked = filterType == FilterType.UNIQUE;
        }

        private void LoadQuickAccessConfigs()
        {
            if (DataContext is not OrderBookViewModel viewModel)
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
            if (DataContext is not OrderBookViewModel viewModel || viewModel.SelectedConfig == null)
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


        internal async void RestoreLayout(string uid)
        {
            if (DataContext is OrderBookViewModel dataContext)
            {
                dataContext.Uid = uid;
                UpdateFonts(OmsCore.Config);
                if (!_layoutRestored)
                {
                    _layoutRestored = true;
                    string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                    string instanceExportPath = Path.Combine(layoutDir, $"{uid}-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                    {
                        string export = File.ReadAllText(instanceExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        await RestoreFromConfigSave(configSave);
                    }
                    else
                    {
                        string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                        if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                        {
                            string export = File.ReadAllText(defaultExportPath);
                            ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                            await RestoreFromConfigSave(configSave);
                        }
                        else
                        {
                            await RestoreFromConfigSave(ConfigSave);
                        }
                    }

                    Ready?.Invoke();
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
                ConfigSave configSave = await (DataContext as OrderBookViewModel).OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
                await RestoreFromConfigSave(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal async Task RestoreFromConfigSave(ConfigSave configSave)
        {
            if (configSave != null)
            {
                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                await LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                Window window = StartupWindowViewModel.MainWindow.WindowHelper.FindParentWindow(this);
                if (window != null && window.GetType() != typeof(MainView))
                {
                    window.Title = configSave.Title + " - " + MODULE_NAME;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
            GridLengthConverter glc = new();
            dataContext.OrderBookViewModelConfig.SplitterHeight = glc.ConvertToString(GridSplitterRow.Height).Replace("*", "");

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(OpenOrderGrid)] = LayoutHelper.GetLayoutAsString(OpenOrderGrid),
                [nameof(FilledOrderGrid)] = LayoutHelper.GetLayoutAsString(FilledOrderGrid),
                [nameof(dataContext.OrderBookViewModelConfig)] = dataContext.GetConfig(),
                [nameof(WindowSetting)] = new WindowSetting(StartupWindowViewModel.MainWindow.WindowHelper.FindParentWindow(this), isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
                [nameof(FilledOrdersGridFieldNameToConfigMap)] = JsonConvert.SerializeObject(FilledOrdersGridFieldNameToConfigMap),
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

                OrderBookViewModel dataContext = (OrderBookViewModel)DataContext;
                await dataContext.LoadConfigFromJsonAsync(configDictionary[nameof(dataContext.OrderBookViewModelConfig)]);

                if (configDictionary.TryGetValue(nameof(OpenOrderGrid), out var openOrdersLayout))
                {
                    openOrdersLayout = LayoutHelper.RemoveHiddenAndDuplicateColumns(openOrdersLayout, LayoutHelper.OrderbookDeadColumns);
                    LayoutHelper.RestoreLayoutFromString(openOrdersLayout, OpenOrderGrid);
                }
                if (configDictionary.TryGetValue(nameof(FilledOrderGrid), out var closedOrdersLayout))
                {
                    closedOrdersLayout = LayoutHelper.RemoveHiddenAndDuplicateColumns(closedOrdersLayout, LayoutHelper.OrderbookDeadColumns);
                    LayoutHelper.RestoreLayoutFromString(closedOrdersLayout, FilledOrderGrid);
                }

                switch (dataContext.FilterType)
                {
                    case FilterType.ALL:
                        ALL.IsChecked = true;
                        break;
                    case FilterType.FILLED:
                        FILLED.IsChecked = true;
                        break;
                    case FilterType.UNIQUE:
                        UNIQUE.IsChecked = true;
                        break;
                    case FilterType.UNIQUE_ORDERS:
                        UNIQUE_ORDERS.IsChecked = true;
                        break;
                }

                if (dataContext.OrderBookViewModelConfig != null && !string.IsNullOrWhiteSpace(dataContext.OrderBookViewModelConfig.SplitterHeight))
                {
                    if (dataContext.OrderBookViewModelConfig.ShowWorkingOrdersGrid)
                    {
                        GridLengthConverter glc = new();
                        GridSplitterRow.Height = (GridLength)glc.ConvertFromString(dataContext.OrderBookViewModelConfig.SplitterHeight)!;
                    }
                    else
                    {
                        HideWorkingOrders();
                    }
                }
                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                    Window window = StartupWindowViewModel.MainWindow.WindowHelper.FindParentWindow(this);
                    window.Left = windowSettings.Left;
                    window.Top = windowSettings.Top;
                    if (windowSettings.Width > 0)
                    {
                        window.Width = windowSettings.Width;
                    }
                    if (windowSettings.Height > 0)
                    {
                        window.Height = windowSettings.Height;
                    }
                    window.WindowState = windowSettings.WindowState;
                }
                if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(OpenOrderGrid, GridFieldNameToConfigMap);
                }
                if (configDictionary.TryGetValue(nameof(FilledOrdersGridFieldNameToConfigMap), out fieldMapConfig))
                {
                    FilledOrdersGridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(FilledOrderGrid, FilledOrdersGridFieldNameToConfigMap);
                }

                OverrideHeader(FilledOrderGrid);
                OverrideHeader(OpenOrderGrid);

                SaveAutoScrollState();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        protected void ShowConfigSave(string title)
        {
            if (DataContext is not OrderBookViewModel viewModel)
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
                    var defaultConfig = configs.Where(x => x.Group == DefaultConfigGroupName && x.Title == title).FirstOrDefault();
                    if (defaultConfig == null)
                    {
                        viewModel.MessageBoxService.ShowMessage("Default configuration not found.");
                        return;
                    }
                    RestoreFromConfigSaveId(defaultConfig.Id);
                });
            });
        }

        private void OverrideHeader(GridControl grid)
        {
            try
            {
                foreach (GridColumn col in grid.Columns)
                {
                    if (!string.IsNullOrEmpty(col.FieldName) && _headerOverride.TryGetValue(col.FieldName, out string newHeader))
                    {
                        col.Header = newHeader;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OverrideHeader));
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                if (e.VerticalChange == 0.0)
                {
                    return;
                }

                OrderBookViewModel viewModel = DataContext as OrderBookViewModel;
                if (viewModel.UserScroll)
                {
                    double scrollerOffset = e.VerticalOffset + e.ViewportHeight;
                    viewModel.AutoScroll = _autoScroll && (Math.Abs(scrollerOffset - e.ExtentHeight) < SCROLL_EDGE_OFFSET || Math.Abs(e.VerticalOffset) < SCROLL_EDGE_OFFSET);
                }
                viewModel.UserScroll = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnScrollChanged));
            }
        }

        private void AutoScrollCheckedChanged(object sender, ItemClickEventArgs e)
        {
            if (sender is BarCheckItem barButton)
            {
                SaveAutoScrollState();
            }
        }

        private void AutoScrollChecked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.IsPressed)
                {
                    SaveAutoScrollState();
                }
            }
        }

        private void AutoScrollUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.IsPressed)
                {
                    SaveAutoScrollState();
                }
            }
        }

        private void SaveAutoScrollState()
        {
            OrderBookViewModel viewModel = DataContext as OrderBookViewModel;
            _autoScroll = viewModel.AutoScroll;
        }

        private void ShowColumnChooser(object sender, RoutedEventArgs e)
        {
            FilledOrdersTableView.ShowColumnChooser();
        }

        private void OnAutoGeneratingColumn(object sender, AutoGeneratingColumnEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Column?.FieldName) && LayoutHelper.OrderbookDeadColumns.Contains(e.Column.FieldName))
            {
                e.Cancel = true;
            }

            if (e.Column is { AllowUnboundExpressionEditor: true })
            {
                e.Cancel = true;
            }
        }
    }
}
