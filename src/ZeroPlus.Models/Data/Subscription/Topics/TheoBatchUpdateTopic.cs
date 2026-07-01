using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class TheoBatchUpdateTopic : ITheoBatchUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.High;
    private readonly ILogger<TheoBatchUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, TheoBatchUpdate> _updates = new();

    [ThreadStatic]
    private static HashSet<int>? _pooledProcessedSet;
    [ThreadStatic]
    private static HashSet<int>? _pooledProcessedFitSet;
    [ThreadStatic]
    private static Dictionary<int, DateTimeOffset>? _pooledBaseFitLastUpdate;

    private ulong _index;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get => _index; set => _index = value; }

    public TheoBatchUpdateTopic(ILogger<TheoBatchUpdateTopic> logger)
    {
        _logger = logger;
        _index = 0;

        Id = Guid.NewGuid();
        Compressed = false;
        MessagePriority = PRIORITY;
    }

    public void Update(TheoBatchUpdate update)
    {
        var nextIndex = GetNextIndex();
        _updates[nextIndex] = update;
    }

    public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
    {
        try
        {
            var ctx = (SbeEncodeBufferContext)encodeContext;
            ulong lastIndex = _index;
            if (lastIndex == index)
            {
                nextIndex = lastIndex;
                return false;
            }

            bool sent = false;
            var processed = _pooledProcessedSet ??= [];
            var processedFit = _pooledProcessedFitSet ??= [];
            var map = _pooledBaseFitLastUpdate ??= [];

            nextIndex = index;

            while (_updates.TryGetValue(nextIndex + 1, out var update))
            {
                nextIndex++;

                map.TryGetValue(update.UnderIndex, out var lastBaseFitUpdate);
                var utcNow = DateTimeOffset.UtcNow;

                if (update.BaseFitUpdated || (utcNow - lastBaseFitUpdate).TotalSeconds > 10)
                {
                    if (processedFit.Add(update.UnderIndex))
                    {
                        int written = ctx.Encoder.EncodeTheoBatchUpdateMessage(ctx.DirectBuffer, 0, update, utcNow.Ticks);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                    map[update.UnderIndex] = utcNow;
                }
                else
                {
                    if (!processedFit.Contains(update.UnderIndex) && processed.Add(update.UnderIndex))
                    {
                        int written = ctx.Encoder.EncodeAdjTheoBatchUpdateMessage(ctx.DirectBuffer, 0, update, utcNow.Ticks);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                }
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(TryEncodeAndSend));
            nextIndex = index;
            return false;
        }
        finally
        {
            _pooledProcessedSet?.Clear();
            _pooledProcessedFitSet?.Clear();
        }
    }

    private ulong GetNextIndex()
    {
        return Interlocked.Increment(ref _index);
    }
}