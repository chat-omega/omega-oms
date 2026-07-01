using DevExpress.Mvvm;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class HedgeLookupModel : BindableBase
    {

        private string _OrderSymbol;
        public string OrderSymbol
        {
            get => _OrderSymbol;
            set => SetValue(ref _OrderSymbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        private string _HedgeSymbol;
        public string HedgeSymbol
        {
            get => _HedgeSymbol;
            set => SetValue(ref _HedgeSymbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial double Multiplier { get; set; }

        public string Id => OrderSymbol + HedgeSymbol;

        public HedgeLookupModel(string symbol, string route, double interval)
        {
            OrderSymbol = symbol;
            HedgeSymbol = route;
            Multiplier = interval;
        }
    }
}