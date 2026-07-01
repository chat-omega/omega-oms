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
    public partial class LiquidatorModel : BindableBase, IDynamicConfigModel
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
        [JsonIgnore]
        [Bindable]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string Route { get; set; }
        [JsonProperty]
        [Bindable]
        public partial ChaserControlModel ChaserModel { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LiquidatorType Type { get; set; }

        public LiquidatorModel(IEnumerable<string> routes)
        {
            ChaserModel = new ChaserControlModel();
            RoutesList = [.. routes];
            Route = RoutesList.FirstOrDefault() ?? string.Empty;
        }

        public void Save()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(Type)] = Type.ToString(),
                [nameof(Route)] = Route,
                [nameof(ChaserModel)] = JsonConvert.SerializeObject(ChaserModel)
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
                    if (configDictionary.TryGetValue(nameof(Type), out var typeString))
                    {
                        if (Enum.TryParse(typeString, true, out LiquidatorType type))
                        {
                            Type = type;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(Route), out var route))
                    {
                        Route = route;
                    }
                    if (configDictionary.TryGetValue(nameof(ChaserModel), out var chaserConfig))
                    {
                        var chaserControlModel = JsonConvert.DeserializeObject<ChaserControlModel>(chaserConfig);
                        if (chaserControlModel != null)
                        {
                            ChaserModel = chaserControlModel;
                        }
                    }
                }
            }
        }

        public jsonRequestLiquidatorController GetParams()
        {
            return new jsonRequestLiquidatorController
            {
                Chaser = ChaserModel?.JsonRequestExecutionChaserParams
            };
        }
    }
}
