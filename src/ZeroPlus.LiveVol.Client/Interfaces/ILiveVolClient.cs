using System;
using System.Threading.Tasks;

namespace ZeroPlus.LiveVol.Client.Interfaces
{
    public interface ILiveVolClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
