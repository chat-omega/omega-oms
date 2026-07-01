using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace ZeroPlus.Models.Data.Enums
{
    /// <summary>
    /// Provides allocation-free lookup from a <see cref="DescriptionAttribute"/>
    /// token (as a <see cref="ReadOnlySpan{Char}"/>) to the corresponding enum value.
    /// </summary>
    /// <remarks>
    /// The lookup table is built once via reflection during the static
    /// initialization of each closed generic instantiation. After init, every
    /// call performs an ordinal binary search using
    /// <see cref="MemoryExtensions.SequenceCompareTo{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
    /// which is hardware-accelerated for <c>char</c> spans. No allocations occur
    /// at call time.
    /// </remarks>
    /// <typeparam name="TEnum">
    /// An enum whose members carry <see cref="DescriptionAttribute"/>. Members
    /// without a description are ignored. When two members share the same
    /// description, the first one declared wins.
    /// </typeparam>
    public static class EnumDescriptionLookup<TEnum> where TEnum : struct, Enum
    {
        private static readonly (string Description, TEnum Value)[] _entries = Build();

        public static bool TryGetValue(ReadOnlySpan<char> description, out TEnum value)
        {
            var entries = _entries;
            int lo = 0;
            int hi = entries.Length - 1;
            while (lo <= hi)
            {
                int mid = (int)(((uint)lo + (uint)hi) >> 1);
                int cmp = description.SequenceCompareTo(entries[mid].Description.AsSpan());
                if (cmp == 0)
                {
                    value = entries[mid].Value;
                    return true;
                }
                if (cmp < 0)
                {
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            value = default;
            return false;
        }

        private static (string Description, TEnum Value)[] Build()
        {
            var fields = typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<(string, TEnum)>(fields.Length);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<DescriptionAttribute>();
                if (attr is null || string.IsNullOrEmpty(attr.Description)) continue;
                if (!seen.Add(attr.Description)) continue;

                var boxed = field.GetValue(null);
                if (boxed is null) continue;
                list.Add((attr.Description, (TEnum)boxed));
            }

            list.Sort(static (a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            return list.ToArray();
        }
    }
}
