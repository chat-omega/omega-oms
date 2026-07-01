using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Protocols;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ITopic
    {
        public Guid Id { get; }
        public ulong Index { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; } 
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex);
    }
}
