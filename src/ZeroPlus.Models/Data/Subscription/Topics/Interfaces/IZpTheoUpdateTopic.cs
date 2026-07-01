namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IZpTheoUpdateTopic : ITopic
{
    void Initialize(int tickerId);
    void Update(ulong sequence, double theoBid, double theoAsk);
}
