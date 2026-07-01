using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using LayoutHelper = ZeroPlus.Oms.Ui.Helper.LayoutHelper;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ComplexOrderTicketView.xaml
    /// </summary>
    public partial class ComplexOrderTicketView : ModuleWindow, IModuleView
    {
        private const string MODULE_NAME = "Complex Order Ticket";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly object _initLock = new();

        private bool _dragging;
        private Point _startpos;
        private bool _layoutRestored;
        private ComplexOrderTicketViewModel _viewModel => (ComplexOrderTicketViewModel)ViewModel;
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool Manual { get; set; } = true;
        public bool Clone { get; set; }
        public bool Contra { get; set; }

        public ComplexOrderTicketView() : this(null, Guid.NewGuid().ToString())
        {
        }

        public ComplexOrderTicketView(IModuleFactory moduleFactory, string uid = "", bool loadDefault = true) : base(Module.ComplexOrderTicket, uid, moduleFactory, loadDefault)
        {
            Uid = uid;
            lock (_initLock)
            {
                InitializeComponent();
            }
            Name = nameof(ComplexOrderTicketView);
            Closing += Window_Closing;
            OmsCore.Config.ConfigChangedEvent += Config_ConfigChangedEvent;
            Module = Module.ComplexOrderTicketLayout;
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
                Dispatcher?.Invoke(() =>
                {
                    Topmost = OmsCore.Config.TicketAlwaysOnTop;
                    UpdateFonts(config);
                });
            }
            catch (Exception) { }
        }

        private void UpdateFonts(OmsConfig config)
        {
            if (PercentBidBox.FontSize != config.PercentBidFontSize)
            {
                PercentBidBox.FontSize = config.PercentBidFontSize;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Closing -= Window_Closing;
            OmsCore.Config.ConfigChangedEvent -= Config_ConfigChangedEvent;
            ComplexLegsGrid.Columns?.Clear();
        }

        private void ComplexLegsGrid_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher?.BeginInvoke(() => ComplexLegsGrid?.RefreshData());
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
            else if (PriceSpinEditDepth.IsKeyboardFocusWithin && !PriceSpinEditDepth.IsMouseOver)
            {
                if (e.Delta > 0)
                {
                    PriceSpinEditDepth.SpinUp();
                }
                else if (e.Delta < 0)
                {
                    PriceSpinEditDepth.SpinDown();
                }
            }
            else if (PriceSpinEditSellDepth.IsKeyboardFocusWithin && !PriceSpinEditSellDepth.IsMouseOver)
            {
                if (e.Delta > 0)
                {
                    PriceSpinEditSellDepth.SpinUp();
                }
                else if (e.Delta < 0)
                {
                    PriceSpinEditSellDepth.SpinDown();
                }
            }
        }
        private void SpinEdit_MouseUp(object sender, MouseButtonEventArgs e)
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

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                ComplexLegsGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
            }
        }

        private void TicketWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ControlPxKeyToggle.Focus();
                Keyboard.Focus(null);
            }
        }
        #region Layout and Config Save and Restore

        protected override async void RestoreLayout(bool loadDefault)
        {
            base.RestoreLayout(loadDefault);
            if (loadDefault)
            {
                if (!Clone && _viewModel.ShowQuickRoutes)
                {
                    Height += 30;
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
                        string export = await File.ReadAllTextAsync(instanceExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        await RestoreFromConfigSaveAsync(configSave);
                    }
                    else
                    {
                        string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                        if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                        {
                            string export = await File.ReadAllTextAsync(defaultExportPath);
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
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(ComplexLegsGrid)] = LayoutHelper.GetLayoutAsString(ComplexLegsGrid),
                [nameof(TronTradesGrid)] = LayoutHelper.GetLayoutAsString(TronTradesGrid),
                ["showQuickRoutesCheckBox"] = _viewModel.ShowQuickRoutes.ToString(),
                ["showAutoHedgePanelCheckBox"] = _viewModel.ShowAutoHedge.ToString(),
                ["showTimeAndSalesPanelCheckBox"] = _viewModel.ShowTimeAndSales.ToString(),
                ["showDepthBookCheckBox"] = _viewModel.ShowDepthBook.ToString(),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
                [nameof(ControlPxKeyToggle)] = ControlPxKeyToggle.IsChecked.ToString(),
            };

            if (_viewModel.ShowTimeAndSales || _viewModel.ShowDepthBook)
            {
                System.Windows.GridLengthConverter glc = new();
                configDictionary[nameof(GridSplitterRow)] = glc.ConvertToString(GridSplitterRow.Height).Replace("*", "");
            }

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }

                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));
                LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(ComplexLegsGrid)], ComplexLegsGrid);

                if (configDictionary.ContainsKey(nameof(TronTradesGrid)))
                {
                    if (configDictionary.TryGetValue(nameof(TronTradesGrid), out string export))
                    {
                        LayoutHelper.RestoreLayoutFromString(export, TronTradesGrid);
                    }
                }

                if (configDictionary.TryGetValue("showQuickRoutesCheckBox", out var value))
                {
                    if (bool.TryParse(value, out bool showQuickRoutes))
                    {
                        _viewModel.ShowQuickRoutes = showQuickRoutes;
                    }
                }

                if (configDictionary.TryGetValue("showAutoHedgePanelCheckBox", out var value2))
                {
                    if (bool.TryParse(value2, out bool showAutoHedgePanel))
                    {
                        _viewModel.ShowAutoHedge = showAutoHedgePanel;
                    }
                }

                bool showTimeAndSalesPanel = false;
                if (configDictionary.TryGetValue("showTimeAndSalesPanelCheckBox", out var value3))
                {
                    if (bool.TryParse(value3, out showTimeAndSalesPanel))
                    {
                        _viewModel.ShowTimeAndSales = showTimeAndSalesPanel;

                        if (configDictionary.TryGetValue(nameof(GridSplitterRow), out string height))
                        {
                            System.Windows.GridLengthConverter glc = new();
                            GridSplitterRow.Height = (GridLength)glc.ConvertFromString(height);
                        }
                    }
                }

                if (configDictionary.TryGetValue("showDepthBookCheckBox", out var value4))
                {
                    if (bool.TryParse(value4, out bool showDepthBook))
                    {
                        _viewModel.ShowDepthBook = showDepthBook;

                        if (!showTimeAndSalesPanel && configDictionary.TryGetValue(nameof(GridSplitterRow), out string height))
                        {
                            System.Windows.GridLengthConverter glc = new();
                            GridSplitterRow.Height = (GridLength)glc.ConvertFromString(height);
                        }
                    }
                }

                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                    LoadWindowSettings(windowSettings);
                }

                if (configDictionary.TryGetValue(nameof(ControlPxKeyToggle), out string controlPxKeyToggled) && bool.TryParse(controlPxKeyToggled, out bool enabled) && DataContext is ComplexOrderTicketViewModel viewModel)
                {
                    viewModel.EnableControlPxKey = enabled;
                }

                if (configDictionary.TryGetValue(nameof(GridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    GridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(ComplexLegsGrid, GridFieldNameToConfigMap);
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

            if (!Clone && !Contra)
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
                bool contDown = false;
                bool contRight = false;

                int id = GetHashCode();
                string name = Name;

                System.Drawing.Size size = new((int)Width, (int)Height);
                System.Drawing.Point location = new((int)Left, (int)Top);
                System.Drawing.Rectangle rectangle = new(location, size);

                Tuple<bool, System.Drawing.Rectangle> takenTestTuple = await StartupWindowViewModel.MainWindow.WindowHelper.IsPointTakenAsync(id, name, rectangle);
                bool isTaken = takenTestTuple.Item1;
                System.Drawing.Rectangle other = takenTestTuple.Item2;
                while (isTaken)
                {
                    if (!contRight)
                    {
                        rectangle = !contDown
                            ? new System.Drawing.Rectangle(new System.Drawing.Point(rectangle.X, (int)(other.Top - Height)), size)
                            : new System.Drawing.Rectangle(new System.Drawing.Point(rectangle.X, (int)(other.Top + Height)), size);
                    }
                    else
                    {
                        contRight = false;
                        contDown = false;

                        rectangle = new System.Drawing.Rectangle(new System.Drawing.Point(rectangle.X + other.Width, rectangle.Y), size);
                    }
                    if (!StartupWindowViewModel.MainWindow.WindowHelper.IsVisible(rectangle))
                    {
                        if (!contDown)
                        {
                            rectangle = new System.Drawing.Rectangle(new System.Drawing.Point(rectangle.X, other.Top), size);
                            contDown = true;
                        }
                        else
                        {
                            rectangle = new(location, size);
                            if (!contRight)
                            {
                                contRight = true;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    takenTestTuple = await StartupWindowViewModel.MainWindow.WindowHelper.IsPointTakenAsync(id, name, rectangle);
                    isTaken = takenTestTuple.Item1;
                    other = takenTestTuple.Item2;
                }

                Left = rectangle.X;
                Top = rectangle.Y;
            }
        }
        #endregion

        private void ShowTimeAndSalesUpdated(object sender, RoutedEventArgs e)
        {
            const int TIME_AND_SALES_PANEL = 0;
            if (_viewModel.ShowTimeAndSales)
            {
                Height += TIME_AND_SALES_PANEL;
                GridSplitterRow.Height = GridLength.Auto;
            }
            else
            {
                Height -= TIME_AND_SALES_PANEL;
                GridSplitterRow.Height = new GridLength(1, GridUnitType.Star);
            }
            UpdateWidth();
        }

        private void ShowDepthBookUpdated(object sender, RoutedEventArgs e)
        {
            const int TIME_AND_SALES_PANEL = 0;
            if (_viewModel.ShowDepthBook)
            {
                Height += TIME_AND_SALES_PANEL;
                GridSplitterRow.Height = GridLength.Auto;
            }
            else
            {
                Height -= TIME_AND_SALES_PANEL;
                GridSplitterRow.Height = new GridLength(1, GridUnitType.Star);
            }
            UpdateWidth();
        }

        private void UpdateWidth()
        {
            if (_viewModel.ShowDepthBook && _viewModel.ShowTimeAndSales)
            {
                depthBookWidth.Width = new GridLength(2, GridUnitType.Star);
                timeAndSalesWidth.Width = new GridLength(1, GridUnitType.Star);
            }
            else if (_viewModel.ShowDepthBook && !_viewModel.ShowTimeAndSales)
            {
                depthBookWidth.Width = new GridLength(2, GridUnitType.Star);
                timeAndSalesWidth.Width = GridLength.Auto;
            }
            else if (!_viewModel.ShowDepthBook && _viewModel.ShowTimeAndSales)
            {
                depthBookWidth.Width = GridLength.Auto;
                timeAndSalesWidth.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                depthBookWidth.Width = GridLength.Auto;
                timeAndSalesWidth.Width = GridLength.Auto;
            }
        }

        private void ShowOrderInstructionsPanelCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int QUICK_ROUTES_PANEL = 30;
            if (_viewModel.ShowOrderInstructions)
            {
                Height += QUICK_ROUTES_PANEL;
            }
            else
            {
                Height -= QUICK_ROUTES_PANEL;
            }
        }

        private void ShowQuickRoutesCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int QUICK_ROUTES_PANEL = 0;
            if (_viewModel.ShowQuickRoutes)
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
            if (_viewModel.ShowAutoHedge)
            {
                Height += AUTO_HEDGE_PANEL;
            }
            else
            {
                Height -= AUTO_HEDGE_PANEL;
            }
        }

        private void ShowShowSpeedTraderCheckBoxUpdated(object sender, RoutedEventArgs e)
        {
            const int SPEED_TRADER_PANEL = 40;
            if (_viewModel.ShowSpeedTrader)
            {
                Height += SPEED_TRADER_PANEL;
            }
            else
            {
                Height -= SPEED_TRADER_PANEL;
            }
        }

        private void DepthBookBidGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ComplexOrderTicketViewModel viewModel)
            {
                int rowHandle = DepthBookBidGrid.View.GetRowHandleByMouseEventArgs(e);
                if (rowHandle != DataControlBase.InvalidRowHandle &&
                    !DepthBookBidGrid.IsGroupRowHandle(rowHandle))
                {
                    DepthItemModel model = (DepthItemModel)DepthBookBidGrid.GetRow(rowHandle);
                    viewModel.SetBidFromDepthItem(model);
                }
            }
        }

        private void DepthBookAskGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ComplexOrderTicketViewModel viewModel)
            {
                int rowHandle = DepthBookAskGrid.View.GetRowHandleByMouseEventArgs(e);
                if (rowHandle != DataControlBase.InvalidRowHandle &&
                    !DepthBookAskGrid.IsGroupRowHandle(rowHandle))
                {
                    DepthItemModel model = (DepthItemModel)DepthBookAskGrid.GetRow(rowHandle);
                    viewModel.SetAskFromDepthItem(model);
                }
            }
        }

        public override void ClearFiltersClick()
        {
            ComplexLegsGrid.FilterCriteria = null;
            ComplexLegsGrid.FilterString = string.Empty;
            DepthBookBidGrid.FilterCriteria = null;
            DepthBookBidGrid.FilterString = string.Empty;
            DepthBookAskGrid.FilterCriteria = null;
            DepthBookAskGrid.FilterString = string.Empty;
            TronTradesGrid.FilterCriteria = null;
            TronTradesGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            ComplexLegsGrid.ClearSorting();
            DepthBookBidGrid.ClearSorting();
            DepthBookAskGrid.ClearSorting();
            TronTradesGrid.ClearSorting();
        }

        private void TrackerClick(object sender, RoutedEventArgs e)
        {
            TrackerDropDown.IsPopupOpen = false;
        }

        private void ShowMatrixAlgoPanelChecked(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowMatrixAlgoPanel = true;
        }

        private void ShowMatrixAlgoPanelUnchecked(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowMatrixAlgoPanel = false;
        }
    }
}
