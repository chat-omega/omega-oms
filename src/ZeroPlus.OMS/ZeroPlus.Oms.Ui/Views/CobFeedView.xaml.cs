using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for CobFeedView.xaml
    /// </summary>
    public partial class CobFeedView
    {
        private const Module MODULE = Module.CobFeed;

        public CobFeedView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
            StrategySelector.SeparatorString = string.Empty;
            CallPutSelector.SeparatorString = string.Empty;
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            CobFeedViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                BulletinGridSettings = LayoutHelper.GetLayoutAsString(UpdatesGrid),
            };
            if (DataContext is CobFeedViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            CobFeedViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<CobFeedViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.BulletinGridSettings, UpdatesGrid);
                    if (DataContext is CobFeedViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
            }
        }

        public override List<IBarManagerControllerAction> GetHeaderBarButtons(GridColumn column)
        {
            List<IBarManagerControllerAction> items = new List<IBarManagerControllerAction>();

            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Excel",
            };

            exportToExcelButton.ItemClick += (s, e) => ExportToExcel(column.View as TableView);

            items.Add(exportToExcelButton);
            return items;
        }

        private void ExportToExcel(TableView tableView)
        {
            try
            {
                if (tableView != null)
                {
                    CobFeedViewModel viewModel = (CobFeedViewModel)DataContext;
                    ISaveFileDialogService saveFileDialogService = viewModel.SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"{MODULE.ToString().FromCamelCase()} {tableView.Name?.FromCamelCase()} - {DateTime.Now:MM-dd-yyyy hh.mm}";
                    saveFileDialogService.Filter = "xlsx|*.xlsx";
                    bool dialogResult = saveFileDialogService.ShowDialog();
                    if (dialogResult)
                    {
                        string filePath = saveFileDialogService.GetFullFileName();
                        tableView.ExportToXlsx(filePath);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void SelectedCallPutSummary(object sender, CustomDisplayTextEventArgs e)
        {
            e.Handled = true;
            ComboBoxEdit editor = (ComboBoxEdit)sender;
            if (editor.EditValue == null)
            {
                e.DisplayText = "Call Put: None";
                return;
            }
            List<object> value = (List<object>)editor.EditValue;
            if (e.EditValue.ToString() == value[0].ToString())
            {
                if (ViewModel is CobFeedViewModel viewModel)
                {
                    if (viewModel.SelectedCallPut == null || viewModel.SelectedCallPut.Count == 0)
                    {
                        e.DisplayText = "Call Put: None";
                    }
                    else if (viewModel.SelectedCallPut.Count == viewModel.CallPut.Count)
                    {
                        e.DisplayText = "Call Put: All";
                    }
                    else
                    {
                        e.DisplayText = "Call Put: " + viewModel.SelectedCallPut.FirstOrDefault();
                    }
                }
            }
            else
            {
                e.DisplayText = string.Empty;
            }
        }

        private void SelectedStrategiesSummary(object sender, CustomDisplayTextEventArgs e)
        {
            e.Handled = true;
            ComboBoxEdit editor = (ComboBoxEdit)sender;
            if (editor.EditValue == null)
            {
                e.DisplayText = "Strategies: None";
                return;
            }
            List<object> value = (List<object>)editor.EditValue;
            if (e.EditValue.ToString() == value[0].ToString())
            {
                if (ViewModel is CobFeedViewModel viewModel)
                {
                    if (viewModel.SelectedStrategies == null || viewModel.SelectedStrategies.Count == 0)
                    {
                        e.DisplayText = "Strategies: None";
                    }
                    else if (viewModel.SelectedStrategies.Count == viewModel.Strategies.Count)
                    {
                        e.DisplayText = "Strategies: All";
                    }
                    else
                    {
                        e.DisplayText = "Strategies: " + viewModel.SelectedStrategies.Count + " Selected";
                    }
                }
            }
            else
            {
                e.DisplayText = string.Empty;
            }
        }

        public override void ClearFiltersClick()
        {
            UpdatesGrid.FilterCriteria = null;
            UpdatesGrid.FilterString = string.Empty;
            OrdersGrid.FilterCriteria = null;
            OrdersGrid.FilterString = string.Empty;
            PrintsGrid.FilterCriteria = null;
            PrintsGrid.FilterString = string.Empty;
            AuctionPrintsGrid.FilterCriteria = null;
            AuctionPrintsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            UpdatesGrid.ClearSorting();
            OrdersGrid.ClearSorting();
            PrintsGrid.ClearSorting();
            AuctionPrintsGrid.ClearSorting();
        }
    }
}
