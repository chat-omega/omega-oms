using System;
using System.Threading.Tasks;

namespace ZeroPlus.Theos.Client.Interfaces
{
    public interface ITheosClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
