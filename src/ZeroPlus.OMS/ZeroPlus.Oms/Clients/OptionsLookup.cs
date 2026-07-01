using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.DataStructure;

namespace ZeroPlus.Oms.Clients
{
    public class OptionsLookup
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, List<Option>> _symbolToOptionsListMap = new();
        private readonly OptionTree _optionTree = new();
        public bool Contains(string symbol)
        {
            return _symbolToOptionsListMap.ContainsKey(symbol);
        }

        public List<Option> GetAllOptions(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                _log.Error($"Symbol: can not be empty.");
                return new List<Option>();
            }
            if (!_symbolToOptionsListMap.TryGetValue(symbol, out List<Option> options))
            {
                _log.Error($"Symbol: {symbol} not found in options lookup.");
                return new List<Option>();
            }
            return options;
        }

        public List<Option> GetOptionsWithExpiration(string symbol, DateTime expiration)
        {
            if (_symbolToOptionsListMap.TryGetValue(symbol, out List<Option> options))
            {
                IEnumerable<Option> optionsWithExpiration = options.Where(o => o.Expiration.Date == expiration.Date);
                return optionsWithExpiration.ToList();
            }
            throw new SlimException($"Symbol: {symbol} not found in options lookup.");
        }

        internal Option GetNextExpiration(Option option, PermutationDirection direction)
        {
            return _optionTree.GetNextExpirationOption(option, direction);
        }

        internal Option GetNextStrike(Option option, PermutationDirection direction)
        {
            return _optionTree.GetNextStrikeOption(option, direction);
        }

        internal HashSet<double> GetStrikesSharingExpiration(Option option)
        {
            return _optionTree.GetStrikesSharingExpiration(option);
        }

        internal HashSet<DateTime> GetExpirationsSharingStrike(Option option)
        {
            return _optionTree.GetExpirationsSharingStrike(option);
        }

        public void AddOptions(string symbol, List<Option> options, bool forceUpdate = false)
        {
            if (!string.IsNullOrWhiteSpace(symbol) && options != null && options.Count > 0)
            {
                if (forceUpdate)
                {
                    _symbolToOptionsListMap[symbol.ToUpper()] = options;
                }
                else
                {
                    _symbolToOptionsListMap.TryAdd(symbol.ToUpper(), options);
                }

                if (_log.IsTraceEnabled)
                {
                    _log.Trace($"{nameof(AddOptions)} -> Adding {options.Count} options to {symbol} lookup. ForceUpdate: {forceUpdate}");
                }
            }

            AddOptionsToOptionTree(options);
        }

        public void Clear()
        {
            try
            {
                _symbolToOptionsListMap.Clear();
                _optionTree.Clear();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clear));
            }
        }

        private void AddOptionsToOptionTree(List<Option> options)
        {
            if (options == null)
            {
                return;
            }
            foreach (Option option in options)
            {
                _optionTree.Add(option);
            }
        }
    }
}