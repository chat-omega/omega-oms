using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ITradeUpdateTopic : ITopic
    {
        void TradeUpdated(TradeUpdateModel tradeUpdateModel);
    }
}