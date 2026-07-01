using System;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Matrix.Strategies;

namespace ZeroPlus.Oms.Ui.Models;

public partial class MatrixStrategyConfigModel : BindableBase, IDynamicConfigModel
{

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
    [Bindable]
    public partial SyntheticSpreadStrategyData SyntheticSpreadStrategyData { get; set; }
    [JsonProperty]
    [Bindable]
    public partial SeekerStrategyData SeekerStrategyData { get; set; }
    [JsonProperty]
    [Bindable]
    public partial SeekerSpreadStrategyData SeekerSpreadStrategyData { get; set; }
    [JsonProperty]
    [Bindable]
    public partial ScrapeStrategyData ScrapeStrategyData { get; set; }

    public void Save()
    {
    }

    public void Load()
    {
    }

    public string GetAsJson()
    {
        return JsonConvert.SerializeObject(this);
    }
}