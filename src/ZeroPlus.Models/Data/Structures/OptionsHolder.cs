using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Structures
{
    public class OptionsHolder
    {
        private readonly HashSet<Option> _options = new();

        public PutCall OptionType { get; }

        public OptionsHolder(PutCall optionType)
        {
            OptionType = optionType;
        }

        public void Add(Option option)
        {
            _options.Add(option);
        }

        public void Clear()
        {
            _options.Clear();
        }

        public Option GetNextBestOption(Option option, bool matchRoot)
        {
            Option? nextBestOption = matchRoot ?
                _options.Where(x => x.RootSymbol == option.RootSymbol).OrderBy(x => x.Expiration).FirstOrDefault() :
                _options.OrderBy(x => x.Expiration).FirstOrDefault();

            return nextBestOption ?? throw new ApplicationException($"No next best option found for option: {option}");
        }
    }
}