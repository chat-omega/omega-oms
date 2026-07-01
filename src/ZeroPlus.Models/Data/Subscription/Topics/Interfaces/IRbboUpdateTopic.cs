using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IRbboUpdateTopic : ITopic
{
    public string Symbol { get; set; }
    public int SymbolIndex { get; set; }
    RbboUpdateModel UpdateModel { get; }
    void Updated();
}
