using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models;

public partial class LegInSettings : BindableBase
{

    [Bindable(Default = true)]
    public partial bool FishForLiquidityOnStart { get; set; }
    [Bindable(Default = 2)]
    public partial int Ratio1 { get; set; }
    [Bindable(Default = 5)]
    public partial int Ratio2 { get; set; }
    [Bindable]
    public partial double CheapoDeltaRangeMin { get; set; }
    [Bindable(Default = .01)]
    public partial double CheapoDeltaRangeMax { get; set; }
    [Bindable]
    public partial double CheapoWidthRangeMin { get; set; }
    [Bindable(Default = .01)]
    public partial double CheapoWidthRangeMax { get; set; }
    [Bindable]
    public partial double CushionValue { get; set; }
    [Bindable]
    public partial double MinEdge { get; set; }
    [Bindable(Default = 2)]
    public partial int SpreadsCount { get; set; }
    [Bindable(Default = true)]
    public partial bool AdjustForCheapoWidth { get; set; }
    [Bindable(Default = 5)]
    public partial int CheapoMaxResubmit { get; set; }
}