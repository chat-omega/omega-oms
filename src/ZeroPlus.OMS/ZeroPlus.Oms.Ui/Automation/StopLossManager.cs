using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public partial class StopLossManager : OrderUpdateHandler
    {
        private readonly object _lock = new();
        private OrderTicket _ticket;
        private string _lastOrderId;

        public override OrderSubType? SubType { get; set; } = OrderSubType.StopLoss;

        [Bindable]
        public partial bool IsRunning { get; set; }

        public bool IsDisposed { get; set; }
        public int Attempts { get; set; }
        public int AttemptCount { get; set; }
        public double Interval { get; set; }
        public double Increment { get; set; }
        public double BidPercent { get; private set; }

        public StopLossManager(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;
                if (!IsRunning || IsDisposed || execReport.ClientOrderId != _lastOrderId)
                {
                    return;
                }

                if (executionType != null && executionType.Value.IsFilled())
                {
                    lock (_ticket.PositionUpdateLock)
                    {
                        _ticket.SpreadPosition = 0;
                    }
                }

                switch (orderStatus)
                {
                    case OrderStatus.Canceled:
                    case OrderStatus.Rejected:
                        SendOrder();
                        break;
                    case OrderStatus.Filled:
                        IsRunning = false;
                        break;
                }

                var side = IsMainOrder() ? _ticket.Side : _ticket.Side == Side.Buy ? Side.Sell : Side.Buy;
                if (orderStatus is OrderStatus.Filled or OrderStatus.PartiallyFilled)
                {
                    int lastQuantity = execReport.LastQty;
                    int qty = side == Side.Buy ? lastQuantity : -lastQuantity;

                    lock (_ticket.PositionUpdateLock)
                    {
                        _ticket.SingleOrderTicketPosition += qty;
                        _ticket.SingleOrderTicketWorkingPosition -= qty;
                    }
                }
                else if (orderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                {
                    int leavesQty = execReport.Qty - execReport.CumQty;
                    int qty = side == Side.Buy ? leavesQty : -leavesQty;
                    lock (_ticket.PositionUpdateLock)
                    {
                        _ticket.SingleOrderTicketWorkingPosition -= qty;
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

        internal void InitiateExit(int attemptCount, double interval, double increment, double bidPercent)
        {
            lock (_lock)
            {
                if (IsRunning || IsDisposed)
                {
                    return;
                }
                else
                {
                    IsRunning = true;
                }
            }
            Attempts = 0;
            AttemptCount = attemptCount;
            Interval = interval;
            Increment = increment;
            BidPercent = bidPercent;
            SendOrder();
        }

        private async void SendOrder()
        {
            if (Attempts++ < AttemptCount)
            {
                if (IsMainOrder())
                {
                    var order = _ticket.BuildOrder(isContra: false, SubType, Math.Abs(_ticket.SpreadPosition));
                    double cancelDelay = Interval;

                    if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    order.SetCancelDelay(cancelDelay);
                    order.Price = _ticket.CalculateBidPercent(BidPercent, overrideEdge: false).Price;
                    _lastOrderId = _ticket.OmsCore.OrderClient.GetNextOrderId();
                    order.LocalID = _lastOrderId;
                    await _ticket.OmsCore.OrderClient.SendOrderAsync(order, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);

                    int qty = _ticket.Side == Side.Buy ? order.Qty : -order.Qty;
                    lock (_ticket.PositionUpdateLock)
                    {
                        _ticket.SingleOrderTicketWorkingPosition += qty;
                    }
                }
                else if (IsContra())
                {
                    var order = _ticket.BuildOrder(isContra: true, SubType, Math.Abs(_ticket.SpreadPosition));
                    double cancelDelay = Interval;

                    if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    order.SetCancelDelay(cancelDelay);
                    order.Price = _ticket.CalculateBidPercent(BidPercent, overrideEdge: false).ContraPrice;
                    _lastOrderId = _ticket.OmsCore.OrderClient.GetNextOrderId();
                    order.LocalID = _lastOrderId;
                    await _ticket.OmsCore.OrderClient.SendOrderAsync(order, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);

                    int qty = _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell ? order.Qty : -order.Qty;
                    lock (_ticket.PositionUpdateLock)
                    {
                        _ticket.SingleOrderTicketWorkingPosition += qty;
                    }
                }
            }
            else
            {
                Stop();
            }
        }

        private bool IsMainOrder()
        {
            return (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy && _ticket.SpreadPosition < 0) || (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell && _ticket.SpreadPosition > 0);
        }

        private bool IsContra()
        {
            return (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy && _ticket.SpreadPosition > 0) || (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell && _ticket.SpreadPosition < 0);
        }

        internal void Stop()
        {
            IsRunning = false;
        }

        internal void Dispose()
        {
            IsRunning = false;
            _ticket = null;
            IsDisposed = true;
        }
    }
}
