using DevExpress.Mvvm;
using System;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Generators.SpreadGenerators;

namespace ZeroPlus.Oms.Ui.Models;

public class BasketDefaultLayoutModel : BindableBase
{
    public Tuple<int, string, LegTypes, Strategy, ConfigSave> Export => Tuple.Create(Index, Underlying, LegType, BaseStrategy, Layout);

    public int Index { get; set; }
    public string Underlying { get; set; }
    public ConfigSave Layout { get; set; }
    public LegTypes LegType { get; set; }
    public Strategy BaseStrategy { get; set; }

    internal bool IsValid()
    {
        if (Index < 0)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(Underlying))
        {
            return false;
        }
        if (Layout == null)
        {
            return false;
        }

        return true;
    }
}