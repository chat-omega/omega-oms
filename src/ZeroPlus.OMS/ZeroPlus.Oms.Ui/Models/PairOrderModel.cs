using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void OrderStatusChangedEventHandler(PairOrderModel order, OrderStatus orderStatus);
    public partial class PairOrderModel : BindableBase
    {
        public event OrderStatusChangedEventHandler OrderStatusChanged;
        public DateTime _SubmitTime;
        public DateTime _LastUpdateTime;
        public PairOrderRequest _OrderRequest;
        public string _ClientOrderId;
        public string _OrderId;
        public string _Symbol;
        public string _Reason;
        public string _TriggerMode;
        public int _Quantity;
        public int _Filled;
        public int _Leaves;
        public OmsOrderUpdateModel _OrderUpdateModel;
        public OrderStatus _OrderStatus;
        public ObservableCollection<PairOrderLegModel> _Legs;
        public PositionEffect _Type;
        public Side _Side;
        public InitSide _InitSide;
        public double _TriggerValue;
        public double _Pnl;
        public double _LastEdge;
        public double _AvgFillPx;
        public double _PxImprovement;
        public double _Bid;
        public double _Mid;
        public double _Ask;
        public double _HighestBid;
        public double _LowestAsk;
        public double _BidEma;
        public double _MidEma;
        public double _AskEma;
        public string _Tag;

        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        private double _stopLoss = double.NaN;

        public PairTriggerModel Trigger { get; set; }
        public PairOrderModel OpeningOrder { get; set; }

        [Bindable]
        public partial DateTime SubmitTime { get; set; }
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [Bindable]
        public partial PairOrderRequest OrderRequest { get; set; }
        [Bindable]
        public partial string ClientOrderId { get; set; }
        [Bindable]
        public partial string OrderId { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string Reason { get; set; }
        [Bindable]
        public partial string TriggerMode { get; set; }
        [Bindable]
        public partial int Quantity { get; set; }
        [Bindable]
        public partial int Filled { get; set; }
        [Bindable]
        public partial int Leaves { get; set; }
        [Bindable]
        public partial OmsOrderUpdateModel OrderUpdateModel { get; set; }
        [Bindable]
        public partial OrderStatus OrderStatus { get; set; }
        [Bindable]
        public partial ObservableCollection<PairOrderLegModel> Legs { get; set; }
        [Bindable]
        public partial PositionEffect Type { get; set; }
        [Bindable]
        public partial Side Side { get; set; }
        [Bindable]
        public partial InitSide InitSide { get; set; }
        [Bindable]
        public partial double TriggerValue { get; set; }
        [Bindable]
        public partial double Pnl { get; set; }
        [Bindable]
        public partial double LastEdge { get; set; }
        [Bindable]
        public partial double AvgFillPx { get; set; }
        [Bindable]
        public partial double PxImprovement { get; set; }
        [Bindable]
        public partial double Bid { get; set; }
        [Bindable]
        public partial double Mid { get; set; }
        [Bindable]
        public partial double Ask { get; set; }
        [Bindable]
        public partial double HighestBid { get; set; }
        [Bindable]
        public partial double LowestAsk { get; set; }
        [Bindable]
        public partial double BidEma { get; set; }
        [Bindable]
        public partial double MidEma { get; set; }
        [Bindable]
        public partial double AskEma { get; set; }
        [Bindable]
        public partial string Tag { get; set; }
        public double StopLoss
        {
            get { return _stopLoss; }
            set { SetValue(ref _stopLoss, value); }
        }

        public PairOrderModel()
        {
            Legs = new ObservableCollection<PairOrderLegModel>();
            AvgFillPx = double.NaN;
            PxImprovement = double.NaN;
            Bid = double.NaN;
            Mid = double.NaN;
            Ask = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            BidEma = double.NaN;
            MidEma = double.NaN;
            AskEma = double.NaN;
            Pnl = double.NaN;
            LastEdge = double.NaN;
        }

        internal void Init(PairOrderRequest pairOrder, Side side, PositionEffect type)
        {
            TriggerValue = pairOrder.TriggerValue;
            OrderRequest = pairOrder;
            ClientOrderId = pairOrder.ClientOrderId;
            Type = type;
            Side = side;
            InitSide = pairOrder.InitSide;
            PairOrderLegModel leg1 = new()
            {
                Side = pairOrder.Leg1Side,
                Symbol = pairOrder.Leg1Symbol,
                Quantity = Math.Abs(pairOrder.Leg1Quantity),
                ClientOrderId = pairOrder.ClientOrderIdLeg1
            };
            Legs.Add(leg1);
            PairOrderLegModel leg2 = new()
            {
                Side = pairOrder.Leg2Side,
                Symbol = pairOrder.Leg2Symbol,
                Quantity = Math.Abs(pairOrder.Leg2Quantity),
                ClientOrderId = pairOrder.ClientOrderIdLeg2
            };
            Legs.Add(leg2);
            OrderUpdateModel = new OmsOrderUpdateModel()
            {
                Side = Side,
                Filled = 0,
                OrderStatus = OrderStatus.PendingNew,
            };
            OrderStatus = OrderStatus.PendingNew;
        }

        public void Update(OrderUpdateValues orderInfoUpdate)
        {
            try
            {
                OrderStatus = orderInfoUpdate.OrderStatus;
                if (!string.IsNullOrEmpty(orderInfoUpdate.Message))
                {
                    Reason = orderInfoUpdate.Message;
                }
                DateTime lastUpdateTime = GetLastUpdate(orderInfoUpdate);
                LastUpdateTime = lastUpdateTime;
                if (orderInfoUpdate.OrderStatus == OrderStatus.New)
                {
                    SubmitTime = lastUpdateTime;
                }

                PairOrderLegModel leg1 = Legs[0];
                PairOrderLegModel leg2 = Legs[1];
                if (orderInfoUpdate.LocalOrderId == leg1.ClientOrderId)
                {
                    leg1.OrderId = orderInfoUpdate.OrderId;
                    leg1.OrderStatus = orderInfoUpdate.OrderStatus;
                    leg1.Filled += orderInfoUpdate.LastQuantity;
                    leg1.Leaves = orderInfoUpdate.LeavesQuantity;
                    leg1.AvgPrice = orderInfoUpdate.AveragePrice;
                    leg1.Reason = orderInfoUpdate.Message;
                    leg1.LastUpdateTime = lastUpdateTime;
                }
                else if (orderInfoUpdate.LocalOrderId == leg2.ClientOrderId)
                {
                    leg2.OrderId = orderInfoUpdate.OrderId;
                    leg2.OrderStatus = orderInfoUpdate.OrderStatus;
                    leg2.Filled += orderInfoUpdate.LastQuantity;
                    leg2.Leaves = orderInfoUpdate.LeavesQuantity;
                    leg2.AvgPrice = orderInfoUpdate.AveragePrice;
                    leg2.Reason = orderInfoUpdate.Message;
                    leg2.LastUpdateTime = lastUpdateTime;
                }
                int filled = Quantity;
                if (leg1.OrderStatus == leg2.OrderStatus)
                {
                    if (orderInfoUpdate.OrderStatus is OrderStatus.Filled or
                        OrderStatus.PartiallyFilled)
                    {
                        List<int> qtyList = Legs.Select(x => Math.Abs(x.Filled)).ToList();
                        int divisor = 1;
                        if (qtyList.Count > 0)
                        {
                            List<int> lcdAdjustedList = Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out divisor);
                        }

                        double avgPx = 0;

                        if (TriggerMode == TriggerMethod.SBS.ToString())
                        {
                            if (leg1.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                            {
                                avgPx += leg1.AvgPrice * leg1.Filled;
                                avgPx -= leg2.AvgPrice * leg2.Filled;
                            }
                            else
                            {
                                avgPx -= leg1.AvgPrice * leg1.Filled;
                                avgPx += leg2.AvgPrice * leg2.Filled;
                            }
                            avgPx /= divisor;
                            PxImprovement = TriggerValue - avgPx;
                        }
                        else if (TriggerMode == TriggerMethod.SSB.ToString())
                        {
                            if (leg1.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                            {
                                avgPx -= leg1.AvgPrice * leg1.Filled;
                                avgPx += leg2.AvgPrice * leg2.Filled;
                            }
                            else
                            {
                                avgPx += leg1.AvgPrice * leg1.Filled;
                                avgPx -= leg2.AvgPrice * leg2.Filled;
                            }
                            avgPx /= divisor;
                            PxImprovement = avgPx - TriggerValue;
                        }

                        AvgFillPx = avgPx;
                        Filled = divisor;
                    }
                    Leaves = Quantity - Filled;

                    Notify();
                }

                OrderUpdateModel = new OmsOrderUpdateModel()
                {
                    Side = Side,
                    Filled = filled,
                    OrderStatus = orderInfoUpdate.OrderStatus,
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, nameof(Update));
            }
        }

        private void Notify()
        {
            try
            {
                OrderStatusChanged?.Invoke(this, OrderStatus);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, nameof(Notify));
            }
        }

        private static DateTime GetLastUpdate(OrderUpdateValues orderInfoUpdate)
        {
            try
            {
                return orderInfoUpdate.LastUpdateTime.FromUtc();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, nameof(GetLastUpdate));
                return default;
            }
        }
    }
}
