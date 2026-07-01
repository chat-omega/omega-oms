using System;
using System.Threading.Tasks;

namespace ZeroPlus.EdgeScanFeedRunner.Client.Interfaces
{
    public interface IEdgeScanFeedRunnerClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
