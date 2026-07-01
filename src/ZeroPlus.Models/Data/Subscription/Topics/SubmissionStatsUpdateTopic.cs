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

public class SubmissionStatsUpdateTopic : ISubmissionStatsUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Low;

    private readonly object _indexLock;

    private readonly ILogger<SubmissionStatsUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, SubmissionsSummary?> _indexToModelMap;
    private readonly ConcurrentDictionary<SubmissionsSummary, ulong> _modelToIndexMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }

    public SubmissionStatsUpdateTopic(ILogger<SubmissionStatsUpdateTopic> logger)
    {
        _logger = logger;
        _indexToModelMap = new ConcurrentDictionary<ulong, SubmissionsSummary?>();
        _modelToIndexMap = new ConcurrentDictionary<SubmissionsSummary, ulong>();
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
            var updated = GetUpdatesSince(index, out nextIndex);
            if (index == nextIndex || updated == null || updated.Count == 0)
            {
                return false;
            }

            foreach (var model in updated)
            {
                int written = ctx.Encoder.EncodeSubmissionSummaryUpdateMessage(ctx.DirectBuffer, 0, model);
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

    public void AddUpdate(SubmissionsSummary update)
    {
        lock (_indexLock)
        {
            var index = Index++;
            _indexToModelMap[index] = update;
            _modelToIndexMap[update] = index;
        }
    }

    public void RemoveUpdate(SubmissionsSummary update)
    {
        lock (_indexLock)
        {
            var removed = _modelToIndexMap.TryRemove(update, out var index);
            if (removed)
                _indexToModelMap[index] = null;
        }
    }

    private HashSet<SubmissionsSummary>? GetUpdatesSince(ulong index, out ulong nextIndex)
    {
        HashSet<SubmissionsSummary> updated = new HashSet<SubmissionsSummary>();

        ulong count = Index - index;
        if (count > 0)
        {
            for (ulong i = 0; i < count; i++)
            {
                if (_indexToModelMap.TryGetValue(index + i, out var update))
                {
                    if (update != null)
                        updated.Add(update);
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
            return null;
        }

        return updated;
    }
}