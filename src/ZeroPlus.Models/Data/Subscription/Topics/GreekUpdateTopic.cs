using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class GreekUpdateTopic : IGreekUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.High;
    private readonly ILogger<GreekUpdateTopic> _logger;
    private ulong _index;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public int SymbolIndex { get; set; }
    public MessagePriority MessagePriority { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public IGreekUpdate? UpdateModel { get; set; }
    public ulong Index { get => _index; set => _index = value; }

    public GreekUpdateTopic(ILogger<GreekUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;

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

            switch (UpdateModel)
            {
                case GreekUpdateModel greekUpdateModel:
                {
                    int written = ctx.Encoder.EncodeGreekUpdate(ctx.DirectBuffer, 0, greekUpdateModel);
                    sender.SendEncoded(encodeContext, written, Compressed);
                    return true;
                }
                case SlimGreekUpdateModel slimGreekUpdate:
                {
                    int written = ctx.Encoder.EncodeSlimGreekUpdateMessage(ctx.DirectBuffer, 0, slimGreekUpdate);
                    sender.SendEncoded(encodeContext, written, Compressed);
                    return true;
                }
                default:
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

    private ulong GetNextIndex()
    {
        return Interlocked.Increment(ref _index);
    }
}