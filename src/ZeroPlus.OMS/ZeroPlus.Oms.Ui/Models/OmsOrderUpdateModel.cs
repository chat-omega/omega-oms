using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class OmsOrderUpdateModel : BindableBase
    {

        [Bindable]
        public partial OrderStatus OrderStatus { get; set; }

        [Bindable]
        public partial Side Side { get; set; }

        [Bindable]
        public partial int Filled { get; set; }

        public override string ToString()
        {
            return Side + " " + Filled + " " + OrderStatus;
        }
    }
}
