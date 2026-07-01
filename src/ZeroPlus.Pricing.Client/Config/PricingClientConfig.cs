using ZeroPlus.Pricing.Client.Config.Interfaces;

namespace ZeroPlus.Pricing.Client.Config
{
    public class PricingClientConfig : IPricingClientConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 9000;
    }
}
