#nullable enable
using NLog;
using OpenTelemetry.Resources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Helpers;
using ZeroPlus.Telemetry.Client.Helpers;
using ZeroPlus.Telemetry.Client.Interfaces;
using ZeroPlus.Telemetry.Client.Lifecycle;

namespace ZeroPlus.Oms.Services;

public sealed class OmsOrderLifecycleService : IDisposable
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan StaleEntryThreshold = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, OrderLifecycleTimings> _cache = new();
    private readonly ITelemetryClient _telemetryClient;
    private readonly byte _boxId;
    private readonly byte _progId;
    private readonly byte _instanceId;
    private readonly Timer _cleanupTimer;
    private readonly bool _isLocal;
    private bool _disposed;

    public OmsOrderLifecycleService(ITelemetryClient telemetryClient, byte boxId, byte progId, byte instanceId)
    {
        _telemetryClient = telemetryClient;
        _boxId = boxId;
        _progId = progId;
        _instanceId = instanceId;
        _cleanupTimer = new Timer(CleanupStaleEntries, null, StaleEntryThreshold, StaleEntryThreshold);
        _isLocal = TelemetryHelper.IsLocal();
        _log.Info("OmsOrderLifecycleService initialized");
    }

    public void RecordSentToAutoTrader(string localOrderId, long initiatedNanos, long sendNanos)
    {
        if (string.IsNullOrWhiteSpace(localOrderId)) return;
        var timings = new OrderLifecycleTimings();
        if (_isLocal)
        {
            timings.OmsOrderInitiated = initiatedNanos;
            timings.OmsSentToAutoTrader = sendNanos;
        }
        else
        {
            timings.RemOmsOrderInitiated = initiatedNanos;
            timings.RemOmsSentToAutoTrader = sendNanos;
        }
        _cache[localOrderId] = timings;
    }

    public bool Complete(OrderUpdateValues orderUpdateValues)
    {
        if (!_telemetryClient.IsClientConnected)
        {
            return false;
        }

        if ((orderUpdateValues.ParentLocalOrderId == null || !_cache.TryRemove(orderUpdateValues.ParentLocalOrderId, out var timings)) &&
            (orderUpdateValues.LocalOrderId == null || !_cache.TryRemove(orderUpdateValues.LocalOrderId, out timings)))
        {
            return false;
        }

        try
        {
            timings.OrderId = orderUpdateValues.OriginalOrderId;
            KeyValuePair<string, string>[] entries = timings.ToKeyValuePairs();
            long timestamp = EpochNanosTimer.Now();

            _telemetryClient.SendStateSnapshot(_boxId, _progId, _instanceId,
                timings.SnapshotName, timestamp, entries);

            if (orderUpdateValues.LocalOrderId != null && orderUpdateValues.LocalOrderId.StartsWith("ATO"))
            {
                var copy = new KeyValuePair<string, string>[entries.Length];
                Array.Copy(entries, copy, entries.Length);
                copy[0] = new KeyValuePair<string, string>(copy[0].Key, orderUpdateValues.LocalOrderId);
                _telemetryClient.SendStateSnapshot(_boxId, _progId, _instanceId, timings.SnapshotName, timestamp, copy);
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to send OMS order lifecycle snapshot for {0}", orderUpdateValues.OriginalOrderId);
            return false;
        }
    }

    private void CleanupStaleEntries(object? state)
    {
        long thresholdNanos = EpochNanosTimer.Now() - (long)StaleEntryThreshold.TotalMilliseconds * 1_000_000;
        int removed = 0;
        foreach (var kvp in _cache)
        {
            if ((kvp.Value.OmsOrderInitiated > 0 && kvp.Value.OmsOrderInitiated < thresholdNanos) ||
                (kvp.Value.RemOmsOrderInitiated > 0 && kvp.Value.RemOmsOrderInitiated < thresholdNanos))
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            _log.Warn("Removed {0} stale OMS lifecycle entries", removed);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}
