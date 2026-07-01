using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.DataStructure
{
    internal class ExpirationTree
    {
        private readonly SortedList<double, StrikeTree> _strikeToStrikeTreeMap = new();
        public HashSet<double> Strikes { get; } = new();

        public DateTime ExpirationDate { get; set; }

        public ExpirationTree(DateTime expirationDate)
        {
            ExpirationDate = expirationDate;
        }

        internal void Add(Option option)
        {
            double strike = option.Strike;
            Strikes.Add(strike);
            StrikeTree strikeTree = GetStrikeTreeFor(strike);
            strikeTree.Add(option);
        }

        public void Clear()
        {
            foreach (var item in _strikeToStrikeTreeMap.Values)
            {
                item.Clear();
            }
            _strikeToStrikeTreeMap.Clear();
        }

        internal bool ContainsStrike(double strike)
        {
            return _strikeToStrikeTreeMap.ContainsKey(strike);
        }

        private StrikeTree GetStrikeTreeFor(double strike)
        {
            if (!_strikeToStrikeTreeMap.TryGetValue(strike, out StrikeTree strikeTree))
            {
                strikeTree = new StrikeTree(strike);
                _strikeToStrikeTreeMap.TryAdd(strike, strikeTree);
            }
            return strikeTree;
        }

        internal Option GetClosestStrike(Option option)
        {
            StrikeTree strikeTree;
            int sourceIndex = _strikeToStrikeTreeMap.IndexOfKey(option.Strike);
            if (sourceIndex >= 0)
            {
                strikeTree = _strikeToStrikeTreeMap.ElementAt(sourceIndex).Value;
                return strikeTree.GetNextBestOption(option);
            }
            else if (sourceIndex == -1)
            {
                int prevStrikeIndex = -1;
                for (int i = 0; i < _strikeToStrikeTreeMap.Count; i++)
                {
                    double strike = _strikeToStrikeTreeMap.ElementAt(i).Key;
                    if (strike > option.Strike)
                    {
                        int nextStrikeIndex = i;

                        if (nextStrikeIndex == 0)
                        {
                            strikeTree = _strikeToStrikeTreeMap.ElementAt(nextStrikeIndex).Value;
                            return strikeTree.GetNextBestOption(option);
                        }

                        double diffWithNext = _strikeToStrikeTreeMap.ElementAt(nextStrikeIndex).Key - option.Strike;
                        double diffWithPrev = option.Strike - _strikeToStrikeTreeMap.ElementAt(prevStrikeIndex).Key;

                        strikeTree = diffWithNext < diffWithPrev
                            ? _strikeToStrikeTreeMap.ElementAt(nextStrikeIndex).Value
                            : _strikeToStrikeTreeMap.ElementAt(prevStrikeIndex).Value;

                        return strikeTree.GetNextBestOption(option);
                    }
                    else
                    {
                        prevStrikeIndex = i;
                    }
                }

                if (prevStrikeIndex != -1)
                {
                    strikeTree = _strikeToStrikeTreeMap.ElementAt(prevStrikeIndex).Value;
                    return strikeTree.GetNextBestOption(option);
                }
            }

            throw new SlimException($"No next best strike found for option: {option}");
        }

        internal Option GetNextStrikeOption(Option option, PermutationDirection direction)
        {
            int sourceIndex = _strikeToStrikeTreeMap.IndexOfKey(option.Strike);

            if (sourceIndex == -1)
            {
                switch (direction)
                {
                    case PermutationDirection.SameLevel:
                    case PermutationDirection.Up:
                        for (int i = 0; i < _strikeToStrikeTreeMap.Count; i++)
                        {
                            double strike = _strikeToStrikeTreeMap.ElementAt(i).Key;
                            if (strike > option.Strike)
                            {
                                sourceIndex = direction == PermutationDirection.SameLevel || i == 0 ? i : i - 1;
                                break;
                            }
                        }
                        break;
                    case PermutationDirection.Down:
                        for (int i = _strikeToStrikeTreeMap.Count - 1; i >= 0; i--)
                        {
                            double strike = _strikeToStrikeTreeMap.ElementAt(i).Key;
                            if (strike < option.Strike)
                            {
                                sourceIndex = i + 1;
                                break;
                            }
                        }
                        break;
                }
                if (sourceIndex == -1)
                {
                    throw new SlimException($"No next best strike for option: {option}");
                }
            }

            StrikeTree strikeTree;
            int startIndex;
            switch (direction)
            {
                case PermutationDirection.SameLevel:
                    startIndex = sourceIndex;
                    strikeTree = _strikeToStrikeTreeMap.ElementAt(startIndex).Value;
                    return strikeTree.GetNextBestOption(option, matchRoot: true);
                case PermutationDirection.Down:
                    startIndex = sourceIndex - 1;
                    for (int index = startIndex; index >= 0; index--)
                    {
                        strikeTree = _strikeToStrikeTreeMap.ElementAt(index).Value;
                        try
                        {
                            return strikeTree.GetNextBestOption(option, matchRoot: true);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    break;
                case PermutationDirection.Up:
                    startIndex = sourceIndex + 1;
                    for (int index = startIndex; index < _strikeToStrikeTreeMap.Count; index++)
                    {
                        strikeTree = _strikeToStrikeTreeMap.ElementAt(index).Value;
                        try
                        {
                            return strikeTree.GetNextBestOption(option, matchRoot: true);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    break;
            }
            throw new SlimException($"No next best strike found for option: {option}");
        }
    }
}