using System.Collections.Generic;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IOrderInfoUpdateTopic : ITopic
    {
        int RequestId { get; set; }

        void AddMultipleOrders(HashSet<OrderInfoUpdate> orders);
        void AddUpdate(OrderInfoUpdate order);
    }
}