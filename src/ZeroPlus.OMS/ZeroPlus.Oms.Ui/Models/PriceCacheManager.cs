using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Oms.Ui.Models
{
    internal class PriceCacheManager
    {
        private readonly object _locker = new();
        private readonly ConcurrentDictionary<string, PriceCache> _spreadIdToPriceCacheMap = new();
        private readonly ConcurrentDictionary<string, PriceCache> _spreadGenericKeyToPriceCacheMap = new();
        private readonly List<PriceCache> _priceCaches = new();
        private Timer _timer;

        public PriceCacheManager()
        {
            _timer = new Timer();
            _timer.Elapsed += ClearTimerElapsed;
            _timer.AutoReset = true;
            _timer.Interval = 1000;
            _timer.Start();
        }

        private void ClearTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                _timer.Stop();

                for (int i = 0; i < _priceCaches.Count; i++)
                {
                    PriceCache item = _priceCaches[i];
                    if (!double.IsNaN(item.LowestAsk) && (DateTime.Now - item.LowestAskTimestamp).TotalMilliseconds > OmsCore.Config.PriceCacheClearIntervalMs)
                    {
                        item.LowestAsk = double.NaN;
                        item.LowestAskUnderlying = double.NaN;
                    }
                    if (!double.IsNaN(item.HighestAsk) && (DateTime.Now - item.HighestAskTimestamp).TotalMilliseconds > OmsCore.Config.PriceCacheClearIntervalMs)
                    {
                        item.HighestAsk = double.NaN;
                        item.HighestAskUnderlying = double.NaN;
                    }
                    if (!double.IsNaN(item.LowestBid) && (DateTime.Now - item.LowestBidTimestamp).TotalMilliseconds > OmsCore.Config.PriceCacheClearIntervalMs)
                    {
                        item.LowestBid = double.NaN;
                        item.LowestBidUnderlying = double.NaN;
                    }
                    if (!double.IsNaN(item.HighestBid) && (DateTime.Now - item.HighestBidTimestamp).TotalMilliseconds > OmsCore.Config.PriceCacheClearIntervalMs)
                    {
                        item.HighestBid = double.NaN;
                        item.HighestBidUnderlying = double.NaN;
                    }
                }
            }
            finally
            {
                _timer.Start();
            }
        }

        public bool TryGetValue(string key, bool createIfNotFound, out PriceCache value)
        {
            try
            {
                if (!_spreadIdToPriceCacheMap.TryGetValue(key, out value))
                {
                    if (createIfNotFound)
                    {
                        value = new PriceCache();
                        _spreadIdToPriceCacheMap[key] = value;
                        lock (_locker)
                        {
                            _priceCaches.Add(value);
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        public bool TryGetGenericValue(string key, bool createIfNotFound, out PriceCache value)
        {
            try
            {
                if (!_spreadGenericKeyToPriceCacheMap.TryGetValue(key, out value))
                {
                    if (createIfNotFound)
                    {
                        value = new PriceCache();
                        _spreadGenericKeyToPriceCacheMap[key] = value;
                        lock (_locker)
                        {
                            _priceCaches.Add(value);
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }
    }
}