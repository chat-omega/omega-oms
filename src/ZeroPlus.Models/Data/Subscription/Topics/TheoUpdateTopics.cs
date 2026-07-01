using ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics;

public class TheoUpdateTopics
{
    public IDeltaAdjTheoDetailsUpdateTopic SlimTheoUpdateTopic { get; }
    public IGreekUpdateTopic GreekUpdateTopic { get; set; }
    public IGreekUpdateTopic VolaGreekUpdateTopic { get; set; }

    public TheoUpdateTopics(IDeltaAdjTheoDetailsUpdateTopic slimTheoUpdateTopic, IGreekUpdateTopic greekUpdateTopic, IGreekUpdateTopic volaGreekUpdateTopic)
    {
        SlimTheoUpdateTopic = slimTheoUpdateTopic;
        GreekUpdateTopic = greekUpdateTopic;
        VolaGreekUpdateTopic = volaGreekUpdateTopic;
    }
}