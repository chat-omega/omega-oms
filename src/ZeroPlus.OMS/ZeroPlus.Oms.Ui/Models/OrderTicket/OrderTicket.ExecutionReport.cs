using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Exceptions;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Models;

public abstract partial class OrderTicket
{
    public OrderTagModel OrderTag { get; set; }

    public async void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
    {
        try
        {
            OrderUpdateValues orderUpdateValues = ParseOrderUpdate(execReport);

            GetOrderType(orderUpdateValues, out var isMainOrder, out var isContraOrder, out var isHedgeOrder);
            OmsOrder order = new()
            {
                LastQuantity = orderUpdateValues.LastQuantity,
                CumulativeQuantity = orderUpdateValues.CumQuantity,
                FilledQty = Math.Max(orderUpdateValues.Filled, 0),
                AveragePrice = orderUpdateValues.AveragePrice,
                LastUpdateTime = orderUpdateValues.LastUpdateTime,
                Symbol = isMainOrder ? SpreadSymbol : isContraOrder ? ContraSpreadSymbol : HedgeUnderlying,
                SpreadId = SpreadId,
                SpreadType = SpreadType,
                Bid = Low,
                Ask = High,
            };

            var isActive = !orderUpdateValues.OrderStatus.IsClosed();
            if (isMainOrder)
            {
                MainResting = isActive;
                IsModifyEnabled = isActive;
            }
            else if (isContraOrder)
            {
                ContraResting = isActive;
                IsContraModifyEnabled = isActive;
            }

            _log.Info("Order status received. Spread: " + SpreadId + ", OrderId:" + orderUpdateValues.OrderId + "/" + orderUpdateValues.OriginalOrderId + "/" + orderUpdateValues.LocalOrderId + ", Status:" + execReport.OrderStatus + ", Exec Type:" + execReport.ExecutionType + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Main: " + isMainOrder + ", Contra:" + isContraOrder + ", Hedge:" + isHedgeOrder + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);

            if (orderUpdateValues.OrderStatus == OrderStatus.PendingNew)
            {
                PendingNewLatency = _latencyTimer.ElapsedMilliseconds;
            }
            if (orderUpdateValues.OrderStatus == OrderStatus.New)
            {
                if (isMainOrder)
                {
                    _mainNewTimestamp = DateTime.Now;
                    _cancelRequestSent = false;
                }
                else if (isContraOrder)
                {
                    _contraNewTimestamp = DateTime.Now;
                    _cancelContraRequestSent = false;
                }
            }
            if (orderUpdateValues.OrderStatus is OrderStatus.PendingCancel or OrderStatus.Canceled)
            {
                if (isMainOrder)
                {
                    _cancelRequestSent = true;
                }
                else if (isContraOrder)
                {
                    _cancelContraRequestSent = true;
                }
            }
            if (orderUpdateValues.OrderStatus is OrderStatus.New or OrderStatus.Replaced)
            {
                if (!isHedgeOrder)
                {
                    NewStatusTimeStamp = DateTime.Now;
                }
            }

            if (!IsBasketOrder && (TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg && isMainOrder)
            {
                CheckForStopLoss(execReport, orderUpdateValues, isMainOrder, execReport.ExecutionType, orderUpdateValues.OrderStatus);
            }

            if (execReport.ExecutionType.IsFilled())
            {
                TotalResubmitCount = 0;
                if (isMainOrder)
                {
                    if (!IsActive)
                    {
                        ResetVolumeCounter(onlyIfFlat: false);
                        VolumeAtFill = TotalVolume;
                    }
                    if (Side != null)
                    {
                        _spreadIdAndSideToLastAvgFillPxMap[Tuple.Create(SpreadId, Side)] = orderUpdateValues.AveragePrice;
                    }
                    LastFilledOrderEdgeToTheo = LastOrderEdgeToTheo;
                    LastExchange = execReport.LastExchange;
                    Order.UpdateExchangesOnLastExchangeUpdate(this);
                    MainNotFilled = false;
                    LastMainUnderPriceAtFill = Last;
                    LastMainUnderMidAtFill = UnderMid;
                    LastMainTotalVolumeAtFill = TotalVolume;
                    AveragePrice = orderUpdateValues.AveragePrice;
                }
                else if (isContraOrder)
                {
                    if (Side != null)
                    {
                        Side? side = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        _spreadIdAndSideToLastAvgFillPxMap[Tuple.Create(SpreadId, side)] = orderUpdateValues.AveragePrice;
                    }
                    LastFilledClosingOrderEdgeToTheo = LastContraOrderEdgeToTheo;
                    LastContraExchange = execReport.LastExchange;
                    ContraNotFilled = false;
                    LastContraUnderPriceAtFill = Last;
                    LastContraUnderMidAtFill = UnderMid;
                    LastContraTotalVolumeAtFill = TotalVolume;
                    ContraAveragePrice = orderUpdateValues.AveragePrice;
                    CheckForActiveUncheck();
                }

                if (!isHedgeOrder)
                {
                    int lastQty = IsSingleLeg ? execReport.LastQty : execReport.LastQty;
                    int lastFillContracts = Legs.Sum(x => x.Ratio * lastQty);
                    ChangeInVolume = TotalVolume - (ChangeInVolume + lastFillContracts + VolumeAtFill);
                    FillTime = (DateTime.Now - NewStatusTimeStamp).TotalMilliseconds;
                    if (IsBasketOrder && BasketSettings.HedgeAutoEnabled && execReport.Side != null)
                    {
                        _lastHedgedMain = isMainOrder;
                        HedgeLastFill(execReport);
                    }
                }
            }

            bool onSmartRoute = await CheckForSmartRouteAsync(execReport, orderUpdateValues, order, orderUpdateValues.OrderStatus);
            if (onSmartRoute)
            {
                return;
            }

            await ProcessAutomation(execReport, receiveTime, orderUpdateValues, isMainOrder, isContraOrder);

            if (isMainOrder && execReport.ExecutionType.IsFilled())
            {
                if (Fisher.IsRunning)
                {
                    Fisher.IsRunning = false;
                }
                IsFreeLooking = false;
                LastFillPx = orderUpdateValues.AveragePrice;
                LastFillUnderBidPx = UnderBid;
                LastFillUnderPx = UnderMid;
                LastFillUnderAskPx = UnderAsk;
                LastFillAdjTheo = NetDeltaAdjTheo;
                LastFillQty = Math.Abs(execReport.CumQty);
                IsCloseEnabled = LastFillQty != 0;
            }
            else if (isMainOrder ^ execReport.ExecutionType.IsFilled())
            {
                IsCloseEnabled = false;
            }

            if (isHedgeOrder)
            {
                Side hedgeSide = ZeroPlus.Models.Data.Enums.Side.Buy;
                bool validReport = false;
                if (execReport.Side == ZeroPlus.Models.Data.Enums.Side.Buy || execReport.Side == ZeroPlus.Models.Data.Enums.Side.BuyToCover)
                {
                    hedgeSide = ZeroPlus.Models.Data.Enums.Side.Buy;
                    validReport = true;
                }
                else if (execReport.Side == ZeroPlus.Models.Data.Enums.Side.Sell || execReport.Side == ZeroPlus.Models.Data.Enums.Side.SellShort)
                {
                    hedgeSide = ZeroPlus.Models.Data.Enums.Side.Sell;
                    validReport = true;
                }

                if (execReport.ExecutionType.IsFilled())
                {
                    int hedgeFillQty = hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(execReport.LastQty) : Math.Abs(execReport.LastQty);
                    object hedgeLock = AcquireSpreadHedgeLock();
                    lock (hedgeLock)
                    {
                        SubmittedStocks -= hedgeFillQty;
                    }
                }
                if (orderUpdateValues.OrderStatus is OrderStatus.Canceled or OrderStatus.Rejected)
                {
                    int hedgeQty = execReport.Qty - execReport.CumQty;
                    hedgeQty = hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(hedgeQty) : Math.Abs(hedgeQty);
                    object hedgeLock = AcquireSpreadHedgeLock();
                    lock (hedgeLock)
                    {
                        SubmittedStocks -= hedgeQty;
                        HedgeClosedEvent.Set();
                        UpdateStockPositions();
                    }
                    if (IsBasketOrder)
                    {
                        if (execReport.Side != null)
                        {
                            if (validReport)
                            {
                                if (BasketSettings.HedgeAutoEnabled && ++HedgeAttempt < BasketSettings.HedgeAttempt)
                                {
                                    HedgeWithStockAsync(hedgeQty);
                                }
                                else
                                {
                                    bool openTicket = await GetVerificationAsync("Hedge Attempt Failed.\n" +
                                                                      "Hedge Id: " + SpreadId + "\n" +
                                                                      "Under: " + HedgeUnderlying + "\n" +
                                                                      "Required: " + RequiredStocks + "\n" +
                                                                      "Hedged: " + HedgedStocks + "\n" +
                                                                      "Side: " + hedgeSide + "\n" +
                                                                      "Attempted Qty: " + execReport.Qty + "\n" +
                                                                      "Cumilitive Qty: " + execReport.CumQty + "\n" +
                                                                      "Would you like to open a hedge ticket?",
                                                                      "Hedge Failed.");
                                    if (openTicket)
                                    {
                                        _ = OpenHedgeTicketAsync(hedgeSide, execReport.Qty - execReport.CumQty);
                                    }
                                    else
                                    {
                                        HedgeAttempt = 0;
                                    }
                                }
                            }
                            else
                            {
                                ShowMessage("Invalid hedge report! Hedge Failed.", "Hedge Failed");
                                _log.Error(nameof(HandleExecutionReport) + " Invalid hedge report, Side: " + execReport.Side);
                            }
                        }
                    }
                    else
                    {
                        ShowMessage("Hedge Attempt Failed.\n" +
                                    "Hedge Id: " + SpreadId + "\n" +
                                    "Under: " + HedgeUnderlying + "\n" +
                                    "Required: " + RequiredStocks + "\n" +
                                    "Hedged: " + HedgedStocks + "\n" +
                                    "Attempted Qty: " + execReport.Qty + "\n" +
                                    "Cumilitive Qty: " + execReport.CumQty,
                                    "Hedge Failed");
                    }
                    CanHedge = true;
                }
                else if (orderUpdateValues.OrderStatus == OrderStatus.Filled)
                {
                    HedgeAttempt = 0;
                    LastHedgePrice = execReport.AvgPrice;
                    LastHedgeQty = hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell ? -execReport.CumQty : execReport.CumQty;
                    HedgeClosedEvent.Set();
                    UpdateStockPositions();
                    if (IsBasketOrder)
                    {
                        CalculateEdgeToMarket();
                    }
                    CanHedge = true;
                }
            }

            if (isMainOrder)
            {
                if (orderUpdateValues.OrderStatus is OrderStatus.Replaced or OrderStatus.New)
                {
                    OrderId = execReport.OrderId;
                    CanReplace = true;
                }
                else
                {
                    CanReplace = false;
                }
            }
            if (isContraOrder)
            {
                if (orderUpdateValues.OrderStatus is OrderStatus.Replaced or OrderStatus.New)
                {
                    ContraOrderId = execReport.OrderId;
                    CanReplaceContra = true;
                }
                else
                {
                    CanReplaceContra = false;
                }
            }

            UpdateOrderStatus(orderUpdateValues, orderUpdateValues.OrderStatus);

            if (!IsBasketOrder && OmsCore.Config.WarnAgainstDoubleFillOnCloseEnabled)
            {
                if (orderUpdateValues.OrderStatus is OrderStatus.PartiallyFilled or OrderStatus.Filled || execReport.ExecutionType.IsFilled())
                {
                    if (!_openingOrderToSideMap.ContainsKey(SpreadId) && Side.HasValue)
                    {
                        _openingOrderToSideMap[SpreadId] = Side.Value;
                    }
                }
            }

            if (OmsCore.Config.PriceCacheClearIntervalEnabled && Side.HasValue && (isMainOrder || isContraOrder))
            {
                if (orderUpdateValues.OrderStatus == OrderStatus.Canceled)
                {
                    if (_priceCacheManager.TryGetValue(SpreadId, true, out PriceCache priceCache))
                    {
                        Side side = isMainOrder ? Side.Value : Side.Value == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        priceCache.UpdateCache(side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, execReport.Price, UnderMid);
                    }

                    if (_priceCacheManager.TryGetGenericValue(SpreadPermId, true, out priceCache))
                    {
                        Side side = isMainOrder ? Side.Value : Side.Value == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        priceCache.UpdateLastAttempt(side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, execReport.LastUpdateTime);
                    }
                }

                if (isContraOrder && orderUpdateValues.OrderStatus.IsFilled() && LastEdge < 0)
                {
                    if (_priceCacheManager.TryGetGenericValue(SpreadPermId, true, out var priceCache))
                    {
                        priceCache.UpdateLastLoser(execReport.LastUpdateTime);
                    }
                }
            }
        }
        catch (RouteSelectionMoveException ex)
        {
            _log.Info(ex);
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(HandleExecutionReport));
        }
    }

    protected abstract Task ProcessAutomation(OrderUpdateModel execReport, DateTime receiveTime, OrderUpdateValues orderUpdateValues, bool isMainOrder, bool isContraOrder);

    internal OrderUpdateValues ParseOrderUpdate(OrderUpdateModel execReport, bool? parseAsSingle = null)
    {
        OrderStatus orderStatus = execReport.OrderStatus;

        OrderUpdateValues orderUpdateValues = new()
        {
            OrderStatus = orderStatus,
            LastUpdateTime = execReport.LastUpdateTime,
            OrderId = execReport.OrderId,
            LastPrice = execReport.LastPx == 0 ? double.NaN : execReport.LastPx,
            AveragePrice = double.NaN,
            AveragePriceAfterFees = double.NaN,
            UnderlyingMidPrice = double.NaN,
            LocalOrderId = execReport.ClientOrderId,
            OriginalOrderId = execReport.OrigOrderId,
            IsCancelEnabled = true,
            IsModifyEnabled = true,
            IsSubmitEnabled = true,
        };

        orderUpdateValues.IsMainOrder = IsMainOrder(orderUpdateValues);
        if (!orderUpdateValues.IsMainOrder)
        {
            orderUpdateValues.IsContraOrder = IsContraOrder(orderUpdateValues);
        }
        if (!orderUpdateValues.IsMainOrder && !orderUpdateValues.IsContraOrder)
        {
            orderUpdateValues.IsHedgeOrder = IsHedgeOrder(orderUpdateValues);
        }

        bool isSingleLeg = IsHedgeOrder(orderUpdateValues) || (parseAsSingle ?? IsSingleLeg);
        orderUpdateValues.Filled = execReport.CumQty != 0 ? execReport.CumQty : -1;
        orderUpdateValues.CumQuantity = execReport.CumQty;
        orderUpdateValues.LastQuantity = execReport.LastQty;

        int inverter = 1;

        if (TicketStyle == OrderTicketStyle.Combined &&
           OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
           IsContraOrder(orderUpdateValues) &&
           !isSingleLeg)
        {
            inverter = -1;
        }

        bool isBuySide = isSingleLeg ? execReport.Side == ZeroPlus.Models.Data.Enums.Side.Buy : execReport.Price > 0.0;
        int filledQty = execReport.CumQty;
        int leavesQty = execReport.LeavesQty;
        double displayPx = execReport.Price * inverter;
        double fillPx = Math.Round((execReport.AvgPrice * inverter), 2);
        string lastExch = !string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "";
        int cumQty = execReport.CumQty;
        switch (orderStatus)
        {
            case OrderStatus.New:
                orderUpdateValues.Status = $"Order Placed. {execReport.Qty:n0} @ {displayPx:n2}";
                orderUpdateValues.StatusMode = StatusMode.Reset;
                orderUpdateValues.IsCancelEnabled = true;
                orderUpdateValues.IsModifyEnabled = true;
                orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                break;
            case OrderStatus.PendingNew:
                orderUpdateValues.Status = $"Placing Order. {execReport.Qty:n0} @ {displayPx:n2}";
                orderUpdateValues.StatusMode = StatusMode.Pending;
                orderUpdateValues.IsCancelEnabled = false;
                orderUpdateValues.IsModifyEnabled = false;
                orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                break;
            case OrderStatus.PendingCancel when filledQty > 0:
            case OrderStatus.PendingReplace when filledQty > 0:
            case OrderStatus.PartiallyFilled:
                double ordPx = Math.Round(displayPx, 2);
                string suffix = "";
                if (ordPx != fillPx)
                {
                    suffix = "AUC Frm " + ordPx.ToString("#,###.00####") + " ";
                }

                orderUpdateValues.Status = $"Partially Filled. {filledQty} " +
                                           $"@ {fillPx:#,###.00####} {suffix}- " +
                                           $"Rem: {leavesQty}" +
                                           $"{lastExch}";
                orderUpdateValues.StatusMode = isBuySide ? StatusMode.NewBuy : StatusMode.NewSell;
                orderUpdateValues.AveragePrice = execReport.AvgPrice;
                orderUpdateValues.AveragePriceAfterFees = execReport.AvgPrice + GetTotalFeesForTicket(execReport.Route, execReport.LastExchange);
                orderUpdateValues.UnderlyingMidPrice = UnderMid;
                orderUpdateValues.IsCancelEnabled = true;
                orderUpdateValues.IsModifyEnabled = true;
                orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                break;
            case OrderStatus.Filled:
                OrderIsClosed = true;
                ordPx = Math.Round(displayPx, 2);
                suffix = "";
                if (ordPx != fillPx)
                {
                    suffix = "AUC Frm " + ordPx.ToString("#,###.00####") + " ";
                }
                orderUpdateValues.Status = $"Filled. {filledQty} " +
                                           $"@ {fillPx:#,###.00####} {suffix}" +
                                           $"{lastExch}";
                orderUpdateValues.StatusMode = isBuySide ? StatusMode.FilledBuy : StatusMode.FilledSell;
                orderUpdateValues.IsCancelEnabled = false;
                orderUpdateValues.IsModifyEnabled = false;
                orderUpdateValues.AveragePrice = execReport.AvgPrice;
                orderUpdateValues.AveragePriceAfterFees = execReport.AvgPrice + GetTotalFeesForTicket(execReport.Route, execReport.LastExchange);
                orderUpdateValues.UnderlyingMidPrice = UnderMid;
                orderUpdateValues.ClearOrderIdSet = true;
                orderUpdateValues.IsSubmitEnabled = true;
                break;
            case OrderStatus.Canceled:
                OrderIsClosed = true;
                orderUpdateValues.Status = execReport.CumQty == 0 && execReport.CumQty == 0
                                         ? $"Canceled. {execReport.Qty:n0} @ {displayPx:n2}"
                                         : $"Canceled. Partially Filled {cumQty} " +
                                           $"@ {fillPx:#,###.00####}";
                orderUpdateValues.AveragePrice = execReport.CumQty == 0 ? double.NaN : execReport.AvgPrice;
                orderUpdateValues.AveragePriceAfterFees = execReport.CumQty == 0 ? double.NaN : execReport.AvgPrice;
                orderUpdateValues.UnderlyingMidPrice = execReport.CumQty == 0 ? double.NaN : UnderMid;
                orderUpdateValues.StatusMode = isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell;
                orderUpdateValues.IsCancelEnabled = false;
                orderUpdateValues.IsModifyEnabled = false;
                orderUpdateValues.ClearOrderIdSet = true;
                orderUpdateValues.IsSubmitEnabled = true;
                break;
            case OrderStatus.Rejected:
                OrderIsClosed = true;
                orderUpdateValues.Status = $"Rejected {execReport.Message}. {execReport.Qty:n0} @ {displayPx:n2}";
                orderUpdateValues.StatusMode = isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell;
                orderUpdateValues.IsCancelEnabled = false;
                orderUpdateValues.IsModifyEnabled = false;
                orderUpdateValues.ClearOrderIdSet = true;
                orderUpdateValues.IsSubmitEnabled = true;
                if (OmsCore.Config.ShowPopupOnRejectedOrder)
                {
                    ShowMessage($"Order Rejected: {execReport.Message}", "Order Rejected");
                }
                break;
            case OrderStatus.Replaced:
                orderUpdateValues.Status = $"Replaced. {execReport.Qty:n0} @ {displayPx:n2}";
                orderUpdateValues.StatusMode = StatusMode.Reset;
                orderUpdateValues.IsCancelEnabled = true;
                orderUpdateValues.IsModifyEnabled = true;
                orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                break;
        }

        if (execReport.IsCancelReject)
        {
            orderUpdateValues.Status = $"Replace Rejected {execReport.Message}. {execReport.Qty:n0} @ {displayPx:n2}";

            orderUpdateValues.StatusMode = isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell;
            if (OmsCore.Config.ShowPopupOnRejectedOrder)
            {
                ShowMessage($"Cancel/Replace Rejected: {execReport.Message}", "Cancel/Replace Rejected");
            }
            orderUpdateValues.IsCancelEnabled = false;
            orderUpdateValues.IsModifyEnabled = false;
            orderUpdateValues.IsSubmitEnabled = true;
        }

        return orderUpdateValues;
    }

    private void GetOrderType(OrderUpdateValues orderUpdateValues, out bool isMainOrder, out bool isContraOrder,
        out bool isHedgeOrder)
    {
        isMainOrder = false;
        isContraOrder = false;
        isHedgeOrder = false;

        if (IsMainOrder(orderUpdateValues))
        {
            isMainOrder = true;
        }
        else if (IsContraOrder(orderUpdateValues))
        {
            isContraOrder = true;
        }
        else if (IsHedgeOrder(orderUpdateValues))
        {
            isHedgeOrder = true;
        }
    }

    private void CheckForStopLoss(OrderUpdateModel execReport, OrderUpdateValues orderUpdateValues, bool isMainOrder, ExecutionType? executionType, OrderStatus status)
    {
        Side side = isMainOrder ? (Side)Side : (Side)Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
        if (executionType != null && executionType.Value.IsFilled())
        {
            double price = orderUpdateValues.AveragePrice;
            int qty = side == ZeroPlus.Models.Data.Enums.Side.Buy ? orderUpdateValues.LastQuantity : -orderUpdateValues.LastQuantity;

            lock (PositionUpdateLock)
            {
                SingleOrderTicketPosition += qty;
                SingleOrderTicketWorkingPosition -= qty;
            }

            if (SingleOrderTicketStopLossEnabled)
            {
                if (SingleOrderTicketStopLossUsePercentage)
                {
                    SingleOrderTicketStopLoss = orderUpdateValues.AveragePrice * SingleOrderTicketStopLossPercentage;
                }
                switch (side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        SingleOrderTicketStopLossValue = orderUpdateValues.AveragePrice - SingleOrderTicketStopLoss;
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                        SingleOrderTicketStopLossValue = orderUpdateValues.AveragePrice + SingleOrderTicketStopLoss;
                        break;
                }
            }

            if (SingleOrderTicketTrailingStopEnabled)
            {
                if (SingleOrderTicketTrailingStopUsePercentage)
                {
                    SingleOrderTicketTrailingStop = orderUpdateValues.AveragePrice * SingleOrderTicketTrailingStopPercentage;
                }
                switch (side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        _bidAtFillForSingleTickets = Low;
                        _askAtFillForSingleTickets = High;
                        SingleOrderTicketTrailingStopValue = orderUpdateValues.AveragePrice - SingleOrderTicketTrailingStop;
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                        _bidAtFillForSingleTickets = Low;
                        _askAtFillForSingleTickets = High;
                        SingleOrderTicketTrailingStopValue = orderUpdateValues.AveragePrice + SingleOrderTicketTrailingStop;
                        break;
                }
            }
        }
        else if (status is OrderStatus.Canceled or OrderStatus.Rejected)
        {
            int leavesQty = execReport.Qty - execReport.CumQty;
            int qty = side == ZeroPlus.Models.Data.Enums.Side.Buy ? leavesQty : -leavesQty;
            lock (PositionUpdateLock)
            {
                SingleOrderTicketWorkingPosition -= qty;
            }
        }
    }

    internal void ResetVolumeCounter(bool onlyIfFlat = true)
    {
        if (onlyIfFlat && _spreadPosition != 0)
        {
            return;
        }
        VolumeAtFill = double.NaN;
        ChangeInVolume = double.NaN;
    }

    private void CheckForActiveUncheck()
    {
        if (IsBasketOrder && BasketSettings.ActiveUncheckEnabled)
        {
            if (FilledQty >= OmsCore.Config.ActiveUncheckQty)
            {
                Side side;
                if (IsSingleLeg)
                {
                    side = Legs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                }
                else
                {
                    side = AveragePrice < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                }
                double edge;
                switch (side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        double buy = Math.Abs(AveragePrice);
                        double sell = Math.Abs(ContraAveragePrice);
                        edge = sell - buy;
                        break;
                    default:
                        buy = Math.Abs(ContraAveragePrice);
                        sell = Math.Abs(AveragePrice);
                        edge = sell - buy;
                        break;
                }

                if (edge <= OmsCore.Config.ActiveUncheckEdge)
                {
                    Active = false;
                }
            }
        }
    }

    private void HedgeLastFill(OrderUpdateModel execReport)
    {
        try
        {
            var side = execReport.Side;
            if (IsBasketOrder)
            {
                ShowMessage("Hedge from basket disabled! Use tickets for hedging", SpreadId);
                _log.Warn("Hedge from basket attempt Spread: " + SpreadId + ", Side: " + side);
                return;
            }
            double delta = -TotalDelta;
            if (Legs.Count > 1 && Side != side)
            {
                delta = TotalDelta;
            }

            int qty = execReport.LastQty;
            qty = side == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(qty) : Math.Abs(qty);

            double tempTotalStocks = qty * delta * 100;
            if (double.IsNaN(tempTotalStocks) || double.IsInfinity(tempTotalStocks))
            {
                return;
            }

            int totalStocks = (int)Math.Round(tempTotalStocks * HedgeMultiplier * StockHedgePercent);

            HedgeWithStockAsync(totalStocks);
        }
        catch (SendOrderServerException ex)
        {
            _log.Error(ex, nameof(HedgeLastFill));
            ShowMessage(ex.Message, "Order Hedge Failed");
        }
        catch (SlimException ex)
        {
            _log.Error(ex, nameof(HedgeLastFill));
            ShowMessage(ex.Message, "Order Hedge Failed");
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(HedgeLastFill));
        }
    }

    private async Task<bool> CheckForSmartRouteAsync(OrderUpdateModel execReport, OrderUpdateValues orderUpdateValues, OmsOrder order, OrderStatus status)
    {
        if (_usingSmartRoute && IsMainOrder(orderUpdateValues))
        {
            try
            {
                switch (status)
                {
                    case OrderStatus.Canceled:
                        await Task.Run(() => SubmitOrderAsync(isContra: false));
                        return true;
                    case OrderStatus.PartiallyFilled:
                        _smartRouteFilledQty += execReport.LastQty;
                        break;
                    case OrderStatus.Filled:
                    case OrderStatus.Rejected:
                        ResetSmartRoutes();
                        _smartRouteFilledQty = 0;
                        break;
                }
            }
            catch (RouteSelectionException)
            {
                ResetSmartRoutes();
                _smartRouteFilledQty = 0;
            }
        }
        else if (_contraUsingSmartRoute && IsContraOrder(orderUpdateValues))
        {
            try
            {
                switch (status)
                {
                    case OrderStatus.Canceled:
                        await Task.Run(() => SubmitOrderAsync(isContra: true));
                        return true;
                    case OrderStatus.PartiallyFilled:
                        _contraSmartRouteFilledQty += execReport.LastQty;
                        break;
                    case OrderStatus.Filled:
                    case OrderStatus.Rejected:
                        _contraUsingSmartRoute = false;
                        _contraSmartRouteOverwatchTimer.Stop();
                        _contraSmartRouteFilledQty = 0;
                        break;
                }
            }
            catch (RouteSelectionException)
            {
                _contraUsingSmartRoute = false;
                _contraSmartRouteOverwatchTimer.Stop();
                _contraSmartRouteFilledQty = 0;
            }
        }
        else
        {
            if (status is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.Rejected)
            {
                _manualRouteFish = null;
                _ = Task.Run(() => OrderClosedUpdateEvent?.Invoke(order, status, this));
            }
        }

        return false;
    }

    protected void RequestCancel(OrderUpdateModel execReport)
    {
        if (OmsCore.Config.IsAlgoRoute(route: execReport.Route))
        {
            _log.Warn("Cancel not supported on current route. Spread: " + SpreadId);
            return;
        }

        var localId = execReport.ClientOrderId;
        var permId = execReport.OrigOrderId;
        var orderId = execReport.OrderId;
        var submitTime = NewStatusTimeStamp;

        CheckForRestTimeAndCancel(true, submitTime, orderId, permId, localId);
    }

    private void CheckForRestTimeAndCancel(bool main, DateTime submitTime, string orderId, string permId, string localId)
    {
        double timeSpan = (DateTime.Now - submitTime).TotalMilliseconds;
        int auction = GetMinRestPeriod();

        if (auction > 0)
        {
            if ((main ? _mainNewTimestamp : _contraNewTimestamp) == default)
            {
                _log.Warn("Premature cancel detected. Spread: " + SpreadId + " " + main + ", Passed: 0" +
                          ", Remaining: " + auction + ", Order Id: " + orderId +
                          ", Perm Id: " + permId + ", Local Id: " + localId);
                Task.Delay(auction).ContinueWith(t =>
                {
                    if ((main && MainResting) || (!main && ContraResting))
                    {
                        SendCancelRequest(main: main, orderId, permId, localId);
                    }
                });
                return;
            }

            if (timeSpan < auction)
            {
                var delay = (int)(auction - timeSpan);
                _log.Warn("Premature cancel detected. Spread: " + SpreadId + " " + main + ", Passed: " + timeSpan +
                          ", Remaining: " + delay + ", Order Id: " + orderId + ", Perm Id: " +
                          permId + ", Local Id: " + localId);
                Task.Delay(delay).ContinueWith(t =>
                {
                    if ((main && MainResting) || (!main && ContraResting))
                    {
                        SendCancelRequest(main: main, orderId, permId, localId);
                    }
                });
                return;
            }
        }

        SendCancelRequest(main: main, orderId, permId, localId);
    }

    protected void Resubmit()
    {
        _resubmitWhenReceivingCancelStatus = false;
        _ = SetEdgeAsync(ignoreAdjTheoRiskCheck: false).ContinueWith(t =>
        {
            if (DateTime.Now - t.Result > RiskTimeSpan)
            {
                ShowErrorMessage("Set Edge Timeout!");
            }
            else
            {
                _ = SubmitAsync();
            }
        });
    }
}
