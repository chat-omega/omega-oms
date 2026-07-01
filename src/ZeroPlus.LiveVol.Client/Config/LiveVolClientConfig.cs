using ZeroPlus.LiveVol.Client.Config.Interfaces;

namespace ZeroPlus.LiveVol.Client.Config
{
    public class LiveVolClientConfig : ILiveVolClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
