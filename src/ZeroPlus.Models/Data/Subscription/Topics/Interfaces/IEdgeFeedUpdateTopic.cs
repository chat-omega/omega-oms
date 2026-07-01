using System.Collections.Generic;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IEdgeFeedUpdateTopic : ITopic
    {
        int RequestId { get; set; }
        bool Initialized { get; set; }
        void AddModels(HashSet<IEdgeScanFeedModel> orders);
        void AddModel(IEdgeScanFeedModel order);
    }
}