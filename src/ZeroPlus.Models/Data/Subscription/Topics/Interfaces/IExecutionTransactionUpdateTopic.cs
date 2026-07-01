using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IExecutionTransactionUpdateTopic : ITopic
{
    int RequestId { get; set; }
    bool Initialized { get; set; }
    void AddTransactions(Transaction[] transactions);
}