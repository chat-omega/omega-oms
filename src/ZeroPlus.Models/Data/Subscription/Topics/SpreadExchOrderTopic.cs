using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class SpreadExchOrderTopic : ISpreadExchOrderTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Medium;

    private readonly object _indexLock;

    private readonly ILogger<SpreadExchOrderTopic> _logger;
    private readonly ConcurrentDictionary<ulong, SpreadExchOrder> _indexToModelMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }
    public string? Symbol { get; set; }

    public SpreadExchOrderTopic(ILogger<SpreadExchOrderTopic> logger)
    {
        _logger = logger;
        _indexToModelMap = new ConcurrentDictionary<ulong, SpreadExchOrder>();
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
            ulong count;
            lock (_indexLock)
            {
                count = Index - index;
            }
            
            if (count > 0)
            {
                for (ulong i = 0; i < count; i++)
                {
                    if (_indexToModelMap.TryGetValue(index + i, out var update))
                    {
                        int written;
                        if (update.OrderStatus == SrOrderStatus.Open)
                        {
                            written = ctx.Encoder.EncodeOpenSpreadExchOrderMessage(ctx.DirectBuffer, 0, update);
                        }
                        else
                        {
                            written = ctx.Encoder.EncodeRemoveSpreadExchOrderMessage(ctx.DirectBuffer, 0, update);
                        }
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

            nextIndex = index;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(TryEncodeAndSend));
            nextIndex = index;
            return false;
        }
    }

    public void AddModel(SpreadExchOrder update)
    {
        if (update.OrderID != null)
        {
            lock (_indexLock)
            {
                _indexToModelMap[Index] = update;
                Index++;
            }
        }
    }
}