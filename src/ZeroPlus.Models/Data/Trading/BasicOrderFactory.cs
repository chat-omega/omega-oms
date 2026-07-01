using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading
{
    public class BasicOrderFactory : IOrderFactory
    {
        private readonly ISecurityBook? _securityBook;
        private readonly ConcurrentDictionary<string, IOrder> _orderMap = new();

        public BasicOrderFactory()
        {
        }
        public BasicOrderFactory(ISecurityBook securityBook)
        {
            _securityBook = securityBook;
        }
        public virtual void OrderAdded(IOrder model) { }
        public virtual void OrderRemoved(string permId) { }
        public virtual void OrderUpdated(IOrder model) { }
        public virtual void OrderTagUpdated(IOrder model) { }
        public virtual void OrderIndicatorUpdated(IOrder model) { }
        public virtual void EdgeScanUpdate(IEdgeScanFeedModel model) { }
        public virtual void TradeFeedUpdate(int id, List<ITradeFeedModel> model, bool isLast) { }
        public virtual void MultipleOrderAdded(int requestId, ref List<IOrder> orders, int totalQueued, int lastMessageIndex) { }
        public virtual void HandleOrderDetailsUpdate(IOrder? order, string json) { }
        public virtual IEdgeScanFeedModel? GetEdgeScanFeedModel() { return default; }
        public virtual ITradeFeedModel? GetTradeFeedModel() { return default; }
        public virtual IPriceChainModel? GetPriceChainModel() { return default; }
        public virtual IOrder GetOrCreateOrder(bool isComplex, string? orderId, out bool isNew)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                isNew = true;
                return isComplex ? new ComplexOrder(_securityBook) : new Order(_securityBook);
            }

            isNew = !_orderMap.TryGetValue(orderId, out var order);
            if (isNew)
            {
                order = isComplex ? new ComplexOrder(_securityBook) : new Order(_securityBook);
                _orderMap[orderId] = order;
            }

            return order!;
        }

        public List<IOrder> GetAllOrders()
        {
            return _orderMap.Values.ToList();
        }

        public virtual IOrder GetOrder(bool isComplex, string? orderId = null)
        {
            return GetOrCreateOrder(isComplex, orderId, out _);
        }
        public virtual IOrderSlim GetOrderSlim(bool isComplex, string? orderId = null)
        {
            return isComplex ? new ComplexOrderSlim(_securityBook) : new OrderSlim(_securityBook);
        }
        public virtual bool GetExistingOrder(string orderId, out IOrder? order)
        {
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                return _orderMap.TryGetValue(orderId, out order);
            }
            order = null;
            return false;
        }

        public void MultipleContrapartyReportsAdded(DateTime targetDate, List<ContraPartyReportModel> contrapartyReports)
        {
        }
    }
}
