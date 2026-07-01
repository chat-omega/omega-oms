using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Structures;

namespace ZeroPlus.Models.Utils
{
    public sealed class PermutationTreeCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

        public TimeSpan Ttl { get; }

        public PermutationTreeCache() : this(TimeSpan.FromMinutes(5)) { }

        public PermutationTreeCache(TimeSpan ttl)
        {
            Ttl = ttl;
        }

        public UnderlyingSymbolTree? GetOrBuild(string underlying, Func<IEnumerable<Option>?> chainFactory)
        {
            if (string.IsNullOrWhiteSpace(underlying) || chainFactory == null)
            {
                return null;
            }

            if (_entries.TryGetValue(underlying, out CacheEntry entry) && IsFresh(entry))
            {
                return entry.Tree;
            }

            IEnumerable<Option>? chain = chainFactory();
            if (chain == null)
            {
                return null;
            }

            UnderlyingSymbolTree tree = PermutationEngine.BuildTree(underlying, chain);
            _entries[underlying] = new CacheEntry(tree, DateTime.UtcNow);
            return tree;
        }

        public async Task<UnderlyingSymbolTree?> GetOrBuildAsync(
            string underlying,
            Func<CancellationToken, Task<List<Option>?>> chainFactory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(underlying) || chainFactory == null)
            {
                return null;
            }

            if (_entries.TryGetValue(underlying, out CacheEntry entry) && IsFresh(entry))
            {
                return entry.Tree;
            }

            List<Option>? chain = await chainFactory(cancellationToken).ConfigureAwait(false);
            if (chain == null || chain.Count == 0)
            {
                return null;
            }

            UnderlyingSymbolTree tree = PermutationEngine.BuildTree(underlying, chain);
            _entries[underlying] = new CacheEntry(tree, DateTime.UtcNow);
            return tree;
        }

        public bool Invalidate(string underlying)
        {
            if (string.IsNullOrWhiteSpace(underlying))
            {
                return false;
            }
            return _entries.TryRemove(underlying, out _);
        }

        public void Clear() => _entries.Clear();

        public int Count => _entries.Count;

        private bool IsFresh(CacheEntry entry)
        {
            return DateTime.UtcNow - entry.Timestamp < Ttl;
        }

        private readonly record struct CacheEntry(UnderlyingSymbolTree Tree, DateTime Timestamp);
    }
}
