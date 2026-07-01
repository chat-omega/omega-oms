using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class SelectionModel : BindableBase
{

    [Bindable]
    public partial string Name { get; set; }
    [Bindable]
    public partial bool Selected { get; set; }
    [Bindable(Default = true)]
    public partial bool Enabled { get; set; }

    public SelectionModel(string name)
    {
        Name = name;
    }
}