using System;

namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct UnsubscribePnlModel
    {
        public readonly int RequestId;
        public readonly DateTime RequestTime;

        public UnsubscribePnlModel(int requestId, DateTime requestTime)
        {
            RequestId = requestId;
            RequestTime = requestTime;
        }
    }
}
