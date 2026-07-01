using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISecurityEmaUpdateTopic : ITopic
    {
        void Init(int securityId, SubscriptionFieldType fieldType);
        void FieldUpdated(EmaUpdateModel emaUpdateModel);
        void FieldUpdated(ulong sequence,
                          double lowPeriodEma, double lowPeriodEmaAdj, double lowPeriodEmaUnderlying,
                          double midPeriodEma, double midPeriodEmaAdj, double midPeriodEmaUnderlying,
                          double midPeriodBidEma, double midPeriodBidEmaAdj, double midPeriodAskEma, double midPeriodAskEmaAdj,
                          double highPeriodEma, double highPeriodEmaAdj, double highPeriodEmaUnderlying,
                          ulong quoteTimestampNanos = 0,
                          ulong calculationTimestampNanos = 0,
                          ulong lowPeriodEmaTimestampNanos = 0,
                          ulong midPeriodEmaTimestampNanos = 0,
                          ulong highPeriodEmaTimestampNanos = 0);
    }
}