namespace ZeroPlus.Ema.Client.Config.Interfaces
{
    public interface IEmaClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}
