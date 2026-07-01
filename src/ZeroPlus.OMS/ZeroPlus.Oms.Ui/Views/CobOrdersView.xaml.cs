using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Core;
using ZeroPlus.Cob.Client.Models;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for CobOrdersView.xaml
    /// </summary>
    public partial class CobOrdersView
    {
        private const Module MODULE = Module.CobOrders;

        public CobOrdersView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
            StrategySelector.SeparatorString = string.Empty;
            CallPutSelector.SeparatorString = string.Empty;
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            CobOrdersViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                OrdersGridSettings = LayoutHelper.GetLayoutAsString(OrdersGrid),
            };
            if (DataContext is CobOrdersViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            CobOrdersViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<CobOrdersViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.OrdersGridSettings, OrdersGrid);
                    if (DataContext is CobOrdersViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override List<IBarManagerControllerAction> GetHeaderBarButtons(GridColumn column)
        {
            List<IBarManagerControllerAction> items =
            [
                GetExportTableToExcelButton(OrdersTable)
            ];
            return items;
        }

        private void SelectedCallPutSummary(object sender, CustomDisplayTextEventArgs e)
        {
            e.Handled = true;
            ComboBoxEdit editor = (ComboBoxEdit)sender;
            if (editor.EditValue == null)
            {
                e.DisplayText = "Call Put: None";
                return;
            }
            List<object> value = (List<object>)editor.EditValue;
            if (e.EditValue.ToString() == value[0].ToString())
            {
                if (ViewModel is CobOrdersViewModel viewModel)
                {
                    if (viewModel.SelectedCallPut == null || viewModel.SelectedCallPut.Count == 0)
                    {
                        e.DisplayText = "Call Put: None";
                    }
                    else if (viewModel.SelectedCallPut.Count == viewModel.CallPut.Count)
                    {
                        e.DisplayText = "Call Put: All";
                    }
                    else
                    {
                        e.DisplayText = "Call Put: " + viewModel.SelectedCallPut.FirstOrDefault();
                    }
                }
            }
            else
            {
                e.DisplayText = string.Empty;
            }
        }

        private void SelectedStrategiesSummary(object sender, CustomDisplayTextEventArgs e)
        {
            e.Handled = true;
            ComboBoxEdit editor = (ComboBoxEdit)sender;
            if (editor.EditValue == null)
            {
                e.DisplayText = "Strategies: None";
                return;
            }
            List<object> value = (List<object>)editor.EditValue;
            if (e.EditValue.ToString() == value[0].ToString())
            {
                if (ViewModel is CobOrdersViewModel viewModel)
                {
                    if (viewModel.SelectedStrategies == null || viewModel.SelectedStrategies.Count == 0)
                    {
                        e.DisplayText = "Strategies: None";
                    }
                    else if (viewModel.SelectedStrategies.Count == viewModel.Strategies.Count)
                    {
                        e.DisplayText = "Strategies: All";
                    }
                    else
                    {
                        e.DisplayText = "Strategies: " + viewModel.SelectedStrategies.Count + " Selected";
                    }
                }
            }
            else
            {
                e.DisplayText = string.Empty;
            }
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            var item = e.Records.FirstOrDefault() as OpenSpreadExchOrderModel;
            string symbol = item?.Symbol;
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
            }
        }

        public override void ClearFiltersClick()
        {
            OrdersGrid.FilterCriteria = null;
            OrdersGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            OrdersGrid.ClearSorting();
        }
    }
}
