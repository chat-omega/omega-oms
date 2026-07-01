using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Subscription
{
    public class SubscribeTransactionModel
    {
        public int RequestId { get; }
        public DateTime RequestTime { get; }
        public ulong SequenceNumber { get; }
        public bool FillsOnly { get; }
        public bool AllOwn { get; }
        public List<string> Accounts { get; } = new();

        public SubscribeTransactionModel(int requestId, DateTime requestTime, ulong sequenceNumber, bool fillsOnly, bool allOwn = false)
        {
            RequestId = requestId;
            RequestTime = requestTime;
            SequenceNumber = sequenceNumber;
            FillsOnly = fillsOnly;
            AllOwn = allOwn;
        }
    }
}
