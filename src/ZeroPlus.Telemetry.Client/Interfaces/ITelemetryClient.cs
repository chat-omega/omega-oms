
using System;
using System.Threading.Tasks;

namespace ZeroPlus.Telemetry.Client.Interfaces
{
    public interface ITelemetryClient
    {
        Task StartAsync();
        Task StopAsync();
        bool IsConnected { get; }
    }
}
