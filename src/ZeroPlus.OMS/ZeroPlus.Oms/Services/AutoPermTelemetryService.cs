using System;
using System.Collections.Concurrent;
using System.Threading;
using NLog;
using ZeroPlus.Telemetry.Client.Helpers;
using ZeroPlus.Telemetry.Client.Interfaces;

namespace ZeroPlus.Oms.Services;

public sealed class AutoPermTelemetryService : IDisposable
{
    private const string SnapshotName = "AutoPermCycle";
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan StaleEntryThreshold = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, AutoPermTelemetryCollector> _cache = new();
    private readonly ITelemetryClient _telemetryClient;
    private readonly byte _boxId;
    private readonly byte _progId;
    private readonly byte _instanceId;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public AutoPermTelemetryService(ITelemetryClient telemetryClient, byte boxId, byte progId, byte instanceId)
    {
        _telemetryClient = telemetryClient;
        _boxId = boxId;
        _progId = progId;
        _instanceId = instanceId;
        _cleanupTimer = new Timer(CleanupStaleEntries, null, StaleEntryThreshold, StaleEntryThreshold);
    }

    public void Begin(string orderId, string spreadId, string basketId, double lastEdge, int generation)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return;

        var collector = new AutoPermTelemetryCollector();
        collector.TimestampNanos = EpochNanosTimer.Now();
        collector.Set("OrderId", orderId);
        collector.Set("SpreadId", spreadId ?? "");
        collector.Set("BasketId", basketId ?? "");
        collector.Set("LastEdge", lastEdge);
        collector.Set("Generation", generation);

        _cache[orderId] = collector;
    }

    public void RecordConfigSelection(string orderId, string configResult, string selectionMode, double configEdge)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.Set("ConfigResult", configResult);
        collector.Set("SelectionMode", selectionMode);
        collector.Set("ConfigEdge", configEdge);
    }

    public void RecordSortMethod(string orderId, string sortMethod)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.Set("SortMethod", sortMethod);
    }

    public void RecordPermsLoaded(string orderId, int loadedCount, int filteredCount, string permSpreadIds)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.Set("PermsLoadedCount", loadedCount);
        collector.Set("FilteredCount", filteredCount);
        collector.Set("PermSpreadIds", permSpreadIds);
    }

    public void RecordFilterRejection(string orderId, string reason)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.IncrementRejection(reason);
    }

    public void RecordQueueStart(string orderId, double startingEdge, double minEdge, double permMinEdge,
        double targetEdge, bool useBasketEdge, bool matchTargetEdge, double backupEdge, double edgePerContract, int permCount)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.Set("StartingEdge", startingEdge);
        collector.Set("MinEdge", minEdge);
        collector.Set("PermMinEdge", permMinEdge);
        collector.Set("TargetEdge", targetEdge);
        collector.Set("UseBasketEdge", useBasketEdge);
        collector.Set("MatchTargetEdge", matchTargetEdge);
        collector.Set("BackupEdge", backupEdge);
        collector.Set("EdgePerContract", edgePerContract);
        collector.Set("PermCount", permCount);
    }

    public void RecordAttempt(string orderId, string spreadId, double edgeInc, double price, double bid,
        double ask, double theo, double adjTheo, string status, bool filled, double elapsedMs, string riskFail)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.RecordAttempt(spreadId, edgeInc, price, bid, ask, theo, adjTheo, status, filled, elapsedMs, riskFail);
    }

    public void RecordRiskCheckFailure(string orderId)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.IncrementRiskCheckFailure();
    }

    public void RecordMarketCrossFailure(string orderId)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.IncrementMarketCrossFailure();
    }

    public void RecordEmaLoadFailure(string orderId)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.IncrementEmaLoadFailure();
    }

    public void RecordCompletion(string orderId, bool gotFill, string filledSide, bool stoppedOnFill, bool maxResubReached)
    {
        if (!_cache.TryGetValue(orderId, out var collector)) return;
        collector.Set("GotFill", gotFill);
        collector.Set("FilledSide", filledSide ?? "");
        collector.Set("StoppedOnFill", stoppedOnFill);
        collector.Set("MaxResubReached", maxResubReached);
    }

    public void Complete(string orderId)
    {
        if (orderId == null || !_cache.TryRemove(orderId, out var collector))
            return;

        if (!_telemetryClient.IsClientConnected)
            return;

        try
        {
            var entries = collector.ToKeyValuePairs();
            _telemetryClient.SendStateSnapshot(_boxId, _progId, _instanceId,
                SnapshotName, collector.TimestampNanos, entries);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send AutoPermCycle snapshot for {0}", orderId);
        }
    }

    private void CleanupStaleEntries(object state)
    {
        long thresholdNanos = EpochNanosTimer.Now() - (long)StaleEntryThreshold.TotalMilliseconds * 1_000_000;
        int removed = 0;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.TimestampNanos < thresholdNanos)
            {
                if (_cache.TryRemove(kvp.Key, out _))
                    removed++;
            }
        }
        if (removed > 0)
        {
            _log.Warn("Removed {0} stale AutoPermCycle entries", removed);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}
