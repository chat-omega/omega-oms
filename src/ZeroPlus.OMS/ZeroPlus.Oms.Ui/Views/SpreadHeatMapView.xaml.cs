using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Charts.Heatmap;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SpreadHeatMapView.xaml
    /// </summary>
    public partial class SpreadHeatmapView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "Heatmap";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public SpreadHeatmapView() : this(Guid.NewGuid().ToString())
        {
        }

        public SpreadHeatmapView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(SpreadHeatmapView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Loaded += RestoreLayout;
            Closed += SpreadHeatMapView_Closed;
            Module = Module.HeatmapLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
        }

        private void SpreadHeatMapView_Closed(object sender, EventArgs e)
        {
            SpreadHeatmapViewModel viewModel = (SpreadHeatmapViewModel)DataContext;
            viewModel.Dispose();
        }

        private void SelectAll(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SpinEdit spinEdit = sender as SpinEdit;
            spinEdit?.SelectAll();
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs e)
        {
            GridColumn column = (GridColumn)e.MenuInfo.Column;
            SpreadHeatmapViewModel dataContext = (SpreadHeatmapViewModel)DataContext;
            if (e.MenuType == GridMenuType.Column)
            {
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.GroupColumn });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.GroupBox });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.ColumnChooser });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.SortAscending });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.SortBySummary });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.SortDescending });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.ClearSorting });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.BestFit });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.BestFitColumns });
                e.Customizations.Add(new RemoveBarItemAndLinkAction() { ItemName = DefaultColumnMenuItemNames.SearchPanel });

                if (column.VisibleIndex > 0)
                {
                    if (dataContext.GroupHeaderToGroupAlertMap.TryGetValue(column.Header.ToString(), out SpreadHeatmapAlert alert))
                    {
                        BarButtonItem editAlertsButton = new()
                        {
                            Content = "Edit Alerts",
                            CommandParameter = new { SpreadHeatmapAlert = alert },
                            Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Bell.svg")),
                            Command = dataContext.EditAlertsCommand,
                        };
                        e.Customizations.Add(editAlertsButton);
                        return;
                    }
                }
            }
            else if (e.MenuType == GridMenuType.RowCell)
            {
                if (column.VisibleIndex > 0)
                {
                    SpreadHeatmapCell cell = (SpreadHeatmapCell)HeatmapGrid.GetCellValue(HeatmapGrid.View.FocusedRowHandle, column);

                    if (cell != null && cell.Initialized)
                    {
                        BarButtonItem editAlertsButton = new()
                        {
                            Content = "Edit Alerts",
                            CommandParameter = new { SpreadHeatmapAlert = cell.CellAlert },
                            Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Bell.svg")),
                            Command = dataContext.EditAlertsCommand,
                        };

                        BarButtonItem showinOptionChain = new()
                        {
                            Content = "Show in Option Chain",
                            CommandParameter = new { Underlying = column.Header, Expiration = cell.Expiration },
                            Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/RichEdit/FloatingObjectLayoutOptions.svg")),
                            Command = dataContext.ShowinOptionChainCommand,
                        };

                        e.Customizations.Add(editAlertsButton);
                        e.Customizations.Add(showinOptionChain);
                    }
                }
            }
        }

        private void CutSearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.Cut();
        }

        private void CopySearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.Copy();
        }

        private void PasteSearchBox(object sender, ItemClickEventArgs e)
        {
            SearchBox.EditValue = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        private void LayoutSettings(object sender, RoutedEventArgs e)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(HeatmapGrid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }


        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            SpreadHeatmapViewModel dataContext = (SpreadHeatmapViewModel)DataContext;
            dataContext.Uid = Uid;
            if (!_layoutRestored)
            {
                _layoutRestored = true;
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                    RestoreFromConfigSave(configSave);
                }
                else
                {
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        RestoreFromConfigSave(configSave);
                    }
                    else
                    {
                        RestoreFromConfigSave(ConfigSave);
                    }
                }
            }
        }

        private void ShareLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ShareWithView view = new();
                ShareWithViewModel viewModel = view.DataContext as ShareWithViewModel;

                viewModel.Module = Module;
                viewModel.Config = GetConfigAsJson();

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareLayout));
            }
        }

        private void SaveLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveView view = new();
                SaveViewModel viewModel = view.DataContext as SaveViewModel;

                viewModel.LoadGroups(Module);
                viewModel.Id = ConfigSave.Id;
                viewModel.Title = ConfigSave.Title;
                viewModel.SelectedGroup = ConfigSave.Group;
                viewModel.Config = GetConfigAsJson();

                view.ShowDialog();

                if (viewModel.Success)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    SaveLayout(viewModel.SetAsDefault);
                }

                if (viewModel.Success && viewModel.AddToFavorites)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    ConfigSave.ConfigJson = GetConfigAsJson();
                    OmsCore.Config.AddFavoriteModule(MODULE_NAME, ConfigSave);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLayout));
            }
        }

        private void LoadLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;

                windowView.Loaded += (s, a) => viewModel.SetModule(Module);
                viewModel.LoadConfig = (ConfigSave configSave) => RestoreFromConfigSaveId(configSave.Id);

                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadLayout));
            }
        }

        public void SaveLayout(bool saveDefault)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ConfigSave.ConfigJson = GetConfigAsJson();

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{Uid}-{Module}-layout.json");
                string export = JsonConvert.SerializeObject(ConfigSave);
                if (!string.IsNullOrWhiteSpace(instanceExportPath))
                {
                    File.WriteAllText(instanceExportPath, export);
                }
                if (saveDefault)
                {
                    ConfigSave.ConfigJson = GetConfigAsJson(isDefault: true);
                    export = JsonConvert.SerializeObject(ConfigSave);
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath))
                    {
                        File.WriteAllText(defaultExportPath, export);
                    }
                }
                SetTitleFromConfigSave(ConfigSave);
            }));
        }

        private async void RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                ConfigSave configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
                RestoreFromConfigSave(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal void RestoreFromConfigSave(ConfigSave configSave)
        {
            if (configSave != null)
            {
                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                _ = LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                if (DataContext is SpreadHeatmapViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            SpreadHeatmapViewModel dataContext = (SpreadHeatmapViewModel)DataContext;

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(HeatmapGrid)] = Helper.LayoutHelper.GetLayoutAsString(HeatmapGrid),
                [nameof(HeatmapConfig)] = dataContext.GetConfigJson(),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            SpreadHeatmapViewModel dataContext = (SpreadHeatmapViewModel)DataContext;
            try
            {
                _layoutRestored = true;

                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));
                dataContext.LoadConfigFromJson(configDictionary[nameof(HeatmapConfig)]);

                Helper.LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(HeatmapGrid)], HeatmapGrid);
                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                    Left = windowSettings.Left;
                    Top = windowSettings.Top;
                    if (windowSettings.Width > 0)
                    {
                        Width = windowSettings.Width;
                    }
                    if (windowSettings.Height > 0)
                    {
                        Height = windowSettings.Height;
                    }
                    WindowState = windowSettings.WindowState;
                }
                if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(HeatmapGrid, GridFieldNameToConfigMap);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
                dataContext?.InvokeReady();
            }
        }

        private void GridSplitter_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender == TopListSplitter)
            {
                SetTopWidth();
            }
            else
            {
                SetQuickAccessWidth();
            }
        }

        private void ExpandCollapseGrid_Click(object sender, RoutedEventArgs e)
        {
            if (sender == TopExpandCollapseGridButton)
            {
                SetTopWidth();
            }
            else
            {
                SetQuickAccessWidth();
            }
        }

        private void SetQuickAccessWidth()
        {
            System.Windows.GridLengthConverter glc = new();
            if (GridSplitterCol.Width.Value > 0)
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0");
                ExpandCollapseGridButton.Content = 4;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("115");
                ExpandCollapseGridButton.Content = 3;
            }
        }

        private void SetTopWidth()
        {
            System.Windows.GridLengthConverter glc = new();
            if (TopListSplitterColumn.Width.Value > 0)
            {
                TopListSplitterColumn.Width = (GridLength)glc.ConvertFromString("0");
                TopExpandCollapseGridButton.Content = 3;
            }
            else
            {
                TopListSplitterColumn.Width = (GridLength)glc.ConvertFromString("300");
                TopExpandCollapseGridButton.Content = 4;
            }
        }

        private void TableView_RowDoubleClick(object sender, RowDoubleClickEventArgs e)
        {
            try
            {
                if (sender == UnderlyingsGrid.View)
                {
                    int rowHandle = e.HitInfo.RowHandle;
                    if (rowHandle != DataControlBase.InvalidRowHandle && !UnderlyingsGrid.IsGroupRowHandle(rowHandle))
                    {
                        object selectedCell = UnderlyingsGrid.GetCellValue(rowHandle, SymbolColumn);
                        GridColumn column = HeatmapGrid.Columns.Where(x => (string)x.Header == (string)selectedCell).FirstOrDefault();
                        if (column != null)
                        {
                            HeatmapGrid.CurrentColumn = column;
                        }
                    }
                }
                if (sender == TopGrid.View)
                {
                    int rowHandle = e.HitInfo.RowHandle;
                    if (rowHandle != DataControlBase.InvalidRowHandle && !TopGrid.IsGroupRowHandle(rowHandle))
                    {
                        object selectedCell = TopGrid.GetCellValue(rowHandle, TopSymbolColumn);
                        string[] parts = selectedCell.ToString().Split(" ");
                        string columnText = parts[0];
                        string rowText = parts[1];
                        GridColumn column = HeatmapGrid.Columns.Where(x => (string)x.Header == columnText).FirstOrDefault();
                        ObservableCollection<SpreadHeatmapRowModel> rowSource = (ObservableCollection<SpreadHeatmapRowModel>)HeatmapGrid.ItemsSource;
                        SpreadHeatmapRowModel row = rowSource.Where(x => x.Expiration.StartsWith(rowText)).FirstOrDefault();
                        if (column != null)
                        {
                            HeatmapGrid.CurrentColumn = column;
                            TableView view = (TableView)HeatmapGrid.View;
                            rowHandle = HeatmapGrid.FindRow(row);
                            FrameworkElement cell = view.GetCellElementByRowHandleAndColumn(rowHandle, column);
                            HeatmapGrid.SelectedItem = cell;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TableView_RowDoubleClick));
            }
        }

        private void HeatmapControl_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                HeatmapHitInfo hitInfo = HeatmapControl.CalcHitInfo(e.GetPosition(HeatmapControl));
                if (hitInfo.InHeatmapCell)
                {
                    if (hitInfo.HeatmapCell.XArgument is double strike)
                    {
                        if (hitInfo.HeatmapCell.YArgument is string expString && DateTime.TryParse(expString, out DateTime expiration))
                        {
                            SpreadHeatmapViewModel viewModel = DataContext as SpreadHeatmapViewModel;
                            viewModel?.LoadTicket(expiration, strike);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HeatmapControl_MouseDoubleClick));
            }
        }
    }
}
