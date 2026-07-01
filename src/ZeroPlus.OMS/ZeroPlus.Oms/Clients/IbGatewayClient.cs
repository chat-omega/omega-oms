using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using ZeroPlus.IbGateway.Client.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Clients;

public class IbGatewayClient : ClientBase
{
    private readonly IOrderUpdateManager _orderUpdateManager;
    public IIbGatewayClient Client { get; private set; }

    public IbGatewayClient(IOrderUpdateManager orderUpdateManager)
    {
        _orderUpdateManager = orderUpdateManager;
    }

    public void Initialize(IIbGatewayClient client)
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

    public async Task<List<Option>> GetSymbolsAsync(string symbol, string secType, string exchange, string currency)
    {
        List<string> symbols = await Client.RequestSymbolsAsync(symbol, secType, exchange, currency);

        if (symbols == null || symbols.Count == 0)
        {
            return null;
        }

        List<Option> results = new List<Option>();
        foreach (var sym in symbols)
        {
            try
            {
                var opt = OptionsHelper.GetOptionFromSymbol(sym);
                results.Add(opt);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetSymbolsAsync));
            }
        }
        return results;
    }

    public void SendOrder(IOrder order, IOrderInfoUpdateHandler handler)
    {
        if (IsConnected)
        {
            _orderUpdateManager.RegisterListener(order.LocalID, handler);
            Client?.SendOrder(order);
        }
    }

    public void CancelOrder(CancelRequest cancelRequest)
    {
        Client?.SendCancelRequest(cancelRequest);
    }

    public void ModifyOrder(ModifyRequest modifyRequest)
    {
        Client?.SendModifyRequest(modifyRequest);
    }
}