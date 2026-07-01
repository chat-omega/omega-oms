using NLog;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Generators;

namespace ZeroPlus.Oms.Clients
{
    public abstract class SubscriptionProvider : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<SubscriptionKey, IDataSubscribers> _subscriptionKeyToSubscribersMap = new();
        private readonly ConcurrentDictionary<IOmsDataSubscriber, ConcurrentDictionary<SubscriptionKey, byte>> _subscriberToSubscriptionsMap = new();
        private readonly object[] _subscriptionLockStripes = Enumerable.Range(0, 1024).Select(_ => new object()).ToArray();

        protected OmsConfig Config { get; set; }
        protected ImpliedStore ImpliedStore { get; set; }
        protected abstract void Subscribe(SubscriptionKey subscription);
        protected abstract void Unsubscribe(SubscriptionKey subscription);

        public void Subscribe(string symbol, SubscriptionFieldType type, IOmsDataSubscriber subscriber)
        {
            try
            {
                SubscriptionKey subscriptionKey = new SubscriptionKey(symbol, type);
                bool isNewSub = AddToLookupMaps(subscriber, subscriptionKey);
                if (isNewSub)
                {
                    Subscribe(subscriptionKey);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(Subscribe)} -> Exception with subscription.");
            }
        }

        public async Task UnsubscribeAsync(string symbol, SubscriptionFieldType type, IOmsDataSubscriber subscriber)
        {
            try
            {
                await Task.Run(() => Unsubscribe(symbol, type, subscriber));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeAsync)} -> Exception with subscription.");
            }
        }

        public void Unsubscribe(string symbol, SubscriptionFieldType type, IOmsDataSubscriber subscriber)
        {
            var subscriptionKey = new SubscriptionKey(symbol, type);
            lock (GetSubscriptionLock(subscriptionKey))
            {
                if (_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionKey, out IDataSubscribers subscribers))
                {
                    bool isEmpty = subscribers.Remove(subscriber);
                    RemoveSubscriptionFromSubscriberMap(subscriber, subscriptionKey);
                    if (isEmpty || type == SubscriptionFieldType.Close)
                    {
                        _subscriptionKeyToSubscribersMap.TryRemove(subscriptionKey, out _);
                        Unsubscribe(subscriptionKey);
                    }
                }
            }
        }

        public bool RemoveFromLookupMaps(IOmsDataSubscriber subscriber, SubscriptionKey subscriptionKey)
        {
            bool isEmpty = false;
            lock (GetSubscriptionLock(subscriptionKey))
            {
                if (_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionKey, out IDataSubscribers subscribers))
                {
                    isEmpty = subscribers.Remove(subscriber);
                    RemoveSubscriptionFromSubscriberMap(subscriber, subscriptionKey);
                }
            }
            return isEmpty;
        }

        public async Task UnsubscribeAllAsync(IOmsDataSubscriber subscriber)
        {
            try
            {
                await Task.Run(() => UnsubscribeAll(subscriber));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeAllAsync)} -> Exception removing subscription.");
            }
        }

        public void UnsubscribeAll(IOmsDataSubscriber subscriber)
        {
            if (_subscriberToSubscriptionsMap.TryRemove(subscriber, out ConcurrentDictionary<SubscriptionKey, byte> subscriptions))
            {
                foreach (SubscriptionKey subscription in subscriptions.Keys)
                {
                    try
                    {
                        lock (GetSubscriptionLock(subscription))
                        {
                            if (_subscriptionKeyToSubscribersMap.TryGetValue(subscription, out IDataSubscribers subscribers))
                            {
                                bool isEmpty = subscribers.Remove(subscriber);
                                if (isEmpty)
                                {
                                    _subscriptionKeyToSubscribersMap.TryRemove(subscription, out _);
                                    Unsubscribe(subscription);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, $"{nameof(UnsubscribeAll)} -> Exception removing subscription. Symbol: {subscription.Symbol}, Type: {subscription.Type}.");
                    }
                }
            }
        }

        public void UnsubscribeAll(SubscriptionFieldType type, IOmsDataSubscriber subscriber)
        {
            if (_subscriberToSubscriptionsMap.TryGetValue(subscriber, out ConcurrentDictionary<SubscriptionKey, byte> subscriptions))
            {
                foreach (SubscriptionKey subscription in subscriptions.Keys.Where(x => x.Type == type))
                {
                    try
                    {
                        lock (GetSubscriptionLock(subscription))
                        {
                            if (_subscriptionKeyToSubscribersMap.TryGetValue(subscription, out IDataSubscribers subscribers))
                            {
                                subscribers.Remove(subscriber);
                                subscriptions.TryRemove(subscription, out _);
                                if (subscribers.IsEmpty())
                                {
                                    _subscriptionKeyToSubscribersMap.TryRemove(subscription, out _);
                                    Unsubscribe(subscription);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, $"{nameof(UnsubscribeAll)} -> Exception removing subscription. Symbol: {subscription.Symbol}, Type: {subscription.Type}.");
                    }
                }

                if (subscriptions.IsEmpty)
                {
                    _subscriberToSubscriptionsMap.TryRemove(subscriber, out _);
                }
            }
        }

        /// <summary>
        /// Pushes an update value to all subscribers for a given symbol and subscription type.
        /// If no subscribers exist for the specified key, the update is ignored unless <paramref name="saveCache"/> is true.
        /// Null updates are ignored.
        /// </summary>
        /// <param name="symbol">The symbol associated with the update.</param>
        /// <param name="type">The type of subscription field being updated.</param>
        /// <param name="update">The new value for the subscription. Null values will be ignored.</param>
        /// <param name="saveCache">
        /// If true and no subscribers currently exist for the given <paramref name="symbol"/> and <paramref name="type"/>,
        /// a new <see cref="IDataSubscribers"/> instance will be created and the update will be cached and sent to it.
        /// Defaults to false.
        /// </param>
        ///  <param name="isFromCache"/>
        public void Update(string symbol, SubscriptionFieldType type, object update, bool saveCache = false, bool isFromCache = false)
        {
            // don't send null to subscribers
            if (update is null) return;
            try
            {
                SubscriptionKey subscriptionKey = new(symbol, type);
                if (TryGetSubscribers(subscriptionKey, out IDataSubscribers subscribers))
                {
                    subscribers.UpdateValues(update, isFromCache);
                }
                else if (saveCache)
                {
                    lock (GetSubscriptionLock(subscriptionKey))
                    {
                        var newDataSubscribers = new DataSubscribers(subscriptionKey);
                        if (_subscriptionKeyToSubscribersMap.TryAdd(subscriptionKey, newDataSubscribers))
                        {
                            newDataSubscribers.UpdateValues(update, isFromCache);
                        }
                    }

                    subscribers?.UpdateValues(update, isFromCache);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Update));
            }
        }

        public void Resubscribe()
        {
            foreach (SubscriptionKey subscription in _subscriptionKeyToSubscribersMap.Keys.ToList())
            {
                lock (GetSubscriptionLock(subscription))
                {
                    Subscribe(subscription);
                }
            }
        }

        protected void Resubscribe(SubscriptionFieldType type)
        {
            foreach (SubscriptionKey subscription in _subscriptionKeyToSubscribersMap.Where(x => x.Key.Type == type).Select(x => x.Key).ToList())
            {
                lock (GetSubscriptionLock(subscription))
                {
                    Subscribe(subscription);
                }
            }
        }

        protected bool AddToLookupMaps(IOmsDataSubscriber subscriber, SubscriptionKey subscriptionKey)
        {
            bool isNewSubscription = false;
            IDataSubscribers subscribers;

            lock (GetSubscriptionLock(subscriptionKey))
            {
                if (!_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionKey, out subscribers))
                {
                    subscribers = new DataSubscribers(subscriptionKey);
                    _subscriptionKeyToSubscribersMap[subscriptionKey] = subscribers;
                }

                if (subscribers.IsEmpty())
                {
                    isNewSubscription = true;
                }

                if (subscriber != null)
                {
                    subscribers.AddAndInitSubscriber(subscriber);

                    if (!_subscriberToSubscriptionsMap.TryGetValue(subscriber, out ConcurrentDictionary<SubscriptionKey, byte> subscriptions))
                    {
                        subscriptions = new ConcurrentDictionary<SubscriptionKey, byte>();
                        _subscriberToSubscriptionsMap[subscriber] = subscriptions;
                    }
                    subscriptions[subscriptionKey] = byte.MinValue;
                }
            }

            return isNewSubscription || !subscribers.SubscriptionInitialized;
        }

        protected bool TryGetSubscribers(SubscriptionKey subscriptionId, out IDataSubscribers subscribers, bool unsubscribeIfEmpty = true)
        {
            try
            {
                if (_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionId, out subscribers))
                {
                    if (!subscribers.IsEmpty())
                    {
                        return true;
                    }
                    else if (unsubscribeIfEmpty)
                    {
                        lock (GetSubscriptionLock(subscriptionId))
                        {
                            if (_subscriptionKeyToSubscribersMap.TryGetValue(subscriptionId, out var current))
                            {
                                if (!current.IsEmpty())
                                {
                                    subscribers = current;
                                    return true;
                                }
                                _subscriptionKeyToSubscribersMap.TryRemove(subscriptionId, out _);
                                Unsubscribe(subscriptionId);
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetSubscribers));
                subscribers = null;
                return false;
            }
        }

        private object GetSubscriptionLock(SubscriptionKey subscriptionKey)
        {
            int index = subscriptionKey.GetHashCode() % _subscriptionLockStripes.Length;
            return _subscriptionLockStripes[index];
        }

        private void RemoveSubscriptionFromSubscriberMap(IOmsDataSubscriber subscriber, SubscriptionKey subscriptionKey)
        {
            if (_subscriberToSubscriptionsMap.TryGetValue(subscriber, out ConcurrentDictionary<SubscriptionKey, byte> subscriptions))
            {
                subscriptions.TryRemove(subscriptionKey, out _);
                if (subscriptions.IsEmpty)
                {
                    _subscriberToSubscriptionsMap.TryRemove(subscriber, out _);
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}