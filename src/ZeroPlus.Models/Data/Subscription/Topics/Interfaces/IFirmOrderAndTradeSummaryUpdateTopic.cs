using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IFirmOrderAndTradeSummaryUpdateTopic : ITopic
{
    FirmOrderAndTradeSummary? UpdateModel { get; }
    public void Init(FirmOrderAndTradeSummary model);
    void Updated();
}