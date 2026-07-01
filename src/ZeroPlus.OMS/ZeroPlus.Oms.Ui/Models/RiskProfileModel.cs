using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class RiskProfileModel : BindableBase
    {
        bool _IsCurrent;

        [Bindable]
        public partial bool IsCurrent { get; set; }
        [Bindable]
        public partial double UnderlyingPrice { get; set; }
        [Bindable]
        public partial double QtyNetDelta { get; set; }
        [Bindable]
        public partial double QtyNetTheta { get; set; }
        [Bindable]
        public partial double QtyNetGamma { get; set; }
        [Bindable]
        public partial double NetDelta { get; set; }
        [Bindable]
        public partial double NetTheta { get; set; }
        [Bindable]
        public partial double NetGamma { get; set; }
        [Bindable]
        public partial double Pnl { get; set; }
        public double Percentage { get; internal set; }
    }
}
