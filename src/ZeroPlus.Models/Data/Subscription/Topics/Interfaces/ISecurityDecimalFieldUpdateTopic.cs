using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface ISecurityDecimalFieldUpdateTopic : ITopic
    {
        void FieldUpdated(Security security,
                          SubscriptionFieldType fieldType,
                          uint sequence,
                          double update,
                          ulong timestamp,
                          bool jumpDetected);
        void FullDeltaAdjTheoUpdate(Security security,
                                    int id,
                                    SubscriptionFieldType fieldType,
                                    uint updateSequence,
                                    ulong underlyingTimestamp,
                                    ulong snapshotTimestamp,
                                    ulong hanweckTimestamp,
                                    double theo,
                                    double delta,
                                    double gamma,
                                    double vega,
                                    double theta,
                                    double rho,
                                    double implied,
                                    double latestMidPrice,
                                    double snapshotMidPrice,
                                    double deltaAdjustedTheo,
                                    bool jumpDetected);
    }
}