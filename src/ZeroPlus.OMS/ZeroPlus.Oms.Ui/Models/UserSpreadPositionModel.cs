using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class UserSpreadPositionModel : BindableBase, IOmsDataSubscriber
    {
        public static readonly ILogger _log = LogManager.GetCurrentClassLogger();


        private int _tempNetQty;
        private int _tempRawNetQty;
        private int _tempFirmPosition;

        private double _midPoint;

        private double _tempDelta;
        private double _tempUnrealPnl;
        private double _tempAvgCost;
        private double _tempNotionalCost;
        private string _tempLastInstance;
        private string _tempAccount;
        private double _tempAdjustedPnl;
        private double _tempBestBuyPrice;
        private double _tempBestSellPrice;

        private DateTime _tempLastTradeTime;


        private string _tempSymbol;
        private string _tempUnderlying;
        private string _tempDescription;

        public bool IsDisposed { get; set; }

        private readonly OmsCore _omsCore;

        public DateTime LastNotified { get; set; }

        private Side _side;
        private List<TicketLegModel> _legs = new();
        readonly PortfolioManagerModel _portfolioManager;

        public UserSpreadPositionModel(OmsCore omsCore, PortfolioManagerModel portfolioManagerModel)
        {
            _omsCore = omsCore;
            _portfolioManager = portfolioManagerModel;
        }

        [Bindable(Default = true)]
        public partial bool ActiveAlert { get; set; }

        [Bindable]
        public partial string Underlying { get; set; }

        [Bindable]
        public partial string Description { get; set; }

        [Bindable]
        public partial string Symbol { get; set; }

        [Bindable]
        public partial int RawNetQty { get; set; }

        [Bindable]
        public partial int NetQty { get; set; }

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
        [Bindable(Default = double.NaN)]
        public partial double NotionalCost { get; set; }
        [Bindable]
        public partial DateTime LastTradeTime { get; set; }
        [Bindable]
        public partial string LastInstance { get; set; }
        [Bindable]
        public partial string Account { get; set; }
        public double Multiplier { get; private set; } = 1;
        public IPosition Position { get; set; }

        internal bool Initialize(IPosition position)
        {
            SymbolLib.SymbolCodec codec = new(position.Symbol);
            _tempDescription = position.Name;
            string underlying = codec.UnderlyingSymbol();
            if (codec.LegCount > 0)
            {
                List<TicketLegModel> legs = new();
                for (int i = 0; i < codec.LegCount; i++)
                {
                    SymbolLib.Instrument instrument = codec.GetLeg(i);

                    string legSymbol = instrument.symbol;
                    var side = instrument.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                    int qty = Math.Abs(instrument.ratio);

                    Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(legSymbol);
                    StrikeInfoModel strikeInfoModel = new(false, option.Strike);
                    ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);

                    TicketLegModel leg = new(_omsCore, underlying, "", null, null)
                    {
                        Symbol = legSymbol,
                        Quantity = qty,
                        Ratio = qty,
                        Side = side,
                        ContraSide = side == Side.Buy ? Side.Sell : Side.Buy,
                        ExpirationInfo = expirationInfoModel,
                        Strike = strikeInfoModel,
                        IsValid = true
                    };

                    leg.ExpirationsList.Add(expirationInfoModel);
                    leg.StrikesList.Add(strikeInfoModel);
                    leg.LegUpdatedEvent += UpdateLcdPosition;
                    leg.SubscribeToPositions();

                    legs.Add(leg);

                    if (instrument.symbol.StartsWith("."))
                    {
                        Multiplier = 100;
                    }
                }

                if (OptionStrategy.TryIdentify(position.Symbol, out string baseType, out string spreadType, out _))
                {
                    _side = OrderTicket.EvaluateSide(baseType, legs);
                }

                _legs = legs;
                _tempSymbol = position.Symbol;
                _tempUnderlying = underlying;
                SubscribeData();
                return true;
            }
            else
            {
                return false;
            }
        }

        private void UpdateLcdPosition()
        {
            try
            {
                if (_legs.Count == 1)
                {
                    _tempFirmPosition = _legs.First().NetQty;
                }
                else if (_legs.Count > 1)
                {
                    if ((_legs.Count(x => (x.NetQty < 0 && x.Side == Side.Buy) || (x.NetQty > 0 && x.Side == Side.Sell)) == _legs.Count) ||
                        (_legs.Count(x => (x.NetQty > 0 && x.Side == Side.Buy) || (x.NetQty < 0 && x.Side == Side.Sell)) == _legs.Count))
                    {
                        int divisor = _legs.Min(x => Math.Abs(x.NetQty));
                        TicketLegModel sample = _legs.First();
                        if (((sample.NetQty < 0 && sample.Side == Side.Buy) || (sample.NetQty > 0 && sample.Side == Side.Sell)) ^ _side == Side.Sell)
                        {
                            _tempFirmPosition = -divisor;
                        }
                        else
                        {
                            _tempFirmPosition = divisor;
                        }
                    }
                    else
                    {
                        _tempFirmPosition = 0;
                    }
                }
                else
                {
                    _tempFirmPosition = 0;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateLcdPosition));
            }
        }

        internal void HandleUpdate(IPosition position)
        {
            Position = position;
            _tempNetQty = position.NetQty;
            _tempRawNetQty = position.RawNetQty;
            _tempBestBuyPrice = position.BestBuyPrice;
            _tempBestSellPrice = position.BestSellPrice;
            _tempAdjustedPnl = position.AdjustedPnl;
            _tempLastTradeTime = position.LastTradeTime;
            _tempAvgCost = -position.OpenPositionAveragePrice;
            _tempNotionalCost = _tempAvgCost * Multiplier;
            _tempLastInstance = position.LastInstance;
            _tempAccount = position.Account;
        }

        internal void Dispose()
        {
            IsDisposed = true;

            foreach (TicketLegModel leg in _legs)
            {
                leg.Dispose();
            }

            if (!string.IsNullOrWhiteSpace(_tempSymbol))
            {
                _omsCore.QuoteClient.Unsubscribe(_tempSymbol, SubscriptionFieldType.MidPoint, this);
                _omsCore.GreekClient.Unsubscribe(_tempSymbol, SubscriptionFieldType.Delta, this);
                _portfolioManager.Unsubscribe(_tempDescription, SubscriptionFieldType.FirmSpreadPosition, this);
                _portfolioManager.Unsubscribe(_tempDescription.ToUpper(), SubscriptionFieldType.UserSpreadPosition, this);
            }
        }

        private void SubscribeData()
        {
            if (!string.IsNullOrWhiteSpace(_tempSymbol))
            {
                _omsCore.QuoteClient.Subscribe(_tempSymbol, SubscriptionFieldType.MidPoint, this);
                _omsCore.GreekClient.Subscribe(_tempSymbol, SubscriptionFieldType.Delta, this);
                _portfolioManager.Subscribe(_tempDescription.ToUpper(), SubscriptionFieldType.UserSpreadPosition, this);
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            switch (key.Type)
            {
                case SubscriptionFieldType.UserSpreadPosition when value is IPosition pos:
                    _tempNetQty = pos.NetQty;
                    _tempRawNetQty = pos.RawNetQty;
                    break;
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

        public void Update()
        {
            if (string.IsNullOrWhiteSpace(Symbol))
            {
                Initialize(Position);
            }
            UpdateLcdPosition();

            if (!string.IsNullOrWhiteSpace(Description) &&
                !string.IsNullOrWhiteSpace(_tempDescription) &&
                Description != _tempDescription)
            {
                _log.Warn("Interesting pos update" +
                          ", Old Description: {}, New Description: {}" +
                          ", Old Symbol: {}, New Symbol: {}" +
                          ", Old Underlying: {}, New Underlying: {}" +
                          ", Old Delta: {}, New Delta: {}" +
                          ", Old UnrealPnl: {}, New UnrealPnl: {}" +
                          ", Old FirmNetQty: {}, New FirmNetQty: {}" +
                          ", Old NetQty: {}, New NetQty: {}" +
                          ", Old RawNetQty: {}, New RawNetQty: {}" +
                          ", Old BestBuyPrice: {}, New BestBuyPrice: {}" +
                          ", Old BestSellPrice: {}, New BestSellPrice: {}" +
                          ", Old AdjustedPnl: {}, New AdjustedPnl: {}" +
                          ", Old LastTradeTime: {}, New LastTradeTime: {}" +
                          ", Old AvgCost: {}, New AvgCost: {}"
                          , Description, _tempDescription
                          , Symbol, _tempSymbol
                          , Underlying, _tempUnderlying
                          , Delta, _tempDelta
                          , UnrealPnl, _tempUnrealPnl
                          , FirmNetQty, _tempFirmPosition
                          , NetQty, _tempNetQty
                          , RawNetQty, _tempRawNetQty
                          , BestBuyPrice, _tempBestBuyPrice
                          , BestSellPrice, _tempBestSellPrice
                          , AdjustedPnl, _tempAdjustedPnl
                          , LastTradeTime, _tempLastTradeTime
                          , AvgCost, _tempAvgCost);
            }

            Description = _tempDescription;
            Symbol = _tempSymbol;
            Underlying = _tempUnderlying;
            Delta = _tempDelta;
            UnrealPnl = _tempUnrealPnl;
            FirmNetQty = _tempFirmPosition;
            NetQty = _tempNetQty;
            RawNetQty = _tempRawNetQty;
            BestBuyPrice = _tempBestBuyPrice;
            BestSellPrice = _tempBestSellPrice;
            AdjustedPnl = _tempAdjustedPnl;
            LastTradeTime = _tempLastTradeTime;
            AvgCost = _tempAvgCost;
            NotionalCost = _tempNotionalCost;
            if (!string.IsNullOrWhiteSpace(_tempLastInstance))
            {
                LastInstance = _tempLastInstance;
            }
            if (!string.IsNullOrWhiteSpace(_tempAccount))
            {
                Account = _tempAccount;
            }
        }
    }
}