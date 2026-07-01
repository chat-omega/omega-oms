using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Structures;
using ZeroPlus.Models.Extensions;
using OptionStrategySpreadLeg = ZeroPlus.Models.Utils.OptionStrategy.SpreadLeg;
using OptionStrategySecurity = ZeroPlus.Models.Utils.OptionStrategy.Security;

namespace ZeroPlus.Models.Utils
{
    public static class PermutationEngine
    {
        public static List<string> WalkNextOptionPerms(
            UnderlyingSymbolTree tree,
            Option start,
            PermMode mode,
            int count,
            CancellationToken cancellationToken = default)
        {
            List<string> results = new(Math.Max(0, count));
            if (tree == null || start == null || count <= 0)
            {
                return results;
            }

            Option current = start;
            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Option? next = TryStep(tree, current, mode);
                if (next == null)
                {
                    break;
                }

                results.Add(next.Symbol);
                current = next;
            }
            return results;
        }

        public static List<PermSpreadResult> WalkNextSpreadPerms(
            UnderlyingSymbolTree tree,
            IReadOnlyList<PermLegRequest> originalLegs,
            List<Option> startState,
            PermMode mode,
            PermSide permSide,
            int count,
            BaseStrategy baseStrategy,
            bool maintainBaseStrategy,
            bool maintainBaseStrategyFlyException,
            bool skipCheck,
            CancellationToken cancellationToken = default)
        {
            if (tree == null || originalLegs == null || originalLegs.Count == 0 || startState == null || startState.Count == 0 || count <= 0)
            {
                return new List<PermSpreadResult>();
            }

            if (permSide == PermSide.Alternate)
            {
                List<PermSpreadResult> union = new();
                HashSet<string> seen = new(StringComparer.Ordinal);
                PermSide[] fanout = new[] { PermSide.All, PermSide.Low, PermSide.High };
                for (int s = 0; s < fanout.Length; s++)
                {
                    List<PermSpreadResult> partial = WalkNextSpreadPerms(
                        tree, originalLegs, startState, mode, fanout[s], count, baseStrategy,
                        maintainBaseStrategy, maintainBaseStrategyFlyException, skipCheck, cancellationToken);
                    for (int j = 0; j < partial.Count; j++)
                    {
                        PermSpreadResult p = partial[j];
                        string key = string.Join("|", p.Legs.Select(l => l.Symbol));
                        if (seen.Add(key))
                        {
                            union.Add(p);
                        }
                    }
                }
                return union;
            }

            List<Option> previous = startState;
            List<PermSpreadResult> results = new(count);

            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                List<Option>? candidate;
                if (skipCheck)
                {
                    candidate = ApplyBump(tree, previous, mode, permSide, baseStrategy, maintainBaseStrategy);
                }
                else if (IsPermValid(tree, previous, mode, permSide, baseStrategy, maintainBaseStrategy, originalLegs))
                {
                    candidate = ApplyBump(tree, previous, mode, permSide, baseStrategy, maintainBaseStrategy);
                }
                else if (maintainBaseStrategy && baseStrategy.IsCalendar()
                         && (mode == PermMode.ExpirationUp || mode == PermMode.ExpirationDown))
                {
                    candidate = TryCalendarFallback(tree, previous, mode, permSide, baseStrategy, maintainBaseStrategy, originalLegs);
                }
                else
                {
                    candidate = null;
                }

                if (candidate == null)
                {
                    break;
                }

                bool drift = false;
                if (maintainBaseStrategy)
                {
                    BaseStrategy candidateBs = EvaluateBaseStrategy(candidate, originalLegs);
                    bool bothFly = maintainBaseStrategyFlyException && baseStrategy.IsAnyFly() && candidateBs.IsAnyFly();
                    if (baseStrategy != candidateBs && !bothFly)
                    {
                        drift = true;
                    }
                }

                if (!drift)
                {
                    results.Add(SnapshotResult(candidate, originalLegs));
                }
                previous = candidate;
            }

            return results;
        }

        public static Option? TryStep(UnderlyingSymbolTree tree, Option current, PermMode mode)
        {
            try
            {
                PermutationDirection direction = GetDirection(mode);
                return mode switch
                {
                    PermMode.StrikeUp or PermMode.StrikeDown => tree.GetNextStrikeOption(current, direction),
                    PermMode.ExpirationUp or PermMode.ExpirationDown => tree.GetNextExpirationOption(current, direction),
                    _ => null,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<Option>? ApplyBump(
            UnderlyingSymbolTree tree,
            List<Option> state,
            PermMode mode,
            PermSide permSide,
            BaseStrategy baseStrategy,
            bool maintainBaseStrategy)
        {
            PermutationDirection direction = GetDirection(mode);
            List<Option> candidate = new(state);
            List<int> targets = SelectLegs(state, permSide);
            if (targets.Count == 0)
            {
                return null;
            }

            bool isCalendarMaintain = maintainBaseStrategy && baseStrategy.IsCalendar();

            int errorCount = 0;
            switch (mode)
            {
                case PermMode.StrikeUp:
                case PermMode.StrikeDown:
                    if (isCalendarMaintain)
                    {
                        HashSet<double>? intersection = TryStrikeIntersection(tree, targets, state);
                        if (intersection == null || intersection.Count == 0)
                        {
                            return null;
                        }
                        double currentStrike = targets.Min(idx => state[idx].Strike);
                        double? nextStrike = mode == PermMode.StrikeUp
                            ? intersection.Where(x => x > currentStrike).OrderBy(x => x).Cast<double?>().FirstOrDefault()
                            : intersection.Where(x => x < currentStrike).OrderByDescending(x => x).Cast<double?>().FirstOrDefault();
                        if (nextStrike == null)
                        {
                            return null;
                        }
                        foreach (int idx in targets)
                        {
                            Option? bumped = FindOption(tree, state[idx], strike: nextStrike.Value);
                            if (bumped == null)
                            {
                                return null;
                            }
                            candidate[idx] = bumped;
                        }
                    }
                    else
                    {
                        foreach (int idx in targets)
                        {
                            Option? bumped = TryStep(tree, state[idx], mode);
                            if (bumped == null)
                            {
                                errorCount++;
                                continue;
                            }
                            candidate[idx] = bumped;
                        }
                        if (errorCount == targets.Count)
                        {
                            return null;
                        }
                    }
                    break;
                case PermMode.ExpirationUp:
                case PermMode.ExpirationDown:
                    if (isCalendarMaintain)
                    {
                        foreach (int idx in targets)
                        {
                            Option curr = state[idx];
                            HashSet<DateTime> shared = SafeGetExpirationsSharingStrike(tree, curr);
                            if (shared.Count == 0)
                            {
                                errorCount++;
                                continue;
                            }
                            DateTime? nextExp = mode == PermMode.ExpirationUp
                                ? shared.Where(x => x > curr.Expiration.Date).OrderBy(x => x).Cast<DateTime?>().FirstOrDefault()
                                : shared.Where(x => x < curr.Expiration.Date).OrderByDescending(x => x).Cast<DateTime?>().FirstOrDefault();
                            if (nextExp == null)
                            {
                                errorCount++;
                                continue;
                            }
                            Option? bumped = FindOption(tree, curr, expiration: nextExp.Value);
                            if (bumped == null)
                            {
                                errorCount++;
                                continue;
                            }
                            candidate[idx] = bumped;
                        }
                        if (errorCount == targets.Count)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        foreach (int idx in targets)
                        {
                            Option? bumped = TryStep(tree, state[idx], mode);
                            if (bumped == null)
                            {
                                errorCount++;
                                continue;
                            }
                            candidate[idx] = bumped;
                        }
                        if (errorCount == targets.Count)
                        {
                            return null;
                        }
                    }
                    break;
                default:
                    return null;
            }

            bool changed = false;
            for (int i = 0; i < state.Count; i++)
            {
                if (!string.Equals(state[i].Symbol, candidate[i].Symbol, StringComparison.Ordinal))
                {
                    changed = true;
                    break;
                }
            }
            return changed ? candidate : null;
        }

        public static List<int> SelectLegs(List<Option> state, PermSide permSide)
        {
            List<int> indices = new();
            switch (permSide)
            {
                case PermSide.All:
                case PermSide.Alternate:
                    for (int i = 0; i < state.Count; i++)
                    {
                        if (state[i] != null && state[i].SecurityType == SecurityType.Option)
                        {
                            indices.Add(i);
                        }
                    }
                    break;
                case PermSide.Low:
                    int low = -1;
                    for (int i = 0; i < state.Count; i++)
                    {
                        if (state[i].SecurityType != SecurityType.Option) continue;
                        if (low == -1 || state[i].Strike < state[low].Strike) low = i;
                    }
                    if (low != -1) indices.Add(low);
                    break;
                case PermSide.High:
                    int high = -1;
                    for (int i = 0; i < state.Count; i++)
                    {
                        if (state[i].SecurityType != SecurityType.Option) continue;
                        if (high == -1 || state[i].Strike > state[high].Strike) high = i;
                    }
                    if (high != -1) indices.Add(high);
                    break;
            }
            return indices;
        }

        public static bool IsPermValid(
            UnderlyingSymbolTree tree,
            List<Option> state,
            PermMode mode,
            PermSide permSide,
            BaseStrategy baseStrategy,
            bool maintainBaseStrategy,
            IReadOnlyList<PermLegRequest> originalLegs)
        {
            try
            {
                List<Option>? candidate = ApplyBump(tree, state, mode, permSide, baseStrategy, maintainBaseStrategy);
                if (candidate == null)
                {
                    return false;
                }

                string currentDesc = EvaluateDescription(state, originalLegs);
                string candidateDesc = EvaluateDescription(candidate, originalLegs);
                return candidateDesc != "INVALID" && candidateDesc != currentDesc;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<Option>? TryCalendarFallback(
            UnderlyingSymbolTree tree,
            List<Option> state,
            PermMode mode,
            PermSide permSide,
            BaseStrategy baseStrategy,
            bool maintainBaseStrategy,
            IReadOnlyList<PermLegRequest> originalLegs)
        {
            PermMode primaryStrike = mode == PermMode.ExpirationUp ? PermMode.StrikeUp : PermMode.StrikeDown;
            PermMode oppositeStrike = mode == PermMode.ExpirationUp ? PermMode.StrikeDown : PermMode.StrikeUp;

            List<Option>? attempt = TryCalendarFallbackBranch(tree, state, primaryStrike, mode, permSide, baseStrategy, maintainBaseStrategy, originalLegs);
            if (attempt != null)
            {
                return attempt;
            }

            return TryCalendarFallbackBranch(tree, state, oppositeStrike, mode, permSide, baseStrategy, maintainBaseStrategy, originalLegs);
        }

        private static List<Option>? TryCalendarFallbackBranch(
            UnderlyingSymbolTree tree,
            List<Option> state,
            PermMode strikeMode,
            PermMode expirationMode,
            PermSide permSide,
            BaseStrategy baseStrategy,
            bool maintainBaseStrategy,
            IReadOnlyList<PermLegRequest> originalLegs)
        {
            if (!IsPermValid(tree, state, strikeMode, permSide, baseStrategy, maintainBaseStrategy, originalLegs))
            {
                return null;
            }
            List<Option>? afterStrike = ApplyBump(tree, state, strikeMode, permSide, baseStrategy, maintainBaseStrategy);
            if (afterStrike == null)
            {
                return null;
            }
            if (!IsPermValid(tree, afterStrike, expirationMode, permSide, baseStrategy, maintainBaseStrategy, originalLegs))
            {
                return null;
            }
            return ApplyBump(tree, afterStrike, expirationMode, permSide, baseStrategy, maintainBaseStrategy);
        }

        public static string EvaluateDescription(List<Option> state, IReadOnlyList<PermLegRequest> originalLegs)
        {
            List<OptionStrategySpreadLeg> spreadLegs = BuildSpreadLegs(state, originalLegs);
            OptionStrategy.EvaluateLegs(spreadLegs, out _, out _, out string description);
            return description ?? string.Empty;
        }

        public static BaseStrategy EvaluateBaseStrategy(List<Option> state, IReadOnlyList<PermLegRequest> originalLegs)
        {
            List<OptionStrategySpreadLeg> spreadLegs = BuildSpreadLegs(state, originalLegs);
            OptionStrategy.EvaluateLegs(spreadLegs, out string baseTypeStr, out _, out _);
            return OptionStrategy.ConvertFromString(baseTypeStr ?? string.Empty);
        }

        private static List<OptionStrategySpreadLeg> BuildSpreadLegs(List<Option> state, IReadOnlyList<PermLegRequest> originalLegs)
        {
            List<OptionStrategySpreadLeg> result = new(state.Count);
            for (int i = 0; i < state.Count; i++)
            {
                Option o = state[i];
                Side? side = i < originalLegs.Count ? originalLegs[i].Side : null;
                int ratio = i < originalLegs.Count ? originalLegs[i].Ratio : 1;
                result.Add(new OptionStrategySpreadLeg
                {
                    Security = new OptionStrategySecurity
                    {
                        Symbol = o.Symbol,
                        UnderlyingSymbol = o.Underlying?.Symbol ?? string.Empty,
                        RootSymbol = o.RootSymbol ?? string.Empty,
                        Expiration = o.Expiration,
                        Type = o.PutCall,
                        Strike = o.Strike,
                        SecurityType = Data.Enums.SecurityType.Option,
                        Multiplier = 100,
                    },
                    Symbol = o.Symbol,
                    Side = side,
                    Quantity = ratio,
                });
            }
            return result;
        }

        public static Option? FindOption(UnderlyingSymbolTree tree, Option reference, double? strike = null, DateTime? expiration = null)
        {
            try
            {
                Option probe = new()
                {
                    Symbol = reference.Symbol,
                    RootSymbol = reference.RootSymbol,
                    Expiration = expiration ?? reference.Expiration,
                    PutCall = reference.PutCall,
                    Strike = strike ?? reference.Strike,
                    Underlying = reference.Underlying,
                };
                return tree.GetNextExpirationOption(probe, PermutationDirection.SameLevel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static HashSet<DateTime> SafeGetExpirationsSharingStrike(UnderlyingSymbolTree tree, Option option)
        {
            try
            {
                return tree.GetExpirationsSharingStrike(option);
            }
            catch (Exception)
            {
                return new HashSet<DateTime>();
            }
        }

        public static HashSet<double>? TryStrikeIntersection(UnderlyingSymbolTree tree, List<int> targets, List<Option> state)
        {
            HashSet<double>? acc = null;
            foreach (int idx in targets)
            {
                HashSet<double> strikes;
                try
                {
                    strikes = tree.GetStrikesSharingExpiration(state[idx]);
                }
                catch (Exception)
                {
                    return null;
                }
                if (acc == null)
                {
                    acc = new HashSet<double>(strikes);
                }
                else
                {
                    acc.IntersectWith(strikes);
                }
            }
            return acc;
        }

        public static PermSpreadResult SnapshotResult(List<Option> state, IReadOnlyList<PermLegRequest> originalLegs)
        {
            List<PermLegResult> legs = new(state.Count);
            for (int i = 0; i < state.Count; i++)
            {
                Option o = state[i];
                Side side = i < originalLegs.Count ? originalLegs[i].Side : Side.Buy;
                int ratio = i < originalLegs.Count ? originalLegs[i].Ratio : 1;
                legs.Add(new PermLegResult
                {
                    Symbol = o.Symbol,
                    Side = side,
                    Ratio = ratio,
                    Strike = o.Strike,
                    Expiration = o.Expiration,
                    PutCall = o.PutCall,
                });
            }
            return new PermSpreadResult { Legs = legs };
        }

        public static PermutationDirection GetDirection(PermMode mode)
        {
            return mode switch
            {
                PermMode.StrikeUp or PermMode.ExpirationUp => PermutationDirection.Up,
                PermMode.StrikeDown or PermMode.ExpirationDown => PermutationDirection.Down,
                _ => PermutationDirection.SameLevel,
            };
        }

        public static UnderlyingSymbolTree BuildTree(string underlying, IEnumerable<Option> chain)
        {
            UnderlyingSymbolTree tree = new(underlying);
            foreach (Option o in chain)
            {
                tree.Add(o);
            }
            return tree;
        }
    }
}
