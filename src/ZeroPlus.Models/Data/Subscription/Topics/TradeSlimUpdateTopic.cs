using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    /// <summary>
    /// Per-symbol topic that buffers <see cref="TradeSlim"/> updates.
    ///
    /// Single-leg trades are published per option-symbol topic; multi-leg
    /// (spread) trades are published per underlying-root topic. The choice
    /// of map is made by the subscription manager on the consumer side.
    ///
    /// Buffering follows <see cref="EdgeFeedUpdateTopic"/>: each added trade
    /// is stored in an index-keyed dictionary, and <see cref="TryEncodeAndSend"/>
    /// flushes every trade not yet seen by the subscriber.
    /// </summary>
    public class TradeSlimUpdateTopic : ITradeSlimUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;

        private readonly object _indexLock;
        private readonly ILogger<TradeSlimUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, TradeSlim> _indexToTradeMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public string Symbol { get; set; }
        public int SymbolIndex { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get; set; }

        public TradeSlimUpdateTopic(ILogger<TradeSlimUpdateTopic> logger)
        {
            _logger = logger;
            _indexToTradeMap = new ConcurrentDictionary<ulong, TradeSlim>();
            _indexLock = new object();

            Symbol = string.Empty;
            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
                ulong count;
                lock (_indexLock)
                {
                    count = Index - index;
                }

                if (count == 0)
                {
                    nextIndex = index;
                    return false;
                }

                var ctx = (SbeEncodeBufferContext)encodeContext;
                bool sent = false;
                for (ulong i = 0; i < count; i++)
                {
                    ulong slot = index + i;
                    if (_indexToTradeMap.TryGetValue(slot, out TradeSlim? trade) && trade != null)
                    {
                        int written = ctx.Encoder.EncodeTradeSlim(ctx.DirectBuffer, 0, trade);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                    else
                    {
                        _logger?.LogError(nameof(TryEncodeAndSend) + " Lookup failed for index: " + slot);
                    }
                }

                nextIndex = index + count;
                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        public void AddTrades(HashSet<TradeSlim> trades)
        {
            foreach (TradeSlim trade in trades)
            {
                AddTrade(trade);
            }
        }

        public void AddTrade(TradeSlim trade)
        {
            lock (_indexLock)
            {
                _indexToTradeMap[Index] = trade;
                Index++;
            }
        }
    }
}
