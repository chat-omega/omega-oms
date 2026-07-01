using System;

namespace ZeroPlus.Models.Data.Subscription
{
    public class UnsubscribeTransactionModel
    {
        public int RequestId { get; }
        public DateTime RequestTime { get; }

        public UnsubscribeTransactionModel(int requestId, DateTime requestTime)
        {
            RequestId = requestId;
            RequestTime = requestTime;
        }
    }
}
