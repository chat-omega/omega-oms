using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EodRiskView.xaml
    /// </summary>
    public partial class EodRiskView
    {
        private const Module MODULE = Module.EodRisk;

        public EodRiskView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            WindowSetting windowSetting = new(this, false);
            string windowSettings = windowSetting.SerializeToJson();
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(RiskGrid)] = LayoutHelper.GetLayoutAsString(RiskGrid),
                [nameof(OpenNotionalGrid)] = LayoutHelper.GetLayoutAsString(OpenNotionalGrid),
                [nameof(WindowSetting)] = windowSettings,
                [nameof(Visibility)] = IsVisible.ToString(),
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

            if (configDictionary.TryGetValue(nameof(RiskGrid), out var riskGrid))
            {
                LayoutHelper.RestoreLayoutFromString(riskGrid, RiskGrid);
            }
            if (configDictionary.TryGetValue(nameof(OpenNotionalGrid), out var notionalGrid))
            {
                LayoutHelper.RestoreLayoutFromString(notionalGrid, OpenNotionalGrid);
            }
        }

        private void RiskGrid_CustomColumnDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString("hh:mm:ss.fff");
            }
        }

        public override List<IBarManagerControllerAction> GetRowBarButtons(GridColumn column)
        {
            var items = new List<IBarManagerControllerAction>();
            if (RiskGrid.SelectedItem is SpreadRiskModel spreadRiskModel)
            {
                EodRiskViewModel dataContext = (EodRiskViewModel)DataContext;
                string filterString = $"([SpreadId] == '{spreadRiskModel.SpreadDescription}')";

                BarButtonItem filterInNewOrderBookButton = new()
                {
                    Content = "Filter in new Order Book",
                    CommandParameter = filterString,
                    Command = dataContext.FilterInNewOrderBookCommand,
                };

                items.Add(filterInNewOrderBookButton);
            }

            return items;
        }

        public override void ClearFiltersClick()
        {
            RiskGrid.FilterCriteria = null;
            RiskGrid.FilterString = string.Empty;
            SelfTradeGrid.FilterCriteria = null;
            SelfTradeGrid.FilterString = string.Empty;
            OpenNotionalGrid.FilterCriteria = null;
            OpenNotionalGrid.FilterString = string.Empty;
            OpenPositionsGrid.FilterCriteria = null;
            OpenPositionsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            RiskGrid.ClearSorting();
            SelfTradeGrid.ClearSorting();
            OpenNotionalGrid.ClearSorting();
            OpenPositionsGrid.ClearSorting();
        }
    }
}
