using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class CrossedImpliedQuoteNotificationTopic : ICrossedImpliedQuoteNotificationTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Medium;

    private readonly object _indexLock;

    private readonly ILogger<EdgeFeedUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, ImpliedQuoteUpdate> _indexToModelMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }

    public CrossedImpliedQuoteNotificationTopic(ILogger<EdgeFeedUpdateTopic> logger)
    {
        _logger = logger;
        _indexToModelMap = new ConcurrentDictionary<ulong, ImpliedQuoteUpdate>();
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
            HashSet<ImpliedQuoteUpdate> updates = GetUpdatesSince(index, out nextIndex);
            if (index == nextIndex || updates.Count == 0)
            {
                return false;
            }

            foreach (ImpliedQuoteUpdate update in updates)
            {
                int written = ctx.Encoder.EncodeImpliedQuoteUpdateMessage(ctx.DirectBuffer, 0, update);
                sender.SendEncoded(encodeContext, written, Compressed);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(TryEncodeAndSend));
            nextIndex = index;
            return false;
        }
    }

    public void AddUpdate(ImpliedQuoteUpdate update)
    {
        lock (_indexLock)
        {
            _indexToModelMap[Index] = update;
            Index++;
        }
    }

    private HashSet<ImpliedQuoteUpdate> GetUpdatesSince(ulong index, out ulong nextIndex)
    {
        HashSet<ImpliedQuoteUpdate> ordersAdded = new HashSet<ImpliedQuoteUpdate>();

        ulong count;
        lock (_indexLock)
        {
            count = Index - index;
        }
        if (count > 0)
        {
            for (ulong i = 0; i < count; i++)
            {
                if (_indexToModelMap.TryGetValue(index + i, out ImpliedQuoteUpdate? update))
                {
                    ordersAdded.Add(update);
                }
                else
                {
                    _logger?.LogError(nameof(TryEncodeAndSend) + "Lookup failed for index: " + index + i);
                }
            }
            nextIndex = index + count;
        }
        else
        {
            nextIndex = index;
        }

        return ordersAdded;
    }
}