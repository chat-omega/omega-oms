using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISecuritySingleFieldUpdateTopic : ITopic
    {
        void Init(int tickerId, SubscriptionFieldType fieldType);
        void FieldUpdated(double value);
    }
}
