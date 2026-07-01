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
    public class IbQuoteUpdateTopic : IIbQuoteUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.High;
        private readonly ILogger<IbQuoteUpdateTopic> _logger;
        private ulong _index;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public IbQuoteUpdateModel UpdateModel { get; }
        public ulong Index { get => _index; set => _index = value; }

        public IbQuoteUpdateTopic(ILogger<IbQuoteUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

            UpdateModel = new IbQuoteUpdateModel();
            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void Updated()
        {
            GetNextIndex();
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;
                if (lastIndex == index || UpdateModel == null)
                {
                    return false;
                }

                int written = ctx.Encoder.EncodeIbQuoteUpdate(ctx.DirectBuffer, 0, UpdateModel);
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
    }
}
