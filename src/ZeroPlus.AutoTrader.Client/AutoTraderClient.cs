using System;
using System.Threading.Tasks;
using ZeroPlus.AutoTrader.Client.Interfaces;

namespace ZeroPlus.AutoTrader.Client
{
    public class AutoTraderClient : IAutoTraderClient
    {
        public event Action? ClientConnected;
        public event Action? ClientDisconnected;
        public bool IsConnected { get; private set; }

        public async Task StartAsync()
        {
            await Task.CompletedTask;
            IsConnected = true;
            _ = Task.Run(async () => { await Task.Delay(50); ClientConnected?.Invoke(); });
        }

        public async Task StopAsync()
        {
            await Task.CompletedTask;
            IsConnected = false;
            ClientDisconnected?.Invoke();
        }
    }
}
