using System.Collections.Generic;
using ZeroPlus.Models.Data.Update.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISymbolStatUpdateTopic : ITopic
    {
        int RequestId { get; set; }

        void Add(ISymbolStatModel order);
        void Update(ISymbolStatModel order);
        void AddMultiple(HashSet<ISymbolStatModel> orders);
    }
}