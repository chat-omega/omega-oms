
using System;
using System.Collections.Generic;
using ZeroPlus.Telemetry.Client.Config.Interfaces;

namespace ZeroPlus.Telemetry.Client.Config
{
    public class TelemetryClientConfigParser : ITelemetryClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "telemetry.config.json" };
        public ITelemetryClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] TelemetryClientConfigParser.Parse({configPath})");
            return new TelemetryClientConfig();
        }
    }
}
