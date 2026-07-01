using NLog;
using SymbolLib;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms.Indicators
{
    public class IvCalculator : SubscriptionProvider, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly IEmaConfig _emaConfig;
        private readonly EmaCalculator _emaCalculator;
        private readonly PricingParameters _pricingParameters;
        private readonly Greeks _greeks;

        private double _optionPxUpdate = double.NaN;
        private double _underBid = double.NaN;
        private double _underAsk = double.NaN;
        private bool _useMarketPrice = false;

        public string Symbol { get; }
        public Option Option { get; }
        public SubscriptionFieldType QuoteType { get; }
        public bool IsDisposed { get; set; }
        public OptionPricingModel OptionPricingModel { get; private set; }

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public IvCalculator(string symbol, SubscriptionFieldType quoteType, IEmaConfig emaConfig)
        {
            _emaConfig = emaConfig;
            _emaCalculator = new EmaCalculator(emaConfig, quoteType);
            _emaCalculator.EmaUpdatedEvent += OnEmaUpdate;
            _pricingParameters = new PricingParameters();
            _greeks = new Greeks();
            Option = OptionsHelper.GetOptionFromSymbol(symbol);
            Symbol = symbol;
            QuoteType = quoteType;
            OptionPricingModel = new OptionPricingModel(Symbol, QuoteType)
            {
                Greeks = _greeks
            };
            _ = InitializeAsync();
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
        }

        public void Dispose()
        {
            try
            {
                _emaCalculator.Reset();
                IsDisposed = true;
                OmsCore.QuoteClient.UnsubscribeAll(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        private async Task InitializeAsync()
        {
            Instrument instrument = new(Symbol);
            string underlyingSymbol = instrument.underlyingSymbol;

            Option atmOption = await OptionsHelper.GetAtmOption(underlyingSymbol, instrument.expiration, Option.Type);
            if (atmOption == null)
            {
                _log.Error("Atm option id failed, Symbol: " + instrument.symbol);
            }
            else
            {
                DataStore atmVegaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                atmVegaStore.GetHanweckDataFor(atmOption.OptionSymbol, SubscriptionFieldType.Vega);
                double atmVega = await atmVegaStore.GetDataAsync(atmOption.OptionSymbol);

                DataStore vegaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                vegaStore.GetHanweckDataFor(instrument.symbol, SubscriptionFieldType.Vega);
                double vega = await vegaStore.GetDataAsync(instrument.symbol);

                _useMarketPrice = vega < atmVega * _emaConfig.PercentVegaThreshold;
            }

            if (_useMarketPrice)
            {
                DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                deltaStore.GetHanweckDataFor(instrument.symbol, SubscriptionFieldType.Delta);
                _greeks.Delta = await deltaStore.GetDataAsync(instrument.symbol);
            }
            else
            {
                double underlyingPrice = double.NaN;
                switch (QuoteType)
                {
                    case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.CALL:
                    case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.PUT:
                        underlyingPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.Bid);
                        break;
                    case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.CALL:
                    case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.PUT:
                        underlyingPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.Ask);
                        break;
                }
                MDUnderlying underlyingDetails = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    _log.Error("Underlying details returned null for symbol" + underlyingSymbol);
                }
                else
                {
                    _pricingParameters.Volatility = 0.0;
                    _pricingParameters.PutCall = instrument.callPut == true ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call;
                    _pricingParameters.Strike = instrument.strike;
                    _pricingParameters.DaysToExpiration = (instrument.expiration - DateTime.Now).TotalDays;
                    _pricingParameters.RiskFreeRate = underlyingDetails.RiskFreeRate;
                    _pricingParameters.StockRate = underlyingDetails.StockRate;
                    _pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, underlyingPrice, underlyingDetails.Dividends, DateTime.Now);
                    _pricingParameters.UnderlyingPrice = underlyingPrice;
                    _pricingParameters.UnderlyingMultiplier = underlyingDetails.Multiplier;
                    _pricingParameters.ExerciseStyle = underlyingSymbol.StartsWith("$") ?
                        Comms.Models.Data.Securities.ExerciseStyle.European :
                        Comms.Models.Data.Securities.ExerciseStyle.American;
                }
            }

            switch (QuoteType)
            {
                case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.CALL:
                case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.PUT:
                    OmsCore.QuoteClient.Subscribe(underlyingSymbol, SubscriptionFieldType.Bid, this);
                    break;
                case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.CALL:
                case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.PUT:
                    OmsCore.QuoteClient.Subscribe(underlyingSymbol, SubscriptionFieldType.Ask, this);
                    break;
            }

            switch (QuoteType)
            {
                case SubscriptionFieldType.BidIvEma:
                    OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
                    break;
                case SubscriptionFieldType.AskIvEma:
                    OmsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
                    break;
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            SubscriptionFieldType quoteType = key.Type;
            if (key.Symbol == Symbol)
            {
                switch (quoteType)
                {
                    case SubscriptionFieldType.Bid:
                    case SubscriptionFieldType.Ask:
                        _optionPxUpdate = (double)value;
                        AddToEma();
                        break;
                }
            }
            else
            {
                switch (quoteType)
                {
                    case SubscriptionFieldType.Bid:
                        _underBid = (double)value;
                        OptionPricingModel.OptionPrice = OptionPricingModel.OptionPrice + ((_underBid - OptionPricingModel.UnderlyingPrice) * OptionPricingModel.Greeks.Delta);
                        Update(Symbol, QuoteType, OptionPricingModel);
                        break;
                    case SubscriptionFieldType.Ask:
                        _underAsk = (double)value;
                        OptionPricingModel.OptionPrice = OptionPricingModel.OptionPrice + ((_underAsk - OptionPricingModel.UnderlyingPrice) * OptionPricingModel.Greeks.Delta);
                        Update(Symbol, QuoteType, OptionPricingModel);
                        break;
                }
                if (_useMarketPrice)
                {
                    _emaCalculator.AddUpdate(OptionPricingModel.OptionPrice);
                }
                else
                {
                    _pricingParameters.UnderlyingPrice = (double)value;
                }
            }
        }

        private void AddToEma()
        {
            if (_emaConfig != null && _emaConfig.EmaEnabled)
            {
                if (_useMarketPrice)
                {
                    _emaCalculator.AddUpdate(_optionPxUpdate);
                }
                else
                {
                    double iv = OptionModel.Binomial.ImpliedVolatility(_pricingParameters, _optionPxUpdate, _greeks);
                    _emaCalculator.AddUpdate(iv);
                }
            }
        }

        private void OnEmaUpdate(double ema)
        {
            if (_emaConfig != null && _emaConfig.EmaEnabled)
            {
                if (_useMarketPrice)
                {
                    OptionPricingModel.OriginalPrice = OptionPricingModel.OptionPrice = ema;
                }
                else
                {
                    _pricingParameters.Volatility = OptionPricingModel.Volatility = ema;
                    OptionPricingModel.OriginalPrice = OptionPricingModel.OptionPrice = OptionModel.Binomial.PriceOption(_pricingParameters, _greeks);
                }

                switch (QuoteType)
                {
                    case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.CALL:
                    case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.PUT:
                        OptionPricingModel.UnderlyingPrice = _underBid;
                        break;
                    case SubscriptionFieldType.AskIvEma when Option.Type == OptionType.CALL:
                    case SubscriptionFieldType.BidIvEma when Option.Type == OptionType.PUT:
                        OptionPricingModel.UnderlyingPrice = _underAsk;
                        break;
                }
            }
            else
            {
                OptionPricingModel.OptionPrice = double.NaN;
            }
            Update(Symbol, QuoteType, OptionPricingModel);
        }
    }
}
