using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DynamicEdgeConfigViewModel : CustomizableTableViewModelBase
    {
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Action<DynamicEdgeModel> Loader { get; internal set; }

        [Bindable]
        public partial DynamicEdgeModel Model { get; set; }

        [Bindable]
        public partial double IncreaseByPercentValue { get; set; }

        [Bindable]
        public partial double IncreaseByValue { get; set; }

        public DynamicEdgeConfigViewModel()
        {
            IncreaseByPercentValue = .1;
            IncreaseByValue = .01;
        }

        [Command]
        public void IncreaseEdgeCommand()
        {
            foreach (DaysToExpirationEdgeModel item in Model.DteTable)
            {
                item.BaseEdge += item.BaseEdge * IncreaseByPercentValue;
                item.LoopMinEdge += item.LoopMinEdge * IncreaseByPercentValue;
            }
        }

        [Command]
        public void DecreaseEdgeCommand()
        {
            foreach (DaysToExpirationEdgeModel item in Model.DteTable)
            {
                item.BaseEdge = Math.Max(0.0, item.BaseEdge - (item.BaseEdge * IncreaseByPercentValue));
                item.LoopMinEdge = Math.Max(0.0, item.LoopMinEdge - (item.LoopMinEdge * IncreaseByPercentValue));
            }
        }

        [Command]
        public void IncreaseEdgeByValueCommand()
        {
            foreach (DaysToExpirationEdgeModel item in Model.DteTable)
            {
                item.BaseEdge += IncreaseByValue;
                item.LoopMinEdge += IncreaseByValue;
            }
        }

        [Command]
        public void DecreaseEdgeByValueCommand()
        {
            foreach (DaysToExpirationEdgeModel item in Model.DteTable)
            {
                item.BaseEdge = Math.Max(0.0, item.BaseEdge - IncreaseByValue);
                item.LoopMinEdge = Math.Max(0.0, item.LoopMinEdge - IncreaseByValue);
            }
        }

        [Command]
        public void AddDteCommand()
        {
            if (Model.DynamicLookupMode)
            {
                Model.DynamicDteTable.Add(new());
            }
            else
            {
                Model.DteTable.Add(new());
            }
        }

        [Command]
        public void AddUnderMultiplierCommand()
        {
            Model.UnderMultiplierTable.Add(new());
        }

        [Command]
        public void AddDeltaCommand()
        {
            Model.DeltaTable.Add(new());
        }

        [Command]
        public void RemoveDteCommand(DaysToExpirationEdgeModel model)
        {
            if (model != null)
            {
                Model.DteTable.Remove(model);
                Model.DynamicDteTable.Remove(model);
            }
        }

        [Command]
        public void RemoveUnderMultiplierCommand(UnderMultiplierModel model)
        {
            if (model != null)
            {
                Model.UnderMultiplierTable.Remove(model);
            }
        }

        [Command]
        public void RemoveDeltaCommand(DeltaEdgeModel model)
        {
            if (model != null)
            {
                Model.DeltaTable.Remove(model);
            }
        }

        [Command]
        public async void SaveCommand()
        {
            if (await Save())
            {
                CurrentWindowService?.Close();
            }
        }

        [Command]
        public async void SaveAndLoadCommand()
        {
            if (await Save())
            {
                Loader?.Invoke(Model);
                CurrentWindowService?.Close();
            }
        }

        private async Task<bool> Save()
        {
            if (Model == null)
            {
                MessageBoxService.ShowMessage("Model can not be empty.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.ShowMessage("Title can not be empty.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (Model.DynamicDteTable.Count == 0 && Model.DteTable.Count == 0 && Model.DeltaTable.Count == 0)
            {
                MessageBoxService.ShowMessage("Invalid edge table.", "Dynamic Edge Config", MessageButton.OK, MessageIcon.Warning);
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
                    List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.DynamicEdgeConfigs);
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
                            Module = (int)Module.DynamicEdgeConfigs,
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
