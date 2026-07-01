using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DrifterControlViewModel : ViewModelBase
    {


        [Bindable]
        public partial bool TargetOffTrades { get; set; }
        [Bindable]
        public partial double PercentBid { get; set; }
        [Bindable]
        public partial int HuntTtl { get; set; }
        [Bindable]
        public partial int DriftTtl { get; set; }
        [Bindable]
        public partial int StaleTradeMs { get; set; }
        [Bindable]
        public partial int PayupTicks { get; set; }
        [Bindable]
        public partial int MinEnterTicks { get; set; }
        [Bindable]
        public partial int MinWorkTicks { get; set; }
        [Bindable]
        public partial int EnterMinL1LeanQty { get; set; }
        [Bindable]
        public partial int EnterMinL1LeanCount { get; set; }
        [Bindable]
        public partial int MaxDriftOrdersToSend { get; set; }
        [Bindable]
        public partial int WorkTtl { get; set; }
        [Bindable]
        public partial int AloneMaxSideSpreadToWork { get; set; }
        [Bindable]
        public partial int AloneMinL1LeanQty { get; set; }
        [Bindable]
        public partial int AloneMinL1LeanCount { get; set; }
        [Bindable]
        public partial int AloneMinL2LeanQty { get; set; }
        [Bindable]
        public partial int AloneMinL2LeanCount { get; set; }
        [Bindable]
        public partial int JoinedMaxSideSpreadToWork { get; set; }
        [Bindable]
        public partial int JoinedMinL1LeanQty { get; set; }
        [Bindable]
        public partial int JoinedMinL1LeanCount { get; set; }
        [Bindable]
        public partial int JoinedMinL2LeanQty { get; set; }
        [Bindable]
        public partial int JoinedMinL2LeanCount { get; set; }
    }
}
