using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class TradeUpdateTopic : ITradeUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<TradeUpdateTopic> _logger;
        private ulong _index;

        public SubscriptionFieldType UpdateType { get; set; }

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }

        public TradeUpdateModel LastTrade;

        public TradeUpdateTopic(ILogger<TradeUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

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
                if (lastIndex == index || string.IsNullOrWhiteSpace(LastTrade.SpreadId))
                {
                    return false;
                }

                int written = ctx.Encoder.EncodeTradeUpdate(ctx.DirectBuffer, 0, ref LastTrade);
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

        public void TradeUpdated(TradeUpdateModel tradeUpdateModel)
        {
            LastTrade = tradeUpdateModel;
            GetNextIndex();
        }

        private ulong GetNextIndex()
        {
            return Interlocked.Increment(ref _index);
        }
    }
}
