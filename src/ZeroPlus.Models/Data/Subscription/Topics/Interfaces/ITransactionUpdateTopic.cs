using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ITransactionUpdateTopic : ITopic
    {
        int RequestId { get; set; }
        bool OneTimeUse { get; set; }

        void AddMultipleOrders(HashSet<IOrder> orders);
        void AddOrder(IOrder order);
        void RemoveOrder(IOrder order);
        void UpdateOrder(IOrder order);
        void UpdateOrderTag(IOrder order);
        void OrderIndicatorUpdated(IOrder order);
        void ContrapartyReportsRead(DateTime targetDate, HashSet<ContraPartyReportModel> reports);
    }
}