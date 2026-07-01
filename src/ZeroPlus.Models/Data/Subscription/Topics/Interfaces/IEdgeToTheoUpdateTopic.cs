using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IEdgeToTheoUpdateTopic : ITopic
{
    EdgeToTheoUpdateModel UpdateModel { get; }
    void Updated();
}