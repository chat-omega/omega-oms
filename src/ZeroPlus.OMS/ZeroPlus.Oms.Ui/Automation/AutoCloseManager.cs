using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Automation
{
    public partial class AutoCloseManager : OrderUpdateHandler
    {
        private readonly object _lock = new();
        private OrderTicket _ticket;
        private string _lastOrderId;

        public override OrderSubType? SubType { get; set; } = OrderSubType.AutoClose;

        [Bindable]
        public partial bool IsRunning { get; set; }

        public bool IsDisposed { get; set; }
        public bool CanSend { get; private set; }

        public AutoCloseManager(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
            CanSend = true;
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;
                if (!IsRunning || IsDisposed || execReport.ClientOrderId != _lastOrderId || orderStatus == null || executionType == null)
                {
                    return;
                }

                switch (orderStatus)
                {
                    case OrderStatus.Canceled:
                    case OrderStatus.Rejected:
                    case OrderStatus.Filled:
                        CanSend = true;
                        break;
                }

                Side side = _ticket.Side == Side.Buy ? Side.Sell : Side.Buy;
                if (executionType.Value.IsFilled())
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

        internal async void InitiateExit(double closePrice, int closeQty)
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
                    CanSend = false;
                    _ticket.AutoCloseArmed = false;
                }
            }

            var order = _ticket.BuildOrder(isContra: true, SubType, Math.Abs(closeQty));
            bool floorPrice = !_ticket.IsSingleLeg || (_ticket.Side == Side.Sell);
            order.Price = _ticket.PriceNeedsPadding(closePrice) ? _ticket.PadForNickelOrDime(closePrice, floorPrice) : Math.Round(closePrice, 2, MidpointRounding.AwayFromZero);
            _lastOrderId = _ticket.OmsCore.OrderClient.GetNextOrderId();
            order.LocalID = _lastOrderId;
            await _ticket.OmsCore.OrderClient.SendOrderAsync(order, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
            int qty = _ticket.Side == Side.Sell ? order.Qty : -order.Qty;
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
