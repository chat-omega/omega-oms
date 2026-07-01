using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IServerClockUpdateTopic : ITopic
    {
        DateTime LastUpdate { get; set; }
        TimeFeedType TimeFeedType { get; set; }

        void FieldUpdated(TimeFeedType timeFeedType, DateTime dateTime);
    }
}