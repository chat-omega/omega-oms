using System.Collections.Generic;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ITradeFeedUpdateTopic : ITopic
    {
        int RequestId { get; set; }

        void AddModels(HashSet<ITradeFeedModel> orders);
        void AddModel(ITradeFeedModel order);
    }
}