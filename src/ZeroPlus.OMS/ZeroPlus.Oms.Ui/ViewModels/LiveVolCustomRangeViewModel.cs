using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    /// <summary>
    /// ViewModel for the custom date range dialog when loading historical LiveVol data.
    /// </summary>
    public partial class LiveVolCustomRangeViewModel : ViewModelBase
    {

        [Bindable(Default = "")]
        public partial string Symbol { get; set; }

        [Bindable]
        public partial DateTime StartDate { get; set; }

        [Bindable]
        public partial DateTime EndDate { get; set; }

        /// <summary>
        /// Title shown in the dialog header (e.g. "Custom range – AAPL").
        /// </summary>
        public string Title => string.IsNullOrWhiteSpace(Symbol)
            ? "Custom date range"
            : $"Custom range – {Symbol}";
    }
}
