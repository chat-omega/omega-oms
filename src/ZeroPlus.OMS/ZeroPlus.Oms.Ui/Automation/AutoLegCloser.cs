using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class AutoLegCloser : OrderUpdateHandler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private OrderTicket _ticket;
        private readonly ConcurrentDictionary<string, TicketLegModel> _localIdToLegMap;

        private string _threeWayTag;

        private int _hardLegQty = 0;
        private int _easyLegQty = 0;
        private int _easyLegAttempt = 0;
        private string _hardLegLocalId;
        private string _easyLegLocalId;
        private double _hardLegExitPx = double.NaN;
        private double _hardLegExitStopPx = double.NaN;
        private double _hardLegMinIncrement;

        public OmsCore OmsCore { get; }
        public override OrderSubType? SubType { get; set; } = OrderSubType.AutoLeg;
        public bool IsDisposed { get; set; }

        public TicketLegModel HardLeg { get; set; }
        public TicketLegModel EasyLeg { get; set; }
        public AutomationConfigModel Config { get; set; }
        public MinimumTickStyle HardLegMinimumTickStyle { get; set; }

        public AutoLegCloser(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
            _localIdToLegMap = new ConcurrentDictionary<string, TicketLegModel>();
            OmsCore = orderTicketBase.OmsCore;
        }

        public double GetMinIncrement(double price, IncrementDirection direction = IncrementDirection.Down)
        {
            double increment = _hardLegMinIncrement;

            switch (HardLegMinimumTickStyle)
            {
                case MinimumTickStyle.None:
                    increment = 0;
                    break;
                case MinimumTickStyle.AllPenny:
                    increment = .01;
                    break;
                case MinimumTickStyle.Pennies:
                    if (price < 3 || (price == 3 && direction == IncrementDirection.Down))
                    {
                        increment = .01;
                    }
                    else if (price > 3 || (price == 3 && direction == IncrementDirection.Up))
                    {
                        increment = .05;
                    }
                    break;
                case MinimumTickStyle.Nickels:
                    if (price < 3 || (price == 3 && direction == IncrementDirection.Down))
                    {
                        increment = .05;
                    }
                    else if (price > 3 || (price == 3 && direction == IncrementDirection.Up))
                    {
                        increment = .10;
                    }
                    break;
                case MinimumTickStyle.Dimes:
                    increment = .10;
                    break;
            }

            return increment;
        }

        public void SetMinIncrement(double value)
        {
            _hardLegMinIncrement = Math.Round(value, 2);
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;
                if (IsDisposed || orderStatus is not OrderStatus status || executionType == null)
                {
                    return;
                }

                string localOrderId = execReport.ClientOrderId;
                if (localOrderId == _hardLegLocalId)
                {
                    if (executionType.Value.IsFilled())
                    {
                        _hardLegQty -= execReport.LastQty;
                    }
                    if (status.IsClosed())
                    {
                        _localIdToLegMap.TryRemove(localOrderId, out _);
                        _hardLegLocalId = string.Empty;
                    }
                    switch (status)
                    {
                        case OrderStatus.Canceled when _hardLegQty > 0:
                        case OrderStatus.Rejected when _hardLegQty > 0:
                            if (!ExitHardLeg())
                            {
                                OpenTicket();
                                _ticket.IsLooping = false;
                            }
                            break;
                        case OrderStatus.Canceled when _hardLegQty == 0:
                        case OrderStatus.Rejected when _hardLegQty == 0:
                        case OrderStatus.Filled:
                            bool exiting = ExitEasyLeg();
                            if (!exiting)
                            {
                                OpenTicket();
                                _ticket.IsLooping = false;
                            }
                            break;
                    }
                }
                else if (localOrderId == _easyLegLocalId)
                {
                    if (executionType.Value.IsFilled())
                    {
                        _easyLegQty -= execReport.LastQty;
                    }
                    if (status.IsClosed())
                    {
                        _localIdToLegMap.TryRemove(localOrderId, out _);
                        _easyLegLocalId = string.Empty;
                    }
                    if (_easyLegQty == 0)
                    {
                        _ticket.IsLooping = false;
                    }
                    switch (status)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            if (++_easyLegAttempt < 20)
                            {
                                bool exiting = ExitEasyLeg();
                                if (!exiting)
                                {
                                    OpenTicket();
                                    _ticket.IsLooping = false;
                                }
                            }
                            else
                            {
                                OpenTicket();
                                _ticket.IsLooping = false;
                            }
                            break;
                    }
                }
                OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);
                _ticket.ContraOrderStatus = status;
                _ticket.ContraStatus = orderUpdateValues.Status;
                _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleExecutionReport));
            }
        }

        private void OpenTicket()
        {
            if (_ticket.BasketSettings.OpenTicketForFailedClose && !_ticket.BasketSettings.OpenTicketForFills)
            {
                if (_hardLegQty > 0)
                {
                    _ticket.BasketTraderViewModel.CreateComplexOrderTicket(_ticket);
                }
                else if (_easyLegQty > 0)
                {
                    _ticket.BasketTraderViewModel.CreateComplexOrderTicket(EasyLeg.Symbol, EasyLeg.Side == Side.Buy ? Side.Sell : Side.Buy, true);
                }
            }
        }

        internal void Stop()
        {
            foreach (string id in _localIdToLegMap.Keys)
            {
                OmsCore.OrderClient.CancelOrder(new CancelRequest
                {
                    OrderId = id,
                    Venue = _ticket.Venue,
                    LocalId = _ticket.LocalId,
                    PermId = _ticket.PermID,
                    Account = _ticket.Account,
                    UserId = _ticket.UserId,
                    RiskCheckId = _ticket.RiskCheckId
                });
            }
            _localIdToLegMap.Clear();
        }

        internal void Dispose()
        {
            Stop();
            _ticket = null;
            IsDisposed = true;
        }

        public bool ClosePosition(int qty)
        {
            if (Config == null)
            {
                _log.Error($"{nameof(ClosePosition)} Config not valid. Qty: {qty}, Id: {_ticket.SpreadId}");
                return false;
            }
            if (qty < 1)
            {
                _log.Error($"{nameof(ClosePosition)} Invalid QTY. Qty: {qty}, Id: {_ticket.SpreadId}");
                return false;
            }

            _hardLegQty = 0;
            _easyLegQty = 0;
            _easyLegAttempt = 0;
            _hardLegLocalId = string.Empty;
            _easyLegLocalId = string.Empty;
            _hardLegExitPx = double.NaN;
            _hardLegExitStopPx = double.NaN;
            _ticket.IsLooping = true;
            SetSpreadTracker();

            _easyLegQty = qty * EasyLeg.Ratio;
            _hardLegQty = qty * HardLeg.Ratio;

            double totalFillPx = _ticket.LastFillPx;
            if (EasyLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                totalFillPx -= EasyLeg.Bid * EasyLeg.Ratio;
            }
            else
            {
                totalFillPx += EasyLeg.Ask * EasyLeg.Ratio;
            }

            totalFillPx = Math.Abs(totalFillPx) / HardLeg.Ratio;

            double loopMaxLoss = Config.AutoLegMaxLoss;
            if (HardLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                double increment = Math.Max(Config.AutoLegCloseIncrement, GetMinIncrement(totalFillPx, IncrementDirection.Down));
                _hardLegExitPx = Math.Round(totalFillPx + Config.AutoLegCloseEdge + increment, 2);
                _hardLegExitStopPx = Math.Round(totalFillPx - loopMaxLoss, 2);
            }
            else
            {
                double increment = Math.Max(Config.AutoLegCloseIncrement, GetMinIncrement(totalFillPx, IncrementDirection.Up));
                _hardLegExitPx = Math.Round(totalFillPx - Config.AutoLegCloseEdge - increment, 2);
                _hardLegExitStopPx = Math.Round(totalFillPx + loopMaxLoss, 2);
            }

            _log.Info($"{nameof(ClosePosition)} Auto-Leg Close initiated. Qty: {qty}, Id: {_ticket.SpreadId}, Hard-Leg: {HardLeg?.Side} {HardLeg?.Ratio} {HardLeg?.Symbol} {_hardLegQty}@{_hardLegExitPx:N2} - {_hardLegExitStopPx:N2} [{HardLeg?.Bid}X{HardLeg?.Ask}], Easy-Leg: {EasyLeg?.Side} {EasyLeg?.Ratio} {EasyLeg?.Symbol} {_easyLegQty} [{EasyLeg?.Bid}X{EasyLeg?.Ask}]");

            return ExitHardLeg();
        }

        private bool ExitHardLeg()
        {
            bool isCloseSellOrder = HardLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
            if (isCloseSellOrder)
            {
                double minIncrement = GetMinIncrement(_hardLegExitPx, IncrementDirection.Down);
                double increment = Math.Max(Config.AutoLegCloseIncrement, minIncrement);
                _hardLegExitPx = Math.Round(_hardLegExitPx - increment, 2);

                if (_hardLegExitPx > HardLeg.Ask)
                {
                    _hardLegExitPx = HardLeg.Ask;
                }
                if (_hardLegExitPx < _hardLegExitStopPx)
                {
                    _log.Error($"{nameof(ExitHardLeg)} Stop Px reached. Side: {HardLeg.Side} Px: {_hardLegExitPx}, Stop: {_hardLegExitStopPx}, Id: {_ticket.SpreadId}");
                    return false;
                }
                if (_hardLegExitPx < HardLeg.Bid)
                {
                    _hardLegExitPx = HardLeg.Bid;
                }

                if (_hardLegExitPx == 0)
                {
                    _hardLegExitPx = minIncrement;
                }
                if (minIncrement == .05 || minIncrement == .10)
                {
                    _hardLegExitPx = _ticket.PadForNickelOrDime(_hardLegExitPx, Convert.ToDecimal(minIncrement), isCloseSellOrder);
                }
            }
            else
            {
                double minIncrement = GetMinIncrement(_hardLegExitPx, IncrementDirection.Up);
                double increment = Math.Max(Config.AutoLegCloseIncrement, minIncrement);
                _hardLegExitPx = Math.Round(_hardLegExitPx + increment, 2);
                if (_hardLegExitPx < HardLeg.Bid)
                {
                    _hardLegExitPx = HardLeg.Bid;
                }
                if (_hardLegExitPx > _hardLegExitStopPx)
                {
                    _log.Error($"{nameof(ExitHardLeg)} Stop Px reached. Side: {HardLeg?.Side} Px: {_hardLegExitPx}, Stop: {_hardLegExitStopPx}, Id: {_ticket.SpreadId}");
                    return false;
                }
                if (_hardLegExitPx > HardLeg.Ask)
                {
                    _hardLegExitPx = HardLeg.Ask;
                }
                if (minIncrement == .05 || minIncrement == .10)
                {
                    _hardLegExitPx = _ticket.PadForNickelOrDime(_hardLegExitPx, Convert.ToDecimal(minIncrement), isCloseSellOrder);
                }
            }

            if (_hardLegQty < 0)
            {
                _log.Error($"{nameof(ExitHardLeg)} Invalid QTY. Side: {HardLeg?.Side} Px: {_hardLegExitPx}, Qty: {_hardLegQty}, Id: {_ticket.SpreadId}");
                return false;
            }

            double spreadPx = 0.0;
            if (HardLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                spreadPx -= _hardLegExitPx * HardLeg.Ratio;
            }
            else
            {
                spreadPx += _hardLegExitPx * HardLeg.Ratio;
            }
            if (EasyLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                spreadPx -= EasyLeg.Bid * EasyLeg.Ratio;
            }
            else
            {
                spreadPx += EasyLeg.Ask * EasyLeg.Ratio;
            }

            double attemptedEdge = _ticket.CalculateAttemptedEdgeOnClose(spreadPx);

            if (_ticket.CheckForAutoHedge(attemptedEdge))
            {
                _log.Error($"{nameof(ExitHardLeg)} Hedged. Id: {_ticket.SpreadId}");
                _ticket.IsLooping = false;
                return !Config.AutoHedgeOpenTicket;
            }

            _log.Info($"{nameof(ExitHardLeg)} Sending Order. Side: {HardLeg?.Side} Px: {_hardLegExitPx},  Leg: {HardLeg.Symbol}, Id: {_ticket.SpreadId}");
            var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, HardLeg, SubType, _hardLegQty, _threeWayTag);
            orderInfo.Price = _hardLegExitPx;
            orderInfo.Route = Config.AutoLegCloseRoute;
            orderInfo.SetCancelDelay(Config.AutoLegRestTime);
            _hardLegLocalId = OmsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _hardLegLocalId;
            _localIdToLegMap[orderInfo.LocalID] = HardLeg;
            _ = OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
            return true;
        }

        private bool ExitEasyLeg()
        {
            if (_easyLegQty < 0)
            {
                _log.Error($"{nameof(ExitHardLeg)} Invalid QTY. Qty: {_easyLegQty}, Id: {_ticket.SpreadId}");
                return false;
            }
            double price;
            if (EasyLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                price = EasyLeg.Bid;
                if (price == 0)
                {
                    price = GetMinIncrement(price);
                }
            }
            else
            {
                price = EasyLeg.Ask;
            }

            _log.Info($"{nameof(ExitEasyLeg)} Sending Order. Leg: {EasyLeg?.Symbol}, Id: {_ticket.SpreadId}");
            var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, EasyLeg, SubType, _easyLegQty, _threeWayTag);
            orderInfo.Price = price;
            orderInfo.Route = Config.AutoLegCloseRoute;
            var sweepRoute = OmsCore.Config.DefaultSweepRoute(_ticket.InstanceMode);
            if (_easyLegAttempt > 15 && !string.IsNullOrWhiteSpace(sweepRoute))
            {
                orderInfo.Route = sweepRoute;
            }
            orderInfo.SetCancelDelay(Config.AutoLegRestTime);
            _easyLegLocalId = OmsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _easyLegLocalId;
            _localIdToLegMap[orderInfo.LocalID] = EasyLeg;
            _ = OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
            return true;
        }

        private void SetSpreadTracker()
        {
            List<string> symbols = new()
            {
                "[" + _ticket.SpreadSymbol + "]"
            };
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                symbols.Add("[" + leg.Symbol + "]");
            }

            string payLoad = "3 Way - " + _ticket.SpreadId + " - " + string.Join(", ", symbols);
            _threeWayTag = payLoad.CompressString();
        }
    }
}
