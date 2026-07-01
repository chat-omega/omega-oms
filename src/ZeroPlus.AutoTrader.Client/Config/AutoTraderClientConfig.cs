
using ZeroPlus.AutoTrader.Client.Config.Interfaces;

namespace ZeroPlus.AutoTrader.Client.Config
{
    public class AutoTraderClientConfig : IAutoTraderClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9100;
    }
}
