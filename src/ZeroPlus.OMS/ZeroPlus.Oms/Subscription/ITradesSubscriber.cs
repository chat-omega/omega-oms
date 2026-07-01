using System.Collections.Generic;
using System.Threading;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Oms.Subscription
{
    public interface ITradesSubscriber
    {
        int RequestId { get; set; }
        CancellationToken CancellationToken { get; set; }
        void QueueTrades(List<OpraDatabaseTradeModel> mdTrades);
        void ProcessQueuedTrades();
    }
}
