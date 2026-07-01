
using System;
using System.Threading.Tasks;

namespace ZeroPlus.EdgeScanner.Client.Interfaces
{
    public interface IEdgeScannerClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
