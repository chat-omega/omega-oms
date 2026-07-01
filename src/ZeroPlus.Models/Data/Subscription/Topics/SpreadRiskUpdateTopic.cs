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
    public partial class SpreadRiskUpdateTopic : ISpreadRiskUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        private readonly object _indexLock;
        private ulong _index;

        private readonly ILogger<SpreadRiskUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, ISpreadRiskModel> _indexToOrderUpdateMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public int RequestId { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public SpreadRiskUpdateTopic(ILogger<SpreadRiskUpdateTopic> logger)
        {
            _logger = logger;
            _indexToOrderUpdateMap = new ConcurrentDictionary<ulong, ISpreadRiskModel>();
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
                var updates = GetUpdatesSince(index, out nextIndex);
                if (index == nextIndex || (updates.Count == 0))
                {
                    return false;
                }

                foreach (ISpreadRiskModel update in updates)
                {
                    int written = ctx.Encoder.EncodeSpreadRiskModel(ctx.DirectBuffer, 0, update);
                    sender.SendEncoded(encodeContext, written, Compressed);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        public void Add(ISpreadRiskModel model)
        {
            lock (_indexLock)
            {
                _indexToOrderUpdateMap[_index] = model;
                _index++;
            }
        }

        private HashSet<ISpreadRiskModel> GetUpdatesSince(ulong index, out ulong nextIndex)
        {
            HashSet<ISpreadRiskModel> newChanges = new HashSet<ISpreadRiskModel>();
            ulong count = _index - index;
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToOrderUpdateMap.TryGetValue(index + i, out var update))
                    {
                        newChanges.Add(update);
                    }
                    else
                    {
                        _logger?.LogError(nameof(TryEncodeAndSend) + "Lookup failed for index: " + index + i);
                    }
                }
                nextIndex = index + count;
            }
            else
            {
                nextIndex = index;
            }
            return newChanges;
        }
    }
}
