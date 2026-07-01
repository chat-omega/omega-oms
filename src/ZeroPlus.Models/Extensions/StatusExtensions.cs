using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Extensions
{
    public static class StatusExtensions
    {
        public static bool IsFilled(this OrderStatus status)
        {
            return status == OrderStatus.Filled ||
                   status == OrderStatus.PartiallyFilled;
        }

        public static bool IsClosed(this OrderStatus status)
        {
            return status == OrderStatus.Filled ||
                   status == OrderStatus.Rejected ||
                   status == OrderStatus.Canceled ||
                   status == OrderStatus.DoneForDay;
        }

        public static bool IsFilled(this ExecutionType type)
        {
            return type == ExecutionType.Filled ||
                   type == ExecutionType.PartiallyFilled ||
                   type == ExecutionType.Trade;
        }
    }
}
