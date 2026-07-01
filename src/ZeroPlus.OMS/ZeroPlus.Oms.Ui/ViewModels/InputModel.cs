using System;
using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class InputModel : BindableBase
{

    [JsonProperty]
    [Bindable]
    public partial string Symbol { get; set; }
    [JsonProperty]
    [Bindable]
    public partial string Contributor { get; set; }
    [JsonProperty]
    [Bindable]
    public partial DateTime AddTime { get; set; }

    public InputModel()
    {
        AddTime = DateTime.Now;
    }

    public InputModel(string symbol, string contributor, DateTime time)
    {
        Symbol = symbol;
        Contributor = contributor;
        AddTime = time;
    }

    public override int GetHashCode()
    {
        return Symbol?.GetHashCode() ?? string.Empty.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is InputModel other)
        {
            return other.Symbol == Symbol;
        }

        return false;
    }
}