using DevExpress.Mvvm;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketGroupModel : BindableBase
    {
        private readonly HashSet<int> _basketIds = new();
        public string _Name;
        public string _Uid;
        public bool IsEmpty => _basketIds.Count == 0;

        [Bindable]
        public partial string Name { get; set; }

        [Bindable]
        public partial string Uid { get; set; }

        public BasketGroupModel()
        {

        }

        public BasketGroupModel(BasketGroupModel source)
        {
            Name = source.Name;
            Uid = source.Uid;
        }

        public BasketGroupModel(string name, string uid)
        {
            Name = name;
            Uid = uid;
        }
    }
}
