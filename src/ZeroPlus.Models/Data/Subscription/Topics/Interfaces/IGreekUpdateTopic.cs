using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IGreekUpdateTopic : ITopic
{
    public int SymbolIndex { get; set; }
    IGreekUpdate? UpdateModel { get; set; }
    void Updated();
}