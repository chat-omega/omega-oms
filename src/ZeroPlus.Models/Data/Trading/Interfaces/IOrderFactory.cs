using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading.Interfaces
{
    public interface IOrderFactory
    {
        List<IOrder> GetAllOrders();
        IOrder? GetOrder(bool isComplex, string? orderId = null);
        IOrder? GetOrCreateOrder(bool isComplex, string? orderId, out bool isNew);
        IOrderSlim? GetOrderSlim(bool isComplex, string orderId);
        bool GetExistingOrder(string orderId, out IOrder? order);
        void OrderAdded(IOrder model);
        void OrderRemoved(string permId);
        void MultipleOrderAdded(int requestId, ref List<IOrder> orders, int totalQueued, int lastMessageIndex);
        void OrderUpdated(IOrder model);
        void OrderTagUpdated(IOrder model);
        void OrderIndicatorUpdated(IOrder model);
        void EdgeScanUpdate(IEdgeScanFeedModel model);
        IEdgeScanFeedModel? GetEdgeScanFeedModel();
        void TradeFeedUpdate(int id, List<ITradeFeedModel> model, bool isLast);
        ITradeFeedModel? GetTradeFeedModel();
        void HandleOrderDetailsUpdate(IOrder? order, string json);
        IPriceChainModel? GetPriceChainModel();
        void MultipleContrapartyReportsAdded(DateTime targetDate, List<ContraPartyReportModel> contrapartyReports);
    }
}
