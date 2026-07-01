using System;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IBarFeedUpdateTopic : ITopic
    {
        void Init(Security security, TimeSpan cacheTime);
        void VolumeUpdated(ulong volume);
        void FieldUpdated(double bidUpdate, double askUpdate, DateTime timestamp, int bidSize, int askSize);
        void SaveBar(TimeSpan start, TimeSpan duration);
        void LoadBar(TimeSpan start, TimeSpan duration);
    }
}