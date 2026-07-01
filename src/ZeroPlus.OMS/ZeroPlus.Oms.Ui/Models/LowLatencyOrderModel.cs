using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LowLatencyOrderModel : BindableBase
    {

        [Bindable]
        public partial string LastUpdateTime { get; set; }
        [Bindable]
        public partial string Underlying { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial double OrderPrice { get; set; }
        [Bindable]
        public partial string FillPrice { get; set; }
        [Bindable]
        public partial int Qty { get; set; }
        [Bindable]
        public partial int FillQty { get; set; }
        [Bindable]
        public partial int RemOrderQty { get; set; }
        [Bindable]
        public partial double Latency { get; set; }
        [Bindable]
        public partial string What { get; set; }
        [Bindable]
        public partial string Who { get; set; }
        [Bindable]
        public partial string UserName { get; set; }
        [Bindable]
        public partial string UserId { get; set; }
        [Bindable]
        public partial string StratType { get; set; }
        [Bindable]
        public partial string StratName { get; set; }
        [Bindable]
        public partial string StratId { get; set; }
        [Bindable]
        public partial string StratIdInResponseTo { get; set; }
        [Bindable]
        public partial string SignalInstance { get; set; }
        [Bindable]
        public partial string ClOrdId { get; set; }
        [Bindable]
        public partial string DiffMillis { get; set; }
        [Bindable]
        public partial string ExecutedExchange { get; set; }
        [Bindable]
        public partial string Error { get; set; }
        [Bindable]
        public partial string ResponseToPrice { get; set; }
        [Bindable]
        public partial string ResponseToClOrdId { get; set; }
        [Bindable]
        public partial string OrderExtra { get; set; }
        [Bindable]
        public partial string Nbbo { get; set; }
        [Bindable]
        public partial OmsOrderUpdateModel OrderUpdateModel { get; set; }
        [Bindable]
        public partial OrderStatus OrderStatus { get; set; }
        [Bindable]
        public partial Side Side { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Pnl { get; set; }
        [Bindable]
        public partial int Dte { get; set; }
        [Bindable]
        public partial double EdgeToTheo { get; set; }
        [Bindable]
        public partial double PctBid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double BidCount { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double BidQty { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double BidPx { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AskCount { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AskQty { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AskPx { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Width { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalBid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalAsk { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalWidth { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalTrade { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalPctBid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalEdgeToTheo { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalLatency { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double SignalDeltaAdjTheo { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderBid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnderAsk { get; set; }
    }
}
