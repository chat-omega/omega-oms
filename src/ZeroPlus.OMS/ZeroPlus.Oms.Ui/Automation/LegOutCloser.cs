using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class LegOutCloser : OrderUpdateHandler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private OrderTicket _ticket;
        readonly ConcurrentDictionary<string, TicketLegModel> _localIdToLegMap;
        readonly ConcurrentDictionary<TicketLegModel, double> _legToMaxLossMap;
        readonly ConcurrentDictionary<TicketLegModel, double> _legToFillPriceMap;
        private readonly object _lock;
        private string _threeWayTag;

        private int _baseQty;

        private TicketLegModel _hardLeg;
        private bool _hedged;
        private double _prevHardPrice;
        private string _spreadExitId;
        private string _hedgeOrderId;
        private string _hardLegExitId;
        private string _spreadScratchExitId;
        private string _hardLegScratchExitId;
        private string _spreadEmaExitId;
        private string _hardLegEmaExitId;
        private string _hardLegWalkExitId;

        public override OrderSubType? SubType { get; set; } = OrderSubType.LegOut;
        internal static ConcurrentDictionary<TicketLegModel, double> LegToScratchPriceMap { get; } = new();
        public bool IsDisposed { get; set; }

        public LegOutCloser(OrderTicket orderTicketBase)
        {
            _ticket = orderTicketBase;
            _lock = new object();
            _localIdToLegMap = new ConcurrentDictionary<string, TicketLegModel>();
            _legToMaxLossMap = new ConcurrentDictionary<TicketLegModel, double>();
            _legToFillPriceMap = new ConcurrentDictionary<TicketLegModel, double>();
        }

        public override async void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;

                if (IsDisposed || orderStatus == null)
                {
                    return;
                }

                string localOrderId = execReport.ClientOrderId;
                if (localOrderId == _spreadExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            int pos = execReport.LeavesQty;
                            if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                            {
                                pos *= -1;
                            }
                            int requiredStocks = _ticket.CalculateRequiredHedgeQty(pos);
                            if (requiredStocks > 0)
                            {
                                CheckForHedge(requiredStocks);
                            }
                            else
                            {
                                TryExitHardLegAtTarget();
                            }
                            break;
                        case OrderStatus.Filled:
                            double avgPrice = execReport.AvgPrice;
                            await _ticket.CheckForLegOutLoopAsync(avgPrice, receiveTime);
                            break;
                    }
                }
                else if (localOrderId == _hedgeOrderId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            TryExitHardLegAtTarget();
                            break;
                        case OrderStatus.Filled:
                            _hedged = true;
                            TryExitHardLegAtTarget();
                            break;
                    }
                }
                else if (localOrderId == _hardLegExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            TryScratchSpread();
                            break;
                        case OrderStatus.Filled:
                            if (_localIdToLegMap.TryRemove(localOrderId, out TicketLegModel ticketLegModel))
                            {
                                _legToFillPriceMap[ticketLegModel] = execReport.AvgPrice;
                                TryExitOtherLegs(skipSymbol: ticketLegModel.Symbol);
                            }
                            break;
                    }
                }
                else if (localOrderId == _spreadScratchExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            TryScratchHardLeg();
                            break;
                        case OrderStatus.Filled:
                            CloseHedge();
                            double avgPrice = execReport.AvgPrice;
                            await _ticket.CheckForLegOutLoopAsync(avgPrice, receiveTime);
                            break;
                    }
                }
                else if (localOrderId == _hardLegScratchExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            TryEmaSpreadExit();
                            break;
                        case OrderStatus.Filled:
                            if (_localIdToLegMap.TryRemove(localOrderId, out TicketLegModel ticketLegModel))
                            {
                                _legToFillPriceMap[ticketLegModel] = execReport.AvgPrice;
                                TryExitOtherLegs(skipSymbol: ticketLegModel.Symbol);
                            }
                            break;
                    }
                }
                else if (localOrderId == _spreadEmaExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            TryHardLegEmaExit();
                            break;
                        case OrderStatus.Filled:
                            CloseHedge();
                            double avgPrice = execReport.AvgPrice;
                            await _ticket.CheckForLegOutLoopAsync(avgPrice, receiveTime);
                            break;
                    }
                }
                else if (localOrderId == _hardLegEmaExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            StartWalkToMaxLoss();
                            break;
                        case OrderStatus.Filled:
                            if (_localIdToLegMap.TryRemove(localOrderId, out TicketLegModel ticketLegModel))
                            {
                                _legToFillPriceMap[ticketLegModel] = execReport.AvgPrice;
                                TryExitOtherLegs(skipSymbol: ticketLegModel.Symbol);
                            }
                            break;
                    }
                }
                else if (localOrderId == _hardLegWalkExitId)
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            SendNextIncrement();
                            break;
                        case OrderStatus.Filled:
                            if (_localIdToLegMap.TryRemove(localOrderId, out TicketLegModel ticketLegModel))
                            {
                                _legToFillPriceMap[ticketLegModel] = execReport.AvgPrice;
                                TryExitOtherLegs(skipSymbol: ticketLegModel.Symbol);
                            }
                            break;
                    }
                }
                else
                {
                    switch (orderStatus)
                    {
                        case OrderStatus.Canceled:
                        case OrderStatus.Rejected:
                            if (_localIdToLegMap.TryRemove(localOrderId, out TicketLegModel ticketLegModel))
                            {
                                int qty = execReport.LeavesQty;
                                SendLegOrder(ticketLegModel, qty);
                            }
                            break;
                        case OrderStatus.Filled:
                            bool found = false;
                            if (_localIdToLegMap.TryRemove(localOrderId, out ticketLegModel))
                            {
                                _legToFillPriceMap[ticketLegModel] = execReport.AvgPrice;
                                found = true;
                            }
                            if (found)
                            {
                                await CalculateSpreadPriceFromLegsAsync(receiveTime);
                            }
                            break;
                    }
                }
                OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);
                _ticket.ContraOrderStatus = orderStatus;
                _ticket.ContraStatus = orderUpdateValues.Status;
                _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleExecutionReport));
            }
        }

        private async Task CalculateSpreadPriceFromLegsAsync(DateTime receiveTime)
        {
            double avgPrice = 0.0;
            lock (_lock)
            {
                if (_legToFillPriceMap.Count == _ticket.Legs.Count)
                {
                    CloseHedge();

                    List<KeyValuePair<TicketLegModel, double>> map = _legToFillPriceMap.ToList();

                    _legToFillPriceMap.Clear();
                    foreach (KeyValuePair<TicketLegModel, double> kvp in map)
                    {
                        avgPrice += kvp.Key.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? -kvp.Value : kvp.Value;
                    }
                }
            }
            await _ticket.CheckForLegOutLoopAsync(avgPrice, receiveTime);
        }

        private void CloseHedge()
        {
            if (_hedged)
            {
                _hedged = false;
                _ = _ticket.FlattenStockHedgeAsyncCommand();
            }
        }

        internal void Stop()
        {
            foreach (string id in _localIdToLegMap.Keys)
            {
                _ticket.OmsCore.OrderClient.CancelOrder(new CancelRequest
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
        }

        internal void Dispose()
        {
            Stop();
            _ticket = null;
            IsDisposed = true;
        }

        public void ClosePosition(int qty)
        {
            if (qty < 1)
            {
                _log.Error($"{nameof(ClosePosition)} Invalid QTY. Qty: {qty}, Id: {_ticket.SpreadId}");
                return;
            }
            _baseQty = qty;
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

            foreach (TicketLegModel leg in _ticket.Legs)
            {
                double maxLoss = _ticket.GetLoopMaxLoss();
                AutomationConfigModel automationConfig = GetAutomationConfig();
                if (maxLoss < automationConfig.LegOutMaxLoss)
                {
                    maxLoss = automationConfig.LegOutMaxLoss;
                }
                double stopPrice;
                double scratchPx = LegToScratchPriceMap[leg];
                if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    stopPrice = scratchPx - maxLoss;
                    _legToMaxLossMap[leg] = stopPrice;
                    double percentAdjBidEma = scratchPx - ((leg.Ask - leg.Bid) * GetAutomationConfig().LegOutMaxPercentThroughEma);
                    double dollarAdjBidEma = scratchPx - GetAutomationConfig().LegOutMaxDollarThroughEma;
                    if (leg.Bid < percentAdjBidEma && leg.Bid < dollarAdjBidEma)
                    {
                        TryExitAsSpread(qty);
                        return;
                    }
                }
                else
                {
                    stopPrice = scratchPx + maxLoss;
                    _legToMaxLossMap[leg] = stopPrice;
                    double percentAdjAskEma = scratchPx + ((leg.Ask - leg.Bid) * GetAutomationConfig().LegOutMaxPercentThroughEma);
                    double dollarAdjAskEma = scratchPx + GetAutomationConfig().LegOutMaxDollarThroughEma;
                    if (leg.Ask > percentAdjAskEma && leg.Ask > dollarAdjAskEma)
                    {
                        TryExitAsSpread(qty);
                        return;
                    }
                }
            }
            ExitLegs(qty);
        }

        private void TryExitAsSpread(int qty)
        {
            var orderInfo = _ticket.BuildOrder(isContra: true, SubType, qty);

            double price = 0.0;
            foreach (KeyValuePair<TicketLegModel, double> kvp in LegToScratchPriceMap)
            {
                TicketLegModel leg = kvp.Key;
                if (leg.Side == Side.Buy)
                {
                    price -= kvp.Value * leg.Ratio;
                }
                else
                {
                    price += kvp.Value * leg.Ratio;
                }
            }

            orderInfo.Price = price;
            AutomationConfigModel automationConfig = GetAutomationConfig();
            orderInfo.SetCancelDelay(automationConfig.LegOutSpreadCancelTime);
            orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
            _spreadExitId = orderInfo.LocalID;
            _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
        }

        private void ExitLegs(int qty)
        {
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                int legQty = leg.Ratio * qty;
                SendLegOrder(leg, legQty);
            }
        }

        private bool CheckForHedge(int requiredStocks)
        {
            double hedgeEstNotional = Math.Round(Math.Abs(requiredStocks > 0 ? requiredStocks * _ticket.UnderAsk : requiredStocks * _ticket.UnderBid), 2);

            _log.Info($"{nameof(CheckForHedge)}. " +
                      $"Id: {_ticket.SpreadId}, " +
                      $"Required Stocks: {requiredStocks}, " +
                      $"Hedge Est Notional: {hedgeEstNotional}, " +
                      $"Delta: {_ticket.TotalDelta}, " +
                      $"Last Fill: {_ticket.LastFillPx}, " +
                      $"Underlying At Fill: {_ticket.LastMainUnderMidAtFill}, " +
                      $"Underlying: {_ticket.UnderBid}X{_ticket.UnderAsk}.");

            if (hedgeEstNotional <= OmsCore.Config.BasketHedgeHouseMaxNotionalV2)
            {
                if (Math.Abs(requiredStocks) <= OmsCore.Config.BasketHedgeHouseMaxQtyV2)
                {
                    _hedgeOrderId = _ticket.HedgeWithStockAsync(requiredStocks);
                    _ticket.ReleaseHedgingInstance();
                    return true;
                }
                else
                {
                    _log.Info($"{nameof(CheckForHedge)}. Attempted hedge above risk limit. " +
                              $"Id: {_ticket.SpreadId}, " +
                              $"Req Stock: {requiredStocks}, " +
                              $"Est Notional: {hedgeEstNotional}, " +
                              $"Max Qty: {OmsCore.Config.BasketHedgeHouseMaxQtyV2}, " +
                              $"Max Notional: {OmsCore.Config.BasketHedgeHouseMaxNotionalV2}.");
                }
            }
            else
            {
                _log.Info($"{nameof(CheckForHedge)}. Attempted hedge above risk limit. " +
                          $"Id: {_ticket.SpreadId}, " +
                          $"Req Stock: {requiredStocks}, " +
                          $"Est Notional: {hedgeEstNotional}, " +
                          $"Max Qty: {OmsCore.Config.BasketHedgeHouseMaxQtyV2}, " +
                          $"Max Notional: {OmsCore.Config.BasketHedgeHouseMaxNotionalV2}.");
            }

            return false;
        }

        private void TryEmaSpreadExit()
        {
            var orderInfo = _ticket.BuildOrder(isContra: true, SubType, _baseQty);

            double price = 0.0;
            foreach (KeyValuePair<TicketLegModel, double> kvp in LegToScratchPriceMap)
            {
                TicketLegModel leg = kvp.Key;
                if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    price -= leg.AdjBidEma * leg.Ratio;
                }
                else
                {
                    price += leg.AdjAskEma * leg.Ratio;
                }
            }

            orderInfo.Price = price;
            AutomationConfigModel automationConfig = GetAutomationConfig();
            orderInfo.SetCancelDelay(automationConfig.LegOutSpreadCancelTime);
            orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
            _spreadEmaExitId = orderInfo.LocalID;
            _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
        }

        private AutomationConfigModel GetAutomationConfig()
        {
            return _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement);
        }

        private void TryScratchSpread()
        {
            var orderInfo = _ticket.BuildOrder(isContra: true, SubType, _baseQty);

            double price = 0.0;
            foreach (KeyValuePair<TicketLegModel, double> kvp in LegToScratchPriceMap)
            {
                TicketLegModel leg = kvp.Key;
                if (leg.Side == Side.Buy)
                {
                    price -= kvp.Value * leg.Ratio;
                }
                else
                {
                    price += kvp.Value * leg.Ratio;
                }
            }

            orderInfo.Price += _ticket.CloseEdgeOveride;

            orderInfo.Price = price;
            orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSpreadCancelTime);
            orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
            _spreadScratchExitId = orderInfo.LocalID;
            _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
        }

        private void SendLegOrder(TicketLegModel leg, int legQty)
        {
            var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (leg.Side == Side.Buy)
            {
                orderInfo.Price = leg.Bid - automationConfigModel.LegOutFillGuarantee;
                double stopPrice = _legToMaxLossMap[leg];
                if (orderInfo.Price < stopPrice)
                {
                    return;
                }
            }
            else
            {
                orderInfo.Price = leg.Ask + automationConfigModel.LegOutFillGuarantee;
                double stopPrice = _legToMaxLossMap[leg];
                if (orderInfo.Price > stopPrice)
                {
                    return;
                }
            }

            if (_ticket.TrySelectRoute(true, false, out string route, out _))
            {
                orderInfo.Route = route;
            }
            orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
            orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
            _localIdToLegMap[orderInfo.LocalID] = leg;
            _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
        }

        private void TryExitHardLegAtTarget()
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                if (leg.Side == Side.Buy)
                {
                    if (leg.Bid < leg.AdjBidEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = LegToScratchPriceMap[leg] - automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
                else
                {
                    if (leg.Ask > leg.AdjAskEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = LegToScratchPriceMap[leg] + automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
            }

            ExitLegs(_baseQty);
        }

        private void TryScratchHardLeg()
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                if (leg.Side == Side.Buy)
                {
                    if (leg.Bid < leg.AdjBidEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = LegToScratchPriceMap[leg] - _ticket.CloseEdgeOveride - automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegScratchExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
                else
                {
                    if (leg.Ask > leg.AdjAskEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = LegToScratchPriceMap[leg] + _ticket.CloseEdgeOveride + automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegScratchExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
            }

            ExitLegs(_baseQty);
        }

        private void TryHardLegEmaExit()
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                if (leg.Side == Side.Buy)
                {
                    if (leg.Bid < leg.AdjBidEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = leg.AdjBidEma - automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegEmaExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
                else
                {
                    if (leg.Ask > leg.AdjAskEma)
                    {
                        int legQty = leg.Ratio * _baseQty;
                        var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                        orderInfo.Price = leg.AdjAskEma + automationConfigModel.LegOutFillGuarantee;
                        if (_ticket.TrySelectRoute(true, false, out string route, out _))
                        {
                            orderInfo.Route = route;
                        }

                        orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                        orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                        _hardLegEmaExitId = orderInfo.LocalID;
                        _localIdToLegMap[orderInfo.LocalID] = leg;
                        _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                        return;
                    }
                }
            }

            ExitLegs(_baseQty);
        }

        private void StartWalkToMaxLoss()
        {
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                if (leg.Side == Side.Buy)
                {
                    if (leg.Bid < leg.AdjBidEma)
                    {
                        _hardLeg = leg;
                        _prevHardPrice = leg.AdjBidEma;
                        break;
                    }
                }
                else
                {
                    if (leg.Ask > leg.AdjAskEma)
                    {
                        _hardLeg = leg;
                        _prevHardPrice = leg.AdjAskEma;
                        break;
                    }
                }
            }

            SendNextIncrement();
        }

        private void SendNextIncrement()
        {
            if (_hardLeg != null)
            {
                TicketLegModel leg = _hardLeg;
                if (leg.Side == Side.Buy)
                {
                    int legQty = leg.Ratio * _baseQty;
                    var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                    AutomationConfigModel automationConfig = GetAutomationConfig();
                    orderInfo.Price = _prevHardPrice - automationConfig.LegOutSingleLegIncrement;
                    _prevHardPrice = orderInfo.Price;

                    if (_prevHardPrice < _legToMaxLossMap[leg])
                    {
                        return;
                    }

                    if (_ticket.TrySelectRoute(true, false, out string route, out _))
                    {
                        orderInfo.Route = route;
                    }

                    orderInfo.SetCancelDelay(automationConfig.LegOutSingleLegCancelTime);
                    orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                    _hardLegWalkExitId = orderInfo.LocalID;
                    _localIdToLegMap[orderInfo.LocalID] = leg;
                    _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                    return;

                }
                else
                {

                    int legQty = leg.Ratio * _baseQty;
                    var orderInfo = _ticket.BuildSingleLegOrder(isContra: true, leg, SubType, legQty, _threeWayTag);
                    orderInfo.Price = _prevHardPrice + GetAutomationConfig().LegOutSingleLegIncrement;
                    _prevHardPrice = orderInfo.Price;

                    if (_prevHardPrice > _legToMaxLossMap[leg])
                    {
                        return;
                    }

                    if (_ticket.TrySelectRoute(true, false, out string route, out _))
                    {
                        orderInfo.Route = route;
                    }

                    orderInfo.SetCancelDelay(GetAutomationConfig().LegOutSingleLegCancelTime);
                    orderInfo.LocalID = _ticket.OmsCore.OrderClient.GetNextOrderId();
                    _hardLegWalkExitId = orderInfo.LocalID;
                    _localIdToLegMap[orderInfo.LocalID] = leg;
                    _ = _ticket.OmsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, checkForDuplicate: false, null);
                    return;
                }
            }
        }

        private void TryExitOtherLegs(string skipSymbol)
        {
            foreach (TicketLegModel leg in _ticket.Legs)
            {
                if (leg.Symbol != skipSymbol)
                {
                    int legQty = leg.Ratio * _baseQty;
                    SendLegOrder(leg, legQty);
                }
            }
        }
    }
}
