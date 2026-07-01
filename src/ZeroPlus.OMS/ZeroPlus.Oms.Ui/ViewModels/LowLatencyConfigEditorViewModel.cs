using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void ConfigSavedHandler(Module module, IDynamicConfigModel model, bool isNew);
    public partial class LowLatencyConfigEditorViewModel : ViewModelBase
    {
        public event ConfigSavedHandler ConfigSavedEvent;
        public event ConfigDeletedHandler ConfigDeletedEvent;

        private readonly OmsCore _omsCore;


        public string OriginalTitle { get; set; }
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial Module Module { get; set; }
        [Bindable]
        public partial IDynamicConfigModel Model { get; set; }
        [Bindable]
        public partial ConfigBrowserViewModel ConfigBrowserViewModel { get; set; }
        [Bindable]
        public partial bool UsedByMultiple { get; set; }

        public LowLatencyConfigEditorViewModel(OmsCore omsCore, ConfigBrowserViewModel configBrowserViewModel)
        {
            _omsCore = omsCore;
            ConfigBrowserViewModel = configBrowserViewModel;
            configBrowserViewModel.ConfigDeletedEvent += OnConfigDeleteEvent;
        }

        [Command]
        public async void ConfigRowDoubleClick(NodeClickArgs args)
        {
            if (args != null && args.Item is Comms.Models.Data.Oms.Config.ConfigSave configSave)
            {
                var config = await _omsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                LoadConfig(config);
            }
        }

        [Command]
        public async void SaveCommand()
        {
            if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.Show("Title can not be empty!", Model.Title,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var isNew = OriginalTitle != Model.Title;
            if (isNew)
            {
                Model.Id = 0;
            }

            Model.Save();

            if (!isNew)
            {
                if (Model.Details != null &&
                    Model.Details.OwnerId > 0 &&
                    Model.Details.Id > 0 &&
                    Model.Details.OwnerId != _omsCore.User.ID)
                {
                    MessageBoxService.Show("You do not have permission to save this config!", Model.Title,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (UsedByMultiple)
                {
                    MessageBoxResult result = MessageBoxService.Show($"{Model.Title} is being used by multiple instances.\nAre you sure you want to save?", Model.Title,
                        MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            Comms.Models.Data.Oms.Config.ConfigSave config = new()
            {
                Id = Model.Id,
                Module = (int)Module,
                SaveTime = DateTime.Now,
                Username = _omsCore.User.Username,
                OwnerId = _omsCore.User.ID,
                ConfigJson = Model.Details?.ConfigJson,
                Title = Model.Title,
                Group = Model.Details?.Group ?? _omsCore.User.Username,
            };

            var configs = await _omsCore.GatewayClient.RequestConfigsAsync((int)Module);
            if (configs != null)
            {
                var thisConfig = configs.FirstOrDefault(x =>
                    x.Id != config.Id &&
                    x.Title == config.Title &&
                    x.Group == config.Group);
                if (thisConfig != null)
                {
                    MessageBoxService.Show("Config with same name already exists!", Model.Title,
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }

            _omsCore.GatewayClient.SaveConfig(config);
            if (Model.Id == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    configs = await _omsCore.GatewayClient.RequestConfigsAsync((int)Module);
                    Comms.Models.Data.Oms.Config.ConfigSave thisConfig = null;

                    for (var index = configs.Count - 1; index >= 0; index--)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave save = configs[index];
                        if (save.Title == config.Title &&
                            save.OwnerId == config.OwnerId &&
                            save.SaveTime.Minute == config.SaveTime.Minute &&
                            save.SaveTime.Second == config.SaveTime.Second)
                        {
                            thisConfig = save;
                            break;
                        }
                    }

                    if (thisConfig != null)
                    {
                        Model.Id = thisConfig.Id;
                        break;
                    }

                    await Task.Delay(2000);
                }
            }

            ConfigBrowserViewModel?.Refresh();
            CurrentWindowService.Close();

            ConfigSavedEvent?.Invoke(Module, Model, isNew);
        }

        private void OnConfigDeleteEvent(Module module, Comms.Models.Data.Oms.Config.ConfigSave config)
        {
            ConfigDeletedEvent?.Invoke(module, config);
        }

        private void LoadConfig(Comms.Models.Data.Oms.Config.ConfigSave config)
        {
            if (config != null)
            {
                var details = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(config));
                if (details != null)
                {
                    Model.Id = details.Id;
                    Model.LastUpdateTime = details.SaveTime;
                    Model.Creator = details.Username ?? "";
                    Model.Title = details.Title ?? "";
                    Model.Details = details;
                    Model.Load();
                }
            }
        }
    }
}
