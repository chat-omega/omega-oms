
using System;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Hercules.Client.Interfaces
{
    public interface IHerculesClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        event Action<FirmOrderAndTradeSummary>? FirmOrderAndTradeSummary;

        Task ConnectAsync();
        Task DisconnectAsync();
        void RegisterClient(string username, string appName, Version version, string hostName);
        void SubscribeTransaction(List<string>? accounts, bool fillsOnly, bool ownAndFills);
        void SubscribePnl(PositionSubscriptionMode mode);
        Task StartAsync();
        Task StopAsync();
        bool IsConnected { get; }
    }
}
