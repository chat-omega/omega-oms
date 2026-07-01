using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using GridLengthConverter = System.Windows.GridLengthConverter;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for DominatorsManagerView.xaml
    /// </summary>
    public partial class DominatorsManagerView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "Dominators Manager";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public DominatorsManagerView() : this(Guid.NewGuid().ToString())
        {
        }

        public DominatorsManagerView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(DominatorsManagerView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Closed += DominatorsManagerView_Closed;
            Loaded += RestoreLayout;
            Module = Module.DominatorsManagerLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new();
        }

        private void DominatorsManagerView_Closed(object sender, EventArgs e)
        {
            DominatorsManagerViewModel dataContext = (DominatorsManagerViewModel)DataContext;
            dataContext.Dispose();
        }

        private void Grid_ColumnsPopulated(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is GridControl grid)
                {
                    foreach (GridColumn col in grid.Columns.ToList())
                    {
                        try { DependencyPropertyDescriptor.FromProperty(BaseColumn.VisibleProperty, typeof(GridColumn)).AddValueChanged(col, ColumnVisibilityChanged); } catch { /* Ignore */ }
                        if (!string.IsNullOrWhiteSpace(col.FieldName))
                        {
                            col.Name = col.FieldName.Replace(".", "").Replace("-", "Remove").Replace(" ", "");
                        }
                        else if (col.Header is string header && !string.IsNullOrWhiteSpace(header))
                        {
                            col.Name = header.Replace(".", "").Replace("-", "Remove").Replace(" ", "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Grid_ColumnsPopulated));
            }
        }

        private void ColumnVisibilityChanged(object sender, EventArgs e)
        {
            if (sender is GridColumn column && column.Visible)
            {
                column.VisibleIndex = 1000;
            }
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                DominatorsGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;

                BarButtonItem removeColumnButton = new()
                {
                    Content = "Hide This Column",
                };

                removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { column.Visible = false; };

                BarButtonItem editColumnButton = new()
                {
                    Content = "Edit Column Header",
                };

                editColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) =>
                {
                    UpdateColumnHeaderView view = new();
                    UpdateColumnHeaderViewModel viewModel = view.DataContext as UpdateColumnHeaderViewModel;
                    viewModel.Title = column.Header != null ? column.Header.ToString() : column.FieldName;
                    viewModel.TitleUpdatedEvent += (string title) => { column.Header = title; };
                    view.Show();
                };

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);
            }
            else if (gridMenuEventArgs.MenuType == GridMenuType.RowCell)
            {
                TableView tableView = sender as TableView;
                GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;
                DominatorsManagerViewModel dataContext = (DominatorsManagerViewModel)DataContext;
                object selectedCellValue = DominatorsGrid.GetCellValue(DominatorsGrid.View.FocusedRowHandle, column);
                BarButtonItem checkAllWithSameValueButton = new()
                {
                    Content = "Check All With Same Value",
                    CommandParameter = new { Field = column.FieldName, Value = selectedCellValue },
                    Command = dataContext.CheckAllWithSameValueCommand,
                };

                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(checkAllWithSameValueButton);
            }
        }

        private BarButtonItem GetEditGridButton(TableView table)
        {
            BarButtonItem editColumnButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/Icon Builder/Actions_Settings.svg")),
                Content = "Edit Columns",
            };

            editColumnButton.ItemClick += (o, i) =>
            {
                LayoutSettings(table.Grid);
            };
            return editColumnButton;
        }

        private void RowActivated(object sender, RoutedEventArgs e)
        {
            foreach (object selectedRow in DominatorsGrid.SelectedItems)
            {
                if (selectedRow is DominatorModel dominatorModel)
                {
                    dominatorModel.Active = true;
                }
            }
        }

        private void RowDeactivated(object sender, RoutedEventArgs e)
        {
            foreach (object selectedRow in DominatorsGrid.SelectedItems)
            {
                if (selectedRow is DominatorModel dominatorModel)
                {
                    dominatorModel.Active = false;
                }
            }
        }

        private void GridColumn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            return;
        }

        private void GridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetQuickAccessWidth();
        }

        private void ExpandCollapseGrid_Click(object sender, RoutedEventArgs e)
        {
            SetQuickAccessWidth();
        }

        private void SetQuickAccessWidth()
        {
            GridLengthConverter glc = new();
            if (GridSplitterCol.Width.Value > 0)
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0");
                ExpandCollapseGridButton.Content = 3;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("125");
                ExpandCollapseGridButton.Content = 4;
            }
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void DominatorsGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            int rowHandle = DominatorsGrid.View.GetRowHandleByMouseEventArgs(e);
            ColumnBase column = DominatorsGrid.View.GetColumnByMouseEventArgs(e);
            if (rowHandle != DataControlBase.InvalidRowHandle &&
                !DominatorsGrid.IsGroupRowHandle(rowHandle))
            {
                if (column.Header != null && !string.IsNullOrEmpty(column.Header.ToString()) && column.Header.ToString() != "Active" && column.Header.ToString() != "Staged")
                {
                    DominatorModel dominator = (DominatorModel)DominatorsGrid.GetRow(rowHandle);
                    dominator.Active = !dominator.Active;
                }
            }
        }

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            DominatorsManagerViewModel dataContext = (DominatorsManagerViewModel)DataContext;
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
                if (DataContext is DominatorsManagerViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(DominatorsGrid)] = Helper.LayoutHelper.GetLayoutAsString(DominatorsGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async Task LoadConfigFromJsonAsync(string configJson)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }
                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));
                Helper.LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(DominatorsGrid)], DominatorsGrid);

                if (configDictionary.ContainsKey(nameof(WindowSetting)))
                {
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
                }

                if (configDictionary.ContainsKey(nameof(GridFieldNameToConfigMap)))
                {
                    if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                    {
                        GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                        TableCustomizationViewModel tableCustomizationViewModel = new();
                        tableCustomizationViewModel.Load(DominatorsGrid, GridFieldNameToConfigMap);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }
    }
}
