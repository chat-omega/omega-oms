using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.Generators
{
    internal class Interpolator : IOmsDataSubscriber
    {
        private readonly ConcurrentDictionary<string, Option> _symbolToOptionMap = new();
        private readonly ConcurrentDictionary<Option, double> _optionToValueMap = new();
        private readonly HashSet<string> _notify = new();
        public string UnderlyingSymbol { get; }
        public DateTime Expiration { get; }
        public OptionType OptionType { get; }
        public SubscriptionFieldType QuoteType { get; }
        public SubscriptionProvider SubscriptionProvider { get; internal set; }

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public bool IsDisposed { get; set; }

        public Interpolator(string underlyingSymbol, DateTime expiration, OptionType optionType, SubscriptionFieldType quoteType)
        {
            UnderlyingSymbol = underlyingSymbol;
            Expiration = expiration;
            OptionType = optionType;
            QuoteType = quoteType;
        }

        public async void AddSubscription(Option limitOption)
        {
            _notify.Add(limitOption.OptionSymbol);

            IEnumerable<Option> options = (await OmsCore.QuoteClient.GetSymbols(UnderlyingSymbol)).Where(x => x.Expiration == Expiration && x.Type == OptionType);

            if ((OptionType == OptionType.PUT && QuoteType == SubscriptionFieldType.BidInterpolated) ||
                (OptionType == OptionType.CALL && QuoteType == SubscriptionFieldType.AskInterpolated))
            {
                options = options.Where(x => x.Strike <= limitOption.Strike).OrderBy(x => x.Strike).ToList();
            }
            else if ((OptionType == OptionType.PUT && QuoteType == SubscriptionFieldType.AskInterpolated) ||
                     (OptionType == OptionType.CALL && QuoteType == SubscriptionFieldType.BidInterpolated))
            {
                options = options.Where(x => x.Strike >= limitOption.Strike).OrderBy(x => x.Strike).ToList();
            }

            foreach (Option option in options)
            {
                if (!_symbolToOptionMap.ContainsKey(option.OptionSymbol))
                {
                    _symbolToOptionMap[option.OptionSymbol] = option;
                    _optionToValueMap[option] = double.NaN;
                    if (QuoteType == SubscriptionFieldType.BidInterpolated)
                    {
                        OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Bid, this);
                    }
                    else if (QuoteType == SubscriptionFieldType.AskInterpolated)
                    {
                        OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Ask, this);
                    }
                }
            }

            await InterpolateAsync(limitOption);
        }

        public async Task RemoveSubscriptionAsync(Option subscribedOption)
        {
            _notify.Remove(subscribedOption.OptionSymbol);
            if (IsEmpty())
            {
                List<Option> options = (await OmsCore.QuoteClient.GetSymbols(UnderlyingSymbol)).Where(x => x.Expiration == Expiration && x.Type == OptionType).OrderBy(x => x.Strike).ToList();

                foreach (Option option in options)
                {
                    _symbolToOptionMap[option.OptionSymbol] = option;
                    _optionToValueMap[option] = double.NaN;
                    if (QuoteType == SubscriptionFieldType.BidInterpolated)
                    {
                        await OmsCore.QuoteClient.UnsubscribeAsync(option.OptionSymbol, SubscriptionFieldType.Bid, this);
                    }
                    else if (QuoteType == SubscriptionFieldType.AskInterpolated)
                    {
                        await OmsCore.QuoteClient.UnsubscribeAsync(option.OptionSymbol, SubscriptionFieldType.Ask, this);
                    }
                }
            }
        }

        public bool IsEmpty()
        {
            return _notify.Count == 0;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;

            if (value is double quote)
            {
                if (_symbolToOptionMap.TryGetValue(symbol, out Option option) &&
                    _optionToValueMap.TryGetValue(option, out double oldVal) &&
                    oldVal != quote)
                {
                    _optionToValueMap[option] = quote;
                    InterpolateAsync(option);
                }
            }
        }

        private Task InterpolateAsync(Option option)
        {
            return Task.Run(() => Interpolate(option));
        }

        private void Interpolate(Option limitOption)
        {
            if (OptionType == OptionType.PUT && QuoteType == SubscriptionFieldType.BidInterpolated)
            {
                double highest = double.NaN;
                List<Option> options = _optionToValueMap.Keys.Where(x => x.Strike <= limitOption.Strike).OrderBy(x => x.Strike).ToList();
                foreach (Option option in options)
                {
                    double cached = _optionToValueMap[option];
                    if (!double.IsNaN(cached))
                    {
                        if (double.IsNaN(highest) || cached > highest)
                        {
                            highest = cached;
                        }
                        if (_notify.Contains(option.OptionSymbol))
                        {
                            double update = Math.Round(highest, 2, MidpointRounding.AwayFromZero);
                            SubscriptionProvider.Update(option.OptionSymbol, QuoteType, update);
                        }
                    }
                }
            }
            else if (OptionType == OptionType.PUT && QuoteType == SubscriptionFieldType.AskInterpolated)
            {
                double lowest = double.NaN;
                List<Option> options = _optionToValueMap.Keys.Where(x => x.Strike >= limitOption.Strike).OrderByDescending(x => x.Strike).ToList();
                foreach (Option option in options)
                {
                    double cached = _optionToValueMap[option];
                    if (!double.IsNaN(cached))
                    {
                        if (double.IsNaN(lowest) || cached < lowest)
                        {
                            lowest = cached;
                        }
                        if (_notify.Contains(option.OptionSymbol))
                        {
                            double update = Math.Round(lowest, 2, MidpointRounding.AwayFromZero);
                            SubscriptionProvider.Update(option.OptionSymbol, QuoteType, update);
                        }
                    }
                }
            }
            else if (OptionType == OptionType.CALL && QuoteType == SubscriptionFieldType.BidInterpolated)
            {
                double highest = double.NaN;
                List<Option> options = _optionToValueMap.Keys.Where(x => x.Strike >= limitOption.Strike).OrderByDescending(x => x.Strike).ToList();
                foreach (Option option in options)
                {
                    double cached = _optionToValueMap[option];
                    if (!double.IsNaN(cached))
                    {
                        if (double.IsNaN(highest) || cached > highest)
                        {
                            highest = cached;
                        }
                        if (_notify.Contains(option.OptionSymbol))
                        {
                            double update = Math.Round(highest, 2, MidpointRounding.AwayFromZero);
                            SubscriptionProvider.Update(option.OptionSymbol, QuoteType, update);
                        }
                    }
                }
            }
            else if (OptionType == OptionType.CALL && QuoteType == SubscriptionFieldType.AskInterpolated)
            {
                double lowest = double.NaN;
                List<Option> options = _optionToValueMap.Keys.Where(x => x.Strike <= limitOption.Strike).OrderBy(x => x.Strike).ToList();
                foreach (Option option in options)
                {
                    double cached = _optionToValueMap[option];
                    if (!double.IsNaN(cached))
                    {
                        if (double.IsNaN(lowest) || cached < lowest)
                        {
                            lowest = cached;
                        }
                        if (_notify.Contains(option.OptionSymbol))
                        {
                            double update = Math.Round(lowest, 2, MidpointRounding.AwayFromZero);
                            SubscriptionProvider.Update(option.OptionSymbol, QuoteType, update);
                        }
                    }
                }
            }
        }
    }
}