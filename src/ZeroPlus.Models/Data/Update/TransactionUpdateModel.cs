using System;

namespace ZeroPlus.Models.Data.Update
{
    public readonly struct TransactionUpdateModel
    {
        public readonly DateTime UpdateTime;
        public readonly ulong SequenceNumber;

        public TransactionUpdateModel(DateTime updateTime, ulong sequenceNumber)
        {
            UpdateTime = updateTime;
            SequenceNumber = sequenceNumber;
        }
    }
}
