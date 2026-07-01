using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Structures
{
    public class OptionTree
    {
        private readonly ConcurrentDictionary<string, UnderlyingSymbolTree> _underlyingSymbolToUnderlyingTreeMap = new();

        public void Add(Option option)
        {
            string? underlyingSymbol = option.Underlying?.Symbol;

            if (underlyingSymbol != null)
            {
                UnderlyingSymbolTree expirationTree = GetUnderlyingTreeFor(underlyingSymbol);
                expirationTree.Add(option);
            }
        }

        public void Clear()
        {
            foreach (var item in _underlyingSymbolToUnderlyingTreeMap.Values)
            {
                item.Clear();
            }
            _underlyingSymbolToUnderlyingTreeMap.Clear();
        }

        private UnderlyingSymbolTree GetUnderlyingTreeFor(string underlyingSymbol)
        {
            if (!_underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree? underlyingSymbolTree))
            {
                underlyingSymbolTree = new UnderlyingSymbolTree(underlyingSymbol);
                _underlyingSymbolToUnderlyingTreeMap.TryAdd(underlyingSymbol, underlyingSymbolTree);
            }
            return underlyingSymbolTree;
        }

        public Option? GetNextExpirationOption(Option option, PermutationDirection direction)
        {
            string? underlyingSymbol = option.Underlying?.Symbol;
            if (underlyingSymbol != null && _underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree? underlyingSymbolTree))
            {
                return underlyingSymbolTree?.GetNextExpirationOption(option, direction);
            }
            else
            {
                throw new ApplicationException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        public Option? GetNextStrikeOption(Option? option, PermutationDirection direction)
        {
            if (option == null)
            {
                return null;
            }

            string? underlyingSymbol = option?.Underlying?.Symbol;
            if (underlyingSymbol != null && _underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree? underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetNextStrikeOption(option, direction);
            }
            else
            {
                throw new ApplicationException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        public HashSet<double> GetStrikesSharingExpiration(Option option)
        {
            string? underlyingSymbol = option.Underlying?.Symbol;
            if (underlyingSymbol != null && _underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree? underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetStrikesSharingExpiration(option);
            }
            else
            {
                throw new ApplicationException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        public HashSet<DateTime> GetExpirationsSharingStrike(Option option)
        {
            string? underlyingSymbol = option.Underlying?.Symbol;
            if (underlyingSymbol != null && _underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree? underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetExpirationsSharingStrike(option);
            }
            else
            {
                throw new ApplicationException($"Underlying symbol not found in tree. option: {option}");
            }
        }
    }
}
