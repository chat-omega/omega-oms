using System;
using System.Threading.Tasks;

namespace ZeroPlus.Databento.Client.Interfaces
{
    public interface IDatabentoClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
