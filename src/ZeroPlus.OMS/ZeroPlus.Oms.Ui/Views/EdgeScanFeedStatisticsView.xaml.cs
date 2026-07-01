using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EdgeScanFeedStatisticsView.xaml
    /// </summary>
    public partial class EdgeScanFeedStatisticsView
    {
        private const Module MODULE = Module.EdgeScanFeedStatistics;

        public EdgeScanFeedStatisticsView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            EdgeScanFeedStatisticsViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is EdgeScanFeedStatisticsViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            EdgeScanFeedStatisticsViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<EdgeScanFeedStatisticsViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is EdgeScanFeedStatisticsViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            StatsGrid.FilterCriteria = null;
            StatsGrid.FilterString = string.Empty;
            DetailsGrid.FilterCriteria = null;
            DetailsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            StatsGrid.ClearSorting();
            DetailsGrid.ClearSorting();
        }

    }
}
