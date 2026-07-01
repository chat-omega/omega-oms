using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Pricing.Client.Interfaces;

namespace ZeroPlus.Oms.Clients;

public readonly record struct TryGetIdResult(bool Success, int Index);

public class PricingClient : ClientBase
{
    public IPricingClient Client { get; private set; }

    public void Initialize(IPricingClient client)
    {
        Client = client;
        Client.ClientConnected += OnClient_ClientConnected;
        Client.ClientDisconnected += OnClient_ClientDisconnected;
        Client.SharedMemorySymbolToIndexMapUpdatedEvent += OnSymbolListUpdate;
    }

    private void OnSymbolListUpdate(Dictionary<string, int> map)
    {
        _log.Info(nameof(OnSymbolListUpdate) + " " + map.Count);
    }

    protected override void RegisterClient()
    {
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        Client.AuthenticateClient(Username, AppId, version!, Dns.GetHostName());
    }

    public override bool Start()
    {
        return Client?.ConnectAndStart() ?? false;
    }

    public override void Stop()
    {
        Client?.DisconnectAndStop();
    }

    /// <summary>Resolves symbol to ticker id via client; returns a result struct for use in async methods (no out parameter).</summary>
    private TryGetIdResult TryGetIdFromSymbol(string symbol)
    {
        return Client.TryGetIdFromSymbol(symbol, out var index)
            ? new TryGetIdResult(true, index)
            : new TryGetIdResult(false, 0);
    }

    public async Task<PricingResponseModel> GetFreshPrices(IOrderSlim order)
    {
        try
        {
            var request = new PricingRequestModel();
            if (order.IsComplexOrder && order is IComplexOrderSlim complexOrder)
            {
                foreach (var leg in complexOrder.Legs)
                {
                    if (leg.Side != null && leg.Security != null)
                    {
                        var idResult = TryGetIdFromSymbol(leg.Security.Symbol);
                        if (!idResult.Success)
                            throw new SlimException("Invalid Leg");
                        request.Legs.Add(new PricingRequestLeg()
                        {
                            Ratio = (uint)leg.Ratio,
                            Side = leg.Side.Value,
                            TickerId = idResult.Index,
                        });
                    }
                    else
                    {
                        throw new SlimException("Invalid Leg");
                    }
                }
            }
            else
            {
                if (order.Side != null && order.Security != null)
                {
                    var idResult = TryGetIdFromSymbol(order.Security.Symbol);
                    if (!idResult.Success)
                        throw new SlimException("Invalid Order");
                    request.Legs.Add(new PricingRequestLeg()
                    {
                        Ratio = 1,
                        Side = order.Side.Value,
                        TickerId = idResult.Index,
                    });
                }
                else
                {
                    throw new SlimException("Invalid Order");
                }
            }

            var result = await Client.PriceAsync(request);
            return result;
        }
        catch (SlimException sle)
        {
            _log.Warn(sle, nameof(GetFreshPrices));
            return new PricingResponseModel()
            {
                Bid = double.NaN,
                Ask = double.NaN,
                HwTheo = double.NaN,
                HwAdjTheo = double.NaN,
                HwDelta = double.NaN,
                VolaTheo = double.NaN,
                VolaAdjTheo = double.NaN,
                AdjDaEma = double.NaN,
                AdjVolaEma = double.NaN,
                UnderBid = double.NaN,
                UnderAsk = double.NaN,
            };
        }
    }
}