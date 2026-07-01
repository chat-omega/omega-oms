using DevExpress.Mvvm;
using ZeroPlus.Oms.Data.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ExecutingBrokerFeeModelViewModel : CustomizableTableViewModelBase
    {

        [Bindable]
        public partial ExecutingBrokerFeeModel Model { get; set; }

    }
}
