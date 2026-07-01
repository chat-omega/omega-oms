using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Structures
{
    public class UnderlyingSymbolTree
    {
        private readonly SortedList<DateTime, ExpirationTree> _expirationDateToExpirationTreeMap = new();

        public string UnderlyingSymbol { get; }

        public UnderlyingSymbolTree(string underlyingSymbol)
        {
            UnderlyingSymbol = underlyingSymbol;
        }

        public void Add(Option option)
        {
            DateTime expirationDateOnly = new(option.Expiration.Year, option.Expiration.Month, option.Expiration.Day);

            ExpirationTree expirationTree = GetExpirationTreeFor(expirationDateOnly);
            expirationTree.Add(option);
        }

        public void Clear()
        {
            foreach (var item in _expirationDateToExpirationTreeMap.Values)
            {
                item.Clear();
            }
            _expirationDateToExpirationTreeMap.Clear();
        }

        private ExpirationTree GetExpirationTreeFor(DateTime expirationDateOnly)
        {
            if (!_expirationDateToExpirationTreeMap.TryGetValue(expirationDateOnly, out var expirationTree))
            {
                expirationTree = new ExpirationTree(expirationDateOnly);
                _expirationDateToExpirationTreeMap.TryAdd(expirationDateOnly, expirationTree);
            }
            return expirationTree;
        }

        public Option? GetNextExpirationOption(Option option, PermutationDirection direction)
        {
            DateTime expirationDateOnly = new(option.Expiration.Year, option.Expiration.Month, option.Expiration.Day);

            int sourceIndex = _expirationDateToExpirationTreeMap.IndexOfKey(expirationDateOnly);
            if (sourceIndex == -1)
            {
                throw new ApplicationException($"Expiration not found in option tree, {expirationDateOnly:MMM-dd-yy}");
            }

            try
            {
                int startIndex;
                ExpirationTree? expirationTree;
                switch (direction)
                {
                    case PermutationDirection.SameLevel:
                        startIndex = sourceIndex;
                        expirationTree = _expirationDateToExpirationTreeMap.ElementAt(startIndex).Value;
                        if (expirationTree.ContainsStrike(option.Strike))
                        {
                            return expirationTree.GetClosestStrike(option);
                        }
                        break;
                    case PermutationDirection.Down:
                        startIndex = sourceIndex - 1;
                        expirationTree = _expirationDateToExpirationTreeMap.ElementAt(startIndex).Value;
                        return expirationTree.GetClosestStrike(option);
                    case PermutationDirection.Up:
                        startIndex = sourceIndex + 1;
                        expirationTree = _expirationDateToExpirationTreeMap.ElementAt(startIndex).Value;
                        return expirationTree.GetClosestStrike(option);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            throw new ApplicationException($"No next best expiration found for option: {option}");
        }

        public Option? GetNextStrikeOption(Option? option, PermutationDirection direction)
        {
            if (option == null)
            {
                return null;
            }

            DateTime expirationDateOnly = new(option.Expiration.Year, option.Expiration.Month, option.Expiration.Day);

            int sourceIndex = _expirationDateToExpirationTreeMap.IndexOfKey(expirationDateOnly);
            if (sourceIndex == -1)
            {
                throw new ApplicationException($"Expiration not found in option tree {expirationDateOnly:MMM-dd-yy}");
            }

            ExpirationTree expirationTree = _expirationDateToExpirationTreeMap.ElementAt(sourceIndex).Value;
            return direction switch
            {
                PermutationDirection.SameLevel => expirationTree.GetNextStrikeOption(option, PermutationDirection.SameLevel),
                PermutationDirection.Down => expirationTree.GetNextStrikeOption(option, PermutationDirection.Down),
                PermutationDirection.Up => expirationTree.GetNextStrikeOption(option, PermutationDirection.Up),
                _ => throw new ApplicationException($"No next best strike found for option: {option}"),
            };
        }

        public HashSet<double> GetStrikesSharingExpiration(Option option)
        {
            DateTime expirationDateOnly = new(option.Expiration.Year, option.Expiration.Month, option.Expiration.Day);

            int sourceIndex = _expirationDateToExpirationTreeMap.IndexOfKey(expirationDateOnly);
            if (sourceIndex == -1)
            {
                throw new ApplicationException($"Expiration not found in option tree {expirationDateOnly:MMM-dd-yy}");
            }

            ExpirationTree expirationTree = _expirationDateToExpirationTreeMap.ElementAt(sourceIndex).Value;
            return expirationTree.Strikes;
        }

        public HashSet<DateTime> GetExpirationsSharingStrike(Option option)
        {
            HashSet<DateTime> expirations = new();
            foreach (KeyValuePair<DateTime, ExpirationTree> kvp in _expirationDateToExpirationTreeMap)
            {
                if (kvp.Value.Strikes.Contains(option.Strike))
                {
                    expirations.Add(kvp.Key);
                }
            }
            return expirations;
        }
    }
}
