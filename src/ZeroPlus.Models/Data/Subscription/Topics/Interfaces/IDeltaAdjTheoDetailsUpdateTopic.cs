using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IDeltaAdjTheoDetailsUpdateTopic : ITopic
    {
        void Initialize(Security security, int id, byte modelId);
        void FieldUpdated(uint sequence,
            double deltaAdjustedTheo,
            double smoothedDeltaAdjustedTheo,
            double underlying,
            bool jumpDetected,
            double secondaryTheo,
            double secondaryTheoAdj,
            double priceMetric,
            double secondaryVol,
            double changeInPremium,
            double secondarySpot,
            double daEma,
            double volaEma);
    }
}