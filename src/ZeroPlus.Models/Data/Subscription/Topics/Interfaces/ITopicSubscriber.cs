using System;
using System.Collections.Concurrent;
using System.Timers;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ITopicSubscriber
{
    bool IsConnected { get; }
    bool PerformanceModeEnabled { get; }
    ConcurrentDictionary<Guid, ITopic> ThrottledTopics { get; }
    Timer PerformanceModeTimer { get; set; }
    void Send(ITopic topic);
}