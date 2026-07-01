using System;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class SweepCloser : OrderUpdateHandler, IOmsOrderUpdateSubscriber
    {
        private readonly object _lock = new();
        private OrderTicket _ticket;
        private string _lastOrderId;

        private bool _canSend;
        private AutomationConfigModel _automationConfig;
        private int _filledQty;
        private double _fillPx;

        public override OrderSubType? SubType { get; set; } = OrderSubType.Sweep;

        public bool IsDisposed { get; set; }

        public SweepCloser(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;

                if (IsDisposed || execReport.ClientOrderId != _lastOrderId)
                {
                    return;
                }

                if (executionType != null && executionType.Value.IsFilled())
                {
                    int lastQuantity = execReport.LastQty;

                    lock (_lock)
                    {
                        _filledQty = Math.Max(_filledQty - lastQuantity, 0);
                        _fillPx = _ticket.IsSingleLeg ? execReport.AvgPrice : -execReport.AvgPrice;
                        if (_filledQty == 0)
                        {
                            _canSend = false;
                        }
                    }
                }

                switch (orderStatus)
                {
                    case OrderStatus.Filled:
                    case OrderStatus.Canceled:
                    case OrderStatus.Rejected:
                        _canSend = _filledQty > 0;
                        break;
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

        public void CheckForExit()
        {
            if (_canSend)
            {
                lock (_lock)
                {
                    if (_canSend)
                    {
                        _canSend = false;
                        if (!_ticket.IsSingleLegSell)
                        {
                            CheckForSpreadExit();
                        }
                        else
                        {
                            CheckForSingleLegSellExit();
                        }
                    }
                }
            }
        }

        private void CheckForSpreadExit()
        {
            double stopLossPrice = Math.Round(_fillPx - (_fillPx * _automationConfig.SweepTradeStopLossPercentage), 2);

            if (Math.Abs(_fillPx) < 1)
            {
                stopLossPrice -= .10;
            }

            if (_ticket.High < stopLossPrice)
            {
                Task.Run(() => TriggerStopLoss());
            }
            else
            {
                double targetPrice = Math.Round(_fillPx + (_fillPx * _automationConfig.SweepTradeExitTriggerPercentage), 2);
                if (_ticket.Low > targetPrice)
                {
                    Task.Run(() => TriggerGainExit());
                }
                else
                {
                    CheckForEodExit();
                }
            }
        }

        private void CheckForSingleLegSellExit()
        {
            double stopLossPrice = Math.Round(_fillPx + (_fillPx * _automationConfig.SweepTradeStopLossPercentage), 2);

            if (Math.Abs(_fillPx) < 1)
            {
                stopLossPrice += .10;
            }

            if (_ticket.Low > stopLossPrice)
            {
                Task.Run(() => TriggerStopLoss());
            }
            else
            {
                double targetPrice = Math.Round(_fillPx - (_fillPx * _automationConfig.SweepTradeExitTriggerPercentage), 2);
                if (_ticket.High < targetPrice)
                {
                    Task.Run(() => TriggerGainExit());
                }
                else
                {
                    CheckForEodExit();
                }
            }
        }

        private void CheckForEodExit()
        {
            if (DateTime.Now.TimeOfDay > _automationConfig.SweepTradeAutoCloseTime.TimeOfDay)
            {
                Task.Run(() => TriggerStopLoss());
            }
            else
            {
                _canSend = true;
            }
        }

        private void TriggerGainExit()
        {
            int exitQty = Math.Min(_filledQty, (int)Math.Ceiling(_filledQty * _automationConfig.SweepTradeScaledExitPercentage));
            if (exitQty > 0)
            {
                SendExitOrder(exitQty, "Profit");
            }
        }

        private void TriggerStopLoss()
        {
            if (_filledQty > 0)
            {
                SendExitOrder(_filledQty, "Stop Loss");
            }
        }

        private void SendExitOrder(int exitQty, string type)
        {
            var exitOrder = _ticket.BuildOrder(true, SubType, exitQty);
            if (_ticket.IsSingleLegSell)
            {
                exitOrder.Price = _ticket.High;
            }
            else
            {
                if (_ticket.IsSingleLeg)
                {
                    exitOrder.Price = _ticket.Low;
                }
                else
                {
                    exitOrder.Price = -_ticket.Low;
                }
            }

            exitOrder.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
            _lastOrderId = exitOrder.LocalID;
            _ = _ticket.OmsCore.OrderClient.SendOrderAsync(exitOrder, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
        }

        internal void Stop()
        {
            _canSend = false;
        }

        internal void Dispose()
        {
            _canSend = false;
            IsDisposed = true;
            _ticket = null;
        }

        internal void Initiate(int qty, double fillPx)
        {
            _automationConfig = _ticket.GetAutomationConfig();
            _filledQty = qty;
            _fillPx = fillPx;
            _canSend = true;
        }
    }
}
