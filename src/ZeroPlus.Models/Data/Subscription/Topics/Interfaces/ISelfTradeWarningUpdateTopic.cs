using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISelfTradeWarningUpdateTopic : ITopic
    {
        int RequestId { get; set; }

        void Add(ISelfTradeModel model);
    }
}