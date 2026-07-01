using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Indicators
{
    public class EmaDerivator : SubscriptionProvider, IOmsDataSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public readonly IEmaConfig EmaConfig;
        public readonly OptionPricingModel _optionPricingModel;
        private bool _running;
        private readonly Option _option;
        private double _underQuote = double.NaN;
        private double _optionQuote = double.NaN;
        private double _prevPeriodStartOptionQuote = double.NaN;
        private double _prevPeriodStartUnderQuote = double.NaN;

        public SubscriptionFieldType QuoteType { get; }

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public bool IsDisposed { get; set; }

        public EmaDerivator(string symbol, SubscriptionFieldType type, IEmaConfig emaConfig)
        {
            _optionPricingModel = new OptionPricingModel(symbol, type);
            _option = OptionsHelper.GetOptionFromSymbol(symbol);
            QuoteType = type;
            EmaConfig = emaConfig;
            EmaConfig.ResetEmaEvent += OnEmaConfig_ResetEmaEvent;
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            OmsCore.GreekClient.Subscribe(_option.OptionSymbol, SubscriptionFieldType.Delta, this);
            switch (QuoteType)
            {
                case SubscriptionFieldType.DerivedBidEma:
                    OmsCore.QuoteClient.Subscribe(_option.OptionSymbol, SubscriptionFieldType.Bid, this);
                    break;
                case SubscriptionFieldType.DerivedAskEma:
                    OmsCore.QuoteClient.Subscribe(_option.OptionSymbol, SubscriptionFieldType.Ask, this);
                    break;
            }

            switch (_option.Type)
            {
                case OptionType.CALL when QuoteType == SubscriptionFieldType.DerivedBidEma:
                case OptionType.PUT when QuoteType == SubscriptionFieldType.DerivedAskEma:
                    OmsCore.DerivedValueGenerator.Subscribe(_option.UnderlyingSymbol, SubscriptionFieldType.DerivedBid, this);
                    break;
                case OptionType.CALL when QuoteType == SubscriptionFieldType.DerivedAskEma:
                case OptionType.PUT when QuoteType == SubscriptionFieldType.DerivedBidEma:
                    OmsCore.DerivedValueGenerator.Subscribe(_option.UnderlyingSymbol, SubscriptionFieldType.DerivedAsk, this);
                    break;
            }
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
            OmsCore.GreekClient.Unsubscribe(_option.OptionSymbol, SubscriptionFieldType.Delta, this);
            switch (QuoteType)
            {
                case SubscriptionFieldType.DerivedBidEma:
                    OmsCore.QuoteClient.Unsubscribe(_option.OptionSymbol, SubscriptionFieldType.Bid, this);
                    break;
                case SubscriptionFieldType.DerivedAskEma:
                    OmsCore.QuoteClient.Unsubscribe(_option.OptionSymbol, SubscriptionFieldType.Ask, this);
                    break;
            }

            switch (_option.Type)
            {
                case OptionType.CALL when QuoteType == SubscriptionFieldType.DerivedBidEma:
                case OptionType.PUT when QuoteType == SubscriptionFieldType.DerivedAskEma:
                    OmsCore.DerivedValueGenerator.Unsubscribe(_option.UnderlyingSymbol, SubscriptionFieldType.DerivedBid, this);
                    break;
                case OptionType.CALL when QuoteType == SubscriptionFieldType.DerivedAskEma:
                case OptionType.PUT when QuoteType == SubscriptionFieldType.DerivedBidEma:
                    OmsCore.DerivedValueGenerator.Unsubscribe(_option.UnderlyingSymbol, SubscriptionFieldType.DerivedAsk, this);
                    break;
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
                    case SubscriptionFieldType.DerivedAsk when symbol == _option.UnderlyingSymbol:
                    case SubscriptionFieldType.DerivedBid when symbol == _option.UnderlyingSymbol:
                        _underQuote = update;
                        DeltaAdjustEma();
                        break;
                    case SubscriptionFieldType.Ask when symbol == _option.OptionSymbol:
                    case SubscriptionFieldType.Bid when symbol == _option.OptionSymbol:
                        _optionQuote = update;
                        AddUpdate(_optionQuote);
                        break;
                    case SubscriptionFieldType.Delta when symbol == _option.OptionSymbol:
                        _optionPricingModel.Greeks.Delta = update;
                        break;
                }
            }
        }

        private void DeltaAdjustEma()
        {
            _optionPricingModel.OptionPrice = _optionPricingModel.OriginalPrice + ((_underQuote - _optionPricingModel.UnderlyingPrice) * _optionPricingModel.Greeks.Delta);
            UpdateEma();
        }

        public void Reset()
        {
            if (EmaConfig == null)
            {
                return;
            }
            if (_running)
            {
                _running = false;
                _underQuote = _optionPricingModel.OptionPrice = _optionPricingModel.OriginalPrice = double.NaN;
                UpdateEma();
            }
        }

        internal void Dispose()
        {
            try
            {
                _running = false;
                OmsCore.QuoteClient.UnsubscribeAll(this);
                OmsCore.DerivedValueGenerator.UnsubscribeAll(this);
                OmsCore.GreekClient.UnsubscribeAll(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
            finally
            {
                IsDisposed = true;
            }
        }

        private void AddUpdate(double newValue)
        {
            if (EmaConfig == null)
            {
                return;
            }

            if (double.IsNaN(_optionPricingModel.OriginalPrice))
            {
                _optionPricingModel.OptionPrice = _optionPricingModel.OriginalPrice = _optionQuote;
                UpdateEma();
            }

            StartEma();
        }

        private void StartEma()
        {
            if (!_running)
            {
                _running = true;
                Task.Run(() => RecalculateEmaAsync());
            }
        }

        private async Task RecalculateEmaAsync()
        {
            while (_running)
            {
                await Task.Delay((int)EmaConfig.EmaInterval);
                double alpha = EmaConfig.EmaSmoothing / (1 + EmaConfig.EmaPeriods);
                double newValue = _optionQuote;

                double changeInUnderlying = (_underQuote - _prevPeriodStartUnderQuote) * _optionPricingModel.Greeks.Delta;
                switch (QuoteType)
                {
                    case SubscriptionFieldType.DerivedBidEma:
                        if (_optionQuote - _prevPeriodStartOptionQuote - changeInUnderlying < -Math.Abs(EmaConfig.MaxBidDeviation))
                        {
                            _prevPeriodStartOptionQuote = _optionQuote - (_optionQuote - _prevPeriodStartOptionQuote) - changeInUnderlying - EmaConfig.MaxBidDeviation;
                        }
                        if (_optionQuote - _prevPeriodStartOptionQuote - changeInUnderlying < -Math.Abs(EmaConfig.MaxBidDeviation))
                        {
                            newValue = _optionQuote - (_optionQuote - _prevPeriodStartOptionQuote) - changeInUnderlying - EmaConfig.MaxBidDeviation;
                        }
                        break;
                    case SubscriptionFieldType.DerivedAskEma:
                        if (_optionQuote - _prevPeriodStartOptionQuote - changeInUnderlying > Math.Abs(EmaConfig.MaxAskDeviation))
                        {
                            _prevPeriodStartOptionQuote = _optionQuote - (_optionQuote - _prevPeriodStartOptionQuote) - changeInUnderlying + EmaConfig.MaxAskDeviation;
                        }
                        if (_optionQuote - _prevPeriodStartOptionQuote - changeInUnderlying > Math.Abs(EmaConfig.MaxAskDeviation))
                        {
                            newValue = _optionQuote - (_optionQuote - _prevPeriodStartOptionQuote) - changeInUnderlying + EmaConfig.MaxAskDeviation;
                        }
                        break;
                }

                _prevPeriodStartOptionQuote = _optionQuote;
                _optionPricingModel.UnderlyingPrice = _prevPeriodStartUnderQuote = _underQuote;

                double newEma = (newValue * alpha) + (_optionPricingModel.OptionPrice * (1 - alpha));
                _optionPricingModel.OptionPrice = _optionPricingModel.OriginalPrice = newEma;
                UpdateEma();
            }
        }

        private void OnEmaConfig_ResetEmaEvent()
        {
            _optionPricingModel.UnderlyingPrice = _prevPeriodStartUnderQuote = _underQuote;
            _optionPricingModel.OptionPrice = _optionPricingModel.OriginalPrice = _optionQuote;
            UpdateEma();
        }

        private void UpdateEma()
        {
            Update(_option.OptionSymbol, QuoteType, _optionPricingModel);
        }
    }
}
