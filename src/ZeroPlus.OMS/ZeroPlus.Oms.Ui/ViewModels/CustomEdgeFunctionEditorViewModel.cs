using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void FormulaUpdatedHandler(string formula);
    public partial class CustomEdgeFunctionEditorViewModel : ViewModelBase
    {
        public event FormulaUpdatedHandler FormulaUpdated;


        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Formula { get; set; }

        public void Save()
        {
            FormulaUpdated?.Invoke(Formula);
        }
    }
}
