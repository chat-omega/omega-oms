using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for WinningTradesMonitorView.xaml
    /// </summary>
    public partial class WinningTradesMonitorView
    {
        private const Module MODULE = Module.WinningTradesMonitor;

        public WinningTradesMonitorView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            WinningTradesMonitorViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is WinningTradesMonitorViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            WinningTradesMonitorViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<WinningTradesMonitorViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is WinningTradesMonitorViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            TradesGridControl.FilterCriteria = null;
            TradesGridControl.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            TradesGridControl.ClearSorting();
        }

    }
}
