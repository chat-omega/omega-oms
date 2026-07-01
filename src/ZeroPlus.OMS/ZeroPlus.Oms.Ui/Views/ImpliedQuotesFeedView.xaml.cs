using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
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
    /// Interaction logic for ImpliedQuotesFeedView.xaml
    /// </summary>
    public partial class ImpliedQuotesFeedView
    {
        private const Module MODULE = Module.ImpliedQuoteFeed;

        public ImpliedQuotesFeedView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            ImpliedQuoteFeedConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                UpdatesGridSettings = LayoutHelper.GetLayoutAsString(UpdatesGrid),
            };
            if (DataContext is ImpliedQuotesFeedViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            ImpliedQuoteFeedConfig viewConfig = await ModuleConfigBase.DeserializeAsync<ImpliedQuoteFeedConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.UpdatesGridSettings, UpdatesGrid);
                    if (DataContext is ImpliedQuotesFeedViewModel viewModel)
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
                    ImpliedQuotesFeedViewModel viewModel = (ImpliedQuotesFeedViewModel)DataContext;
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

        public override void ClearFiltersClick()
        {
            UpdatesGrid.FilterCriteria = null;
            UpdatesGrid.FilterString = string.Empty;
        }
        public override void ClearSortingClick()
        {
            UpdatesGrid.ClearSorting();
        }
    }
}
