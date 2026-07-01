using ZeroPlus.Models.Data.SpiderRock;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ICobFeedUpdateTopic : ITopic
{
    int RequestId { get; set; }
    bool Initialized { get; set; }
    string? Symbol { get; set; }
    void AddModel(ICobData update);
}