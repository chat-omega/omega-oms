using System;
using System.Threading.Tasks;

namespace ZeroPlus.HubTron.Client.Interfaces
{
    public interface IHubTronClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
