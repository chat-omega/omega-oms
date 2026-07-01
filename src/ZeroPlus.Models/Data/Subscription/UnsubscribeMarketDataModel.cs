using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct UnsubscribeMarketDataModel
    {
        public readonly int RequestId;
        public readonly string Symbol;
        public readonly SubscriptionFieldType RequestType;

        public UnsubscribeMarketDataModel(int requestId, SubscriptionFieldType requestType, string symbol)
        {
            RequestId = requestId;
            RequestType = requestType;
            Symbol = symbol;
        }
    }
}
