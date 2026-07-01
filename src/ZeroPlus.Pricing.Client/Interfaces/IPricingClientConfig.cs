namespace ZeroPlus.Pricing.Client.Config.Interfaces
{
    public interface IPricingClientConfig
    {
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
    }
}
