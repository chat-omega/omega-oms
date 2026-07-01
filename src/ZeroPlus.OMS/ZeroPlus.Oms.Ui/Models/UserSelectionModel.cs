using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class UserSelectionModel : BindableBase
    {

        [Bindable]
        public partial int Id { get; set; }

        [Bindable]
        public partial string Username { get; set; }

        [Bindable]
        public partial bool IsOnline { get; set; }

        [Bindable]
        public partial bool IsSelected { get; set; }
    }
}
