using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PresetTriggerModel : BindableBase
    {

        [Bindable(Default = double.NaN)]
        public partial double BuyPrice { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SellPrice { get; set; }

        internal void Reset()
        {
            BuyPrice = double.NaN;
            SellPrice = double.NaN;
        }
    }
}
