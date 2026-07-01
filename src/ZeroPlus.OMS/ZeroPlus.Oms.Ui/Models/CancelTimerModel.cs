using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class CancelTimerModel : BindableBase
    {
        public string _Symbol;
        public string _Route;
        public double _Interval;
        public double _SingleLegInterval;

        [Bindable]
        public partial string Symbol { get; set; }

        [Bindable]
        public partial string Route { get; set; }

        [Bindable]
        public partial double Interval { get; set; }

        [Bindable]
        public partial double SingleLegInterval { get; set; }

        public string Id => Symbol + Route;

        public CancelTimerModel(string symbol, string route, double interval, double singleLegInterval)
        {
            Symbol = symbol;
            Route = route;
            Interval = interval;
            SingleLegInterval = singleLegInterval;
        }
    }
}