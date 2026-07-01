using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Structures
{
    public class OptionsLookup
    {
        private readonly ILogger<OptionsLookup> _logger;
        private readonly ConcurrentDictionary<string, List<Option>> _symbolToOptionsListMap = new();
        private readonly OptionTree _optionTree = new();

        public OptionsLookup(ILogger<OptionsLookup> logger)
        {
            _logger = logger;
        }

        public bool Contains(string symbol)
        {
            return _symbolToOptionsListMap.ContainsKey(symbol);
        }

        public List<Option>? GetAllOptions(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                _logger.LogError($"Symbol: can not be empty.");
                return new List<Option>();
            }
            if (!_symbolToOptionsListMap.TryGetValue(symbol, out List<Option>? options))
            {
                _logger.LogError($"Symbol: {symbol} not found in options lookup.");
                return new List<Option>();
            }
            return options;
        }

        public List<Option>? GetOptionsWithExpiration(string symbol, DateTime expiration)
        {
            if (_symbolToOptionsListMap.TryGetValue(symbol, out List<Option>? options))
            {
                IEnumerable<Option> optionsWithExpiration = options.Where(o => o.Expiration.Date == expiration.Date);
                return optionsWithExpiration.ToList();
            }
            else
            {
                throw new ApplicationException($"Symbol: {symbol} not found in options lookup.");
            }
        }

        public Option? GetNextExpiration(Option option, PermutationDirection direction)
        {
            return _optionTree?.GetNextExpirationOption(option, direction);
        }

        public Option? GetNextStrike(Option option, PermutationDirection direction)
        {
            return _optionTree?.GetNextStrikeOption(option, direction);
        }

        public HashSet<double> GetStrikesSharingExpiration(Option option)
        {
            return _optionTree.GetStrikesSharingExpiration(option);
        }

        public HashSet<DateTime> GetExpirationsSharingStrike(Option option)
        {
            return _optionTree.GetExpirationsSharingStrike(option);
        }

        public void AddOptions(string symbol, List<Option>? options)
        {
            if (!string.IsNullOrWhiteSpace(symbol) && options is { Count: > 0 })
            {
                _symbolToOptionsListMap.TryAdd(symbol.ToUpper(), options);
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"{nameof(AddOptions)} -> Adding {options.Count} options to {symbol} lookup.");
                }

                AddOptionsToOptionTree(options);
            }
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
                _logger.LogError(ex, nameof(Clear));
            }
        }

        private void AddOptionsToOptionTree(List<Option> options)
        {
            foreach (Option option in options)
            {
                _optionTree.Add(option);
            }
        }
    }
}