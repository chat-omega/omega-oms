using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public class AutoCloseConfigTierModel : BindableBase
    {
        [JsonProperty]
        public double ProfitPercentage { get; set; }
        [JsonProperty]
        public double PositionPercentage { get; set; }
    }
}
