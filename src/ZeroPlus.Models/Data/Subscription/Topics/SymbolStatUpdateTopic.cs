using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public partial class SymbolStatUpdateTopic : ISymbolStatUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        private readonly object _indexLock;
        private ulong _index;

        private readonly ILogger<SymbolStatUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, (ISymbolStatModel, TopicUpdateType)> _indexToUpdateMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public int RequestId { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public SymbolStatUpdateTopic(ILogger<SymbolStatUpdateTopic> logger)
        {
            _logger = logger;
            _indexToUpdateMap = new ConcurrentDictionary<ulong, (ISymbolStatModel, TopicUpdateType)>();
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
                (HashSet<ISymbolStatModel> Added, HashSet<ISymbolStatModel> Updated) updates = GetUpdatesSince(index, out nextIndex);
                if (index == nextIndex || (updates.Added.Count == 0 && updates.Updated.Count == 0))
                {
                    return false;
                }

                foreach (ISymbolStatModel model in updates.Added)
                {
                    int written = ctx.Encoder.EncodeSymbolStatModelAddedMessage(ctx.DirectBuffer, 0, model);
                    sender.SendEncoded(encodeContext, written, Compressed);
                }

                foreach (ISymbolStatModel model in updates.Updated)
                {
                    int written = ctx.Encoder.EncodeSymbolStatModelUpdateMessage(ctx.DirectBuffer, 0, model);
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

        public void AddMultiple(HashSet<ISymbolStatModel> models)
        {
            foreach (ISymbolStatModel model in models)
            {
                Add(model);
            }
        }

        public void Add(ISymbolStatModel model)
        {
            AddUpdate(model, TopicUpdateType.Add);
        }

        public void Update(ISymbolStatModel model)
        {
            AddUpdate(model, TopicUpdateType.Update);
        }

        private void AddUpdate(ISymbolStatModel model, TopicUpdateType updateType)
        {
            lock (_indexLock)
            {
                _indexToUpdateMap[_index] = (model, updateType);
                _index++;
            }
        }

        private (HashSet<ISymbolStatModel> Added, HashSet<ISymbolStatModel> Updated) GetUpdatesSince(ulong index, out ulong nextIndex)
        {
            HashSet<ISymbolStatModel> sAdded = new HashSet<ISymbolStatModel>();
            HashSet<ISymbolStatModel> sUpdated = new HashSet<ISymbolStatModel>();
            ulong count = _index - index;
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToUpdateMap.TryGetValue(index + i, out (ISymbolStatModel, TopicUpdateType) update))
                    {
                        switch (update.Item2)
                        {
                            case TopicUpdateType.Add:
                                sAdded.Add(update.Item1);
                                break;
                            case TopicUpdateType.Update:
                                if (!sAdded.Contains(update.Item1))
                                {
                                    sUpdated.Add(update.Item1);
                                }
                                break;
                        }
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
            return (sAdded, sUpdated);
        }
    }
}
