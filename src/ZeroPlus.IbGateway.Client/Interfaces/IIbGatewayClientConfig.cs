namespace ZeroPlus.IbGateway.Client.Config.Interfaces
{
    public interface IIbGatewayClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}
