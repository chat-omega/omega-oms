
using ZeroPlus.Trades.Client.Config.Interfaces;

namespace ZeroPlus.Trades.Client.Config
{
    public class TradesClientConfig : ITradesClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 10000;
    }
}
