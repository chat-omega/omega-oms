using ZeroPlus.Models.Data.SpiderRock;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ISpreadExchOrderTopic : ITopic
{
    int RequestId { get; set; }
    bool Initialized { get; set; }
    string? Symbol { get; set; }
    void AddModel(SpreadExchOrder update);
}