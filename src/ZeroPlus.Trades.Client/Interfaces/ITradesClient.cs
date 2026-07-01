
using System;
using System.Threading.Tasks;

namespace ZeroPlus.Trades.Client.Interfaces
{
    public interface ITradesClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
