using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public class PositionModel : BindableBase, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly string[] _greekFieldNames = new string[]
        {
            nameof(Delta),
            nameof(Gamma),
            nameof(Vega),
            nameof(Theta),
            nameof(Rho),
            nameof(Implied),
            nameof(Theo)
        };
        private readonly ConcurrentDictionary<string, bool> _fieldUpdateMap;
        private readonly List<string> _fieldUpdateKeys;
        private GreekUpdate _greekUpdate;
        private bool _subscribed;

        public int Multiplier { get; private set; }
        private OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool IsDisposed { get; set; }

        private string _Symbol;
        public string Symbol
        {
            get => _Symbol;
            set
            {
                if (_Symbol != value)
                {
                    _Symbol = value;
                    _fieldUpdateMap[nameof(Symbol)] = true;
                }
            }
        }

        private double _UnrealizedPL;
        public double UnrealizedPL
        {
            get => _UnrealizedPL;
            set
            {
                if (_UnrealizedPL != value)
                {
                    _UnrealizedPL = value;
                    _fieldUpdateMap[nameof(UnrealizedPL)] = true;
                }
            }
        }

        private double _TradingPL;
        public double TradingPL
        {
            get => _TradingPL;
            set
            {
                if (_TradingPL != value)
                {
                    _TradingPL = value;
                    _fieldUpdateMap[nameof(TradingPL)] = true;
                }
            }
        }

        private int _TradingNetQty;
        public int TradingNetQty
        {
            get => _TradingNetQty;
            set
            {
                if (_TradingNetQty != value)
                {
                    _TradingNetQty = value;
                    _fieldUpdateMap[nameof(TradingNetQty)] = true;
                }
            }
        }

        private double _TradingAveCost;
        public double TradingAveCost
        {
            get => _TradingAveCost;
            set
            {
                if (_TradingAveCost != value)
                {
                    _TradingAveCost = value;
                    _fieldUpdateMap[nameof(TradingAveCost)] = true;
                }
            }
        }

        private int _NetQty;
        public int NetQty
        {
            get => _NetQty;
            set
            {
                if (_NetQty != value)
                {
                    _NetQty = value;
                    _fieldUpdateMap[nameof(NetQty)] = true;
                }
            }
        }

        private double _NetPL;
        public double NetPL
        {
            get => _NetPL;
            set
            {
                if (_NetPL != value)
                {
                    _NetPL = value;
                    _fieldUpdateMap[nameof(NetPL)] = true;
                }
            }
        }

        private double _MarketValue;
        public double MarketValue
        {
            get => _MarketValue;
            set
            {
                if (_MarketValue != value)
                {
                    _MarketValue = value;
                    _fieldUpdateMap[nameof(MarketValue)] = true;
                }
            }
        }

        private double _NotionalValue;
        public double NotionalValue
        {
            get => _NotionalValue;
            set
            {
                if (_NotionalValue != value)
                {
                    _NotionalValue = value;
                    _fieldUpdateMap[nameof(NotionalValue)] = true;
                }
            }
        }

        private int _OpeningQty;
        public int OpeningQty
        {
            get => _OpeningQty;
            set
            {
                if (_OpeningQty != value)
                {
                    _OpeningQty = value;
                    _fieldUpdateMap[nameof(OpeningQty)] = true;
                }
            }
        }

        private double _DayPL;
        public double DayPL
        {
            get => _DayPL;
            set
            {
                if (_DayPL != value)
                {
                    _DayPL = value;
                    _fieldUpdateMap[nameof(DayPL)] = true;
                }
            }
        }

        private int _TradingSellQty;
        public int TradingSellQty
        {
            get => _TradingSellQty;
            set
            {
                if (_TradingSellQty != value)
                {
                    _TradingSellQty = value;
                    _fieldUpdateMap[nameof(TradingSellQty)] = true;
                }
            }
        }

        private double _TradingSellAvePrice;
        public double TradingSellAvePrice
        {
            get => _TradingSellAvePrice;
            set
            {
                if (_TradingSellAvePrice != value)
                {
                    _TradingSellAvePrice = value;
                    _fieldUpdateMap[nameof(TradingSellAvePrice)] = true;
                }
            }
        }

        private int _TradingBuyQty;
        public int TradingBuyQty
        {
            get => _TradingBuyQty;
            set
            {
                if (_TradingBuyQty != value)
                {
                    _TradingBuyQty = value;
                    _fieldUpdateMap[nameof(TradingBuyQty)] = true;
                }
            }
        }

        private double _RealizedPL;
        public double RealizedPL
        {
            get => _RealizedPL;
            set
            {
                if (_RealizedPL != value)
                {
                    _RealizedPL = value;
                    _fieldUpdateMap[nameof(RealizedPL)] = true;
                }
            }
        }

        private double _OpeningCost;
        public double OpeningCost
        {
            get => _OpeningCost;
            set
            {
                if (_OpeningCost != value)
                {
                    _OpeningCost = value;
                    _fieldUpdateMap[nameof(OpeningCost)] = true;
                }
            }
        }

        private double _MarkedCost;
        public double MarkedCost
        {
            get => _MarkedCost;
            set
            {
                if (_MarkedCost != value)
                {
                    _MarkedCost = value;
                    _fieldUpdateMap[nameof(MarkedCost)] = true;
                }
            }
        }

        private double _AveCost;
        public double AveCost
        {
            get => _AveCost;
            set
            {
                if (_AveCost != value)
                {
                    _AveCost = value;
                    _fieldUpdateMap[nameof(AveCost)] = true;
                }
            }
        }

        private double _TradingBuyAvePrice;
        public double TradingBuyAvePrice
        {
            get => _TradingBuyAvePrice;
            set
            {
                if (_TradingBuyAvePrice != value)
                {
                    _TradingBuyAvePrice = value;
                    _fieldUpdateMap[nameof(TradingBuyAvePrice)] = true;
                }
            }
        }

        private string _Account;
        public string Account
        {
            get => _Account;
            set
            {
                if (_Account != value)
                {
                    _Account = value;
                    _fieldUpdateMap[nameof(Account)] = true;
                }
            }
        }

        private int _AccountID;
        public int AccountID
        {
            get => _AccountID;
            set
            {
                if (_AccountID != value)
                {
                    _AccountID = value;
                    _fieldUpdateMap[nameof(AccountID)] = true;
                }
            }
        }

        private int _ID;
        public int ID
        {
            get => _ID;
            set
            {
                if (_ID != value)
                {
                    _ID = value;
                    _fieldUpdateMap[nameof(ID)] = true;
                }
            }
        }

        private string _Expiration;
        public string Expiration
        {
            get => _Expiration;
            set
            {
                if (_Expiration != value)
                {
                    _Expiration = value;
                    _fieldUpdateMap[nameof(Expiration)] = true;
                }
            }
        }

        private double _Strike;
        public double Strike
        {
            get => _Strike;
            set
            {
                if (_Strike != value)
                {
                    _Strike = value;
                    _fieldUpdateMap[nameof(Strike)] = true;
                }
            }
        }

        private string _Type;
        public string Type
        {
            get => _Type;
            set
            {
                if (_Type != value)
                {
                    _Type = value;
                    _fieldUpdateMap[nameof(Type)] = true;
                }
            }
        }

        private string _Underlying;
        public string Underlying
        {
            get => _Underlying;
            set
            {
                if (_Underlying != value)
                {
                    _Underlying = value;
                    _fieldUpdateMap[nameof(Underlying)] = true;
                }
            }
        }

        private double _bid = double.NaN;
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

        private double _mid = double.NaN;
        public double Mid
        {
            get => _mid;
            set
            {
                if (_mid != value)
                {
                    _mid = value;
                    _fieldUpdateMap[nameof(Mid)] = true;
                }
            }
        }

        private double _ask = double.NaN;
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

        private double _Delta;
        public double Delta
        {
            get => _Delta;
            set
            {
                if (_Delta != value)
                {
                    _Delta = value;
                    _fieldUpdateMap[nameof(Delta)] = true;
                }
            }
        }

        private double _Gamma;
        public double Gamma
        {
            get => _Gamma;
            set
            {
                if (_Gamma != value)
                {
                    _Gamma = value;
                    _fieldUpdateMap[nameof(Gamma)] = true;
                }
            }
        }

        private double _Vega;
        public double Vega
        {
            get => _Vega;
            set
            {
                if (_Vega != value)
                {
                    _Vega = value;
                    _fieldUpdateMap[nameof(Vega)] = true;
                }
            }
        }

        private double _Theta;
        public double Theta
        {
            get => _Theta;
            set
            {
                if (_Theta != value)
                {
                    _Theta = value;
                    _fieldUpdateMap[nameof(Theta)] = true;
                }
            }
        }

        private double _Rho;
        public double Rho
        {
            get => _Rho;
            set
            {
                if (_Rho != value)
                {
                    _Rho = value;
                    _fieldUpdateMap[nameof(Rho)] = true;
                }
            }
        }

        private double _Implied;
        public double Implied
        {
            get => _Implied;
            set
            {
                if (_Implied != value)
                {
                    _Implied = value;
                    _fieldUpdateMap[nameof(Implied)] = true;
                }
            }
        }

        private double _Theo;
        public double Theo
        {
            get => _Theo;
            set
            {
                if (_Theo != value)
                {
                    _Theo = value;
                    _fieldUpdateMap[nameof(Theo)] = true;
                }
            }
        }

        private double _PrevClose;

        public double PrevClose
        {
            get => _PrevClose;
            set
            {
                if (_PrevClose != value)
                {
                    _PrevClose = value;
                    _fieldUpdateMap[nameof(PrevClose)] = true;
                }
            }
        }

        public PositionModel()
        {
            _fieldUpdateMap = new ConcurrentDictionary<string, bool>()
            {
                [nameof(Symbol)] = true,
                [nameof(UnrealizedPL)] = true,
                [nameof(TradingPL)] = true,
                [nameof(TradingNetQty)] = true,
                [nameof(TradingAveCost)] = true,
                [nameof(NetQty)] = true,
                [nameof(NetPL)] = true,
                [nameof(MarketValue)] = true,
                [nameof(NotionalValue)] = true,
                [nameof(OpeningQty)] = true,
                [nameof(DayPL)] = true,
                [nameof(TradingSellQty)] = true,
                [nameof(TradingSellAvePrice)] = true,
                [nameof(TradingBuyQty)] = true,
                [nameof(RealizedPL)] = true,
                [nameof(OpeningCost)] = true,
                [nameof(MarkedCost)] = true,
                [nameof(AveCost)] = true,
                [nameof(TradingBuyAvePrice)] = true,
                [nameof(Account)] = true,
                [nameof(AccountID)] = true,
                [nameof(ID)] = true,
                [nameof(Expiration)] = true,
                [nameof(Strike)] = true,
                [nameof(Type)] = true,
                [nameof(Underlying)] = true,
                [nameof(Delta)] = true,
                [nameof(Gamma)] = true,
                [nameof(Vega)] = true,
                [nameof(Theta)] = true,
                [nameof(Rho)] = true,
                [nameof(Implied)] = true,
                [nameof(Theo)] = true,
                [nameof(PrevClose)] = true,
                [nameof(Bid)] = true,
                [nameof(Mid)] = true,
                [nameof(Ask)] = true,
            };
            _fieldUpdateKeys = _fieldUpdateMap.Keys.ToList();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (value is GreekUpdate greekUpdate)
                {
                    _greekUpdate = greekUpdate;
                    UpdateGreeks();
                }
                else
                {
                    switch (key.Type)
                    {
                        case SubscriptionFieldType.Bid when value is double bid:
                            Bid = bid;
                            Mid = Math.Round((Bid + Ask) / 2, 2);
                            break;
                        case SubscriptionFieldType.Ask when value is double ask:
                            Ask = ask;
                            Mid = Math.Round((Bid + Ask) / 2, 2);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        public void Update(OmsPosition positionMessage, bool raisedPropertyChanged)
        {

            if (UnrealizedPL != positionMessage.UnrealizedPL)
            {
                UnrealizedPL = positionMessage.UnrealizedPL;
            }

            if (TradingPL != positionMessage.TradingPL)
            {
                TradingPL = positionMessage.TradingPL;
            }

            if (TradingNetQty != positionMessage.TradingNetQty)
            {
                TradingNetQty = positionMessage.TradingNetQty;
            }

            if (TradingAveCost != positionMessage.TradingAveCost)
            {
                TradingAveCost = positionMessage.TradingAveCost;
            }

            if (NotionalValue != positionMessage.NotionalValue)
            {
                NotionalValue = positionMessage.NotionalValue;
            }

            if (NetQty != positionMessage.NetQty)
            {
                NetQty = positionMessage.NetQty;
                UpdateGreeks();
            }

            if (NetPL != positionMessage.NetPL)
            {
                NetPL = positionMessage.NetPL;
            }

            if (MarketValue != positionMessage.MarketValue)
            {
                MarketValue = positionMessage.MarketValue;
            }

            if (DayPL != positionMessage.DayPL)
            {
                DayPL = positionMessage.DayPL;
            }

            if (TradingSellQty != positionMessage.TradingSellQty)
            {
                TradingSellQty = positionMessage.TradingSellQty;
            }

            if (TradingSellAvePrice != positionMessage.TradingSellAvePrice)
            {
                TradingSellAvePrice = positionMessage.TradingSellAvePrice;
            }

            if (TradingBuyQty != positionMessage.TradingBuyQty)
            {
                TradingBuyQty = positionMessage.TradingBuyQty;
            }

            if (Symbol != positionMessage.Symbol)
            {
                Symbol = positionMessage.Symbol;
                SubscribeGreeks();
                ParseSymbol();
            }

            if (RealizedPL != positionMessage.RealizedPL)
            {
                RealizedPL = positionMessage.RealizedPL;
            }

            if (OpeningQty != positionMessage.OpeningQty)
            {
                OpeningQty = positionMessage.OpeningQty;
            }

            if (OpeningCost != positionMessage.OpeningCost)
            {
                OpeningCost = positionMessage.OpeningCost;
            }

            if (MarkedCost != positionMessage.MarkedCost)
            {
                MarkedCost = positionMessage.MarkedCost;
            }

            if (ID != positionMessage.ID)
            {
                ID = positionMessage.ID;
            }

            if (AveCost != positionMessage.AveCost)
            {
                AveCost = positionMessage.AveCost;
            }

            if (AccountID != positionMessage.AccountID)
            {
                AccountID = positionMessage.AccountID;
            }

            if (Account != positionMessage.AccountAcronym)
            {
                Account = positionMessage.AccountAcronym;
            }

            if (TradingBuyAvePrice != positionMessage.TradingBuyAvePrice)
            {
                TradingBuyAvePrice = positionMessage.TradingBuyAvePrice;
            }
        }

        internal void Dispose()
        {
            try
            {
                IsDisposed = true;
                UnsubscribeGreeks();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        private void UpdateGreeks()
        {
            if (_greekUpdate != null)
            {
                Delta = _greekUpdate.Delta * NetQty * Multiplier;
                Gamma = _greekUpdate.Gamma * NetQty * Multiplier;
                Vega = _greekUpdate.Vega * NetQty * Multiplier;
                Theta = _greekUpdate.Theta * NetQty * Multiplier;
                Rho = _greekUpdate.Rho * NetQty * Multiplier;
                Implied = _greekUpdate.Implied * NetQty * Multiplier;
                Theo = _greekUpdate.Theo * NetQty * Multiplier;
            }
        }

        private void SubscribeGreeks()
        {
            OmsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Greeks, this);
        }

        private void UnsubscribeGreeks()
        {
            OmsCore.QuoteClient.UnsubscribeAll(this);
            OmsCore.GreekClient.UnsubscribeAllAsync(this);
        }

        private void ParseSymbol()
        {
            try
            {
                if (Symbol.StartsWith("."))
                {
                    Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(Symbol);
                    Expiration = option.Expiration.ToString("yyyy-MM-dd");
                    Type = option.Type.ToString();
                    Strike = option.Strike;
                    Underlying = option.UnderlyingSymbol;
                    Multiplier = 100;
                }
                else
                {
                    Type = "STOCK";
                    Expiration = "";
                    Strike = 0;
                    Delta = 1;
                    Underlying = Symbol;
                    Multiplier = 1;
                    _greekUpdate = new GreekUpdate()
                    {
                        Delta = 1,
                        Vega = double.NaN,
                        Theo = double.NaN,
                        Gamma = double.NaN,
                        Theta = double.NaN,
                        Implied = double.NaN,
                        Rho = double.NaN,
                    };
                    UpdateGreeks();
                }
            }
            catch (Exception)
            {
                Expiration = "";
                Strike = 0;
                Type = "";
                Underlying = "";
            }
        }

        internal void UpdateUiProperties()
        {
            try
            {
                foreach (string key in _fieldUpdateKeys)
                {
                    if (_fieldUpdateMap[key])
                    {
                        _fieldUpdateMap[key] = false;
                        RaisePropertyChanged(key);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateUiProperties));
            }
        }

        public void Subscribe()
        {
            if (!_subscribed)
            {
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
                _subscribed = true;
            }
        }

        public void Unsubscribe()
        {
            if (_subscribed)
            {
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
                _subscribed = false;
                Bid = double.NaN;
                Mid = double.NaN;
                Ask = double.NaN;
            }
        }
    }
}
