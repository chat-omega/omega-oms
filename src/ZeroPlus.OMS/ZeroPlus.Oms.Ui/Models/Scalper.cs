using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.TagCodecLib;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Models;

public partial class Scalper : OrderUpdateHandler, IOmsDataSubscriber, IOmsPositionSubscriber, IQuoteDisplay
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentStack<TradeUnit> _sells = new();
    private readonly ConcurrentStack<TradeUnit> _buys = new();

    private readonly HashSet<string> _buyHedgeOrderIdsSet;
    private readonly MDUnderlying _underlyingDetails;
    private readonly OmsCore _omsCore;

    private readonly object _hedgeLock = new();
    private readonly Timer _checkTimer;

    private DateTime _lastHedgeTime;
    private double _lastHedgeDelta = double.NaN;
    private double _underlyingClosing = double.NaN;
    private bool _underlyingClosingInitialized;
    private string _hedgeOrderId;
    private Side? _lastSide;
    private bool _firstHedgeSet;


    private readonly ConcurrentDictionary<string, OrderModel> _orderIdToOrderModelMap = new();
    public bool IsDisposed { get; set; }
    public override OrderSubType? SubType { get; set; } = OrderSubType.GammaScalp;
    public ObservableCollection<OrderModel> Orders { get; }
    public bool Active { get; set; } = true;
    public Side? Side { get; set; }
    public int Ratio { get; set; } = 1;
    public int Quantity { get; set; } = 0;
    public int ActualQty { get; set; } = 0;
    public double ManualAvgCost { get; set; } = double.NaN;
    public double ManualRealPnl { get; set; } = double.NaN;
    public double ManualUnrealPnl { get; set; } = double.NaN;
    public ExpirationInfoModel ExpirationInfo { get; set; } = null;
    public StrikeInfoModel Strike { get; set; } = default;
    public string Type { get; set; } = "STOCK";
    public string Position { get; set; } = "-";
    public double Ema { get; set; } = double.NaN;
    public double Delta { get; set; } = double.NaN;
    public double GammaAdjustedDelta { get; set; } = double.NaN;
    public double DeltaModeled { get; set; } = double.NaN;
    public double ThetaModeled { get; set; } = double.NaN;
    public double GammaModeled { get; set; } = double.NaN;
    public double Gamma { get; set; } = double.NaN;
    public double Vega { get; set; } = double.NaN;
    public double Theta { get; set; } = double.NaN;
    public double Rho { get; set; } = double.NaN;
    public double Implied { get; set; } = double.NaN;

    [Bindable]
    public partial bool SmartHedgeEnabled { get; set; }
    [Bindable]
    public partial double SmartHedgeBidPercent { get; set; }
    [Bindable(Default = 1000)]
    public partial double SmartHedgeRestPeriod { get; set; }
    [Bindable(Default = 500)]
    public partial int MaxHwTimeDiff { get; set; }
    [Bindable(Default = GreekSource.AdjHanweck)]
    public partial GreekSource GreekSource { get; set; }
    [Bindable]
    public partial OrderModel LastOrderModel { get; set; }
    [Bindable]
    public partial ObservableCollection<string> RoutesList { get; set; }
    [Bindable]
    public partial OrderTicket OrderTicket { get; set; }
    [Bindable]
    public partial string Symbol { get; set; }
    [Bindable]
    public partial string HedgeSymbol { get; set; }
    [Bindable]
    public partial double HedgeMultiplier { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double Last { get; set; }
    [Bindable]
    public partial double RealUnderBid { get; set; }
    [Bindable]
    public partial double RealUnderMid { get; set; }
    [Bindable]
    public partial double RealUnderAsk { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double Bid { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double Mid { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double Ask { get; set; }
    [Bindable]
    public partial int BuyQty { get; set; }
    [Bindable]
    public partial int SellQty { get; set; }
    [Bindable]
    public partial double AvgBuy { get; set; }
    [Bindable]
    public partial double AvgSell { get; set; }
    [Bindable]
    public partial double NetChange { get; set; }
    [Bindable]
    public partial string NetPosition { get; set; }
    [Bindable]
    public partial double NetDelta { get; set; }
    [Bindable]
    public partial double TotalDelta { get; set; }
    [Bindable]
    public partial double NetGamma { get; set; }
    [Bindable]
    public partial double NetTheta { get; set; }
    [Bindable]
    public partial int HedgeQty { get; set; }
    [Bindable]
    public partial string BuyStatus { get; set; }
    [Bindable]
    public partial StatusMode BuyStatusMode { get; set; }
    [Bindable]
    public partial string Status { get; set; }
    [Bindable]
    public partial StatusMode StatusMode { get; set; }
    [Bindable]
    public partial string SellStatus { get; set; }
    [Bindable]
    public partial StatusMode SellStatusMode { get; set; }
    [Bindable]
    public partial int FilledQty { get; set; }
    [Bindable]
    public partial int WorkingQty { get; set; }
    [Bindable]
    public partial string Route { get; set; }
    [Bindable]
    public partial bool MinHedgeIntervalEnabled { get; set; }
    [Bindable]
    public partial double MinHedgeInterval { get; set; }
    [Bindable]
    public partial bool MinDeltaChangeEnabled { get; set; }
    [Bindable]
    public partial double MinDeltaChange { get; set; }
    [Bindable]
    public partial double ScalpPnl { get; set; }
    [Bindable]
    public partial double ScalpUnrealPnl { get; set; }
    [Bindable]
    public partial double ScalpNetPnl { get; set; }
    [Bindable]
    public partial double PositionUnrealPnl { get; set; }
    [Bindable]
    public partial double PositionRealPnl { get; set; }
    [Bindable]
    public partial double PositionNetPnl { get; set; }
    [Bindable]
    public partial double NetPnl { get; set; }
    [Bindable]
    public partial bool IsCancelEnabled { get; set; }
    [Bindable]
    public partial bool InSync { get; set; }
    [Bindable]
    public partial bool IsSubmitEnabled { get; set; }
    [Bindable]
    public partial string InstanceId { get; set; }
    [Bindable(Default = 1)]
    public partial double HedgePercent { get; set; }
    [Bindable]
    public partial bool RoundDeltas { get; set; }
    [Bindable]
    public partial bool Running { get; set; }

    public Scalper(OmsCore omsCore, OrderTicket orderTicket, string underlyingSymbol, string hedgeUnderlying = null, double hedgeMultiplier = 1, MDUnderlying details = null)
    {
        _omsCore = omsCore;
        _buyHedgeOrderIdsSet = new HashSet<string>();
        _underlyingDetails = details ?? _omsCore.QuoteClient.GetUnderlyingDetails(Symbol);
        Symbol = underlyingSymbol;
        HedgeSymbol = hedgeUnderlying ?? underlyingSymbol;
        HedgeMultiplier = hedgeMultiplier;
        MinHedgeInterval = 500;
        InstanceId = Guid.NewGuid().ToString().Split('-')[0];
        OrderTicket = orderTicket;

        _checkTimer = new Timer(500)
        {
            AutoReset = false,
        };
        _checkTimer.Elapsed += CheckTimer_Elapsed;
        _checkTimer.Start();

        _omsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.LastPrice, this);
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
        _omsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Subscribe(HedgeSymbol, SubscriptionFieldType.Ask, this);

        Orders = new ObservableCollection<OrderModel>();

        Route = OmsCore.Config.DefaultHedgeRoute(OmsCore.Config.InstanceModeV3);
        _omsCore.QuoteClient.GetSnapshotAsync(HedgeSymbol, SubscriptionFieldType.PreviousClose)
            .ContinueWith(t =>
            {
                _underlyingClosing = t.Result;
                _underlyingClosingInitialized = true;
            });
    }

    private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        try
        {
            CheckForTrade();
        }
        finally
        {
            _checkTimer.Start();
        }
    }

    public void SubmitStocks()
    {
        try
        {
            HedgeWithStock(HedgeQty, InstanceId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(SubmitStocks));
        }
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
    {
        string symbol = key.Symbol;
        SubscriptionFieldType type = key.Type;
        if (value is double update)
        {
            switch (type)
            {
                case SubscriptionFieldType.LastPrice:
                    _last = update;
                    if (_underlyingClosingInitialized)
                    {
                        NetChange = _last - _underlyingClosing;
                    }
                    break;

                case SubscriptionFieldType.Bid:
                    if (symbol == Symbol)
                    {
                        RealUnderBid = update;
                    }
                    if (symbol == HedgeSymbol)
                    {
                        Bid = update;
                    }
                    UpdateMid();
                    break;
                case SubscriptionFieldType.Ask:
                    if (symbol == Symbol)
                    {
                        RealUnderAsk = update;
                    }
                    if (symbol == HedgeSymbol)
                    {
                        Ask = update;
                    }
                    UpdateMid();
                    break;
            }
        }
    }

    private void UpdateMid()
    {
        RealUnderMid = (RealUnderBid + RealUnderAsk) / 2;
        Mid = (Bid + Ask) / 2;
        CheckForTrade();
        UpdateNetPnl();
    }

    public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
    {
        CheckForTrade();
    }

    private void CheckForTrade()
    {
        if (OrderTicket != null)
        {
            Update();
        }
    }

    private void UpdateNetPnl()
    {
        if (OrderTicket == null)
        {
            return;
        }
        while (!_buys.IsEmpty && !_sells.IsEmpty)
        {
            if (_sells.TryPop(out TradeUnit sell))
            {
                if (_buys.TryPop(out TradeUnit buy))
                {
                    double netPnl = sell.NetPrice - buy.NetPrice;
                    ScalpPnl += netPnl;
                }
                else
                {
                    _sells.Push(sell);
                }
            }
        }

        double openPositionAveragePrice = 0.0;
        if (!_sells.IsEmpty)
        {
            openPositionAveragePrice += _sells.Sum(x => x.Price);
        }
        if (!_buys.IsEmpty)
        {
            openPositionAveragePrice -= _buys.Sum(x => x.Price);
        }
        openPositionAveragePrice = FilledQty != 0 ? Math.Abs(openPositionAveragePrice / Math.Abs(FilledQty)) : 0;

        if (FilledQty < 0)
        {
            ScalpUnrealPnl = (openPositionAveragePrice - Ask) * Math.Abs(FilledQty);
        }
        else if (FilledQty > 0)
        {
            ScalpUnrealPnl = (Bid - openPositionAveragePrice) * FilledQty;
        }
        else
        {
            ScalpUnrealPnl = 0;
        }

        double positionUnrealPnl = 0.0;
        double positionRealPnl = 0.0;
        foreach (var position in OrderTicket.Legs)
        {
            if (position.AddToPnl)
            {
                positionUnrealPnl += position.ManualUnrealPnl;
                positionRealPnl += position.ManualRealPnl;
            }
        }

        PositionUnrealPnl = positionUnrealPnl;
        PositionRealPnl = positionRealPnl;

        ScalpNetPnl = ScalpPnl + ScalpUnrealPnl;
        PositionNetPnl = PositionUnrealPnl + PositionRealPnl;
        NetPnl = ScalpNetPnl + PositionNetPnl;
    }

    internal void Update()
    {
        try
        {
            double totalDelta = 0.0;
            double netDelta = 0.0;
            double netGamma = 0.0;
            double netTheta = 0.0;
            double mid = RealUnderMid;

            bool inSync = true;

            lock (_hedgeLock)
            {
                DateTime? time = null;
                ulong? seq = null;
                foreach (TicketLegModel position in OrderTicket.Legs)
                {
                    double greeksDelta;
                    double greeksGamma;
                    double greeksTheta;

                    switch (GreekSource)
                    {
                        case GreekSource.Modeled:
                            GetModeledGreeks(position, mid, out greeksDelta, out greeksGamma, out greeksTheta);
                            break;
                        case GreekSource.Hanweck:
                            time ??= position.HanweckTimestamp;
                            double diff = Math.Abs((time!.Value - position.HanweckTimestamp).TotalMilliseconds);
                            inSync &= (diff < MaxHwTimeDiff);
                            GetHwGreeks(position, out greeksDelta, out greeksGamma, out greeksTheta);
                            break;
                        case GreekSource.AdjHanweck:
                            seq ??= position.DeltaAdjTheoSequence;
                            inSync &= seq == position.DeltaAdjTheoSequence;
                            GetAdjHwGreeks(position, out greeksDelta, out greeksGamma, out greeksTheta);
                            break;
                        default:
                            return;
                    }

                    totalDelta += greeksDelta * position.Ratio;
                    netDelta += greeksDelta * position.ActualQty * position.Multiplier;
                    netGamma += greeksGamma * position.ActualQty * position.Multiplier;
                    netTheta += greeksTheta * position.ActualQty * position.Multiplier;
                }
            }

            TotalDelta = totalDelta;
            NetDelta = netDelta;
            NetGamma = netGamma;
            NetTheta = netTheta;
            InSync = inSync;

            if (!double.IsNaN(netDelta))
            {
                var percentDelta = Math.Round(netDelta * HedgeMultiplier * HedgePercent);
                int required;
                if (RoundDeltas)
                {
                    int num = (int)(percentDelta % 100);
                    if (Math.Abs(num) <= 50)
                    {
                        required = (int)((percentDelta - num) * -1);
                    }
                    else
                    {
                        required = (int)((percentDelta - num + (num > 0 ? 100 : -100)) * -1);
                    }
                }
                else
                {
                    required = (int)percentDelta * -1;
                }

                HedgeQty = required - FilledQty;
                IsSubmitEnabled = HedgeQty != 0 && HedgeQty - WorkingQty != 0;

                if (InSync && IsSubmitEnabled && Running)
                {
                    HedgeWithStock(HedgeQty, InstanceId);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(Update));
        }
    }

    private static void GetHwGreeks(TicketLegModel position, out double greeksDelta, out double greeksGamma,
        out double greeksTheta)
    {
        greeksDelta = position.Delta;
        greeksGamma = position.Gamma;
        greeksTheta = position.Theta;
    }

    private static void GetAdjHwGreeks(TicketLegModel position, out double greeksDelta, out double greeksGamma,
        out double greeksTheta)
    {
        greeksDelta = position.GammaAdjustedDelta;
        greeksGamma = position.Gamma;
        greeksTheta = position.Theta;
    }

    private void GetModeledGreeks(TicketLegModel position, double mid, out double greeksDelta, out double greeksGamma,
        out double greeksTheta)
    {
        Greeks greeks = position.UpdateGreeks(_underlyingDetails, mid);
        greeksDelta = greeks.Delta;
        greeksGamma = greeks.Gamma;
        greeksTheta = greeks.Theta;
    }

    internal void Dispose()
    {
        try
        {
            _checkTimer?.Stop();
            _omsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.LastPrice, this);
            _omsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.Bid, this);
            _omsCore.QuoteClient.Unsubscribe(HedgeSymbol, SubscriptionFieldType.Ask, this);
            _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
            _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(Dispose));
        }
    }

    public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
    {
        OrderStatus? orderStatus = execReport.OrderStatus;
        ExecutionType? executionType = execReport.ExecutionType;

        Side hedgeSide = ZeroPlus.Models.Data.Enums.Side.Buy;
        bool validReport = false;
        if (execReport.Side is ZeroPlus.Models.Data.Enums.Side.Buy or ZeroPlus.Models.Data.Enums.Side.BuyToCover)
        {
            hedgeSide = ZeroPlus.Models.Data.Enums.Side.Buy;
            validReport = true;
        }
        else if (execReport.Side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
        {
            hedgeSide = ZeroPlus.Models.Data.Enums.Side.Sell;
            validReport = true;
        }
        if (validReport)
        {
            if (executionType.Value.IsFilled())
            {
                int hedgeFillQty = hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(execReport.LastQty) : Math.Abs(execReport.LastQty);

                if (hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    int qty = execReport.LastQty;
                    double avgPx = execReport.AvgPrice;
                    AvgSell = ((AvgSell * SellQty) + (avgPx * qty)) / (SellQty + qty);
                    SellQty += qty;
                }
                else
                {
                    int qty = execReport.LastQty;
                    double avgPx = execReport.AvgPrice;
                    AvgBuy = ((AvgBuy * BuyQty) + (avgPx * qty)) / (BuyQty + qty);
                    BuyQty += qty;
                }

                lock (_hedgeLock)
                {
                    FilledQty += hedgeFillQty;
                    WorkingQty -= hedgeFillQty;
                }
                bool isFlat = HedgeQty - WorkingQty == 0;
                IsSubmitEnabled = !isFlat;

                if (!_firstHedgeSet)
                {
                    _firstHedgeSet = true;
                    AvgBuy = 0;
                    BuyQty = 0;
                    AvgSell = 0;
                    SellQty = 0;
                }
                if (_lastSide.HasValue && _lastSide != hedgeSide)
                {
                    if (hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        AvgBuy = 0;
                        BuyQty = 0;
                    }
                    else
                    {
                        AvgSell = 0;
                        SellQty = 0;
                    }
                }
                _lastSide = hedgeSide;

                TradeUnit singleTrade = new()
                {
                    Quantity = 1,
                    Price = execReport.AvgPrice,
                    TotalPrice = execReport.AvgPrice,
                    NetPrice = execReport.AvgPrice,
                };
                for (int i = 0; i < execReport.LastQty; i++)
                {
                    if (hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        _sells.Push(singleTrade);
                    }
                    else
                    {
                        _buys.Push(singleTrade);
                    }
                }

                UpdateNetPnl();
            }
            else if (orderStatus is OrderStatus.Canceled or
                     OrderStatus.Rejected)
            {
                int hedgeQty = execReport.Qty - execReport.CumQty;
                hedgeQty = hedgeSide == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(hedgeQty) : Math.Abs(hedgeQty);
                lock (_hedgeLock)
                {
                    WorkingQty -= hedgeQty;
                    IsSubmitEnabled = HedgeQty - WorkingQty != 0;

                    if (hedgeQty != 0)
                    {
                        CheckForTrade();
                    }
                }
            }
        }

        ParseOrderUpdate(execReport);
    }

    private void ParseOrderUpdate(OrderUpdateModel execReport)
    {
        bool isBuySide = _buyHedgeOrderIdsSet.Contains(execReport.ClientOrderId);

        if (execReport.ClientOrderId != null && _orderIdToOrderModelMap.TryGetValue(execReport.ClientOrderId, out var orderModel))
        {
            orderModel.OrderUpdateModel = new OmsOrderUpdateModel()
            {
                Filled = execReport.CumQty,
                OrderStatus = execReport.OrderStatus,
                Side = isBuySide ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell
            };
            orderModel.OrderStatus = execReport.OrderStatus;
        }

        int inverter = 1;

        if (isBuySide)
        {
            switch (execReport.OrderStatus)
            {
                case OrderStatus.New:
                    BuyStatus = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    BuyStatusMode = StatusMode.Reset;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.PendingNew:
                    BuyStatus = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    BuyStatusMode = StatusMode.Pending;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.PartiallyFilled:
                    BuyStatus = $"Partially Filled {execReport.CumQty} " +
                             $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                             $"Remaining: {execReport.LeavesQty}" +
                             $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                    BuyStatusMode = StatusMode.NewBuy;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.Filled:
                    BuyStatus = $"Filled {execReport.CumQty} " +
                             $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                             $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                    BuyStatusMode = StatusMode.FilledBuy;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Canceled:
                    BuyStatus = execReport.CumQty == 0 && execReport.CumQty == 0
                        ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                        : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                          $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                    BuyStatusMode = StatusMode.CancelledBuy;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Rejected:
                    BuyStatus = $"Rejected {execReport.Message}";
                    BuyStatusMode = StatusMode.RejectedBuy;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Replaced:
                    BuyStatus = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    BuyStatusMode = StatusMode.Reset;
                    IsCancelEnabled = false;
                    break;
            }

            if (execReport.IsCancelReject)
            {
                BuyStatus = $"Cancel Rejected {execReport.Message}";
                BuyStatusMode = StatusMode.RejectedBuy;
            }
        }
        else
        {
            switch (execReport.OrderStatus)
            {
                case OrderStatus.New:
                    SellStatus = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    SellStatusMode = StatusMode.Reset;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.PendingNew:
                    SellStatus = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    SellStatusMode = StatusMode.Pending;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.PartiallyFilled:
                    SellStatus = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                    SellStatusMode = StatusMode.NewSell;
                    IsCancelEnabled = true;
                    break;
                case OrderStatus.Filled:
                    SellStatus = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                    SellStatusMode = StatusMode.FilledSell;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Canceled:
                    SellStatus = execReport.CumQty == 0 && execReport.CumQty == 0
                        ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                        : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                          $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                    SellStatusMode = StatusMode.CancelledSell;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Rejected:
                    SellStatus = $"Rejected {execReport.Message}";
                    SellStatusMode = StatusMode.RejectedSell;
                    IsCancelEnabled = false;
                    break;
                case OrderStatus.Replaced:
                    SellStatus = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                    SellStatusMode = StatusMode.Reset;
                    IsCancelEnabled = false;
                    break;
            }

            if (execReport.IsCancelReject)
            {
                SellStatus = $"Cancel Rejected {execReport.Message}";
                SellStatusMode = StatusMode.RejectedSell;
            }
        }
    }

    public override void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
    {
        Status = $"Cancel Rejected {orderCancelReject.Comment}";
        StatusMode = StatusMode.RejectedBuy;
    }

    private void HedgeWithStock(int hedgeQty, string comment = null)
    {
        try
        {
            lock (_hedgeLock)
            {
                int stockHedgeQty = hedgeQty - WorkingQty;

                if (stockHedgeQty != 0)
                {
                    var orderInfo = BuildStockHedgeOrderAsync(stockHedgeQty, comment, forceLean: false);
                    if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
                    {
                        if (double.IsNaN(orderInfo.Price))
                        {
                            Status = "[Risk] Hedge price could not be determined.";
                            StatusMode = StatusMode.NewSell;
                            return;
                        }
                        if (orderInfo.Price > OmsCore.Config.MaxAutoHedgeNetCash)
                        {
                            Status = "[Risk] Hedge price above risk limit.";
                            StatusMode = StatusMode.NewSell;
                            return;
                        }
                    }

                    if (OmsCore.Config.MaxAutoHedgePositionEnabled)
                    {
                        if (orderInfo.Qty > OmsCore.Config.MaxAutoHedgePosition)
                        {
                            Status = "[Risk] Hedge qty above risk limit.";
                            StatusMode = StatusMode.NewSell;
                            return;
                        }
                    }

                    if (MinHedgeIntervalEnabled && (DateTime.Now - _lastHedgeTime).TotalMilliseconds < MinHedgeInterval)
                    {
                        Status = "Min Hedge Interval Not Satisfied.";
                        StatusMode = StatusMode.CancelledSell;
                        return;
                    }

                    if (MinDeltaChangeEnabled && Math.Abs(TotalDelta - _lastHedgeDelta) < MinDeltaChange)
                    {
                        Status = "Min Delta Change Not Satisfied.";
                        StatusMode = StatusMode.CancelledSell;
                        return;
                    }

                    Status = "";
                    StatusMode = StatusMode.Reset;

                    _hedgeOrderId = _omsCore.OrderClient.SendOrder(orderInfo, OrderTicket.GetInstanceMode(), this, false, 1);
                    _lastHedgeTime = DateTime.Now;
                    _lastHedgeDelta = TotalDelta;
                    WorkingQty += stockHedgeQty;
                    if (orderInfo.OMSSide == ZeroPlus.Models.Data.Enums.Side.Buy.ToString().ToUpper())
                    {
                        _buyHedgeOrderIdsSet.Add(_hedgeOrderId);
                    }
                    OrderModel orderModel = new()
                    {
                        LocalId = orderInfo.LocalID,
                        Symbol = orderInfo.Symbol,
                        Price = orderInfo.Price,
                        Qty = Math.Abs(orderInfo.Qty),
                        Side = stockHedgeQty > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        Timestamp = DateTime.Now,
                        Bid = OrderTicket.Bid,
                        Ask = OrderTicket.Ask,
                        Mid = OrderTicket.Mid,
                        UnderBid = Bid,
                        UnderMid = Mid,
                        UnderAsk = Ask,
                        TotalDelta = TotalDelta,
                        NetDelta = NetDelta,
                    };
                    _orderIdToOrderModelMap[orderModel.LocalId] = orderModel;
                    OrderTicket.Dispatcher?.BeginInvoke(() =>
                    {
                        Orders.Add(orderModel);
                        LastOrderModel = orderModel;
                    });
                }
            }
        }
        catch (SlimException ae)
        {
            Status = ae.Message;
            StatusMode = StatusMode.NewSell;
        }
    }

    internal OpsOrderModel BuildStockHedgeOrderAsync(int qty, string comment = null, bool forceLean = false)
    {
        Side side = qty < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;

        var subtype = SubType;
        double bid = Bid;
        double ask = Ask;
        string type = "LIMIT";

        double cancelDelay;
        double price;
        if (SmartHedgeEnabled)
        {
            double bidPercent = Math.Abs(bid - ask) * SmartHedgeBidPercent;
            price = side == ZeroPlus.Models.Data.Enums.Side.Buy ? bid + bidPercent : ask - bidPercent;
            cancelDelay = SmartHedgeRestPeriod;
        }
        else
        {
            price = side == ZeroPlus.Models.Data.Enums.Side.Buy ? ask : bid;
            cancelDelay = OmsCore.Config.HedgeInterval;
        }

        comment ??= GeHedgeIdentifier();

        string tif = ZeroPlus.Models.Data.Enums.TimeInForce.DAY.ToString();
        if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
        {
            tif = Route.StartsWith("D") ?
                ZeroPlus.Models.Data.Enums.TimeInForce.GTX.ToString() :
                ZeroPlus.Models.Data.Enums.TimeInForce.ETH.ToString();
        }

        qty = Math.Abs(qty);

        var order = new OpsOrderModel()
        {
            Symbol = HedgeSymbol,
            Qty = qty,
            OMSSide = side.ToString(),
            OpenClose = "Auto",
            Price = Math.Round(price, 2),
            Account = OmsCore.Config.DefaultAccount,
            Tif = tif,
            Route = Route,
            OMSOrderType = type,
            Timestamp = DateTime.Now,
            UnderlyingSymbol = HedgeSymbol,
            MinUnderBid = double.MinValue,
            MaxUnderAsk = double.MaxValue,

            BaseStrategy = OrderTicket.BaseStrategy,
            Currency = OrderTicket.Currency,
            SpreadId = OrderTicket.SpreadId,
            Security = _omsCore.SecurityBook.GetSecurity(HedgeSymbol),
            Side = side,
            MinimumTickStyle = OrderTicket.MinimumTickStyle,
            Quantity = qty,
            Bid = OrderTicket.Bid,
            Mid = OrderTicket.Mid,
            Ask = OrderTicket.Ask,
            Ema = OrderTicket.Ema,

            DigBid = OrderTicket.DigBid,
            DigAsk = OrderTicket.DigAsk,
            DigBidSize = OrderTicket.DigBidSize,
            DigAskSize = OrderTicket.DigAskSize,
            WeightedVega = OrderTicket.WeightedVega,

            TotalDelta = OrderTicket.Delta,
            HanweckTotalTheo = OrderTicket.HanweckTotalTheo,
            DeltaAdjustedTheo = OrderTicket.DeltaAdjustedTheo,
            UnderBid = OrderTicket.UnderBid,
            UnderAsk = OrderTicket.UnderAsk,
            SubType = OrderSubType.GammaScalp,
            AdjustedEdgeOverride = OrderTicket.AdjustedEdgeOverride,
            EdgeOverride = OrderTicket.EdgeOverride,
            Multiplier = OrderTicket.Multiplier,
            Venue = OrderTicket.GetVenue(OrderTicket.InstanceMode),
            TagEdge = OrderTicket.GetTagEdge(isContra: false),
            AccountAcronym = OrderTicket.AccountAcronym,
            TimeInForce = OrderTicket.TimeInForce,
            PositionEffect = OrderTicket.PositionEffect,
            NewToCancelTime = cancelDelay,
            Comment = comment,
            Destination = "HedgeLocal",
            PrimaryExchange = OrderTicket.PrimaryExchange,

            Tag = new TagCodec(trader: _omsCore.User.Username,
                edge: 0,
                type: _omsCore.OrderClient.TYPE,
                subtype: subtype?.ToSpacedString(),
                tv: 0,
                ema: 0,
                bid: bid,
                ask: ask,
                comment: !string.IsNullOrEmpty(comment) ? comment : "",
                sharedId: OrderTicket.SharedId,
                sequence: OrderTicket.Sequence,
                typeId: (ushort)OrderTicket.TypeId,
                subTypeId: (ushort)ZeroPlus.Models.Data.Enums.SubType.HedgeOpen,
                subTypeSequence: OrderTicket.SubTypeSequence,
                v0: OrderTicket.VolaTheoAdjV0,
                v1: OrderTicket.VolaTheoAdjV1,
                v2: OrderTicket.VolaTheoAdjV2).Encode(),
            OrderTag = new OrderTagModel()
            {
                Trader = _omsCore.User.Username,
                Instance = !string.IsNullOrEmpty(comment) ? comment : "",
                Bid = bid,
                Ask = ask,
                BidSize = (uint)OrderTicket.BidSize,
                AskSize = (uint)OrderTicket.AskSize,
                Theo = OrderTicket.DeltaAdjustedTheo,
                Ema = OrderTicket.Ema,
                UnderBid = OrderTicket.UnderBid,
                UnderAsk = OrderTicket.UnderAsk,
                UnderBidSize = (uint)OrderTicket.UnderlyingBidSize,
                UnderAskSize = (uint)OrderTicket.UnderlyingAskSize,
                Edge = OrderTicket.Edge,
                OrderSubType = SubType ?? OrderSubType.Ticket,
                ModuleType = ModuleType.None,
                VolaTheo = OrderTicket.VolaTheoV0,
                VolaTheoAdj = OrderTicket.VolaTheoAdjV0,
                VolaIv = OrderTicket.VolaIv,
                TheoBid = OrderTicket.TheoBid,
                TheoAsk = OrderTicket.TheoAsk,
                SubType = OrderTicket.SubTypeId,
                SharedId = OrderTicket.SharedId,
                Sequence = OrderTicket.Sequence,
                SubTypeSequence = OrderTicket.SubTypeSequence,
                ResubmitCount = (uint)OrderTicket.ResubmitCount,
                TotalEstimatedResubmit = (uint)OrderTicket.TotalEstimatedResubmit,
                ParentSpreadHash = OrderTicket.SpreadHash ?? string.Empty,
            }
        };
        order.SetCancelDelay(cancelDelay);
        return order;
    }

    private string GeHedgeIdentifier()
    {
        return _omsCore.User.Username.ToUpper() + " - " + OrderTicket.Symbol + " - " + HedgeSymbol.ToUpper();
    }

    public void LiquidateHedge()
    {
        lock (_hedgeLock)
        {
            foreach (TicketLegModel position in OrderTicket.Legs)
            {
                position.ActualQty = 0;
            }

            if (FilledQty != 0)
            {
                HedgeWithStock(-FilledQty, InstanceId);
            }
        }
    }
}