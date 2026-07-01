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
    /// Interaction logic for MarketMoversView.xaml
    /// </summary>
    public partial class MarketMoversView
    {
        private const Module MODULE = Module.MarketMovers;

        public MarketMoversView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            MarketMoversViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                MoversGridSettings = LayoutHelper.GetLayoutAsString(MoversGrid),
            };
            if (DataContext is MarketMoversViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            MarketMoversViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<MarketMoversViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.MoversGridSettings, MoversGrid);
                    if (DataContext is LowLatencyOrderBookViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            MoversGrid.FilterCriteria = null;
            MoversGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            MoversGrid.ClearSorting();
        }
    }
}
