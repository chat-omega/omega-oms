using DevExpress.Xpf.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for QuoteBoardView.xaml
    /// </summary>
    public partial class QuotesAndGreeksBoardView : ModuleWindow, IModuleView
    {
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public QuotesAndGreeksBoardView(IModuleFactory moduleFactory, string uid = null) : base(Module.QuotesAndGreeksBoard, uid, moduleFactory)
        {
            InitializeComponent();
            Module = Module.QuotesAndGreeksBoard;
            ConfigSave = new ConfigSave()
            {
                Title = Title,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            QuotesAndGreeksBoardViewConfig viewConfig = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson(),
            };
            if (DataContext is QuotesAndGreeksBoardViewModel viewModel)
            {
                viewConfig.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return viewConfig.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            QuotesAndGreeksBoardViewConfig viewConfig = await ModuleConfigBase.DeserializeAsync<QuotesAndGreeksBoardViewConfig>(configJson);
            if (viewConfig != null)
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(viewConfig.WindowSetting, offset: offset);
                    if (DataContext is QuotesAndGreeksBoardViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(viewConfig.ViewModelConfig);
                    }
                });
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

        private void TableView_DragRecordOver(object sender, DragRecordOverEventArgs e)
        {
            if (e.IsFromOutside)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private void TableView_DropRecord(object sender, DropRecordEventArgs e)
        {
            try
            {
                QuotesAndGreeksBoardViewModel viewModel = ViewModel as QuotesAndGreeksBoardViewModel;
                if (e.Data.GetDataPresent(DataFormats.Serializable))
                {
                    List<OmsOrder> orders = (List<OmsOrder>)e.Data.GetData(DataFormats.Serializable);
                    List<string> newRecords = new();

                    if (orders != null)
                    {
                        foreach (OmsOrder order in orders)
                        {
                            newRecords.Add(order.Symbol);
                        }
                    }

                    viewModel?.LoadSymbols(newRecords);
                    e.Data = null;
                }
                else if (e.Data.GetDataPresent(DataFormats.CommaSeparatedValue))
                {
                    string dragItem = e.Data.GetData(DataFormats.CommaSeparatedValue, false)?.ToString();
                    if (dragItem != null)
                    {
                        string[] items = dragItem.Split('\n');
                        List<string> uniqueSpreads = new();
                        foreach (var item in items)
                        {
                            string spreadId = item;
                            if (!string.IsNullOrWhiteSpace(spreadId))
                            {
                                spreadId = spreadId.Trim();
                                uniqueSpreads.Add(spreadId);
                            }
                        }
                        viewModel?.LoadSymbols(uniqueSpreads);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
