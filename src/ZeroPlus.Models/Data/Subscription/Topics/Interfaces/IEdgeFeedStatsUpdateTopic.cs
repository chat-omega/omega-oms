using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IEdgeFeedStatsUpdateTopic : ITopic
{
    void AddModel(IEdgeScanFeedStatisticsSummary model);
    void UpdateModel(IEdgeScanFeedStatisticsSummary model);
}

public interface ISubmissionStatsUpdateTopic : ITopic
{
    void AddUpdate(SubmissionsSummary model);
    void RemoveUpdate(SubmissionsSummary model);
}