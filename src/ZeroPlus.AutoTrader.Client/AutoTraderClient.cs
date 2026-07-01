using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.AutoTrader.Client
{
    public class AutoTraderClient : IAutoTraderClient
    {
        public event Action? ClientConnected;
        public event Action? ClientDisconnected;
        public event Action? AccountsAndRoutesInitialized;
        public bool IsConnected { get; private set; }
        public List<Account> Accounts { get; private set; } = new()
        {
            new Account { AccountId = "DEMO001", Description = "Demo Account" }
        };

        public async Task StartAsync()
        {
            await Task.CompletedTask;
            IsConnected = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                ClientConnected?.Invoke();
                await Task.Delay(50);
                AccountsAndRoutesInitialized?.Invoke();
            });
        }

        public async Task StopAsync()
        {
            await Task.CompletedTask;
            IsConnected = false;
            ClientDisconnected?.Invoke();
        }

        public async Task<List<Account>> RequestAccountsAsync()
        {
            await Task.CompletedTask;
            return Accounts;
        }
    }
}