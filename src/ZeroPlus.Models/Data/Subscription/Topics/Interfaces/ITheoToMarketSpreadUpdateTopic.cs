using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ITheoToMarketSpreadUpdateTopic : ITopic
{
    TheoToMarketSpread UpdateModel { get; }
    void Updated();
}