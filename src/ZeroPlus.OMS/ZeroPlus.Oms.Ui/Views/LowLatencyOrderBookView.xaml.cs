using System;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LowLatencyOrderBookView.xaml
    /// </summary>
    public partial class LowLatencyOrderBookView
    {
        private const Module MODULE = Module.LowLatencyOrderBook;

        public LowLatencyOrderBookView(IModuleFactory moduleFactory, string uid = "") : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            LowLatencyOrderbookViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                WorkingOrdersGridSettings = LayoutHelper.GetLayoutAsString(WorkingOrdersGrid),
                OrdersGridSettings = LayoutHelper.GetLayoutAsString(OrdersGrid),
            };
            if (DataContext is LowLatencyOrderBookViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            LowLatencyOrderbookViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<LowLatencyOrderbookViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.WorkingOrdersGridSettings, WorkingOrdersGrid);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.OrdersGridSettings, OrdersGrid);
                    if (DataContext is LowLatencyOrderBookViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            WorkingOrdersGrid.FilterCriteria = null;
            WorkingOrdersGrid.FilterString = string.Empty;
            OrdersGrid.FilterCriteria = null;
            OrdersGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            WorkingOrdersGrid.ClearSorting();
            OrdersGrid.ClearSorting();
        }
    }
}
