using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class ServerClockUpdateTopic : IServerClockUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private readonly ILogger<ServerClockUpdateTopic> _logger;
        private ulong _index;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }

        public DateTime LastUpdate { get; set; }
        public TimeFeedType TimeFeedType { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public ServerClockUpdateTopic(ILogger<ServerClockUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

            LastUpdate = DateTime.MinValue;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;
                if (lastIndex == index)
                {
                    return false;
                }

                int written = ctx.Encoder.EncodeTimeUpdateMessage(ctx.DirectBuffer, 0, TimeFeedType, LastUpdate);
                sender.SendEncoded(encodeContext, written, Compressed);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        private ulong GetNextIndex()
        {
            return Interlocked.Increment(ref _index);
        }

        public void FieldUpdated(TimeFeedType timeFeedType, DateTime dateTime)
        {
            if (dateTime > LastUpdate)
            {
                TimeFeedType = timeFeedType;
                LastUpdate = dateTime;
                GetNextIndex();
            }
        }
    }
}
