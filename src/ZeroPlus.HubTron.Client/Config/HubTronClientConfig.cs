using ZeroPlus.HubTron.Client.Config.Interfaces;

namespace ZeroPlus.HubTron.Client.Config
{
    public class HubTronClientConfig : IHubTronClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
