
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.AutoTrader.Client.Interfaces
{
    public interface IAutoTraderClient
    {
        event Action? ClientConnected;
        event Action? ClientDisconnected;
        event Action? AccountsAndRoutesInitialized;
        bool IsConnected { get; }
        List<Account> Accounts { get; }
        Task<List<Account>> RequestAccountsAsync();
        Task StartAsync();
        Task StopAsync();
    }
}
