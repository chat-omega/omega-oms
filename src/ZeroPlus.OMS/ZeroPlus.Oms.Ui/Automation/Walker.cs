using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class Walker : OrderUpdateHandler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly OmsCore _omsCore;
        private readonly IAutomation _automation;


        private readonly OrderTicket _ticket;


        public sealed override OrderSubType? SubType { get; set; }
        public double Multiplier { get; set; }

        internal WalkerOrderStateManager MainOrder { get; }
        internal WalkerOrderStateManager SecondaryOrder { get; }
        internal WalkerOrderStateManager TertiaryOrder { get; }

        public int Index { get; set; }
        public double Spacing { get; set; }
        public bool IsDisposed { get; set; }

        public Walker(OrderTicket ticket, IAutomation automation, OmsCore omsCore, OrderSubType? type, PxCalculator order, double multiplier) : this(ticket, automation, omsCore, type, order, null, multiplier)
        {
        }

        public Walker(OrderTicket ticket, IAutomation automation, OmsCore omsCore, OrderSubType? type, PxCalculator order, PxCalculator secondaryOrder, double multiplier) : this(ticket, automation, omsCore, type, order, secondaryOrder, null, multiplier)
        {
        }

        public Walker(OrderTicket ticket, IAutomation automation, OmsCore omsCore, OrderSubType? type, PxCalculator order, PxCalculator secondaryOrder, PxCalculator thirdOrder, double multiplier)
        {
            _omsCore = omsCore;
            _ticket = ticket;
            _automation = automation;
            SubType = type;
            Multiplier = multiplier;
            if (order != null)
            {
                MainOrder = new(order);
            }
            if (secondaryOrder != null)
            {
                SecondaryOrder = new(secondaryOrder);
            }
            if (thirdOrder != null)
            {
                TertiaryOrder = new(thirdOrder);
            }
        }

        public override async void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            OrderStatus? status = execReport.OrderStatus;
            ExecutionType? executionType = execReport.ExecutionType;

            if (IsDisposed)
            {
                return;
            }

            string localOrderId = execReport.ClientOrderId;
            if (localOrderId == MainOrder.LastOrderId)
            {
                var isSingleLeg = MainOrder.PriceCalculator.Order.Legs.Count <= 1;
                var lastQty = execReport.LastQty;
                var leavesQty = execReport.LeavesQty;
                var cumlQty = execReport.CumQty;

                if (executionType.Value.IsFilled())
                {
                    MainOrder.PriceCalculator.Order.Qty -= lastQty;
                }

                switch (status)
                {
                    case OrderStatus.Canceled when cumlQty == 0:
                        _ = _automation.ContinueAsync(this);
                        break;
                    case OrderStatus.Canceled when leavesQty == 0:
                    case OrderStatus.Filled:
                        if (SecondaryOrder != null)
                        {
                            await SendSecondaryAsync();
                        }
                        else
                        {
                            Stop();
                        }
                        break;
                    case OrderStatus.Canceled:
                        Stop();
                        break;
                }
            }
            else if (SecondaryOrder != null && localOrderId == SecondaryOrder.LastOrderId)
            {
                var isSingleLeg = SecondaryOrder.PriceCalculator.Order.Legs.Count <= 1;
                var lastQty = execReport.LastQty;
                var leavesQty = execReport.LeavesQty;

                if (executionType.Value.IsFilled())
                {
                    SecondaryOrder.PriceCalculator.Order.Qty -= lastQty;
                }

                switch (status)
                {
                    case OrderStatus.Canceled when SecondaryOrder.PriceCalculator.Order.Qty > 0:
                        await SendSecondaryAsync();
                        break;
                    case OrderStatus.Canceled when leavesQty == 0:
                    case OrderStatus.Filled:
                        if (TertiaryOrder != null)
                        {
                            await SendThirdOrderAsync();
                        }
                        else
                        {
                            Stop();
                        }
                        break;
                    case OrderStatus.Canceled:
                        Stop();
                        break;
                }
            }
            else if (TertiaryOrder != null && localOrderId == TertiaryOrder.LastOrderId)
            {
                var isSingleLeg = TertiaryOrder.PriceCalculator.Order.Legs.Count <= 1;
                var lastQty = execReport.LastQty;

                if (executionType.Value.IsFilled())
                {
                    TertiaryOrder.PriceCalculator.Order.Qty -= lastQty;
                }

                switch (status)
                {
                    case OrderStatus.Canceled when TertiaryOrder.PriceCalculator.Order.Qty > 0:
                        await SendThirdOrderAsync();
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.Filled:
                        Stop();
                        break;
                }
            }

            _automation.ShowStatus(execReport, status.Value);
        }

        internal async Task<bool> SendPrimaryOrder()
        {
            if (!await SetupOrder(MainOrder))
            {
                Stop();
                return false;
            }

            return true;
        }

        private async Task<bool> SendSecondaryAsync()
        {
            if (!await SetupOrder(SecondaryOrder))
            {
                Stop();
                return false;
            }

            return true;
        }

        private async Task<bool> SendThirdOrderAsync()
        {
            if (!await SetupOrder(TertiaryOrder))
            {
                Stop();
                return false;
            }

            return true;
        }

        private async Task<bool> SetupOrder(WalkerOrderStateManager manager)
        {
            if (IsDisposed || manager == null)
            {
                return false;
            }

            var order = manager.PriceCalculator.Order;
            if (order.Qty == 0)
            {
                return false;
            }

            if (manager.StopPriceCalculator == null)
            {
                if (manager.OrderResubmitCount++ > manager.OrderMaxResubmit)
                {
                    Stop();
                    return false;
                }
            }

            manager.LastOrderId = _omsCore.OrderClient.GetNextOrderId();
            order.LocalID = manager.LastOrderId;

            Func<double> pxLoader = manager.NextPriceCalculator;
            Func<double> stopPxLoader = manager.StopPriceCalculator;


            double nextPx = pxLoader();

            var isSingleLeg = order.Legs.Count <= 1;
            if (isSingleLeg && order.OMSSide == "SELL")
            {
                nextPx -= _automation.Increment * manager.IncrementCounter++;
                var stopPx = stopPxLoader?.Invoke();
                if (nextPx < stopPx)
                {
                    return false;
                }
                order.Price = _ticket.PriceNeedsPadding(nextPx) ? _ticket.PadForNickelOrDime(nextPx, false) : Math.Round(nextPx, 2);
            }
            else
            {
                nextPx += (order.Legs.Count > 1 ? (double)_ticket.GetSpreadIncrement() : _automation.Increment) * manager.IncrementCounter++;
                var stopPx = stopPxLoader?.Invoke();
                if (nextPx > stopPx)
                {
                    return false;
                }
                order.Price = _ticket.PriceNeedsPadding(nextPx) ? _ticket.PadForNickelOrDime(nextPx, true) : Math.Round(nextPx, 2);
            }
            order.SetCancelDelay(_automation.Interval);
            var setupOrder = !double.IsNaN(order.Price) || (isSingleLeg && order.Price == 0);
            if (setupOrder)
            {
                await _omsCore.OrderClient.SendOrderAsync(order, _ticket.GetInstanceMode(), this, false, Multiplier, false);
            }

            return setupOrder;
        }

        private void Stop()
        {
            _automation.Stop();
            _ticket.IsLooping = false;
        }

        internal void Dispose()
        {
            try
            {
                IsDisposed = true;
                MainOrder?.Dispose();
                SecondaryOrder?.Dispose();
                TertiaryOrder?.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }
    }
}
