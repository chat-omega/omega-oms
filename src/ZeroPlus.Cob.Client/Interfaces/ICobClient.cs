using System;
using System.Threading.Tasks;

namespace ZeroPlus.Cob.Client.Interfaces
{
    public interface ICobClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
