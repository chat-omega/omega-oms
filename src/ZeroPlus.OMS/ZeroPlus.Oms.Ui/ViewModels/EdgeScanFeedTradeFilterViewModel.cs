using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EdgeScanFeedTradeFilterViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public TextWrapping HeaderTextWrapping => OmsCore.Config.WrapColumnHeaderV2 ? TextWrapping.WrapWithOverflow : TextWrapping.NoWrap;
        public Action<EdgeScanFeedTradeFilterModel> Loader { get; internal set; }

        [Bindable]
        public partial EdgeScanFeedTradeFilterModel Model { get; set; }

        [Bindable]
        public partial bool TableMode { get; set; }

        [Command]
        public void ExportCommand()
        {
            try
            {
                if (Model != null)
                {
                    SaveFileDialogService.DefaultExt = "csv";
                    SaveFileDialogService.DefaultFileName = $"Edge Scan Feed - {Model?.Title ?? "Config"} - {DateTime.Now:MM-dd-yyyy hh.mm}";
                    SaveFileDialogService.Filter = "csv|*.csv";
                    bool dialogResult = SaveFileDialogService.ShowDialog();
                    if (dialogResult)
                    {
                        string filePath = SaveFileDialogService.GetFullFileName();
                        string csv = "Scanner Type," +
                                     "Legs," +
                                     "Edge To Theo Enabled," +
                                     "Edge To Theo," +
                                     "Min % Bid Enabled," +
                                     "Min % Bid," +
                                     "Max % Bid Enabled," +
                                     "Max % Bid," +
                                     "Min Bid Enabled," +
                                     "Min Bid," +
                                     "Min Qty," +
                                     "Max Qty," +
                                     "Loop TimeSpan," +
                                     "Loop Interval," +
                                     "Loop Count," +
                                     "Mkt Width Range," +
                                     "Px Range," +
                                     "Edge Range," +
                                     "^ Adj Edge Range," +
                                     "UL Blocked," +
                                     "UL Allow," +
                                     "UL Range," +
                                     "UL Width," +
                                     "Max Time Delay," +
                                     "Dte Range," +
                                     "Allow Uncertain," +
                                     "Allow Qty Mismatch," +
                                     "Max Change In UL Enabled," +
                                     "Max Change In UL," +
                                     "Delta Range," +
                                     "Near Exp Range," +
                                     "Far Exp Range," +
                                     "Conditions," +
                                     "Strategies\r\n";
                        foreach (EdgeScanFeedTradeFilterRowModel filter in Model.Filters)
                        {
                            csv += $"{string.Join(";", filter.SelectedEdgeFeedScanners)}," +
                                   $"{string.Join(";", filter.SelectedLegTypes)}," +
                                   $"{filter.MinEdgeToTheoEnabled}," +
                                   $"{filter.MinEdgeToTheo}," +
                                   $"{filter.MinBidPercentEnabled}," +
                                   $"{filter.MinBidPercent}," +
                                   $"{filter.MaxBidPercentEnabled}," +
                                   $"{filter.MaxBidPercent}," +
                                   $"{filter.MinBidEnabled}," +
                                   $"{filter.MinBid}," +
                                   $"{filter.MinQty}," +
                                   $"{filter.MaxQty}," +
                                   $"{filter.LoopTimeSpan}," +
                                   $"{filter.LoopInterval}," +
                                   $"{filter.MinLoopCount}," +
                                   $"{filter.MinMarketWidth + " - " + filter.MaxMarketWidth}," +
                                   $"{filter.MinPrice + " - " + filter.MaxPrice}," +
                                   $"{filter.MinEdge + " - " + filter.MaxEdge}," +
                                   $"{filter.MinDeltaAdjEdge + " - " + filter.MaxDeltaAdjEdge}," +
                                   $"{filter.BlockedUnderlyings.Replace(",", ";")}," +
                                   $"{filter.AllowUnderlyings.Replace(",", ";")}," +
                                   $"{filter.MinUnderlying + " - " + filter.MaxUnderlying}," +
                                   $"{filter.UnderlyingWidth}," +
                                   $"{filter.MaxTimeDelay}," +
                                   $"{filter.MinDte + " - " + filter.MaxDte}," +
                                   $"{filter.AllowUncertain}," +
                                   $"{filter.AllowQtyMismatch}," +
                                   $"{filter.MaxChangeInUnderlyingEnabled}," +
                                   $"{filter.MaxChangeInUnderlying}," +
                                   $"{filter.MinDelta + " - " + filter.MaxDelta}," +
                                   $"{filter.MinNearExpirationFilter + " - " + filter.MaxNearExpirationFilter}," +
                                   $"{filter.MinFarExpirationFilter + " - " + filter.MaxFarExpirationFilter}," +
                                   $"{string.Join(";", filter.SelectedTradeConditionCodes)}," +
                                   $"{string.Join(";", filter.Strategies.Where(x => x.IsChecked).Select(x => x.Name))}," +
                                   $"\r\n";
                        }

                        File.WriteAllText(filePath, csv);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportCommand));
            }
        }

        [Command]
        public void AddFilterCommand()
        {
            EdgeScanFeedTradeFilterRowModel item = new();
            item.InitializeAllStrategies();
            item.SelectedLegTypes = LegTypes.All;

            item.EdgeRangeEnabled = false;
            item.MinEdgeToTheoEnabled = false;
            item.MinBidPercentEnabled = false;
            item.MaxBidPercentEnabled = false;
            item.DeltaAdjEdgeRangeEnabled = false;

            item.BlockedExpirationInput = DateTime.Today;
            item.MinNearExpirationFilter = DateTime.Today;
            item.MaxNearExpirationFilter = DateTime.Today + TimeSpan.FromDays(2191.5);
            item.MinFarExpirationFilter = DateTime.Today;
            item.MaxFarExpirationFilter = DateTime.Today + TimeSpan.FromDays(2191.5);

            item.MinQty = 0;
            item.MaxQty = 0;
            item.MinUnderlying = 0;
            item.MaxUnderlying = 0;
            item.MinDelta = 0;
            item.MaxDelta = 0;
            item.MinMarketWidth = 0;
            item.MaxMarketWidth = 0;
            item.MaxTimeDelay = 0;
            item.UnderlyingWidth = 0;
            item.MinLoopCount = 0;
            item.AllowUncertain = true;
            item.Header = "Filter " + (Model.Filters.Count + 1);

            Model.Filters.Add(item);
        }

        [Command]
        public async void SaveFilterCommand()
        {
            try
            {
                if (await Save())
                {
                    CurrentWindowService?.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public async void SaveAndLoadCommand()
        {
            try
            {
                if (await Save())
                {
                    Loader?.Invoke(Model);
                    CurrentWindowService?.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task<bool> Save()
        {
            if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.ShowMessage("Title can not be empty.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (Model.Filters == null || Model.Filters.Count == 0)
            {
                MessageBoxService.ShowMessage("Filters can not be empty.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else
            {
                Model.Normalize();
                await SaveToServer();
                return true;
            }
        }

        private async Task SaveToServer()
        {
            try
            {
                if (Model.Details == null)
                {
                    List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedFilter);
                    ConfigSave sameConfig = config?.FirstOrDefault(x => x.Title == Model.Title);
                    bool save = false;

                    if (sameConfig != null)
                    {
                        if (sameConfig.OwnerId == OmsCore.User.ID)
                        {
                            MessageResult response = MessageBoxService.ShowMessage($"{Model.Title} already exists.\n" +
                                                                         $"Do you want to replace it?",
                                                                         $"ZeroPlus OMS",
                                                                         MessageButton.YesNo,
                                                                         MessageIcon.Warning);

                            if (response == MessageResult.Yes)
                            {
                                Model.Id = sameConfig.Id;
                                save = true;
                            }
                        }
                        else
                        {
                            MessageBoxService.ShowMessage($"{Model.Title} already exists.",
                                                         $"ZeroPlus OMS",
                                                         MessageButton.OK,
                                                         MessageIcon.Error);
                        }
                    }
                    else
                    {
                        save = true;
                    }

                    if (save)
                    {
                        ConfigSave configSave = new()
                        {
                            Id = Model.Id,
                            OwnerId = OmsCore.User.ID,
                            Username = OmsCore.User.Username,
                            Module = (int)Module.EdgeScanFeedFilter,
                            ConfigJson = Model.GetAsJson(),
                            Group = "",
                            SaveTime = DateTime.Now,
                            Title = Model.Title,
                        };

                        OmsCore.GatewayClient.SaveConfig(configSave);

                        MessageBoxService.ShowMessage($"{Model.Title} config saved.",
                                                      $"ZeroPlus OMS",
                                                      MessageButton.OK,
                                                      MessageIcon.Information);
                    }
                }
                else
                {
                    if (Model.Details.OwnerId == OmsCore.User.ID)
                    {
                        ConfigSave configSave = new();
                        if (Model.Details != null)
                        {
                            configSave.Id = Model.Details.Id;
                            configSave.OwnerId = Model.Details.OwnerId;
                            configSave.Username = Model.Details.Username;
                            configSave.SaveTime = Model.Details.SaveTime;
                            configSave.Module = Model.Details.Module;
                            configSave.ConfigJson = Model.Details.ConfigJson;
                            configSave.Title = Model.Details.Title;
                            configSave.Group = Model.Details.Group;
                        }
                        configSave.Title = Model.Title;
                        configSave.ConfigJson = Model.GetAsJson();
                        configSave.SaveTime = DateTime.Now;
                        OmsCore.GatewayClient.SaveConfig(configSave);
                    }
                    else
                    {
                        MessageBoxService.ShowMessage("You do not have permission to edit this config!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [Command]
        public void RemoveCommand(EdgeScanFeedTradeFilterRowModel model)
        {
            Model.Filters.Remove(model);
        }

        [Command]
        public void ShowStrategiesSelectorCommand(EdgeScanFeedTradeFilterRowModel model)
        {
            EdgeScanFeedFilterStrategySelectorView view = new();
            if (view.DataContext is EdgeScanFeedFilterStrategySelectorViewModel viewModel)
            {
                viewModel.Model = model;
                view.Show();
                view.Closed += (s, e) => model.UpdateMap();
            }
        }

        [Command]
        public void ShowBlockedExpirationsSelectorCommand(EdgeScanFeedTradeFilterRowModel model)
        {
            EdgeScanFeedFilterBlockedExpirationsConfigView view = new();
            if (view.DataContext is EdgeScanFeedFilterBlockedExpirationsConfigViewModel viewModel)
            {
                view.Show();
                view.Closed += (s, e) => model.UpdateMap();
            }
        }
    }
}
