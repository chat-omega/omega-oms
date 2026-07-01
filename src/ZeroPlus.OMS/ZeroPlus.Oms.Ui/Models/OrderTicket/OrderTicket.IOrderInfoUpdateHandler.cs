using System;
using System.Threading.Tasks;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Ui.Models;

public abstract partial class OrderTicket : IOrderInfoUpdateHandler
{
    public void OrderInfoUpdated(OrderInfoUpdate update)
    {

    }
    /// <summary>
    /// Processes an order status update and triggers relevant notifications and calculations.
    /// </summary>
    /// <param name="orderUpdate">Contains updated values and state for the order</param>
    /// <remarks>
    /// 1. Updates the UI with new order status
    /// 2. Handles order completion notifications for filled/canceled/rejected orders
    /// 3. Processes trade events for partial fills
    /// 4. Manages basket order specific logic
    /// 5. Triggers edge calculations for non-basket orders
    /// 6. Updates notification system based on configuration
    /// </remarks>
    /// <seealso cref="OrderStatus"/>
    /// <seealso cref="OmsOrder"/>
    public void OrderUpdated(OrderUpdateValues orderUpdate)
    {
        try
        {
            IsLooping = orderUpdate.IsLooping;
            AutomationRunning = orderUpdate.AutomationRunning;

            var isActive = !orderUpdate.OrderStatus.IsClosed();
            if (orderUpdate.IsMainOrder)
            {
                MainResting = isActive;
                IsModifyEnabled = isActive;
            }
            else if (orderUpdate.IsContraOrder)
            {
                ContraResting = isActive;
                IsContraModifyEnabled = isActive;
            }

            UpdateOrderStatus(orderUpdate, orderUpdate.OrderStatus);
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(orderUpdate));
        }
    }

    public void AutomationStateChanged(bool running)
    {
        AutomationRunning = running;
        if (!running)
        {
            IsLooping = false;
            IsFreeLooking = false;
        }
    }

    private void UpdateOrderStatus(OrderUpdateValues orderUpdateValues, OrderStatus status)
    {
        if (IsDisposed)
        {
            return;
        }
        OrderStatus = status;

        if (status == OrderStatus.New && orderUpdateValues.IsMainOrder)
        {
            OmsCore.OrderLifecycleService.Complete(orderUpdateValues);
        }
        OmsOrder order = UpdateUiStatus(orderUpdateValues);
        bool isMainOrder = IsMainOrder(orderUpdateValues);
        bool isContraOrder = IsContraOrder(orderUpdateValues);
        if (status is OrderStatus.Canceled or OrderStatus.Rejected && isMainOrder && orderUpdateValues.CumQuantity == 0)
        {
            NotifyOrderCloseWaitHandlers(main: true, status);
        }
        if (orderUpdateValues.LastQuantity > 0)
        {
            Task.Run(() => TradeEvent?.Invoke(this, order));
            if (isMainOrder)
            {
                AveragePrice = orderUpdateValues.AveragePrice;
                LastMainUnderMidAtFill = orderUpdateValues.UnderlyingMidPrice;
            }
            else if (isContraOrder)
            {
                ContraAveragePrice = orderUpdateValues.AveragePrice;
                LastContraUnderMidAtFill = orderUpdateValues.UnderlyingMidPrice;
            }
        }
        if (isMainOrder && status.IsClosed() && orderUpdateValues.CumQuantity > 0)
        {
            Task.Run(() => OrderFilledUpdatedEvent?.Invoke(this, status));
        }
        if (IsBasketOrder && orderUpdateValues.RequiresManualIntervention && !BasketSettings.OpenTicketForFills)
        {
            BasketTraderViewModel?.CreateComplexOrderTicket(this, null, true);
        }
        if (!IsBasketOrder)
        {
            EdgeProjector?.CalculateEdge();
        }

        if (OmsCore.Config.NotificationsForMyOrdersOnly && OmsCore.HerculesClientConfig.TransactionSubscriptionMode is TransactionSubscriptionMode.Fills or TransactionSubscriptionMode.OwnAndFills)
        {
            var notified = _notificationManager.AddOrder(this);
            if (!notified)
            {
                if (order.LastQuantity > 0 && order.LeavesQuantity > 0)
                {
                    _notificationManager.NotifyPartialFill(this);
                }
                else if (order.LastQuantity > 0 && order.LeavesQuantity == 0)
                {
                    _notificationManager.NotifyFill(this);
                }
            }
        }
    }
}

