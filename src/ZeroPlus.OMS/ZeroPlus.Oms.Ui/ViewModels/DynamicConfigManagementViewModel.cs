using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DynamicConfigManagementViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public OmsCore OmsCore { get; }

        private Module _configModule;

        public IDynamicConfigParentModule Parent { get; internal set; }
        public FastObservableCollection<IDynamicConfigModel> Models { get; }

        [Bindable]
        public partial IDynamicConfigModel CurrentModel { get; set; }
        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowLoadButton { get; set; }
        [Bindable]
        public partial string Title { get; set; }
        public Module ConfigModule
        {
            get => _configModule;
            set
            {
                SetValue(ref _configModule, value);
                Title = value.ToString().FromCamelCase();
            }
        }

        public DynamicConfigManagementViewModel(OmsCore omsCore)
        {
            OmsCore = omsCore;
            Models = new FastObservableCollection<IDynamicConfigModel>();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)ConfigModule);
                Models.Clear();
                if (configs != null)
                {
                    List<IDynamicConfigModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            IDynamicConfigModel model = Parent.GetDynamicConfig(ConfigModule, details.ConfigJson);
                            model.Id = config.Id;
                            string configJson = JsonConvert.SerializeObject(config);
                            model.Details = JsonConvert.DeserializeObject<ConfigSave>(configJson);
                            model.Load();
                            models.Add(model);
                        }
                    }

                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                        int currentId = Parent.GetCurrentConfigId(ConfigModule);
                        CurrentModel = currentId > 0 ? Models?.Where(x => x.Id == currentId).FirstOrDefault() : null;
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
                IDynamicConfigModel selectedModel = Parent.GetDynamicConfig(ConfigModule);
                Parent.EditDynamicConfig(ConfigModule, selectedModel);
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
                Load(CurrentModel);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCommand));
            }
        }

        [Command]
        public void EditCommand(IDynamicConfigModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    Parent.EditDynamicConfig(ConfigModule, selectedModel);
                    RefreshCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditCommand));
            }
        }

        [Command]
        public async void CloneCommand(IDynamicConfigModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(selectedModel.Id));
                    if (details != null)
                    {
                        IDynamicConfigModel model = Parent.GetDynamicConfig(ConfigModule, details.ConfigJson);
                        model.Creator = OmsCore.User.Username;
                        model.Id = 0;
                        model.Details = null;
                        model.Title += " Clone";
                        EditCommand(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloneCommand));
            }
        }

        [Command]
        public async void DeleteCommand(IDynamicConfigModel selectedModel)
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
                                var status = await OmsCore.GatewayClient.DeleteConfigAsync(selectedModel.Details.Id);
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

        public void Load(IDynamicConfigModel currentModel)
        {
            try
            {
                currentModel?.Load();
                Parent.LoadDynamicConfig(ConfigModule, currentModel);

                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
