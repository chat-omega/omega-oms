using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public partial class OrderInfoUpdateTopic : IOrderInfoUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        private readonly object _indexLock;
        private ulong _index;

        private readonly ILogger<OrderInfoUpdateTopic> _logger;
        private readonly ConcurrentDictionary<ulong, OrderInfoUpdate> _indexToOrderUpdateMap;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public int RequestId { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public OrderInfoUpdateTopic(ILogger<OrderInfoUpdateTopic> logger)
        {
            _logger = logger;
            _indexToOrderUpdateMap = new ConcurrentDictionary<ulong, OrderInfoUpdate>();
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
                ulong count = _index - index;
                if (count > 0)
                {
                    for (ulong i = 0; i < count; i++)
                    {
                        if (_indexToOrderUpdateMap.TryGetValue(index + i, out OrderInfoUpdate? update))
                        {
                            int written = ctx.Encoder.EncodeOrderInfoUpdate(ctx.DirectBuffer, 0, update);
                            sender.SendEncoded(encodeContext, written, Compressed);
                        }
                        else
                        {
                            _logger?.LogError(nameof(TryEncodeAndSend) + "Lookup failed for index: " + index + i);
                        }
                    }

                    nextIndex = index + count;
                    return true;
                }
                else
                {
                    nextIndex = index;
                    return false;
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, nameof(TryEncodeAndSend));
                nextIndex = index;
                return false;
            }
        }

        public void AddMultipleOrders(HashSet<OrderInfoUpdate> orders)
        {
            lock (_indexLock)
            {
                foreach (OrderInfoUpdate orderInfo in orders)
                {
                    _indexToOrderUpdateMap[_index] = orderInfo;
                    _index++;
                }
            }
        }

        public void AddUpdate(OrderInfoUpdate orderInfo)
        {
            lock (_indexLock)
            {
                _indexToOrderUpdateMap[_index] = orderInfo;
                _index++;
            }
        }
    }
}
