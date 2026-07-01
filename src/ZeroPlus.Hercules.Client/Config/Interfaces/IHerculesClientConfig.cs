
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Hercules.Client.Config.Interfaces
{
    public interface IHerculesClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
        TransactionSubscriptionMode TransactionSubscriptionMode { get; }
        bool SubscribePositionsOnConnect { get; }
    }
}
