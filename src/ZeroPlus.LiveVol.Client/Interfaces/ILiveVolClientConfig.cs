namespace ZeroPlus.LiveVol.Client.Config.Interfaces
{
    public interface ILiveVolClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}
