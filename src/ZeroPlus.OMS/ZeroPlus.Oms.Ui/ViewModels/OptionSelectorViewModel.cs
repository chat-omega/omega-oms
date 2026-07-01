using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void SelectionChangedHandler(string leg, string selection);
    public partial class OptionSelectorViewModel : ViewModelBase
    {
        public event SelectionChangedHandler SelectionChanged;


        public string WhichLeg { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Selected { get; set; }

        [Bindable]
        public partial HashSet<string> Options { get; set; }


        [Command]
        public void UpdateSelectionCommand(List<object> selectedTokens)
        {
            Selected = string.Join(", ", selectedTokens.Select(x => x.ToString()));
            SelectionChanged?.Invoke(WhichLeg, Selected);
        }
    }
}
