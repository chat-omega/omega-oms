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
    public partial class DynamicIntervalConfigViewModel : CustomizableTableViewModelBase
    {
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Action<DynamicIntervalModel> Loader { get; internal set; }

        [Bindable]
        public partial DynamicIntervalModel Model { get; set; }

        [Bindable]
        public partial double IncreaseByPercentValue { get; set; }

        [Bindable]
        public partial double IncreaseByValue { get; set; }

        public DynamicIntervalConfigViewModel()
        {
            IncreaseByPercentValue = .1;
            IncreaseByValue = .01;
        }

        [Command]
        public void IncreaseEdgeCommand()
        {
            foreach (IntervalModel item in Model.IntervalTable)
            {
                item.AttemptedEdge += item.AttemptedEdge * IncreaseByPercentValue;
            }
        }

        [Command]
        public void DecreaseEdgeCommand()
        {
            foreach (IntervalModel item in Model.IntervalTable)
            {
                item.AttemptedEdge = Math.Max(0.0, item.AttemptedEdge - (item.AttemptedEdge * IncreaseByPercentValue));
            }
        }

        [Command]
        public void IncreaseEdgeByValueCommand()
        {
            foreach (IntervalModel item in Model.IntervalTable)
            {
                item.AttemptedEdge += IncreaseByValue;
            }
        }

        [Command]
        public void DecreaseEdgeByValueCommand()
        {
            foreach (IntervalModel item in Model.IntervalTable)
            {
                item.AttemptedEdge = Math.Max(0.0, item.AttemptedEdge - IncreaseByValue);
            }
        }

        [Command]
        public void AddCommand()
        {
            Model.IntervalTable.Add(new IntervalModel());
        }

        [Command]
        public void RemoveCommand(IntervalModel model)
        {
            if (model != null)
            {
                Model.IntervalTable.Remove(model);
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
                MessageBoxService.ShowMessage("Model can not be empty.", "Dynamic Interval Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Model.Title))
            {
                MessageBoxService.ShowMessage("Title can not be empty.", "Dynamic Interval Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else if (Model.IntervalTable.Count == 0)
            {
                MessageBoxService.ShowMessage("Invalid edge table.", "Dynamic Interval Config", MessageButton.OK, MessageIcon.Warning);
                return false;
            }
            else
            {
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
                    List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.DynamicIntervalConfigs);
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
                            Module = (int)Module.DynamicIntervalConfigs,
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
