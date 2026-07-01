using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class NagbotIntervalConfigModel : BindableBase
    {

        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool Enabled { get; set; }

        [JsonProperty]
        [Bindable(Default = 30)]
        public partial double Interval { get; set; }

        [JsonConstructor]
        public NagbotIntervalConfigModel() { }

        public NagbotIntervalConfigModel(NagbotIntervalConfigModel item)
        {
            Enabled = item.Enabled;
            Interval = item.Interval;
        }
    }
}
