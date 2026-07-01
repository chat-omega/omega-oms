using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LoopTableModel : BindableBase
    {

        [JsonProperty]
        [Bindable]
        public partial double Profit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int PayupTicks { get; set; }
    }
}
