using System;
using System.Threading.Tasks;

namespace ZeroPlus.IbGateway.Client.Interfaces
{
    public interface IIbGatewayClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
