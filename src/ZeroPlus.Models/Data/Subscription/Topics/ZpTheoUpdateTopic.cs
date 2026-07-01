using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class ZpTheoUpdateTopic : IZpTheoUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.High;
    private readonly ILogger<ZpTheoUpdateTopic> _logger;
    private ulong _index;
    private int _tickerId;
    private ulong _sequence;
    private double _theoBid;
    private double _theoAsk;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get => _index; set => _index = value; }

    public ZpTheoUpdateTopic(ILogger<ZpTheoUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;
        _tickerId = -1;
        _sequence = 0;
        _theoBid = double.NaN;
        _theoAsk = double.NaN;

        Id = Guid.NewGuid();
        Compressed = false;
        MessagePriority = PRIORITY;
    }

    public void Initialize(int tickerId)
    {
        _tickerId = tickerId;
        Initialized = true;
    }

    public void Update(ulong sequence, double theoBid, double theoAsk)
    {
        if (!Initialized || _tickerId < 0)
        {
            return;
        }

        if (sequence <= _sequence)
        {
            return;
        }

        _sequence = sequence;
        _theoBid = theoBid;
        _theoAsk = theoAsk;
        Interlocked.Increment(ref _index);
    }

    public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
    {
        try
        {
            var ctx = (SbeEncodeBufferContext)encodeContext;
            ulong lastIndex = _index;
            nextIndex = lastIndex;
            if (!Initialized || lastIndex == index || _tickerId < 0)
            {
                return false;
            }

            int written = ctx.Encoder.EncodeZpTheoUpdateMessage(ctx.DirectBuffer, 0, _tickerId, _sequence, _theoBid, _theoAsk);
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
}
