namespace ZeroPlus.HubTron.Client.Config.Interfaces
{
    public interface IHubTronClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}
