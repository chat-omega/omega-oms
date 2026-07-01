using System.Collections.Generic;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Oms.Ui.Models
{
    internal interface IOrderArchiveReceiver
    {
        void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex);
        void AddMultiplePortfolios(HashSet<IPortfolio> portfolios);
    }
}