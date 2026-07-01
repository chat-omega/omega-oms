using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
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
    public partial class DynamicIntervalManagementViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public Helper.IAutomationTrader AutomationTrader { get; internal set; }
        public FastObservableCollection<DynamicIntervalModel> Models { get; }

        [Bindable]
        public partial DynamicIntervalModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public DynamicIntervalManagementViewModel()
        {
            Models = new FastObservableCollection<DynamicIntervalModel>();
            RefreshCommand();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.DynamicIntervalConfigs);
                if (configs != null)
                {
                    List<DynamicIntervalModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            DynamicIntervalModel model = JsonConvert.DeserializeObject<DynamicIntervalModel>(details.ConfigJson);
                            model.Id = config.Id;
                            model.Details = config;
                            models.Add(model);
                        }
                    }

                    Models.Clear();
                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                        AutomationConfigModel automationConfigModel = AutomationTrader.GetAutomationConfig();
                        CurrentModel = automationConfigModel != null ? Models.Where(x => x.Id == automationConfigModel.DynamicIntervalModelId).FirstOrDefault() : null;
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
                DynamicIntervalConfigView view = new();
                if (view.DataContext is DynamicIntervalConfigViewModel viewModel)
                {
                    viewModel.Loader = Load;
                    viewModel.Model = new DynamicIntervalModel()
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };
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

        [Command]
        public void EditCommand(DynamicIntervalModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    DynamicIntervalConfigView view = new();
                    if (view.DataContext is DynamicIntervalConfigViewModel viewModel)
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
        public void CloneCommand(DynamicIntervalModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    DynamicIntervalModel model = new()
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
        public void DeleteCommand(DynamicIntervalModel selectedModel)
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

        public void Load(DynamicIntervalModel currentModel)
        {
            try
            {
                AutomationConfigModel automationConfigModel = AutomationTrader.GetAutomationConfig();
                automationConfigModel.DynamicIntervalModelId = currentModel != null ? currentModel.Id : 0;
                automationConfigModel.DynamicIntervalModel = currentModel;
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
