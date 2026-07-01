using System;
using System.Threading.Tasks;

namespace ZeroPlus.Pricing.Client.Interfaces
{
    public interface IPricingClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
