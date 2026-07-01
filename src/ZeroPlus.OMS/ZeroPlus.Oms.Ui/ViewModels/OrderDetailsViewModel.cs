using DevExpress.Mvvm;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class OrderDetailsViewModel : CustomizableTableViewModelBase
    {

        [Bindable]
        public partial OmsOrderModel Order { get; set; }

        public OrderDetailsViewModel()
        {

        }
    }
}
