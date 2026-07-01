using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DynamicIncrementConfigModel : BindableBase
    {

        [JsonProperty]
        [Bindable(Default = 0.10)]
        public partial double Edge { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool MinTickEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial double MinTick { get; set; }

        [JsonProperty]
        [Bindable(Default = 0.05)]
        public partial double Increment { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Default { get; set; }

        public DynamicIncrementModel GetConfig()
        {
            return new DynamicIncrementModel
            {
                Edge = Edge,
                Increment = Increment,
            };
        }
    }
}
