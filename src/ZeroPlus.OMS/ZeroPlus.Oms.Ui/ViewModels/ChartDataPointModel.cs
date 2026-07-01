using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ChartDataPointModel : BindableBase
    {

        public DateTime Timestamp { get; set; }

        [Bindable]
        public partial double Bid { get; set; }
        [Bindable]
        public partial double Mid { get; set; }
        [Bindable]
        public partial double Ask { get; set; }
        [Bindable]
        public partial double Ema { get; set; }
    }
}
