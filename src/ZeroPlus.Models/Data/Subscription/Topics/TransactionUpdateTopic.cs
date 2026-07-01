using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;
using ZeroPlus.Models.Protocols;
using ZeroPlus.Models.Protocols.Sbe;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics
{
    public sealed class TransactionUpdateTopic : ITransactionUpdateTopic, IDisposable
    {
        private const MessagePriority PRIORITY = MessagePriority.Medium;
        private const int BATCH_UPDATE_SIZE = 65500;

        // Permanent storage (Append-only log)
        private readonly List<TopicEntry> _entries;
        private readonly ReaderWriterLockSlim _lock;

        private readonly ILogger<TransactionUpdateTopic> _logger;
        public Guid Id { get; }
        public bool Compressed { get; }
        public bool Initialized { get; set; }
        public MessagePriority MessagePriority { get; }
        public int RequestId { get; set; }
        public HashSet<ITopicSubscriber>? Subscribers { get; set; }
        public bool OneTimeUse { get; set; }

        public ulong Index
        {
            get
            {
                _lock.EnterReadLock();
                try { return (ulong)_entries.Count; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public TransactionUpdateTopic(ILogger<TransactionUpdateTopic> logger)
        {
            _logger = logger;
            // Pre-allocate based on your 500k requirement
            _entries = new List<TopicEntry>(500000);
            _lock = new ReaderWriterLockSlim();

            Id = Guid.NewGuid();
            Compressed = true;
            MessagePriority = PRIORITY;
        }

        public bool TryEncodeAndSend(ulong index, IMessageSender sender, IEncodeBufferContext encodeContext, out ulong nextIndex)
        {
            var ctx = (SbeEncodeBufferContext)encodeContext;
            // 1. Initialize ThreadStatic buffers (allocates only once per thread lifetime)
            ThreadBuffers.Initialize();

            int startIndex = (int)index;

            _lock.EnterReadLock();
            try
            {
                // Fast path: No new messages
                if (startIndex >= _entries.Count)
                {
                    nextIndex = index;
                    return false;
                }

                // 2. Iterate Log and populate ThreadStatic collections
                // No 'new' allocations here
                var count = _entries.Count - startIndex;
                for (int i = 0; i < count; i++)
                {
                    var entry = _entries[startIndex + i];

                    if (entry.Order != null)
                    {
                        var order = entry.Order;
                        switch (entry.UpdateType)
                        {
                            case TopicUpdateType.Add:
                                ThreadBuffers.AddedOrders!.Add(order);
                                break;
                            case TopicUpdateType.Remove:
                                ThreadBuffers.RemovedOrders!.Add(order);
                                break;
                            case TopicUpdateType.IndicatorUpdate:
                                ThreadBuffers.IndicatorUpdatedOrders!.Add(order);
                                break;
                            case TopicUpdateType.Update:
                                // Deduplication logic using the HashSet
                                if (!ThreadBuffers.AddedOrders!.Contains(order))
                                {
                                    ThreadBuffers.UpdatedOrders!.Add(order);
                                }
                                break;
                            case TopicUpdateType.TagUpdate:
                                if (!ThreadBuffers.AddedOrders!.Contains(order))
                                {
                                    ThreadBuffers.TagUpdatedOrders!.Add(order);
                                }
                                break;
                        }
                    }
                    else if (entry.Report != null)
                    {
                        ThreadBuffers.ContrapartyReports!.Add(entry.Report);
                        ThreadBuffers.ContrapartyReportTargetDate = entry.ReportTimestamp;
                    }
                }

                nextIndex = (ulong)_entries.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            if (OneTimeUse)
            {
                ClearEntriesSafe();
            }

            // 3. Encoding Phase (Encode and send directly)
            try
            {
                bool sent = false;
                var addedOrders = ThreadBuffers.AddedOrders!;

                // --- Added Orders ---
                if (addedOrders.Count > 0)
                {
                    if (addedOrders.Count == 1)
                    {
                        // Enumerable.First() allocates, use Enumerator
                        foreach (var order in addedOrders)
                        {
                            int written = ctx.Encoder.EncodeOrderAdded(ctx.DirectBuffer, 0, order);
                            sender.SendEncoded(encodeContext, written, Compressed);
                            sent = true;
                            break;
                        }
                    }
                    else
                    {
                        // Manual Chunking using Reused Array
                        var chunkBuffer = ThreadBuffers.ChunkBuffer!;
                        int total = addedOrders.Count;
                        int currentChunkCount = 0;
                        int processedCount = 0;

                        foreach (var order in addedOrders)
                        {
                            chunkBuffer[currentChunkCount++] = order;

                            if (currentChunkCount == BATCH_UPDATE_SIZE)
                            {
                                int written = ctx.Encoder.EncodeMultipleOrderAdded(ctx.DirectBuffer, 0, 
                                    RequestId,
                                    chunkBuffer,
                                    currentChunkCount, // Pass the count explicitly
                                    total,
                                    processedCount + currentChunkCount);
                                sender.SendEncoded(encodeContext, written, Compressed);
                                sent = true;

                                currentChunkCount = 0;
                                processedCount += BATCH_UPDATE_SIZE;
                            }
                        }

                        // Final partial chunk
                        if (currentChunkCount > 0)
                        {
                            int written = ctx.Encoder.EncodeMultipleOrderAdded(ctx.DirectBuffer, 0, 
                                RequestId,
                                chunkBuffer,
                                currentChunkCount,
                                total,
                                processedCount + currentChunkCount);
                            sender.SendEncoded(encodeContext, written, Compressed);
                            sent = true;
                        }
                    }
                }

                // --- Updates ---
                if (ThreadBuffers.UpdatedOrders!.Count > 0)
                {
                    foreach (var order in ThreadBuffers.UpdatedOrders)
                    {
                        int written = ctx.Encoder.EncodeOrderUpdate(ctx.DirectBuffer, 0, order);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                }

                // --- Indicators ---
                if (ThreadBuffers.IndicatorUpdatedOrders!.Count > 0)
                {
                    foreach (var order in ThreadBuffers.IndicatorUpdatedOrders)
                    {
                        int written = ctx.Encoder.EncodeOrderIndicatorUpdate(ctx.DirectBuffer, 0, order);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                }

                // --- Tags ---
                if (ThreadBuffers.TagUpdatedOrders!.Count > 0)
                {
                    foreach (var order in ThreadBuffers.TagUpdatedOrders)
                    {
                        int written = ctx.Encoder.EncodeOrderTagUpdate(ctx.DirectBuffer, 0, order);
                        sender.SendEncoded(encodeContext, written, Compressed);
                        sent = true;
                    }
                }

                // --- Removed ---
                if (ThreadBuffers.RemovedOrders!.Count > 0)
                {
                    foreach (var order in ThreadBuffers.RemovedOrders)
                    {
                        if (order.PermID != null)
                        {
                            int written = ctx.Encoder.EncodeOrderRemoved(ctx.DirectBuffer, 0, order.PermID);
                            sender.SendEncoded(encodeContext, written, Compressed);
                            sent = true;
                        }
                    }
                }

                // --- Reports ---
                if (ThreadBuffers.ContrapartyReports!.Count > 0)
                {
                    var reportsList = ThreadBuffers.ContrapartyReports;
                    var reportChunk = ThreadBuffers.ReportChunkBuffer!;
                    int totalReports = reportsList.Count;
                    int currentChunkCount = 0;

                    for (int i = 0; i < totalReports; i++)
                    {
                        reportChunk[currentChunkCount++] = reportsList[i];

                        if (currentChunkCount == BATCH_UPDATE_SIZE || i == totalReports - 1)
                        {
                            int written = ctx.Encoder.EncodeMultipleContrapartyReportsAddedMessage(ctx.DirectBuffer, 0, 
                                ThreadBuffers.ContrapartyReportTargetDate,
                                reportChunk,
                                currentChunkCount);
                            sender.SendEncoded(encodeContext, written, Compressed);
                            sent = true;

                            currentChunkCount = 0; // Reset for next chunk
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
                // CRITICAL: Clear references to prevent memory leaks and prepare for next call
                ThreadBuffers.Clear();
            }
        }

        // --- Write Operations (Same as before) ---
        public void AddMultipleOrders(HashSet<IOrder> orders)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_entries.Capacity < _entries.Count + orders.Count)
                    _entries.Capacity = _entries.Count + orders.Count + 1000;

                foreach (IOrder order in orders)
                    _entries.Add(new TopicEntry(order, TopicUpdateType.Add));
            }
            finally { _lock.ExitWriteLock(); }
        }

        public void AddOrder(IOrder order) => AddEntry(order, TopicUpdateType.Add);
        public void RemoveOrder(IOrder order) => AddEntry(order, TopicUpdateType.Remove);
        public void UpdateOrder(IOrder order) => AddEntry(order, TopicUpdateType.Update);
        public void UpdateOrderTag(IOrder order) => AddEntry(order, TopicUpdateType.TagUpdate);
        public void OrderIndicatorUpdated(IOrder order) => AddEntry(order, TopicUpdateType.IndicatorUpdate);

        private void AddEntry(IOrder order, TopicUpdateType type)
        {
            _lock.EnterWriteLock();
            try { _entries.Add(new TopicEntry(order, type)); }
            finally { _lock.ExitWriteLock(); }
        }

        public void ContrapartyReportsRead(DateTime targetDate, HashSet<ContraPartyReportModel> reports)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var report in reports)
                    _entries.Add(new TopicEntry(report, targetDate));
            }
            finally { _lock.ExitWriteLock(); }
        }

        private void ClearEntriesSafe()
        {
            _lock.EnterWriteLock();
            try { _entries.Clear(); }
            finally { _lock.ExitWriteLock(); }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        // --- Helper Types ---

        private readonly struct TopicEntry
        {
            public readonly IOrder? Order;
            public readonly ContraPartyReportModel? Report;
            public readonly TopicUpdateType UpdateType;
            public readonly DateTime ReportTimestamp;

            public TopicEntry(IOrder order, TopicUpdateType type)
            {
                Order = order;
                UpdateType = type;
                Report = null;
                ReportTimestamp = default;
            }

            public TopicEntry(ContraPartyReportModel report, DateTime timestamp)
            {
                Order = null;
                UpdateType = default;
                Report = report;
                ReportTimestamp = timestamp;
            }
        }

        /// <summary>
        /// Holds reusable collections per thread to prevent allocations.
        /// </summary>
        private static class ThreadBuffers
        {
            [ThreadStatic] public static HashSet<IOrder>? AddedOrders;
            [ThreadStatic] public static HashSet<IOrder>? UpdatedOrders;
            [ThreadStatic] public static HashSet<IOrder>? TagUpdatedOrders;
            [ThreadStatic] public static List<IOrder>? IndicatorUpdatedOrders;
            [ThreadStatic] public static List<IOrder>? RemovedOrders;
            [ThreadStatic] public static List<ContraPartyReportModel>? ContrapartyReports;
            [ThreadStatic] public static IOrder[]? ChunkBuffer; // For batching
            [ThreadStatic] public static DateTime ContrapartyReportTargetDate;
            [ThreadStatic] public static ContraPartyReportModel[]? ReportChunkBuffer;

            public static void Initialize()
            {
                // Only allocate if this is the first time THIS thread is calling the code
                if (AddedOrders == null)
                {
                    AddedOrders = new HashSet<IOrder>();
                    UpdatedOrders = new HashSet<IOrder>();
                    TagUpdatedOrders = new HashSet<IOrder>();
                    IndicatorUpdatedOrders = new List<IOrder>(100);
                    RemovedOrders = new List<IOrder>(100);
                    ContrapartyReports = new List<ContraPartyReportModel>(100);
                    ChunkBuffer = new IOrder[BATCH_UPDATE_SIZE];
                    ReportChunkBuffer = new ContraPartyReportModel[BATCH_UPDATE_SIZE];
                }
            }

            public static void Clear()
            {
                // Clearing is fast (sets Count to 0) and necessary to release object references
                AddedOrders?.Clear();
                UpdatedOrders?.Clear();
                TagUpdatedOrders?.Clear();
                IndicatorUpdatedOrders?.Clear();
                RemovedOrders?.Clear();
                ContrapartyReports?.Clear();

                // Note: We do not need to clear ChunkBuffer array content as we overwrite it, 
                // but if IOrder holds resources, we might want to Array.Clear it to help GC.
                if (ChunkBuffer != null)
                {
                    Array.Clear(ChunkBuffer, 0, ChunkBuffer.Length);
                }
                if (ReportChunkBuffer != null)
                {
                    Array.Clear(ReportChunkBuffer, 0, ReportChunkBuffer.Length);
                }
                ContrapartyReportTargetDate = default;
            }
        }
    }
}