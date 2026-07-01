using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class QuickRouteModel : BindableBase
    {

        [Bindable]
        public partial string Name { get; set; }

        [Bindable]
        public partial string Route { get; set; }

        public string Id => Name + Route;

        public QuickRouteModel(string name, string route)
        {
            Name = name;
            Route = route;
        }
    }
}