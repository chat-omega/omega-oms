
using System;
using System.Threading.Tasks;

namespace ZeroPlus.SymbolMap.Client.Interfaces
{
    public interface ISymbolMapClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        bool IsConnected { get; }
        Task StartAsync();
        Task StopAsync();
    }
}
