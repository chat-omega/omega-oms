using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.TagCodecLib;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class StockModel : OrderUpdateHandler, IOmsDataSubscriber
    {
        private readonly ConcurrentStack<TradeUnit> _sells = new();
        private readonly ConcurrentStack<TradeUnit> _buys = new();

        public int _FilledQty;
        public int _WorkingQty;
        public int _BuyQty;
        public int _SellQty;
        public double _AvgBuy;
        public double _AvgSell;
        public double _RealPnl;
        public double _UnrealPnl;
        public double _NetPnl;
        public Side _PairSide;

        private string _orderId;
        private readonly ConcurrentDictionary<string, bool> _orderIdsSet = new();
        private int _resubmitAttempt;
        private bool _pausedByPartialFill;
        private readonly ComboTraderViewModel _ComboTraderViewModel;
        protected OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial string Symbol { get; set; }

        [Bindable(Default = true)]
        public partial bool CanEdit { get; set; }

        private readonly object _lock = new();

        [Bindable(Default = 1)]
        public partial int Ratio { get; set; }
        [Bindable]
        public partial int FilledQty { get; set; }
        [Bindable]
        public partial int WorkingQty { get; set; }
        [Bindable]
        public partial int BuyQty { get; set; }
        [Bindable]
        public partial int SellQty { get; set; }
        [Bindable]
        public partial double AvgBuy { get; set; }
        [Bindable]
        public partial double AvgSell { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial Side PairSide { get; set; }

        public double Bid { get; set; } = double.NaN;
        public double Ask { get; set; } = double.NaN;
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public double Low { get; set; } = double.NaN;
        public double High { get; set; } = double.NaN;
        public double Mid { get; set; } = double.NaN;
        public double Volume { get; set; }
        public bool IsDisposed { get; set; }
        public override OrderSubType? SubType { get; set; }
        public double Multiplier => Symbol.StartsWith(".") ? 100 : 1;

        public StockModel(ComboTraderViewModel ComboTraderViewModel)
        {
            _ComboTraderViewModel = ComboTraderViewModel;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            SubscriptionFieldType type = key.Type;
            switch (type)
            {
                case SubscriptionFieldType.TopQuote:
                    if (value is DoubleUpdateModel doubleUpdateModel)
                    {
                        if (Bid != doubleUpdateModel.Bid || Ask != doubleUpdateModel.Ask)
                        {
                            Bid = doubleUpdateModel.Bid;
                            Ask = doubleUpdateModel.Ask;
                            BidSize = doubleUpdateModel.BidSize;
                            AskSize = doubleUpdateModel.AskSize;

                            Mid = (Bid + Ask) / 2;


                            UpdateNetPnl();
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
                case SubscriptionFieldType.Bid:
                    if (value is double bid)
                    {
                        if (Bid != bid)
                        {
                            Bid = bid;
                            Mid = (Bid + Ask) / 2;

                            UpdateNetPnl();
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
                case SubscriptionFieldType.Ask:
                    if (value is double ask)
                    {
                        if (Ask != ask)
                        {
                            Ask = ask;
                            Mid = (Bid + Ask) / 2;

                            UpdateNetPnl();
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
                case SubscriptionFieldType.BidSize:
                    if (value is double bidSize)
                    {
                        AskSize = (int)bidSize;
                    }
                    break;
                case SubscriptionFieldType.AskSize:
                    if (value is double askSize)
                    {
                        AskSize = (int)askSize;
                    }
                    break;
                case SubscriptionFieldType.Low:
                    if (value is double low)
                    {
                        if (Low != low)
                        {
                            Low = low;
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
                case SubscriptionFieldType.High:
                    if (value is double high)
                    {
                        if (High != high)
                        {
                            High = high;
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
                case SubscriptionFieldType.Volume:
                    if (value is double volume)
                    {
                        if (Volume != volume)
                        {
                            Volume = volume;
                            _ComboTraderViewModel?.Update();
                        }
                    }
                    break;
            }
        }

        public void Init()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                switch (_ComboTraderViewModel.DataType)
                {
                    case DataType.Batched:
                        OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
                        OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
                        OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.BidSize, this);
                        OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.AskSize, this);
                        break;
                    case DataType.Live:
                        OmsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.TopQuote, this);
                        break;
                }
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Volume, this);
                CanEdit = false;
            }
        }

        public void Stop()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                OmsCore.UpdateManager.Unsubscribe(Symbol, SubscriptionFieldType.TopQuote, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Volume, this);
            }
            CanEdit = true;
            Bid = double.NaN;
            Ask = double.NaN;
            BidSize = 0;
            AskSize = 0;
            Mid = double.NaN;
            Low = double.NaN;
            High = double.NaN;
            Volume = 0;
        }

        public void Dispose()
        {
            try
            {
                IsDisposed = true;
                Stop();
            }
            catch (Exception)
            {
            }
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            OrderStatus? orderStatus = execReport.OrderStatus;
            ExecutionType? executionType = execReport.ExecutionType;

            Side side = Side.Buy;
            bool validReport = false;

            if (execReport.Side is Side.Buy or Side.BuyToCover)
            {
                side = Side.Buy;
                validReport = true;
            }
            else if (execReport.Side is Side.Sell or Side.SellShort)
            {
                side = Side.Sell;
                validReport = true;
            }

            if (validReport)
            {
                if (orderStatus is OrderStatus.PartiallyFilled or
                    OrderStatus.Filled)
                {
                    int hedgeFillQty = side == Side.Sell ? -Math.Abs(execReport.LastQty) : Math.Abs(execReport.LastQty);
                    int qty = execReport.LastQty;
                    double avgPx = execReport.AvgPrice;
                    if (side == Side.Sell)
                    {
                        AvgSell = ((AvgSell * SellQty) + (avgPx * qty)) / (SellQty + qty);
                        SellQty += qty;
                    }
                    else
                    {
                        AvgBuy = ((AvgBuy * BuyQty) + (avgPx * qty)) / (BuyQty + qty);
                        BuyQty += qty;
                    }

                    lock (_lock)
                    {
                        FilledQty += hedgeFillQty;
                        WorkingQty -= hedgeFillQty;
                    }

                    if (_ComboTraderViewModel.RestOrders)
                    {
                        if (side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            SendRestingSellOrder(qty, avgPx);
                        }
                    }

                    TradeUnit singleTrade = new()
                    {
                        Quantity = 1,
                        Price = execReport.AvgPrice,
                        TotalPrice = execReport.AvgPrice,
                        NetPrice = execReport.AvgPrice,
                    };
                    for (int i = 0; i < execReport.LastQty; i++)
                    {
                        if (side == ZeroPlus.Models.Data.Enums.Side.Sell)
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
                    int qty = execReport.Qty - execReport.CumQty;
                    qty = side == ZeroPlus.Models.Data.Enums.Side.Sell ? -Math.Abs(qty) : Math.Abs(qty);
                    lock (_lock)
                    {
                        WorkingQty -= qty;
                    }

                    if (!string.IsNullOrWhiteSpace(execReport.ClientOrderId))
                    {
                        _orderIdsSet.TryRemove(execReport.ClientOrderId, out bool _);
                    }

                    if (!_ComboTraderViewModel.RestOrders)
                    {
                        if (_ComboTraderViewModel.OrderEnabled)
                        {
                            _pausedByPartialFill = true;
                            _ComboTraderViewModel.OrderEnabled = false;
                        }
                        int leaves = execReport.LeavesQty;
                        if (_resubmitAttempt++ < 3)
                        {
                            if (side != Side.Buy)
                            {
                                leaves = -leaves;
                            }
                            SendOrder(leaves);
                        }
                        _ComboTraderViewModel.ShowMessage("Order Failed!\nSymbol: " + Symbol + "\nQty: " + leaves + "\nSide: " + side);
                    }
                }

                if (orderStatus == OrderStatus.Filled)
                {
                    _resubmitAttempt = 0;

                    if (!string.IsNullOrWhiteSpace(execReport.ClientOrderId))
                    {
                        _orderIdsSet.TryRemove(execReport.ClientOrderId, out bool _);
                    }

                    if (_pausedByPartialFill && !_ComboTraderViewModel.OrderEnabled)
                    {
                        _pausedByPartialFill = false;
                        _ComboTraderViewModel.OrderEnabled = true;
                    }
                }
            }

            ParseOrderUpdate(execReport, side);
        }

        private void UpdateNetPnl()
        {
            while (!_buys.IsEmpty && !_sells.IsEmpty)
            {
                if (_sells.TryPop(out TradeUnit sell))
                {
                    if (_buys.TryPop(out TradeUnit buy))
                    {
                        double netPnl = sell.NetPrice - buy.NetPrice;
                        RealPnl += netPnl;
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
                UnrealPnl = (openPositionAveragePrice - Ask) * Math.Abs(FilledQty);
            }
            else if (FilledQty > 0)
            {
                UnrealPnl = (Bid - openPositionAveragePrice) * FilledQty;
            }
            else
            {
                UnrealPnl = 0;
            }
            NetPnl = (RealPnl + UnrealPnl) * Multiplier;
        }

        private void ParseOrderUpdate(OrderUpdateModel execReport, Side side)
        {

            int inverter = 1;

            bool isBuySide = side == Side.Buy;
            if (isBuySide)
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        _ComboTraderViewModel.Status = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusMode = StatusMode.Reset;
                        break;
                    case OrderStatus.PendingNew:
                        _ComboTraderViewModel.Status = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusMode = StatusMode.Pending;
                        break;
                    case OrderStatus.PartiallyFilled:
                        _ComboTraderViewModel.Status = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        _ComboTraderViewModel.StatusMode = StatusMode.NewBuy;
                        break;
                    case OrderStatus.Filled:
                        _ComboTraderViewModel.Status = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        _ComboTraderViewModel.StatusMode = StatusMode.FilledBuy;
                        break;
                    case OrderStatus.Canceled:
                        _ComboTraderViewModel.Status = execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        _ComboTraderViewModel.StatusMode = StatusMode.CancelledBuy;
                        break;
                    case OrderStatus.Rejected:
                        _ComboTraderViewModel.Status = $"Rejected {execReport.Message}";
                        _ComboTraderViewModel.StatusMode = StatusMode.RejectedBuy;
                        break;
                    case OrderStatus.Replaced:
                        _ComboTraderViewModel.Status = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusMode = StatusMode.Reset;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    _ComboTraderViewModel.Status = $"Cancel Rejected {execReport.Message}";
                    _ComboTraderViewModel.StatusMode = StatusMode.RejectedBuy;
                }
            }
            else
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        _ComboTraderViewModel.StatusSell = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.Reset;
                        break;
                    case OrderStatus.PendingNew:
                        _ComboTraderViewModel.StatusSell = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.Pending;
                        break;
                    case OrderStatus.PartiallyFilled:
                        _ComboTraderViewModel.StatusSell = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.NewSell;
                        break;
                    case OrderStatus.Filled:
                        _ComboTraderViewModel.StatusSell = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.FilledSell;
                        break;
                    case OrderStatus.Canceled:
                        _ComboTraderViewModel.StatusSell = execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.CancelledSell;
                        break;
                    case OrderStatus.Rejected:
                        _ComboTraderViewModel.StatusSell = $"Rejected {execReport.Message}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.RejectedSell;
                        break;
                    case OrderStatus.Replaced:
                        _ComboTraderViewModel.StatusSell = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        _ComboTraderViewModel.StatusModeSell = StatusMode.Reset;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    _ComboTraderViewModel.StatusSell = $"Cancel Rejected {execReport.Message}";
                    _ComboTraderViewModel.StatusModeSell = StatusMode.RejectedSell;
                }
            }
        }

        public override void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            _ComboTraderViewModel.Status = $"Cancel Rejected {orderCancelReject.Comment}";
            _ComboTraderViewModel.StatusMode = StatusMode.RejectedBuy;
        }

        internal void CancelResume()
        {
            _pausedByPartialFill = false;
        }

        internal void ClosePositions()
        {
            List<string> orderIds = _orderIdsSet.Keys.ToList();
            _orderIdsSet.Clear();
            foreach (string orderId in orderIds)
            {
                OmsCore.OrderClient.CancelOrder(new CancelRequest
                {
                    OrderId = orderId
                });
            }

            if (FilledQty != 0)
            {
                int qty = -FilledQty;
                SendMarketOrder(qty);
            }
        }

        internal bool CheckForBuy()
        {
            if (Ratio > 0)
            {
                return Math.Abs(BidSize) >= Math.Abs(Ratio);
            }
            else if (Ratio < 0)
            {
                return Math.Abs(AskSize) >= Math.Abs(Ratio);
            }
            else
            {
                return false;
            }
        }

        internal bool CheckForSell()
        {
            if (Ratio > 0)
            {
                return Math.Abs(AskSize) >= Math.Abs(Ratio);
            }
            else if (Ratio < 0)
            {
                return Math.Abs(BidSize) >= Math.Abs(Ratio);
            }
            else
            {
                return false;
            }
        }

        internal void SendBuyOrder()
        {
            if (_ComboTraderViewModel.RestOrders)
            {
                if (WorkingQty == 0 && FilledQty == 0)
                {
                    SendRestingBuyOrder(Ratio);
                }
            }
            else
            {
                SendOrder(Ratio);
            }
        }

        internal void SendSellOrder()
        {
            SendOrder(-Ratio);
        }

        private void SendMarketOrder(int qty)
        {
            var order = BuildOrder(Symbol, qty, ZeroPlus.Models.Data.Enums.OrderType.Market);
            OmsCore.OrderClient.SendOrder(order, null, this, false, 1);
            WorkingQty += qty;
        }

        private void SendOrder(int qty)
        {
            var order = BuildOrder(Symbol, qty, _ComboTraderViewModel.OrderType);
            OmsCore.OrderClient.SendOrder(order, null, this, false, 1);
            WorkingQty += qty;
        }

        private void SendRestingBuyOrder(int qty)
        {
            qty = Math.Abs(qty);
            var order = BuildOrder(Symbol, qty, ZeroPlus.Models.Data.Enums.OrderType.Limit);
            order.Price = Bid;
            double cancelDelay = _ComboTraderViewModel.RestingOrderCancelDelay;
            order.SetCancelDelay(cancelDelay);
            OmsCore.OrderClient.SendOrder(order, null, this, false, 1);
            WorkingQty += qty;
        }

        private void SendRestingSellOrder(int qty, double avgPx)
        {
            qty = Math.Abs(qty) * -1;
            var order = BuildOrder(Symbol, qty, ZeroPlus.Models.Data.Enums.OrderType.Limit);
            order.Price = avgPx + _ComboTraderViewModel.RestingOrderCloseEdge;
            order.SetCancelDelay(0.0);
            _orderId = OmsCore.OrderClient.SendOrder(order, null, this, false, 1);
            _orderIdsSet.GetOrAdd(_orderId, true);
            WorkingQty += qty;
        }

        internal OpsOrderModel BuildOrder(string symbol, int qty, ZeroPlus.Models.Data.Enums.OrderType orderType)
        {
            Side side;
            double price;

            if (qty < 0)
            {
                side = Side.Sell;
                price = Bid;
            }
            else
            {
                side = Side.Buy;
                price = Ask;
            }

            double pxDiff = 0.0;

            string route = OmsCore.Config.DefaultHedgeRoute(OmsCore.Config.InstanceModeV3);
            string tif = ZeroPlus.Models.Data.Enums.TimeInForce.DAY.ToString();
            if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
            {
                tif = route.StartsWith("D") ?
                    ZeroPlus.Models.Data.Enums.TimeInForce.GTX.ToString() :
                    ZeroPlus.Models.Data.Enums.TimeInForce.ETH.ToString();
            }

            var order = new OpsOrderModel()
            {
                Symbol = symbol,
                Qty = Math.Abs(qty),
                OMSSide = side.ToString(),
                OpenClose = "Auto",
                Price = price,
                Account = OmsCore.Config.DefaultAccount,
                Tif = tif,
                Route = route,
                OMSOrderType = orderType.ToString().ToUpper(),
                Timestamp = DateTime.Now,
                UnderlyingSymbol = symbol,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,
                Tag = new TagCodec(_trader: OmsCore.User.Username,
                                   _edge: pxDiff,
                                   _type: OmsCore.OrderClient.TYPE,
                                   _subtype: "Combo Trader",
                                   _tv: 0,
                                   _ema: Mid,
                                   _bid: Bid,
                                   _ask: Ask,
                                   _comment: _ComboTraderViewModel.InstanceId).Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(_ComboTraderViewModel.InstanceId) ? _ComboTraderViewModel.InstanceId : "",
                    Bid = Bid,
                    Ask = Ask,
                    BidSize = 0,
                    AskSize = 0,
                    Theo = 0,
                    Ema = 0,
                    UnderBid = 0,
                    UnderAsk = 0,
                    UnderBidSize = 0,
                    UnderAskSize = 0,
                    Edge = pxDiff,
                    OrderSubType = SubType ?? ZeroPlus.Models.Data.Enums.OrderSubType.Ticket,
                    ModuleType = ZeroPlus.Models.Data.Enums.ModuleType.None,
                    VolaTheo = 0,
                    VolaTheoAdj = 0,
                    SubType = 0,
                    SharedId = 0,
                    Sequence = 0,
                    SubTypeSequence = 0,
                    ResubmitCount = 0,
                    TotalEstimatedResubmit = 0,
                    ParentSpreadHash = string.Empty,
                }
            };
            order.SetCancelDelay(0.0);
            return order;
        }
    }
}
