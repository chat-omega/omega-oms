using ZeroPlus.AutoTrader.Client.Interfaces;

namespace ZeroPlus.Oms.Clients;

public interface IOrderUpdateManager
{
    void RegisterClient(IAutoTraderClient orderGatewayClient);
    void RegisterListener(string orderLocalId, IOrderInfoUpdateHandler handler);
}