using System;
using System.Threading.Tasks;

namespace ZeroPlus.Ema.Client.Interfaces
{
    public interface IEmaClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
