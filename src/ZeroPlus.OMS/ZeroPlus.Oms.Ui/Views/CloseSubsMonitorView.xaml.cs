using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for CloseSubsMonitorView.xaml
    /// </summary>
    public partial class CloseSubsMonitorView
    {
        private const Module MODULE = Module.CloseSubsMonitor;

        public CloseSubsMonitorView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            CloseSubsMonitorViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is CloseSubsMonitorViewModel viewModel)
            {
                viewConfig.ModelsGridControlConfig = LayoutHelper.GetLayoutAsString(ModelsGridControl);
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            CloseSubsMonitorViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<CloseSubsMonitorViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is CloseSubsMonitorViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                    LayoutHelper.RestoreLayoutFromString(viewConfig.ModelsGridControlConfig, ModelsGridControl);
                });
            }
        }

        public override void ClearFiltersClick()
        {
            ModelsGridControl.FilterCriteria = null;
            ModelsGridControl.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            ModelsGridControl.ClearSorting();
        }
    }
}
