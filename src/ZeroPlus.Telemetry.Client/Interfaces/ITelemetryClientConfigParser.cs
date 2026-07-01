
using System.Collections.Generic;

namespace ZeroPlus.Telemetry.Client.Config.Interfaces
{
    public interface ITelemetryClientConfigParser
    {
        List<string> GetSavedConfigsList();
        ITelemetryClientConfig Parse(string configPath);
    }
}
