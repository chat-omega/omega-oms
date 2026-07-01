using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;
using ConfigSave = ZeroPlus.Comms.Models.Data.Oms.Config.ConfigSave;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class AutoPermConfigViewModel : CustomizableTableViewModelBase
    {
        const Module MODULE = Module.AutoPermConfig;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public OmsCore OmsCore { get; }
        public IEnumerable<AutoPermSelectionMode> AutoPermSelectionModes { get; } = ((AutoPermSelectionMode[])Enum.GetValues(typeof(AutoPermSelectionMode))).ToList();
        public IEnumerable<PermMatchingMode> PermMatchingModes { get; } = ((PermMatchingMode[])Enum.GetValues(typeof(PermMatchingMode))).ToList();
        public IEnumerable<SideOperation> SideOperations { get; } = ((SideOperation[])Enum.GetValues(typeof(SideOperation))).ToList();
        public Action<Module, IDynamicConfigModel> Loader { get; set; }

        [Bindable]
        public partial BasketAutoPermModel Model { get; set; }

        [Bindable]
        public partial ObservableCollection<AutoPermConfigModel> AutoPermConfigs { get; set; }

        [Bindable]
        public partial ObservableCollection<PermOperationModel> PermOperationModels { get; set; }

        public AutoPermConfigViewModel(OmsCore omsCore)
        {
            AutoPermConfigs = new ObservableCollection<AutoPermConfigModel>();
            OmsCore = omsCore;
        }

        [Command]
        public void OpenPermComboEditorCommand()
        {
            PermComboEditorView view = new();
            view.Show();
        }

        [Command]
        public void AddNewAutoPermConfigCommand()
        {
            if (AutoPermConfigs.Count < 6)
            {
                AutoPermConfigModel sizeupConfigModel = new();
                AutoPermConfigs.Add(sizeupConfigModel);
            }
        }

        [Command]
        public async Task SaveAutoPermConfigCommand()
        {
            if (Model != null)
            {
                Model.AutoPermConfigs = AutoPermConfigs.ToList();
                await Save();
            }
        }

        [Command]
        public async Task SaveAndLoadAutoPermConfigCommand()
        {
            if (Model != null)
            {
                await SaveAutoPermConfigCommand();
                Loader?.Invoke(Module.AutoPermConfig, Model);
            }
        }

        [Command]
        public void RemoveAutoPermItemCommand(AutoPermConfigModel model)
        {
            AutoPermConfigs.Remove(model);
        }

        private async Task<bool> Save()
        {
            if (Model == null)
            {
                MessageBoxService.ShowMessage("Model can not be empty.", "Auto Perm Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.ShowMessage("Title can not be empty.", "Auto Perm Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else
            {
                Model.Load();
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
                    var config = await OmsCore.GatewayClient.RequestConfigsAsync((int)MODULE);
                    var sameConfig = config?.FirstOrDefault(x => x.Title == Model.Title);
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
                            Module = (int)MODULE,
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
                        Model.Details.Module = (int)MODULE;
                        Model.LastUpdateTime = DateTime.Now;
                        ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(Model.Details));
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
