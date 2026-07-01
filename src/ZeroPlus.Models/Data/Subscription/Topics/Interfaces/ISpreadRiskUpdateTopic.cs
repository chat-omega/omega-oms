using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISpreadRiskUpdateTopic : ITopic
    {
        int RequestId { get; set; }

        void Add(ISpreadRiskModel model);
    }
}