using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class TheoPriceInfoModel : BindableBase
    {

        [Bindable]
        public partial DateTime Date { get; set; }

        [Bindable]
        public partial double IntRate { get; set; }

        [Bindable]
        public partial double VolAdj { get; set; }

        [Bindable]
        public partial string Summary { get; set; }

        public TheoPriceInfoModel()
        {
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            Summary = $"Theo Price: {Date:d}";

            if (IntRate != 0)
            {
                Summary += $" Int. Rate: {IntRate:P}";
            }

            if (VolAdj != 0)
            {
                Summary += $" Vol Adj. {VolAdj:P}";
            }
        }
    }
}
