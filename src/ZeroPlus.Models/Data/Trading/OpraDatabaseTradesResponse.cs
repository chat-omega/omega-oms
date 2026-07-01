using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Trading
{
    public class OpraDatabaseTradesResponse
    {
        public OpraDatabaseTradesResponse(int requestId, bool isLastMessage, List<OpraDatabaseTradeModel> trades)
        {
            RequestId = requestId;
            IsLastMessage = isLastMessage;
            Trades = trades;
        }

        public OpraDatabaseTradesResponse() { }

        public int RequestId { get; }
        public bool IsLastMessage { get; }
        public List<OpraDatabaseTradeModel> Trades { get; } = [];
    }
}
