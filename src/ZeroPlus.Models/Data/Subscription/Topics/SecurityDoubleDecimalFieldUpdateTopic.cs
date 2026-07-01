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
    public class SecurityDoubleDecimalFieldUpdateTopic : ISecurityDoubleDecimalFieldUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<SecurityDecimalFieldUpdateTopic> _logger;
        private ulong _index;
        private double _bidUpdate;
        private double _askUpdate;
        private double _bidChange;
        private double _askChange;
        private DateTime _timestamp;
        private int _bidSize;
        private int _askSize;
        private int _tickerId;
        private double _lastPrice;
        private double _latencyMs;

        public SubscriptionFieldType UpdateType { get; set; }

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public SecurityDoubleDecimalFieldUpdateTopic(ILogger<SecurityDecimalFieldUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

            _bidUpdate = double.NaN;
            _askUpdate = double.NaN;
            _lastPrice = double.NaN;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void Init(int tickerId, SubscriptionFieldType fieldType)
        {
            _tickerId = tickerId;
            UpdateType = fieldType;
        }

        public void FieldUpdated(double bidUpdate, double askUpdate, int bidSize, int askSize, double lastPrice, DateTime timestamp, double latencyMs = 0)
        {
            _bidChange = bidUpdate - _bidUpdate;
            _askChange = askUpdate - _askUpdate;

            _bidUpdate = bidUpdate;
            _askUpdate = askUpdate;
            _timestamp = timestamp;
            _lastPrice = lastPrice;
            _latencyMs = latencyMs;

            _bidSize = bidSize;
            _askSize = askSize;

            GetNextIndex();
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

                int written = ctx.Encoder.EncodeSecurityDoubleDecimalUpdate(ctx.DirectBuffer, 0, _tickerId,
                    UpdateType,
                    _bidUpdate,
                    _askUpdate,
                    _timestamp,
                    _bidChange,
                    _askChange,
                    _bidSize,
                    _askSize,
                    _lastPrice,
                    _latencyMs);
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

        private void GetNextIndex()
        {
            Interlocked.Increment(ref _index);
        }
    }
}
