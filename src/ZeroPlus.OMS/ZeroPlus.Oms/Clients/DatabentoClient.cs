using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using ZeroPlus.Databento.Client.Interfaces;
using ZeroPlus.Models.Data.Models.Databento;

namespace ZeroPlus.Oms.Clients;

public class DatabentoClient : ClientBase
{
    public IDatabentoClient Client { get; private set; }

    public void Initialize(IDatabentoClient client)
    {
        Client = client;
        Client.ClientConnected += OnClient_ClientConnected;
        Client.ClientDisconnected += OnClient_ClientDisconnected;
    }

    protected override void RegisterClient()
    {
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        Client.RegisterClient(Username, AppId, version!, Dns.GetHostName());
    }

    public override bool Start()
    {
        return Client?.ConnectAndStart(openSharedMemory: false) ?? false;
    }

    public override void Stop()
    {
        Client?.DisconnectAndStop();
    }

    public async Task<List<MbpTradeModel>> RequestTradesAsync(string symbol, DateTime startTime, DateTime endTime)
    {
        var trades = await Client.RequestTradesAsync(symbol, startTime.ToUniversalTime(), endTime.ToUniversalTime());
        if (trades != null)
        {
            foreach (var trade in trades)
            {
                if(Client.TryGetSymbolFromId((int)trade.InstrumentId, out var instrumentSymbol))
                {
                    trade.Symbol = instrumentSymbol;
                }
            }
        }
        return trades;
    }
}