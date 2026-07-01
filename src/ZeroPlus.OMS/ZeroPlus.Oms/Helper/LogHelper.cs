using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;
using System.IO;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Helper
{
    internal class LogHelper
    {
        private static AsyncTargetWrapper _asyncTargetWrapper;
        private static LoggingConfiguration _loggingConfiguration;
        private static OmsConfig _config;
        private static string _logFilePath;
        private static LoggingRule _asyncFileRule;

        internal static void SetupLoger(OmsConfig config)
        {
            try
            {
                _config = config;

                _config.ConfigChangedEvent += (OmsConfig c, bool r) => SetLogLevel();
                SetupLogDirectory();
                SetupLogFile();
                SetupOtlpTarget();
                SetLogLevel();

                LogManager.Configuration = _loggingConfiguration;
                NLog.Common.InternalLogger.LogFile = @"c:\temp\console-example-internal.log";
            }
            catch (Exception ex)
            {
                throw new Exception("Error setting up logger.", ex);
            }
        }

        private static void SetupLogDirectory()
        {
            _logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                OmsConfig.SOLUTION_NAME,
                "logs");
            if (!Directory.Exists(_logFilePath))
            {
                Directory.CreateDirectory(_logFilePath);
            }
        }

        private static void SetupLogFile()
        {
            _loggingConfiguration = new LoggingConfiguration();
            FileTarget fileTarget = new()
            {
                FileName = _logFilePath + $"\\ZeroPlus.Oms-" + "${shortdate}-${processid}.log",
                Layout =
                    "${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} [${processid}]-[${threadid}] [${logger}] [${level}] ${message} ${exception:format=tostring}",
                ArchiveEvery = FileArchivePeriod.Day,
            };
            _asyncTargetWrapper = new AsyncTargetWrapper(fileTarget);
            _loggingConfiguration.AddTarget("file", fileTarget);
            var level = LogLevel.FromString(_config.LogLevel);
            _asyncFileRule = new LoggingRule("*", level, _asyncTargetWrapper);
            _loggingConfiguration.LoggingRules.Add(_asyncFileRule);
        }

        private static void SetupOtlpTarget()
        {
            var otlpTarget = new OtlpTarget
            {
                Name = "otlp",
                Endpoint = "http://alloy.telemetry.zp:4317",
                ServiceName = _config.AppId,
                UseHttp = false,
                OnlyIncludeProperties = new System.Collections.Generic.HashSet<string> { "correlationId", "messageId" },
                ScheduledDelayMilliseconds = 1000,
                UseDefaultResources = false,
                IncludeFormattedMessage = true,
            };

            otlpTarget.Attributes.Add(new TargetPropertyWithContext
            {
                Name = "thread.id",
                Layout = "${threadid}"
            });

            otlpTarget.Attributes.Add(new TargetPropertyWithContext
            {
                Name = "host",
                Layout = "${hostname}"
            });

            // Add resources (matching your XML)
            otlpTarget.Resources.Add(new TargetPropertyWithContext
            {
                Name = "process.name",
                Layout = "${processname}"
            });

            otlpTarget.Resources.Add(new TargetPropertyWithContext
            {
                Name = "process.id",
                Layout = "${processid}"
            });

            otlpTarget.Resources.Add(new TargetPropertyWithContext
            {
                Name = "deployment.environment",
                Layout = "DEV"
            });

            _loggingConfiguration.AddTarget(otlpTarget);
            _loggingConfiguration.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, otlpTarget));   
        }

        private static void SetLogLevel()
        {
            var level = LogLevel.FromString(_config.LogLevel);
            _asyncFileRule.DisableLoggingForLevels(LogLevel.Off, LogLevel.Trace);
            _asyncFileRule.EnableLoggingForLevels(LogLevel.Off, level);

            LogManager.ReconfigExistingLoggers();
        }
    }
}
