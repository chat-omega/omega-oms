using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.LowLatency.Ext;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ChaserControlModel : ViewModelBase
    {

        [JsonProperty]
        [Bindable]
        public partial int InitialDelayMs { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int OffsetWorkTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxDuplicateRetries { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int OffsetProfitTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ChaseTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ChaseTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int ScratchTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int TradeOutTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int TradeOutTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int TradeOutSweepTTL { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double SpookPrice { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double PayupAdjTheoPx { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double ChaseIncrement { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AdjTheoMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SweepOnTradeoutMode { get; set; }

        public MsgRequests.jsonRequestExecutionChaserParams JsonRequestExecutionChaserParams => new()
        {
            DelayMs = InitialDelayMs,
            ChaseTTL = ChaseTtl,
            OffsetTTL = OffsetWorkTtl,
            OffsetTicks = OffsetProfitTicks,
            ScratchTTL = ScratchTtl,
            TradeOutTicks = TradeOutTicks,
            TradeOutTTL = TradeOutTtl,
            TradeOutSweepTTL = TradeOutSweepTTL,
            ChasePrice = $"{ChaseIncrement:N2}",
            SpookPrice = $"{SpookPrice:N2}",
            PayupTheoPrice = $"{PayupAdjTheoPx:N2}",
            AdjTheoMode = AdjTheoMode ? 1 : 0,
            SweepOnTradeoutMode = SweepOnTradeoutMode ? 1 : 0,
            MaxDupRetries = MaxDuplicateRetries,
        };
    }
}
