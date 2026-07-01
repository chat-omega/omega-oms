using ZeroPlus.Ema.Client.Config.Interfaces;

namespace ZeroPlus.Ema.Client.Config
{
    public class EmaClientConfig : IEmaClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
