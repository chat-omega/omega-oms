using DevExpress.Mvvm;
using ICSharpCode.AvalonEdit.Document;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ScriptEditorViewModel : ViewModelBase
    {

        [Bindable]
        public partial TextDocument Script { get; set; }
    }
}
