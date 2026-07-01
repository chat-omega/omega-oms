
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Hercules.Client.Config
{
    public class HerculesClientConfig : IHerculesClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
        public TransactionSubscriptionMode TransactionSubscriptionMode { get; set; } = TransactionSubscriptionMode.Off;
        public bool SubscribePositionsOnConnect { get; set; } = true;
    }
}
