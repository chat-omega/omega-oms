using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ITradeSlimUpdateTopic : ITopic
    {
        string Symbol { get; set; }
        int SymbolIndex { get; set; }

        void AddTrades(HashSet<TradeSlim> trades);
        void AddTrade(TradeSlim trade);
    }
}
