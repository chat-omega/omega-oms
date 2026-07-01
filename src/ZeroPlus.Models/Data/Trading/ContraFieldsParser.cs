using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading
{
    /// <summary>
    /// Parses a pipe-delimited list of <c>tag=token</c> pairs into the contra-field
    /// collections of an <see cref="IOrder"/> or <see cref="IComplexOrderLeg"/>.
    /// Recognised FIX tags:
    /// <list type="bullet">
    ///   <item><description>9204 -&gt; <see cref="ContraCapacity"/></description></item>
    ///   <item><description>9375 -&gt; <see cref="ContraBrokerName"/></description></item>
    ///   <item><description>9439 -&gt; <see cref="ContraCmta"/></description></item>
    ///   <item><description>9337 -&gt; <see cref="ContraTrader"/></description></item>
    /// </list>
    /// Parsing walks the input as <see cref="ReadOnlySpan{T}"/> with no intermediate
    /// string allocations. Tokens are mapped to enum members via the per-enum
    /// <see cref="DescriptionAttribute"/> through <see cref="EnumDescriptionLookup{TEnum}"/>;
    /// the lookup tables are built once per enum during static initialization, so each
    /// call here is a span-vs-string ordinal binary search with no allocations.
    /// Malformed pairs, unknown tags, and unknown tokens are silently skipped.
    /// </summary>
    public static class ContraFieldsParser
    {
        public static void Parse(IOrder order, string? input)
        {
            if (order is null) throw new ArgumentNullException(nameof(order));
            if (string.IsNullOrEmpty(input)) return;
            ParseCore(input.AsSpan(), new OrderTarget(order));
        }

        public static void Parse(IComplexOrderLeg leg, string? input)
        {
            if (leg is null) throw new ArgumentNullException(nameof(leg));
            if (string.IsNullOrEmpty(input)) return;
            ParseCore(input.AsSpan(), new LegTarget(leg));
        }

        /// <summary>
        /// Parses <paramref name="input"/> once and appends each value to both
        /// <paramref name="order"/>'s and <paramref name="leg"/>'s contra collections.
        /// </summary>
        public static void Parse(IOrder order, IComplexOrderLeg leg, string? input)
        {
            if (order is null) throw new ArgumentNullException(nameof(order));
            if (leg is null) throw new ArgumentNullException(nameof(leg));
            if (string.IsNullOrEmpty(input)) return;
            ParseCore(input.AsSpan(), new OrderAndLegTarget(order, leg));
        }

        private static void ParseCore<TTarget>(ReadOnlySpan<char> input, TTarget target)
            where TTarget : struct, IContraTarget
        {
            while (!input.IsEmpty)
            {
                int pipe = input.IndexOf('|');
                ReadOnlySpan<char> pair;
                if (pipe >= 0)
                {
                    pair = input.Slice(0, pipe);
                    input = input.Slice(pipe + 1);
                }
                else
                {
                    pair = input;
                    input = default;
                }

                if (pair.IsEmpty) continue;

                int eq = pair.IndexOf('=');
                if (eq <= 0 || eq == pair.Length - 1) continue;

                ReadOnlySpan<char> key = pair.Slice(0, eq);
                ReadOnlySpan<char> val = pair.Slice(eq + 1);

                switch (key)
                {
                    case "9204":
                        if (EnumDescriptionLookup<ContraCapacity>.TryGetValue(val, out var cap)) target.AddCapacity(cap);
                        break;
                    case "9375":
                        if (EnumDescriptionLookup<ContraBrokerName>.TryGetValue(val, out var br)) target.AddBroker(br);
                        break;
                    case "9439":
                        if (EnumDescriptionLookup<ContraCmta>.TryGetValue(val, out var cm)) target.AddCmta(cm);
                        break;
                    case "9337":
                        if (EnumDescriptionLookup<ContraTrader>.TryGetValue(val, out var tr)) target.AddTrader(tr);
                        break;
                }
            }
        }

        private interface IContraTarget
        {
            void AddCapacity(ContraCapacity value);
            void AddBroker(ContraBrokerName value);
            void AddCmta(ContraCmta value);
            void AddTrader(ContraTrader value);
        }

        private readonly struct OrderTarget : IContraTarget
        {
            private readonly IOrder _order;
            public OrderTarget(IOrder order) => _order = order;
            public void AddCapacity(ContraCapacity v) => (_order.ContraCapacities ??= new List<ContraCapacity>()).Add(v);
            public void AddBroker(ContraBrokerName v) => (_order.ContraBrokerNames ??= new List<ContraBrokerName>()).Add(v);
            public void AddCmta(ContraCmta v) => (_order.ContraCmtas ??= new List<ContraCmta>()).Add(v);
            public void AddTrader(ContraTrader v) => (_order.ContraTraders ??= new List<ContraTrader>()).Add(v);
        }

        private readonly struct LegTarget : IContraTarget
        {
            private readonly IComplexOrderLeg _leg;
            public LegTarget(IComplexOrderLeg leg) => _leg = leg;
            public void AddCapacity(ContraCapacity v) => (_leg.ContraCapacities ??= new List<ContraCapacity>()).Add(v);
            public void AddBroker(ContraBrokerName v) => (_leg.ContraBrokerNames ??= new List<ContraBrokerName>()).Add(v);
            public void AddCmta(ContraCmta v) => (_leg.ContraCmtas ??= new List<ContraCmta>()).Add(v);
            public void AddTrader(ContraTrader v) => (_leg.ContraTraders ??= new List<ContraTrader>()).Add(v);
        }

        private readonly struct OrderAndLegTarget : IContraTarget
        {
            private readonly IOrder _order;
            private readonly IComplexOrderLeg _leg;
            public OrderAndLegTarget(IOrder order, IComplexOrderLeg leg)
            {
                _order = order;
                _leg = leg;
            }

            public void AddCapacity(ContraCapacity v)
            {
                (_order.ContraCapacities ??= new List<ContraCapacity>()).Add(v);
                (_leg.ContraCapacities ??= new List<ContraCapacity>()).Add(v);
            }

            public void AddBroker(ContraBrokerName v)
            {
                (_order.ContraBrokerNames ??= new List<ContraBrokerName>()).Add(v);
                (_leg.ContraBrokerNames ??= new List<ContraBrokerName>()).Add(v);
            }

            public void AddCmta(ContraCmta v)
            {
                (_order.ContraCmtas ??= new List<ContraCmta>()).Add(v);
                (_leg.ContraCmtas ??= new List<ContraCmta>()).Add(v);
            }

            public void AddTrader(ContraTrader v)
            {
                (_order.ContraTraders ??= new List<ContraTrader>()).Add(v);
                (_leg.ContraTraders ??= new List<ContraTrader>()).Add(v);
            }
        }
    }
}
