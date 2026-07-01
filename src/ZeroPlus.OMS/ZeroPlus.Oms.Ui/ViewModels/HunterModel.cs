using DevExpress.Mvvm;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.LowLatency.Ext;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class HunterModel : ViewModelBase
    {

        [JsonProperty]
        [Bindable]
        public partial bool UseSignalPrice { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool LiquidateOnlyWhenComplete { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool UseSignalExchange { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int WorkTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int HuntTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int PayupTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MinEnterTicks { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LeanModel LeanModel { get; set; }

        public HunterModel()
        {
            LeanModel = new LeanModel();
        }

        public MsgRequests.jsonRequestExecutionHunterParams JsonRequestExecutionHunterParams(LoopModel loopModel)
        {
            return new MsgRequests.jsonRequestExecutionHunterParams
            {
                HuntTTL = HuntTtl,
                WorkTTL = WorkTtl,
                PayupTicks = PayupTicks,
                LoopDelayTTL = loopModel.LoopDelay,
                LoopPayupTicks0 = loopModel.LoopTable[0].PayupTicks,
                LoopPayupTicks1 = loopModel.LoopTable[1].PayupTicks,
                LoopPayupTicks2 = loopModel.LoopTable[2].PayupTicks,
                LoopPayupTicks3 = loopModel.LoopTable[3].PayupTicks,
                LoopPayupTicks4 = loopModel.LoopTable[4].PayupTicks,
                LoopPayupQty0 = loopModel.LoopTable[0].LoopQty,
                LoopPayupQty1 = loopModel.LoopTable[1].LoopQty,
                LoopPayupQty2 = loopModel.LoopTable[2].LoopQty,
                LoopPayupQty3 = loopModel.LoopTable[3].LoopQty,
                LoopPayupQty4 = loopModel.LoopTable[4].LoopQty,
                LoopProfit0 = $"{loopModel.LoopTable[0].Profit:N2}",
                LoopProfit1 = $"{loopModel.LoopTable[1].Profit:N2}",
                LoopProfit2 = $"{loopModel.LoopTable[2].Profit:N2}",
                LoopProfit3 = $"{loopModel.LoopTable[3].Profit:N2}",
                LoopProfit4 = $"{loopModel.LoopTable[4].Profit:N2}",
                UseSignalPrice = UseSignalPrice ? 1 : 0,
                LiquidateOnlyWhenComplete = LiquidateOnlyWhenComplete ? 1 : 0,
                UseSignalExchange = UseSignalExchange ? 1 : 0,
                Lean = LeanModel.JsonRequestLeanBaseParams
            };
        }
    }
}
