using System;
using System.Threading.Tasks;
using ZeroPlus.Databento.Client.Interfaces;

namespace ZeroPlus.Databento.Client
{
    public class DatabentoClient : IDatabentoClient
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
