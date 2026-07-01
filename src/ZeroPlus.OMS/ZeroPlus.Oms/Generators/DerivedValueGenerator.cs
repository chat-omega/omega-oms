using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Generators
{
    public class DerivedValueGenerator : SubscriptionProvider
    {
        private readonly ConcurrentDictionary<string, UnderlyingDerivator> _keyToDerivatorMap;
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public DerivedValueGenerator()
        {
            _keyToDerivatorMap = new ConcurrentDictionary<string, UnderlyingDerivator>();
        }

        protected override void Subscribe(SubscriptionKey subscription)
        {
            string symbol = subscription.Symbol;
            SubscriptionFieldType type = subscription.Type;
            if (type is SubscriptionFieldType.DerivedBid or
                SubscriptionFieldType.DerivedAsk)
            {
                if (!_keyToDerivatorMap.TryGetValue(symbol, out UnderlyingDerivator derivator))
                {
                    if (OmsCore.Config.DerivedValueConfigModelLookup.TryGetValue(symbol?.ToUpper(), out Data.Models.DerivedValueConfigModel derivedValueConfig))
                    {
                        derivator = new UnderlyingDerivator(derivedValueConfig, Update);
                        _keyToDerivatorMap[symbol] = derivator;
                    }
                }
            }
        }

        protected override void Unsubscribe(SubscriptionKey subscription)
        {
        }
    }
}
