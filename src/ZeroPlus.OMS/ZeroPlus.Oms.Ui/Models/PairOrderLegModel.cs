using DevExpress.Mvvm;
using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class PairOrderLegModel : BindableBase
    {
        public DateTime _LastUpdateTime;
        public string _ClientOrderId;
        public string _OrderId;
        public string _Symbol;
        public string _Reason;
        public int _Quantity;
        public int _Filled;
        public int _Leaves;
        public double _AvgPrice;
        public OrderStatus _OrderStatus;
        public Side _Side;

        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [Bindable]
        public partial string ClientOrderId { get; set; }
        [Bindable]
        public partial string OrderId { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string Reason { get; set; }
        [Bindable]
        public partial int Quantity { get; set; }
        [Bindable]
        public partial int Filled { get; set; }
        [Bindable]
        public partial int Leaves { get; set; }
        [Bindable]
        public partial double AvgPrice { get; set; }
        [Bindable]
        public partial OrderStatus OrderStatus { get; set; }
        [Bindable]
        public partial Side Side { get; set; }

        public PairOrderLegModel()
        {
            AvgPrice = double.NaN;
        }
    }
}
