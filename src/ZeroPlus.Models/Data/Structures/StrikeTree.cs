using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Structures
{
    public class StrikeTree
    {
        private readonly ConcurrentDictionary<PutCall, OptionsHolder> _optionTypeToOptionsHolderMap = new();

        public double Strike { get; set; }

        public StrikeTree(double strike)
        {
            Strike = strike;
        }

        public void Add(Option option)
        {
            PutCall optionType = option.PutCall;

            OptionsHolder? optionsHolder = GetOptionsHolderFor(optionType);
            optionsHolder?.Add(option);
        }

        public void Clear()
        {
            foreach (var item in _optionTypeToOptionsHolderMap.Values)
            {
                item.Clear();
            }
            _optionTypeToOptionsHolderMap.Clear();
        }

        private OptionsHolder? GetOptionsHolderFor(PutCall optionType)
        {
            if (!_optionTypeToOptionsHolderMap.TryGetValue(optionType, out OptionsHolder? optionsHolder))
            {
                optionsHolder = new OptionsHolder(optionType);
                _optionTypeToOptionsHolderMap.TryAdd(optionType, optionsHolder);
            }
            return optionsHolder;
        }

        public Option? GetNextBestOption(Option option, bool matchRoot = false)
        {
            PutCall optionType = option.PutCall;

            if (_optionTypeToOptionsHolderMap.TryGetValue(optionType, out OptionsHolder? optionsHolder))
            {
                Option retOption = optionsHolder.GetNextBestOption(option, matchRoot);
                return retOption;
            }
            else
            {
                throw new ApplicationException($"Option with the same type not found in tree. Key: {option}");
            }
        }
    }
}