using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LoopSizeupManagementViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public Helper.IAutomationTrader AutomationTrader { get; internal set; }
        public FastObservableCollection<DynamicSizeupModel> Models { get; }

        [Bindable]
        public partial DynamicSizeupModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public LoopSizeupManagementViewModel()
        {
            Models = new FastObservableCollection<DynamicSizeupModel>();
            RefreshCommand();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.SizeupConfigs);
                if (configs != null)
                {
                    List<DynamicSizeupModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            DynamicSizeupModel model = JsonConvert.DeserializeObject<DynamicSizeupModel>(details.ConfigJson);
                            model.Id = config.Id;
                            model.Details = config;
                            model.SizeUpConfigs = model.SizeUpConfigs.OrderByDescending(x => x.Edge).ThenByDescending(x => x.Size).ToObservableCollection();
                            models.Add(model);
                        }
                    }

                    Models.Clear();
                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                        CurrentModel = Models.FirstOrDefault(x => x.Id == AutomationTrader.GetAutomationConfig()?.SizeupConfig?.Id);
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
                LoopSizeupView view = new();
                if (view.DataContext is LoopSizeupViewModel viewModel)
                {
                    viewModel.Model = new DynamicSizeupModel()
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };
                    viewModel.Loader = Load;
                }
                view.ShowDialog();
                RefreshCommand();
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
                if (AutomationTrader.GetAutomationConfig() == null)
                {
                    _log.Error(nameof(LoadCommand) + "Basket Automation Config not loaded.");
                    MessageBoxService.ShowMessage("Select Automation Config First.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                }
                else
                {
                    Load(CurrentModel);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCommand));
            }
        }

        private void Load(DynamicSizeupModel currentModel)
        {
            var automationConfigModel = AutomationTrader.GetAutomationConfig();
            automationConfigModel.SizeupConfigId = currentModel?.Id ?? 0;
            automationConfigModel.SizeupConfig = currentModel;
            CurrentWindowService?.Close();
        }

        [Command]
        public void EditCommand(DynamicSizeupModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    LoopSizeupView view = new();
                    if (view.DataContext is LoopSizeupViewModel viewModel)
                    {
                        viewModel.Model = selectedModel;
                        viewModel.Loader = Load;
                    }
                    view.ShowDialog();
                    RefreshCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditCommand));
            }
        }

        [Command]
        public void CloneCommand(DynamicSizeupModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    DynamicSizeupModel model = new()
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
        public void DeleteCommand(DynamicSizeupModel selectedModel)
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
    }
}
