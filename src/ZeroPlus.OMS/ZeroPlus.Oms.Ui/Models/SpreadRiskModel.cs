using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SpreadRiskModel : BindableBase
    {

        [Bindable]
        public partial int Id { get; set; }
        [Bindable]
        public partial int TotalOpen { get; set; }
        [Bindable]
        public partial int TotalClose { get; set; }
        [Bindable]
        public partial bool Action { get; set; }
        [Bindable]
        public partial DateTime LastTradeTime { get; set; }
        [Bindable]
        public partial string SpreadDescription { get; set; }
        [Bindable]
        public partial string Tags { get; set; }
        [Bindable]
        public partial DateTime? ExDividend { get; set; }
        [Bindable]
        public partial string Underlying { get; set; }

        public SpreadRiskModel(string spreadId)
        {
            SpreadDescription = spreadId;
        }
    }
}