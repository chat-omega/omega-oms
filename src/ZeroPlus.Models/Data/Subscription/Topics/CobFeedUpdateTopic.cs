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

public class CobFeedUpdateTopic : ICobFeedUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Medium;

    private readonly object _indexLock;

    private readonly ILogger<CobFeedUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, ICobData> _indexToModelMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }
    public string? Symbol { get; set; }

    public CobFeedUpdateTopic(ILogger<CobFeedUpdateTopic> logger)
    {
        _logger = logger;
        _indexToModelMap = new ConcurrentDictionary<ulong, ICobData>();
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
            var updates = GetUpdatesSince(index, out nextIndex);
            if (updates == null || index == nextIndex || updates.Count == 0)
            {
                return false;
            }

            foreach (var update in updates)
            {
                int written;
                switch (update.DataType)
                {
                    case CobDataType.Quote:
                        written = ctx.Encoder.EncodeSpreadBookQuoteMessage(ctx.DirectBuffer, 0, (SpreadBookQuote)update);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        break;
                    case CobDataType.Order:
                        written = ctx.Encoder.EncodeSpreadExchOrderMessage(ctx.DirectBuffer, 0, (SpreadExchOrder)update);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        break;
                    case CobDataType.Print:
                        written = ctx.Encoder.EncodeSpreadPrintMessage(ctx.DirectBuffer, 0, (SpreadPrint)update);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        break;
                    case CobDataType.Auction:
                        written = ctx.Encoder.EncodeAuctionPrintMessage(ctx.DirectBuffer, 0, (AuctionPrint)update);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        break;
                    default:
                        _logger.LogError("Unknown COB data type: {DataType}", update.DataType);
                        nextIndex = index;
                        return false;
                }
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

    public void AddModel(ICobData update)
    {
        lock (_indexLock)
        {
            _indexToModelMap[Index] = update;
            Index++;
        }
    }

    private List<ICobData>? GetUpdatesSince(ulong index, out ulong nextIndex)
    {
        List<ICobData>? newUpdates = null;

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
                    newUpdates ??= new List<ICobData>((int)count);
                    newUpdates.Add(update);
                }
                else
                {
                    _logger?.LogError(nameof(GetUpdatesSince) + "Lookup failed for index: " + index + i);
                }
            }
            nextIndex = index + count;
        }
        else
        {
            nextIndex = index;
        }

        return newUpdates;
    }
}