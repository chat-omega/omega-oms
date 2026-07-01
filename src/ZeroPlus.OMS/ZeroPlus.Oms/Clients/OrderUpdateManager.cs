using System;
using System.Collections.Concurrent;
using ZeroPlus.AutoTrader.Client.Interfaces;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;

namespace ZeroPlus.Oms.Clients;

public class OrderUpdateManager : IOrderUpdateManager
{
    private readonly ConcurrentDictionary<string, IOrderInfoUpdateHandler> _clientIdToHandlerMap = new();

    public void RegisterClient(IAutoTraderClient client)
    {
        if (client == null)
        {
            return;
        }

        client.OrderInfoUpdate += OrderInfoUpdated;
        client.OrderUpdateValue += OrderUpdated;
        client.AutomationStateChanged += OnAutomationStateChange;
        client.OrderUpdate += OnOrderUpdate;
    }

    private void OnAutomationStateChange(string id, bool automationRunning)
    {
        if (_clientIdToHandlerMap.TryGetValue(id, out var client))
        {
            client.AutomationStateChanged(automationRunning);
        }
    }

    private void OnOrderUpdate(OrderUpdateModel orderUpdate)
    {
        if (orderUpdate.ClientOrderId != null && _clientIdToHandlerMap.TryGetValue(orderUpdate.ClientOrderId, out IOrderInfoUpdateHandler handler))
        {
            handler.HandleExecutionReport(orderUpdate, DateTime.Now);
        }
    }

    public void OrderInfoUpdated(OrderInfoUpdate update)
    {
        if (update.ClientOrderId != null && _clientIdToHandlerMap.TryGetValue(update.ClientOrderId, out IOrderInfoUpdateHandler handler))
        {
            handler.OrderInfoUpdated(update);
        }
    }

    public void OrderUpdated(OrderUpdateValues orderUpdate)
    {
        if (orderUpdate.LocalOrderId != null && _clientIdToHandlerMap.TryGetValue(orderUpdate.LocalOrderId, out IOrderInfoUpdateHandler handler))
        {
            handler.OrderUpdated(orderUpdate);
        }
        else if (orderUpdate.ParentLocalOrderId != null && _clientIdToHandlerMap.TryGetValue(orderUpdate.ParentLocalOrderId, out handler))
        {
            handler.OrderUpdated(orderUpdate);
        }
    }

    public void RegisterListener(string orderLocalId, IOrderInfoUpdateHandler handler)
    {
        if (!_clientIdToHandlerMap.TryAdd(orderLocalId, handler))
        {
            throw new SlimException("Order Id Already Used!");
        }
    }
}