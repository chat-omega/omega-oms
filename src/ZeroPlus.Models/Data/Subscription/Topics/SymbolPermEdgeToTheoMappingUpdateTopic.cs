using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public class SymbolPermEdgeToTheoMappingUpdateTopic : ISymbolPermEdgeToTheoMappingUpdateTopic
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;

        private readonly ILogger<SymbolPermEdgeToTheoMappingUpdateTopic> _logger;
        private ulong _index;

        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public ulong Index { get => _index; set => _index = value; }
        public string? Symbol { get; set; }
        public List<EdgeToTheoTrackerModel>? LatestMapping { get; set; }


        public SymbolPermEdgeToTheoMappingUpdateTopic(ILogger<SymbolPermEdgeToTheoMappingUpdateTopic> logger)
        {
            _logger = logger;
            _index = 0;

            Id = Guid.NewGuid();
            Compressed = false;
            MessagePriority = PRIORITY;
        }

        public void Init(string symbol)
        {
            Symbol = symbol;
        }

        public void Update(List<EdgeToTheoTrackerModel> models)
        {
            LatestMapping = models;
            GetNextIndex();
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            try
            {
            var ctx = (SbeEncodeBufferContext)encodeContext;
                ulong lastIndex = _index;
                nextIndex = lastIndex;
                if (lastIndex == index || Symbol == null || LatestMapping == null)
                {
                    return false;
                }

                int written = ctx.Encoder.EncodePermEdgeToTheoMappingMessage(ctx.DirectBuffer, 0, Symbol, LatestMapping);
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

        private ulong GetNextIndex()
        {
            return Interlocked.Increment(ref _index);
        }
    }
}
