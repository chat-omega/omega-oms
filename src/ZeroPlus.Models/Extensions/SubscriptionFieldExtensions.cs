using System;
using System.Linq;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Extensions
{
    public static class SubscriptionFieldExtensions
    {
        private static readonly SubscriptionFieldType _esfMin = (SubscriptionFieldType)Enum.GetValues(typeof(EdgeScannerType)).Cast<EdgeScannerType>().Min();
        private static readonly SubscriptionFieldType _esfMax = (SubscriptionFieldType)Enum.GetValues(typeof(EdgeScannerType)).Cast<EdgeScannerType>().Max();

        public static bool IsEdgeScanFeedSubscription(this SubscriptionFieldType subscriptionField)
        {
            return subscriptionField >= _esfMin && subscriptionField <= _esfMax;
        }
    }
}
