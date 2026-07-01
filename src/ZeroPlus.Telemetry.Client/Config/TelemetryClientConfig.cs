
using ZeroPlus.Telemetry.Client.Config.Interfaces;

namespace ZeroPlus.Telemetry.Client.Config
{
    public class TelemetryClientConfig : ITelemetryClientConfig
    {
        public string Endpoint { get; set; } = "http://localhost:4317";
    }
}
