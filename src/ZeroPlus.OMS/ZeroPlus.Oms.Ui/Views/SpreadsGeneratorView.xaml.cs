using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;
using GridLengthConverter = System.Windows.GridLengthConverter;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SpreadsGeneratorView.xaml
    /// </summary>
    public partial class SpreadsGeneratorView
    {
        private const Module MODULE = Module.SpreadsGenerator;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _draggingExportText;
        private Point _exportTextDragStartPos;
        private readonly OmsCore _omsCore;

        public SpreadsGeneratorView(OmsCore omsCore, IModuleFactory moduleFactory, string uid = null, bool loadDefault = true) : base(MODULE, uid, moduleFactory, loadDefault)
        {
            _omsCore = omsCore;
            InitializeComponent();
            SetQuickAccessWidth();
        }

        protected override void OnModuleLoaded()
        {
            base.OnModuleLoaded();
            _omsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTraderLayout)
                        .ContinueWith(t =>
                        {
                            if (t != null && t.Result != null)
                            {
                                List<ConfigSave> userConfigs = t.Result.Where(x => string.Equals(_omsCore.User.Username, x.Username, StringComparison.OrdinalIgnoreCase)).ToList();

                                Dispatcher.BeginInvoke(() =>
                                {
                                    OpenInBasketSubButton.Items.Clear();
                                    BarButtonItem openInLockBasketButton = new()
                                    {
                                        Content = "Open in Lock Trader",
                                        Command = ((SpreadsGeneratorViewModel)DataContext).OpenInLockTraderCommand,
                                    };
                                    OpenInBasketSubButton.Items.Add(openInLockBasketButton);
                                    OpenInBasketSubButton.Items.Add(new BarItemSeparator());
                                    BarButtonItem openInDominatorButton = new()
                                    {
                                        Content = "Open in Dominator",
                                        Command = ((SpreadsGeneratorViewModel)DataContext).OpenInDominatorCommand,
                                    };
                                    OpenInBasketSubButton.Items.Add(openInDominatorButton);
                                    OpenInBasketSubButton.Items.Add(new BarItemSeparator());
                                    foreach (ConfigSave config in userConfigs)
                                    {
                                        BarButtonItem instanceButton = new()
                                        {
                                            Content = config.Title,
                                            CommandParameter = config,
                                            Command = ((SpreadsGeneratorViewModel)DataContext).OpenInBasketTraderWithConfigCommand,
                                        };
                                        OpenInBasketSubButton.Items.Add(instanceButton);
                                    }
                                });
                            }
                        });
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
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0")!;
                ExpandCollapseGridButton.Content = 4;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("750")!;
                ExpandCollapseGridButton.Content = 3;
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

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            SpreadsGeneratorViewModel dataContext = (SpreadsGeneratorViewModel)DataContext;
            GridLengthConverter glc = new();
            string splitterHeight = glc.ConvertToString(GridSplitterCol.Width)?.Replace("*", "");

            Dictionary<string, string> configDictionary = new()
            {
                [nameof(GridSplitterCol)] = splitterHeight,
                [nameof(SpreadsGeneratorConfig)] = dataContext.GetConfigSerialized(),
                [nameof(WindowSetting)] = new WindowSetting(this, isDefault).SerializeToJson(),
            };

            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }

                SpreadsGeneratorViewModel dataContext = (SpreadsGeneratorViewModel)DataContext;
                await dataContext.LoadConfigFromJsonAsync(configJson);

                GridLengthConverter glc = new();
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0")!;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        public override void ClearFiltersClick()
        {
            UnderlyingsGrid.FilterCriteria = null;
            UnderlyingsGrid.FilterString = string.Empty;
        }

        public override void ClearSortingClick()
        {
            UnderlyingsGrid.ClearSorting();
        }

        private void ExportText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            TextBlock text = (TextBlock)sender;
            _draggingExportText = false;
            _exportTextDragStartPos = e.GetPosition(text);
        }

        private void ExportText_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            TextBlock text = (TextBlock)sender;
            Point currentPos = e.GetPosition(text);
            text.ReleaseMouseCapture();
            Vector delta = currentPos - _exportTextDragStartPos;
            if ((delta.Length > 10.0 || _draggingExportText) && e.LeftButton == MouseButtonState.Pressed)
            {
                _draggingExportText = true;
                if (DataContext is SpreadsGeneratorViewModel viewModel)
                {
                    List<SpreadGeneratorResults> orders = viewModel.LatestSpreadGeneratorResults.ToList();
                    DataObject dataObject = new(DataFormats.Serializable, orders);
                    DragDrop.DoDragDrop(text, dataObject, DragDropEffects.Move);
                }
            }
        }

        private void ExportText_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            TextBlock text = sender as TextBlock;
            text.ReleaseMouseCapture();
            if (_draggingExportText)
            {
                _draggingExportText = false;
            }
        }

        private void AutoSuggestEdit_QuerySubmitted(object sender, AutoSuggestEditQuerySubmittedEventArgs e)
        {
            if (sender is AutoSuggestEdit suggestEdit)
            {
                var viewModel = (ViewModel as SpreadsGeneratorViewModel)!;
                if (string.IsNullOrWhiteSpace(e.Text) || viewModel.Options == null || viewModel.Options.Count == 0)
                {
                    suggestEdit.ItemsSource = null;
                    suggestEdit.ClosePopup();
                }
                else
                {
                    List<string> match = viewModel.Options.Where(x => x.Contains(e.Text.ToUpper())).ToList();
                    suggestEdit.ItemsSource = match.Count() > 20 ? match.Take(20).ToArray() : match.ToArray();
                    if (match.Any())
                    {
                        suggestEdit.ShowPopup();
                    }
                }
            }
        }
    }
}