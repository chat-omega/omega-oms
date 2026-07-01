using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISecurityDoubleDecimalFieldUpdateTopic : ITopic
    {
        void Init(int tickerId, SubscriptionFieldType fieldType);
     
        void FieldUpdated(double bidUpdate,
                          double askUpdate,
                          int bidSize,
                          int askSize,
                          double lastPrice,
                          DateTime timestamp,
                          double latencyMs = 0);
    }
}