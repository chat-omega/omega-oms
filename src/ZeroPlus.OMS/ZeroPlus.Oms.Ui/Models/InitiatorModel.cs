using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.ViewModels;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.MsgRequests;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class InitiatorModel : BindableBase, IDynamicConfigModel
    {

        [Bindable]
        public partial string Title { get; set; }
        [Bindable]
        public partial int Id { get; set; }
        [Bindable]
        public partial string Creator { get; set; }
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [Bindable]
        public partial ConfigSave Details { get; set; }
        [JsonProperty]
        [Bindable]
        public partial InitiatorType Type { get; set; }
        [JsonIgnore]
        [Bindable]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string Route { get; set; }
        [JsonProperty]
        [Bindable]
        public partial HunterModel HunterModel { get; set; }

        public InitiatorModel(IEnumerable<string> routes)
        {
            HunterModel = new HunterModel();
            RoutesList = [.. routes];
            Route = RoutesList.FirstOrDefault() ?? string.Empty;
        }

        public void Save()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(Type)] = Type.ToString(),
                [nameof(Route)] = Route,
                [nameof(HunterModel)] = JsonConvert.SerializeObject(HunterModel)
            };
            Details ??= new ConfigSave();
            Details.ConfigJson = JsonConvert.SerializeObject(configDictionary);
            Details.SaveTime = DateTime.Now;
        }

        public void Load()
        {
            if (Details != null && !string.IsNullOrWhiteSpace(Details.ConfigJson))
            {
                var configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Details.ConfigJson);
                if (configDictionary != null)
                {
                    if (configDictionary.TryGetValue(nameof(Type), out var instanceType))
                    {
                        if (Enum.TryParse(instanceType, true, out InitiatorType type))
                        {
                            Type = type;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(Route), out var route))
                    {
                        Route = route;
                    }
                    if (configDictionary.TryGetValue(nameof(HunterModel), out var hunterConfig))
                    {
                        var hunterModel = JsonConvert.DeserializeObject<HunterModel>(hunterConfig);
                        if (hunterModel != null)
                        {
                            HunterModel = hunterModel;
                        }
                    }
                }
            }
        }

        public jsonRequestInitiatorController GetParams(LoopModel loopModel)
        {
            jsonRequestInitiatorController initiatorController = new jsonRequestInitiatorController
            {
                Hunter = HunterModel.JsonRequestExecutionHunterParams(loopModel),
            };

            return initiatorController;
        }
    }
}
