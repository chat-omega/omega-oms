using ZeroPlus.Interpolator.Client.Config.Interfaces;

namespace ZeroPlus.Interpolator.Client.Config
{
    public class InterpolatorClientConfig : IInterpolatorClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
