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
    public class HerculesEchoMessageTopic : IHerculesEchoMessageTopic
    {
        private readonly ILogger<HerculesEchoMessageTopic> _logger;
        private readonly ConcurrentDictionary<ulong, HerculesEchoMessageModel> _indexToModelMap = [];
        private readonly object _indexLock = new();

        public Guid Id { get; } = Guid.NewGuid();
        public ulong Index { get; set; }
        public bool Compressed { get; } = true;
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; } = MessagePriority.Medium;
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }

        public HerculesEchoMessageTopic(ILogger<HerculesEchoMessageTopic> logger)
        {
            _logger = logger;
        }

        public void AddModel(HerculesEchoMessageModel echoMessage)
        {
            lock (_indexLock)
            {
                _indexToModelMap[Index] = echoMessage;
                Index++;
            }
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                var updates = GetUpdatesSince(index, out nextIndex);
                if (updates == null || index == nextIndex || updates.Count == 0)
                {
                    return false;
                }

                foreach (var echoUpdate in updates)
                {
                    int written = ctx.Encoder.EncodeHerculesEchoMessage(ctx.DirectBuffer, 0, echoUpdate.Order, echoUpdate.Source, echoUpdate.Venue, echoUpdate.BookUpdateType);
                    sender.SendEncoded(encodeContext, written, Compressed);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        private List<HerculesEchoMessageModel>? GetUpdatesSince(ulong index, out ulong nextIndex)
        {
            List<HerculesEchoMessageModel>? newUpdates = null;

            ulong count;
            lock (_indexLock)
            {
                count = Index - index;
            }
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToModelMap.TryGetValue(index + i, out var update))
                    {
                        newUpdates ??= new List<HerculesEchoMessageModel>((int)count);
                        newUpdates.Add(update);
                    }
                    else
                    {
                        _logger?.LogError(nameof(GetUpdatesSince) + "Lookup failed for index: " + index + i);
                    }
                }
                nextIndex = index + count;
            }
            else
            {
                nextIndex = index;
            }

            return newUpdates;
        }
    }
}
