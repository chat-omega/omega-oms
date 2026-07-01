using System;
using System.Threading.Tasks;
using ZeroPlus.HubTron.Client.Interfaces;

namespace ZeroPlus.HubTron.Client
{
    public class HubTronClient : IHubTronClient
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
