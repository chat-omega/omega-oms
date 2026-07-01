using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Oms.Clients
{
    internal class PositionSubscribers
    {
        public bool _SubscriptionInitialized = false;

        private readonly object _subscribersLock = new();
        private readonly HashSet<IOmsPositionSubscriber> _subscribers = new();
        private object _cachedValue;

        public Tuple<string, string> SubscriptionKey { get; }
        public int SubscribersCount => _subscribers.Count;

        public PositionSubscribers(Tuple<string, string> subscriptionKey)
        {
            SubscriptionKey = subscriptionKey;
        }

        internal void AddAndInitSubscriber(IOmsPositionSubscriber subscriber)
        {
            lock (_subscribersLock)
            {
                _subscribers.Add(subscriber);
            }

            if (_SubscriptionInitialized)
            {
                subscriber.SubscibedPositionUpdateValue(SubscriptionKey, _cachedValue);
            }
        }

        internal void Remove(IOmsPositionSubscriber subscriber)
        {
            lock (_subscribersLock)
            {
                _subscribers.Remove(subscriber);
            }
        }

        internal bool IsEmpty()
        {
            lock (_subscribersLock)
            {
                return _subscribers.Count == 0;
            }
        }

        internal void UpdateValues(object value)
        {
            foreach (IOmsPositionSubscriber subscriber in _subscribers.ToList())
            {
                subscriber.SubscibedPositionUpdateValue(SubscriptionKey, value);
            }

            if (!_SubscriptionInitialized)
            {
                _SubscriptionInitialized = true;
            }

            _cachedValue = value;
        }
    }
}