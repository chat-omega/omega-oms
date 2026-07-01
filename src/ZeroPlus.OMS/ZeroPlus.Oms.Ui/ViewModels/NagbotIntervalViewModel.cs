using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class NagbotIntervalViewModel : CustomizableTableViewModelBase
    {
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial NagbotIntervalModel Model { get; set; }

        [Command]
        public void AddCommand()
        {
            Model.Configs.Add(new NagbotIntervalConfigModel());
        }

        [Command]
        public void RemoveCommand(NagbotIntervalConfigModel model)
        {
            if (model != null)
            {
                Model.Configs.Remove(model);
            }
        }

        [Command]
        public async void SaveCommand()
        {
            if (Model == null)
            {
                MessageBoxService.ShowMessage("Model can not be empty.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
            }
            else if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.ShowMessage("Title can not be empty.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
            }
            else if (Model.Configs.Count == 0)
            {
                MessageBoxService.ShowMessage("Invalid edge table.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
            }
            else
            {
                await SaveToServer();
                CurrentWindowService?.Close();
            }
        }

        private async Task SaveToServer()
        {
            try
            {
                if (Model.Details == null)
                {
                    List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.NagbotIntervalConfigs);
                    ConfigSave sameConfig = config?.FirstOrDefault(x => x.Title == Model.Title);
                    bool save = false;

                    if (sameConfig == null)
                    {
                        save = true;
                    }
                    else
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

                    if (save)
                    {
                        Model.LastUpdateTime = DateTime.Now;
                        string json = Model.GetAsJson();
                        ConfigSave configSave = new()
                        {
                            Id = Model.Id,
                            OwnerId = OmsCore.User.ID,
                            Username = OmsCore.User.Username,
                            Module = (int)Module.NagbotIntervalConfigs,
                            ConfigJson = json,
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
                        Model.LastUpdateTime = DateTime.Now;
                        ConfigSave configSave = Model.Details;
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
    }
}
