using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class RbboUpdateTopic : IRbboUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.High;
    private readonly ILogger<RbboUpdateTopic> _logger;
    private ulong _index;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public string Symbol { get; set; }
    public int SymbolIndex { get; set; }
    public MessagePriority MessagePriority { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public RbboUpdateModel UpdateModel { get; }
    public ulong Index { get => _index; set => _index = value; }

    public RbboUpdateTopic(ILogger<RbboUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;

        Symbol = string.Empty;
        Id = Guid.NewGuid();
        Compressed = false;
        MessagePriority = PRIORITY;
        UpdateModel = new RbboUpdateModel();
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

            int written = ctx.Encoder.EncodeRbboUpdate(ctx.DirectBuffer, 0, UpdateModel);
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
