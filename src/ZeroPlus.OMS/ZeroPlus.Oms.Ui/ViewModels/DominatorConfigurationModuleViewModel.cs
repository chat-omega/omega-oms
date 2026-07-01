using DevExpress.Mvvm;
using ZeroPlus.Oms.Ui.Models;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DominatorConfigurationModuleViewModel : ViewModelBase
    {
        public List<DominatorTraderModel> TraderModels { get; init; }
        [Bindable]
        public partial DominatorConfigurationViewModel ConfigViewModel { get; set; }
    }
}
