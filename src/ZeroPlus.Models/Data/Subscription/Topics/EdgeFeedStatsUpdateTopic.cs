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

public class EdgeFeedStatsUpdateTopic : IEdgeFeedStatsUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Low;

    private readonly object _indexLock;

    private readonly ILogger<EdgeFeedUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, (IEdgeScanFeedStatisticsSummary, TopicUpdateType)> _indexToModelMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }

    public EdgeFeedStatsUpdateTopic(ILogger<EdgeFeedUpdateTopic> logger)
    {
        _logger = logger;
        _indexToModelMap = new ConcurrentDictionary<ulong, (IEdgeScanFeedStatisticsSummary, TopicUpdateType)>();
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
            (HashSet<IEdgeScanFeedStatisticsSummary>? added, HashSet<IEdgeScanFeedStatisticsSummary>? updated) = GetUpdatesSince(index, out nextIndex);
            if (index == nextIndex || added == null || updated == null || (added.Count == 0 && updated.Count == 0))
            {
                return false;
            }

            foreach (IEdgeScanFeedStatisticsSummary model in added)
            {
                int written = ctx.Encoder.EncodeEdgeScanFeedStatisticsModel(ctx.DirectBuffer, 0, model);
                sender.SendEncoded(encodeContext, written, Compressed);
            }
            foreach (IEdgeScanFeedStatisticsSummary model in updated)
            {
                int written = ctx.Encoder.EncodeEdgeScanFeedStatisticsMiniUpdate(ctx.DirectBuffer, 0, model);
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

    public void AddModel(IEdgeScanFeedStatisticsSummary update)
    {
        lock (_indexLock)
        {
            _indexToModelMap[Index++] = (update, TopicUpdateType.Add);
        }
    }

    public void UpdateModel(IEdgeScanFeedStatisticsSummary update)
    {
        lock (_indexLock)
        {
            _indexToModelMap[Index++] = (update, TopicUpdateType.Update);
        }
    }

    private (HashSet<IEdgeScanFeedStatisticsSummary>? added, HashSet<IEdgeScanFeedStatisticsSummary>? updated) GetUpdatesSince(ulong index, out ulong nextIndex)
    {
        HashSet<IEdgeScanFeedStatisticsSummary> added = new HashSet<IEdgeScanFeedStatisticsSummary>();
        HashSet<IEdgeScanFeedStatisticsSummary> updated = new HashSet<IEdgeScanFeedStatisticsSummary>();

        ulong count = Index - index;
        if (count > 0)
        {
            for (ulong i = 0; i < count; i++)
            {
                if (_indexToModelMap.TryGetValue(index + i, out var update))
                {
                    switch (update.Item2)
                    {
                        case TopicUpdateType.Add:
                            added.Add(update.Item1);
                            break;
                        case TopicUpdateType.Update:
                            updated.Add(update.Item1);
                            break;
                    }
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
            return (null, null);
        }

        return (added, updated);
    }
}