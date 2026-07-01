using System;
using System.Threading.Tasks;

namespace ZeroPlus.Interpolator.Client.Interfaces
{
    public interface IInterpolatorClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
