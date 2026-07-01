using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LockTraderView.xaml
    /// </summary>
    public partial class LockTraderView : IModuleView, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private const Module MODULE = Module.LockTrader;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public bool BasketExpanded { get; private set; }

        public LockTraderView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
            Reset();
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            List<OmsOrder> orders = new();
            foreach (BasketTraderItemModel basketItem in e.Records)
            {
                orders.Add(basketItem.ToOrder());
            }
            e.Data.SetData(DataFormats.Serializable, orders);
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
                LockTraderViewModel dataContext = (LockTraderViewModel)DataContext;
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    List<OmsOrder> orders = (List<OmsOrder>)e.Data.GetData(DataFormats.Serializable);
                    List<BasketTraderItemModel> newRecords = new();
                    var loadTasks = new List<Task>(orders.Count);
                    foreach (OmsOrder order in orders)
                    {
                        BasketTraderItemModel basketItem = new(dataContext, Dispatcher, dataContext.OmsCore);
                        var task = basketItem.LoadFromOrder(order);
                        loadTasks.Add(task);
                        newRecords.Add(basketItem);
                    }
                    await Task.WhenAll(loadTasks);
                    _ = ((LockTraderViewModel)DataContext).AddMultipleToBasketAsync(newRecords);
                    e.Data = null;
                }
                else if (e.Data.GetDataPresent(DataFormats.CommaSeparatedValue))
                {
                    string dragItem = e.Data.GetData(DataFormats.CommaSeparatedValue, false)?.ToString();
                    if (dragItem != null)
                    {
                        string[] items = dragItem.Split('\n');
                        HashSet<string> uniqueSpreads = new();
                        for (int i = 0; i < items.Length; i++)
                        {
                            string spreadId = items[i];
                            if (!string.IsNullOrWhiteSpace(spreadId))
                            {
                                spreadId = spreadId.Trim();
                                uniqueSpreads.Add(spreadId);
                            }
                        }
                        _ = ((LockTraderViewModel)DataContext).LoadFromSpreadIdsAsync(uniqueSpreads.Select(x => Tuple.Create(x, double.NaN)).ToList());
                    }
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
                AllowEditing = colTemplate.AllowEditing ? DefaultBoolean.True : DefaultBoolean.False,
            };

            if (LockTraderGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                LockTraderGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                LockTraderGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in LockTraderGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
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
            for (int i = 0; i < LockTraderGrid.VisibleRowCount; i++)
            {
                int rowHandle = LockTraderGrid.GetRowHandleByVisibleIndex(i);
                list.Add(Tuple.Create(i + 1, LockTraderGrid.GetRow(rowHandle)));
            }
            if (startFromSelectedRow && LockTraderGrid.SelectedItem != null)
            {
                Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == LockTraderGrid.SelectedItem);
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
            for (int i = 0; i < LockTraderGrid.VisibleRowCount; i++)
            {
                int rowHandle = LockTraderGrid.GetRowHandleByVisibleIndex(i);
                object check = LockTraderGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
        }

        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            LockTraderGrid.FilterCriteria = null;
            LockTraderGrid.FilterString = "";
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            LockTraderGrid.ClearSorting();
        }

        private void LockTraderGrid_ExpandCollapse(object sender, RoutedEventArgs e)
        {
            if (BasketExpanded)
            {
                LockTraderGrid.CollapseAllGroups();
                for (int i = 0; i < LockTraderGrid.VisibleRowCount; i++)
                {
                    int rowHandle = LockTraderGrid.GetRowHandleByVisibleIndex(i);
                    LockTraderGrid.CollapseMasterRow(rowHandle);
                }
                BasketExpanded = false;
                ExpandCollapseButton.Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Spreadsheet/FillDown.svg"));
            }
            else
            {
                LockTraderGrid.ExpandAllGroups();
                for (int i = 0; i < LockTraderGrid.VisibleRowCount; i++)
                {
                    if (i > 20)
                    {
                        break;
                    }
                    int rowHandle = LockTraderGrid.GetRowHandleByVisibleIndex(i);
                    LockTraderGrid.ExpandMasterRow(rowHandle);
                }
                BasketExpanded = true;
                ExpandCollapseButton.Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Spreadsheet/FillUp.svg"));
            }
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                LockTraderGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        protected override void OnModuleLoaded()
        {
            base.OnModuleLoaded();
            LockTraderViewModel dataContext = (LockTraderViewModel)DataContext;
            dataContext.Uid = Uid;
            dataContext.Name = "Lock " + dataContext.BasketSettings.Uid;
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            LockTraderViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                LockTraderGridSettings = Helper.LayoutHelper.GetLayoutAsString(LockTraderGrid)
            };
            if (DataContext is LockTraderViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            LockTraderViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<LockTraderViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(async () =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    Helper.LayoutHelper.RestoreLayoutFromString(viewConfig.LockTraderGridSettings, LockTraderGrid);
                    if (DataContext is LockTraderViewModel viewModel)
                    {
                        await viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            LockTraderGrid.FilterCriteria = null;
            LockTraderGrid.FilterString = string.Empty;
            DetailsGrid.FilterCriteria = null;
            DetailsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            LockTraderGrid.ClearSorting();
            DetailsGrid.ClearSorting();
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
    }
}
