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

public class FirmOrderAndTradeSummaryUpdateTopic : IFirmOrderAndTradeSummaryUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Medium;
    private readonly ILogger<FirmOrderAndTradeSummaryUpdateTopic> _logger;
    private ulong _index;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public FirmOrderAndTradeSummary? UpdateModel { get; private set; }
    public ulong Index { get => _index; set => _index = value; }

    public FirmOrderAndTradeSummaryUpdateTopic(ILogger<FirmOrderAndTradeSummaryUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;

        Id = Guid.NewGuid();
        Compressed = false;
        MessagePriority = PRIORITY;
    }

    public void Init(FirmOrderAndTradeSummary model)
    {
        UpdateModel = model;
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

            int written = ctx.Encoder.EncodeFirmOrderAndTradeSummary(ctx.DirectBuffer, 0, UpdateModel);
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