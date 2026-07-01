using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Configs;

namespace ZeroPlus.Oms.Ui.Models;

public partial class LoopIncrementConfigModel : BindableBase, IDynamicConfigModel
{
    private List<DynamicIncrementConfigModel> _dynamicIncrementConfigs;

    [JsonProperty]
    [Bindable]
    public partial int Id { get; set; }
    [JsonProperty]
    [Bindable]
    public partial string Title { get; set; }
    [JsonProperty]
    [Bindable]
    public partial string Creator { get; set; }
    [JsonProperty]
    [Bindable]
    public partial DateTime LastUpdateTime { get; set; }
    [JsonProperty]
    [Bindable]
    public partial ConfigSave Details { get; set; }
    [JsonProperty]
    public List<DynamicIncrementConfigModel> DynamicIncrementConfigs
    {
        get => _dynamicIncrementConfigs;
        set => SetValue(ref _dynamicIncrementConfigs, value?.OrderByDescending(x => x.Edge).ToList());
    }
    [JsonProperty]
    [Bindable(Default = 1)]
    public partial double MaxPercentOfMarketWidth { get; set; }


    public LoopIncrementConfigModel()
    {
        DynamicIncrementConfigs = new List<DynamicIncrementConfigModel>();
    }

    public void Save()
    {
    }

    public void Load()
    {
    }

    internal string GetAsJson()
    {
        return JsonConvert.SerializeObject(this);
    }
}