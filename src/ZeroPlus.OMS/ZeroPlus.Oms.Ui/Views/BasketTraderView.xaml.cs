using DevExpress.Utils;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;
using GridLengthConverter = System.Windows.GridLengthConverter;
using GroupBox = DevExpress.Xpf.LayoutControl.GroupBox;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for BasketTraderView.xaml
    /// </summary>
    public partial class BasketTraderView : ModuleWindow, IModuleView, ISupportCustomColumn, ISupportGettingItemsByVisualOrder
    {
        private readonly OmsCore _omsCore;
        private static readonly HashSet<string> _oldBasketColumnsToRemove = new() { "IsLooping", "SpreadId" };

        private const string MODULE_NAME = "Basket Trader";
        private const Module MODULE = Module.BasketTraderLayout;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public BasketTraderView(OmsCore omsCore, IModuleFactory moduleFactory, string uid = null, bool loadDefault = true) : base(MODULE, uid, moduleFactory, loadDefault)
        {
            _omsCore = omsCore;
            InitializeComponent();
            Reset();
            Closing += View_Closing;
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (DataContext is BasketTraderViewModel viewModel)
                {
                    _log.Info(nameof(View_Closing) + " Basket View Closed. Id: " + viewModel.InstanceId);
                    bool cancel = viewModel.Dispose();
                    e.Cancel = cancel;
                    if (!cancel)
                    {
                        Closing -= View_Closing;
                        Basket.BasketGrid.ItemsSource = null;
                        StartupWindowViewModel.MainWindow.WindowHelper.RemoveWindow(this);
                        DataContext = null;
                    }
                }
                else
                {
                    StartupWindowViewModel.MainWindow.WindowHelper.RemoveWindow(this);
                    DataContext = null;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(View_Closing));
            }
        }

        public void AddColumn(CustomColumnTemplateModel colTemplate)
        {
            GridColumn column = new()
            {
                FieldName = colTemplate.Header,
                Header = colTemplate.Header,
                Visible = true,
                AllowUnboundExpressionEditor = colTemplate.AllowEquationEvaluator,
                UnboundType = colTemplate.Type,
                AllowEditing = colTemplate.AllowEditing ? DefaultBoolean.True : DefaultBoolean.False,
            };

            if (Basket.BasketGrid.Columns.Any(x => x.FieldName == column.FieldName))
            {
                Basket.BasketGrid.Columns.First(x => x.FieldName == column.FieldName).Visible = true;
            }
            else
            {
                Basket.BasketGrid.Columns.Add(column);
            }
        }

        public List<CustomColumnTemplateModel> GetExpressionEditors()
        {
            List<CustomColumnTemplateModel> columns = new();
            foreach (GridColumn column in Basket.BasketGrid.Columns.Where(x => x.AllowUnboundExpressionEditor))
            {
                columns.Add(new CustomColumnTemplateModel()
                {
                    Header = column.FieldName,
                    AllowEditing = column.AllowEditing == DefaultBoolean.True,
                    AllowEquationEvaluator = column.AllowUnboundExpressionEditor,
                    Equation = column.UnboundExpression
                });
            }

            return columns;
        }

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();
            for (int i = 0; i < Basket.BasketGrid.VisibleRowCount; i++)
            {
                int rowHandle = Basket.BasketGrid.GetRowHandleByVisibleIndex(i);
                list.Add(Tuple.Create(i + 1, Basket.BasketGrid.GetRow(rowHandle)));
            }
            if (startFromSelectedRow && Basket.BasketGrid.SelectedItem != null)
            {
                Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == Basket.BasketGrid.SelectedItem);
                if (selectedRow != null)
                {
                    list = list.Skip(list.IndexOf(selectedRow)).ToList();
                }
            }
            return list;
        }

        public HashSet<T> GetVisibleItems<T>()
        {
            return null;
        }

        public bool ItemIsVisible(object item)
        {
            for (int i = 0; i < Basket.BasketGrid.VisibleRowCount; i++)
            {
                int rowHandle = Basket.BasketGrid.GetRowHandleByVisibleIndex(i);
                object check = Basket.BasketGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
        }

        private void SetQuickAccessWidth()
        {
            if (Basket.GridSplitterCol.Width.Value > 0)
            {
                Basket.GridSplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
                Basket.ExpandCollapseGridButton.Content = 4;
            }
            else
            {
                Basket.GridSplitterCol.Width = new GridLength(750, GridUnitType.Pixel);
                Basket.ExpandCollapseGridButton.Content = 3;
            }
        }

        protected override void OnModuleLoaded()
        {
            base.OnModuleLoaded();
            BasketTraderViewModel dataContext = (BasketTraderViewModel)DataContext;
            dataContext.Uid = Uid;
            dataContext.Name = "Basket " + dataContext.BasketSettings.Uid;
            SetQuickAccessWidth();
        }

        private void LoadQuickLayout(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem { Tag: ConfigSave config })
                {
                    switch ((Module)config.Module)
                    {
                        case Module.BasketTrader:
                        case Module.BasketTraderLayout:
                            RestoreFromConfigSave(config, withContent: false);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadQuickLayout));
            }
        }

        internal void RestoreFromConfigSave(ConfigSave configSave, bool withContent = true)
        {
            if (configSave != null)
            {
                UsingDefaultConfig = false;
                ConfigSave = configSave;
                Basket.ConfigSave = configSave;
                LoadConfigFromJsonAsync(configSave.ConfigJson, true, withContent);
            }
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withItems = false)
        {
            BasketTraderViewModel dataContext = (BasketTraderViewModel)DataContext;
            GridLengthConverter glc = new();
            string splitterHeight = glc.ConvertToString(Basket.GridSplitterCol.Width)?.Replace("*", "") ?? "0";

            Dictionary<string, int> subModuleOrderMap = new();

            System.Collections.IList list = Basket.SubmodulePanel.Children;
            for (int i = 0; i < list.Count; i++)
            {
                GroupBox item = (GroupBox)list[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                {
                    subModuleOrderMap[item.Name] = i;
                }
            }

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(BasketTraderConfig)] = dataContext.GetConfigSerialized(withItems: withItems, onlyLayout: true),
                [nameof(Basket.BasketGrid)] = LayoutHelper.GetLayoutAsString(Basket.BasketGrid),
                [nameof(Basket.GridSplitterCol)] = splitterHeight,
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(Basket.BasketTradersGridFieldNameToConfigMap)] = JsonConvert.SerializeObject(Basket.BasketTradersGridFieldNameToConfigMap),
                [nameof(Basket.SubmodulePanel)] = JsonConvert.SerializeObject(subModuleOrderMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            try
            {
                UsingDefaultConfig = false;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        BasketTraderViewModel dataContext = (BasketTraderViewModel)DataContext;
                        dataContext.ModuleTitle = ConfigSave.Title + " - " + MODULE_NAME;
                        dataContext?.InvokeReady();
                    });
                    return;
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                Dictionary<string, string> configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson);
                var baseConfigDeserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                Dictionary<string, ColumnConfigModel> configMap = null;
                if (configDictionary.ContainsKey(nameof(Basket.BasketTradersGridFieldNameToConfigMap)))
                {
                    if (configDictionary.TryGetValue(nameof(Basket.BasketTradersGridFieldNameToConfigMap), out string fieldMapConfig))
                    {
                        configMap = JsonConvert.DeserializeObject<Dictionary<string, ColumnConfigModel>>(fieldMapConfig);
                    }
                }
                var gridFieldDeserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                WindowSetting windowSettings = null;
                if (configDictionary.ContainsKey(nameof(WindowSetting)))
                {
                    if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
                    {
                        windowSettings = WindowSetting.DeserializeFromJson(windowSettingExport);
                    }
                }
                var windowSettingDeserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                Dictionary<string, int> subModuleMap = null;
                if (configDictionary.TryGetValue(nameof(Basket.SubmodulePanel), out string subModuleDictionary))
                {
                    subModuleMap = JsonConvert.DeserializeObject<Dictionary<string, int>>(subModuleDictionary);
                }
                var subModuleMapDeserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                byte[] layoutBuffer = null;
                if (configDictionary.TryGetValue(nameof(Basket.BasketGrid), out string content))
                {
                    content = LayoutHelper.RemoveHiddenAndDuplicateColumns(content, _oldBasketColumnsToRemove);
                    layoutBuffer = Encoding.UTF8.GetBytes(content);
                }
                var layoutBufferDeserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                Dispatcher?.Invoke(() =>
                {
                    var waitForDispatcher = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    ApplyGridSplitterConfig(configDictionary);
                    var gridSplitterLoad = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    ApplyTableCustomizations(configMap);
                    var tableCustomizationLoad = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    ApplyGridLayout(layoutBuffer);
                    var layoutLoad = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    ApplyViewModelConfigs(withContent, configDictionary);
                    var vmLoad = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    ApplySubModuleArrangement(subModuleMap);
                    var submoduleArrangement = stopwatch.ElapsedMilliseconds;
                    stopwatch.Stop();

                    if (!offset)
                    {
                        ApplyWindowSettings(windowSettings);
                    }
                    var windowSizeAndLoc = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();

                    long total = baseConfigDeserialize + gridFieldDeserialize + windowSettingDeserialize +
                                 subModuleMapDeserialize + layoutBufferDeserialize + layoutLoad +
                                 waitForDispatcher + windowSizeAndLoc + gridSplitterLoad +
                                 tableCustomizationLoad + submoduleArrangement + vmLoad;

                    _log.Info($"Basket Open Stats, " +
                              $"{nameof(baseConfigDeserialize)}: {baseConfigDeserialize:F0}, " +
                              $"{nameof(gridFieldDeserialize)}: {gridFieldDeserialize:F0}, " +
                              $"{nameof(windowSettingDeserialize)}: {windowSettingDeserialize:F0}, " +
                              $"{nameof(subModuleMapDeserialize)}: {subModuleMapDeserialize:F0}, " +
                              $"{nameof(waitForDispatcher)}: {waitForDispatcher:F0}, " +
                              $"{nameof(windowSizeAndLoc)}: {windowSizeAndLoc:F0}, " +
                              $"{nameof(gridSplitterLoad)}: {gridSplitterLoad:F0}, " +
                              $"{nameof(tableCustomizationLoad)}: {tableCustomizationLoad:F0}, " +
                              $"{nameof(submoduleArrangement)}: {submoduleArrangement:F0}, " +
                              $"{nameof(layoutBufferDeserialize)}: {layoutBufferDeserialize:F0}, " +
                              $"{nameof(layoutLoad)}: {layoutLoad:F0}, " +
                              $"{nameof(vmLoad)}: {vmLoad:F0}, " +
                              $"Total: {total:F0}");
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private void ApplyGridLayout(byte[] layoutBuffer)
        {
            if (layoutBuffer != null)
            {
                LayoutHelper.RestoreLayoutFromBuffer(layoutBuffer, Basket.BasketGrid);
            }
        }

        private void ApplyWindowSettings(WindowSetting windowSettings)
        {
            if (windowSettings != null)
            {
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

        private void ApplyGridSplitterConfig(Dictionary<string, string> configDictionary)
        {
            GridLengthConverter glc = new();
            if (configDictionary.TryGetValue(nameof(Basket.GridSplitterCol), out var value) && value != null)
            {
                var fromString = glc.ConvertFromString(value);
                if (fromString != null)
                {
                    Basket.GridSplitterCol.Width = (GridLength)fromString;
                }
            }
        }

        private void ApplyTableCustomizations(Dictionary<string, ColumnConfigModel> configMap)
        {
            if (configMap != null)
            {
                Basket.BasketTradersGridFieldNameToConfigMap = configMap;
                TableCustomizationViewModel tableCustomizationViewModel = new();
                tableCustomizationViewModel.Load(Basket.BasketGrid, Basket.BasketTradersGridFieldNameToConfigMap);
            }
        }

        private void ApplyViewModelConfigs(bool withContent, Dictionary<string, string> configDictionary)
        {
            if (configDictionary.ContainsKey(nameof(BasketTraderConfig)))
            {
                configDictionary.TryGetValue(nameof(BasketTraderConfig), out string basketTraderConfig);
                BasketTraderViewModel dataContext = (BasketTraderViewModel)DataContext;
                dataContext.ModuleTitle = ConfigSave.Title + " - " + MODULE_NAME;
                dataContext.LoadConfigFromJsonAsync(basketTraderConfig, withContent);
            }
        }

        private void ApplySubModuleArrangement(Dictionary<string, int> subModuleMap)
        {
            if (subModuleMap != null)
            {
                System.Collections.IList list = Basket.SubmodulePanel.Children;
                List<GroupBox> copy = new();
                foreach (GroupBox item in list)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                    {
                        copy.Add(item);
                    }
                }

                Basket.SubmodulePanel.Children.Clear();
                List<GroupBox> sorted = copy.Where(x => subModuleMap.TryGetValue(x.Name, out _)).OrderBy(x => subModuleMap[x.Name]).ToList();
                foreach (GroupBox item in sorted)
                {
                    Basket.SubmodulePanel.Children.Add(item);
                }

                foreach (GroupBox item in copy)
                {
                    if (!sorted.Contains(item))
                    {
                        Basket.SubmodulePanel.Children.Add(item);
                    }
                }
            }
        }

        private void Reset()
        {
            Basket.NameEdit.IsReadOnly = true;
            Basket.NameEdit.Focusable = false;
            Basket.NameEdit.Cursor = Cursors.Arrow;
            Basket.NameEdit.CaretIndex = Basket.NameEdit.Text.Length;
            Basket.NameEditButton.IsChecked = false;
        }

        public void LoadDefaultConfig(string underlying, bool isSingleLeg, Strategy strategy)
        {
            var config = OmsCore.Config.SavedBasketDefaultLayouts.FirstOrDefault(x => x.Item2 == underlying && ((x.Item3 == LegTypes.All) || (x.Item3 == LegTypes.MLeg && !isSingleLeg) || (x.Item3 == LegTypes.Single && isSingleLeg)) && (x.Item4 == Strategy.All || x.Item4 == strategy));
            if (config != null)
            {
                RestoreFromConfigSave(config.Item5);
            }
        }

        private async void LoadQuickConfig(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            try
            {
                if (e.NewValue is ConfigSave configSave)
                {
                    if (string.IsNullOrWhiteSpace(configSave.ConfigJson))
                    {
                        configSave = await _omsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                    }

                    if (configSave != null)
                    {
                        RestoreFromConfigSave(configSave, withContent: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadQuickConfig));
            }
        }
    }
}