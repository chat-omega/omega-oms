using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Helper
{
    public static class OrderStatusExtensions
    {
        public static OrdStatus ToOrdStatus(this OrderStatus orderStatus)
        {
            return orderStatus switch
            {
                OrderStatus.Filled => OrdStatus.Filled,
                OrderStatus.Canceled => OrdStatus.Canceled,
                OrderStatus.Expired => OrdStatus.Expired,
                OrderStatus.DoneForDay => OrdStatus.DoneForDay,
                OrderStatus.Suspended => OrdStatus.Suspended,
                OrderStatus.New => OrdStatus.New,
                OrderStatus.PartiallyFilled => OrdStatus.PartiallyFilled,
                OrderStatus.Replaced => OrdStatus.Replaced,
                OrderStatus.PendingCancel => OrdStatus.PendingCancel,
                OrderStatus.Stopped => OrdStatus.Stopped,
                OrderStatus.Rejected => OrdStatus.Rejected,
                OrderStatus.PendingNew => OrdStatus.PendingNew,
                OrderStatus.Restated => OrdStatus.Restated,
                OrderStatus.PendingReplace => OrdStatus.PendingReplace,
                _ => OrdStatus.PendingNew
            };
        }
        public static OrderStatus ToOrderStatus(this OrdStatus ordStatus)
        {
            return ordStatus switch
            {
                OrdStatus.Filled => OrderStatus.Filled,
                OrdStatus.Canceled => OrderStatus.Canceled,
                OrdStatus.Expired => OrderStatus.Expired,
                OrdStatus.DoneForDay => OrderStatus.DoneForDay,
                OrdStatus.Suspended => OrderStatus.Suspended,
                OrdStatus.New => OrderStatus.New,
                OrdStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                OrdStatus.Replaced => OrderStatus.Replaced,
                OrdStatus.PendingCancel => OrderStatus.PendingCancel,
                OrdStatus.Stopped => OrderStatus.Stopped,
                OrdStatus.Rejected => OrderStatus.Rejected,
                OrdStatus.PendingNew => OrderStatus.PendingNew,
                OrdStatus.Restated => OrderStatus.Restated,
                OrdStatus.PendingReplace => OrderStatus.PendingReplace,
                _ => OrderStatus.PendingNew
            };
        }
    }
}