using System;
using System.Collections.Generic;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models;

public partial class CustomListModel : BindableBase, IDynamicConfigModel
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
    public partial HashSet<InputModel> SymbolModels { get; set; }

    public CustomListModel()
    {
        SymbolModels = new HashSet<InputModel>();
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