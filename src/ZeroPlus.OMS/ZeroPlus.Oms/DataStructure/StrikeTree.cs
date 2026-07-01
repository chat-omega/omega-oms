using System.Collections.Concurrent;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.DataStructure
{
    internal class StrikeTree
    {
        private readonly ConcurrentDictionary<OptionType, OptionsHolder> _optionTypeToOptionsHolderMap = new();

        public double Strike { get; set; }

        public StrikeTree(double strike)
        {
            Strike = strike;
        }

        internal void Add(Option option)
        {
            OptionType optionType = option.Type;

            OptionsHolder optionsHolder = GetOptionsHolderFor(optionType);
            optionsHolder.Add(option);
        }

        public void Clear()
        {
            foreach (var item in _optionTypeToOptionsHolderMap.Values)
            {
                item.Clear();
            }
            _optionTypeToOptionsHolderMap.Clear();
        }

        private OptionsHolder GetOptionsHolderFor(OptionType optionType)
        {
            if (!_optionTypeToOptionsHolderMap.TryGetValue(optionType, out OptionsHolder optionsHolder))
            {
                optionsHolder = new OptionsHolder(optionType);
                _optionTypeToOptionsHolderMap.TryAdd(optionType, optionsHolder);
            }
            return optionsHolder;
        }

        internal Option GetNextBestOption(Option option, bool matchRoot = false)
        {
            OptionType optionType = option.Type;

            if (_optionTypeToOptionsHolderMap.TryGetValue(optionType, out OptionsHolder optionsHolder))
            {
                Option retOption = optionsHolder.GetNextBestOption(option, matchRoot);
                return retOption;
            }
            else
            {
                throw new SlimException($"Option with the same type not found in tree. Key: {option}");
            }
        }
    }
}