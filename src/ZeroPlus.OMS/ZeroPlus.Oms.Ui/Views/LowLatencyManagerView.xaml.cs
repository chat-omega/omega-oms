using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LowLatencyManagerView.xaml
    /// </summary>
    public partial class LowLatencyManagerView
    {
        private const Module MODULE = Module.LowLatencyManager;

        public LowLatencyManagerView(IModuleFactory moduleFactory, string uid = "") : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            LowLatencyManagerViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                LowLatencyManagerGridSettings = LayoutHelper.GetLayoutAsString(LowLatencyMainGrid)
            };
            if (DataContext is LowLatencyManagerViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            LowLatencyManagerViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<LowLatencyManagerViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.LowLatencyManagerGridSettings, LowLatencyMainGrid);
                    if (DataContext is LowLatencyManagerViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                LowLatencyMainGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        protected new void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                base.TableView_ShowGridMenu(sender, gridMenuEventArgs);
            }
            else
            {
                if (LowLatencyMainGrid.SelectedItem is LowLatencyModel instance)
                {
                    if (ViewModel is LowLatencyManagerViewModel viewModel)
                    {
                        List<LowLatencyOrderModel> hangs = viewModel.LowLatencyTransactionsProcessor.GetHangs(instance.LatencyInstance);
                        if (hangs != null && hangs.Any())
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                OpenHangsSubButton.Items.Clear();
                                foreach (var hang in hangs.OrderByDescending(x => x.LastUpdateTime))
                                {
                                    BarButtonItem instanceButton = new()
                                    {
                                        Content = hang.Symbol,
                                        CommandParameter = Tuple.Create(instance.LatencyInstance, hang),
                                        Command = viewModel.OpenHangCommand,
                                    };
                                    OpenHangsSubButton.Items.Add(instanceButton);
                                }
                            });
                        }
                    }
                }
            }
        }

        public override void ClearFiltersClick()
        {
            LowLatencyMainGrid.FilterCriteria = null;
            LowLatencyMainGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            LowLatencyMainGrid.ClearSorting();
        }
    }
}
