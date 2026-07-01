using DevExpress.Xpf.Editors;
using System;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ScriptTraderView.xaml
    /// </summary>
    public partial class ScriptTraderView : ModuleWindow
    {
        private const Models.Module MODULE = Models.Module.ScriptTrader;

        public ScriptTraderView(IModuleFactory moduleFactory, string uid = "") : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override void ClearFiltersClick()
        {
            PairOrdersGrid.FilterCriteria = null;
            PairOrdersGrid.FilterString = "";
        }

        public override void ClearSortingClick()
        {
            PairOrdersGrid.ClearSorting();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            ScriptTraderModuleConfig config = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                PairOrdersGridSettings = LayoutHelper.GetLayoutAsString(PairOrdersGrid)
            };
            if (DataContext is ScriptTraderViewModel viewModel)
            {
                config.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return config.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            ScriptTraderModuleConfig config = await ModuleConfigBase.DeserializeAsync<ScriptTraderModuleConfig>(configJson);
            if (config != null)
            {
                Dispatcher?.BeginInvoke(() =>
                {
                    LayoutHelper.RestoreLayoutFromString(config.PairOrdersGridSettings, PairOrdersGrid);
                    LoadWindowSettingsFromJson(config.WindowSetting, offset: offset);
                    if (DataContext is ScriptTraderViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(config.ViewModelConfig);
                    }
                });
            }
        }

        private void ReadonlyPopupOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is ComboBoxEdit comboBoxEdit)
            {
                comboBoxEdit.IsPopupOpen = !comboBoxEdit.IsReadOnly;
                e.Handled = comboBoxEdit.IsReadOnly;
            }
        }

        private void ConsoleTextEdit_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (AutoScrollCheckEdit.IsChecked.HasValue && AutoScrollCheckEdit.IsChecked.Value)
            {
                ConsoleTextEdit.ScrollToEnd();
            }
        }
    }
}
