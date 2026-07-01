using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ICrossedImpliedQuoteNotificationTopic : ITopic
{
    void AddUpdate(ImpliedQuoteUpdate update);
}