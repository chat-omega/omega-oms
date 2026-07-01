using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Structures;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Utils
{
    public static class PermutationOrderHelper
    {
        public static async Task<PermSpreadResult?> ComputeNextSpreadAsync(
            IOrderSlim order,
            PermMode mode,
            PermSide permSide,
            Func<string, CancellationToken, Task<IEnumerable<Option>?>> chainFactory,
            bool maintainBaseStrategy = false,
            bool maintainBaseStrategyFlyException = false,
            bool skipCheck = false,
            PermutationTreeCache? cache = null,
            CancellationToken cancellationToken = default)
        {
            if (order == null || chainFactory == null)
            {
                return null;
            }

            if (!TryBuildEngineInputs(order, out List<PermLegRequest>? legRequests, out List<Option>? state))
            {
                return null;
            }

            string underlying = order.UnderlyingSymbol ?? state[0].Underlying?.Symbol ?? string.Empty;
            if (string.IsNullOrWhiteSpace(underlying))
            {
                return null;
            }

            UnderlyingSymbolTree? tree = await LoadTreeAsync(underlying, chainFactory, cache, cancellationToken).ConfigureAwait(false);
            if (tree == null)
            {
                return null;
            }

            List<Option>? candidate;
            bool directValid = skipCheck || PermutationEngine.IsPermValid(tree, state, mode, permSide, order.BaseStrategy, maintainBaseStrategy, legRequests);
            if (directValid)
            {
                candidate = PermutationEngine.ApplyBump(tree, state, mode, permSide, order.BaseStrategy, maintainBaseStrategy);
            }
            else if (maintainBaseStrategy
                     && order.BaseStrategy.IsCalendar()
                     && (mode == PermMode.ExpirationUp || mode == PermMode.ExpirationDown))
            {
                candidate = PermutationEngine.TryCalendarFallback(tree, state, mode, permSide, order.BaseStrategy, maintainBaseStrategy, legRequests);
            }
            else
            {
                candidate = null;
            }
            if (candidate == null)
            {
                return null;
            }

            if (maintainBaseStrategy)
            {
                BaseStrategy candidateBs = PermutationEngine.EvaluateBaseStrategy(candidate, legRequests);
                bool bothFly = maintainBaseStrategyFlyException && order.BaseStrategy.IsAnyFly() && candidateBs.IsAnyFly();
                if (order.BaseStrategy != candidateBs && !bothFly)
                {
                    return null;
                }
            }

            return PermutationEngine.SnapshotResult(candidate, legRequests);
        }

        public static async Task<bool> IsNextPermValidAsync(
            IOrderSlim order,
            PermMode mode,
            PermSide permSide,
            Func<string, CancellationToken, Task<IEnumerable<Option>?>> chainFactory,
            bool maintainBaseStrategy = false,
            PermutationTreeCache? cache = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (order == null || chainFactory == null)
                {
                    return false;
                }

                if (!TryBuildEngineInputs(order, out List<PermLegRequest>? legRequests, out List<Option>? state))
                {
                    return false;
                }

                string underlying = order.UnderlyingSymbol ?? state[0].Underlying?.Symbol ?? string.Empty;
                if (string.IsNullOrWhiteSpace(underlying))
                {
                    return false;
                }

                UnderlyingSymbolTree? tree = await LoadTreeAsync(underlying, chainFactory, cache, cancellationToken).ConfigureAwait(false);
                if (tree == null)
                {
                    return false;
                }

                return PermutationEngine.IsPermValid(tree, state, mode, permSide, order.BaseStrategy, maintainBaseStrategy, legRequests);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryBuildEngineInputs(
            IOrderSlim order,
            [NotNullWhen(true)] out List<PermLegRequest>? legRequests,
            [NotNullWhen(true)] out List<Option>? state)
        {
            legRequests = null;
            state = null;

            if (order is IComplexOrderSlim complex && complex.Legs != null && complex.Legs.Count > 0)
            {
                List<PermLegRequest> reqs = new();
                List<Option> opts = new();
                foreach (IComplexOrderLeg leg in complex.Legs)
                {
                    if (leg == null || string.IsNullOrWhiteSpace(leg.Symbol))
                    {
                        continue;
                    }
                    Option? legOption = ResolveOption(leg.Security, leg.Symbol);
                    if (legOption == null)
                    {
                        continue;
                    }
                    reqs.Add(new PermLegRequest
                    {
                        Symbol = leg.Symbol,
                        Side = leg.Side ?? Side.Buy,
                        Ratio = leg.Ratio > 0 ? leg.Ratio : 1,
                    });
                    opts.Add(legOption);
                }
                if (reqs.Count > 0)
                {
                    legRequests = reqs;
                    state = opts;
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(order.Symbol))
            {
                return false;
            }

            Option? single = ResolveOption(order.Security, order.Symbol);
            if (single == null)
            {
                return false;
            }

            legRequests = new List<PermLegRequest>
            {
                new() { Symbol = order.Symbol!, Side = order.Side ?? Side.Buy, Ratio = 1 },
            };
            state = new List<Option> { single };
            return true;
        }

        private static Option? ResolveOption(Security? security, string? symbol)
        {
            if (security is Option direct)
            {
                EnsureUnderlying(direct, symbol);
                return direct;
            }
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }
            Security? parsed = SymbolParser.GetSecurityFromSymbol(symbol, out string? underlyingSymbol);
            if (parsed is Option option)
            {
                if (option.Underlying == null && !string.IsNullOrEmpty(underlyingSymbol))
                {
                    option.Underlying = new Security
                    {
                        Symbol = underlyingSymbol,
                        SecurityType = Data.Enums.SecurityType.Stock,
                    };
                }
                return option;
            }
            return null;
        }

        private static void EnsureUnderlying(Option option, string? symbol)
        {
            if (option.Underlying != null && !string.IsNullOrEmpty(option.Underlying.Symbol))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return;
            }
            SymbolParser.GetSecurityFromSymbol(symbol, out string? underlyingSymbol);
            if (string.IsNullOrEmpty(underlyingSymbol))
            {
                return;
            }
            option.Underlying = new Security
            {
                Symbol = underlyingSymbol,
                SecurityType = Data.Enums.SecurityType.Stock,
            };
        }

        private static Task<UnderlyingSymbolTree?> LoadTreeAsync(
            string underlying,
            Func<string, CancellationToken, Task<IEnumerable<Option>?>> chainFactory,
            PermutationTreeCache? cache,
            CancellationToken cancellationToken)
        {
            if (cache != null)
            {
                return cache.GetOrBuildAsync(underlying, async ct =>
                {
                    IEnumerable<Option>? chain = await chainFactory(underlying, ct).ConfigureAwait(false);
                    return chain == null ? null : new List<Option>(chain);
                }, cancellationToken);
            }

            return BuildFreshAsync();

            async Task<UnderlyingSymbolTree?> BuildFreshAsync()
            {
                IEnumerable<Option>? chain = await chainFactory(underlying, cancellationToken).ConfigureAwait(false);
                if (chain == null)
                {
                    return null;
                }
                return PermutationEngine.BuildTree(underlying, chain);
            }
        }
    }
}
