using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Clients;

public interface IOmsOrderUpdateSubscriber : IOrderInfoUpdateHandler
{
    OrderSubType? SubType { get; set; }
    void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject);
}