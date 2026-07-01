
using System;
using System.Threading.Tasks;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Hercules.Client
{
    public class HerculesClient : IHerculesClient
    {
        public event Action? ClientConnected;
        public event Action? ClientDisconnected;
        public event Action<FirmOrderAndTradeSummary>? FirmOrderAndTradeSummary;
        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            await Task.CompletedTask;
            IsConnected = true;
            // Fire connected after a short delay to simulate connection
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                ClientConnected?.Invoke();
            });
        }

        public async Task DisconnectAsync()
        {
            await Task.CompletedTask;
            IsConnected = false;
            ClientDisconnected?.Invoke();
        }

        public void RegisterClient(string username, string appName, Version version, string hostName)
        {
            Console.WriteLine($"[STUB] HerculesClient.RegisterClient({username}, {appName}, {version}, {hostName})");
        }

        public void SubscribeTransaction(List<string>? accounts, bool fillsOnly, bool ownAndFills)
        {
            Console.WriteLine($"[STUB] HerculesClient.SubscribeTransaction(fillsOnly={fillsOnly})");
        }

        public void SubscribePnl(PositionSubscriptionMode mode)
        {
            Console.WriteLine($"[STUB] HerculesClient.SubscribePnl({mode})");
        }

        public async Task StartAsync()
        {
            await ConnectAsync();
        }

        public async Task StopAsync()
        {
            await DisconnectAsync();
        }
    }
}
