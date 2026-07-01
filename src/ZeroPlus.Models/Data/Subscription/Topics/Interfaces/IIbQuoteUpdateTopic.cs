using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IIbQuoteUpdateTopic : ITopic
    {
        IbQuoteUpdateModel UpdateModel { get; }
        void Updated();
    }
}