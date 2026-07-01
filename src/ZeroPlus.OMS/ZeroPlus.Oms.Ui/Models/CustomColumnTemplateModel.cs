using DevExpress.Data;
using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class CustomColumnTemplateModel : BindableBase
    {
        public string _Header;
        public UnboundColumnType _Type;
        public bool _AllowEditing;
        public bool _AllowEquationEvaluator;
        public string _Equation;

        [Bindable]
        public partial string Header { get; set; }

        [Bindable]
        public partial UnboundColumnType Type { get; set; }

        [Bindable]
        public partial bool AllowEditing { get; set; }

        [Bindable]
        public partial bool AllowEquationEvaluator { get; set; }

        [Bindable]
        public partial string Equation { get; set; }

        public CustomColumnTemplateModel()
        {
            Header = "";
            Type = UnboundColumnType.Decimal;
            AllowEditing = true;
            AllowEquationEvaluator = false;
            Equation = "";
        }
    }
}
