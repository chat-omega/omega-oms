using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid.TreeList;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView
    {
        private const Module MODULE = Module.Dashboard;

        public DashboardView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
            TreeListDataController.DisableThreadingProblemsDetection = true;
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            WindowSetting windowSetting = new(this, false);
            string windowSettings = windowSetting.SerializeToJson();
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(FirmGrid)] = LayoutHelper.GetLayoutAsString(FirmGrid),
                [nameof(ApiGrid)] = LayoutHelper.GetLayoutAsString(ApiGrid),
                [nameof(TraderGrid)] = LayoutHelper.GetLayoutAsString(TraderGrid),
                [nameof(UnderlyingDetailsGrid)] = LayoutHelper.GetLayoutAsString(UnderlyingDetailsGrid),
                [nameof(SpreadTypeDetailsGrid)] = LayoutHelper.GetLayoutAsString(SpreadTypeDetailsGrid),
                [nameof(RouteDetailsGrid)] = LayoutHelper.GetLayoutAsString(RouteDetailsGrid),
                [nameof(ExchangeDetailsGrid)] = LayoutHelper.GetLayoutAsString(ExchangeDetailsGrid),
                [nameof(WindowSetting)] = windowSettings,
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool _ = true)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return;
            }

            Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

            if (configDictionary.ContainsKey(nameof(WindowSetting)))
            {
                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    LoadWindowSettingsFromJson(windowSettingExport, offset);
                }
            }

            if (configDictionary.TryGetValue(nameof(FirmGrid), out var firmGrid))
            {
                LayoutHelper.RestoreLayoutFromString(firmGrid, FirmGrid);
            }
            if (configDictionary.TryGetValue(nameof(ApiGrid), out var apiGrid))
            {
                LayoutHelper.RestoreLayoutFromString(apiGrid, ApiGrid);
            }
            if (configDictionary.TryGetValue(nameof(TraderGrid), out var tradesGrid))
            {
                LayoutHelper.RestoreLayoutFromString(tradesGrid, TraderGrid);
            }
            if (configDictionary.TryGetValue(nameof(UnderlyingDetailsGrid), out var ulDetailsGrid))
            {
                LayoutHelper.RestoreLayoutFromString(ulDetailsGrid, UnderlyingDetailsGrid);
            }
            if (configDictionary.TryGetValue(nameof(SpreadTypeDetailsGrid), out var spreadTypeGrid))
            {
                LayoutHelper.RestoreLayoutFromString(spreadTypeGrid, SpreadTypeDetailsGrid);
            }
            if (configDictionary.TryGetValue(nameof(RouteDetailsGrid), out var routeDetailsGrid))
            {
                LayoutHelper.RestoreLayoutFromString(routeDetailsGrid, RouteDetailsGrid);
            }
            if (configDictionary.TryGetValue(nameof(ExchangeDetailsGrid), out var exchDetailsGrid))
            {
                LayoutHelper.RestoreLayoutFromString(exchDetailsGrid, ExchangeDetailsGrid);
            }
        }

        public override void ClearFiltersClick()
        {
            FirmGrid.FilterCriteria = null;
            FirmGrid.FilterString = string.Empty;
            ApiGrid.FilterCriteria = null;
            ApiGrid.FilterString = string.Empty;
            TraderGrid.FilterCriteria = null;
            TraderGrid.FilterString = string.Empty;
            UnderlyingDetailsGrid.FilterCriteria = null;
            UnderlyingDetailsGrid.FilterString = string.Empty;
            SpreadTypeDetailsGrid.FilterCriteria = null;
            SpreadTypeDetailsGrid.FilterString = string.Empty;
            RouteDetailsGrid.FilterCriteria = null;
            RouteDetailsGrid.FilterString = string.Empty;
            ExchangeDetailsGrid.FilterCriteria = null;
            ExchangeDetailsGrid.FilterString = string.Empty;
            SymbolStatGrid.FilterCriteria = null;
            SymbolStatGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            FirmGrid.ClearSorting();
            ApiGrid.ClearSorting();
            TraderGrid.ClearSorting();
            UnderlyingDetailsGrid.ClearSorting();
            SpreadTypeDetailsGrid.ClearSorting();
            RouteDetailsGrid.ClearSorting();
            ExchangeDetailsGrid.ClearSorting();
            SymbolStatGrid.ClearSorting();
        }
    }
}
