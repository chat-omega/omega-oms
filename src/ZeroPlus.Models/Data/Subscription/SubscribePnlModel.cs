using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct SubscribePnlModel
    {
        public readonly int RequestId;
        public readonly DateTime RequestTime;
        public readonly PositionSubscriptionMode PositionSubscription;

        public SubscribePnlModel(int requestId, DateTime requestTime, PositionSubscriptionMode positionSubscription)
        {
            RequestId = requestId;
            RequestTime = requestTime;
            PositionSubscription = positionSubscription;
        }
    }
}
