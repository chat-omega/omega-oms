using DevExpress.Mvvm;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DominatorSetupGroupModel : BindableBase
    {


        [Bindable]
        public partial string Title { get; set; }

        [Bindable]
        public partial List<string> Setups { get; set; }

        [Bindable]
        public partial string SelectedSetup { get; set; }

        public DominatorSetupGroupModel(string title, List<string> setups)
        {
            Title = title;
            Setups = setups;
        }
    }
}
