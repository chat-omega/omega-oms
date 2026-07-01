using DevExpress.Mvvm;
using System;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class UserSymbolPositionModel : BindableBase, IOmsDataSubscriber, IOmsPositionSubscriber
    {
        private int _netQty;
        private double _midPoint;
        private double _tempDelta;
        private double _tempUnrealPnl;
        private int _tempFirmNetQty;
        private int _tempNetQty;
        private double _tempBestBuyPrice;
        private double _tempBestSellPrice;
        private double _tempAdjustedPnl;
        private DateTime _tempLastTradeTime;
        private double _tempAvgCost;
        private bool _marketDataSubscribed;

        public bool IsDisposed { get; set; }

        private OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable(Default = true)]
        public partial bool ActiveAlert { get; set; }

        [Bindable]
        public partial string Underlying { get; set; }

        [Bindable]
        public partial string Symbol { get; set; }

        public int NetQty
        {
            get => _netQty;
            set
            {
                SetValue(ref _netQty, value);
                if (value == 0)
                {
                    UnsubscribeMarketData();
                }
                else
                {
                    SubscribeMarketData();
                }
            }
        }

        [Bindable]
        public partial int FirmNetQty { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double BestBuyPrice { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double BestSellPrice { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double AdjustedPnl { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double UnrealPnl { get; set; }

        [Bindable]
        public partial double Delta { get; set; }

        [Bindable(Default = double.NaN)]
        public partial double AvgCost { get; set; }

        [Bindable]
        public partial DateTime LastTradeTime { get; set; }
        public double Multiplier { get; private set; } = 1;

        internal void Initialize()
        {
            if (string.IsNullOrWhiteSpace(Underlying))
            {
                SymbolLib.SymbolCodec codec = new(Symbol);
                if (codec.LegCount > 0)
                {
                    if (Symbol.StartsWith("."))
                    {
                        Multiplier = 100;
                    }
                    Underlying = codec.UnderlyingSymbol();
                    SubscribeData();
                }
            }
        }

        internal void HandleUpdate(IPosition position)
        {
            if (position.Name.Contains(' '))
            {
                return;
            }
            _tempNetQty = position.NetQty;
            _tempBestBuyPrice = position.BestBuyPrice;
            _tempBestSellPrice = position.BestSellPrice;
            _tempAdjustedPnl = position.AdjustedPnl;
            _tempLastTradeTime = position.LastTradeTime;
            _tempAvgCost = position.OpenPositionAveragePrice;
        }

        internal void Dispose()
        {
            IsDisposed = true;
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Delta, this);
                OmsCore.OrderClient.UnsubscribePosition(Symbol, OmsCore.Config.DefaultAccount, this);
            }
        }

        private void SubscribeData()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                OmsCore.OrderClient.SubscribePosition(Symbol, OmsCore.Config.DefaultAccount, this);
            }
        }

        private void SubscribeMarketData()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                if (!_marketDataSubscribed)
                {
                    OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.MidPoint, this);
                    OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Delta, this);
                    _marketDataSubscribed = true;
                }
            }
        }

        private void UnsubscribeMarketData()
        {
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                if (_marketDataSubscribed)
                {
                    OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.MidPoint, this);
                    OmsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Delta, this);
                    _marketDataSubscribed = false;
                }
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            switch (key.Type)
            {
                case SubscriptionFieldType.Delta when value is double delta:
                    _tempDelta = delta * NetQty;
                    break;
                case SubscriptionFieldType.MidPoint when value is double midPoint:
                    _midPoint = midPoint;
                    if (NetQty > 0)
                    {
                        _tempUnrealPnl = (Math.Abs(_midPoint) - Math.Abs(AvgCost)) * NetQty * Multiplier;
                    }
                    else if (NetQty < 0)
                    {
                        _tempUnrealPnl = (Math.Abs(AvgCost) - Math.Abs(_midPoint)) * -NetQty * Multiplier;
                    }
                    else
                    {
                        _tempUnrealPnl = double.NaN;
                    }
                    break;
            }
        }

        public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
        {
            if (value is OMSSendPosition position)
            {
                _tempFirmNetQty = position.NetQty;
            }
        }

        public void Update()
        {
            Initialize();

            Delta = _tempDelta;
            UnrealPnl = _tempUnrealPnl;
            FirmNetQty = _tempFirmNetQty;

            NetQty = _tempNetQty;
            BestBuyPrice = _tempBestBuyPrice;
            BestSellPrice = _tempBestSellPrice;
            AdjustedPnl = _tempAdjustedPnl;
            LastTradeTime = _tempLastTradeTime;
            AvgCost = _tempAvgCost;
        }
    }
}