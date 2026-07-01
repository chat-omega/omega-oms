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
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ComplexOrderTicketView.xaml
    /// </summary>
    public partial class CombinedOrderTicketView : ThemedWindow, IModuleView
    {
        private const string MODULE_NAME = "Combined Order Ticket";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly object _initLock = new();

        private bool _dragging;
        private Point _startpos;
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool Manual { get; set; } = true;
        public bool Clone { get; set; }
        public CombinedOrderTicketView() : this(Guid.NewGuid().ToString())
        {
        }

        public CombinedOrderTicketView(string uid)
        {
            Uid = uid;
            lock (_initLock)
            {
                InitializeComponent();
            }
            Name = nameof(CombinedOrderTicketView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Closing += Window_Closing;
            Closed += ComplexOrderTicketView_Closed;
            Loaded += RestoreLayout;
            CombinedTicketLegsGrid.PropertyChanged += ComplexLegsGrid_PropertyChanged;
            OmsCore.Config.ConfigChangedEvent += Config_ConfigChangedEvent;
            Closed += (s, e) => OmsCore.Config.ConfigChangedEvent -= Config_ConfigChangedEvent;
            ((OrderTicket)DataContext).TicketStyle = Oms.Enums.OrderTicketStyle.Combined;
            Module = Module.CombinedOrderTicketLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new();
            Topmost = OmsCore.Config.TicketAlwaysOnTop;
        }

        private void Config_ConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            try
            {
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    Topmost = OmsCore.Config.TicketAlwaysOnTop;
                    UpdateFonts(config);
                }));
            }
            catch (Exception) { }
        }

        private void UpdateFonts(OmsConfig config)
        {
            if (BuyPercentBidBox.FontSize != config.PercentBidFontSize)
            {
                BuyPercentBidBox.FontSize = config.PercentBidFontSize;
            }
            if (SellPercentBidBox.FontSize != config.PercentBidFontSize)
            {
                SellPercentBidBox.FontSize = config.PercentBidFontSize;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CombinedTicketLegsGrid.PropertyChanged -= ComplexLegsGrid_PropertyChanged;
        }

        private void ComplexOrderTicketView_Closed(object sender, EventArgs e)
        {
            ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)DataContext;
            _log.Info(nameof(ComplexOrderTicketView_Closed) + " Disposing order model for " + viewModel.SpreadId);
            viewModel.Dispose();
        }

        private void ComplexLegsGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                CombinedTicketLegsGrid?.RefreshData();
            }
            catch (Exception)
            {
            }
        }

        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PriceSpinEdit.IsKeyboardFocusWithin && !PriceSpinEdit.IsMouseOver)
            {
                if (e.Delta > 0)
                {
                    PriceSpinEdit.SpinUp();
                }
                else if (e.Delta < 0)
                {
                    PriceSpinEdit.SpinDown();
                }
            }
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

        private void GridControl_CustomColumnsDisplayText(object sender, DevExpress.Xpf.Grid.CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date)
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName == "LutTimeOnly")
                {
                    e.DisplayText = dateTime.ToString("T");
                }
                else
                {
                    e.DisplayText = dateTime.ToString(OmsCore.Config.LayoutDefaultDateTimeColumnFormat);
                }
            }
            else if (e.Value is double doubleVal)
            {
                if (double.IsNaN(doubleVal))
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName is "Price" or "AveragePrice")
                {
                    e.DisplayText = doubleVal.ToString("#,##0.00;(#,##0.00)");
                }
                else if (e.Column.FieldName == "Strike")
                {
                    e.DisplayText = doubleVal.ToString();
                }
                else
                {
                    e.DisplayText = doubleVal.ToString("n2");
                }
            }
        }

        private void SpinEdit_Click(object sender, MouseButtonEventArgs e)
        {
            SpinEdit spinEdit = (SpinEdit)sender;
            spinEdit.SelectAll();
        }

        private void BasketButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            SimpleButton button = (SimpleButton)sender;
            _dragging = false;
            _startpos = e.GetPosition(button);
        }

        private void BasketButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            SimpleButton button = (SimpleButton)sender;
            Point currentpos = e.GetPosition(button);
            button.ReleaseMouseCapture();
            Vector delta = currentpos - _startpos;
            if ((delta.Length > 10.0 || _dragging) && e.LeftButton == MouseButtonState.Pressed)
            {
                _dragging = true;
                if (DataContext is ComplexOrderTicketViewModel viewModel)
                {
                    OmsOrder order = viewModel.ToOrder();
                    List<OmsOrder> orders = new()
                    {
                        order
                    };
                    DataObject dataObject = new(DataFormats.Serializable, orders);
                    DragDrop.DoDragDrop(button, dataObject, DragDropEffects.Move);
                }
            }
        }

        private void BasketButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SimpleButton button = sender as SimpleButton;
            button.ReleaseMouseCapture();
            if (_dragging)
            {
                _dragging = false;
            }
            else
            {
                ((ComplexOrderTicketViewModel)DataContext).OpenInBasketTrader();
            }
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                GridColumn col = (GridColumn)gridMenuEventArgs.MenuInfo.Column;

                BarButtonItem removeColumnButton = new()
                {
                    Content = "Hide This Column",
                };

                removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { col.Visible = false; };

                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);
            }

            if (OmsCore.Config.ShowNagBotButtonInOrderbookV2)
            {
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarSubItem sendToNagbotButton = GetSendToNagBotButtonAsync();
                if (sendToNagbotButton != null)
                {
                    gridMenuEventArgs.Customizations.Add(sendToNagbotButton);
                }
            }
        }

        private BarSubItem GetSendToNagBotButtonAsync()
        {
            try
            {
                ComplexOrderTicketViewModel dataContext = (ComplexOrderTicketViewModel)DataContext;
                List<Tuple<string, BasketTraderViewModel>> baskets = dataContext.BasketGroupManagerModel.AllBaskets;
                BarSubItem sendToNagbotButton = new()
                {
                    Content = "Send To NagBot"
                };

                BarButtonItem nagBotSubMenu = new()
                {
                    Content = "New Basket Trader",
                    Command = dataContext.OpenInNagbotBasketTraderCommand,
                };
                sendToNagbotButton.Items.Add(nagBotSubMenu);
                sendToNagbotButton.Items.Add(new BarItemSeparator());

                foreach (Tuple<string, BasketTraderViewModel> basket in baskets)
                {
                    string title = basket.Item1;

                    nagBotSubMenu = new BarButtonItem
                    {
                        Content = title,
                        CommandParameter = basket.Item2,
                        Command = dataContext.SendToNagBotCommand,
                    };
                    sendToNagbotButton.Items.Add(nagBotSubMenu);
                }

                return sendToNagbotButton;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetSendToNagBotButtonAsync));
                return null;
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

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                CombinedTicketLegsGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void LayoutSettings(GridControl grid)
        {
            TableCustomizationView tableCustomizationView = new();
            TableCustomizationViewModel viewModel = (TableCustomizationViewModel)tableCustomizationView.DataContext;

            viewModel.Customize(grid, GridFieldNameToConfigMap);

            tableCustomizationView.ShowDialog();
        }

        private void TicketWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false);
        }

        private async void RestoreLayout(object sender, RoutedEventArgs e)
        {
            if (!Clone && showQuickRoutesCheckBox.IsChecked == true)
            {
                Height += 30;
            }
            if (!Clone && showOrderInstructionsPanelCheckBox.IsChecked == true)
            {
                Height += 65;
            }
            UpdateFonts(OmsCore.Config);
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            ComplexOrderTicketViewModel dataContext = (ComplexOrderTicketViewModel)DataContext;
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
                    await RestoreFromConfigSaveAsync(configSave);
                }
                else
                {
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        await RestoreFromConfigSaveAsync(configSave);
                    }
                    else
                    {
                        await RestoreFromConfigSaveAsync(ConfigSave);
                    }
                }
                await dataContext.LoadViewModelConfigAsync(Uid);
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
                viewModel.ShowLocation = true;
                view.ShowDialog();

                if (viewModel.Success)
                {
                    ConfigSave.Id = viewModel.Id;
                    ConfigSave.Title = viewModel.Title;
                    ConfigSave.Group = viewModel.SelectedGroup;
                    SaveLayout(viewModel.SetAsDefault, viewModel.SaveLocation);
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

        public void SaveLayout(bool saveDefault, bool saveLocation = false)
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
                    ConfigSave.ConfigJson = GetConfigAsJson(isDefault: !saveLocation);
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
                await RestoreFromConfigSaveAsync(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal async Task RestoreFromConfigSaveAsync(ConfigSave configSave)
        {
            if (configSave != null)
            {
                SetTitleFromConfigSave(configSave);
                ConfigSave = configSave;
                await LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private void SetTitleFromConfigSave(ConfigSave configSave)
        {
            if (!string.IsNullOrWhiteSpace(configSave.Title))
            {
                if (DataContext is ComplexOrderTicketViewModel viewModel)
                {
                    viewModel.Description = configSave.Title + " - " + MODULE_NAME;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(CombinedTicketLegsGrid)] = LayoutHelper.GetLayoutAsString(CombinedTicketLegsGrid),
                [nameof(showQuickRoutesCheckBox)] = showQuickRoutesCheckBox.IsChecked.ToString(),
                [nameof(showOrderInstructionsPanelCheckBox)] = showOrderInstructionsPanelCheckBox.IsChecked.ToString(),
                [nameof(showAutoHedgePanelCheckBox)] = showAutoHedgePanelCheckBox.IsChecked.ToString(),
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

                if (configDictionary.ContainsKey(nameof(CombinedTicketLegsGrid)))
                {
                    if (configDictionary.TryGetValue(nameof(CombinedTicketLegsGrid), out string gridSetup))
                    {
                        LayoutHelper.RestoreLayoutFromString(gridSetup, CombinedTicketLegsGrid);
                    }
                }

                if (configDictionary.ContainsKey(nameof(showQuickRoutesCheckBox)))
                {
                    if (bool.TryParse(configDictionary[nameof(showQuickRoutesCheckBox)], out bool showQuickRoutes))
                    {
                        showQuickRoutesCheckBox.IsChecked = showQuickRoutes;
                    }
                }

                if (configDictionary.ContainsKey(nameof(showOrderInstructionsPanelCheckBox)))
                {
                    if (bool.TryParse(configDictionary[nameof(showOrderInstructionsPanelCheckBox)], out bool showOrderInstructions))
                    {
                        showOrderInstructionsPanelCheckBox.IsChecked = showOrderInstructions;
                    }
                }

                if (configDictionary.ContainsKey(nameof(showAutoHedgePanelCheckBox)))
                {
                    if (bool.TryParse(configDictionary[nameof(showAutoHedgePanelCheckBox)], out bool showAutoHedgePanel))
                    {
                        showAutoHedgePanelCheckBox.IsChecked = showAutoHedgePanel;
                    }
                }

                if (configDictionary.ContainsKey(nameof(WindowSetting)))
                {
                    if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                    {
                        WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                        LoadWindowSettings(windowSettings);
                    }
                }

                if (configDictionary.ContainsKey(nameof(GridFieldNameToConfigMap)))
                {
                    if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                    {
                        GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                        TableCustomizationViewModel tableCustomizationViewModel = new();
                        tableCustomizationViewModel.Load(CombinedTicketLegsGrid, GridFieldNameToConfigMap);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private async void LoadWindowSettings(WindowSetting windowSettings)
        {
            bool isDefault = windowSettings.Left < 0 && windowSettings.Top < 0;

            if (isDefault)
            {
                if (!Clone && !Manual)
                {
                    Left = 0;
                    Top = SystemParameters.PrimaryScreenHeight - Height - 50;
                    return;
                }
            }

            if (!Clone)
            {
                Left = windowSettings.Left;
                Top = windowSettings.Top;
                if (windowSettings.Width >= 0)
                {
                    Width = windowSettings.Width;
                }
                if (windowSettings.Height >= 0)
                {
                    Height = windowSettings.Height;
                }
                WindowState = windowSettings.WindowState;
            }

            if ((Clone || !Manual) && OmsCore.Config.DoNotStackUpTickets)
            {
                while (await StartupWindowViewModel.MainWindow.WindowHelper.IsPointTakenAsync(this))
                {
                    double nextTop = Top - Height;
                    if (nextTop > 0)
                    {
                        Top = nextTop;
                    }
                    else
                    {
                        Top = SystemParameters.PrimaryScreenHeight - Height - 50;

                        double nextLeft = Left + Width;
                        if (nextLeft > SystemParameters.VirtualScreenWidth)
                        {
                            break;
                        }
                        Left = nextLeft;
                    }
                }
            }
        }

        private void ShowOrderInstructionsPanelCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int ORDER_INSTRUCTIONS_PANEL = 65;
            if (showOrderInstructionsPanelCheckBox.IsChecked)
            {
                Height += ORDER_INSTRUCTIONS_PANEL;
            }
            else
            {
                Height -= ORDER_INSTRUCTIONS_PANEL;
            }
        }

        private void ShowQuickRoutesCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int QUICK_ROUTES_PANEL = 30;
            if (showQuickRoutesCheckBox.IsChecked)
            {
                Height += QUICK_ROUTES_PANEL;
            }
            else
            {
                Height -= QUICK_ROUTES_PANEL;
            }
        }

        private void ShowAutoHedgeCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int AUTO_HEDGE_PANEL = 0;
            if (showAutoHedgePanelCheckBox.IsChecked)
            {
                Height += AUTO_HEDGE_PANEL;
            }
            else
            {
                Height -= AUTO_HEDGE_PANEL;
            }
        }

        private void ShowSubmitWithDelayPanelCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int PANEL_HEIGHT = 118;
            if (showSubmitWithDelayPanelCheckBox.IsChecked)
            {
                Height += PANEL_HEIGHT;
            }
            else
            {
                Height -= PANEL_HEIGHT;
            }
        }

        private void ShowStopLossPanelCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int PANEL_HEIGHT = 40;
            if (showStopLossPanelCheckBox.IsChecked)
            {
                Height += PANEL_HEIGHT;
            }
            else
            {
                Height -= PANEL_HEIGHT;
            }
        }

        private void ShowShowSpeedTraderCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int SPEED_TRADER_PANEL = 80;
            if (showSpeedTraderCheckBox.IsChecked)
            {
                Height += SPEED_TRADER_PANEL;
            }
            else
            {
                Height -= SPEED_TRADER_PANEL;
            }
        }

        private void ShowRiskRunCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int AUTO_HEDGE_PANEL = 0;
            if (showAutoHedgePanelCheckBox.IsChecked)
            {
                Height += AUTO_HEDGE_PANEL;
            }
            else
            {
                Height -= AUTO_HEDGE_PANEL;
            }
        }
    }
}
