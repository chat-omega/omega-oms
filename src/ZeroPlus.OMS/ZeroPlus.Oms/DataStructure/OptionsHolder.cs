using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.DataStructure
{
    internal class OptionsHolder
    {
        private readonly HashSet<Option> _options = new();

        public OptionType OptionType { get; }

        public OptionsHolder(OptionType optionType)
        {
            OptionType = optionType;
        }

        internal void Add(Option option)
        {
            _options.Add(option);
        }

        public void Clear()
        {
            _options.Clear();
        }

        internal Option GetNextBestOption(Option option, bool matchRoot)
        {
            Option nextBestOption = matchRoot ?
                _options.Where(x => x.RootSymbol == option.RootSymbol).OrderBy(x => x.Expiration).FirstOrDefault() :
                _options.OrderBy(x => x.Expiration).FirstOrDefault();

            return nextBestOption ?? throw new SlimException($"No next best option found for option: {option}");
        }
    }
}