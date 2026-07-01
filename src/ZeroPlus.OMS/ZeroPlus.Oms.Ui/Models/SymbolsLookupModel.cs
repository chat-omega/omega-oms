using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SymbolsLookupModel : BindableBase
    {

        [Bindable]
        public partial string Old { get; set; }
        [Bindable]
        public partial string New { get; set; }
        [Bindable]
        public partial bool SubscribeToTicks { get; set; }
        [Bindable(Default = 1)]
        public partial double Multiplier { get; set; }

        public string Id => Old + New;

        public SymbolsLookupModel(string old, string newVal, double multiplier, bool subscribeToTicks)
        {
            Old = old;
            New = newVal;
            Multiplier = multiplier;
            SubscribeToTicks = subscribeToTicks;
        }
    }
}