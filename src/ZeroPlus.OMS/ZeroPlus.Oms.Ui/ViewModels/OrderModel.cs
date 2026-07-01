using System;
using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class OrderModel : BindableBase
    {
        public string LocalId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; }
        public Side Side { get; set; }
        public int Qty { get; set; }
        public double Price { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Ask { get; set; }
        public double UnderBid { get; set; }
        public double UnderMid { get; set; }
        public double UnderAsk { get; set; }
        public double TotalDelta { get; set; }
        public double NetDelta { get; set; }
        [Bindable]
        public partial OmsOrderUpdateModel OrderUpdateModel { get; set; }
        [Bindable]
        public partial OrderStatus? OrderStatus { get; set; }
    }
}
