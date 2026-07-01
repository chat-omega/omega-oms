using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms
{
    public record SubscriptionKey(string Symbol, SubscriptionFieldType Type)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(Symbol, Type) & int.MaxValue;
        }
    }
}
