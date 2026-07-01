using NLog;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Clients
{
    public delegate void ValueUpdatedEventHandler(SubscriptionKey subscriptionKey, object value, bool isFromCache);

    public sealed class DataSubscribers : IDataSubscribers
    {
        public event ValueUpdatedEventHandler ValueUpdatedEvent
        {
            add
            {
                lock (_lock)
                {
                    _internalEventDelegate += value;
                    RebuildSnapshot();
                }
            }
            remove
            {
                lock (_lock)
                {
                    _internalEventDelegate -= value;
                    RebuildSnapshot();
                }
            }
        }

        // We implement the event manually to expose the optimized array to the rest of the class
        // without relying on the slow GetInvocationList() reflection during updates.
        private readonly object _lock = new();
        private ValueUpdatedEventHandler _internalEventDelegate;

        // This array is the "Hot Path" source of truth. It is swapped atomically on updates.
        private volatile ValueUpdatedEventHandler[] _handlersSnapshot = Array.Empty<ValueUpdatedEventHandler>();

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly HashSet<SubscriptionFieldType> _notCachedTypes = [SubscriptionFieldType.TronTrade];

        // Pre-calculated reset value to avoid switch statements at runtime
        private readonly object _resetValue;
        private readonly bool _allowCaching;
        // Used to enforce the unique subscriber logic from the original code
        private readonly HashSet<IOmsDataSubscriber> _uniqueSubscribers = new();

        private volatile object _cachedValue;

        public readonly SubscriptionKey SubscriptionKey;
        public bool SubscriptionInitialized { get; set; }

        public DataSubscribers(SubscriptionKey subscriptionKey)
        {
            SubscriptionKey = subscriptionKey;
            _allowCaching = !_notCachedTypes.Contains(subscriptionKey.Type);
            _resetValue = DetermineResetValue(subscriptionKey.Type);
        }

        private static object DetermineResetValue(SubscriptionFieldType type)
        {
            switch (type)
            {
                case SubscriptionFieldType.Bid:
                case SubscriptionFieldType.Ask:
                case SubscriptionFieldType.Delta:
                case SubscriptionFieldType.Gamma:
                case SubscriptionFieldType.Vega:
                case SubscriptionFieldType.Theta:
                case SubscriptionFieldType.Rho:
                case SubscriptionFieldType.ImpliedVol:
                case SubscriptionFieldType.TheorethicalValue:
                case SubscriptionFieldType.GreekUnderBid:
                case SubscriptionFieldType.GreekUnderAsk:
                case SubscriptionFieldType.GreekBid:
                case SubscriptionFieldType.GreekAsk:
                case SubscriptionFieldType.PreviousClose:
                case SubscriptionFieldType.Ema:
                case SubscriptionFieldType.BidInterpolated:
                case SubscriptionFieldType.AskInterpolated:
                case SubscriptionFieldType.DeltaAdjTheo:
                case SubscriptionFieldType.DeltaAdjTheoDelta:
                case SubscriptionFieldType.DeltaAdjTheoMid:
                case SubscriptionFieldType.DeltaAdjTheoBase:
                    return double.NaN;
                default:
                    return null;
            }
        }

        public void AddAndInitSubscriber(IOmsDataSubscriber subscriber)
        {
            lock (_lock)
            {
                // Enforce Uniqueness: Remove existing if present so we can re-add (move to end/update)
                if (!_uniqueSubscribers.Add(subscriber))
                {
                    _internalEventDelegate -= subscriber.SubscribedDataUpdateValue;
                }

                _internalEventDelegate += subscriber.SubscribedDataUpdateValue;
                RebuildSnapshot();

                if (SubscriptionInitialized && _allowCaching)
                {
                    InitSubscriber(subscriber);
                }
            }
        }

        private void InitSubscriber(IOmsDataSubscriber subscriber)
        {
            try
            {
                subscriber.SubscribedDataUpdateValue(SubscriptionKey, _cachedValue, true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddAndInitSubscriber));
            }
        }

        public bool Remove(IOmsDataSubscriber subscriber)
        {
            lock (_lock)
            {
                _uniqueSubscribers.Remove(subscriber);
                _internalEventDelegate -= subscriber.SubscribedDataUpdateValue;
                RebuildSnapshot();

                return _internalEventDelegate == null;
            }
        }

        public bool IsEmpty()
        {
            return _handlersSnapshot.Length == 0;
        }

        public void ResetValuesAsync()
        {
            UpdateValues(_resetValue);
        }

        /// <summary>
        /// HOT PATH: This method is called very frequently.
        /// Optimization: Zero locks, zero allocations.
        /// </summary>
        public void UpdateValues(object value, bool isFromCache = false, bool allowCaching = true)
        {
            if (_allowCaching && allowCaching)
            {
                _cachedValue = value;
            }

            if (!SubscriptionInitialized)
            {
                SubscriptionInitialized = true;
            }

            // 1. Atomic Read: Grab reference to current array.
            // Even if 'Add' is called on another thread right now, we iterate the "old" safe array.

            var handlers = _handlersSnapshot;

            // 2. Iterate array directly (No GetInvocationList() allocation)
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](SubscriptionKey, value, isFromCache);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(UpdateValues));
                }
            }
        }

        // Must be called inside a lock
        private void RebuildSnapshot()
        {
            if (_internalEventDelegate == null)
            {
                _handlersSnapshot = Array.Empty<ValueUpdatedEventHandler>();
            }
            else
            {
                // GetInvocationList returns an array of delegates.
                // We cast it once here so the hot path doesn't have to.
                var invocationList = _internalEventDelegate.GetInvocationList();
                var newSnap = new ValueUpdatedEventHandler[invocationList.Length];
                for (int i = 0; i < invocationList.Length; i++)
                {
                    newSnap[i] = (ValueUpdatedEventHandler)invocationList[i];
                }
                _handlersSnapshot = newSnap;
            }
        }
    }
}