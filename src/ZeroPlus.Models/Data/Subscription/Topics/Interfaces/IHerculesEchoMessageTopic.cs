using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface IHerculesEchoMessageTopic : ITopic
{
    void AddModel(HerculesEchoMessageModel echoMessage);
}