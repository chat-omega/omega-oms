using NLog;
using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;

namespace ZeroPlus.Oms.Generators
{
    internal class UnderlyingDerivator : IOmsDataSubscriber, IEmaConfig
    {
        public event ResetEmaEventHandler ResetEmaEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly string _symbol;
        private readonly string _derivedSymbol;
        private readonly double _multiplier;
        private readonly Action<string, SubscriptionFieldType, object, bool, bool> _updateHandler;
        private readonly EmaCalculator _emaCalculator;

        private double _symbolBid;
        private double _symbolAsk;
        private double _symbolDerivedBid;
        private double _symbolDerivedAsk;
        private double _derivedOffsetEma;

        public OmsCore OmsCore { get; } = ServiceLocator.GetService<OmsCore>();
        public bool IsDisposed { get; set; }

        public bool EmaEnabled { get; set; }
        public EmaType SelectedEmaType { get; set; }
        public double PercentVegaThreshold { get; set; }
        public double EmaSmoothing { get; set; }
        public double EmaInterval { get; set; }
        public double EmaPeriods { get; set; }
        public double MaxBidDeviation { get; set; }
        public double MaxAskDeviation { get; set; }

        public UnderlyingDerivator(DerivedValueConfigModel derivedValueConfig, Action<string, SubscriptionFieldType, object, bool, bool> updateHandler)
        {
            EmaEnabled = true;
            EmaSmoothing = 2;
            EmaInterval = 3000;
            EmaPeriods = 200;

            _symbol = derivedValueConfig.Symbol;
            _derivedSymbol = derivedValueConfig.DerivedSymbol;
            _multiplier = derivedValueConfig.Multiplier;
            _updateHandler = updateHandler;

            _symbolBid = double.NaN;
            _symbolAsk = double.NaN;
            _symbolDerivedBid = double.NaN;
            _symbolDerivedAsk = double.NaN;
            _derivedOffsetEma = double.NaN;

            _emaCalculator = new EmaCalculator(this, SubscriptionFieldType.DerivedBid);
            _emaCalculator.EmaUpdatedEvent += OnEmaCalculatorUpdatedEvent;
            Initialize();
        }

        private void Initialize()
        {
            if (!string.IsNullOrWhiteSpace(_symbol) &&
                !string.IsNullOrWhiteSpace(_derivedSymbol) &&
                _multiplier > 0)
            {
                OmsCore.QuoteClient.Subscribe(_symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(_symbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Subscribe(_derivedSymbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(_derivedSymbol, SubscriptionFieldType.Ask, this);
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (IsDisposed)
            {
                return;
            }

            string symbol = key.Symbol;
            SubscriptionFieldType type = key.Type;
            if (value is double update)
            {
                switch (type)
                {
                    case SubscriptionFieldType.Bid when symbol == _symbol:
                        _symbolBid = update;
                        break;
                    case SubscriptionFieldType.Ask when symbol == _symbol:
                        _symbolAsk = update;
                        break;
                    case SubscriptionFieldType.Bid when symbol == _derivedSymbol:
                        _symbolDerivedBid = update;
                        double derivedBid = (update * _multiplier) + _derivedOffsetEma;
                        _updateHandler?.Invoke(_symbol, SubscriptionFieldType.DerivedBid, derivedBid, false, false);
                        break;
                    case SubscriptionFieldType.Ask when symbol == _derivedSymbol:
                        _symbolDerivedAsk = update;
                        double derivedAsk = (update * _multiplier) + _derivedOffsetEma;
                        _updateHandler?.Invoke(_symbol, SubscriptionFieldType.DerivedAsk, derivedAsk, false, false);
                        break;
                }
                double offset = ((_symbolBid + _symbolAsk) / 2) - ((_symbolDerivedBid + _symbolDerivedAsk) / 2 * _multiplier);
                _emaCalculator.AddUpdate(offset);
            }
        }

        internal void Dispose()
        {
            try
            {
                OmsCore.QuoteClient.UnsubscribeAll(this);
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

        internal void ResetEma()
        {
            ResetEmaEvent?.Invoke();
        }

        private void OnEmaCalculatorUpdatedEvent(double ema)
        {
            _derivedOffsetEma = ema;
        }
    }
}
