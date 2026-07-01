using NLog;
using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Indicators
{
    public class EmaCalculatorGenerator
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<SubscriptionKey, IvCalculator> _keyToIvCalculatorMap = new();
        private readonly ConcurrentDictionary<SubscriptionKey, EmaDerivator> _keyToEmaDerivatorMap = new();
        private readonly IEmaConfig _emaConfig;

        public EmaCalculatorGenerator(IEmaConfig emaConfig)
        {
            _emaConfig = emaConfig;
        }

        public void Subscribe(string symbol, SubscriptionFieldType quoteType, IOmsDataSubscriber subscriber)
        {
            if (_emaConfig.EmaEnabled)
            {
                SubscriptionKey key = new(symbol, quoteType);
                switch (_emaConfig.SelectedEmaType)
                {
                    case EmaType.IV:
                        if (!_keyToIvCalculatorMap.TryGetValue(key, out IvCalculator ivCalculator))
                        {
                            ivCalculator = new IvCalculator(symbol, quoteType, _emaConfig);
                            _keyToIvCalculatorMap[key] = ivCalculator;
                        }
                        ivCalculator.Subscribe(symbol, quoteType, subscriber);
                        break;
                    case EmaType.Derived:
                        if (!_keyToEmaDerivatorMap.TryGetValue(key, out EmaDerivator derivator))
                        {
                            derivator = new EmaDerivator(symbol, quoteType, _emaConfig);
                            _keyToEmaDerivatorMap[key] = derivator;
                        }
                        derivator.Subscribe(symbol, quoteType, subscriber);
                        break;
                }
            }
        }

        public void Unsubscribe(string symbol, SubscriptionFieldType quoteType, IOmsDataSubscriber subscriber)
        {
            SubscriptionKey key = new(symbol, quoteType);
            if (_keyToIvCalculatorMap.TryGetValue(key, out IvCalculator ivCalculator))
            {
                ivCalculator.Unsubscribe(symbol, quoteType, subscriber);
            }
            if (_keyToEmaDerivatorMap.TryGetValue(key, out EmaDerivator derivator))
            {
                derivator.Unsubscribe(symbol, quoteType, subscriber);
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (IvCalculator ivCalculator in _keyToIvCalculatorMap.Values)
                {
                    ivCalculator.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }
    }
}
