using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for UserPositionView.xaml
    /// </summary>
    public partial class UserPositionView
    {
        private const Module MODULE = Module.UserPosition;

        public UserPositionView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            WindowSetting windowSetting = new(this);
            string windowSettings = windowSetting.SerializeToJson();
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(PositionGrid)] = LayoutHelper.GetLayoutAsString(PositionGrid),
                [nameof(SymbolsGrid)] = LayoutHelper.GetLayoutAsString(SymbolsGrid),
                [nameof(WindowSetting)] = windowSettings,
                [nameof(Visibility)] = IsVisible.ToString(),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return;
            }

            Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

            if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
            {
                LoadWindowSettingsFromJson(windowSettingExport, offset);
            }
            if (configDictionary.TryGetValue(nameof(PositionGrid), out string positionGridLayout))
            {
                LayoutHelper.RestoreLayoutFromString(positionGridLayout, PositionGrid);
            }
            if (configDictionary.TryGetValue(nameof(SymbolsGrid), out string symbolGridLayout))
            {
                LayoutHelper.RestoreLayoutFromString(symbolGridLayout, SymbolsGrid);
            }
        }

        public override List<IBarManagerControllerAction> GetRowBarButtons(GridColumn column)
        {
            List<IBarManagerControllerAction> list = null;
            DominatorsManagerModel dominatorsManager = (DataContext as UserPositionViewModel)?.DominatorsManagerModel;
            if (dominatorsManager != null && dominatorsManager.Dominators.Any())
            {
                BarSubItem sendToDomButton = GetSendToDominatorButton();

                list = new List<IBarManagerControllerAction>
                {
                    new BarItemSeparator(),
                    sendToDomButton
                };
            }

            return list;
        }

        public override void ClearFiltersClick()
        {
            PositionGrid.FilterCriteria = null;
            PositionGrid.FilterString = string.Empty;
            SymbolsGrid.FilterCriteria = null;
            SymbolsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            PositionGrid.ClearSorting();
            SymbolsGrid.ClearSorting();
        }

        private BarSubItem GetSendToDominatorButton()
        {
            UserPositionViewModel dataContext = (UserPositionViewModel)DataContext;
            BarSubItem sendToDomButton = new()
            {
                Content = "Send To Dominator"
            };

            DominatorsManagerModel dominatorsManager = ((UserPositionViewModel)DataContext)?.DominatorsManagerModel;
            if (dominatorsManager != null)
            {
                foreach (IGrouping<string, DominatorModel> dominator in dominatorsManager.Dominators.GroupBy(
                             x => x.Host))
                {
                    BarSubItem domSubMenu = new()
                    {
                        Content = dominator.Key
                    };
                    foreach (DominatorModel instance in dominator.ToList())
                    {
                        BarButtonItem instanceButton = new()
                        {
                            Content = instance.Instance,
                            CommandParameter = Tuple.Create(PositionGrid.SelectedItem, instance),
                            Command = dataContext.SendToDominatorCommand,
                        };
                        domSubMenu.Items.Add(instanceButton);
                    }

                    sendToDomButton.Items.Add(domSubMenu);
                }
            }

            return sendToDomButton;
        }

        private void OnPositionsZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                GridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void OnSymbolsZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                SymbolsGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }
    }
}
