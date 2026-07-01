using DevExpress.Mvvm;
using System.Collections.ObjectModel;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class ComplexOrderLegsViewModel : CustomizableTableViewModelBase
    {
        public string Uid { get; set; }
        public OmsOrderModel Order { get; set; }
        public ObservableCollection<OmsOrderModel> Orders { get; set; } = new ObservableCollection<OmsOrderModel>();
    }
}