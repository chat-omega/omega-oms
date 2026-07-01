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
    /// Interaction logic for BulletinBoardView.xaml
    /// </summary>
    public partial class BulletinBoardView
    {
        private const Module MODULE = Module.BulletinBoard;

        public BulletinBoardView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            BulletinBoardViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                BulletinGridSettings = LayoutHelper.GetLayoutAsString(BulletinGrid),
            };
            if (DataContext is BulletinBoardViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            BulletinBoardViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<BulletinBoardViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.BulletinGridSettings, BulletinGrid);
                    if (DataContext is BulletinBoardViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override void ClearFiltersClick()
        {
            BulletinGrid.FilterCriteria = null;
            BulletinGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            BulletinGrid.ClearSorting();
        }
    }
}
