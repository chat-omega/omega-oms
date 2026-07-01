using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels.Interfaces
{
    public interface ITraderModel
    {
        void OnTrade(OrderTicket order, IOmsOrder trade);
        void OnOrderFilledEvent(OrderTicket changedOrder, OrderStatus orderStatus);
        void OnOrderClosedEvent(IOmsOrder order, OrderStatus orderStatus, OrderTicket ticket);
    }
}
