using System.Collections.Generic;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Subscription
{
    public interface IOrderUpdateSubscriber
    {
        void AddUpdatedOrder(OmsOrder order);
        void AddMultipleUpdatedOrders(IEnumerable<OmsOrder> orders);
    }
}