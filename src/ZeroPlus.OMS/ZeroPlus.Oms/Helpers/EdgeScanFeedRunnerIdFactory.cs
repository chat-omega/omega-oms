using System;
using System.IO;
using System.Threading;

namespace ZeroPlus.Oms.Helpers;

public static class EdgeScanFeedRunnerIdFactory
{
    private static int _instanceCounter;

    public static string Create() =>
        $"{Environment.MachineName}-{Environment.ProcessId}-{Interlocked.Increment(ref _instanceCounter)}".ToUpperInvariant();

    public static string ToSafeFileName(string runnerId) =>
        string.Join("_", (runnerId ?? "runner").Split(Path.GetInvalidFileNameChars()));
}
