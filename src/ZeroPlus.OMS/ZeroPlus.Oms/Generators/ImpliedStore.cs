using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Generators
{
    public class ImpliedStore
    {
        private readonly SubscriptionProvider _subscriptionProvider;
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<Tuple<string, DateTime, OptionType, SubscriptionFieldType>, Interpolator> _interpolatorKeyToInterpolatorMap = new();

        public ImpliedStore(SubscriptionProvider subscriptionProvider)
        {
            _subscriptionProvider = subscriptionProvider;
        }

        internal void InitializeInterpolator(string symbol, SubscriptionFieldType quoteType)
        {
            Option option = OptionsHelper.GetOptionFromSymbol(symbol);
            InitializeInterpolator(option, quoteType);
        }

        internal void InitializeInterpolator(Option option, SubscriptionFieldType quoteType)
        {
            Interpolator interpolator = GetInterpolator(option, quoteType);
            interpolator.AddSubscription(option);
        }

        internal async void RemoveInterpolator(string symbol, SubscriptionFieldType quoteType)
        {
            Option option = OptionsHelper.GetOptionFromSymbol(symbol);
            Tuple<string, DateTime, OptionType, SubscriptionFieldType> interpolatorKey = Tuple.Create(option.UnderlyingSymbol, option.Expiration, option.Type, quoteType);
            Interpolator interpolator = null;
            lock (_lock)
            {
                _interpolatorKeyToInterpolatorMap.TryGetValue(interpolatorKey, out interpolator);
            }
            if (interpolator != null)
            {
                await interpolator.RemoveSubscriptionAsync(option);
                lock (_lock)
                {
                    if (interpolator.IsEmpty())
                    {
                        _interpolatorKeyToInterpolatorMap.TryRemove(interpolatorKey, out _);
                    }
                }
            }
        }

        private Interpolator GetInterpolator(Option option, SubscriptionFieldType quoteType)
        {
            Tuple<string, DateTime, OptionType, SubscriptionFieldType> interpolatorKey = Tuple.Create(option.UnderlyingSymbol, option.Expiration, option.Type, quoteType);
            lock (_lock)
            {
                if (!_interpolatorKeyToInterpolatorMap.TryGetValue(interpolatorKey, out Interpolator interpolator))
                {
                    interpolator = new Interpolator(option.UnderlyingSymbol, option.Expiration, option.Type, quoteType)
                    {
                        SubscriptionProvider = _subscriptionProvider,
                    };
                    _interpolatorKeyToInterpolatorMap[interpolatorKey] = interpolator;
                }
                return interpolator;
            }
        }
    }
}