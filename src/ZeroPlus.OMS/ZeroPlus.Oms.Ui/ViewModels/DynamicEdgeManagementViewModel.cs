using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DynamicEdgeManagementViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public Helper.IAutomationTrader ParentBasket { get; internal set; }
        public FastObservableCollection<DynamicEdgeModel> Models { get; }

        [Bindable]
        public partial DynamicEdgeModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public DynamicEdgeManagementViewModel()
        {
            Models = new FastObservableCollection<DynamicEdgeModel>();
            RefreshCommand();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.DynamicEdgeConfigs);
                if (configs != null)
                {
                    List<DynamicEdgeModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            DynamicEdgeModel model = JsonConvert.DeserializeObject<DynamicEdgeModel>(details.ConfigJson);
                            model.Id = config.Id;
                            model.Details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(config));
                            model.Load();
                            models.Add(model);
                        }
                    }

                    Models.Clear();
                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                        AutomationConfigModel automationConfigModel = ParentBasket.GetAutomationConfig();
                        CurrentModel = automationConfigModel != null ? Models.Where(x => x.Id == automationConfigModel.DynamicEdgeModelId).FirstOrDefault() : null;
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
                DynamicEdgeConfigView view = new();
                if (view.DataContext is DynamicEdgeConfigViewModel viewModel)
                {
                    viewModel.Loader = Load;
                    viewModel.Model = new DynamicEdgeModel()
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
                if (ParentBasket.GetAutomationConfig() == null)
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
        public void EditCommand(DynamicEdgeModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    DynamicEdgeConfigView view = new();
                    if (view.DataContext is DynamicEdgeConfigViewModel viewModel)
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
        public void CloneCommand(DynamicEdgeModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    DynamicEdgeModel model = new()
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
        public void DeleteCommand(DynamicEdgeModel selectedModel)
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

        public void Load(DynamicEdgeModel currentModel)
        {
            try
            {
                currentModel.Load();
                AutomationConfigModel automationConfigModel = ParentBasket.GetAutomationConfig();
                automationConfigModel.DynamicEdgeModelId = currentModel != null ? currentModel.Id : 0;
                automationConfigModel.DynamicEdgeModel = currentModel;
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
