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
    /// Interaction logic for BasketGroupView.xaml
    /// </summary>
    public partial class BasketGroupView
    {
        private const Module MODULE = Module.BasketGroup;

        public BasketGroupView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            BasketGroupViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is BasketGroupViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            BasketGroupViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<BasketGroupViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is BasketGroupViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        private void OnBasketGroupDragOver(object sender, DragEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.Serializable) &&
                e.Data.GetData(DataFormats.Serializable) is string basketId &&
                BasketGroupViewModel.BasketIdToBasketDragMap.TryGetValue(basketId, out var basketDragItem) &&
                DataContext is BasketGroupViewModel viewModel)
            {
                viewModel.AttachBasket(basketDragItem);
            }
        }

        private void BasketTraderTabLoaded(object sender, RoutedEventArgs e)
        {
            var lastConfig = BasketGroupViewModel.LastConfig;
            if (sender is BasketTraderContentView basketTraderView && lastConfig != null)
            {
                basketTraderView.LoadConfigFromJson(lastConfig, false);
                BasketGroupViewModel.LastConfig = null;
            }
        }

    }
}
