using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;
using ConfigSave = ZeroPlus.Comms.Models.Data.Oms.Config.ConfigSave;

namespace ZeroPlus.Oms.Ui.ViewModels;

public abstract partial class DynamicConfigEditorBase : CustomizableTableViewModelBase
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public abstract Module Module { get; }

    public OmsCore OmsCore { get; }

    public IDynamicConfigModel Model { get; set; }

    [Bindable]
    public partial string Title { get; set; }

    protected DynamicConfigEditorBase(OmsCore omsCore)
    {
        OmsCore = omsCore;
    }

    protected async Task<bool> Save(string json, bool skipPermissionCheck = false)
    {
        if (Model == null)
        {
            MessageBoxService.ShowMessage("Model can not be empty.", Module.ToString().FromCamelCase(), MessageButton.OK, MessageIcon.Warning);
            return false;
        }
        else if (string.IsNullOrWhiteSpace(Model.Title))
        {
            MessageBoxService.ShowMessage("Title can not be empty.", Module.ToString().FromCamelCase(), MessageButton.OK, MessageIcon.Warning);
            return false;
        }
        else
        {
            Model.Title = Title;
            Model.Load();
            await SaveToServer(json, skipPermissionCheck);
            return true;
        }
    }

    protected async Task SaveToServer(string json, bool skipPermissionCheck)
    {
        try
        {
            if (Model.Details == null)
            {
                var config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module);
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
                    ConfigSave configSave = new()
                    {
                        Id = Model.Id,
                        OwnerId = OmsCore.User.ID,
                        Username = OmsCore.User.Username,
                        Module = (int)Module,
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
                if (Model.Details.OwnerId == OmsCore.User.ID || skipPermissionCheck)
                {
                    Model.Details.Module = (int)Module;
                    Model.LastUpdateTime = DateTime.Now;
                    ConfigSave configSave = JsonConvert.DeserializeObject<ConfigSave>(JsonConvert.SerializeObject(Model.Details));
                    configSave.Title = Model.Title;
                    configSave.ConfigJson = json;
                    configSave.SaveTime = DateTime.Now;
                    OmsCore.GatewayClient.SaveConfig(configSave);

                    MessageBoxService.ShowMessage($"{Model.Title} config saved.",
                        $"ZeroPlus OMS",
                        MessageButton.OK,
                        MessageIcon.Information);
                }
                else
                {
                    MessageBoxService.ShowMessage("You do not have permission to edit this config!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, nameof(SaveToServer));
        }
    }
}