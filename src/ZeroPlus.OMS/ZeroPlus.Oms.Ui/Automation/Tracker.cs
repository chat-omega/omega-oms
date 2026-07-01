using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public partial class Tracker : OrderUpdateHandler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private OrderTicket _ticket;

        public OmsCore OmsCore { get; }
        public override OrderSubType? SubType { get; set; } = OrderSubType.Tracker;
        public bool Resting { get; private set; }
        public PriceLevel PriceLevel { get; private set; }
        public int Qty { get; private set; }
        public string LastOrderLocalId { get; private set; }
        public DateTime MainNewTimestamp { get; private set; }
        public bool CancelRequestSent { get; private set; }
        public double LastPx { get; private set; }
        public int LastQty { get; private set; }
        public bool CanAutoCancel { get; private set; }
        public string LastOrderId { get; private set; }
        public bool MainResting { get; private set; }
        public int Counter { get; private set; }
        [Bindable]
        public partial bool IsRunning { get; set; }

        public Tracker(OrderTicket orderTicketViewModelBase)
        {
            _ticket = orderTicketViewModelBase;
            OmsCore = _ticket.OmsCore;
        }

        public void StartTracking(PriceLevel priceLevel, bool resting = false)
        {
            Resting = resting;
            PriceLevel = priceLevel;
            Qty = _ticket.Lcd;
            if (!IsRunning)
            {
                Counter = 0;
                IsRunning = true;
                _ = SubmitOrderAsync();
            }
        }

        internal void CheckForExit()
        {
            double newPrice = GetNewPrice();
            double minIncrement = Math.Abs(Convert.ToDouble(_ticket.PriceIncrement));

            if (!_ticket.IsSingleLegSell)
            {
                if (newPrice > LastPx && newPrice - LastPx >= minIncrement)
                {
                    CancelOrder();
                }
            }
            else
            {
                if (newPrice < LastPx && LastPx - newPrice >= minIncrement)
                {
                    CancelOrder();
                }
            }
        }

        private double GetNewPrice()
        {
            double newPrice = double.NaN;
            switch (PriceLevel)
            {
                case PriceLevel.Mid:
                    newPrice = _ticket.CalculateBidPercent(.50, false).Price;
                    break;
            }

            if (_ticket.PriceNeedsPadding(newPrice))
            {
                bool floorPrice = !_ticket.IsSingleLegSell;
                newPrice = _ticket.PadForNickelOrDime(newPrice, floorPrice);
            }

            return newPrice;
        }

        internal void Stop()
        {
            if (IsRunning)
            {
                CancelOrder();
            }
            IsRunning = false;
        }

        internal void Dispose()
        {
            Stop();
            _ticket = null;
        }

        protected async Task SubmitOrderAsync()
        {
            try
            {
                if (!IsRunning)
                {
                    return;
                }

                if (Counter++ > 50)
                {
                    throw new SlimException("Max Resubmit Reached.");
                }

                bool isClosing = _ticket.IsClosing(false);
                _ticket.CheckForPosEffect(false, isClosing);
                _ticket.ValidateAccount();
                double price = GetNewPrice();

                if (_ticket.Legs.Count == 0)
                {
                    throw new SlimException("No valid legs.");
                }
                else if (Qty == 0)
                {
                    throw new SlimException("Invalid Qty.");
                }
                else if (double.IsNaN(price))
                {
                    throw new SlimException("Invalid Price.");
                }

                var orderInfo = _ticket.BuildOrder(false, SubType);
                orderInfo.Price = price;
                orderInfo.Qty = Qty;

                if (Resting)
                {
                    orderInfo.SetCancelDelay(0);
                }

                MainNewTimestamp = default;
                CancelRequestSent = true;
                LastPx = orderInfo.Price;
                LastQty = orderInfo.Qty;
                orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                LastOrderLocalId = orderInfo.LocalID;
                CanAutoCancel = !OmsCore.Config.NonAutoCancelRoutes.Contains(orderInfo.Route);
                LastOrderId = await OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, tags: null);
                MainResting = true;
                _log.Info($"Order submitted. OrderId: {_ticket.OrderId}, {_ticket.GetStats()}");
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                _ticket.Reason = ex.Message;
                _ticket.ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                _ticket.Reason = ex.Message;
                _ticket.ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (RouteSelectionException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
            }
        }

        private void CancelOrder()
        {
            try
            {
                if (!CancelRequestSent)
                {
                    var timeSpan = (int)(DateTime.Now - MainNewTimestamp).TotalMilliseconds;
                    var restPeriod = _ticket.GetMinRestPeriod();
                    if (MainNewTimestamp == default)
                    {
                        _log.Warn("Premature cancel detected. Spread: " + _ticket.SpreadId + ", TimeSpan: " + TimeSpan.Zero + ", Delay: " + restPeriod);
                        if (restPeriod > 0)
                        {
                            Task.Delay(restPeriod).ContinueWith(t => SendCancel());
                        }
                        return;
                    }
                    if (timeSpan < restPeriod)
                    {
                        var delay = restPeriod - timeSpan;
                        _log.Warn("Premature cancel detected. Spread: " + _ticket.SpreadId + ", TimeSpan: " + timeSpan + ", Delay: " + delay);
                        if (delay > 0)
                        {
                            Task.Delay(delay).ContinueWith(t => SendCancel());
                        }
                        return;
                    }

                    SendCancel();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelOrder));
            }
        }

        private void SendCancel()
        {
            if (CanAutoCancel && !string.IsNullOrWhiteSpace(LastOrderId))
            {
                CancelRequestSent = true;
                OmsCore.OrderClient.CancelOrder(new CancelRequest
                {
                    OrderId = LastOrderId,
                    Venue = _ticket.Venue,
                    LocalId = _ticket.LocalId,
                    PermId = _ticket.PermID,
                    Account = _ticket.Account,
                    UserId = _ticket.UserId,
                    RiskCheckId = _ticket.RiskCheckId
                });
            }
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;

                if (execReport.ClientOrderId != LastOrderLocalId || orderStatus is not OrderStatus status)
                {
                    return;
                }

                if (executionType != null && executionType.Value.IsFilled())
                {
                    int lastQuantity = execReport.LastQty;
                    var qty = _ticket.Lcd - lastQuantity;
                    Qty = qty;
                    if (qty > 0)
                    {
                        _ticket.UpdateQty(qty);
                    }
                    else
                    {
                        IsRunning = false;
                    }
                }

                if (status.IsClosed())
                {
                    CancelRequestSent = true;
                    MainResting = false;
                }

                switch (status)
                {
                    case OrderStatus.New:
                        MainNewTimestamp = receiveTime;
                        CancelRequestSent = false;
                        MainResting = true;
                        break;
                    case OrderStatus.Canceled:
                    case OrderStatus.Rejected:
                        _ = SubmitOrderAsync();
                        break;
                    case OrderStatus.Filled:
                        IsRunning = false;
                        break;
                }

                OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);
                _ticket.OrderStatus = status;
                _ticket.Status = orderUpdateValues.Status;
                _ticket.StatusMode = orderUpdateValues.StatusMode;
                _ticket.Filled = orderUpdateValues.Filled >= 0 ? orderUpdateValues.Filled.ToString() : "";
                _ticket.LastQuantity = orderUpdateValues.LastQuantity;
                _ticket.CumulativeQty = orderUpdateValues.CumQuantity;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleExecutionReport));
            }
        }

        public override void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            _log.Warn($"Order: {orderCancelReject}");
        }
    }
}
