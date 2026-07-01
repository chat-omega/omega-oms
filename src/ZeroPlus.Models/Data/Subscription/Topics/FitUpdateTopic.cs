using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class FitUpdateTopic : IFitUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.High;
    private readonly ILogger<FitUpdateTopic> _logger;
    private ulong _index;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public UnderFitResult UnderFitResult { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get => _index; set => _index = value; }

    public FitUpdateTopic(ILogger<FitUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;

        UnderFitResult = new();
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
            if (lastIndex == index)
            {
                return false;
            }

            int written = ctx.Encoder.EncodeFitUpdateMessage(ctx.DirectBuffer, 0, UnderFitResult);
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