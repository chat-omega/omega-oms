using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IFitUpdateTopic : ITopic
{
    UnderFitResult UnderFitResult { get; }
    void Updated();
}