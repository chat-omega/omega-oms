using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PairLegModel : BindableBase, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentStack<TradeUnit> _sells = new();
        private readonly ConcurrentStack<TradeUnit> _buys = new();

        private bool _pausedByPartialFill;
        private readonly PairTraderViewModel _PairTraderViewModel;
        protected OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        private readonly object _lock = new();


        [Bindable(Default = "")]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial Side Side { get; set; }
        [Bindable(Default = true)]
        public partial bool CanEdit { get; set; }
        [Bindable(Default = 1)]
        public partial int Quantity { get; set; }
        [Bindable]
        public partial int TotalQty { get; set; }
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

        [Bindable(Default = double.NaN)]
        public partial double Bid { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double Ask { get; set; }

        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public double Mid { get; set; } = double.NaN;
        public bool IsDisposed { get; set; }
        public string ModuleType { get; set; }
        public double Multiplier => Symbol.StartsWith(".") ? 100 : 1;

        public PairLegModel(PairTraderViewModel PairTraderViewModel)
        {
            _PairTraderViewModel = PairTraderViewModel;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            if (symbol != Symbol)
            {
                return;
            }
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
                            _PairTraderViewModel?.Update();
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
                            _PairTraderViewModel?.Update();
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
                            _PairTraderViewModel?.Update();
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
            }
        }

        public void Init()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                switch (_PairTraderViewModel.DataType)
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

        internal void Update(OrderUpdateValues update, bool reverse = false)
        {
            try
            {
                OrderStatus status = update.OrderStatus;

                Side side = !reverse ? Side : Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;

                if (status is OrderStatus.PartiallyFilled or
                    OrderStatus.Filled)
                {
                    double avgPx = update.AveragePrice;
                    int qty = update.LastQuantity;
                    int hedgeFillQty = (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort) ? -Math.Abs(qty) : Math.Abs(qty);
                    if (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
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

                    TradeUnit singleTrade = new()
                    {
                        Quantity = 1,
                        Price = avgPx,
                        TotalPrice = avgPx,
                        NetPrice = avgPx,
                    };
                    for (int i = 0; i < qty; i++)
                    {
                        if (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
                        {
                            _sells.Push(singleTrade);
                        }
                        else
                        {
                            _buys.Push(singleTrade);
                        }
                    }
                    TotalQty += qty;

                    UpdateNetPnl();
                }
                else if (status is OrderStatus.Canceled or
                         OrderStatus.Rejected)
                {
                    int leaves = update.LeavesQuantity;
                    leaves = (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort) ? -Math.Abs(leaves) : Math.Abs(leaves);
                    lock (_lock)
                    {
                        WorkingQty -= leaves;
                    }
                }

                if (status == OrderStatus.Filled)
                {
                    if (_pausedByPartialFill && !_PairTraderViewModel.OrderEnabled)
                    {
                        _pausedByPartialFill = false;
                        _PairTraderViewModel.OrderEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        public void HandleExecutionReport(OrderInfoUpdate update)
        {
            if (update.Type != "UserSubmitOrder")
            {
                return;
            }
            OrderStatus status = update.OrderStatus;
            Side side = update.Side;
            if (status is OrderStatus.PartiallyFilled or
                OrderStatus.Filled)
            {
                if (double.TryParse(update.AvgPrice, out double avgPx))
                {
                    int hedgeFillQty = (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort) ? -Math.Abs(update.VolumeTraded) : Math.Abs(update.VolumeTraded);
                    int qty = update.VolumeTraded;
                    if (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
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

                    TradeUnit singleTrade = new()
                    {
                        Quantity = 1,
                        Price = avgPx,
                        TotalPrice = avgPx,
                        NetPrice = avgPx,
                    };
                    for (int i = 0; i < update.VolumeTraded; i++)
                    {
                        if (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
                        {
                            _sells.Push(singleTrade);
                        }
                        else
                        {
                            _buys.Push(singleTrade);
                        }
                    }
                    TotalQty += qty;

                    UpdateNetPnl();
                }
            }
            else if (status is OrderStatus.Canceled or
                     OrderStatus.Rejected)
            {
                int qty = update.RemainingVolume;
                qty = (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort) ? -Math.Abs(qty) : Math.Abs(qty);
                lock (_lock)
                {
                    WorkingQty -= qty;
                }
            }

            if (status == OrderStatus.Filled)
            {
                if (_pausedByPartialFill && !_PairTraderViewModel.OrderEnabled)
                {
                    _pausedByPartialFill = false;
                    _PairTraderViewModel.OrderEnabled = true;
                }
            }
        }

        private void UpdateNetPnl()
        {
            UpdateRealPnl();

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

        private void UpdateRealPnl()
        {
            try
            {
                while (!_buys.IsEmpty && !_sells.IsEmpty)
                {
                    if (_sells.TryPeek(out TradeUnit sell))
                    {
                        if (_buys.TryPeek(out TradeUnit buy))
                        {
                            double netPnl = sell.NetPrice - buy.NetPrice;
                            RealPnl += netPnl;
                            _sells.TryPop(out _);
                            _buys.TryPop(out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateRealPnl));
            }
        }

        internal void CancelResume()
        {
            _pausedByPartialFill = false;
        }
    }
}
