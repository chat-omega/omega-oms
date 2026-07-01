using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class ExecutionTransactionUpdateTopic : IExecutionTransactionUpdateTopic
{
    private const MessagePriority PRIORITY = MessagePriority.Medium;

    private readonly object _indexLock;

    private readonly ILogger<ExecutionTransactionUpdateTopic> _logger;
    private readonly ConcurrentDictionary<ulong, Transaction> _indexToUpdateMap;

    public Guid Id { get; }
    public bool Compressed { get; }
    public bool Initialized { get; set; }
    public MessagePriority MessagePriority { get; }
    public int RequestId { get; set; }
    public HashSet<ITopicSubscriber>? Subscribers { get; set; }
    public ulong Index { get; set; }

    public ExecutionTransactionUpdateTopic(ILogger<ExecutionTransactionUpdateTopic> logger)
    {
        _logger = logger;
        _indexToUpdateMap = new ConcurrentDictionary<ulong, Transaction>();
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
            HashSet<Transaction> updates = GetUpdatesSince(index, out nextIndex);
            if (index == nextIndex || updates.Count == 0)
            {
                return false;
            }

            foreach (Transaction updatedOrder in updates)
            {
                int written = ctx.Encoder.EncodeTransactionMessage(ctx.DirectBuffer, 0, updatedOrder);
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

    public void AddTransactions(Transaction[] transactions)
    {
        lock (_indexLock)
        {
            foreach (var transaction in transactions)
            {
                _indexToUpdateMap[Index] = transaction;
                Index++;
            }
        }
    }

    private HashSet<Transaction> GetUpdatesSince(ulong index, out ulong nextIndex)
    {
        HashSet<Transaction> ordersAdded = new HashSet<Transaction>();

        ulong count;
        lock (_indexLock)
        {
            count = Index - index;
        }
        if (count > 0)
        {
            for (ulong i = 0; i < count; i++)
            {
                if (_indexToUpdateMap.TryGetValue(index + i, out var update))
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