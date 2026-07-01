using System;
using System.Threading.Tasks;
using ZeroPlus.LiveVol.Client.Interfaces;

namespace ZeroPlus.LiveVol.Client
{
    public class LiveVolClient : ILiveVolClient
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
