using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using DevExpress.Xpf.PropertyGrid;
using DevExpress.Data.PLinq;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for NewDominatorManager.xaml
    /// </summary>
    public partial class NewDominatorManager : ModuleWindow
    {
        public NewDominatorManager(IModuleFactory moduleFactory, string uid = "") : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        private const Module MODULE = Module.NewDominatorManager;

        #region ModuleWindow Overrides
        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            try
            {
                return new NewDominatorManagerModuleConfig
                {
                    WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                    ViewModelConfig = ViewModel.GetConfigSerialized()
                }.Serialize();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetConfigAsJson));
                return string.Empty;
            }
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            NewDominatorManagerModuleConfig config = await ModuleConfigBase.DeserializeAsync<NewDominatorManagerModuleConfig>(configJson);
            ViewModel.LoadConfigFromJsonAsync(config.ViewModelConfig);
        }

        public override void ClearFiltersClick()
        {
            DominatorsGrid.FilterCriteria = null;
            DominatorsGrid.FilterString = string.Empty;
        }
        public override void ClearSortingClick()
        {
            DominatorsGrid.ClearSorting();
        }

        private new NewDominatorManagerViewModel ViewModel => DataContext as NewDominatorManagerViewModel;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        #endregion

        private void pg_CellValueChanged(object sender, CellValueChangedEventArgs args)
        {
            foreach (int rowHandle in DominatorsGrid.GetSelectedRowHandles())
            {
                DominatorsGrid.RefreshRow(rowHandle);
            }
        }
        private void pg_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is PropertyGridControl p
                && DominatorsGrid.SelectedItem is DominatorTraderModel traderModel)
            {
                DominatorConfig config = traderModel.DominatorConfig;
                p.SelectedObject = config;
            }
        }
        private void pg_BulkLoad(object sender, RoutedEventArgs e)
        {
            if (sender is PropertyGridControl p)
            {
                var vm = DataContext as NewDominatorManagerViewModel;
                p.SelectedObjects = vm.DominatorTraderModels.Where(d => d.Selected);
            }
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (DominatorsGrid.SelectedItem is DominatorTraderModel traderModel)
                if (sender is DevExpress.Xpf.Grid.GridControl gc)
                {
                    gc.ItemsSource = new PLinqServerModeSource { Source = traderModel.DominatorItems };
                }
        }
        private void XamlInitializer_Initialize(object sender, InstanceInitializeEventArgs e)
        {
            e.Instance.ExpirationGap = 0;
            e.Instance.Multiplier = 1.0;
        }
    }
}
