using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IDerivedValueUpdateTopic : ITopic
    {
        DerivedValueUpdateModelContainer UpdateModel { get; }
        void Updated();
    }
}