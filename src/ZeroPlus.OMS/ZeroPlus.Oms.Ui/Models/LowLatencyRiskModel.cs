using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Configs;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LowLatencyRiskModel : BindableBase, IDynamicConfigModel
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
        public partial int MaxLossInitiator { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxLossLiquidator { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxOpenPosition { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxOpenSymbols { get; set; }

        public void Save()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(MaxLossInitiator)] = MaxLossInitiator.ToString(),
                [nameof(MaxLossLiquidator)] = MaxLossLiquidator.ToString(),
                [nameof(MaxOpenPosition)] = MaxOpenPosition.ToString(),
                [nameof(MaxOpenSymbols)] = MaxOpenSymbols.ToString(),
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
                    if (configDictionary.TryGetValue(nameof(MaxLossInitiator), out var maxLossInitiator) &&
                        int.TryParse(maxLossInitiator, out var maxLossInitiatorVal))
                    {
                        MaxLossInitiator = maxLossInitiatorVal;
                    }
                    if (configDictionary.TryGetValue(nameof(MaxLossLiquidator), out var maxLossLiquidator) &&
                        int.TryParse(maxLossLiquidator, out var maxLossLiquidatorVal))
                    {
                        MaxLossLiquidator = maxLossLiquidatorVal;
                    }
                    if (configDictionary.TryGetValue(nameof(MaxOpenPosition), out var maxOpenPosition) &&
                        int.TryParse(maxOpenPosition, out var maxOpenPositionVal))
                    {
                        MaxOpenPosition = maxOpenPositionVal;
                    }
                    if (configDictionary.TryGetValue(nameof(MaxOpenSymbols), out var maxOpenSymbols) &&
                        int.TryParse(maxOpenSymbols, out var maxOpenSymbolsVal))
                    {
                        MaxOpenSymbols = maxOpenSymbolsVal;
                    }

                }
            }
        }
    }
}
