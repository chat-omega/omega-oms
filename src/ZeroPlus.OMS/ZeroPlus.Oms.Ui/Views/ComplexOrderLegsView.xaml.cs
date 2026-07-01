using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Serialization;
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
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ComplexOrderLegsView.xaml
    /// </summary>
    public partial class ComplexOrderLegsView : ThemedWindow
    {
        private const string MODULE_NAME = "Orderbook";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        private Dictionary<string, ColumnConfigModel> FilledOrdersGridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public ComplexOrderLegsView()
        {
            InitializeComponent();
            Loaded += RestoreLayout;
            Module = Module.OrderBookLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
            FilledOrdersGridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
        }

        private void RestoreLayout(object sender, RoutedEventArgs e)
        {
            Loaded -= RestoreLayout;
            RestoreLayout(Uid);
        }

        internal async void RestoreLayout(string uid)
        {
            ComplexOrderLegsViewModel dataContext = (ComplexOrderLegsViewModel)DataContext;
            dataContext.Uid = uid;
            if (!_layoutRestored)
            {
                _layoutRestored = true;
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string instanceExportPath = Path.Combine(layoutDir, $"{uid}-{Module}-layout.json");
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                    await RestoreFromConfigSave(configSave);
                }
                else
                {
                    string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(export);
                        await RestoreFromConfigSave(configSave);
                    }
                    else
                    {
                        await RestoreFromConfigSave(ConfigSave);
                    }
                }
            }
        }

        private async void RestoreFromConfigSaveId(int configSaveId)
        {
            try
            {
                ConfigSave configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSaveId);
                await RestoreFromConfigSave(configSave);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreFromConfigSaveId));
            }
        }

        internal async Task RestoreFromConfigSave(ConfigSave configSave)
        {
            if (configSave != null)
            {
                ConfigSave = configSave;
                await LoadConfigFromJsonAsync(configSave.ConfigJson);
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            ComplexOrderLegsViewModel dataContext = (ComplexOrderLegsViewModel)DataContext;

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(FilledOrderGrid)] = LayoutHelper.GetLayoutAsString(FilledOrderGrid),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
                [nameof(FilledOrdersGridFieldNameToConfigMap)] = JsonConvert.SerializeObject(FilledOrdersGridFieldNameToConfigMap),
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
                if (configDictionary.TryGetValue(nameof(FilledOrderGrid), out string gridExport))
                {
                    LayoutHelper.RestoreLayoutFromString(gridExport, FilledOrderGrid);
                    FilledOrderGrid.FilterString = "";
                }

                if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                {
                    WindowSetting windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                    Window window = this;
                    window.Left = windowSettings.Left;
                    window.Top = windowSettings.Top;
                    if (windowSettings.Width > 0)
                    {
                        window.Width = windowSettings.Width;
                    }
                    if (windowSettings.Height > 0)
                    {
                        window.Height = windowSettings.Height;
                    }
                    window.WindowState = windowSettings.WindowState;
                }
                if (configDictionary.TryGetValue(nameof(FilledOrdersGridFieldNameToConfigMap), out string fieldMapConfig))
                {
                    FilledOrdersGridFieldNameToConfigMap = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig));
                    TableCustomizationViewModel tableCustomizationViewModel = new();
                    tableCustomizationViewModel.Load(FilledOrderGrid, FilledOrdersGridFieldNameToConfigMap);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
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

                        col.AddHandler(DXSerializer.CustomGetSerializablePropertiesEvent,
                            new CustomGetSerializablePropertiesEventHandler(Column_CustomGetSerializableProperties));
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

        private void Column_CustomGetSerializableProperties(object sender, CustomGetSerializablePropertiesEventArgs e)
        {
            e.SetPropertySerializable(ColumnBase.CellStyleProperty, new DXSerializable());
            e.SetPropertySerializable(ColumnBase.ActualCellStyleProperty, new DXSerializable());
        }

        private void GridControl_CustomColumnsDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date)
                {
                    e.DisplayText = "";
                }
                else if (e.Column.FieldName == "LutTimeOnly")
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString("hh:mm:ss.fff");
                }
                else if (e.Column.FieldName.StartsWith("HanweckTimestamp"))
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString("hh:mm:ss.fff");
                }
                else
                {
                    e.DisplayText = dateTime == DateTime.MinValue || dateTime.Date == new DateTime(1970, 1, 1).Date ? "" : dateTime.ToString(OmsCore.Config.LayoutDefaultDateTimeColumnFormat);
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
                else if (e.Column.FieldName == "Delta")
                {
                    e.DisplayText = doubleVal.ToString("n3");
                }
                else
                {
                    e.DisplayText = doubleVal.ToString("n2");
                }
            }
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if (sender is GridControl gridControl)
                {
                    if (gridControl == FilledOrderGrid)
                    {
                        FilledOrderGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
                    }
                }
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private void OnStartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            List<OmsOrder> orders = new();
            string symbol = "";
            foreach (OmsOrderModel orderModel in e.Records)
            {
                orders.Add(orderModel.ToOrder());
                symbol = orderModel.Symbol;
            }
            e.Data.SetData(DataFormats.Serializable, orders);
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                e.Data.SetData(DataFormats.CommaSeparatedValue, symbol);
            }
        }
    }
}
