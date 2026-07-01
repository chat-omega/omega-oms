using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models;

public partial class ContraEdgeLookupModel : BindableBase
{

    [Bindable]
    public partial string Symbol { get; set; }
    [Bindable(Default = 1)]
    public partial double Edge { get; set; }

    public ContraEdgeLookupModel(string symbol, double edge)
    {
        Symbol = symbol;
        Edge = edge;
    }
}