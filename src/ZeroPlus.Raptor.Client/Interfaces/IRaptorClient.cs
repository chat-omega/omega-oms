using System;
using System.Threading.Tasks;

namespace ZeroPlus.Raptor.Client.Interfaces
{
    public interface IRaptorClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
