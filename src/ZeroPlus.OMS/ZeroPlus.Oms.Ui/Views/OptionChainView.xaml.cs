using DevExpress.Images;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for OptionChainView.xaml
    /// </summary>
    public partial class OptionChainView : ThemedWindow, IModuleView, ISupportGettingItemsByVisualOrder
    {
        private const string MODULE_NAME = "Option Chain";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;
        private OptionChainViewModel _dataContext;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        private Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public OptionChainView() : this(Guid.NewGuid().ToString())
        {
        }

        public OptionChainView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(OptionChainView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += (s, e) => OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            Closed += OptionChainView_Closed;
            Loaded += RestoreLayout;
            Module = Module.OptionChainLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new Dictionary<string, ColumnConfigModel>();
            OptionChainGrid.GroupRowExpanded += GroupExpanded;
        }

        private void GroupExpanded(object sender, RowEventArgs e)
        {
            if (e.Row is OptionChainItemModel first)
            {
                var expiration = first.Expiration;
                OptionChainGrid.CurrentItem = _dataContext.Options.Where(x => x.Expiration == expiration)
                    .MinBy(x => Math.Abs(_dataContext.UnderlyingQuoteSubscriber.Mark - x.Strike));
            }
        }

        private void OptionChainView_Closed(object sender, EventArgs e)
        {
            OptionChainViewModel dataContext = (OptionChainViewModel)DataContext;
            dataContext.Dispose();
        }

        private void ComboBoxEdit_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            string newValue = (string)e.NewValue;
            if (string.IsNullOrEmpty(newValue))
            {
                newValue = "Expiration";
                GroupByComboBox.SelectedItem = newValue;
            }
            GroupBy(newValue);
        }

        private void GroupBy(string field)
        {
            if (OptionChainGrid.IsGrouped)
            {
                OptionChainGrid.ClearGrouping();
            }
            if (string.IsNullOrWhiteSpace(field))
            {
                field = "Expiration";
                GroupByComboBox.SelectedItem = field;
            }
            switch (field)
            {
                case "None":
                    break;
                default:
                    if (!string.IsNullOrEmpty(field))
                    {
                        OptionChainGrid.GroupBy(field, false);
                    }
                    break;
            }
        }

        internal void SelectGroup(DateTime expiration)
        {
            try
            {
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (!OptionChainGrid.IsGrouped)
                    {
                        GroupBy("Expiration");
                    }
                    OptionChainGrid.CollapseAllGroups();
                    for (int i = 0; i < OptionChainGrid.VisibleRowCount; i++)
                    {
                        int rowHandle = OptionChainGrid.GetRowHandleByVisibleIndex(i);
                        if (OptionChainGrid.IsGroupRowHandle(rowHandle))
                        {
                            if (OptionChainGrid.GetRow(rowHandle) is OptionChainItemModel row && row.Expiration == expiration)
                            {
                                OptionChainGrid.ExpandGroupRow(rowHandle);
                            }
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SelectGroup));
            }
        }

        private void TableView_ShowGridMenu(object sender, GridMenuEventArgs gridMenuEventArgs)
        {
            TableView tableView = sender as TableView;
            GridColumn column = (GridColumn)gridMenuEventArgs.MenuInfo.Column;
            if (gridMenuEventArgs.MenuType == GridMenuType.Column)
            {
                BarButtonItem removeColumnButton = GetRemoveColumnButton(column);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editColumnButton = GetEditColumnButton(column);
                gridMenuEventArgs.Customizations.Add(editColumnButton);
                gridMenuEventArgs.Customizations.Add(removeColumnButton);
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                BarButtonItem editGridButton = GetEditGridButton(sender as TableView);
                gridMenuEventArgs.Customizations.Add(editGridButton);
                BarButtonItem exportToExcelButton = GetExportToExcelButton();
                gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                gridMenuEventArgs.Customizations.Add(exportToExcelButton);
            }

            if (gridMenuEventArgs.MenuType is not GridMenuType.RowCell and not GridMenuType.GroupRow)
            {
                return;
            }

            TableViewHitInfo hitInfo = tableView.CalcHitInfo(Mouse.GetPosition(tableView));
            if (gridMenuEventArgs.MenuType == GridMenuType.GroupRow)
            {
                OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedBanList)
                    .ContinueWith(t =>
                    {
                        if (t.Result != null)
                        {
                            List<ConfigSave> userConfigs = t.Result.Where(x => string.Equals(OmsCore.User.Username, x.Username, StringComparison.OrdinalIgnoreCase)).ToList();

                            Dispatcher.Invoke(() =>
                            {
                                AddToGroupBlockListSubButton.Items.Clear();
                                RemoveFromGroupBlockListSubButton.Items.Clear();
                                foreach (ConfigSave config in userConfigs)
                                {
                                    BarButtonItem instanceButton = new()
                                    {
                                        Content = config.Title,
                                    };
                                    instanceButton.ItemClick += (o, e) => AddGroupToBanList(config);
                                    AddToGroupBlockListSubButton.Items.Add(instanceButton);
                                    BarButtonItem removeButton = new()
                                    {
                                        Content = config.Title,
                                    };
                                    removeButton.ItemClick += (o, e) => RemoveGroupFromBanList(config);
                                    RemoveFromGroupBlockListSubButton.Items.Add(removeButton);
                                }
                            });
                        }
                    });
            }
            else if (gridMenuEventArgs.MenuType == GridMenuType.RowCell)
            {
                OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedBanList)
                    .ContinueWith(t =>
                    {
                        if (t.Result != null)
                        {
                            List<ConfigSave> userConfigs = t.Result.Where(x => string.Equals(OmsCore.User.Username, x.Username, StringComparison.OrdinalIgnoreCase)).ToList();

                            Dispatcher.Invoke(() =>
                            {
                                AddToBlockListSubButton.Items.Clear();
                                RemoveFromBlockListSubButton.Items.Clear();
                                foreach (ConfigSave config in userConfigs)
                                {
                                    BarButtonItem instanceButton = new()
                                    {
                                        Content = config.Title,
                                    };
                                    instanceButton.ItemClick += (o, e) => AddItemToBanListAsync(config);
                                    AddToBlockListSubButton.Items.Add(instanceButton);
                                    BarButtonItem removeButton = new()
                                    {
                                        Content = config.Title,
                                    };
                                    removeButton.ItemClick += (o, e) => RemoveItemFromBanList(config);
                                    RemoveFromBlockListSubButton.Items.Add(removeButton);
                                }
                            });
                        }
                    });

                string fieldName = column.FieldName;
                if (fieldName.StartsWith("Call") || fieldName.StartsWith("Put"))
                {
                    OptionChainViewModel dataContext = (OptionChainViewModel)DataContext;
                    OptionChainItemModel item = (OptionChainItemModel)OptionChainGrid.SelectedItem;
                    Option option = fieldName.StartsWith("Call") ? item.CallOption : item.PutOption;
                    Dictionary<string, bool> validStrategiesPair = dataContext.GetValidStrategies(option);

                    BarSubItem buySubMenu = GetBarSubItem(option, validStrategiesPair.Select(x => Tuple.Create(x.Key, x.Value)).Take(10).ToList(), "BUY");
                    BarSubItem sellSubMenu = GetBarSubItem(option, validStrategiesPair.Select(x => Tuple.Create(x.Key, x.Value)).Take(10).ToList(), "SELL");

                    BarButtonItem deltaNeutralButton = new()
                    {
                        Content = "Delta Neutral",
                        IsEnabled = validStrategiesPair["Covered Stock"],
                        CommandParameter = Tuple.Create("Delta Neutral", "BUY", option),
                        Command = dataContext.LoadStrategyCommand,
                    };

                    BarButtonItem conversionButton = new()
                    {
                        Content = "Conversion",
                        IsEnabled = validStrategiesPair["Conversion"],
                        CommandParameter = Tuple.Create("Conversion", "SELL", option),
                        Command = dataContext.LoadStrategyCommand,
                    };

                    BarButtonItem reversalButton = new()
                    {
                        Content = "Reversal",
                        IsEnabled = validStrategiesPair["Reversal"],
                        CommandParameter = Tuple.Create("Reversal", "SELL", option),
                        Command = dataContext.LoadStrategyCommand,
                    };

                    BarButtonItem complexOrderTicketButton = new()
                    {
                        Content = "Complex Order Ticket",
                        CommandParameter = Tuple.Create("ComplexOrderTicket", "BUY", option),
                        Command = dataContext.LoadStrategyCommand,
                    };

                    BarButtonItem basketTraderButton = new()
                    {
                        Content = "Basket Trader",
                        CommandParameter = Tuple.Create("BasketTrader", "BUY", option),
                        Command = dataContext.LoadStrategyCommand,
                    };

                    gridMenuEventArgs.Customizations.Remove(AddToBlockListSubButton);
                    gridMenuEventArgs.Customizations.Remove(RemoveFromBlockListSubButton);
                    gridMenuEventArgs.Customizations.Add(buySubMenu);
                    gridMenuEventArgs.Customizations.Add(sellSubMenu);
                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(deltaNeutralButton);
                    gridMenuEventArgs.Customizations.Add(conversionButton);
                    gridMenuEventArgs.Customizations.Add(reversalButton);
                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(complexOrderTicketButton);
                    gridMenuEventArgs.Customizations.Add(basketTraderButton);
                    gridMenuEventArgs.Customizations.Add(new BarItemSeparator());
                    gridMenuEventArgs.Customizations.Add(AddToBlockListSubButton);
                    gridMenuEventArgs.Customizations.Add(RemoveFromBlockListSubButton);
                }
            }
        }

        private void AddGroupToBanList(ConfigSave config)
        {
            object selected = OptionChainGrid.SelectedItem;
        }

        private void RemoveGroupFromBanList(ConfigSave config)
        {
            object selected = OptionChainGrid.SelectedItem;
        }

        private async void AddItemToBanListAsync(ConfigSave config)
        {
            ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(config.Id);
            if (details != null)
            {
                BlockedSymbolModel model = JsonConvert.DeserializeObject<BlockedSymbolModel>(details.ConfigJson);
                model.Id = config.Id;
                model.Details = new()
                {
                    Id = config.Id,
                    OwnerId = config.OwnerId,
                    Username = config.Username,
                    SaveTime = config.SaveTime,
                    Module = config.Module,
                    ConfigJson = config.ConfigJson,
                    Title = config.Title,
                    Group = config.Group
                };
                System.Collections.IList selected = OptionChainGrid.SelectedItems;
                bool updated = false;
                foreach (object item in selected)
                {
                    if (item is OptionChainItemModel optionChainItem)
                    {
                        model.Symbols.Add(new BlockedSymbolModelItem()
                        {
                            Symbol = optionChainItem.CallSymbol
                        });
                        model.Symbols.Add(new BlockedSymbolModelItem()
                        {
                            Symbol = optionChainItem.PutSymbol
                        });
                        updated = true;
                    }
                }

                if (updated)
                {
                    await Task.Run(() =>
                    {
                        model.LastUpdateTime = DateTime.Now;
                        ConfigSave configSave = new()
                        {
                            Id = model.Details.Id,
                            OwnerId = model.Details.OwnerId,
                            Username = model.Details.Username,
                            SaveTime = model.Details.SaveTime,
                            Module = model.Details.Module,
                            ConfigJson = model.Details.ConfigJson,
                            Title = model.Details.Title,
                            Group = model.Details.Group
                        };
                        configSave.Title = model.Title;
                        configSave.ConfigJson = model.GetAsJson();
                        configSave.SaveTime = DateTime.Now;
                        OmsCore.GatewayClient.SaveConfig(configSave);
                    });
                }
            }
        }

        private async void RemoveItemFromBanList(ConfigSave config)
        {
            ConfigSave details = await OmsCore.GatewayClient.RequestConfigDataAsync(config.Id);
            if (details != null)
            {
                BlockedSymbolModel model = JsonConvert.DeserializeObject<BlockedSymbolModel>(details.ConfigJson);
                model.Id = config.Id;
                model.Details = new()
                {
                    Id = config.Id,
                    OwnerId = config.OwnerId,
                    Username = config.Username,
                    SaveTime = config.SaveTime,
                    Module = config.Module,
                    ConfigJson = config.ConfigJson,
                    Title = config.Title,
                    Group = config.Group
                };
                System.Collections.IList selected = OptionChainGrid.SelectedItems;
                bool updated = false;
                foreach (object item in selected)
                {
                    if (item is OptionChainItemModel optionChainItem)
                    {
                        foreach (BlockedSymbolModelItem symbol in model.Symbols.ToList())
                        {
                            if (symbol.Symbol == optionChainItem.CallSymbol || symbol.Symbol == optionChainItem.PutSymbol)
                            {
                                model.Symbols.Remove(symbol);
                            }
                        }
                        updated = true;
                    }
                }

                if (updated)
                {
                    await Task.Run(() =>
                    {
                        model.LastUpdateTime = DateTime.Now;
                        ConfigSave configSave = new()
                        {
                            Id = model.Details.Id,
                            OwnerId = model.Details.OwnerId,
                            Username = model.Details.Username,
                            SaveTime = model.Details.SaveTime,
                            Module = model.Details.Module,
                            ConfigJson = model.Details.ConfigJson,
                            Title = model.Details.Title,
                            Group = model.Details.Group
                        };
                        configSave.Title = model.Title;
                        configSave.ConfigJson = model.GetAsJson();
                        configSave.SaveTime = DateTime.Now;
                        OmsCore.GatewayClient.SaveConfig(configSave);
                    });
                }
            }
        }

        private BarButtonItem GetExportToExcelButton()
        {
            BarButtonItem exportToExcelButton = new()
            {
                Glyph = WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(typeof(DXImages).Assembly, "SvgImages/XAF/Action_Export_ToXls.svg")),
                Content = "Export to Excel",
            };

            exportToExcelButton.ItemClick += ExportToExcel;
            return exportToExcelButton;
        }

        private void ExportToExcel(object sender, ItemClickEventArgs e)
        {
            try
            {
                TableView tableView = OptionChainTable;
                if (tableView != null)
                {
                    OptionChainViewModel viewModel = (OptionChainViewModel)DataContext;
                    ISaveFileDialogService saveFileDialogService = viewModel.SaveFileDialogService;
                    saveFileDialogService.DefaultExt = "xlsx";
                    saveFileDialogService.DefaultFileName = $"{viewModel.Underlying} Option Chain Export - {DateTime.Now:MM-dd-yyyy hh.mm}";
                    saveFileDialogService.Filter = "xlsx|*.xlsx";
                    bool dialogResult = saveFileDialogService.ShowDialog();
                    if (dialogResult)
                    {
                        string filePath = saveFileDialogService.GetFullFileName();
                        tableView.ExportToXlsx(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportToExcel));
            }
        }

        private static BarButtonItem GetEditColumnButton(GridColumn column)
        {
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
            return editColumnButton;
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

        private static BarButtonItem GetRemoveColumnButton(GridColumn column)
        {
            BarButtonItem removeColumnButton = new()
            {
                Content = "Hide This Column",
            };

            removeColumnButton.ItemClick += (object _, ItemClickEventArgs itemClickEventArgs) => { column.Visible = false; };
            return removeColumnButton;
        }

        private BarSubItem GetBarSubItem(Option option, List<Tuple<string, bool>> validStrategiesPair, string side)
        {
            OptionChainViewModel dataContext = (OptionChainViewModel)DataContext;
            BarSubItem buySubMenu = new()
            {
                Content = side
            };

            foreach (Tuple<string, bool> strategy in validStrategiesPair)
            {
                string strategyName = strategy.Item1;
                bool strategyEnabled = strategy.Item2;

                BarButtonItem strategyButton = new()
                {
                    Content = strategyName,
                    IsEnabled = strategyEnabled,
                    CommandParameter = Tuple.Create(strategyName, side, option),
                    Command = dataContext.LoadStrategyCommand,
                };
                buySubMenu.Items.Add(strategyButton);
            }

            return buySubMenu;
        }

        private void TableView_StartRecordDrag(object sender, StartRecordDragEventArgs e)
        {
            TableView tableView = sender as TableView;
            TableViewHitInfo hitInfo = tableView.CalcHitInfo(Mouse.GetPosition(tableView));
            GridColumn column = hitInfo.Column;
            if (column == null)
            {
                e.Handled = true;
                return;
            }
            else
            {
                string fieldName = column.FieldName;
                if (fieldName.StartsWith("Call") || fieldName.StartsWith("Put"))
                {
                    if (e.Records[0] is OptionChainItemModel model)
                    {
                        Option option = fieldName.StartsWith("Call") ? model.CallOption : model.PutOption;
                        OmsOrder order = new()
                        {
                            Symbol = option.OptionSymbol,
                            UnderlyingSymbol = option.UnderlyingSymbol
                        };
                        List<OmsOrder> orders = new()
                        {
                            order
                        };
                        e.Data.SetData(DataFormats.Serializable, orders);
                    }
                }
                else
                {
                    e.Handled = true;
                }
            }
        }

        private void OnCompleteRecordDragDrop(object sender, CompleteRecordDragDropEventArgs e)
        {
            e.Handled = true;
        }

        private void OnZoomMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OmsCore.Config.ProgressiveZoomEnabled &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                 Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                OptionChainGridScale.Value += (e.Delta > 0) ? 0.1 : -0.1;
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
            _dataContext = (OptionChainViewModel)DataContext;
            _dataContext.Uid = Uid;
            _ = _dataContext.LoadViewModelConfigAsync(Uid);
            if (!_layoutRestored)
            {
                GroupBy("Expiration");
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
                if (DataContext is OptionChainViewModel viewModel)
                {
                    viewModel.ModuleTitle = configSave.Title + (configSave.Title != MODULE_NAME ? " - " + MODULE_NAME : "");;
                }
            }
        }

        private string GetConfigAsJson(bool isDefault = false)
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(UnderlyingInfoGrid)] = Helper.LayoutHelper.GetLayoutAsString(UnderlyingInfoGrid),
                [nameof(OptionChainGrid)] = Helper.LayoutHelper.GetLayoutAsString(OptionChainGrid),
                [nameof(GroupByComboBox)] = GroupByComboBox.SelectedItem?.ToString(),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
                [nameof(GridFieldNameToConfigMap)] = JsonConvert.SerializeObject(GridFieldNameToConfigMap),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        private async Task LoadConfigFromJsonAsync(string configJson)
        {
            try
            {
                _layoutRestored = true;
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }

                Dictionary<string, string> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));
                Helper.LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(OptionChainGrid)], OptionChainGrid);
                Helper.LayoutHelper.RestoreLayoutFromString(configDictionary[nameof(UnderlyingInfoGrid)], UnderlyingInfoGrid);
                GroupByComboBox.SelectedItem = configDictionary[nameof(GroupByComboBox)];
                GroupBy(configDictionary[nameof(GroupByComboBox)]);
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
                    tableCustomizationViewModel.Load(OptionChainGrid, GridFieldNameToConfigMap);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        public HashSet<T> GetVisibleItems<T>()
        {
            HashSet<T> list = null;
            ScrollViewer scrollViewer = LayoutTreeHelper.GetVisualChildren(OptionChainTable).OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer != null)
            {
                int bottomIndex = Convert.ToInt32(scrollViewer.ViewportHeight + scrollViewer.VerticalOffset);

                for (int i = OptionChainGrid.View.TopRowIndex; i < bottomIndex; i++)
                {
                    int handle = OptionChainGrid.GetRowHandleByVisibleIndex(i);
                    if (!OptionChainGrid.IsValidRowHandle(handle))
                        continue;
                    object item = OptionChainGrid.GetRow(handle);
                    if (item is not null and T itemT)
                    {
                        list ??= new HashSet<T>();
                        list.Add(itemT);
                    }
                }
            }
            return list;
        }

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly)
        {
            List<Tuple<int, object>> list = new();

            if (renderedOnly)
            {
                ScrollViewer scrollViewer = LayoutTreeHelper.GetVisualChildren(OptionChainTable).OfType<ScrollViewer>().FirstOrDefault();
                if (scrollViewer != null)
                {
                    int bottomIndex = Convert.ToInt32(scrollViewer.ViewportHeight + scrollViewer.VerticalOffset);

                    for (int i = OptionChainGrid.View.TopRowIndex; i < bottomIndex; i++)
                    {
                        int handle = OptionChainGrid.GetRowHandleByVisibleIndex(i);
                        if (!OptionChainGrid.IsValidRowHandle(handle))
                            continue;
                        object item = OptionChainGrid.GetRow(handle);
                        if (item != null)
                        {
                            list.Add(Tuple.Create(i + 1, item));
                        }
                    }
                }
            }
            else
            {

                for (int i = 0; i < OptionChainGrid.VisibleRowCount; i++)
                {
                    int rowHandle = OptionChainGrid.GetRowHandleByVisibleIndex(i);
                    list.Add(Tuple.Create(i + 1, OptionChainGrid.GetRow(rowHandle)));
                }
                if (startFromSelectedRow && OptionChainGrid.SelectedItem != null)
                {
                    Tuple<int, object> selectedRow = list.FirstOrDefault(x => x.Item2 == OptionChainGrid.SelectedItem);
                    if (selectedRow != null)
                    {
                        list = list.Skip(list.IndexOf(selectedRow)).ToList();
                    }
                }
            }
            return list;
        }

        public bool ItemIsVisible(object item)
        {
            for (int i = 0; i < OptionChainGrid.VisibleRowCount; i++)
            {
                int rowHandle = OptionChainGrid.GetRowHandleByVisibleIndex(i);
                object check = OptionChainGrid.GetRow(rowHandle);
                if (check == item)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _dataContext?.UpdateSubscription();
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
