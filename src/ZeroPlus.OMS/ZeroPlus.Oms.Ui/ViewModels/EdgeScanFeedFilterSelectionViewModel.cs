using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EdgeScanFeedFilterSelectionViewModel : CustomizableTableViewModelBase
    {
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();


        [Bindable]
        public partial EdgeScanFeedTradeFilterModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public EdgeScanFeedViewModel EdgeScanFeed { get; set; }
        public FastObservableCollection<EdgeScanFeedTradeFilterModel> EdgeScanFeedFilterModels { get; }

        public EdgeScanFeedFilterSelectionViewModel()
        {
            EdgeScanFeedFilterModels = new FastObservableCollection<EdgeScanFeedTradeFilterModel>();
            RefreshFiltersCommand();
        }

        [Command]
        public async void RefreshFiltersCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.EdgeScanFeedFilter);
                if (configs != null)
                {
                    List<EdgeScanFeedTradeFilterModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            EdgeScanFeedTradeFilterModel model = JsonConvert.DeserializeObject<EdgeScanFeedTradeFilterModel>(details.ConfigJson);
                            model.NormalizeAfterLoad();
                            model.Id = config.Id;
                            model.Details = new ZeroPlus.Models.Data.Configs.ConfigSave()
                            {
                                Id = config.Id,
                                OwnerId = config.OwnerId,
                                Username = config.Username,
                                SaveTime = config.SaveTime,
                                Module = config.Module,
                                ConfigJson = config.ConfigJson,
                                Title = config.Title,
                                Group = config.Group,
                            };
                            models.Add(model);
                        }
                    }

                    EdgeScanFeedFilterModels.Clear();
                    if (models.Count > 0)
                    {
                        EdgeScanFeedFilterModels.AddRange(models);
                        CurrentModel = EdgeScanFeed.SelectedModel != null ? EdgeScanFeedFilterModels.Where(x => x.Id == EdgeScanFeed.SelectedModel.Id).FirstOrDefault() : null;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RefreshFiltersCommand));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public void AddFilterCommand()
        {
            try
            {
                EdgeScanFeedTradeFilterView view = new();
                if (view.DataContext is EdgeScanFeedTradeFilterViewModel viewModel)
                {
                    viewModel.Model = new EdgeScanFeedTradeFilterModel()
                    {
                        Creator = OmsCore.User.Username,
                    };
                    viewModel.Loader = Load;
                }
                view.ShowDialog();
                RefreshFiltersCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddFilterCommand));
            }
        }

        [Command]
        public void LoadFilterCommand()
        {
            try
            {
                Load(CurrentModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFilterCommand));
            }
        }

        [Command]
        public void EditFilterCommand(EdgeScanFeedTradeFilterModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    EdgeScanFeedTradeFilterView view = new();
                    if (view.DataContext is EdgeScanFeedTradeFilterViewModel viewModel)
                    {
                        viewModel.Model = selectedModel;
                        viewModel.Loader = Load;
                    }
                    view.ShowDialog();
                    RefreshFiltersCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditFilterCommand));
            }
        }

        [Command]
        public void CloneFilterCommand(EdgeScanFeedTradeFilterModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    EdgeScanFeedTradeFilterModel model = new()
                    {
                        Creator = OmsCore.User.Username,
                    };
                    model.CloneFrom(selectedModel);
                    model.Title += " Clone";
                    EditFilterCommand(model);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloneFilterCommand));
            }
        }

        [Command]
        public void DeleteFilterCommand(EdgeScanFeedTradeFilterModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    bool reset = selectedModel == EdgeScanFeed.SelectedModel;

                    if (selectedModel.Details != null && selectedModel.Details.OwnerId != OmsCore.User.ID)
                    {
                        MessageBoxService.ShowMessage("You do not have permission to delete this config!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                    }
                    else
                    {
                        MessageResult response = MessageBoxService.ShowMessage("Are you sure you want to delete " + selectedModel.Title + "?", "ZeroPlus OMS", MessageButton.YesNo, MessageIcon.Warning);
                        if (response == MessageResult.Yes)
                        {
                            if (selectedModel.Details != null && selectedModel.Details.OwnerId == OmsCore.User.ID)
                            {
                                _ = OmsCore.GatewayClient.DeleteConfigAsync(selectedModel.Details.Id);
                            }
                        }
                    }

                    if (reset)
                    {
                        EdgeScanFeed.LoadFilterModel(null);
                    }

                    RefreshFiltersCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeleteFilterCommand));
            }
        }

        private void Load(EdgeScanFeedTradeFilterModel currentModel)
        {
            try
            {
                EdgeScanFeed.LoadFilterModel(currentModel);
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
