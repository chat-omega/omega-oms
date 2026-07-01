using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Ui.Models;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Automation
{
    public partial class StopOrderManager : OrderUpdateHandler
    {
        private readonly object _lock = new();
        private OrderTicket _ticket;
        private string _lastOrderId;

        public override OrderSubType? SubType { get; set; } = OrderSubType.Stop;

        [Bindable]
        public partial bool IsRunning { get; set; }

        [Bindable]
        public partial int WorkingPosition { get; set; }

        public bool IsDisposed { get; set; }
        public double Interval { get; set; }
        public double Increment { get; set; }
        public double BidPercent { get; set; }
        public int Qty { get; set; }
        public Side StopSide { get; private set; }
        public int Attempt { get; private set; }

        public StopOrderManager(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;

                if (!IsRunning || IsDisposed || execReport.ClientOrderId != _lastOrderId)
                {
                    return;
                }

                switch (orderStatus)
                {
                    case OrderStatus.Canceled:
                    case OrderStatus.Rejected:
                        SendOrder();
                        break;
                    case OrderStatus.Filled:
                        Stop();
                        break;
                }

                Side? side = IsMainOrder() ? _ticket.Side : _ticket.Side == Side.Buy ? Side.Sell : Side.Buy;
                if (orderStatus is OrderStatus.Filled or OrderStatus.PartiallyFilled)
                {
                    int lastQuantity = execReport.LastQty;
                    int qty = side == Side.Buy ? lastQuantity : -lastQuantity;

                    lock (_lock)
                    {
                        WorkingPosition -= qty;
                        if (Qty >= Math.Abs(qty))
                        {
                            Qty -= Math.Abs(qty);
                        }
                        else
                        {
                            Stop();
                        }
                    }
                }
                else if (orderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                {
                    int leavesQty = execReport.Qty - execReport.CumQty;
                    int qty = side == Side.Buy ? leavesQty : -leavesQty;

                    lock (_lock)
                    {
                        WorkingPosition -= qty;
                    }
                }

                OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

                _ticket.ContraOrderStatus = orderStatus;
                _ticket.ContraStatus = orderUpdateValues.Status;
                _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
                _ticket.ContraFilled = orderUpdateValues.Filled >= 0 ? orderUpdateValues.Filled.ToString() : "";
                _ticket.ContraLastQuantity = orderUpdateValues.LastQuantity;
                _ticket.ContraCumulativeQty = orderUpdateValues.CumQuantity;
                _ticket.IsContraSubmitEnabled = orderUpdateValues.IsSubmitEnabled;
                _ticket.IsContraCancelEnabled = orderUpdateValues.IsCancelEnabled;
                _ticket.IsContraModifyEnabled = orderUpdateValues.IsModifyEnabled;
            }
            catch (Exception) { }
        }

        internal void Start(Side stopLossSide, int qty)
        {
            lock (_lock)
            {
                if (!_ticket.StopLossEnabled || IsRunning || IsDisposed)
                {
                    return;
                }
                else
                {
                    IsRunning = true;
                }
            }
            Qty = qty;
            StopSide = stopLossSide;
            Attempt = 0;
            SendOrder();
        }

        private async void SendOrder()
        {
            if (!_ticket.StopLossEnabled || !IsRunning || IsDisposed)
            {
                return;
            }

            OpsOrderModel order;
            double price;
            double bid;
            double ask;

            if (IsMainOrder())
            {
                order = _ticket.BuildOrder(isContra: false, SubType, Qty);
                price = _ticket.CalculateBidPercent(BidPercent, overrideEdge: false).Price;
                price += Attempt++ * GetIncrement(price);
                bid = _ticket.Low;
                ask = _ticket.High;
            }
            else if (IsContra())
            {
                order = _ticket.BuildOrder(isContra: true, SubType, Qty);
                price = _ticket.CalculateBidPercent(BidPercent, overrideEdge: false).ContraPrice;
                price += Attempt++ * GetIncrement(price);
                bid = _ticket.IsSingleLeg ? _ticket.Low : -_ticket.High;
                ask = _ticket.IsSingleLeg ? _ticket.High : -_ticket.Low;
            }
            else
            {
                return;
            }

            bool triggerPassed = (StopSide == Side.Buy && _ticket.Low > _ticket.StopLossCloseTriggerPrice) ||
                                 (StopSide == Side.Sell && _ticket.High < _ticket.StopLossCloseTriggerPrice);

            double ext = triggerPassed ? Math.Abs(ask - bid) : 0.0;

            if (_ticket.IsSingleLeg)
            {
                if (StopSide == Side.Buy)
                {
                    if (price > ask + ext)
                    {
                        price = ask + ext;
                    }
                }
                else
                {
                    if (price < bid - ext)
                    {
                        price = bid - ext;
                    }
                }
            }
            else
            {
                if (price > ask + ext)
                {
                    price = ask + ext;
                }
            }

            _lastOrderId = _ticket.OmsCore.OrderClient.GetNextOrderId();
            bool floorPrice = !_ticket.IsSingleLeg || StopSide != Side.Sell;
            order.Price = _ticket.PriceNeedsPadding(price) ? _ticket.PadForNickelOrDime(price, floorPrice) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
            order.LocalID = _lastOrderId;
            double cancelDelay = _ticket.StopLossInterval;

            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            order.SetCancelDelay(cancelDelay);
            await _ticket.OmsCore.OrderClient.SendOrderAsync(order, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
            lock (_lock)
            {
                WorkingPosition += StopSide == Side.Buy ? Qty : -Qty;
            }
        }

        private double GetIncrement(double basePrice)
        {
            if (_ticket.IsSingleLeg)
            {
                if (StopSide == Side.Buy)
                {
                    return Math.Abs(Math.Round(Math.Max(_ticket.StopLossIncrement, (double)_ticket.GetPriceIncrement(basePrice, IncrementDirection.Up)), 2));
                }
                else
                {
                    return -Math.Abs(Math.Round(Math.Max(_ticket.StopLossIncrement, (double)_ticket.GetPriceIncrement(basePrice, IncrementDirection.Down)), 2));
                }
            }
            else
            {
                return Math.Abs(Math.Round(Math.Max(_ticket.StopLossIncrement, (double)_ticket.GetPriceIncrement(basePrice, IncrementDirection.Down)), 2));
            }
        }

        private bool IsMainOrder()
        {
            return (StopSide == Side.Buy && _ticket.Side == Side.Buy) ||
                   (StopSide == Side.Sell && _ticket.Side == Side.Sell);
        }

        private bool IsContra()
        {
            return (StopSide == Side.Buy && _ticket.Side == Side.Sell) ||
                   (StopSide == Side.Sell && _ticket.Side == Side.Buy);
        }

        internal void Stop()
        {
            IsRunning = false;
            if (_ticket != null)
            {
                _ticket.StopLossEnabled = false;
            }
        }

        internal void Dispose()
        {
            IsRunning = false;
            IsDisposed = true;
            if (_ticket != null)
            {
                _ticket.StopLossEnabled = false;
                _ticket = null;
            }
        }
    }
}
