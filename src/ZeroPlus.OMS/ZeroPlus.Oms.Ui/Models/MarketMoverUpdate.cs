using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models;

public partial class MarketMoverUpdate : BindableBase
{

    [Bindable(Default = "")]
    public partial string Symbol { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double DayPercentChange { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double HourPercentChange { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double HalfHourPercentChange { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double QuarterHourPercentChange { get; set; }
    [Bindable]
    public partial int Volume { get; set; }
    [Bindable]
    public partial int OptionVolume { get; set; }
}