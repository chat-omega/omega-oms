using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedView.xaml
    /// </summary>
    public partial class EdgeScanFeedView : IModuleView, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private const Module MODULE = Module.EdgeScanFeed;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public EdgeScanFeedView(IModuleFactory moduleFactory, string uid = "", bool loadDefault = true) : base(MODULE, uid, moduleFactory, loadDefault)
        {
            InitializeComponent();
        }

        protected override void RestoreLayout(bool loadDefault)
        {
            base.RestoreLayout(loadDefault);
            if (loadDefault)
            {
                Basket.LoadDefault();
            }
        }

        public override List<IBarManagerControllerAction> GetRowBarButtons(GridColumn column)
        {
            EdgeScanFeedViewModel viewModel = (EdgeScanFeedViewModel)DataContext;
            List<IBarManagerControllerAction> items = new List<IBarManagerControllerAction>();

            items.Add(new BarItemSeparator());

            EdgeScanFeedModel item = (EdgeScanFeedModel)FeedGrid.SelectedItem;

            var searchTerm = item.BuySymbol;
            if (!string.IsNullOrWhiteSpace(item.ExtraTag))
            {
                searchTerm += "," + item.ExtraTag;
            }
            if (!item.Mleg)
            {
                searchTerm = searchTerm.Replace("+", "").Replace("-", "");
            }
            BarButtonItem filterInTradesButton = new()
            {
                Content = "Time and Sales",
                CommandParameter = searchTerm,
                Command = viewModel.FilterInNewTradesModuleCommand,
            };
            items.Add(filterInTradesButton);

            var selectedCellValue = item.SpreadId;
            var filterString = $"([SpreadId] == '{selectedCellValue}')";
            BarButtonItem filterInNewOrderBookButton = new()
            {
                Content = "Filter in new Order Book",
                CommandParameter = filterString,
                Command = viewModel.FilterInNewOrderBookCommand,
            };
            items.Add(filterInNewOrderBookButton);

            BarButtonItem chartBidAskIvButton = new()
            {
                Content = "Chart Symbol Bid/Ask IV",
                CommandParameter = item,
                Command = viewModel.ChartSymbolBidAskIvCommand,
            };
            items.Add(chartBidAskIvButton);

            return items;
        }

        public override List<IBarManagerControllerAction> GetHeaderBarButtons(GridColumn column)
        {

            List<IBarManagerControllerAction> items = new List<IBarManagerControllerAction>();

            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Excel",
            };

            exportToExcelButton.ItemClick += ExportToExcel;

            items.Add(exportToExcelButton);
            return items;
        }

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                TableView tableView = TradesTableView;
                if (tableView != null)
                {
                    EdgeScanFeedViewModel viewModel = (EdgeScanFeedViewModel)DataContext;
                    ISaveFileDialogService saveFileDialogService = viewModel.SaveFileDialogService;
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
            foreach (EdgeScanFeedModel trade in e.Records)
            {
                orders.Add(trade.ToOrder());
            }
            e.Data.SetData(DataFormats.Serializable, orders);
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }
        public override void ClearFiltersClick()
        {
            FeedGrid.FilterCriteria = null;
            FeedGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
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

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            EdgeScanFeedViewModel viewModel = (EdgeScanFeedViewModel)DataContext;
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(FeedGrid)] = LayoutHelper.GetLayoutAsString(FeedGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
                [nameof(EdgeScanFeedFilterConfig)] = viewModel.GetConfigSerialized(),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }

                configJson = configJson.Replace("EdgeScanFeedViewModelConfig", "EdgeScanFeedFilterConfig");

                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

                if (configDictionary.TryGetValue(nameof(FeedGrid), out string feedGridConfig))
                {
                    LayoutHelper.RestoreLayoutFromString(feedGridConfig, FeedGrid);
                }

                if (configDictionary.TryGetValue(nameof(EdgeScanFeedFilterConfig), out string viewModelConfig))
                {
                    EdgeScanFeedFilterConfig config = await Task.Run(() => JsonConvert.DeserializeObject<EdgeScanFeedFilterConfig>(viewModelConfig));
                    if (config != null)
                    {
                        Dispatcher.BeginInvoke(async () =>
                        {
                            if (DataContext is EdgeScanFeedViewModel viewModel)
                            {
                                await viewModel.LoadConfig(config);
                            }
                        });
                    }
                }

                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    LoadWindowSettingsFromJson(windowSettingExport, offset);
                }

                if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(FeedGrid, GridFieldNameToConfigMap);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
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
                    AllowEditing = column.AllowEditing == DefaultBoolean.True,
                    AllowEquationEvaluator = column.AllowUnboundExpressionEditor,
                    Equation = column.UnboundExpression
                });
            }

            return columns;
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetEmbeddedBasketSize();
        }

        private void ExpandCollapseGrid_Click(object sender, RoutedEventArgs e)
        {
            SetEmbeddedBasketSize();
        }

        private void SetEmbeddedBasketSize()
        {
            GridLengthConverter glc = new();
            if (GridSplitterRow.Height.Value > 0)
            {
                Basket.Visibility = Visibility.Hidden;
                Height = Math.Max(400, Height - GridSplitterRow.Height.Value);
                GridSplitterRow.Height = (GridLength)glc.ConvertFromString("0")!;
                ExpandCollapseGridButton.Content = 5;
            }
            else
            {
                Basket.Visibility = Visibility.Visible;
                Height += 750;
                GridSplitterRow.Height = (GridLength)glc.ConvertFromString("750")!;
                ExpandCollapseGridButton.Content = 6;
            }
        }
    }
}
