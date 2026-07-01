using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.DataStructure
{
    internal class OptionTree
    {
        private readonly ConcurrentDictionary<string, UnderlyingSymbolTree> _underlyingSymbolToUnderlyingTreeMap = new();

        internal void Add(Option option)
        {
            string underlyingSymbol = option.UnderlyingSymbol;

            UnderlyingSymbolTree expirationTree = GetUnderlyingTreeFor(underlyingSymbol);
            expirationTree.Add(option);
        }

        internal void Clear()
        {
            foreach (var item in _underlyingSymbolToUnderlyingTreeMap.Values)
            {
                item.Clear();
            }
            _underlyingSymbolToUnderlyingTreeMap.Clear();
        }

        private UnderlyingSymbolTree GetUnderlyingTreeFor(string underlyingSymbol)
        {
            return _underlyingSymbolToUnderlyingTreeMap.GetOrAdd(underlyingSymbol, key => new UnderlyingSymbolTree(key));
        }

        internal Option GetNextExpirationOption(Option option, PermutationDirection direction)
        {
            string underlyingSymbol = option.UnderlyingSymbol;
            if (_underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetNextExpirationOption(option, direction);
            }
            else
            {
                throw new SlimException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        internal Option GetNextStrikeOption(Option option, PermutationDirection direction)
        {
            string underlyingSymbol = option.UnderlyingSymbol;
            if (_underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetNextStrikeOption(option, direction);
            }
            else
            {
                throw new SlimException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        internal HashSet<double> GetStrikesSharingExpiration(Option option)
        {
            string underlyingSymbol = option.UnderlyingSymbol;
            if (_underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetStrikesSharingExpiration(option);
            }
            else
            {
                throw new SlimException($"Underlying symbol not found in tree. option: {option}");
            }
        }

        internal HashSet<DateTime> GetExpirationsSharingStrike(Option option)
        {
            string underlyingSymbol = option.UnderlyingSymbol;
            if (_underlyingSymbolToUnderlyingTreeMap.TryGetValue(underlyingSymbol, out UnderlyingSymbolTree underlyingSymbolTree))
            {
                return underlyingSymbolTree.GetExpirationsSharingStrike(option);
            }
            else
            {
                throw new SlimException($"Underlying symbol not found in tree. option: {option}");
            }
        }
    }
}
