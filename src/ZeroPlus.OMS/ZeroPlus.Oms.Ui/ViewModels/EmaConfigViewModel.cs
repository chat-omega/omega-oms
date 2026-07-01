using DevExpress.Mvvm;
using ZeroPlus.Oms.Indicators;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EmaConfigViewModel : ViewModelBase
    {

        [Bindable]
        public partial IEmaConfig EmaConfig { get; set; }
    }
}
