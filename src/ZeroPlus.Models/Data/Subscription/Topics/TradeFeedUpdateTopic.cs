using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public partial class TradeFeedUpdateTopic : ITradeFeedUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        private readonly object _indexLock;
        private ulong _index;

        private readonly ILogger<TradeFeedUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, ITradeFeedModel> _indexToModelMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public int RequestId { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public TradeFeedUpdateTopic(ILogger<TradeFeedUpdateTopic> logger)
        {
            _logger = logger;
            _indexToModelMap = new ConcurrentDictionary<ulong, ITradeFeedModel>();
            _indexLock = new object();

            Id = Guid.NewGuid();
            Compressed = true;
            MessagePriority = PRIORITY;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
                var ctx = (SbeEncodeBufferContext)encodeContext;

                ulong snapshotIndex = _index;
                if (snapshotIndex <= index)
                {
                    nextIndex = snapshotIndex;
                    return false;
                }

                ulong total = snapshotIndex - index;
                List<ITradeFeedModel> batch = new List<ITradeFeedModel>(
                    capacity: (int)Math.Min((ulong)BATCH_UPDATE_SIZE, total));

                int batchId = 0;
                ulong cursor = index;
                ulong end = snapshotIndex;
                bool sentAny = false;

                while (cursor < end)
                {
                    batch.Clear();
                    ulong batchEnd = Math.Min(end, cursor + (ulong)BATCH_UPDATE_SIZE);
                    for (ulong i = cursor; i < batchEnd; i++)
                    {
                        if (_indexToModelMap.TryGetValue(i, out ITradeFeedModel? update))
                        {
                            batch.Add(update);
                        }
                        else
                        {
                            _logger?.LogError("{Method} Lookup failed for index: {Index}", nameof(TryEncodeAndSend), i);
                        }
                    }

                    cursor = batchEnd;
                    bool isLast = cursor >= end;

                    if (batch.Count > 0)
                    {
                        int written = ctx.Encoder.EncodeTradeFeedModel(ctx.DirectBuffer, 0, batchId, batch, isLast);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sentAny = true;
                    }

                    batchId++;
                }

                nextIndex = snapshotIndex;
                return sentAny;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        public void AddModels(HashSet<ITradeFeedModel> updates)
        {
            foreach (ITradeFeedModel update in updates)
            {
                AddModel(update);
            }
        }

        public void AddModel(ITradeFeedModel update)
        {
            lock (_indexLock)
            {
                _indexToModelMap[_index] = update;
                _index++;
            }
        }
    }
}
