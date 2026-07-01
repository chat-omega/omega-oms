using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class TicketStopLossModel : BindableBase
    {


        [Bindable]
        public partial string UnderlyingSymbol { get; set; }

        [Bindable]
        public partial double Interval { get; set; }

        [Bindable]
        public partial double Increment { get; set; }

        [Bindable]
        public partial int Count { get; set; }

        public string Id => UnderlyingSymbol;

        public TicketStopLossModel(string symbol, double interval, double increment, int count)
        {
            UnderlyingSymbol = symbol;
            Interval = interval;
            Increment = increment;
            Count = count;
        }
    }
}