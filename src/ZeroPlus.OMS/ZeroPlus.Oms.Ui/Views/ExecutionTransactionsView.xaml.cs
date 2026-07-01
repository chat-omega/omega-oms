using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ExecutionTransactionsView.xaml
    /// </summary>
    public partial class ExecutionTransactionsView
    {
        private const Module MODULE = Module.ExecutionTransaction;

        public ExecutionTransactionsView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            BulletinBoardViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
                BulletinGridSettings = LayoutHelper.GetLayoutAsString(TransactionsGrid),
            };
            if (DataContext is ExecutionTransactionsViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            BulletinBoardViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<BulletinBoardViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    LayoutHelper.RestoreLayoutFromString(viewConfig.BulletinGridSettings, TransactionsGrid);
                    if (DataContext is ExecutionTransactionsViewModel viewModel)
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

            exportToExcelButton.ItemClick += ExportToExcel;

            items.Add(exportToExcelButton);
            return items;
        }

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                TableView tableView = TransactionsTable;
                if (tableView != null)
                {
                    ExecutionTransactionsViewModel viewModel = (ExecutionTransactionsViewModel)DataContext;
                    ISaveFileDialogService saveFileDialogService = viewModel.SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"Executions Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
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
            TransactionsGrid.FilterCriteria = null;
            TransactionsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            TransactionsGrid.ClearSorting();
        }

    }
}
