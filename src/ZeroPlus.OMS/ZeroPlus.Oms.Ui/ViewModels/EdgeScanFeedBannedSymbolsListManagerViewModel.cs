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
    public partial class EdgeScanFeedBannedSymbolsListManagerViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public Module ModuleId { get; } = Module.EdgeScanFeedBanList;
        public EdgeScanFeedViewModel Parent { get; internal set; }
        public FastObservableCollection<BlockedSymbolModel> Models { get; }

        [Bindable]
        public partial BlockedSymbolModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public EdgeScanFeedBannedSymbolsListManagerViewModel()
        {
            Models = new FastObservableCollection<BlockedSymbolModel>();
            RefreshCommand();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)ModuleId);
                if (configs != null)
                {
                    List<BlockedSymbolModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
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
                            models.Add(model);
                        }
                    }

                    Models.Clear();
                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                        CurrentModel = Parent.BlockedSymbolModel != null ? Models.Where(x => x.Id == Parent.BlockedSymbolModelId).FirstOrDefault() : null;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RefreshCommand));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public void AddCommand()
        {
            try
            {
                BannedSymbolsListView view = new();
                if (view.DataContext is BannedSymbolsListViewModel viewModel)
                {
                    view.Closed += (s, e) => RefreshCommand();
                    viewModel.Loader = Load;
                    viewModel.Model = new BlockedSymbolModel()
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };
                }
                view.Show();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddCommand));
            }
        }

        [Command]
        public void LoadCommand()
        {
            try
            {
                Load(CurrentModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCommand));
            }
        }

        [Command]
        public void ClearCommand()
        {
            try
            {
                Load(null);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearCommand));
            }
        }

        [Command]
        public void EditCommand(BlockedSymbolModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    BannedSymbolsListView view = new();
                    if (view.DataContext is BannedSymbolsListViewModel viewModel)
                    {
                        view.Closed += (s, e) => RefreshCommand();
                        viewModel.Model = selectedModel;
                        viewModel.Loader = Load;
                    }
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditCommand));
            }
        }

        [Command]
        public void CloneCommand(BlockedSymbolModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    BlockedSymbolModel model = new()
                    {
                        Creator = OmsCore.User.Username,
                    };
                    model.CloneFrom(selectedModel);
                    model.Title += " Clone";
                    EditCommand(model);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloneCommand));
            }
        }

        [Command]
        public void DeleteCommand(BlockedSymbolModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
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

                    RefreshCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeleteCommand));
            }
        }

        public void Load(BlockedSymbolModel currentModel)
        {
            try
            {
                if (currentModel != null)
                {
                    currentModel.SymbolsSet = currentModel.Symbols.Select(x => x.Symbol).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
                }
                Parent.BlockedSymbolModelId = currentModel != null ? currentModel.Id : 0;
                Parent.BlockedSymbolModel = currentModel;
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
