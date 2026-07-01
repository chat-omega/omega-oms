using ZeroPlus.IbGateway.Client.Config.Interfaces;

namespace ZeroPlus.IbGateway.Client.Config
{
    public class IbGatewayClientConfig : IIbGatewayClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
