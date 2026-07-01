using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.Enums;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.MsgRequests;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LeanModel : BindableBase
    {

        [JsonProperty]
        [Bindable]
        public partial double MinNbboPrice { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MaxNbboPrice { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MinMarketWidth { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MaxMarketWidth { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MinSideQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double MaxSideSpread { get; set; }
        [JsonProperty]
        [Bindable]
        public partial SignalDataType SignalDataType { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MarketMinL1LeanQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MarketMinL1LeanCount { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MarketMinL2LeanQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MarketMinL2LeanCount { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int DigQty { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int DigCount { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double DigWidthMin { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double DigWidthMax { get; set; }

        public jsonRequestLeanBaseParams JsonRequestLeanBaseParams => new()
        {
            MinSpreadPrice = $"{MinMarketWidth:N2}",
            MaxSpreadPrice = $"{MaxMarketWidth:N2}",
            MaxSideSpreadPrice = $"{MaxSideSpread:N2}",
            MinNbboPrice = $"{MinNbboPrice:N2}",
            MaxNbboPrice = $"{MaxNbboPrice:N2}",
            MinL1LeanQty = MarketMinL1LeanQty,
            MinL1LeanCnt = MarketMinL1LeanCount,
            MinL2LeanQty = MarketMinL2LeanQty,
            MinL2LeanCnt = MarketMinL2LeanCount,
            MinSideQty = MinSideQty,
            MinDigQty = DigQty,
            MinDigCnt = DigCount,
            UseDig = SignalDataType == SignalDataType.Market ? 0 : 1,
        };
    }
}