using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for GammaScalpView.xaml
    /// </summary>
    public partial class GammaScalpView
    {
        private const Module MODULE = Module.GammaScalp;

        public GammaScalpView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            GammaScalpViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is GammaScalpViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            GammaScalpViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<GammaScalpViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is GammaScalpViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private void OnDragRecordOver(object sender, DragRecordOverEventArgs e)
        {
            if (e.IsFromOutside)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private async void OnDropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                if (ViewModel is GammaScalpViewModel viewModel)
                {
                    if (e.Data.GetDataPresent(DataFormats.CommaSeparatedValue))
                    {
                        string dragItem = e.Data.GetData(DataFormats.CommaSeparatedValue, false)?.ToString();
                        if (dragItem != null)
                        {
                            string[] items = dragItem.Split('\n');
                            if (items.Length > 0)
                            {
                                var symbol = items[0];
                                SymbolLib.SymbolCodec codec = new(symbol);
                                string underlying = codec.UnderlyingSymbol();
                                if (viewModel.Scalper is not { Running: true })
                                {
                                    await viewModel.LoadUnderlying(underlying);
                                    await viewModel.OrderTicket.LoadLegsFromTosAsync(symbol, null, true, true);
                                }
                            }
                        }
                    }
                }

                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }

        public override List<IBarManagerControllerAction> GetHeaderBarButtons(GridColumn column)
        {
            List<IBarManagerControllerAction> items =
            [
                GetExportTableToExcelButton(OrdersTableView)
            ];
            return items;
        }

        public override void ClearFiltersClick()
        {
            PositionsGrid.FilterCriteria = null;
            PositionsGrid.FilterString = string.Empty;
            HedgeOrdersGrid.FilterCriteria = null;
            HedgeOrdersGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            PositionsGrid.ClearSorting();
            HedgeOrdersGrid.ClearSorting();
        }
    }
}