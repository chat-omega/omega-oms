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
    public class SecuritySingleFieldUpdateTopic : ISecuritySingleFieldUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<SecuritySingleFieldUpdateTopic> _logger;
        private ulong _index;
        private int _tickerId;
        private double _value;

        public SubscriptionFieldType UpdateType { get; private set; }

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public SecuritySingleFieldUpdateTopic(ILogger<SecuritySingleFieldUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;
            _value = double.NaN;
            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void Init(int tickerId, SubscriptionFieldType fieldType)
        {
            _tickerId = tickerId;
            UpdateType = fieldType;
        }

        public void FieldUpdated(double value)
        {
            _value = value;
            Interlocked.Increment(ref _index);
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

                int written = ctx.Encoder.EncodeSingleFieldUpdateMessage(ctx.DirectBuffer, 0, _tickerId, UpdateType, _value);
                sender.SendEncoded(encodeContext, written, Compressed);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }
    }
}
