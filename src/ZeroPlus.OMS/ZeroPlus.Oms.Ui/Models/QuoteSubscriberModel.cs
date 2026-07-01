using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class QuoteSubscriberModel : BindableBase, IOmsDataSubscriber
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, bool> _fieldUpdateMap;
        private readonly List<string> _fieldUpdateKeys;
        private string _underlying;
        private double _last;
        private double _bid;
        private double _ask;
        private double _volume;
        private double _open;
        private double _high;
        private double _low;
        private double _earnings;
        private double _exDividend;
        private double _annualDividend;
        private double _askSize;
        private double _avgVol;
        private double _beta;
        private double _bidSize;
        private string _description;
        private double _divAmount;
        private double _eps;
        private string _industry;
        private double _lastSize;
        private double _mark;
        private double _marketCap;
        private double _pE;
        private double _prevClose;
        private double _yield;
        private string _sector;

        protected OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial double NetChange { get; set; }

        [Bindable]
        public partial double PercentChange { get; set; }

        public string Underlying
        {
            get => _underlying;
            set
            {
                if (_underlying != value)
                {
                    _underlying = value;
                    _fieldUpdateMap[nameof(Underlying)] = true;
                }
            }
        }
        public double Last
        {
            get => _last;
            set
            {
                if (_last != value)
                {
                    _last = value;
                    _fieldUpdateMap[nameof(Last)] = true;
                }
            }
        }
        public double Bid
        {
            get => _bid;
            set
            {
                if (_bid != value)
                {
                    _bid = value;
                    _fieldUpdateMap[nameof(Bid)] = true;
                }
            }
        }
        public double Ask
        {
            get => _ask;
            set
            {
                if (_ask != value)
                {
                    _ask = value;
                    _fieldUpdateMap[nameof(Ask)] = true;
                }
            }
        }
        public double Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    _fieldUpdateMap[nameof(Volume)] = true;
                }
            }
        }
        public double Open
        {
            get => _open;
            set
            {
                if (_open != value)
                {
                    _open = value;
                    _fieldUpdateMap[nameof(Open)] = true;
                }
            }
        }
        public double High
        {
            get => _high;
            set
            {
                if (_high != value)
                {
                    _high = value;
                    _fieldUpdateMap[nameof(High)] = true;
                }
            }
        }
        public double Low
        {
            get => _low;
            set
            {
                if (_low != value)
                {
                    _low = value;
                    _fieldUpdateMap[nameof(Low)] = true;
                }
            }
        }
        public double Earnings
        {
            get => _earnings;
            set
            {
                if (_earnings != value)
                {
                    _earnings = value;
                    _fieldUpdateMap[nameof(Earnings)] = true;
                }
            }
        }
        public double ExDividend
        {
            get => _exDividend;
            set
            {
                if (_exDividend != value)
                {
                    _exDividend = value;
                    _fieldUpdateMap[nameof(ExDividend)] = true;
                }
            }
        }
        public double AnnualDividend
        {
            get => _annualDividend;
            set
            {
                if (_annualDividend != value)
                {
                    _annualDividend = value;
                    _fieldUpdateMap[nameof(AnnualDividend)] = true;
                }
            }
        }
        public double AskSize
        {
            get => _askSize;
            set
            {
                if (_askSize != value)
                {
                    _askSize = value;
                    _fieldUpdateMap[nameof(AskSize)] = true;
                }
            }
        }
        public double AvgVol
        {
            get => _avgVol;
            set
            {
                if (_avgVol != value)
                {
                    _avgVol = value;
                    _fieldUpdateMap[nameof(AvgVol)] = true;
                }
            }
        }
        public double Beta
        {
            get => _beta;
            set
            {
                if (_beta != value)
                {
                    _beta = value;
                    _fieldUpdateMap[nameof(Beta)] = true;
                }
            }
        }
        public double BidSize
        {
            get => _bidSize;
            set
            {
                if (_bidSize != value)
                {
                    _bidSize = value;
                    _fieldUpdateMap[nameof(BidSize)] = true;
                }
            }
        }
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    _fieldUpdateMap[nameof(Description)] = true;
                }
            }
        }
        public double DivAmount
        {
            get => _divAmount;
            set
            {
                if (_divAmount != value)
                {
                    _divAmount = value;
                    _fieldUpdateMap[nameof(DivAmount)] = true;
                }
            }
        }
        public double Eps
        {
            get => _eps;
            set
            {
                if (_eps != value)
                {
                    _eps = value;
                    _fieldUpdateMap[nameof(Eps)] = true;
                }
            }
        }
        public string Industry
        {
            get => _industry;
            set
            {
                if (_industry != value)
                {
                    _industry = value;
                    _fieldUpdateMap[nameof(Industry)] = true;
                }
            }
        }
        public double LastSize
        {
            get => _lastSize;
            set
            {
                if (_lastSize != value)
                {
                    _lastSize = value;
                    _fieldUpdateMap[nameof(LastSize)] = true;
                }
            }
        }
        public double Mark
        {
            get => _mark;
            set
            {
                if (_mark != value)
                {
                    _mark = value;
                    _fieldUpdateMap[nameof(Mark)] = true;
                }
            }
        }
        public double MarketCap
        {
            get => _marketCap;
            set
            {
                if (_marketCap != value)
                {
                    _marketCap = value;
                    _fieldUpdateMap[nameof(MarketCap)] = true;
                }
            }
        }
        public double PE
        {
            get => _pE;
            set
            {
                if (_pE != value)
                {
                    _pE = value;
                    _fieldUpdateMap[nameof(PE)] = true;
                }
            }
        }
        public double PrevClose
        {
            get => _prevClose;
            set
            {
                if (_prevClose != value)
                {
                    _prevClose = value;
                    _fieldUpdateMap[nameof(PrevClose)] = true;
                }
            }
        }
        public double Yield
        {
            get => _yield;
            set
            {
                if (_yield != value)
                {
                    _yield = value;
                    _fieldUpdateMap[nameof(Yield)] = true;
                }
            }
        }
        public string Sector
        {
            get => _sector;
            set
            {
                if (_sector != value)
                {
                    _sector = value;
                    _fieldUpdateMap[nameof(Sector)] = true;
                }
            }
        }
        public bool IsDisposed { get; set; }

        public QuoteSubscriberModel()
        {
            _fieldUpdateMap = new Dictionary<string, bool>()
            {
                {nameof(Underlying), true},
                {nameof(Last), true},
                {nameof(Bid), true},
                {nameof(Ask), true},
                {nameof(Volume), true},
                {nameof(Open), true},
                {nameof(High), true},
                {nameof(Low), true},
                {nameof(Earnings), true},
                {nameof(ExDividend), true},
                {nameof(AnnualDividend), true},
                {nameof(AskSize), true},
                {nameof(AvgVol), true},
                {nameof(Beta), true},
                {nameof(BidSize), true},
                {nameof(Description), true},
                {nameof(DivAmount), true},
                {nameof(Eps), true},
                {nameof(Industry), true},
                {nameof(LastSize), true},
                {nameof(Mark), true},
                {nameof(MarketCap), true},
                {nameof(PE), true},
                {nameof(PrevClose), true},
                {nameof(Yield), true},
                {nameof(Sector), true},
                {nameof(IsDisposed), true },
            };
            _fieldUpdateKeys = _fieldUpdateMap.Keys.ToList();

            Clear();
        }

        public void SetUnderlying(string underlying)
        {
            Clear();
            Underlying = underlying;
            SubscribeDataAsync();
        }

        internal void UpdateChanges()
        {
            try
            {
                foreach (string key in _fieldUpdateKeys)
                {
                    if (_fieldUpdateMap[key])
                    {
                        RaisePropertyChanged(key);
                        _fieldUpdateMap[key] = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateChanges));
            }
        }

        public void Dispose()
        {
            try
            {
                IsDisposed = true;
                OmsCore.QuoteClient.UnsubscribeAll(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        private async void SubscribeDataAsync()
        {
            string symbol = Underlying;
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.LastSize, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.NetChange, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Open, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Volume, this);
                PrevClose = await OmsCore.QuoteClient.GetSnapshotAsync(symbol, SubscriptionFieldType.PreviousClose);
            }
        }

        private void UnsubscribeDataAsync(string symbol, SubscriptionFieldType type)
        {
            _ = OmsCore.QuoteClient.UnsubscribeAsync(symbol, type, this);
        }

        public void Clear()
        {
            string symbol = Underlying;
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.LastSize, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.NetChange, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Open, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Volume, this);
            }
            Last = NetChange = PercentChange = Bid = Ask = Volume = Open = High = Low = Earnings = ExDividend = AnnualDividend = AskSize = AvgVol = Beta = BidSize = DivAmount = Eps = LastSize = Mark = MarketCap = PE = PrevClose = Yield = double.NaN;
            Underlying = Description = Sector = Industry = "";
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (IsDisposed)
            {
                return;
            }
            if (key.Symbol != Underlying)
            {
                UnsubscribeDataAsync(key.Symbol, key.Type);
                return;
            }
            else
            {
                try
                {
                    SubscriptionFieldType quoteType = key.Type;
                    switch (quoteType)
                    {
                        case SubscriptionFieldType.Ask:
                            if (value is double ask && ask != Ask)
                            {
                                Ask = ask;
                            }
                            break;
                        case SubscriptionFieldType.AskSize:
                            if (value is double asksize && asksize != AskSize)
                            {
                                AskSize = asksize;
                            }
                            break;
                        case SubscriptionFieldType.Bid:
                            if (value is double bid && bid != Bid)
                            {
                                Bid = bid;
                            }
                            break;

                        case SubscriptionFieldType.BidSize:
                            if (value is double bidsize && bidsize != BidSize)
                            {
                                BidSize = bidsize;
                            }
                            break;
                        case SubscriptionFieldType.High:
                            if (value is double high && high != High)
                            {
                                High = high;
                            }
                            break;
                        case SubscriptionFieldType.LastPrice:
                            if (value is double lastprice && lastprice != Last)
                            {
                                Last = lastprice;

                                SetNetAndPercentChange(lastprice);
                            }
                            break;
                        case SubscriptionFieldType.LastSize:
                            if (value is double lastsize && lastsize != LastSize)
                            {
                                LastSize = lastsize;
                            }
                            break;
                        case SubscriptionFieldType.Low:
                            if (value is double low && low != Low)
                            {
                                Low = low;
                            }
                            break;
                        case SubscriptionFieldType.MidPoint:
                            if (value is double midpoint && midpoint != Mark)
                            {
                                Mark = midpoint;
                            }
                            break;
                        case SubscriptionFieldType.NetChange:
                            if (value is double netchange && netchange != NetChange)
                            {
                                NetChange = netchange;
                            }
                            break;
                        case SubscriptionFieldType.Open:
                            if (value is double open && open != Open)
                            {
                                Open = open;
                            }
                            break;
                        case SubscriptionFieldType.Volume:
                            if (value is double volume && volume != Volume)
                            {
                                Volume = volume;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(SubscribedDataUpdateValue));
                }
            }
        }

        private void SetNetAndPercentChange(double last)
        {
            if (!double.IsNaN(PrevClose) && !double.IsNaN(last) && PrevClose != 0.0)
            {
                double netChange = last - PrevClose;
                NetChange = netChange;
                PercentChange = netChange / PrevClose * 100;
            }
        }
    }
}
