using Microsoft.Diagnostics.NETCore.Client;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.Profiling
{
    public class Profiler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private EventPipeSession _session;
        private Task _tracingTask;
        private readonly int _processId = Process.GetCurrentProcess().Id;

        public string OutputFilePath { get; private set; }

        public Task StartTracing(int duration = 0)
        {
            if (_session != null)
            {
                return _tracingTask;
            }

            OutputFilePath = GetExportPath();
            SetupSession();
            _tracingTask = Task.Run(() => SaveTrace(OutputFilePath));

            _log.Info($"Tracing started. Output: {OutputFilePath}");
            if (duration > 0)
            {
                Task.Delay(duration).ContinueWith(_ => StopTracing());
            }

            return _tracingTask;
        }


        public async Task StopTracing()
        {
            if (_session == null)
            {
                return;
            }

            _session.Stop();

            await _tracingTask;

            _session.Dispose();
            _session = null;

            _log.Info($"Tracing done.");
        }

        private string GetExportPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                OmsConfig.SOLUTION_NAME,
                "logs",
                $"TraceReport_{Dns.GetHostName()}_{_processId}_{DateTime.Now:_yyyyMMdd_HHmmssfff}.nettrace");
        }

        private void SetupSession()
        {
            var providers = new List<EventPipeProvider>()
            {
                new(
                    "Microsoft-DotNETCore-SampleProfiler",
                    EventLevel.Verbose,
                    0 // Keywords (0 means all keywords for this provider)
                ),
                new(
                    "Microsoft-Windows-DotNETRuntime",
                    EventLevel.Verbose,
                    0x4c14fccbd // A common set of runtime keywords
                ),
                new(
                    "Microsoft-DotNet-Core-Runtime",
                    EventLevel.Verbose,
                    0x49 // Keywords (0 means all keywords for this provider)
                ),
                new(
                    "System.Threading.Tasks.TplEventSource",
                    EventLevel.Verbose,
                    0x1 // A common set of runtime keywords
                )
            };

            var client = new DiagnosticsClient(_processId);
            _session = client.StartEventPipeSession(providers);
        }

        private void SaveTrace(string outputFilePath)
        {
            try
            {
                using var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
                _session.EventStream.CopyTo(stream);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartTracing));
            }
        }

        public void GenerateMemoryDump()
        {
            var client = new DiagnosticsClient(_processId);
            client.WriteDump(DumpType.Full, GetExportPath().Replace("nettrace", "dmp"));
        }
    }
}
